namespace WazzufJobs.BLL.Features.Auth.Command.Login;
public class LoginCommandHandler(
    IUserRepository userRepository,
    SignInManager<AppUser> signInManager,
    IJwtProvider jwtProvider
    )
    : IRequestHandler<LoginCommand, Result<loginResponseDTO>>
{
    private readonly IUserRepository _userrepository = userRepository;
    private readonly SignInManager<AppUser> _signinmanager = signInManager;
    private readonly IJwtProvider _jwtprovider = jwtProvider;
    private readonly int _refreshTokenValidityInDays = 14;


    public async Task<Result<loginResponseDTO>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {

        var user = await _userrepository.FindByEmailAsync(request.Email);
        if (user is null)
            return Result.Failure<loginResponseDTO>(UserErrors.InvalidCredentials);

        if (user.IsDisabled)
            return Result.Failure<loginResponseDTO>(UserErrors.DisabledUser);

        var result = await _signinmanager.PasswordSignInAsync(user, request.Password, false, true);

        if (result.Succeeded)
        {
            var userRoles = await _userrepository.GetRolesAsync(user);
            var userPermissions = await _userrepository.GetUserPermissionsAsync(userRoles, cancellationToken);

            var (token, expireIn) = _jwtprovider.GenerateToken(user, userRoles, userPermissions);
            var refreshToken = GenerateRefreshTokenHelper.GenerateRefreshToken();
            var refreshTokenExpiration = DateTime.UtcNow.AddDays(_refreshTokenValidityInDays);

            user.RefreshTokens.Add(new RefreshToken
            {
                Token = refreshToken,
                ExpiresOn = refreshTokenExpiration,
            });
            await _userrepository.UpdateAsync(user);

            var response = new loginResponseDTO(user.Id, user.Email!, user.UserName!, user.FirstName, user.LastName, token, expireIn, refreshToken, refreshTokenExpiration);
            return Result.Success(response);
        }

        var error = result.IsNotAllowed ? UserErrors.EmailNotConfirmed
            : result.IsLockedOut ? UserErrors.UserLockedOut
            : UserErrors.InvalidCredentials;

        return Result.Failure<loginResponseDTO>(error);
    }


}
