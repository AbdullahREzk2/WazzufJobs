namespace WazzufJobs.BLL.Abstractions;
public record Error(string Code, string Message, int? StatusCode)
{
    public static readonly Error None = new(string.Empty, string.Empty, null);
}
