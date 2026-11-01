namespace WazzufJobs.BLL.Features.Auth.Command.ConfirmEmail;
public class ConfirmEmailCommandHandler(IUserRepository userRepository, IBackgroundJobClient backgroundJob, IsendWelcomeEmail isendWelcome, IAuditLog audit) : IRequestHandler<ConfirmEmailCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;
    private readonly IBackgroundJobClient _backgroundjob = backgroundJob;
    private readonly IsendWelcomeEmail _isendwelcome = isendWelcome;

    public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {

        var user = await _userrepository.FindByIdAsync(request.emailRequest.UserId);
        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        if (user.EmailConfirmed)
            return Result.Failure(UserErrors.DuplicateConfirmation);

        string code;
        try
        {
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.emailRequest.Code));
        }
        catch (FormatException)
        {
            return Result.Failure(UserErrors.InvalidCode);
        }

        // confirmation, the Candidate role and the audit entry are committed together
        var outcome = await audit.RecordAsync(async () =>
            {
                var result = await _userrepository.ConfirmEmailAsync(user, code);
                if (!result.Succeeded)
                {
                    var error = result.Errors.First();
                    return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
                }

                var role = await _userrepository.AddToRoleAsync(user, PlatformRoles.Candidate);
                return role.Succeeded
                    ? Result.Success()
                    : Result.Failure(new Error(role.Errors.First().Code, role.Errors.First().Description, StatusCodes.Status400BadRequest));
            },
            r => r.IsSuccess
                ? AuditEvent.Success(AuditActions.EmailConfirmed).On(AuditTargets.User, user.Id)
                    .With(("roles", new[] { PlatformRoles.Candidate })) with { ActorUserId = user.Id }
                : null,
            cancellationToken);

        if (outcome.IsSuccess)
            await _isendwelcome.sendEmail(user);
        return outcome;
    }


}
