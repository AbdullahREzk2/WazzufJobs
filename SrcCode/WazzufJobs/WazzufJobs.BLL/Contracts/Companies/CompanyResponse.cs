namespace WazzufJobs.BLL.Contracts.Companies;

/// <summary>A company as its members see it. No storage keys, demo or platform flags, or concurrency tokens.</summary>
public record CompanyResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Website,
    string? Industry,
    string? Size,
    string? Location,
    string? Description,
    string VerificationStatus,
    string Status,
    DateTime CreatedAt,
    string MyRole);
