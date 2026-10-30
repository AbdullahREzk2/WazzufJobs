using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.BLL.Features.Auth.Command.GoogleLogin;

public class GoogleLoginCommandHandler(
    IUserRepository userRepository,
    IJwtProvider jwtProvider,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IRequestHandler<GoogleLoginCommand, Result<loginResponseDTO>>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IJwtProvider _jwtProvider = jwtProvider;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IConfiguration _configuration = configuration;
    private readonly int _refreshTokenValidityInDays = 14;

    public async Task<Result<loginResponseDTO>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var clientId = _configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            return Result.Failure<loginResponseDTO>(new Error("Google.Disabled", "Google sign-in is not configured.", StatusCodes.Status503ServiceUnavailable));

        var payload = await ValidateGoogleTokenAsync(request.IdToken, cancellationToken);
        if (payload is null)
            return Result.Failure<loginResponseDTO>(UserErrors.InvalidCredentials);

        if (!string.Equals(payload.Audience, clientId, StringComparison.Ordinal))
            return Result.Failure<loginResponseDTO>(UserErrors.InvalidCredentials);

        var user = await _userRepository.FindByEmailAsync(payload.Email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = payload.Email,
                Email = payload.Email,
                FirstName = payload.GivenName ?? "User",
                LastName = payload.FamilyName ?? string.Empty,
                EmailConfirmed = true
            };

            var createResult = await _userRepository.CreateAsync(user, Guid.NewGuid().ToString("N") + "Aa1!");
            if (!createResult.Succeeded)
            {
                var error = createResult.Errors.First();
                return Result.Failure<loginResponseDTO>(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
            }

            await _userRepository.AddToRoleAsync(user, AppRoles.User.Name);
        }

        if (user.IsDisabled)
            return Result.Failure<loginResponseDTO>(UserErrors.DisabledUser);

        var userRoles = await _userRepository.GetRolesAsync(user);
        var userPermissions = await _userRepository.GetUserPermissionsAsync(userRoles, cancellationToken);
        var (token, expireIn) = _jwtProvider.GenerateToken(user, userRoles, userPermissions);
        var refreshToken = GenerateRefreshTokenHelper.GenerateRefreshToken();
        var refreshTokenExpiration = DateTime.UtcNow.AddDays(_refreshTokenValidityInDays);

        user.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshToken,
            ExpiresOn = refreshTokenExpiration,
        });
        await _userRepository.UpdateAsync(user);

        var response = new loginResponseDTO(
            user.Id,
            user.Email!,
            user.UserName!,
            user.FirstName,
            user.LastName,
            token,
            expireIn,
            refreshToken,
            refreshTokenExpiration);

        return Result.Success(response);
    }

    private async Task<GoogleTokenPayload?> ValidateGoogleTokenAsync(string idToken, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<GoogleTokenPayload>(cancellationToken: cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private sealed class GoogleTokenPayload
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        [JsonPropertyName("family_name")]
        public string? FamilyName { get; set; }

        [JsonPropertyName("aud")]
        public string Audience { get; set; } = string.Empty;
    }
}
