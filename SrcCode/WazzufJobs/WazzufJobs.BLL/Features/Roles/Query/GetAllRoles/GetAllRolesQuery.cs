namespace WazzufJobs.BLL.Features.Roles.Query.GetAllRoles;
public record GetAllRolesQuery(bool? includeDisabled = false) : IRequest<IEnumerable<RoleResponse>>;
