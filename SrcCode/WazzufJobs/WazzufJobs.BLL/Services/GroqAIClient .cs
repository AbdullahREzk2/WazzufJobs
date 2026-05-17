namespace WazzufJobs.BLL.Services;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Contracts.Applications;
using WazzufJobs.BLL.Settings;

public class GroqAIClient : IAIClient
{
    private readonly HttpClient _httpClient;
    private readonly AISettings _settings;

    public GroqAIClient(HttpClient httpClient, IOptions<AISettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _settings.Model, // llama3-8b-8192
            messages = new[]
            {
            new
            {
                role = "user",
                content = prompt
            }
        },
            temperature = 0.2
        };

        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            _settings.BaseUrl);

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Groq Error: {responseText}");
        }

        var result = JsonSerializer.Deserialize<GroqResponse>(responseText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result?.choices?.FirstOrDefault()?.message?.content ?? "";
    }
}