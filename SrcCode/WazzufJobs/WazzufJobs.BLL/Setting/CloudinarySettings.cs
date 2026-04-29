using System.ComponentModel.DataAnnotations;

namespace WazzufJobs.BLL.Setting;
public class CloudinarySettings
{
    public static string SectionName = "CloudinarySettings";

    [Required]
    public string CloudName { get; set; } = string.Empty;
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    [Required]
    public string ApiSecret { get; set; } = string.Empty;
}
