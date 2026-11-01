namespace WazzufJobs.DAL.Persistence.Seeders;

/// <summary>
/// The single platform company (seeded by Baseline_v2). Jobs created by platform staff belong to it until companies
/// post their own jobs (P04). Handlers look the company up by <c>IsPlatform</c>; this id is only the seed value.
/// </summary>
public static class PlatformCompany
{
    public static readonly Guid Id = new("6f1d2c3b-8a47-4e5f-9b0a-1c2d3e4f5a6b");
    public const string Name = "WazzufJobs";
    public const string Slug = "wazzufjobs";
}
