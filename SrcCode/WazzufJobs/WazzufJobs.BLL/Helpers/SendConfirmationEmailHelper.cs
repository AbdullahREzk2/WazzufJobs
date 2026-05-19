using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Setting;

namespace WazzufJobs.BLL.Helpers;
public class SendConfirmationEmailHelper(IBackgroundJobClient backgroundJob, IOptions<AppURLSetting> appURL) : ISendConfirmationEmailHelper
{
    private readonly IBackgroundJobClient _backgroundjob = backgroundJob;
    private readonly AppURLSetting _appurl = appURL.Value;

    public async Task sendConfirmationEmail(AppUser user, string code)
    {
        var emailBody = EmailBodyBuilder.GenerateEmailBody("EmailConfirmation",
            new Dictionary<string, string>
            {
            { "{name}", user.FirstName },
            { "{action_url}", $"{_appurl.GetFrontendBase()}/confirm-email?userId={user.Id}&code={Uri.EscapeDataString(code)}" }
            });
        _backgroundjob.Enqueue<IEmailSender>(x =>
            x.SendEmailAsync(user.Email!, "✅ Wazzuf Jobs : Email Confirmation", emailBody));
        await Task.CompletedTask;
    }

}
