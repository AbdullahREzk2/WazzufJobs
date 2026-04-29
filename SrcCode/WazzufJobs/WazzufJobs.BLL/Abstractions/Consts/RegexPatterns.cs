namespace WazzufJobs.BLL.Abstractions.Consts;
public static class RegexPatterns
{
    public const string Password = "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[!@#$%^&*()_\\-+=<>?{}[\\]~]).{8,}$";
}
