using System.Text.Json;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.BLL.Features.Auth.Command.GoogleLogin;

public class GoogleLoginCommandHandler(
    IUserRepository userRepository,
    IJwtProvider jwtProvider,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IAuditLog audit,
    ISessionService sessions) : IRequestHandler<GoogleLoginCommand, Result<loginResponseDTO>>
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

        // S8: only accept addresses Google has verified
        if (!payload.IsEmailVerified || string.IsNullOrWhiteSpace(payload.Email))
            return Result.Failure<loginResponseDTO>(UserErrors.InvalidCredentials);

        var user = await _userRepository.FindByEmailAsync(payload.Email);

        // S8: never link Google sign-in to a local account whose email was never confirmed
        // (someone else may have registered it to pre-hijack the account)
        if (user is not null && !user.EmailConfirmed)
            return Result.Failure<loginResponseDTO>(UserErrors.GoogleAccountNotLinkable);

        var newAccount = user is null;
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

            await _userRepository.AddToRoleAsync(user, PlatformRoles.Candidate);
        }

        if (user.IsDisabled)
        {
            await audit.WriteAsync(AuditEvent.Failure(AuditActions.LoginFailed).On(AuditTargets.User, user.Id)
                .With(("reason", "disabled")) with { Anonymous = true, IsDemo = user.IsDemo }, cancellationToken);
            return Result.Failure<loginResponseDTO>(UserErrors.DisabledUser);
        }

        await audit.WriteAsync(AuditEvent.Success(AuditActions.GoogleSignIn).On(AuditTargets.User, user.Id)
            .With(("newAccount", newAccount)) with { ActorUserId = user.Id, IsDemo = user.IsDemo }, cancellationToken);

        var grant = PermissionCatalog.Resolve(user.AccountType, user.IsDemo, await _userRepository.GetRolesAsync(user));
        var session = await sessions.StartAsync(user, SessionPolicy.Amr.External, cancellationToken);
        var (token, expireIn) = _jwtProvider.GenerateToken(user, grant, session.Id, session.Amr);
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

        // tokeninfo returns this claim as the string "true"/"false"; a JSON boolean is accepted too
        [JsonPropertyName("email_verified")]
        public JsonElement EmailVerified { get; set; }

        public bool IsEmailVerified =>
            EmailVerified.ValueKind == JsonValueKind.True ||
            (EmailVerified.ValueKind == JsonValueKind.String &&
             string.Equals(EmailVerified.GetString(), "true", StringComparison.OrdinalIgnoreCase));
    }
}
