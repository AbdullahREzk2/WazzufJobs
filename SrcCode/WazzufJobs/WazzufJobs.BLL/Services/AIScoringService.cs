// WazzufJobs.BLL/Services/AIScoringService.cs
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Helpers;
using WazzufJobs.BLL.Hubs;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.BLL.Services;

public class AIScoringService(
    ApplicationDBContext context,
    IAIClient aiClient,
    IEmailSender emailSender,
    IHubContext<ScoringHub> hubContext,
    ILogger<AIScoringService> logger) : IAIScoringService
{
    private readonly ApplicationDBContext _context = context;
    private readonly IAIClient _aiClient = aiClient;
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

        if (application.User.CV is null)
        {
            _logger.LogWarning("AI Scoring: No CV for user {UserId}.", application.UserId);
            return;
        }

        var cvText = application.User.CV.ExtractedText;

        _logger.LogInformation("AI Scoring: CV text length = {Length}.", cvText?.Length ?? 0);

        if (string.IsNullOrWhiteSpace(cvText))
        {
            _logger.LogWarning("AI Scoring: No extracted text for application {Id}.", applicationId);

            application.AIScore = 0;
            application.AIFeedback = "CV text could not be extracted. Please re-upload your CV.";
            application.IsAIScored = true;

            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var scoringPrompt = BuildScoringPrompt(cvText, application.Job);

            _logger.LogInformation("AI Scoring: Calling Groq for application {Id}.", applicationId);

            var responseText = await _aiClient.GenerateAsync(scoringPrompt, cancellationToken);

            _logger.LogInformation("AI Scoring: Raw response = {Response}.", responseText);

            var (score, feedback) = ParseAIResponse(responseText);

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

            // send score email
            await SendScoreEmailAsync(application, score, feedback, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AI Scoring: Exception for application {Id}: {Message}",
                applicationId, ex.Message);
            throw;
        }
    }

    private async Task SendScoreEmailAsync(DAL.Entities.Application application,float score,string feedback,CancellationToken cancellationToken)
    {
        try
        {
            var emailBody = EmailBodyBuilder.GenerateEmailBody("ApplicationScore",
                new Dictionary<string, string>
                {
                    { "{name}",       application.User.FirstName       },
                    { "{jobTitle}",   application.Job.Title            },
                    { "{score}",      ((int)score).ToString()          },
                    { "{feedback}",   feedback                         },
                    { "{status}",     GetScoreStatus(score)            },
                    { "{scoreClass}", GetScoreClass(score)             },
                    { "{appUrl}",     "https://wazzuf-jobs.vercel.app" }
                });

            await _emailSender.SendEmailAsync(
                application.User.Email!,
                $"🎯 Your AI Match Score for {application.Job.Title} — Wazzuf Jobs",
                emailBody);

            _logger.LogInformation(
                "AI Scoring: Score email sent to {Email}.", application.User.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AI Scoring: Failed to send score email for application {Id}.", application.Id);
        }
    }

    // ── Static helpers ───────────────────────────────────

    private static string GetScoreStatus(float score) => score switch
    {
        >= 80 => "Excellent match! 🌟",
        >= 60 => "Good match! ✅",
        >= 40 => "Fair match 📊",
        _ => "Low match ⚠️"
    };

    private static string GetScoreClass(float score) => score switch
    {
        >= 80 => "excellent",
        >= 60 => "good",
        >= 40 => "fair",
        _ => "low"
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
        Respond in this EXACT format only — no extra text:

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
                var raw = line
                    .Replace("SCORE:", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();

                if (float.TryParse(raw, out var parsed))
                    score = Math.Clamp(parsed, 0, 100);
            }
            else if (line.StartsWith("FEEDBACK:", StringComparison.OrdinalIgnoreCase))
            {
                feedback = line
                    .Replace("FEEDBACK:", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();
            }
        }

        return (score, feedback);
    }
}