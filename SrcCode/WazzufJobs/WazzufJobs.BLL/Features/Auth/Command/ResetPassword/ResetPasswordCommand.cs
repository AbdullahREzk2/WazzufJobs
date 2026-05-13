namespace WazzufJobs.BLL.Features.Auth.Command.ResetPassword;
public record ResetPasswordCommand(ResetPasswordRequest passRequest) : IRequest<Result>;
