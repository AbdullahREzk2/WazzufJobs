using WazzufJobs.BLL.Contracts.Companies;

namespace WazzufJobs.BLL.Features.Companies.Queries.GetCompanyMembers;

public record GetCompanyMembersQuery(Guid CompanyId) : IRequest<Result<IReadOnlyList<CompanyMemberResponse>>>;
