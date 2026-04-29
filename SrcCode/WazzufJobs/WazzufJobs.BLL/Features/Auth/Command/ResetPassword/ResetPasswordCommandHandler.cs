namespace WazzufJobs.BLL.Features.Auth.Command.ResetPassword;
public class ResetPasswordCommandHandler(IUserRepository userRepository) : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {

        var user = await _userrepository.FindByEmailAsync(request.passRequest.Email);
        if (user is null || !user.EmailConfirmed)
            return Result.Failure(UserErrors.UserNotFound);

        IdentityResult result;
        try
        {
            var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.passRequest.Code));
            result = await _userrepository.ResetPasswordAsync(user, code, request.passRequest.NewPassword);
        }
        catch (FormatException)
        {
            return Result.Failure(UserErrors.InvalidCode);
        }

        if (result.Succeeded)
            return Result.Success();

        var error = result.Errors.First();
        return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status401Unauthorized));
    }
}
