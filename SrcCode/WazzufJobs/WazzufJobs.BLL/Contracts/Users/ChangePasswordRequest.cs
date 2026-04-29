namespace WazzufJobs.BLL.Contracts.Users;
public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
    );

