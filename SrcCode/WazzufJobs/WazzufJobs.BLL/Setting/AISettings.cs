using System.ComponentModel.DataAnnotations;

namespace WazzufJobs.BLL.Settings;

public class AISettings
{
    public const string SectionName = "AISettings";

    [Required] public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "llama-3.1-8b-instant";
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/chat/completions";
    public int MaxTokens { get; set; } = 1000;
}