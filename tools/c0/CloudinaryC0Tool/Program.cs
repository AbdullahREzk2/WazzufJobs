using System.Globalization;
using System.Text;
using System.Text.Json;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// WazzufJobs Phase C0 - Cloudinary operator tool
//
//   inventory  READ-ONLY. Lists raw assets (CVs) and optionally joins them with the CVs table of a
//              LOCALLY RESTORED database backup. Never uploads, renames or deletes anything.
//   spike      Synthetic test of authenticated storage. Creates synthetic PDFs (no personal data) under
//              "wazzuf-jobs/c0-spike/", tests access, then deletes only the assets it created in this run.
//
// Credentials are read from environment variables (CLOUDINARY_CLOUD_NAME / CLOUDINARY_API_KEY /
// CLOUDINARY_API_SECRET) or from the API project's user-secrets (CloudinarySettings:*). They are never printed.

var command = args.FirstOrDefault()?.ToLowerInvariant();
var options = ParseOptions(args.Skip(1).ToArray());

var config = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Marker).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();

var cloudName = config["CLOUDINARY_CLOUD_NAME"] ?? config["CloudinarySettings:CloudName"];
var apiKey = config["CLOUDINARY_API_KEY"] ?? config["CloudinarySettings:ApiKey"];
var apiSecret = config["CLOUDINARY_API_SECRET"] ?? config["CloudinarySettings:ApiSecret"];

if (command is not ("inventory" or "spike"))
{
    Console.WriteLine("""
        Usage:
          dotnet run -- inventory [--db "<connection string to LOCAL restored backup>"] [--out <folder>] [--probe-public]
          dotnet run -- spike [--out <folder>]
        """);
    return 1;
}

if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
{
    Console.Error.WriteLine("Cloudinary credentials not found (env vars or user-secrets). Nothing was done.");
    return 2;
}

var cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret)) { Api = { Secure = true } };

// Test mode only: point the SDK at a LOCAL fake Cloudinary server to verify this tool without a real account.
// Restricted to localhost so real credentials can never be sent to any other host.
var testBase = Environment.GetEnvironmentVariable("C0_FAKE_CLOUDINARY_BASE");
if (!string.IsNullOrEmpty(testBase))
{
    if (!Uri.TryCreate(testBase, UriKind.Absolute, out var testUri) || !(testUri.IsLoopback))
    {
        Console.Error.WriteLine("C0_FAKE_CLOUDINARY_BASE must be a localhost URL. Nothing was done.");
        return 2;
    }
    cloudinary.Api.ApiBaseAddress = testBase.TrimEnd('/');
    Console.WriteLine($"[TEST MODE] using fake Cloudinary at {cloudinary.Api.ApiBaseAddress}");
}
var outDir = options.GetValueOrDefault("out")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WazzufJobs-C0");
Directory.CreateDirectory(outDir);
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

return command == "inventory"
    ? await RunInventoryAsync()
    : await RunSpikeAsync();

