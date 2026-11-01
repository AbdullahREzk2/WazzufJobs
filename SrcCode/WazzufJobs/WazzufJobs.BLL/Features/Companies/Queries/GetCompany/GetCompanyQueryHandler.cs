using WazzufJobs.BLL.Contracts.Companies;

namespace WazzufJobs.BLL.Features.Companies.Queries.GetCompany;

public class GetCompanyQueryHandler(ICompanyAccessService access, ICompanyRepository companies)
    : IRequestHandler<GetCompanyQuery, Result<CompanyResponse>>
{
    public async Task<Result<CompanyResponse>> Handle(GetCompanyQuery request, CancellationToken cancellationToken)
    {
        var allowed = await access.RequireAsync(request.CompanyId, CompanyPermission.ViewCompany, cancellationToken);
        if (!allowed.IsSuccess)
            return Result.Failure<CompanyResponse>(allowed.Error);

        var c = await companies.GetProfileAsync(request.CompanyId, cancellationToken);
        if (c is null)
            return Result.Failure<CompanyResponse>(CompanyErrors.NotFound);

        return Result.Success(new CompanyResponse(c.Id, c.Name, c.Slug, c.Website, c.Industry, c.Size, c.Location, c.Description,
            c.VerificationStatus.ToString(), c.Status.ToString(), c.CreatedAt, allowed.Value.Role.ToString()));
    }
}
