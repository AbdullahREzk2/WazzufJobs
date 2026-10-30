namespace WazzufJobs.BLL.Contracts.Users;
public record UserResponse(
    string Id,
    string firstName,
    string lastName,
    string Email,
    bool isDisabled,
    IEnumerable<string> Roles,
    string? ProfilePhotoUrl = null,
    string? CvUrl = null,
    decimal? DesiredSalary = null,
    bool? ShowSalary = null
);
