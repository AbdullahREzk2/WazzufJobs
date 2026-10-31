using FluentValidation.Results;

namespace WazzufJobs.BLL.Errors;

/// <summary>
/// Converts validation failures into the field-level <c>errors</c> map of a validation ProblemDetails response.
/// Only property names and the validators' messages are exposed: attempted values are never included.
/// </summary>
public static class ValidationErrors
{
    public const string Code = "Validation.Failed";
    public const string Title = "One or more validation errors occurred.";
    public const string Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    public static Dictionary<string, string[]> ToDictionary(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(f => ToClientKey(f.PropertyName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray(), StringComparer.Ordinal);

    /// <summary>
    /// Maps a validator property path to the client's (camelCase JSON) field name. Commands wrap the client's request
    /// DTO in a property, so that wrapper segment is removed: "requestDTO.Email" → "email",
    /// "Request.Skills[0]" → "skills[0]". Top-level command properties keep their name: "userId" → "userId".
    /// </summary>
    public static string ToClientKey(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return "request";

        var segments = propertyName.Split('.');
        if (segments.Length > 1)
            segments = segments[1..];

        return string.Join('.', segments.Select(CamelCase));
    }

    private static string CamelCase(string segment) =>
        segment.Length == 0 || char.IsLower(segment[0])
            ? segment
            : char.ToLowerInvariant(segment[0]) + segment[1..];
}
