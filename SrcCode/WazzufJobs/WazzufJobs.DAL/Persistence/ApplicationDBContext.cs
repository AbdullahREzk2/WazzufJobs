using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence;

public class ApplicationDBContext : IdentityDbContext<AppUser, ApplicationRole, string>
{
    public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
        : base(options) { }

    public DbSet<CV> CVs => Set<CV>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<JobCategory> JobCategories => Set<JobCategory>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<SavedJob> SavedJobs => Set<SavedJob>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDBContext).Assembly);
    }
}