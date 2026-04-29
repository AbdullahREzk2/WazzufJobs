namespace WazzufJobs.BLL.Features.Roles.Command.CreateRole;
public record CreateRoleCommand(RoleRequest roleRequest) : IRequest<Result<RoleDetailResponse>>;
