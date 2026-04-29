using Hangfire;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Setting;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.BLL.Helpers;
public class SendResetPasswordEmailHelper(IBackgroundJobClient backgroundJob, IOptions<AppURLSetting> appURL) : ISendResetPasswordEmailHelper
{
    private readonly IBackgroundJobClient _backgroundjob = backgroundJob;
    private readonly AppURLSetting _appurl = appURL.Value;

    public async Task sendResetPasswordEmail(AppUser user, string code)
    {
        var emailBody = EmailBodyBuilder.GenerateEmailBody("ResetPassword",
            new Dictionary<string, string>
            {
            { "{{UserName}}", user.FirstName },
            { "{{ResetLink}}", $"{_appurl.BaseUrl}/reset-password?email={user.Email}&code={code}" }
            });
        _backgroundjob.Enqueue<IEmailSender>(x =>
            x.SendEmailAsync(user.Email!, "🔑 Wazzuf Jobs : Reset Password Email", emailBody));
        await Task.CompletedTask;
    }

}
