using Microsoft.AspNetCore.RateLimiting;
using WazzufJobs.BLL.Contracts.Authentication;
using WazzufJobs.BLL.Features.Auth.Command.ConfirmEmail;
using WazzufJobs.BLL.Features.Auth.Command.Login;
using WazzufJobs.BLL.Features.Auth.Command.Register;
using WazzufJobs.BLL.Features.Auth.Command.ResetPassword;
using WazzufJobs.BLL.Features.Auth.Command.RevokeRefreshToken;
using WazzufJobs.BLL.Features.Auth.Query.GetRefreshToken;
using WazzufJobs.BLL.Features.Auth.Query.ResendConfimationEmail;
using WazzufJobs.BLL.Features.Auth.Query.SendResetPassword;

namespace WazzufJobs.API.Controllers;
[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("ipLimit")]
public class AuthController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;


    #region Login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] loginRequestDTO request, CancellationToken cancellationToken)
    {
        var authResult = await _mediator.Send(new LoginCommand(request.Email, request.Password));
        return authResult.IsSuccess ? Ok(authResult.Value) : authResult.ToProblem();
    }
    #endregion

    #region resfresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] refreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRefreshTokenQuery(request.Token, request.RefreshToken));

        return result.IsSuccess
            ? Ok(result)
            : result.ToProblem();

    }
    #endregion

    #region revoke-refresh-Token
    [HttpPost("revoke-refresh-token")]
    public async Task<IActionResult> revokeRefreshToken([FromBody] refreshTokenRequest request, CancellationToken cancellationToken)
    {
        var isRevoked = await _mediator.Send(new RevokeRefreshTokenCommand(request.Token, request.RefreshToken));

        return isRevoked.IsSuccess
            ? Ok()
            : isRevoked.ToProblem();
    }
    #endregion


    #region Register
    [HttpPost("Register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDTO request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RegisterCommand(request));

        return result.IsSuccess
            ? Ok()
            : result.ToProblem();
    }
    #endregion

    #region Confirm-Email
    [HttpGet("Confirm-Email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] ConfirmEmailRequestDTO request)
    {
        var result = await _mediator.Send(new ConfirmEmailCommand(request));
        return result.IsSuccess
            ? Ok()
            : result.ToProblem();
    }
    #endregion

    #region Resend-Confirm-Email
    [HttpPost("Resend-Confirm-Email")]
    public async Task<IActionResult> ResendConfirmEmail([FromBody] ResendConfirmationEmailRequest request)
    {
        var result = await _mediator.Send(new ResendConfirmationEmailQuery(request));

        return result.IsSuccess
            ? Ok()
            : result.ToProblem();
    }
    #endregion


    #region Forget-Password
    [HttpPost("forget-Password")]
    public async Task<IActionResult> forgetPassword([FromBody] ForgetPasswordRequest request)
    {
        var result = await _mediator.Send(new SendResetPasswordQuery(request));

        return result.IsSuccess
            ? Ok()
            : result.ToProblem();
    }
    #endregion

    #region Reset-Password
    [HttpPost("Reset-Password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _mediator.Send(new ResetPasswordCommand(request));
        return result.IsSuccess
            ? Ok()
            : result.ToProblem();
    }
    #endregion

}
