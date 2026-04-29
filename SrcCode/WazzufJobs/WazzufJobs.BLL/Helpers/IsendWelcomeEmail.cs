using WazzufJobs.DAL.Entities;

namespace WazzufJobs.BLL.Helpers;
public interface IsendWelcomeEmail
{
    Task sendEmail(AppUser user);
}
