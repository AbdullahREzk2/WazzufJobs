namespace WazzufJobs.BLL.Contracts.Authentication;
public record loginResponseDTO(
    string Id,
    string Email,
    string UserName,
    string firstName,
    string lastName,
    string Token,
    int ExpiresIn,
    string RefreshToken,
    DateTime RefreshTokenExpiration
  );
