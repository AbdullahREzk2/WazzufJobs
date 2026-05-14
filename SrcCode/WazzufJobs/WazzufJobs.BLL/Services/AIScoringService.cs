using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Mscc.GenerativeAI;
using WazzufJobs.BLL.Helpers;
using WazzufJobs.BLL.Hubs;
using WazzufJobs.BLL.Settings;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.BLL.Services;

public class AIScoringService(
    ApplicationDBContext context,
    ICVTextExtractor cvTextExtractor,
    IOptions<AISettings> aiSettings,
    IEmailSender emailSender,
    IHubContext<ScoringHub> hubContext,
    ILogger<AIScoringService> logger) : IAIScoringService
{
    private readonly ApplicationDBContext _context = context;
    private readonly ICVTextExtractor _cvTextExtractor = cvTextExtractor;
    private readonly AISettings _aiSettings = aiSettings.Value;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly IHubContext<ScoringHub> _hubContext = hubContext;
    private readonly ILogger _logger = logger;

    public async Task ScoreApplicationAsync(int applicationId,CancellationToken cancellationToken)
    {
        _logger.LogInformation("AI Scoring: Starting for application {Id}.", applicationId);

        var application = await _context.Applications
            .Include(a => a.User).ThenInclude(u => u.CV)
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application is null)
        {
            _logger.LogWarning("AI Scoring: Application {Id} not found.", applicationId);
            return;
        }

        _logger.LogInformation("AI Scoring: Found application for user {UserId}, job {JobId}.",
            application.UserId, application.JobId);

        if (application.User.CV is null)
        {
            _logger.LogWarning("AI Scoring: No CV found for user {UserId}.", application.UserId);
            return;
        }

        _logger.LogInformation("AI Scoring: CV found at {Url}.", application.User.CV.Url);

        // extract CV text
        var cvText = await _cvTextExtractor.ExtractTextAsync(
            application.User.CV.Url, cancellationToken);

        _logger.LogInformation("AI Scoring: Extracted CV text length = {Length}.",
            cvText?.Length ?? 0);

        if (string.IsNullOrWhiteSpace(cvText))
        {
            _logger.LogWarning("AI Scoring: CV text is empty for application {Id}. " +
                "PDF may be image-based or unreadable.", applicationId);

            // still mark as scored with 0 so it doesn't keep retrying
            application.AIScore = 0;
            application.AIFeedback = "Could not extract text from CV. Please ensure your CV is a text-based PDF.";
            application.IsAIScored = true;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            _logger.LogInformation("AI Scoring: Calling Gemini for application {Id}.", applicationId);

            var scoringPrompt = BuildScoringPrompt(cvText, application.Job);

            var googleAI = new GoogleAI(apiKey: _aiSettings.ApiKey);
            var model = googleAI.GenerativeModel(
                model: _aiSettings.Model); 
            var response = await model.GenerateContent(scoringPrompt);
            var text = response?.Text ?? string.Empty;

            _logger.LogInformation("AI Scoring: Gemini raw response = {Response}.", text);

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("AI Scoring: Gemini returned empty response for application {Id}.", applicationId);
                return;
            }

            var (score, feedback) = ParseAIResponse(text);

            _logger.LogInformation("AI Scoring: Parsed score={Score}, feedback={Feedback}.",
                score, feedback);

            application.AIScore = score;
            application.AIFeedback = feedback;
            application.IsAIScored = true;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "AI Scoring: Application {Id} scored {Score}/100.", applicationId, score);

            // notify via SignalR
            await _hubContext.Clients
                .User(application.UserId)
                .SendAsync("ApplicationScored", new
                {
                    applicationId = application.Id,
                    jobTitle = application.Job.Title,
                    score,
                    feedback
                }, cancellationToken);

            // send email
            await SendScoreEmailAsync(application, score, feedback, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AI Scoring: Exception for application {Id}. Message: {Message}",
                applicationId, ex.Message);

            throw;
        }
    }
    private async Task SendScoreEmailAsync(DAL.Entities.Application application,float score,string feedback,CancellationToken cancellationToken)
    {
        var emailBody = EmailBodyBuilder.GenerateEmailBody("ApplicationScore",
            new Dictionary<string, string>
            {
                { "{name}",      application.User.FirstName },
                { "{jobTitle}",  application.Job.Title      },
                { "{score}",     score.ToString("F1")       },
                { "{feedback}",  feedback                   },
                { "{status}",    GetScoreStatus(score)      }
            });

        await _emailSender.SendEmailAsync(
            application.User.Email!,
            $"🎯 Your Application Score for {application.Job.Title}",
            emailBody);
    }

    private static string GetScoreStatus(float score) => score switch
    {
        >= 80 => "Excellent match! 🌟",
        >= 60 => "Good match! ✅",
        >= 40 => "Fair match 📊",
        _ => "Low match ⚠️"
    };

    private static string BuildScoringPrompt(string cvText, DAL.Entities.Job job) =>
        $"""
        You are an expert HR recruiter. Analyze the match between this CV and job posting.

        ## Job Details
        Title: {job.Title}
        Location: {job.Location}
        Description: {job.Description}
        Required Skills: {string.Join(", ", job.Skills)}
        Job Type: {job.JobType}
        Workplace: {job.WorkplaceType}

        ## Candidate CV
        {cvText}

        ## Instructions
        Respond in this EXACT format only:

        SCORE: [number between 0 and 100]
        FEEDBACK: [2-3 sentences explaining the score]

        Base the score on:
        - Skills match (40%)
        - Experience relevance (30%)
        - Education fit (20%)
        - Location/work type compatibility (10%)
        """;

    private static (float Score, string Feedback) ParseAIResponse(string response)
    {
        float score = 0;
        var feedback = string.Empty;

        foreach (var line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("SCORE:", StringComparison.OrdinalIgnoreCase))
            {
                var raw = line.Replace("SCORE:", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (float.TryParse(raw, out var parsed))
                    score = Math.Clamp(parsed, 0, 100);
            }
            else if (line.StartsWith("FEEDBACK:", StringComparison.OrdinalIgnoreCase))
            {
                feedback = line.Replace("FEEDBACK:", "", StringComparison.OrdinalIgnoreCase).Trim();
            }
        }

        return (score, feedback);
    }
}