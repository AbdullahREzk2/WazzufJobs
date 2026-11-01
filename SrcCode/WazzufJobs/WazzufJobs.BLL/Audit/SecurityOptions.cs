namespace WazzufJobs.BLL.Audit;

/// <summary>
/// <c>Security</c> configuration (Phase 02, D-CONFIG). <see cref="DataKey"/> is a server secret set by the owner
/// (32 random bytes, base64); it is never committed. Subkeys are derived from it per purpose (HKDF).
/// </summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string? DataKey { get; init; }

    /// <summary>The decoded key, or null when it is missing or not exactly 32 bytes of base64.</summary>
    public byte[]? TryDecodeDataKey()
    {
        if (string.IsNullOrWhiteSpace(DataKey))
            return null;
        try
        {
            var bytes = Convert.FromBase64String(DataKey);
            return bytes.Length == 32 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
