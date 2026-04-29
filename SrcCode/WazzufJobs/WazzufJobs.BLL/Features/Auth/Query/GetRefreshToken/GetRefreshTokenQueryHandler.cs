using WazzufJobs.BLL.Features.Auth.Query.GetRefreshToken;

namespace SurveyBasket.BLL.Features.Auth.Query.GetRefreshToken;
public class GetRefreshTokenQueryHandler(IJwtProvider jwtProvider,IUserRepository userRepository) : IRequestHandler<GetRefreshTokenQuery, Result<loginResponseDTO>>
{
    private readonly IJwtProvider _jwtprovider = jwtProvider;
    private readonly IUserRepository _userrepository = userRepository;
    private readonly int _refreshTokenValidityInDays = 14;


    public async Task<Result<loginResponseDTO>> Handle(GetRefreshTokenQuery request, CancellationToken cancellationToken)
    {

        var userId = _jwtprovider.validateToken(request.token);
        if (userId is null)
            return Result.Failure<loginResponseDTO>(UserErrors.InvalidToken);

        var user = await _userrepository.FindByIdAsync(userId);
        if (user is null)
            return Result.Failure<loginResponseDTO>(UserErrors.UserNotFound);

        if (user.IsDisabled)
            return Result.Failure<loginResponseDTO>(UserErrors.DisabledUser);

        if (user.LockoutEnd > DateTime.UtcNow)
            return Result.Failure<loginResponseDTO>(UserErrors.UserLockedOut);

        var userRefreshToken = user.RefreshTokens.SingleOrDefault(rt => rt.Token == request.refreshToken && rt.IsActive);
        if (userRefreshToken is null)
            return Result.Failure<loginResponseDTO>(UserErrors.RefreshTokenNotFound);

        userRefreshToken.RevokedOn = DateTime.UtcNow;

        var userRoles = await _userrepository.GetRolesAsync(user);
        var userPermissions = await _userrepository.GetUserPermissionsAsync(userRoles, cancellationToken);

        var (newToken, expireIn) = _jwtprovider.GenerateToken(user, userRoles, userPermissions);
        var newRefreshToken = GenerateRefreshTokenHelper.GenerateRefreshToken();
        var refreshTokenExpiration = DateTime.UtcNow.AddDays(_refreshTokenValidityInDays);

        user.RefreshTokens.Add(new RefreshToken
        {
            Token = newRefreshToken,
            ExpiresOn = refreshTokenExpiration,
        });
        await _userrepository.UpdateAsync(user);

        var response = new loginResponseDTO(user.Id, user.Email!, user.UserName!, user.FirstName, user.LastName, newToken, expireIn, newRefreshToken, refreshTokenExpiration);
        return Result.Success(response);
    }
}
