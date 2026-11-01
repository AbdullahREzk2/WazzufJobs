using WazzufJobs.BLL.Contracts.Companies;

namespace WazzufJobs.BLL.Features.Companies.Queries.GetCompanyMembers;

public class GetCompanyMembersQueryHandler(ICompanyAccessService access, ICompanyRepository companies)
    : IRequestHandler<GetCompanyMembersQuery, Result<IReadOnlyList<CompanyMemberResponse>>>
{
    public async Task<Result<IReadOnlyList<CompanyMemberResponse>>> Handle(GetCompanyMembersQuery request, CancellationToken cancellationToken)
    {
        var allowed = await access.RequireAsync(request.CompanyId, CompanyPermission.ViewMembers, cancellationToken);
        if (!allowed.IsSuccess)
            return Result.Failure<IReadOnlyList<CompanyMemberResponse>>(allowed.Error);

        var members = await companies.GetActiveMembersAsync(request.CompanyId, cancellationToken);
        return Result.Success<IReadOnlyList<CompanyMemberResponse>>(members
            .Select(m => new CompanyMemberResponse(m.UserId, m.FirstName, m.LastName, m.Email, m.Role.ToString(), m.JoinedAt))
            .ToList());
    }
}
