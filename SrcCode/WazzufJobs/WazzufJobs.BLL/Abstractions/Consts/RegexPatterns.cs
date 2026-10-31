namespace WazzufJobs.BLL.Abstractions.Consts;
public static class RegexPatterns
{
    // Mirrors the ASP.NET Identity password policy exactly (length >= 8, ASCII lower, upper and digit, and at least one
    // character that is not an ASCII letter or digit), so the validator never rejects a password Identity accepts.
    // Phase 02 S1: the earlier list of allowed special characters was narrower than Identity's rule.
    public const string Password = "^(?=[\\s\\S]*[a-z])(?=[\\s\\S]*[A-Z])(?=[\\s\\S]*[0-9])(?=[\\s\\S]*[^a-zA-Z0-9])[\\s\\S]{8,}$";
}
