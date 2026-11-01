using System.Text.Json;
using System.Text.RegularExpressions;

namespace WazzufJobs.BLL.Audit;

/// <summary>
/// The explicit allowlist for audit details (Phase 02, S5). A key that is not listed is dropped. A value is kept only
/// if it is a boolean, a number, an enum, or a short plain token (letters, digits and <c>_ . : -</c>, at most 64
/// characters; so no email address, sentence, path, token or URL), or a short list of those. Only <c>route</c> may
/// also contain <c>/ { }</c> (an endpoint template), never <c>://</c>. Everything else is dropped and counted in
/// <c>dropped</c>.
/// </summary>
public static partial class AuditDetails
{
    public static IReadOnlySet<string> AllowedKeys { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "reason",           // fixed reason code (e.g. invalid_password, User.LastAdministrator)
        "roles", "rolesAdded", "rolesRemoved",
        "emailChanged",
        "from", "to",       // status or role transitions
        "role",
        "permission", "permissions",
        "method", "route",  // endpoint template, never the concrete path or query
        "newAccount",
        "revokedTokens"
    };

    private const int MaxList = 10;

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,64}$")]
    private static partial Regex PlainToken();

    [GeneratedRegex("^[A-Za-z0-9_.:/{}-]{1,100}$")]
    private static partial Regex RouteTemplate();

    public static string? Serialize(IReadOnlyDictionary<string, object?>? details)
    {
        if (details is null || details.Count == 0)
            return null;

        var kept = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var dropped = 0;
        foreach (var (key, value) in details)
        {
            if (AllowedKeys.Contains(key) && (key == "route" ? Route(value) : Normalize(value)) is { } safe)
                kept[key] = safe;
            else
                dropped++;
        }
        if (dropped > 0)
            kept["dropped"] = dropped;

        return kept.Count == 0 ? null : JsonSerializer.Serialize(kept);
    }

    private static object? Normalize(object? value) => value switch
    {
        null => null,
        bool b => b,
        int or long or short or byte => Convert.ToInt64(value),
        Enum e => Plain(e.ToString()),
        string s => Plain(s),
        IEnumerable<string> list => NormalizeList(list),
        _ => null
    };

    private static string? Plain(string s) => PlainToken().IsMatch(s) ? s : null;

    private static string? Route(object? value) =>
        value is string s && RouteTemplate().IsMatch(s) && !s.Contains("//", StringComparison.Ordinal) ? s : null;

    private static string[]? NormalizeList(IEnumerable<string> list)
    {
        var items = list.Take(MaxList + 1).ToList();
        if (items.Count > MaxList)
            return null;
        var plain = items.Select(Plain).ToList();
        return plain.Any(p => p is null) ? null : plain.Cast<string>().ToArray();
    }
}
