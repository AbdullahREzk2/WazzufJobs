using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Features.UserCV.Commands.DeleteCV;
using WazzufJobs.BLL.Features.UserCV.Commands.UploadCV;
using WazzufJobs.BLL.Features.UserCV.Queries.GetCV;
using WazzufJobs.BLL.Services;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CVController(IMediator mediator,ICloudinaryService cloudinaryService) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly ICloudinaryService _cloudinaryservice = cloudinaryService;

    [HttpGet]
    [HasPermission(Permissions.CVUpload)]
    public async Task<IActionResult> GetMyCV(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId()!;
        var result = await _mediator.Send(new GetCVQuery(userId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost]
    [HasPermission(Permissions.CVUpload)]
    public async Task<IActionResult> Upload(IFormFile file,CancellationToken cancellationToken)
    {
        var userId = User.GetUserId()!;
        var result = await _mediator.Send(
            new UploadCVCommand(userId, file), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpDelete]
    [HasPermission(Permissions.CVDelete)]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId()!;
        var result = await _mediator.Send(new DeleteCVCommand(userId), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }


    
}