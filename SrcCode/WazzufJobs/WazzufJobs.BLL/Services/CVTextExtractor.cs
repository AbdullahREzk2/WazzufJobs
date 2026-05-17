// WazzufJobs.BLL/Services/CVTextExtractor.cs
using System.Text;
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
            _logger.LogInformation("CVExtractor: Downloading from {Url}.", cvUrl);

            // force follow redirects and set proper headers
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, cvUrl);
            requestMessage.Headers.Add("User-Agent", "Mozilla/5.0");
            requestMessage.Headers.Add("Accept", "application/pdf,*/*");

            var httpResponse = await _httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            _logger.LogInformation("CVExtractor: HTTP status = {Status}.",
                httpResponse.StatusCode);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("CVExtractor: Failed to download. Status={Status}.",
                    httpResponse.StatusCode);
                return null;
            }

            var contentType = httpResponse.Content.Headers.ContentType?.MediaType;
            _logger.LogInformation("CVExtractor: Content-Type = {ContentType}.", contentType);

            var pdfBytes = await httpResponse.Content.ReadAsByteArrayAsync(cancellationToken);

            _logger.LogInformation("CVExtractor: Downloaded {Size} bytes.", pdfBytes.Length);

            // verify it's actually a PDF (starts with %PDF)
            if (pdfBytes.Length < 4 ||
                pdfBytes[0] != 0x25 || // %
                pdfBytes[1] != 0x50 || // P
                pdfBytes[2] != 0x44 || // D
                pdfBytes[3] != 0x46)   // F
            {
                _logger.LogWarning("CVExtractor: Downloaded bytes are not a valid PDF.");
                return null;
            }

            using var document = PdfDocument.Open(pdfBytes);
            var text = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                // try Page.Text first
                var pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    text.AppendLine(pageText);
                    continue;
                }

                // fallback to word-by-word extraction
                var words = page.GetWords();
                foreach (var word in words)
                    text.Append(word.Text + " ");

                text.AppendLine();
            }

            var result = text.ToString().Trim();

            _logger.LogInformation("CVExtractor: Extracted {Length} characters.", result.Length);

            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CVExtractor: Exception for {Url}. {Message}",
                cvUrl, ex.Message);
            return null;
        }
    }

    public string? ExtractFromBytes(byte[] pdfBytes)
    {
        try
        {
            using var document = PdfDocument.Open(pdfBytes);
            var text = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                var pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    text.AppendLine(pageText);
                    continue;
                }

                foreach (var word in page.GetWords())
                    text.Append(word.Text + " ");

                text.AppendLine();
            }

            var result = text.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CVExtractor: Failed to extract from bytes.");
            return null;
        }
    }


}