using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.DTOS;

/// <summary>An active membership that can grant company access (see <c>ICompanyRepository.FindActiveMembershipAsync</c>).</summary>
public record CompanyMembership(Guid CompanyId, string UserId, CompanyMemberRole Role, CompanyStatus CompanyStatus);

/// <summary>An active member as seen by team management: role and whether the account is disabled.</summary>
public record CompanyMemberState(CompanyMemberRole Role, bool UserDisabled);

/// <summary>The company profile visible to its members. Storage keys and internal flags are not part of it.</summary>
public record CompanyProfile(
    Guid Id, string Name, string Slug, string? Website, string? Industry, string? Size, string? Location, string? Description,
    CompanyVerificationStatus VerificationStatus, CompanyStatus Status, DateTime CreatedAt);

public record CompanyMemberSummary(string UserId, string FirstName, string LastName, string Email, CompanyMemberRole Role, DateTime JoinedAt);
