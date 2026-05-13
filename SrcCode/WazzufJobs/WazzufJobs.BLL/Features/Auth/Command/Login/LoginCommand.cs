namespace WazzufJobs.BLL.Features.Auth.Command.Login;
public record LoginCommand(string Email, string Password) : IRequest<Result<loginResponseDTO>>;
