using System.Security.Cryptography;

namespace WazzufJobs.BLL.Helpers;
public static class GenerateRefreshTokenHelper
{
    public static string GenerateRefreshToken()
    => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

}

