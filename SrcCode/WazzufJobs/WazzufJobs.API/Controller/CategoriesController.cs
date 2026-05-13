using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Contracts.Categories;
using WazzufJobs.BLL.Features.Categories.Commands.CreateCategory;
using WazzufJobs.BLL.Features.Categories.Commands.DeleteCategory;
using WazzufJobs.BLL.Features.Categories.Commands.UpdateCategory;
using WazzufJobs.BLL.Features.Categories.Queries.GetAllCategories;
using WazzufJobs.BLL.Features.Categories.Queries.GetCategoryById;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoriesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [Authorize]
    [HasPermission(Permissions.CategoriesRead)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllCategoriesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    [HasPermission(Permissions.CategoriesRead)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCategoryByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost]
    [HasPermission(Permissions.CategoriesCreate)]
    public async Task<IActionResult> Create([FromForm] CategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateCategoryCommand(request), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.ToProblem();
    }

    [HttpPut("{id}")]
    [HasPermission(Permissions.CategoriesUpdate)]
    public async Task<IActionResult> Update(int id, [FromForm] CategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateCategoryCommand(id, request), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpDelete("{id}")]
    [HasPermission(Permissions.CategoriesDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteCategoryCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }
}