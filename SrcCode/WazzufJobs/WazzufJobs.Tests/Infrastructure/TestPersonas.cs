using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.Persistence;

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

    /// <summary>
    /// Authenticates the client as <paramref name="user"/> with the persona's catalog access, in a new server-side
    /// session (Phase 02, S6a: the API accepts only tokens whose session is active).
    /// </summary>
    public static HttpClient As(this HttpClient client, IServiceProvider services, Persona persona, AppUser user)
    {
        var jwt = services.GetRequiredService<IJwtProvider>();
        var (token, _) = jwt.GenerateToken(user, persona.Grant, StartSession(services, user), "pwd");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Creates an active session for the user, as a sign-in would. A user the test never stored is stored first (a copy,
    /// so the caller's instance stays untracked).
    /// </summary>
    public static Guid StartSession(IServiceProvider services, AppUser user)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        if (!db.Users.Any(u => u.Id == user.Id))
            db.Users.Add(new AppUser
            {
                Id = user.Id, UserName = user.UserName, NormalizedUserName = user.NormalizedUserName ?? user.UserName?.ToUpperInvariant(),
                Email = user.Email, NormalizedEmail = user.NormalizedEmail ?? user.Email?.ToUpperInvariant(), EmailConfirmed = user.EmailConfirmed,
                FirstName = user.FirstName, LastName = user.LastName, AccountType = user.AccountType, IsDemo = user.IsDemo,
                IsDisabled = user.IsDisabled, SecurityStamp = user.SecurityStamp ?? Guid.NewGuid().ToString()
            });

        var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, AbsoluteExpiresAt = DateTime.UtcNow.AddDays(1), Amr = "pwd" };
        db.UserSessions.Add(session);
        db.SaveChanges();
        return session.Id;
    }

    /// <summary>Removes any credentials: subsequent requests are anonymous.</summary>
    public static HttpClient AsAnonymous(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }
}
