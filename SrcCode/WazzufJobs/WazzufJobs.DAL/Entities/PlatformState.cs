namespace WazzufJobs.DAL.Entities;

/// <summary>Platform-wide state values, e.g. when the one-time Super Admin bootstrap completed (Phase 02, §7).</summary>
public class PlatformState
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
