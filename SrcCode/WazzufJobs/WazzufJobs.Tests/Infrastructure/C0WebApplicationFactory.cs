namespace WazzufJobs.Tests.Infrastructure;

/// <summary>
/// The Phase C0 test host. Kept as a named subclass of <see cref="ApiFactory"/> so the C0 regression suite
/// runs unchanged on the shared infrastructure.
/// </summary>
public class C0WebApplicationFactory : ApiFactory;

public class CvUploadsEnabledFactory : C0WebApplicationFactory
{
    protected override bool CvUploadsEnabled => true;
}

public class StrictRateLimitFactory : C0WebApplicationFactory
{
    public const int Limit = 3;
    protected override int AuthPermitLimitPerMinute => Limit;
}