// ─────────────────────────────────────────────────────────────────────────────
async Task<int> RunInventoryAsync()
{
    Console.WriteLine($"[inventory] read-only listing of raw assets in cloud '{Mask(cloudName!)}'");

    var assets = new List<Resource>();
    foreach (var type in new[] { "upload", "authenticated", "private" })
    {
        string? cursor = null;
        do
        {
            var page = await cloudinary.ListResourcesAsync(new ListResourcesParams
            {
                ResourceType = ResourceType.Raw,
                Type = type,
                MaxResults = 500,
                NextCursor = cursor
            });

            if (page.Error is not null)
            {
                Console.Error.WriteLine($"[inventory] listing type '{type}' failed: {page.Error.Message}");
                return 3;
            }

            assets.AddRange(page.Resources);
            cursor = page.NextCursor;
        } while (!string.IsNullOrEmpty(cursor));
    }

    var cvRows = new Dictionary<string, (string UserId, string Url, string Domain, DateTime UploadedAt)>(StringComparer.Ordinal);
    if (options.TryGetValue("db", out var connectionString))
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = new SqlCommand("""
            SELECT c.UserId, c.PublicId, c.Url, c.UploadedAt,
                   LOWER(SUBSTRING(u.Email, CHARINDEX('@', u.Email) + 1, 255)) AS Domain
            FROM dbo.CVs c LEFT JOIN dbo.AspNetUsers u ON u.Id = c.UserId
            """, connection);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            cvRows[reader.GetString(1)] = (reader.GetString(0), reader.GetString(2),
                reader.IsDBNull(4) ? "" : reader.GetString(4), reader.GetDateTime(3));
        Console.WriteLine($"[inventory] {cvRows.Count} CV rows read from the restored database (read-only SELECT).");
    }
    else
    {
        Console.WriteLine("[inventory] no --db given: DB-referenced vs orphaned cannot be determined.");
    }

    var probe = options.ContainsKey("probe-public");
    var manifestPath = Path.Combine(outDir, $"cloudinary-cv-manifest-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    var sb = new StringBuilder("public_id,type,format,bytes,created_at,db_referenced,db_user_id,user_email_domain,anonymous_head_status,proposed_action\n");

    foreach (var a in assets.OrderBy(a => a.CreatedAt))
    {
        var referenced = cvRows.TryGetValue(a.PublicId, out var row);
        var headStatus = "";
        if (probe && a.Type == "upload" && a.SecureUrl is not null)
        {
            // HEAD only: checks reachability without downloading the document
            using var head = new HttpRequestMessage(System.Net.Http.HttpMethod.Head, a.SecureUrl);
            using var response = await http.SendAsync(head);
            headStatus = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        }

        sb.AppendLine(string.Join(',',
            Csv(a.PublicId), a.Type, a.Format, a.Bytes.ToString(CultureInfo.InvariantCulture),
            string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-ddTHH:mm:ssZ}", a.CreatedAt),
            cvRows.Count == 0 ? "unknown" : referenced ? "yes" : "NO (orphan)",
            referenced ? row.UserId : "", referenced ? Csv(row.Domain) : "", headStatus, ""));
    }

    await File.WriteAllTextAsync(manifestPath, sb.ToString());

    Console.WriteLine();
    Console.WriteLine("[inventory] summary (no asset was modified):");
    foreach (var g in assets.GroupBy(a => a.Type))
        Console.WriteLine($"  type={g.Key,-14} assets={g.Count(),5}  bytes={g.Sum(x => x.Bytes),12:N0}");
    if (cvRows.Count > 0)
    {
        Console.WriteLine($"  referenced by CVs table : {assets.Count(a => cvRows.ContainsKey(a.PublicId))}");
        Console.WriteLine($"  orphaned (no CV row)    : {assets.Count(a => !cvRows.ContainsKey(a.PublicId))}");
        Console.WriteLine($"  CV rows without asset   : {cvRows.Keys.Count(k => assets.All(a => a.PublicId != k))}");
    }
    if (probe)
        Console.WriteLine("  anonymous HEAD results are in the manifest (anonymous_head_status).");
    Console.WriteLine($"[inventory] manifest: {manifestPath}");
    Console.WriteLine("[inventory] the manifest may contain user ids; keep it local and do not commit it.");
    return 0;
}

