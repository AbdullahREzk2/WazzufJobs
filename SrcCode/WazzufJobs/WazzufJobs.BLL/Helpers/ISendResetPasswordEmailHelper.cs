namespace WazzufJobs.BLL.Helpers;
public interface ISendResetPasswordEmailHelper
{
    Task sendResetPasswordEmail(AppUser user, string code);
}
