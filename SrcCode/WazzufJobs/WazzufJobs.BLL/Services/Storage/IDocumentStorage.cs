namespace WazzufJobs.BLL.Services.Storage;

/// <summary>
/// Provider-neutral storage for private documents (CVs). Implementations must never create publicly
/// deliverable files and must only hand file content to the application server (no client URLs).
/// The provider is selected with configuration key "Storage:DocumentProvider".
/// </summary>
public interface IDocumentStorage
{
    /// <summary>Name of the configured provider (e.g. "Cloudinary").</summary>
    string ProviderName { get; }

    /// <summary>Stores the content privately. Returns null if the provider failed or did not store it privately.</summary>
    Task<StoredDocument?> StorePrivateAsync(Stream content, string folder, CancellationToken cancellationToken = default);

    /// <summary>Reads a stored document server-side. Returns null when unavailable.</summary>
    Task<byte[]?> ReadAsync(string key, string accessType, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string key, string accessType, CancellationToken cancellationToken = default);
}

/// <param name="Key">Provider key of the document (stored in CVs.PublicId).</param>
/// <param name="AccessType">Provider access/delivery type (e.g. "authenticated").</param>
/// <param name="ProviderReference">Provider reference kept server-side only (stored in CVs.Url); never returned to clients.</param>
public record StoredDocument(string Key, string AccessType, string ProviderReference);
