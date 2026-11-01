using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// Phase 02, S3: platform roles in access tokens, legacy aliases, the read-only Roles API and the Users API rules
/// (no staff roles, account-type compatibility, no mass assignment). Role-escalation attempts must change nothing.
/// </summary>
public class PlatformRoleTests(C0WebApplicationFactory factory) : IClassFixture<C0WebApplicationFactory>
{
    private const string Password = "Roles#Passw0rd!S3";

    // ---------------------------------------------------------------- access tokens and legacy aliases

    public static TheoryData<string> PersonaNames => new(TestPersonas.All.Select(p => p.Name));

    [Theory]
    [MemberData(nameof(PersonaNames))]
    public async Task Sign_in_issues_the_catalog_roles_aliases_and_permissions_of_the_account(string personaName)
    {
        var persona = TestPersonas.All.Single(p => p.Name == personaName);
        var user = await CreateUserAsync(persona.AccountType, persona.IsDemo, persona.Role);

        var token = await LoginAsync(user);

        var payload = Payload(token);
        var role = PermissionCatalog.Find(persona.Role)!;
        Assert.Equal(new[] { role.Name }.Concat(role.LegacyAlias is null ? [] : [role.LegacyAlias]), Strings(payload, "roles"));
        Assert.Equal(role.Permissions.Order(), Strings(payload, "permissions").Order());
        Assert.Equal(persona.AccountType.ToString(), payload.GetProperty("acct").GetString());
    }

    // The Angular guards read only the "roles" claim: isAdmin() = includes('Admin'), isUser() = includes('User') or no
    // roles (auth.service.ts). The aliases keep the existing admin area and seeker area working, and nothing else.
    [Theory]
    [InlineData("superadmin", true, false)]
    [InlineData("candidate", false, true)]
    [InlineData("moderator", false, false)]
    [InlineData("companyuser", false, false)]
    [InlineData("demoadmin", false, false)]
    public async Task Legacy_angular_guards_see_admin_only_for_super_admins_and_user_only_for_candidates(string personaName, bool isAdmin, bool isUser)
    {
        var persona = TestPersonas.All.Single(p => p.Name == personaName);
        var token = await LoginAsync(await CreateUserAsync(persona.AccountType, persona.IsDemo, persona.Role));

        var roles = Strings(Payload(token), "roles");
        Assert.Equal(isAdmin, roles.Contains("Admin"));
        Assert.Equal(isUser, roles.Contains("User") || roles.Count == 0);
    }

