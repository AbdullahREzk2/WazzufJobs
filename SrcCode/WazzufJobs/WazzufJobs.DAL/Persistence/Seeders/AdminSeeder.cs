namespace WazzufJobs.DAL.Persistence.Seeders;

/// <summary>
/// Automatic administrator seeding was removed in Phase C0: the application never creates an
/// administrator account and contains no default administrator credentials. The first administrator
/// is created through the controlled bootstrap procedure documented in
/// docs/runbooks/configuration-and-secrets.md. Kept only as a guard: any attempt to reintroduce a
/// seeder call fails at compile time.
/// </summary>
[Obsolete("Automatic administrator seeding is not allowed. Use the controlled administrator bootstrap procedure.", error: true)]
public static class AdminSeeder
{
}
