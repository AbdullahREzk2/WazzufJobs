namespace WazzufJobs.API.Controllers;
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AccountController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    #region get user info
    [HttpGet("userInfo")]
    public async Task<IActionResult> GetUserInfo()
    {
        var result = await _mediator.Send(new GetProfileQuery(User.GetUserId()!));

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
    #endregion

    #region Update user info
    [HttpPut("update-User-Info")]
    public async Task<IActionResult> UpdateUserInfo([FromBody] UpdateProfileRequest request)
    {
        await _mediator.Send(new UpdateUserProfileCommand(User.GetUserId()!, request));

        return NoContent();
    }
    #endregion

    #region chnage User Password
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await _mediator.Send(new ChangePasswordCommand(User.GetUserId()!, request));
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }
    #endregion

    #region add Profile Image
    [HttpPost("profile-image")]
    public async Task<IActionResult> UploadProfileImage(IFormFile image, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UploadProfileImageCommand(User.GetUserId()!, image));
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }
    #endregion

}
