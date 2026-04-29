using WazzufJobs.BLL.Features.Auth.Query.ResendConfimationEmail;

namespace SurveyBasket.BLL.Features.Auth.Query.ResendConfimationEmail;
public class ResendConfirmationEmailQueryHandler(
    IUserRepository userRepository,
    ISendConfirmationEmailHelper confirmationEmailHelper
    ) : IRequestHandler<ResendConfirmationEmailQuery, Result>
{
    private readonly IUserRepository _userrepository = userRepository;
    private readonly ISendConfirmationEmailHelper _confirmationemailhelper = confirmationEmailHelper;

    public async Task<Result> Handle(ResendConfirmationEmailQuery request, CancellationToken cancellationToken)
    {
        var user = await _userrepository.FindByEmailAsync(request.emailRequest.Email);
        if (user is null)
            return Result.Success();

        if (user.EmailConfirmed)
            return Result.Failure(UserErrors.DuplicateConfirmation);

        var code = await _userrepository.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        await _confirmationemailhelper.sendConfirmationEmail(user, code);

        return Result.Success();
    }
}
