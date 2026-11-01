using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace WazzufJobs.BLL.Authentication;
public class JwtProvider : IJwtProvider
{
    private readonly JwtOptions _options;

    public JwtProvider(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }
    public (string token, int expireIn) GenerateToken(AppUser user, AccessGrant grant)
    {
        Claim[] claims = [
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email,user.Email!),
            new(JwtRegisteredClaimNames.GivenName,user.FirstName!),
            new(JwtRegisteredClaimNames.FamilyName,user.LastName!),
            new(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()),
            // Phase 02 (S3): effective platform roles plus legacy aliases (display only: the API authorizes by
            // permission, never by role), the account type, and the catalog permissions of the effective roles
            new(AccessTokenClaims.Roles,JsonSerializer.Serialize(grant.TokenRoles),JsonClaimValueTypes.JsonArray),
            new(AccessTokenClaims.AccountType,grant.AccountType.ToString()),
            new(AccessTokenClaims.Permissions,JsonSerializer.Serialize(grant.Permissions),JsonClaimValueTypes.JsonArray)
            ];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.key));

        var singingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);


        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: singingCredentials
            );

        return (token: new JwtSecurityTokenHandler().WriteToken(token), expireIn: _options.ExpiryMinutes * 60);
    }

    public string? validateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.key));

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                IssuerSigningKey = key,
                ValidateIssuerSigningKey = true,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            return jwtToken.Claims.First(x => x.Type == JwtRegisteredClaimNames.Sub).Value;
        }
        catch
        {
            return null;
        }

    }


}
