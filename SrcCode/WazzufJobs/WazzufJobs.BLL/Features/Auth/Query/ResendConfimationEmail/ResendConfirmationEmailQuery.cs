namespace WazzufJobs.BLL.Features.Auth.Query.ResendConfimationEmail;
public record ResendConfirmationEmailQuery(ResendConfirmationEmailRequest emailRequest) : IRequest<Result>;
