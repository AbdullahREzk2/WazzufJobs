namespace WazzufJobs.BLL.Features.Companies.Commands.RemoveMember;

/// <summary>
/// Removes a member from a company (Phase 02, S4); the membership is kept as Removed. Not exposed over HTTP yet (P04).
/// <paramref name="CompanyId"/> must come from the route.
/// </summary>
public record RemoveCompanyMemberCommand(Guid CompanyId, string UserId) : IRequest<Result>;
