using Hangfire;
using Microsoft.AspNetCore.Identity.UI.Services;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.BLL.Helpers;
public class sendWelcomeEmail(IBackgroundJobClient backgroundJob) : IsendWelcomeEmail
{
    private readonly IBackgroundJobClient _backgroundjob = backgroundJob;

    public async Task sendEmail(AppUser user)
    {
        var emailBody = EmailBodyBuilder.GenerateEmailBody("Welcome",
            new Dictionary<string, string> { { "{{UserName}}", user.FirstName } });
        _backgroundjob.Enqueue<IEmailSender>(x =>
            x.SendEmailAsync(user.Email!, "👋 Wazzuf Jobs : Welcome Email", emailBody));
        await Task.CompletedTask;
    }
}
