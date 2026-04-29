namespace WazzufJobs.BLL.Features.Auth.Query.GetRefreshToken;
public record GetRefreshTokenQuery(string token, string refreshToken) : IRequest<Result<loginResponseDTO>>;
