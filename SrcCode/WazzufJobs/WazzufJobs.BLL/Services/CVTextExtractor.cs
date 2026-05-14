using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace WazzufJobs.BLL.Services;

public class CVTextExtractor(
    HttpClient httpClient,
    ILogger<CVTextExtractor> logger) : ICVTextExtractor
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger _logger = logger;

    public async Task<string?> ExtractTextAsync(string cvUrl,CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("CVExtractor: Downloading PDF from {Url}.", cvUrl);

            var pdfBytes = await _httpClient.GetByteArrayAsync(cvUrl, cancellationToken);

            _logger.LogInformation("CVExtractor: Downloaded {Size} bytes.", pdfBytes.Length);

            using var document = PdfDocument.Open(pdfBytes);
            var text = new StringBuilder();

            foreach (Page page in document.GetPages())
                text.AppendLine(page.Text);

            var result = text.ToString().Trim();

            _logger.LogInformation("CVExtractor: Extracted {Length} characters.", result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CVExtractor: Failed to extract text from {Url}.", cvUrl);
            return null;
        }
    }
}