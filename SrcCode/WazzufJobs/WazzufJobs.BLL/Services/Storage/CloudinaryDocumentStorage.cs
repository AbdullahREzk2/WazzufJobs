using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Setting;

namespace WazzufJobs.BLL.Services.Storage;

/// <summary>
/// Cloudinary implementation of <see cref="IDocumentStorage"/>: documents are raw assets of type
/// "authenticated" (no public delivery URL) and are read back through 60-second signed API downloads.
/// </summary>
public class CloudinaryDocumentStorage : IDocumentStorage
{
    public const string HttpClientName = "CloudinaryDocuments";
    private const int MaxDownloadBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan SignedUrlLifetime = TimeSpan.FromSeconds(60);

    private readonly Cloudinary _cloudinary;
    private readonly IHttpClientFactory _httpClientFactory;

    public CloudinaryDocumentStorage(IOptions<CloudinarySettings> options, IHttpClientFactory httpClientFactory)
    {
        var settings = options.Value;
        _cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
        _cloudinary.Api.Secure = true;
        _httpClientFactory = httpClientFactory;
    }

    public string ProviderName => "Cloudinary";

    public async Task<StoredDocument?> StorePrivateAsync(Stream content, string folder, CancellationToken cancellationToken = default)
    {
        var result = await _cloudinary.UploadLargeRawAsync(BuildPrivateRawUploadParams(content, folder));

        if (result.Error is not null)
            return null;

        // Defence in depth: never accept a stored asset that is not authenticated.
        if (!string.Equals(result.Type, CvStorage.AuthenticatedType, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteAsync(result.PublicId, result.Type ?? CvStorage.PublicUploadType, cancellationToken);
            return null;
        }

        return new StoredDocument(result.PublicId, CvStorage.AuthenticatedType, result.SecureUrl?.ToString() ?? string.Empty);
    }

    public async Task<byte[]?> ReadAsync(string key, string accessType, CancellationToken cancellationToken = default)
    {
        var signedUrl = _cloudinary.DownloadPrivate(
            key,
            attachment: true,
            format: string.Empty,
            type: accessType,
            expiresAt: DateTimeOffset.UtcNow.Add(SignedUrlLifetime).ToUnixTimeSeconds(),
            resourceType: CvStorage.RawResourceType);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(signedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > MaxDownloadBytes)
            return null;

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return bytes.Length > MaxDownloadBytes ? null : bytes;
    }

    public async Task<bool> DeleteAsync(string key, string accessType, CancellationToken cancellationToken = default)
    {
        var result = await _cloudinary.DestroyAsync(new DeletionParams(key)
        {
            ResourceType = ResourceType.Raw,
            Type = accessType,
            Invalidate = true
        });
        return result.Result == "ok";
    }

    public static RawUploadParams BuildPrivateRawUploadParams(Stream content, string folder) => new()
    {
        // The original client filename is never sent: Cloudinary generates a random public id.
        File = new FileDescription("cv.pdf", content),
        Folder = folder,
        Type = CvStorage.AuthenticatedType,
        UseFilename = false,
        UniqueFilename = true,
        Overwrite = false
    };
}
