namespace WazzufJobs.BLL.Features.Auth.Query.SendResetPassword;
public record SendResetPasswordQuery(ForgetPasswordRequest passRequest) : IRequest<Result>;
