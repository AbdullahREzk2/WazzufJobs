namespace WazzufJobs.BLL.Features.Auth.Command.ResetPassword;
public class ResetPasswordCommandHandler(IUserRepository userRepository, IAuditLog audit) : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {

        var user = await _userrepository.FindByEmailAsync(request.passRequest.Email);
        if (user is null || !user.EmailConfirmed)
            return Result.Failure(UserErrors.UserNotFound);

        string code;
        try
        {
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.passRequest.Code));
        }
        catch (FormatException)
        {
            return Result.Failure(UserErrors.InvalidCode);
        }

        // the new password, the revoked sessions and the audit entry are committed together
        var revoked = 0;
        return await audit.RecordAsync(async () =>
            {
                var result = await _userrepository.ResetPasswordAsync(user, code, request.passRequest.NewPassword);
                if (!result.Succeeded)
                {
                    var error = result.Errors.First();
                    return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status401Unauthorized));
                }

                revoked = await _userrepository.RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
                return Result.Success();
            },
            r => r.IsSuccess
                ? AuditEvent.Success(AuditActions.PasswordResetSucceeded).On(AuditTargets.User, user.Id)
                    .With(("revokedTokens", revoked)) with { Anonymous = true, IsDemo = user.IsDemo }
                : AuditEvent.Failure(AuditActions.PasswordResetFailed).On(AuditTargets.User, user.Id)
                    .With(("reason", r.Error.Code)) with { Anonymous = true, IsDemo = user.IsDemo },
            cancellationToken);
    }
}
