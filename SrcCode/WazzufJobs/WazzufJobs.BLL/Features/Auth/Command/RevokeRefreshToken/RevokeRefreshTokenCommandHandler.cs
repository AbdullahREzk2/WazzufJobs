namespace WazzufJobs.BLL.Features.Auth.Command.RevokeRefreshToken;
public class RevokeRefreshTokenCommandHandler(IJwtProvider jwtProvider, IUserRepository userRepository, IAuditLog audit) : IRequestHandler<RevokeRefreshTokenCommand, Result>
{
    private readonly IJwtProvider _jwtprovider = jwtProvider;
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result> Handle(RevokeRefreshTokenCommand request, CancellationToken cancellationToken)
    {

        var userId = _jwtprovider.validateToken(request.token);
        if (userId is null)
            return Result.Failure(UserErrors.InvalidToken);

        var user = await _userrepository.FindByIdAsync(userId);
        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        var userRefreshToken = user.RefreshTokens.SingleOrDefault(rt => rt.Token == request.refreshToken && rt.IsActive);
        if (userRefreshToken is null)
            return Result.Failure(UserErrors.RefreshTokenNotFound);

        userRefreshToken.RevokedOn = DateTime.UtcNow;
        await _userrepository.UpdateAsync(user);

        // signing out reduces access: it never fails because of the audit log
        await audit.WriteAsync(AuditEvent.Success(AuditActions.Logout) with
        {
            ActorUserId = user.Id, TargetType = AuditTargets.User, TargetId = user.Id, IsDemo = user.IsDemo, Required = false
        }, cancellationToken);

        return Result.Success();
    }
}
