using System.Net;
using System.Security.Cryptography;

namespace WazzufJobs.BLL.Audit;

/// <summary>Turns a client address into a keyed hash; the plaintext address is never stored (Phase 02, D-AUDIT).</summary>
public interface IIpHasher
{
    byte[]? Hash(IPAddress? address);
}

/// <summary>
/// HMAC-SHA256 with a subkey derived from <c>Security:DataKey</c> (HKDF, purpose-specific info). The same address
/// always gives the same 32-byte value, so entries can be correlated, but the address cannot be recovered without the
/// server key. IPv4-mapped IPv6 addresses are hashed as IPv4.
/// </summary>
public sealed class HmacIpHasher : IIpHasher
{
    private static readonly byte[] Purpose = "WazzufJobs/audit/ip-hmac/v1"u8.ToArray();
    private readonly byte[] _key;

    public HmacIpHasher(byte[] dataKey)
    {
        if (dataKey.Length != 32)
            throw new ArgumentException("The data key must be 32 bytes.", nameof(dataKey));
        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, dataKey, 32, salt: [], info: Purpose);
    }

    public byte[]? Hash(IPAddress? address)
    {
        if (address is null)
            return null;
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        return HMACSHA256.HashData(_key, address.GetAddressBytes());
    }
}
