using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using WazzufJobs.BLL.Helpers;

namespace WazzufJobs.BLL.Features.Applications.Commands.UpdateApplicationStatus;

public class UpdateApplicationStatusCommandHandler(
    IApplicationRepository applicationRepository,
    IEmailSender emailSender,
    ILogger<UpdateApplicationStatusCommandHandler> logger) : IRequestHandler<UpdateApplicationStatusCommand, Result>
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly ILogger<UpdateApplicationStatusCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateApplicationStatusCommand request, CancellationToken cancellationToken)
    {
        var application = await _applicationRepository
            .GetByIdWithDetailsAsync(request.Id, cancellationToken);

        if (application is null)
            return Result.Failure(ApplicationErrors.NotFound);

        application.Status = request.Request.Status;

        await _applicationRepository.UpdateAsync(application);
        await _applicationRepository.SaveChangesAsync(cancellationToken);

        await SendStatusEmailAsync(application, cancellationToken);

        return Result.Success();
    }

    private async Task SendStatusEmailAsync(DAL.Entities.Application application, CancellationToken cancellationToken)
    {
        try
        {
            var statusLabel = application.Status.ToString();
            var emailBody = EmailBodyBuilder.GenerateEmailBody("ApplicationStatusUpdate",
                new Dictionary<string, string>
                {
                    { "{name}", application.User.FirstName },
                    { "{jobTitle}", application.Job.Title },
                    { "{status}", statusLabel },
                    { "{message}", GetStatusMessage(application.Status) },
                    { "{appUrl}", "https://wazzuf-jobs.vercel.app/dashboard" }
                });

            await _emailSender.SendEmailAsync(
                application.User.Email!,
                $"📬 Application update for {application.Job.Title} — Wazzuf Jobs",
                emailBody);

            _logger.LogInformation(
                "Status email sent to {Email} for application {Id}.",
                application.User.Email,
                application.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send status email for application {Id}.",
                application.Id);
        }
    }

    private static string GetStatusMessage(DAL.Enums.ApplicationStatus status) => status switch
    {
        DAL.Enums.ApplicationStatus.Pending => "Your application is pending review. We will notify you when there is an update.",
        DAL.Enums.ApplicationStatus.UnderReview => "Great news — your application is now under review.",
        DAL.Enums.ApplicationStatus.Shortlisted => "Congratulations! You have been shortlisted for the next stage.",
        DAL.Enums.ApplicationStatus.Accepted => "Congratulations! Your application has been accepted.",
        DAL.Enums.ApplicationStatus.Rejected => "Thank you for applying. Unfortunately, your application was not selected this time.",
        _ => "Your application status has changed."
    };
}
