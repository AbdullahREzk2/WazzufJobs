using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.DAL.Persistence.Seeders;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>Guards the shared test infrastructure itself (Phase 02, stage S0).</summary>
public class InfrastructureTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    // relational: HasData role claims are applied by EnsureCreated before the host starts
    [SqlServerFact]
    public async Task Test_personas_carry_exactly_the_permissions_seeded_for_their_role()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        foreach (var persona in TestPersonas.All)
        {
            var roleIds = await db.Roles.Where(r => persona.Roles.Contains(r.Name!)).Select(r => r.Id).ToListAsync();
            var seeded = await db.RoleClaims
                .Where(c => roleIds.Contains(c.RoleId) && c.ClaimType == Permissions.Type)
                .Select(c => c.ClaimValue!)
                .ToListAsync();

            Assert.Equal(persona.Roles.Count, roleIds.Count);
            Assert.Equal(seeded.Order(), persona.Permissions.Order());
        }
    }
}
