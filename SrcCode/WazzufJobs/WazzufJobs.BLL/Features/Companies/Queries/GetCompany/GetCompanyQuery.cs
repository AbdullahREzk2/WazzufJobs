using WazzufJobs.BLL.Contracts.Companies;

namespace WazzufJobs.BLL.Features.Companies.Queries.GetCompany;

public record GetCompanyQuery(Guid CompanyId) : IRequest<Result<CompanyResponse>>;
