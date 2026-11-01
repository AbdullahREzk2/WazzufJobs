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
-- 04_verify_schema.sql  (WazzufJobs Phase 02, S2c)
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
ELSE IF ISNULL((SELECT STRING_AGG(MigrationId, N', ') FROM dbo.__EFMigrationsHistory), N'') <> N'20261101054955_Baseline_v2'
BEGIN SET @failures += 1; PRINT N'FAIL: migration history is not exactly 20261101054955_Baseline_v2.'; END
ELSE PRINT N'ok   migration history = 20261101054955_Baseline_v2';

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
ELSE IF (SELECT COUNT(*) FROM dbo.AspNetRoles WHERE Name IN (N'Admin', N'User')) <> 2
BEGIN SET @failures += 1; PRINT N'FAIL: roles Admin and User missing.'; END
ELSE PRINT N'ok   roles Admin, User; role permission claims = ' + CONVERT(nvarchar(10), @claims);
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

-- 6. information only
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