    [Theory]
    [InlineData(AccountType.Staff, "Admin")]          // a legacy "Admin" role row (e.g. restored by hand)
    [InlineData(AccountType.Candidate, "User")]
    [InlineData(AccountType.Candidate, "SuperAdmin")]  // staff role on a candidate account
    [InlineData(AccountType.Company, "Moderator")]
    public async Task An_unknown_or_incompatible_stored_role_grants_nothing_at_sign_in(AccountType accountType, string storedRole)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            if (await roles.FindByNameAsync(storedRole) is null)
                Assert.True((await roles.CreateAsync(new ApplicationRole { Name = storedRole })).Succeeded);
        }
        var user = await CreateUserAsync(accountType, false, storedRole);

        var token = await LoginAsync(user);

        var payload = Payload(token);
        Assert.Empty(Strings(payload, "roles"));
        Assert.Empty(Strings(payload, "permissions"));
        var client = Bearer(token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/roles")).StatusCode);
    }

    [Fact]
    public async Task Role_claims_are_never_authorization_grants()
    {
        // a validly signed token that claims every role but carries only candidate permissions
        var candidate = await CreateUserAsync(AccountType.Candidate, false, PlatformRoles.Candidate);
        var forged = new AccessGrant(AccountType.Staff, [], ["Admin", "SuperAdmin", "Moderator", "User"], PermissionCatalog.Candidate.Permissions, []);
        var (token, _) = factory.Services.GetRequiredService<IJwtProvider>().GenerateToken(candidate, forged);
        var client = Bearer(token);

        foreach (var url in new[] { "/api/users", "/api/roles", $"/api/users/{candidate.Id}", "/api/applications/job/1" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);

        // the validated principal answers no role question at all
        var options = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.Equal(AccessTokenClaims.NoRoleAuthorization, options.TokenValidationParameters.RoleClaimType);
        var validation = await options.TokenHandlers.First().ValidateTokenAsync(token, options.TokenValidationParameters);
        Assert.True(validation.IsValid);
        var principal = new ClaimsPrincipal(validation.ClaimsIdentity);
        foreach (var role in new[] { "Admin", "SuperAdmin", "Moderator", "User", "Candidate" })
            Assert.False(principal.IsInRole(role));
        Assert.DoesNotContain(principal.Claims, c => c.Type == AccessTokenClaims.NoRoleAuthorization);
    }

    [Fact]
    public void Production_code_never_authorizes_by_role()
    {
        var root = SolutionDirectory();
        var pattern = new Regex(@"Authorize\s*\([^)]*\bRoles\s*=|AuthorizeAttribute\b[^;]*\bRoles\s*=|IsInRole\s*\(|RequireRole\s*\(|ClaimTypes\.Role\b|RequireClaim\s*\(\s*""roles""");
        var offenders = new[] { "WazzufJobs.API", "WazzufJobs.BLL", "WazzufJobs.DAL" }
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, line, i)))
            .Where(x => !x.line.TrimStart().StartsWith("//") && pattern.IsMatch(x.line))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}: {x.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    // ---------------------------------------------------------------- Users API: no staff roles, matching account types

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Moderator")]
    [InlineData("DemoAdmin")]
    [InlineData("superadmin")]
    public async Task Creating_a_staff_account_through_the_users_api_is_refused(string role)
    {
        var email = NewEmail();
        var response = await Admin().PostAsJsonAsync("/api/users",
            new { firstName = "Would", lastName = "Be", email, password = Password, roles = new[] { role } });

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Users.StaffRolesManagedSeparately");
        Assert.Null(await FindAsync(email));
    }

    [Theory]
    [InlineData(new[] { "Admin" }, HttpStatusCode.BadRequest, "User.InvalidRoles")]
    [InlineData(new[] { "User" }, HttpStatusCode.BadRequest, "User.InvalidRoles")]
    [InlineData(new[] { "Owner" }, HttpStatusCode.BadRequest, "User.InvalidRoles")]
    [InlineData(new[] { "CompanyUser" }, HttpStatusCode.BadRequest, "User.RoleAccountTypeMismatch")]
    [InlineData(new[] { "Candidate", "SuperAdmin" }, HttpStatusCode.Forbidden, "Users.StaffRolesManagedSeparately")]
    public async Task Creating_an_account_with_a_legacy_alias_or_mismatching_role_is_refused(string[] roles, HttpStatusCode status, string code)
    {
        var email = NewEmail();
        var response = await Admin().PostAsJsonAsync("/api/users", new { firstName = "A", lastName = "B", email, password = Password, roles });

        await AssertProblemAsync(response, status, code);
        Assert.Null(await FindAsync(email));
    }

    [Fact]
    public async Task Accounts_created_through_the_users_api_are_real_candidates_whatever_the_payload_says()
    {
        var email = NewEmail();
        var response = await Admin().PostAsJsonAsync("/api/users", new
        {
            firstName = "New", lastName = "Candidate", email, password = Password, roles = new[] { "candidate" },
            accountType = 2, isDemo = true, emailConfirmed = false, isDisabled = true
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(["Candidate"], Strings(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement, "roles"));
        var (user, roles) = (await FindAsync(email))!.Value;
        Assert.Equal(AccountType.Candidate, user.AccountType);
        Assert.False(user.IsDemo);
        Assert.False(user.IsDisabled);
        Assert.Equal(["Candidate"], roles);
    }

    [Theory]
    [InlineData(new object[] { new[] { "SuperAdmin" } })]
    [InlineData(new object[] { new[] { "Moderator" } })]
    [InlineData(new object[] { new[] { "DemoAdmin" } })]
    [InlineData(new object[] { new[] { "Candidate", "SuperAdmin" } })]
    public async Task A_candidate_cannot_be_given_a_staff_role(string[] roles)
    {
        var candidate = await CreateUserAsync(AccountType.Candidate, false, PlatformRoles.Candidate);

        var response = await Admin().PutAsJsonAsync($"/api/users/{candidate.Id}",
            new { firstName = candidate.FirstName, lastName = candidate.LastName, email = NewEmail(), roles });

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Users.StaffRolesManagedSeparately");
        var (_, current) = (await FindAsync(candidate.Email!))!.Value;   // email unchanged: nothing was applied
        Assert.Equal(["Candidate"], current);
    }

    [Theory]
    [InlineData(new[] { "CompanyUser" }, HttpStatusCode.BadRequest, "User.RoleAccountTypeMismatch")]
    [InlineData(new[] { "User" }, HttpStatusCode.BadRequest, "User.InvalidRoles")]
    [InlineData(new[] { "Admin" }, HttpStatusCode.BadRequest, "User.InvalidRoles")]
    public async Task A_candidate_cannot_be_given_another_account_types_role_or_an_alias(string[] roles, HttpStatusCode status, string code)
    {
        var candidate = await CreateUserAsync(AccountType.Candidate, false, PlatformRoles.Candidate);

        var response = await Admin().PutAsJsonAsync($"/api/users/{candidate.Id}",
            new { firstName = candidate.FirstName, lastName = candidate.LastName, email = candidate.Email, roles });

        await AssertProblemAsync(response, status, code);
        Assert.Equal(["Candidate"], (await FindAsync(candidate.Email!))!.Value.Roles);
    }

    [Theory]
    [InlineData("Moderator", AccountType.Staff, false)]
    [InlineData("DemoAdmin", AccountType.Staff, true)]
    public async Task A_staff_role_cannot_be_revoked_or_swapped_but_an_unchanged_one_is_accepted(string role, AccountType accountType, bool isDemo)
    {
        var staff = await CreateUserAsync(accountType, isDemo, role);
        var client = Admin();

        foreach (var roles in new[] { Array.Empty<string>(), ["SuperAdmin"], ["Candidate"], [role, "SuperAdmin"] })
        {
            var refused = await client.PutAsJsonAsync($"/api/users/{staff.Id}",
                new { firstName = staff.FirstName, lastName = staff.LastName, email = staff.Email, roles });
            await AssertProblemAsync(refused, HttpStatusCode.Forbidden, "Users.StaffRolesManagedSeparately");
        }

        Assert.NotNull(await FindAsync(staff.Email!));   // nothing was applied
        var newEmail = NewEmail();
        var unchanged = await client.PutAsJsonAsync($"/api/users/{staff.Id}",
            new { firstName = staff.FirstName, lastName = staff.LastName, email = newEmail, roles = new[] { role }, accountType = 0, isDemo = !isDemo });

        Assert.Equal(HttpStatusCode.NoContent, unchanged.StatusCode);
        var (user, current) = (await FindAsync(newEmail))!.Value;
        Assert.Equal([role], current);
        Assert.Equal(accountType, user.AccountType);
        Assert.Equal(isDemo, user.IsDemo);
    }

    [Fact]
    public async Task Moderators_can_read_the_user_list_but_change_nothing()
    {
        var moderator = await CreateUserAsync(AccountType.Staff, false, PlatformRoles.Moderator);
        var candidate = await CreateUserAsync(AccountType.Candidate, false, PlatformRoles.Candidate);
        var client = factory.CreateClient().As(factory.Services, TestPersonas.Moderator, moderator);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/users/{candidate.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/users/{moderator.Id}",
            new { firstName = "Self", lastName = "Promoted", email = moderator.Email, roles = new[] { "SuperAdmin" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync($"/api/users/{candidate.Id}/toggle-status", null)).StatusCode);
        Assert.Equal(["Moderator"], (await FindAsync(moderator.Email!))!.Value.Roles);
        Assert.False((await FindAsync(candidate.Email!))!.Value.User.IsDisabled);
    }

    [Fact]
    public async Task Self_registration_creates_a_real_candidate_account_whatever_the_payload_says()
    {
        var email = NewEmail();
        var register = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, firstName = "Self", lastName = "Registered", accountType = 2, isDemo = true, roles = new[] { "SuperAdmin" } });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        string userId, code;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByEmailAsync(email))!;
            Assert.Equal(AccountType.Candidate, user.AccountType);
            Assert.False(user.IsDemo);
            Assert.Empty(await users.GetRolesAsync(user));
            userId = user.Id;
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user)));
        }

        var confirm = await factory.CreateClient().GetAsync($"/api/auth/Confirm-Email?UserId={userId}&Code={code}");
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal(["Candidate"], (await FindAsync(email))!.Value.Roles);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var roles = Strings(Payload((await login.Content.ReadFromJsonAsync<LoginResult>())!.Token), "roles");
        Assert.Equal(["Candidate", "User"], roles);
    }

    // ---------------------------------------------------------------- Roles API: read-only

    [Fact]
    public async Task The_roles_api_lists_the_fixed_roles_from_the_catalog()
    {
        var client = Admin();

        var list = JsonDocument.Parse(await client.GetStringAsync("/api/roles")).RootElement;
        Assert.Equal(PermissionCatalog.Roles.Select(r => r.Name), list.EnumerateArray().Select(r => r.GetProperty("name").GetString()));
        Assert.All(list.EnumerateArray(), r => Assert.False(r.GetProperty("isDeleted").GetBoolean()));

        foreach (var role in PermissionCatalog.Roles)
        {
            var detail = JsonDocument.Parse(await client.GetStringAsync($"/api/roles/{role.Id}")).RootElement;
            Assert.Equal(role.Name, detail.GetProperty("name").GetString());
            Assert.Equal(role.Permissions, Strings(detail, "permissions"));
            Assert.Equal(role.AccountType.ToString(), detail.GetProperty("accountType").GetString());
        }

        await AssertProblemAsync(await client.GetAsync($"/api/roles/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "Role.NotFound");
    }

    [Fact]
    public async Task Role_and_permission_mutations_are_refused_and_change_nothing()
    {
        var before = await RoleStateAsync();
        var client = Admin();
        var escalation = new { name = "Candidate", permissions = Permissions.All };

        await AssertProblemAsync(await client.PostAsJsonAsync("/api/roles", new { name = "Owner", permissions = Permissions.All }), HttpStatusCode.Forbidden, "Roles.Fixed");
        await AssertProblemAsync(await client.PostAsync("/api/roles", null), HttpStatusCode.Forbidden, "Roles.Fixed");
        await AssertProblemAsync(await client.PutAsJsonAsync($"/api/roles/{PermissionCatalog.Candidate.Id}", escalation), HttpStatusCode.Forbidden, "Roles.Fixed");
        await AssertProblemAsync(await client.PutAsJsonAsync($"/api/roles/{PermissionCatalog.SuperAdmin.Id}", new { name = "SuperAdmin", permissions = Array.Empty<string>() }), HttpStatusCode.Forbidden, "Roles.Fixed");
        await AssertProblemAsync(await client.PutAsync($"/api/roles/{PermissionCatalog.SuperAdmin.Id}/toggle-status", null), HttpStatusCode.Forbidden, "Roles.Fixed");

        var candidate = factory.CreateClient().AsCandidate(factory.Services, TestData.NewUser("candidate"));
        Assert.Equal(HttpStatusCode.Forbidden, (await candidate.PutAsJsonAsync($"/api/roles/{PermissionCatalog.Candidate.Id}", escalation)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/roles", escalation)).StatusCode);

        Assert.Equal(before, await RoleStateAsync());
    }

    // ---------------------------------------------------------------- helpers

    private HttpClient Admin() => factory.CreateClient().AsAdmin(factory.Services, TestPersonas.SuperAdmin.NewUser());

    private HttpClient Bearer(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string NewEmail() => $"s3-{Guid.NewGuid():N}@example.test";

    private async Task<AppUser> CreateUserAsync(AccountType accountType, bool isDemo, string role)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = NewEmail();
        var user = new AppUser { UserName = email, Email = email, FirstName = "S3", LastName = "Test", EmailConfirmed = true, AccountType = accountType, IsDemo = isDemo };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return user;
    }

    private async Task<(AppUser User, IList<string> Roles)?> FindAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(email);
        return user is null ? null : (user, await users.GetRolesAsync(user));
    }

    private async Task<string> RoleStateAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        var roles = await db.Roles.AsNoTracking().Select(r => r.Id + ":" + r.Name).ToListAsync();
        var claims = await db.RoleClaims.AsNoTracking().Select(c => c.RoleId + ":" + c.ClaimValue).ToListAsync();
        return string.Join("|", roles.Order()) + "#" + string.Join("|", claims.Order());
    }

    private async Task<string> LoginAsync(AppUser user)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResult>())!.Token;
    }

    private static JsonElement Payload(string token)
    {
        var part = token.Split('.')[1];
        return JsonDocument.Parse(WebEncoders.Base64UrlDecode(part)).RootElement;
    }

    private static List<string> Strings(JsonElement element, string property)
    {
        var value = element.TryGetProperty(property, out var p) ? p : default;
        return value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().Select(v => v.GetString()!).ToList(),
            JsonValueKind.String => [value.GetString()!],
            _ => []
        };
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"expected {(int)status}, got {(int)response.StatusCode}: {body}");
        Assert.Contains(code, body);
    }

    private static string SolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WazzufJobs.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("WazzufJobs.sln not found");
    }

    private record LoginResult(string Token);
}
