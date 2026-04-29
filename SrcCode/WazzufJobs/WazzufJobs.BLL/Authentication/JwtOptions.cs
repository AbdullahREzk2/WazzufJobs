using System.ComponentModel.DataAnnotations;

namespace WazzufJobs.BLL.Authentication;
public class JwtOptions
{
    public static string sectionName = "Jwt";

    [Required]
    public string key { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Expiry date must be int !")]
    public int ExpiryMinutes { get; init; }

}
