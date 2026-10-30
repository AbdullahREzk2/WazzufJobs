using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Setting;

namespace WazzufJobs.API.Services;

/// <summary>
/// Phase C0 telemetry (VER-03): records process start/stop times so the hosting idle-shutdown and
/// recycle behaviour can be measured. Writes timestamps only (no request data, no personal data)
/// to App_Data/lifecycle.log, and also to the regular logger.
/// Also records, once per process, whether the client address seen by the app looks like a proxy
/// (no IP address is stored) so the per-IP rate limit can be interpreted.
/// </summary>
public static class LifecycleLog
{
    private static readonly object Sync = new();
    private static int _firstRequestLogged;

    public static void Register(WebApplication app)
    {
        var cvUploadsEnabled = app.Services.GetRequiredService<IOptions<FeatureSettings>>().Value.CvUploadsEnabled;
        // Google sign-in is disabled (HTTP 503, no call to Google) while Google:ClientId is not configured
        var googleSignInEnabled = !string.IsNullOrWhiteSpace(app.Configuration["Google:ClientId"]);
        app.Logger.LogInformation("Feature state: CV uploads enabled = {CvUploadsEnabled}; Google sign-in enabled = {GoogleSignInEnabled}.",
            cvUploadsEnabled, googleSignInEnabled);

        if (!app.Configuration.GetValue("Diagnostics:LifecycleLogEnabled", true))
            return;

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Lifecycle");
        var path = Path.Combine(app.Environment.ContentRootPath, "App_Data", "lifecycle.log");
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();

        void Write(string evt)
        {
            var uptime = DateTime.UtcNow - processStart;
            var line = $"{DateTime.UtcNow:O}\t{evt}\tpid={Environment.ProcessId}\tuptime={uptime:c}";
            logger.LogInformation("Lifecycle {Event} pid={Pid} uptime={Uptime}", evt, Environment.ProcessId, uptime);

            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Lifecycle log file could not be written.");
            }
        }

        app.Lifetime.ApplicationStarted.Register(() => Write($"started\tcvUploadsEnabled={cvUploadsEnabled}\tgoogleSignInEnabled={googleSignInEnabled}"));
        app.Lifetime.ApplicationStopping.Register(() => Write("stopping"));
        app.Lifetime.ApplicationStopped.Register(() => Write("stopped"));

        app.Use(async (context, next) =>
        {
            if (Interlocked.Exchange(ref _firstRequestLogged, 1) == 0)
            {
                var address = context.Connection.RemoteIpAddress;
                var addressClass = address is null ? "none"
                    : IPAddress.IsLoopback(address) ? "loopback"
                    : IsPrivate(address) ? "private"
                    : "public";
                var forwarded = context.Request.Headers.ContainsKey("X-Forwarded-For");
                Write($"first-request\tremoteAddressClass={addressClass}\txForwardedForPresent={forwarded}");
            }

            await next();
        });
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;

        var b = address.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
    }
}
