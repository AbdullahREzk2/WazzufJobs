using System.Net;

namespace WazzufJobs.Shared;

/// <summary>Facts about the current HTTP request needed by the audit log (Phase 02, S5). Never read from forwarded headers.</summary>
public interface IRequestInfo
{
    /// <summary>False outside an HTTP request (background work): audit entries are then recorded as System.</summary>
    bool HasRequest { get; }

    /// <summary>The TCP peer address (D-PROXY-IP v2: never a forwarded header).</summary>
    IPAddress? RemoteIpAddress { get; }

    string? UserAgent { get; }

    string? CorrelationId { get; }
}
