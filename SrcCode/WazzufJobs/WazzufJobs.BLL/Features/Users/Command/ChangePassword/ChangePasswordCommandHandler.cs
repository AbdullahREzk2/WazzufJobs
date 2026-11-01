using WazzufJobs.BLL.Features.Users.Command.ChangePassword;

namespace SurveyBasket.BLL.Features.Users.Command.ChangePassword;
public class ChangePasswordCommandHandler(IUserRepository userRepository, IAuditLog audit) : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userrepository.FindByIdAsync(request.userId);
        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        // the new password, the revoked sessions and the audit entry are committed together
        var revoked = 0;
        return await audit.RecordAsync(async () =>
            {
                var result = await _userrepository.ChangePasswordAsync(user, request.Passrequest.CurrentPassword, request.Passrequest.NewPassword);
                if (!result.Succeeded)
                {
                    var error = result.Errors.First();
                    return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
                }

                // sign out other sessions: a rotated password must not leave old refresh tokens usable
                revoked = await _userrepository.RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
                return Result.Success();
            },
            r => r.IsSuccess
                ? AuditEvent.Success(AuditActions.PasswordChanged).On(AuditTargets.User, user.Id).With(("revokedTokens", revoked))
                : AuditEvent.Failure(AuditActions.PasswordChangeFailed).On(AuditTargets.User, user.Id).With(("reason", r.Error.Code)),
            cancellationToken);
    }
}
