namespace WazzufJobs.BLL.Features.Auth.Command.Register;
public class RegisterCommandHandler(
    IUserRepository userRepository,
    ISendConfirmationEmailHelper confirmationEmailHelper
    ) : IRequestHandler<RegisterCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;
    private readonly ISendConfirmationEmailHelper _confirmationemailhelper = confirmationEmailHelper;

    public async Task<Result> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {

        if (await _userrepository.EmailExistsAsync(request.requestDTO.Email))
            return Result.Failure(UserErrors.DuplicatedEmail);

        var user = request.requestDTO.Adapt<AppUser>();
        var result = await _userrepository.CreateAsync(user, request.requestDTO.Password);

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
