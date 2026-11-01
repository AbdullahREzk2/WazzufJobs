using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using WazzufJobs.BLL.Helpers;
using WazzufJobs.Shared;

namespace WazzufJobs.BLL.Features.Applications.Commands.UpdateApplicationStatus;

public class UpdateApplicationStatusCommandHandler(
    IApplicationRepository applicationRepository,
    IEmailSender emailSender,
    ICurrentUserService currentUser,
    ILogger<UpdateApplicationStatusCommandHandler> logger,
    IAuditLog audit) : IRequestHandler<UpdateApplicationStatusCommand, Result>
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly ILogger<UpdateApplicationStatusCommandHandler> _logger = logger;

    // the status change, its history and the audit entry are committed together; the email is sent only afterwards
    public async Task<Result> Handle(UpdateApplicationStatusCommand request, CancellationToken cancellationToken)
    {
        DAL.Entities.Application? application = null;
        DAL.Enums.ApplicationStatus? previous = null;
        var result = await audit.RecordAsync(async () =>
            {
                application = await _applicationRepository.GetByIdWithDetailsAsync(request.Id, cancellationToken);
                if (application is null)
                    return Result.Failure(ApplicationErrors.NotFound);

                previous = application.Status;
                return await ApplyAsync(application, request, cancellationToken);
            },
            r => r.IsSuccess
                ? AuditEvent.Success(AuditActions.ApplicationStatusChanged).On(AuditTargets.Application, request.Id.ToString())
                    .With(("from", previous), ("to", request.Request.Status)) with { CompanyId = application?.CompanyId, IsDemo = application?.IsDemo ?? false }
                : null,
            cancellationToken);

        if (result.IsSuccess)
            await SendStatusEmailAsync(application!, cancellationToken);
        return result;
    }

    private async Task<Result> ApplyAsync(DAL.Entities.Application application, UpdateApplicationStatusCommand request, CancellationToken cancellationToken)
    {
        if (application.Status != request.Request.Status)
        {
            application.StatusHistory.Add(new ApplicationStatusHistory
            {
                FromStatus = application.Status,
                ToStatus = request.Request.Status,
                ChangedById = currentUser.UserId,
                ChangedAt = DateTime.UtcNow
            });
        }

        application.Status = request.Request.Status;
        application.UpdatedAt = DateTime.UtcNow;

        await _applicationRepository.UpdateAsync(application);
        await _applicationRepository.SaveChangesAsync(cancellationToken);

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
