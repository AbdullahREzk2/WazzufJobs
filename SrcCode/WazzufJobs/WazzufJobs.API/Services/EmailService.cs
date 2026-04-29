using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using WazzufJobs.API.Setting;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace WazzufJobs.API.Services;


public class EmailService(IOptions<MailSettings> mailsettings, ILogger<EmailService> logger) : IEmailSender
{
    private readonly MailSettings _mailsettings = mailsettings.Value;
    private readonly ILogger<EmailService> _logger = logger;

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var Message = new MimeMessage
        {
            Sender = MailboxAddress.Parse(_mailsettings.FromEmail),
            Subject = subject,

        };

        Message.To.Add(MailboxAddress.Parse(email));

        var builder = new BodyBuilder
        {
            HtmlBody = htmlMessage
        };
        Message.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();

        _logger.LogInformation("Sending email to {email}", email);

        smtp.Connect(_mailsettings.Host, _mailsettings.Port, SecureSocketOptions.StartTls);
        smtp.Authenticate(_mailsettings.FromEmail, _mailsettings.Password);
        await smtp.SendAsync(Message);
        smtp.Disconnect(true);
    }


}
