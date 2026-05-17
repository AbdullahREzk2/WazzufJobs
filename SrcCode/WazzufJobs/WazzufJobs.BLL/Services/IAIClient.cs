namespace WazzufJobs.BLL.Services;
public interface IAIClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken);
}