using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace WazzufJobs.BLL.Services;

public class CVTextExtractor(HttpClient httpClient) : ICVTextExtractor
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<string?> ExtractTextAsync(string cvUrl,CancellationToken cancellationToken)
    {
        try
        {
            var pdfBytes = await _httpClient.GetByteArrayAsync(cvUrl, cancellationToken);

            using var document = PdfDocument.Open(pdfBytes);
            var text = new StringBuilder();

            foreach (Page page in document.GetPages())
                text.AppendLine(page.Text);

            return text.ToString().Trim();
        }
        catch
        {
            return null;
        }
    }
}