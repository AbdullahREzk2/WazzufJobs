using System.ComponentModel.DataAnnotations;

namespace WazzufJobs.API.Setting;

public class MailSettings
{
    [Required]
    public string Host { get; set; } = string.Empty;
    [Required]
    public string Username { get; set; } = string.Empty;
    [Required]
    public string Password { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string FromEmail { get; set; } = string.Empty;
    [Required]
    public string FromName { get; set; } = string.Empty;
    public bool EnableSsl { get; set; }
    [Range(100, 999)]
    public int Port { get; set; }
}

