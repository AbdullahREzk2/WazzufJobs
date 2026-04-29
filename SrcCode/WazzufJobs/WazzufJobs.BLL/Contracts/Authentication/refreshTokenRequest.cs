namespace WazzufJobs.BLL.Contracts.Authentication;
public record refreshTokenRequest(
    string Token,
    string RefreshToken
    );
