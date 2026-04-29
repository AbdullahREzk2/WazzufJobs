namespace WazzufJobs.BLL.Features.Auth.Command.RevokeRefreshToken;
public record RevokeRefreshTokenCommand(string token, string refreshToken) : IRequest<Result>;
