namespace WazzufJobs.BLL.Features.Auth.Command.ConfirmEmail;
public record ConfirmEmailCommand(ConfirmEmailRequestDTO emailRequest) : IRequest<Result>;
