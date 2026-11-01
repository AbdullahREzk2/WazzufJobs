namespace WazzufJobs.BLL.Features.Auth.Query.SendResetPassword;
public class SendResetPasswordQueryHandler(
    IUserRepository userRepository,
    ISendResetPasswordEmailHelper passwordEmailHelper,
    IAuditLog audit
    ) : IRequestHandler<SendResetPasswordQuery, Result>
{
    private readonly IUserRepository _userrepository = userRepository;
    private readonly ISendResetPasswordEmailHelper _passwordemailhelper = passwordEmailHelper;

    public async Task<Result> Handle(SendResetPasswordQuery request, CancellationToken cancellationToken)
    {
        var user = await _userrepository.FindByEmailAsync(request.passRequest.Email);
        if (user is null)
            return Result.Success();

        if (!user.EmailConfirmed)
            return Result.Failure(UserErrors.EmailNotConfirmed);

        // not required: an audit outage must not make existing accounts answer differently from unknown ones
        await audit.WriteAsync(AuditEvent.Success(AuditActions.PasswordResetRequested).On(AuditTargets.User, user.Id)
            with { Anonymous = true, IsDemo = user.IsDemo, Required = false }, cancellationToken);

        var code = await _userrepository.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        await _passwordemailhelper.sendResetPasswordEmail(user, code);

        return Result.Success();
    }
}
