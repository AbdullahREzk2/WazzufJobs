namespace WazzufJobs.BLL.Services;
public interface IAIScoringService
{
    Task ScoreApplicationAsync(int applicationId, CancellationToken cancellationToken);
}
