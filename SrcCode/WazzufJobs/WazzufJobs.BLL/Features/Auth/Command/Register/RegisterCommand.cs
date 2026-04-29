namespace WazzufJobs.BLL.Features.Auth.Command.Register;
public record RegisterCommand(RegisterRequestDTO requestDTO) : IRequest<Result>;
