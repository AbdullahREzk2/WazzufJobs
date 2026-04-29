using WazzufJobs.DAL.Entities;

namespace WazzufJobs.BLL.Authentication;
public interface IJwtProvider
{
    (string token, int expireIn) GenerateToken(AppUser user, IEnumerable<string> roles, IEnumerable<string> permissions);
    string? validateToken(string token);
}
