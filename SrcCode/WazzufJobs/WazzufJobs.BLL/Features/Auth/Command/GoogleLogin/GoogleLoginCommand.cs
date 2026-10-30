namespace WazzufJobs.BLL.Features.Auth.Command.GoogleLogin;

public record GoogleLoginCommand(string IdToken) : IRequest<Result<loginResponseDTO>>;
