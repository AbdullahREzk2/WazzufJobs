namespace WazzufJobs.BLL.Features.Roles.Query.GetRoleDetails;

/// <summary>A fixed platform role and its permissions, read from the code catalog (Phase 02, S3).</summary>
public class GetRoleDetailsQueryHandler : IRequestHandler<GetRoleDetailsQuery, Result<RoleDetailResponse>>
{
    public Task<Result<RoleDetailResponse>> Handle(GetRoleDetailsQuery request, CancellationToken cancellationToken)
    {
        var role = PermissionCatalog.FindById(request.RoleId);
        if (role is null)
            return Task.FromResult(Result.Failure<RoleDetailResponse>(RoleErros.RoleNotFound));

        return Task.FromResult(Result.Success(new RoleDetailResponse(
            role.Id, role.Name, false, role.Permissions, role.Description, role.AccountType.ToString())));
    }
}