// ─────────────────────────────────────────────────────────────────────────────
async Task<int> RunSpikeAsync()
{
    const string folder = "wazzuf-jobs/c0-spike";
    var created = new List<(string PublicId, string Type)>();
    var results = new List<(string Check, bool Pass, string Detail)>();
    var pdf = SyntheticPdf("WazzufJobs C0 synthetic test document - no personal data");

    Console.WriteLine($"[spike] synthetic test in folder '{folder}' (cloud '{Mask(cloudName!)}')");
    try
    {
        // 1. authenticated upload
        var auth = await cloudinary.UploadLargeRawAsync(new RawUploadParams
        {
            File = new FileDescription("synthetic.pdf", new MemoryStream(pdf)),
            Folder = folder, Type = "authenticated", UseFilename = false, UniqueFilename = true, Overwrite = false
        });
        if (auth.Error is not null) throw new InvalidOperationException("authenticated upload failed: " + auth.Error.Message);
        created.Add((auth.PublicId, auth.Type));
        results.Add(("authenticated upload stored with type=authenticated", auth.Type == "authenticated", $"type={auth.Type}"));

        // 2. anonymous access to the authenticated asset must fail
        var anonymous = await GetStatusAsync(auth.SecureUrl?.ToString());
        results.Add(("anonymous GET of authenticated asset is refused", anonymous is not 200, $"HTTP {anonymous}"));

        // 3. signed server-side download must succeed (the mechanism used by GET api/cv/file)
        var signed = cloudinary.DownloadPrivate(auth.PublicId, attachment: true, format: "", type: "authenticated",
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds(), resourceType: "raw");
        var (signedStatus, signedBytes) = await GetBytesAsync(signed);
        results.Add(("signed API download of authenticated asset works", signedStatus == 200 && IsPdf(signedBytes),
            $"HTTP {signedStatus}, {signedBytes.Length} bytes"));

        // 4. remediation rehearsal: public synthetic asset -> rename to_type=authenticated + invalidate
        var pub = await cloudinary.UploadLargeRawAsync(new RawUploadParams
        {
            File = new FileDescription("synthetic.pdf", new MemoryStream(pdf)),
            Folder = folder, Type = "upload", UseFilename = false, UniqueFilename = true, Overwrite = false
        });
        if (pub.Error is not null) throw new InvalidOperationException("public synthetic upload failed: " + pub.Error.Message);
        created.Add((pub.PublicId, "upload"));
        var publicUrl = pub.SecureUrl?.ToString();
        var publicBefore = await GetStatusAsync(publicUrl);
        results.Add(("(info) anonymous GET of a PUBLIC raw PDF before remediation", true,
            $"HTTP {publicBefore} (200 means public PDFs are currently deliverable on this account)"));

        // legacy path used by the C0 API for not-yet-remediated CVs: signed download of a type=upload raw asset
        var signedLegacy = cloudinary.DownloadPrivate(pub.PublicId, attachment: true, format: "", type: "upload",
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds(), resourceType: "raw");
        var (legacyStatus, legacyBytes) = await GetBytesAsync(signedLegacy);
        results.Add(("signed API download of a legacy public (type=upload) asset works", legacyStatus == 200 && IsPdf(legacyBytes),
            $"HTTP {legacyStatus}"));

        var renamed = await cloudinary.RenameAsync(new RenameParams(pub.PublicId, pub.PublicId)
        {
            ResourceType = ResourceType.Raw, Type = "upload", ToType = "authenticated", Invalidate = true
        });
        var renameOk = renamed.Error is null;
        if (renameOk) { created.RemoveAll(c => c.PublicId == pub.PublicId); created.Add((pub.PublicId, "authenticated")); }
        results.Add(("rename upload -> authenticated (to_type + invalidate)", renameOk, renamed.Error?.Message ?? "ok"));

        await Task.Delay(TimeSpan.FromSeconds(20));
        var publicAfter = await GetStatusAsync(publicUrl);
        results.Add(("old public URL no longer serves the file (after ~20 s)", publicAfter is not 200,
            $"HTTP {publicAfter} (CDN invalidation can take a few minutes; re-check later if 200)"));

        var signedRenamed = cloudinary.DownloadPrivate(pub.PublicId, attachment: true, format: "", type: "authenticated",
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds(), resourceType: "raw");
        var (renamedStatus, renamedBytes) = await GetBytesAsync(signedRenamed);
        results.Add(("converted asset still retrievable via signed API download", renamedStatus == 200 && IsPdf(renamedBytes),
            $"HTTP {renamedStatus}"));
    }
    catch (Exception ex)
    {
        results.Add(("spike execution", false, ex.Message));
    }
    finally
    {
        // cleanup: only the synthetic assets created by this run, all inside the spike folder
        foreach (var (publicId, type) in created.Where(c => c.PublicId.StartsWith(folder + "/", StringComparison.Ordinal)))
        {
            var destroyed = await cloudinary.DestroyAsync(new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Raw, Type = type, Invalidate = true
            });
            results.Add(($"cleanup synthetic asset ({type})", destroyed.Result == "ok", destroyed.Result));
        }
    }

    Console.WriteLine();
    foreach (var (check, pass, detail) in results)
        Console.WriteLine($"  [{(pass ? "PASS" : "FAIL")}] {check} - {detail}");

    var reportPath = Path.Combine(outDir, $"cloudinary-spike-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(
        results.Select(r => new { r.Check, r.Pass, r.Detail }), new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"[spike] report: {reportPath}");

    return results.All(r => r.Pass) ? 0 : 4;
}

// ─────────────────────────────────────────────────────────────────────────────
async Task<int> GetStatusAsync(string? url)
{
    if (string.IsNullOrEmpty(url)) return -1;
    using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
    return (int)response.StatusCode;
}

async Task<(int Status, byte[] Bytes)> GetBytesAsync(string url)
{
    using var response = await http.GetAsync(url);
    var bytes = response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : [];
    return ((int)response.StatusCode, bytes);
}

static bool IsPdf(byte[] bytes) => bytes.Length > 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;

static string Mask(string value) => value.Length <= 4 ? "****" : value[..2] + new string('*', value.Length - 4) + value[^2..];

static string Csv(string value) => value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

static Dictionary<string, string> ParseOptions(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) continue;
        var key = args[i][2..];
        var value = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
        result[key] = value;
    }
    return result;
}

static byte[] SyntheticPdf(string text)
{
    var content = $"BT /F1 14 Tf 72 700 Td ({text}) Tj ET";
    string[] objects =
    [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
        $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
    ];
    var sb = new StringBuilder("%PDF-1.4\n");
    var offsets = new List<int>();
    for (var i = 0; i < objects.Length; i++) { offsets.Add(sb.Length); sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
    var xref = sb.Length;
    sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
    foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
    sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    return Encoding.ASCII.GetBytes(sb.ToString());
}

internal sealed class Marker;
