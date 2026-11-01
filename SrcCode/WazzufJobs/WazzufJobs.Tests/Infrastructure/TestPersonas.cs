using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.Tests.Infrastructure;

/// <summary>
/// A role-based identity used by integration tests. Its access token is built exactly like a real sign-in builds it
/// (<see cref="PermissionCatalog.Resolve"/>), so the roles, legacy aliases and permissions match production.
/// Phase 02 adds company-member and demo personas as the corresponding stages land; tests iterate over
/// <see cref="TestPersonas.All"/> for authorization-matrix and leak-sweep coverage.
/// </summary>
public sealed record Persona(string Name, string Role, AccountType AccountType, bool IsDemo = false)
{
    public AccessGrant Grant => PermissionCatalog.Resolve(AccountType, IsDemo, [Role]);
    public IReadOnlyList<string> Roles => Grant.Roles;
    public IReadOnlyList<string> Permissions => Grant.Permissions;

    /// <summary>A user entity with this persona's account type and demo flag (roles are assigned separately).</summary>
    public AppUser NewUser()
    {
        var user = TestData.NewUser(Name);
        user.AccountType = AccountType;
        user.IsDemo = IsDemo;
        return user;
    }

    public override string ToString() => Name;
}

public static class TestPersonas
{
    public static readonly Persona SuperAdmin = new("superadmin", PlatformRoles.SuperAdmin, AccountType.Staff);
    public static readonly Persona Moderator = new("moderator", PlatformRoles.Moderator, AccountType.Staff);
    public static readonly Persona Candidate = new("candidate", PlatformRoles.Candidate, AccountType.Candidate);
    public static readonly Persona CompanyUser = new("companyuser", PlatformRoles.CompanyUser, AccountType.Company);
    public static readonly Persona DemoAdmin = new("demoadmin", PlatformRoles.DemoAdmin, AccountType.Staff, IsDemo: true);

    /// <summary>The C0 tests' administrator: the Super Admin.</summary>
    public static Persona Admin => SuperAdmin;

    /// <summary>Every authenticated persona that exists in the current role model.</summary>
    public static IReadOnlyList<Persona> All { get; } = [SuperAdmin, Moderator, Candidate, CompanyUser, DemoAdmin];

    /// <summary>Authenticates the client as <paramref name="user"/> with the persona's catalog access.</summary>
    public static HttpClient As(this HttpClient client, IServiceProvider services, Persona persona, AppUser user)
    {
        var jwt = services.GetRequiredService<IJwtProvider>();
        var (token, _) = jwt.GenerateToken(user, persona.Grant);
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
