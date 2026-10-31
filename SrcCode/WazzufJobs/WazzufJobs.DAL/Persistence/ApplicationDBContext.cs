using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence;

public class ApplicationDBContext : IdentityDbContext<AppUser, ApplicationRole, string>
{
    /// <summary>Name of the demo-isolation query filters (Phase 02, §8).</summary>
    public const string DemoFilter = "Demo";

    private readonly IDataScope _dataScope;

    public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options, IDataScope? dataScope = null)
        : base(options)
    {
        _dataScope = dataScope ?? DefaultDataScope.Instance;
    }

    // evaluated per query (the filters below are parameterized on this context instance)
    private DataScopeMode ScopeMode => _dataScope.Mode;
    private bool IncludePublicDemo => _dataScope.IncludePublicDemo;

    public DbSet<CV> CVs => Set<CV>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<JobCategory> JobCategories => Set<JobCategory>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<SavedJob> SavedJobs => Set<SavedJob>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Phase 02 (S2a)
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<SessionRefreshToken> SessionRefreshTokens => Set<SessionRefreshToken>();
    public DbSet<AuthChallenge> AuthChallenges => Set<AuthChallenge>();
    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<ResumeText> ResumeTexts => Set<ResumeText>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory => Set<ApplicationStatusHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PlatformState> PlatformState => Set<PlatformState>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDBContext).Assembly);

        // Demo isolation (Phase 02, §8): a request sees only its partition. Real users never see demo rows, except
        // demo-badged public jobs and companies when IncludePublicDemo is set (P10). Identity tables are not filtered,
        // so UserManager lookups are unaffected; admin user lists filter explicitly. Bypassing a filter requires
        // IgnoreQueryFilters([DemoFilter]) at an allow-listed call site.
        builder.Entity<Company>().HasQueryFilter(DemoFilter, c =>
            ScopeMode == DataScopeMode.All || (ScopeMode == DataScopeMode.Demo ? c.IsDemo : !c.IsDemo || IncludePublicDemo));
        builder.Entity<Job>().HasQueryFilter(DemoFilter, j =>
            ScopeMode == DataScopeMode.All || (ScopeMode == DataScopeMode.Demo ? j.IsDemo : !j.IsDemo || IncludePublicDemo));
        builder.Entity<CompanyMember>().HasQueryFilter(DemoFilter, m =>
            ScopeMode == DataScopeMode.All || (ScopeMode == DataScopeMode.Demo ? m.Company.IsDemo : !m.Company.IsDemo));
        builder.Entity<Application>().HasQueryFilter(DemoFilter, a =>
            ScopeMode == DataScopeMode.All || (ScopeMode == DataScopeMode.Demo ? a.IsDemo : !a.IsDemo));
        builder.Entity<Resume>().HasQueryFilter(DemoFilter, r =>
            ScopeMode == DataScopeMode.All || (ScopeMode == DataScopeMode.Demo ? r.IsDemo : !r.IsDemo));
        builder.Entity<CandidateProfile>().HasQueryFilter(DemoFilter, p =>
            ScopeMode == DataScopeMode.All || (ScopeMode == DataScopeMode.Demo ? p.IsDemo : !p.IsDemo));
        builder.Entity<SavedJob>().HasQueryFilter(DemoFilter, s =>
            ScopeMode == DataScopeMode.All || (ScopeMode == DataScopeMode.Demo ? s.IsDemo : !s.IsDemo));

        // dependents of filtered principals follow their principal's partition
        builder.Entity<ApplicationStatusHistory>().HasQueryFilter(DemoFilter, h =>
            ScopeMode == DataScopeMode.All || (ScopeMode == DataScopeMode.Demo ? h.Application.IsDemo : !h.Application.IsDemo));
        builder.Entity<ResumeText>().HasQueryFilter(DemoFilter, t =>
            ScopeMode == DataScopeMode.All || (ScopeMode == DataScopeMode.Demo ? t.Resume.IsDemo : !t.Resume.IsDemo));
    }
}
