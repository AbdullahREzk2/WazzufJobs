namespace WazzufJobs.BLL.Helpers;
public interface IsendWelcomeEmail
{
    Task sendEmail(AppUser user);
}
