using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Setting;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.BLL.Services;

public class AIScoringService(
    ApplicationDBContext context,
    ICVTextExtractor cvTextExtractor,
    IOptions<AISettings> aiSettings,
    ILogger<AIScoringService> logger) : IAIScoringService
{
    private readonly ApplicationDBContext _context = context;
    private readonly ICVTextExtractor _cvTextExtractor = cvTextExtractor;
    private readonly AISettings _aiSettings = aiSettings.Value;
    private readonly ILogger _logger = logger;

    public async Task ScoreApplicationAsync(int applicationId,CancellationToken cancellationToken)
    {
        // load application with all needed data
        var application = await _context.Applications
            .Include(a => a.User)
                .ThenInclude(u => u.CV)
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application is null)
        {
            _logger.LogWarning("AI Scoring: Application {Id} not found.", applicationId);
            return;
        }

        if (application.User.CV is null)
        {
            _logger.LogWarning("AI Scoring: No CV found for application {Id}.", applicationId);
            return;
        }

        // extract CV text from PDF
        var cvText = await _cvTextExtractor.ExtractTextAsync(application.User.CV.Url, cancellationToken);

        if (string.IsNullOrWhiteSpace(cvText))
        {
            _logger.LogWarning("AI Scoring: Could not extract text from CV for application {Id}.", applicationId);
            return;
        }

        // build the prompt
        var prompt = BuildScoringPrompt(cvText, application.Job);

        try
        {
            var client = new AnthropicClient(_aiSettings.ApiKey);

            var response = await client.Messages.GetClaudeMessageAsync(
                new MessageParameters
                {
                    Model = _aiSettings.Model,
                    MaxTokens = _aiSettings.MaxTokens,
                    Messages =
                    [
                        new Message
                        {
                            Role    = RoleType.User,
                            Content = [new TextContent { Text = prompt }]
                        }
                    ]
                });

            var responseText = response.Content
                .OfType<TextContent>()
                .FirstOrDefault()?.Text ?? string.Empty;

            // parse score and feedback from response
            var (score, feedback) = ParseAIResponse(responseText);

            // update application
            application.AIScore = score;
            application.AIFeedback = feedback;
            application.IsAIScored = true;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "AI Scoring: Application {Id} scored {Score}/100.",
                applicationId, score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AI Scoring: Failed to score application {Id}.", applicationId);
        }
    }

    private static string BuildScoringPrompt(string cvText, DAL.Entities.Job job)
    {
        return $"""
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
            Analyze the match and respond in this EXACT format (do not deviate):

            SCORE: [number between 0 and 100]
            FEEDBACK: [2-3 sentences explaining the score, highlighting strengths and gaps]

            Be objective and base the score on:
            - Skills match (40%)
            - Experience relevance (30%)
            - Education fit (20%)
            - Location/work type compatibility (10%)
            """;
    }

    private static (float Score, string Feedback) ParseAIResponse(string response)
    {
        float score = 0;
        var feedback = string.Empty;

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("SCORE:", StringComparison.OrdinalIgnoreCase))
            {
                var scoreStr = line.Replace("SCORE:", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (float.TryParse(scoreStr, out var parsed))
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