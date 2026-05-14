using System.ComponentModel.DataAnnotations;

namespace WazzufJobs.BLL.Settings;

public class AISettings
{
    public const string SectionName = "AISettings";

    [Required] public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
    public int MaxTokens { get; set; } = 1000;
}