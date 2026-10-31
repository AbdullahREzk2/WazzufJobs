namespace WazzufJobs.DAL.Persistence;

/// <summary>Which data partition a request may see (Phase 02, §8).</summary>
public enum DataScopeMode
{
    /// <summary>Real (non-demo) data only. The default for every request.</summary>
    Real = 0,

    /// <summary>Demo data only (demo personas, P10).</summary>
    Demo = 1,

    /// <summary>Both partitions (explicit Super Admin scope, later stages).</summary>
    All = 2
}

/// <summary>
/// The data partition of the current request, applied by the "Demo" named query filters of
/// <see cref="ApplicationDBContext"/>. Demo functionality is not enabled yet (S2a): every request is <see cref="DataScopeMode.Real"/>.
/// </summary>
public interface IDataScope
{
    DataScopeMode Mode { get; }

    /// <summary>Whether real-partition public listings also show demo jobs and companies (badged; Demo:Enabled, P10).</summary>
    bool IncludePublicDemo { get; }
}

/// <summary>S2a default: real data only, no public demo listings.</summary>
public sealed class DefaultDataScope : IDataScope
{
    public static readonly DefaultDataScope Instance = new();

    public DataScopeMode Mode => DataScopeMode.Real;
    public bool IncludePublicDemo => false;
}
