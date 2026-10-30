namespace WazzufJobs.BLL.Contracts.Users;
public record UserResponse(
    string Id,
    string firstName,
    string lastName,
    string Email,
    bool isDisabled,
    IEnumerable<string> Roles,
    string? ProfilePhotoUrl = null,
    bool? HasCv = null,
    decimal? DesiredSalary = null,
    bool? ShowSalary = null
);
