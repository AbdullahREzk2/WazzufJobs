namespace WazzufJobs.BLL.Setting;
public class AppURLSetting
{
    /// <summary>API base URL (legacy).</summary>
    public string BaseUrl { get; set; } = default!;

    /// <summary>Angular SPA URL for email links (confirm email, reset password).</summary>
    public string? FrontendUrl { get; set; }

    public string GetFrontendBase() =>
        string.IsNullOrWhiteSpace(FrontendUrl) ? BaseUrl.TrimEnd('/') : FrontendUrl.TrimEnd('/');
}
