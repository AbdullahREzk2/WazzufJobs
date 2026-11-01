using WazzufJobs.BLL.Features.Companies.Queries.GetCompany;
using WazzufJobs.BLL.Features.Companies.Queries.GetCompanyMembers;

namespace WazzufJobs.API.Controller;

/// <summary>
/// Company module, read-only (Phase 02, S4). Access comes only from an active membership in the company named in the
/// route (<c>ICompanyAccessService</c>); there is no platform permission for it. A company the caller cannot reach,
/// including another tenant's, answers 404. Registration, invitations and team management arrive in P04.
/// </summary>
[Route("api/v1/companies")]
[ApiController]
[Authorize]
public class CompaniesController(IMediator mediator) : ControllerBase
{
    [HttpGet("{companyId:guid}")]
    public async Task<IActionResult> GetCompany([FromRoute] Guid companyId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCompanyQuery(companyId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{companyId:guid}/members")]
    public async Task<IActionResult> GetMembers([FromRoute] Guid companyId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCompanyMembersQuery(companyId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
