namespace WazzufJobs.BLL.Features.Auth.Command.Login;
public class LoginCommandHandler(
    IUserRepository userRepository,
    SignInManager<AppUser> signInManager,
    IJwtProvider jwtProvider,
    IAuditLog audit,
    ISessionService sessions
    )
    : IRequestHandler<LoginCommand, Result<loginResponseDTO>>
{
    private readonly IUserRepository _userrepository = userRepository;
    private readonly SignInManager<AppUser> _signinmanager = signInManager;
    private readonly IJwtProvider _jwtprovider = jwtProvider;
    private readonly int _refreshTokenValidityInDays = 14;


    public async Task<Result<loginResponseDTO>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {

        // unknown emails are not audited (volume; the rate limiter covers them, D-AUDIT)
        var user = await _userrepository.FindByEmailAsync(request.Email);
        if (user is null)
            return Result.Failure<loginResponseDTO>(UserErrors.InvalidCredentials);

        if (user.IsDisabled)
        {
            await audit.WriteAsync(Failed(user, AuditActions.LoginFailed, "disabled"), cancellationToken);
            return Result.Failure<loginResponseDTO>(UserErrors.DisabledUser);
        }

        var result = await _signinmanager.PasswordSignInAsync(user, request.Password, false, true);

        if (result.Succeeded)
        {
            // required: no tokens are issued for a sign-in that cannot be audited
            await audit.WriteAsync(AuditEvent.Success(AuditActions.LoginSucceeded) with
            {
                ActorUserId = user.Id, TargetType = AuditTargets.User, TargetId = user.Id, IsDemo = user.IsDemo
            }, cancellationToken);

            var grant = PermissionCatalog.Resolve(user.AccountType, user.IsDemo, await _userrepository.GetRolesAsync(user));
            // Phase 02 (S6a): every sign-in is a server-side session; the access token names it (sid). The legacy refresh
            // token below is unchanged until S6b moves refresh to the session engine (owner decision).
            var session = await sessions.StartAsync(user, SessionPolicy.Amr.Password, cancellationToken);
            var (token, expireIn) = _jwtprovider.GenerateToken(user, grant, session.Id, session.Amr);
            var refreshToken = GenerateRefreshTokenHelper.GenerateRefreshToken();
            var refreshTokenExpiration = DateTime.UtcNow.AddDays(_refreshTokenValidityInDays);

            user.RefreshTokens.Add(new RefreshToken
            {
                Token = refreshToken,
                ExpiresOn = refreshTokenExpiration,
            });
            await _userrepository.UpdateAsync(user);

            var response = new loginResponseDTO(user.Id, user.Email!, user.UserName!, user.FirstName, user.LastName, token, expireIn, refreshToken, refreshTokenExpiration);
            return Result.Success(response);
        }

        var error = result.IsNotAllowed ? UserErrors.EmailNotConfirmed
            : result.IsLockedOut ? UserErrors.UserLockedOut
            : UserErrors.InvalidCredentials;

        await audit.WriteAsync(result.IsLockedOut
            ? Failed(user, AuditActions.LoginLockedOut, "locked_out")
            : Failed(user, AuditActions.LoginFailed, result.IsNotAllowed ? "email_not_confirmed" : "invalid_password"), cancellationToken);

        return Result.Failure<loginResponseDTO>(error);
    }

    // not required: an audit outage never changes the response (which would reveal that the account exists)
    private static AuditEvent Failed(AppUser user, string action, string reason) =>
        AuditEvent.Failure(action).On(AuditTargets.User, user.Id).With(("reason", reason)) with { Anonymous = true, IsDemo = user.IsDemo };


}
