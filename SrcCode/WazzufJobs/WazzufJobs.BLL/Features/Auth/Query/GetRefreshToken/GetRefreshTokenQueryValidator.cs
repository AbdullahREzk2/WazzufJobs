namespace WazzufJobs.BLL.Features.Auth.Query.GetRefreshToken;

public class GetRefreshTokenQueryValidator : AbstractValidator<GetRefreshTokenQuery>
{
    public GetRefreshTokenQueryValidator()
    {
        RuleFor(x => x.token)
            .NotEmpty().WithMessage("Token is required.");

        RuleFor(x => x.refreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}