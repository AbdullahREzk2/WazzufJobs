namespace WazzufJobs.BLL.Features.Roles.Query.GetRoleDetails;
public record GetRoleDetailsQuery(string RoleId) : IRequest<Result<RoleDetailResponse>>;
