using System.ComponentModel.DataAnnotations;

namespace WazzufJobs.BLL.Setting;
public class AISettings
{
    public const string SectionName = "AISettings";

    [Required] public string ApiKey { get; set; } = string.Empty;
    [Required] public string Model { get; set; } = "claude-sonnet-4-20250514";
    public int MaxTokens { get; set; } = 1000;
}
