-- =====================================================================================================================
-- WazzufJobs SQL deployment scripts: required session options (Phase 02, finding F-S2a-1)
-- Prepended to every reviewed deployment script. Filtered indexes (also indexed views and indexes on computed columns)
-- can only be created with these settings, and sqlcmd starts sessions with QUOTED_IDENTIFIER OFF. The script sets them
-- itself instead of relying on client defaults or on "sqlcmd -I". Do not remove or reorder.
-- =====================================================================================================================
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO

-- Verification. If an option is not in effect, report it and stop: SET NOEXEC ON makes the server compile but not run
-- every following batch, so nothing is changed even by tools that continue after an error.
IF SESSIONPROPERTY('ANSI_NULLS') <> 1 OR SESSIONPROPERTY('ANSI_PADDING') <> 1 OR SESSIONPROPERTY('ANSI_WARNINGS') <> 1
   OR SESSIONPROPERTY('ARITHABORT') <> 1 OR SESSIONPROPERTY('CONCAT_NULL_YIELDS_NULL') <> 1
   OR SESSIONPROPERTY('QUOTED_IDENTIFIER') <> 1 OR SESSIONPROPERTY('NUMERIC_ROUNDABORT') <> 0
   OR (@@OPTIONS & 16384) = 0 -- XACT_ABORT
BEGIN
    RAISERROR(N'WazzufJobs deployment stopped: required session options are not in effect. No change was made.', 16, 1);
    SET NOEXEC ON;
END
GO

-- =====================================================================================================================
-- 04_verify_schema.sql  (WazzufJobs Phase 02, S2c; fixed platform roles added in S3, company memberships in S4)
-- READ-ONLY verification of a Baseline_v2 database. Ends with "VERIFY: PASS" or raises "VERIFY: FAIL".
-- Accounts and other data are counted, never shown.
-- =====================================================================================================================

-- ===== OPERATOR INPUT: the only line to edit ==========================================================================
EXEC sys.sp_set_session_context @key = N'wj.ExpectedDatabase', @value = N'';   -- REQUIRED: exact name of the target database
GO

SET NOCOUNT ON;
DECLARE @expected sysname = CONVERT(sysname, SESSION_CONTEXT(N'wj.ExpectedDatabase'));
DECLARE @failures int = 0;

PRINT N'04_verify_schema.sql: database ' + QUOTENAME(DB_NAME());

IF @expected IS NULL OR @expected = N'' OR DB_NAME() <> @expected
BEGIN SET @failures += 1; PRINT N'FAIL: wj.ExpectedDatabase is not set or does not match the connected database.'; END

-- 1. migration history
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL
BEGIN SET @failures += 1; PRINT N'FAIL: no migration history.'; END
ELSE IF ISNULL((SELECT STRING_AGG(MigrationId, N', ') FROM dbo.__EFMigrationsHistory), N'') <> N'20261101075350_Baseline_v2'
BEGIN SET @failures += 1; PRINT N'FAIL: migration history is not exactly 20261101075350_Baseline_v2.'; END
ELSE PRINT N'ok   migration history = 20261101075350_Baseline_v2';

-- 2. tables
DECLARE @tables TABLE (Name sysname NOT NULL);
INSERT INTO @tables (Name) VALUES (N'ApplicationStatusHistory'), (N'Applications'), (N'AspNetRoleClaims'), (N'AspNetRoles'), (N'AspNetUserClaims'), (N'AspNetUserLogins'), (N'AspNetUserRoles'), (N'AspNetUserTokens'), (N'AspNetUsers'), (N'AuditLogs'), (N'AuthChallenges'), (N'CandidateProfiles'), (N'Companies'), (N'CompanyMembers'), (N'JobCategories'), (N'Jobs'), (N'LegacyRefreshTokens'), (N'PlatformState'), (N'RefreshTokens'), (N'ResumeTexts'), (N'Resumes'), (N'SavedJobs'), (N'UserRecoveryCodes'), (N'UserSessions');
DECLARE @missing nvarchar(max) = (SELECT STRING_AGG(Name, N', ') FROM @tables t WHERE OBJECT_ID(N'dbo.' + QUOTENAME(t.Name), N'U') IS NULL);
DECLARE @tablesPresent bit = CASE WHEN @missing IS NULL THEN 1 ELSE 0 END;
IF @missing IS NOT NULL BEGIN SET @failures += 1; PRINT N'FAIL: missing tables: ' + @missing; END
ELSE PRINT N'ok   all 24 Baseline_v2 tables present';
IF OBJECT_ID(N'dbo.CVs', N'U') IS NOT NULL OR OBJECT_ID(N'dbo.UserPreferences', N'U') IS NOT NULL
BEGIN SET @failures += 1; PRINT N'FAIL: legacy P01 tables (CVs, UserPreferences) still exist.'; END
ELSE PRINT N'ok   no legacy P01 tables';

