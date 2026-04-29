namespace WazzufJobs.BLL.Contracts.Users;
public class UserProfileResponse
{
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ProfilePhotoUrl { get; set; } = string.Empty;
}