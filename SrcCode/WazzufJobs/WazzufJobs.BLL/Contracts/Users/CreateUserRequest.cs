namespace WazzufJobs.BLL.Contracts.Users;
public class CreateUserRequest
{
    public string firstName { get; set; } = default!;
    public string lastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public IList<string> Roles { get; set; }
}
