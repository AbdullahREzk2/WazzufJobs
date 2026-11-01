using System.Diagnostics;
using System.Net;
using WazzufJobs.Shared;

namespace WazzufJobs.API.Setting;

/// <summary>Audit facts of the current request. The address is the TCP peer only; forwarded headers are never read.</summary>
public sealed class HttpRequestInfo(IHttpContextAccessor accessor) : IRequestInfo
{
    public bool HasRequest => accessor.HttpContext is not null;

    public IPAddress? RemoteIpAddress => accessor.HttpContext?.Connection.RemoteIpAddress;

    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null;

    public string? CorrelationId =>
        Activity.Current?.TraceId.ToHexString() is { } traceId && traceId != default(ActivityTraceId).ToHexString()
            ? traceId
            : accessor.HttpContext?.TraceIdentifier;
}
