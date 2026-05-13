namespace WazzufJobs.BLL.Services;
public interface ICVTextExtractor
{
    Task<string?> ExtractTextAsync(string cvUrl, CancellationToken cancellationToken);
}
