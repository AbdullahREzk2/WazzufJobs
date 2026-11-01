namespace WazzufJobs.BLL.Features.Auth.Command.Register;
public class RegisterCommandHandler(
    IUserRepository userRepository,
    ISendConfirmationEmailHelper confirmationEmailHelper,
    IAuditLog audit
    ) : IRequestHandler<RegisterCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;
    private readonly ISendConfirmationEmailHelper _confirmationemailhelper = confirmationEmailHelper;

    public async Task<Result> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {

        if (await _userrepository.EmailExistsAsync(request.requestDTO.Email))
            return Result.Failure(UserErrors.DuplicatedEmail);

        var user = request.requestDTO.Adapt<AppUser>();

        // the account and its audit entry are committed together
        IdentityResult result = IdentityResult.Failed();
        await audit.RecordAsync(async () =>
            {
                result = await _userrepository.CreateAsync(user, request.requestDTO.Password);
                return result.Succeeded ? Result.Success() : Result.Failure(UserErrors.InvalidCredentials);
            },
            r => r.IsSuccess ? AuditEvent.Success(AuditActions.UserRegistered).On(AuditTargets.User, user.Id) with { ActorUserId = user.Id } : null,
            cancellationToken);

        if (result.Succeeded)
        {
            var code = await _userrepository.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            await _confirmationemailhelper.sendConfirmationEmail(user, code);
            return Result.Success();
        }

        var error = result.Errors.First();
        return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
    }

}
