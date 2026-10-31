using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.Tests.Infrastructure;

/// <summary>
/// A role-based identity used by integration tests: the roles and permission claims placed in the access token.
/// Phase 02 adds personas (SuperAdmin, Moderator, company members, demo personas) here as the corresponding roles
/// are introduced; tests then iterate over <see cref="All"/> for authorization-matrix and leak-sweep coverage.
/// </summary>
public sealed record Persona(string Name, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)
{
    public override string ToString() => Name;
}

public static class TestPersonas
{
    public static readonly Persona Candidate = new("candidate", [AppRoles.User.Name], Permissions.GetUserPermissions());
    public static readonly Persona Admin = new("admin", [AppRoles.Admin.Name], Permissions.GetAllPermissions());

    /// <summary>Every authenticated persona that exists in the current role model.</summary>
    public static IReadOnlyList<Persona> All { get; } = [Candidate, Admin];

    /// <summary>Authenticates the client as <paramref name="user"/> with the persona's roles and permissions.</summary>
    public static HttpClient As(this HttpClient client, IServiceProvider services, Persona persona, AppUser user)
    {
        var jwt = services.GetRequiredService<IJwtProvider>();
        var (token, _) = jwt.GenerateToken(user, persona.Roles, persona.Permissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Removes any credentials: subsequent requests are anonymous.</summary>
    public static HttpClient AsAnonymous(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }
}
