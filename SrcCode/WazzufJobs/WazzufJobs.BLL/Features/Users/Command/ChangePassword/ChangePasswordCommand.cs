
namespace WazzufJobs.BLL.Features.Users.Command.ChangePassword;
public record ChangePasswordCommand(string userId, ChangePasswordRequest Passrequest) : IRequest<Result>;
