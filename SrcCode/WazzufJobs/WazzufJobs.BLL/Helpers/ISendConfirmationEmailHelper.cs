using WazzufJobs.DAL.Entities;

namespace WazzufJobs.BLL.Helpers;
public interface ISendConfirmationEmailHelper
{
    Task sendConfirmationEmail(AppUser user, string code);
}