-- 3. append-only audit trigger
IF NOT EXISTS (SELECT 1 FROM sys.triggers WHERE name = N'TR_AuditLogs_AppendOnly' AND parent_id = OBJECT_ID(N'dbo.AuditLogs') AND is_disabled = 0)
BEGIN SET @failures += 1; PRINT N'FAIL: trigger TR_AuditLogs_AppendOnly missing or disabled.'; END
ELSE PRINT N'ok   AuditLogs append-only trigger enabled';

-- 4. invariant indexes and ownership keys
DECLARE @indexes TABLE (Name sysname NOT NULL);
INSERT INTO @indexes (Name) VALUES (N'UX_RefreshTokens_ActivePerSession'), (N'UX_Resumes_DefaultPerCandidate'), (N'UX_CompanyMembers_ActivePerUser'), (N'UX_Companies_SinglePlatform');
SET @missing = (SELECT STRING_AGG(Name, N', ') FROM @indexes x
                WHERE NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.name = x.Name AND i.is_unique = 1 AND i.has_filter = 1));
IF @missing IS NOT NULL BEGIN SET @failures += 1; PRINT N'FAIL: missing filtered unique indexes: ' + @missing; END
ELSE PRINT N'ok   4 filtered unique invariant indexes';
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'AK_Jobs_Id_CompanyId')
   OR NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'AK_Resumes_Id_CandidateId')
   OR NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Applications_Jobs_JobId_CompanyId')
   OR NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Applications_Resumes_ResumeId_CandidateId')
BEGIN SET @failures += 1; PRINT N'FAIL: ownership keys (job company, applicant resume) missing.'; END
ELSE PRINT N'ok   ownership keys (applications -> job company, applicant resume)';

-- 5. reference data (only when the tables exist)
DECLARE @claims int = 0, @categories int = 0, @accounts int = 0;
IF @tablesPresent = 1
BEGIN
    SET @claims = (SELECT COUNT(*) FROM dbo.AspNetRoleClaims);
    SET @categories = (SELECT COUNT(*) FROM dbo.JobCategories);
    SET @accounts = (SELECT COUNT(*) FROM dbo.AspNetUsers);
END
IF @tablesPresent = 0
    PRINT N'skip reference-data checks: tables missing';
