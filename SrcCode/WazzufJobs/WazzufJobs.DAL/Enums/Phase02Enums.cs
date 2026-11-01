namespace WazzufJobs.DAL.Enums;

// Phase 02 domain enums. Stored as tinyint; numbering is append-only (never reorder or reuse a value).

public enum AccountType : byte
{
    Candidate = 0,
    Company = 1,
    Staff = 2
}

public enum CompanyVerificationStatus : byte
{
    Unverified = 0,
    Pending = 1,
    Verified = 2,
    Rejected = 3
}

public enum CompanyStatus : byte
{
    Active = 0,
    Suspended = 1
}

public enum CompanyMemberRole : byte
{
    Owner = 0,
    Admin = 1,
    Recruiter = 2
}

public enum CompanyMemberStatus : byte
{
    Active = 0,
    Removed = 1
}

public enum ResumeStorageAccess : byte
{
    Authenticated = 0,
    LegacyPublic = 1
}

public enum ResumeStatus : byte
{
    Active = 0,
    Archived = 1,

    /// <summary>File deleted by the candidate; the row remains only because an application references it.</summary>
    Removed = 2
}

public enum AuthChallengePurpose : byte
{
    TwoFactor = 0,
    TwoFactorEnrollment = 1,
    SessionHandoff = 2
}

public enum AuditActorKind : byte
{
    User = 0,
    System = 1,
    Anonymous = 2
}

public enum AuditOutcome : byte
{
    Success = 0,
    Failure = 1,
    Denied = 2
}
