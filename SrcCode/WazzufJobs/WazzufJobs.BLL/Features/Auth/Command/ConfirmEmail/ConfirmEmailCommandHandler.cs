namespace WazzufJobs.BLL.Features.Auth.Command.ConfirmEmail;
public class ConfirmEmailCommandHandler(IUserRepository userRepository, IBackgroundJobClient backgroundJob, IsendWelcomeEmail isendWelcome) : IRequestHandler<ConfirmEmailCommand, Result>
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

        var result = await _userrepository.ConfirmEmailAsync(user, code);
        if (result.Succeeded)
        {
            await _isendwelcome.sendEmail(user);
            await _userrepository.AddToRoleAsync(user, AppRoles.User.Name);
            return Result.Success();
        }

        var error = result.Errors.First();
        return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
    }


}