ELSE
BEGIN
    -- the fixed platform roles and their permissions must equal PermissionCatalog exactly (generated; drift-tested)
    DECLARE @roles TABLE (Id nvarchar(450) NOT NULL, Name nvarchar(256) NOT NULL, ConcurrencyStamp nvarchar(max) NOT NULL,
        AccountType tinyint NOT NULL, DemoOnly bit NOT NULL, RealOnly bit NOT NULL, Description nvarchar(256) NOT NULL);
    INSERT INTO @roles (Id, Name, ConcurrencyStamp, AccountType, DemoOnly, RealOnly, Description) VALUES
    (N'bf766c90-9e7c-4798-99c6-e90fe3a7960f', N'SuperAdmin', N'17291d98-8355-4440-a359-d4138485b8e6', 2, 0, 1, N'Platform owner: every platform permission, including users, roles (read-only) and applications.'),
    (N'21eb5d41-fffb-4b7d-8c09-abb15d40187b', N'Moderator', N'0de9d4f3-4fb2-4bac-bf7e-f5cd60efc2b0', 2, 0, 1, N'Platform moderation and the user list (read-only). Never CVs, applications or AI assessments.'),
    (N'eb935101-a57b-48a7-ad2d-ee21fc5e6ad1', N'Candidate', N'304deecb-d38e-43eb-aad3-b0476d91b2a3', 0, 0, 0, N'Job seeker: own profile, CV, saved jobs and applications.'),
    (N'f6d38004-b93a-4e15-80c0-824e87e50339', N'CompanyUser', N'7dcedc97-bb94-4df4-9744-3fa4b6870d12', 1, 0, 0, N'Company account: no platform permissions; rights come from company membership.'),
    (N'6b2d257a-0e04-49aa-9d22-e893759ed0c9', N'DemoAdmin', N'cb1d55b7-5d07-4845-9c1f-fba303e9f9c4', 2, 1, 0, N'Demo staff: public reference data only until demo scope exists. Never real users, CVs or AI assessments.');
    DECLARE @rolePermissions TABLE (RoleName nvarchar(256) NOT NULL, Permission nvarchar(100) NOT NULL);
    INSERT INTO @rolePermissions (RoleName, Permission) VALUES
    (N'SuperAdmin', N'jobs:read'),
    (N'SuperAdmin', N'jobs:create'),
    (N'SuperAdmin', N'jobs:update'),
    (N'SuperAdmin', N'jobs:delete'),
    (N'SuperAdmin', N'applications:read'),
    (N'SuperAdmin', N'applications:create'),
    (N'SuperAdmin', N'applications:update'),
    (N'SuperAdmin', N'categories:read'),
    (N'SuperAdmin', N'categories:create'),
    (N'SuperAdmin', N'categories:update'),
    (N'SuperAdmin', N'categories:delete'),
    (N'SuperAdmin', N'users:read'),
    (N'SuperAdmin', N'users:create'),
    (N'SuperAdmin', N'users:update'),
    (N'SuperAdmin', N'users:delete'),
    (N'SuperAdmin', N'roles:read'),
    (N'SuperAdmin', N'cv:upload'),
    (N'SuperAdmin', N'cv:delete'),
    (N'SuperAdmin', N'savedjobs:read'),
    (N'SuperAdmin', N'savedjobs:create'),
    (N'SuperAdmin', N'savedjobs:delete'),
    (N'Moderator', N'jobs:read'),
    (N'Moderator', N'categories:read'),
    (N'Moderator', N'users:read'),
    (N'Candidate', N'jobs:read'),
    (N'Candidate', N'applications:read'),
    (N'Candidate', N'applications:create'),
    (N'Candidate', N'categories:read'),
    (N'Candidate', N'cv:upload'),
    (N'Candidate', N'cv:delete'),
    (N'Candidate', N'savedjobs:read'),
    (N'Candidate', N'savedjobs:create'),
    (N'Candidate', N'savedjobs:delete'),
    (N'DemoAdmin', N'jobs:read'),
    (N'DemoAdmin', N'categories:read');

    DECLARE @roleDiff nvarchar(max) = (SELECT STRING_AGG(d.Name, N', ') FROM (
        SELECT r.Name FROM @roles r WHERE NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles x WHERE x.Id = r.Id AND x.Name = r.Name AND x.NormalizedName = UPPER(r.Name))
        UNION ALL
        SELECT x.Name FROM dbo.AspNetRoles x WHERE NOT EXISTS (SELECT 1 FROM @roles r WHERE r.Id = x.Id)) d(Name));
    DECLARE @claimDiff int = (SELECT COUNT(*) FROM (
        SELECT r.Id, N'permissions' AS ClaimType, p.Permission FROM @rolePermissions p JOIN @roles r ON r.Name = p.RoleName
        EXCEPT SELECT RoleId, ClaimType, ClaimValue FROM dbo.AspNetRoleClaims) missing)
      + (SELECT COUNT(*) FROM dbo.AspNetRoleClaims c WHERE NOT EXISTS (
        SELECT 1 FROM @rolePermissions p JOIN @roles r ON r.Name = p.RoleName
        WHERE r.Id = c.RoleId AND c.ClaimType = N'permissions' AND c.ClaimValue = p.Permission));
    IF @roleDiff IS NOT NULL
    BEGIN SET @failures += 1; PRINT N'FAIL: platform roles differ from the catalog: ' + @roleDiff; END
    ELSE IF @claimDiff > 0 OR @claims <> 35
    BEGIN SET @failures += 1; PRINT N'FAIL: role permission claims differ from the catalog (' + CONVERT(nvarchar(10), @claimDiff) + N' difference(s), ' + CONVERT(nvarchar(10), @claims) + N' claims).'; END
    ELSE PRINT N'ok   5 fixed platform roles; 35 role permission claims equal the catalog';

    -- every account holds at most one role, matching its account type and demo flag
    DECLARE @mismatched int = (SELECT COUNT(DISTINCT u.Id) FROM dbo.AspNetUsers u JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
        LEFT JOIN @roles r ON r.Id = ur.RoleId
        WHERE r.Id IS NULL OR u.AccountType <> r.AccountType OR (r.DemoOnly = 1 AND u.IsDemo = 0) OR (r.RealOnly = 1 AND u.IsDemo = 1)
           OR (SELECT COUNT(*) FROM dbo.AspNetUserRoles o WHERE o.UserId = u.Id) > 1);
    IF @mismatched > 0
    BEGIN SET @failures += 1; PRINT N'FAIL: ' + CONVERT(nvarchar(10), @mismatched) + N' account(s) hold more than one role or a role that does not match their account type.'; END
    ELSE PRINT N'ok   account roles match account types';
