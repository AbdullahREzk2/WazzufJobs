using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Features.Companies.Commands.ChangeMemberRole;

/// <summary>
/// Changes a member's company role (Phase 02, S4). Not exposed over HTTP yet: team management endpoints arrive with
/// invitations in P04. <paramref name="CompanyId"/> must come from the route.
/// </summary>
public record ChangeCompanyMemberRoleCommand(Guid CompanyId, string UserId, CompanyMemberRole Role) : IRequest<Result>;