END
IF @tablesPresent = 0
    SET @failures += 0;
ELSE IF (SELECT COUNT(*) FROM dbo.JobCategories WHERE Id BETWEEN 1 AND 8) <> 8
BEGIN SET @failures += 1; PRINT N'FAIL: reference categories 1-8 missing.'; END
ELSE PRINT N'ok   reference categories present (total ' + CONVERT(nvarchar(10), @categories) + N')';
IF @tablesPresent = 0
    SET @failures += 0;
ELSE IF (SELECT COUNT(*) FROM dbo.Companies WHERE IsPlatform = 1) <> 1
BEGIN SET @failures += 1; PRINT N'FAIL: exactly one platform company is required.'; END
ELSE PRINT N'ok   platform company';
IF @tablesPresent = 0
    SET @failures += 0;
ELSE IF NOT EXISTS (SELECT 1 FROM dbo.PlatformState WHERE [Key] = N'SchemaBaseline' AND [Value] = N'v2')
BEGIN SET @failures += 1; PRINT N'FAIL: schema baseline marker missing.'; END
ELSE PRINT N'ok   schema baseline marker v2';

-- 6. company memberships (Phase 02, S4): only company accounts of the company's partition, never the platform company;
--    every company with active members keeps an active owner
IF @tablesPresent = 1
BEGIN
    DECLARE @badMemberships int = (SELECT COUNT(*) FROM dbo.CompanyMembers m
        JOIN dbo.AspNetUsers u ON u.Id = m.UserId JOIN dbo.Companies c ON c.Id = m.CompanyId
        WHERE m.Status = 0 AND (u.AccountType <> 1 OR u.IsDemo <> c.IsDemo OR c.IsPlatform = 1));
    DECLARE @ownerless int = (SELECT COUNT(*) FROM dbo.Companies c
        WHERE EXISTS (SELECT 1 FROM dbo.CompanyMembers m WHERE m.CompanyId = c.Id AND m.Status = 0)
          AND NOT EXISTS (SELECT 1 FROM dbo.CompanyMembers m WHERE m.CompanyId = c.Id AND m.Status = 0 AND m.Role = 0));
    IF @badMemberships > 0
    BEGIN SET @failures += 1; PRINT N'FAIL: ' + CONVERT(nvarchar(10), @badMemberships) + N' active company membership(s) belong to a non-company account, another partition or the platform company.'; END
    IF @ownerless > 0
    BEGIN SET @failures += 1; PRINT N'FAIL: ' + CONVERT(nvarchar(10), @ownerless) + N' company(ies) with active members have no active owner.'; END
    IF @badMemberships = 0 AND @ownerless = 0
        PRINT N'ok   company memberships consistent (company accounts only, an active owner per company)';
END

-- 7. information only
IF @tablesPresent = 1
    PRINT N'info accounts: ' + CONVERT(nvarchar(10), @accounts) + N' (0 expected directly after a reset)';
DECLARE @unknown nvarchar(max) = (SELECT STRING_AGG(QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name), N', ')
    FROM sys.tables t WHERE t.is_ms_shipped = 0 AND SCHEMA_NAME(t.schema_id) <> N'HangFire'
      AND t.name <> N'__EFMigrationsHistory' AND NOT EXISTS (SELECT 1 FROM @tables x WHERE x.Name = t.name AND t.schema_id = SCHEMA_ID(N'dbo')));
PRINT N'info other tables (preserved, not part of Baseline_v2): ' + ISNULL(@unknown, N'none');

IF @failures > 0
    RAISERROR(N'VERIFY: FAIL (%d check(s) failed).', 16, 1, @failures);
ELSE
    PRINT N'VERIFY: PASS';
GO
