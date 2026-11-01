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
-- 03_reference_data.sql  (WazzufJobs Phase 02, S2c)
-- Reference data for a Baseline_v2 database: job categories, the platform company and the schema-baseline marker.
-- Baseline_v2 already seeds these rows; this script verifies them and restores any that are missing.
-- INSERT-ONLY and idempotent: existing rows are never updated or deleted. Running it again changes nothing.
-- Guards: exact database name and migration history = 20261101054955_Baseline_v2. Default: DRY RUN.
-- =====================================================================================================================

-- ===== OPERATOR INPUT: the only lines to edit =========================================================================
EXEC sys.sp_set_session_context @key = N'wj.ExpectedDatabase', @value = N'';   -- REQUIRED: exact name of the target database
EXEC sys.sp_set_session_context @key = N'wj.Apply', @value = 0;                -- 0 = DRY RUN (default, changes nothing); 1 = apply
GO

-- ===== GUARDS AND REPORT (no changes) =================================================================================
SET NOCOUNT ON;
EXEC sys.sp_set_session_context @key = N'wj.Approved', @value = 0;

DECLARE @expected sysname = CONVERT(sysname, SESSION_CONTEXT(N'wj.ExpectedDatabase'));
DECLARE @apply int = ISNULL(CONVERT(int, SESSION_CONTEXT(N'wj.Apply')), 0);
DECLARE @problems int = 0;

PRINT N'03_reference_data.sql: database ' + QUOTENAME(DB_NAME()) + N', mode ' + CASE WHEN @apply = 1 THEN N'APPLY' ELSE N'DRY RUN' END;

IF @expected IS NULL OR @expected = N''
BEGIN SET @problems += 1; PRINT N'STOP: wj.ExpectedDatabase is not set.'; END
ELSE IF DB_NAME() <> @expected
BEGIN SET @problems += 1; PRINT N'STOP: connected to ' + QUOTENAME(DB_NAME()) + N' but wj.ExpectedDatabase is ' + QUOTENAME(@expected) + N'.'; END

IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL
BEGIN SET @problems += 1; PRINT N'STOP: no migration history; the migration history must be exactly 20261101054955_Baseline_v2.'; END
ELSE IF ISNULL((SELECT STRING_AGG(MigrationId, N', ') FROM dbo.__EFMigrationsHistory), N'') <> N'20261101054955_Baseline_v2'
BEGIN SET @problems += 1; PRINT N'STOP: the migration history must be exactly 20261101054955_Baseline_v2.'; END

IF @problems = 0
BEGIN
    IF OBJECT_ID(N'tempdb..#wj_categories') IS NOT NULL DROP TABLE #wj_categories;
    CREATE TABLE #wj_categories (Id int NOT NULL, Name nvarchar(100) NOT NULL, Slug nvarchar(100) NOT NULL);
    INSERT INTO #wj_categories (Id, Name, Slug) VALUES
    (1, N'Software & IT', N'software-it'),
    (2, N'Banking & Finance', N'banking-finance'),
    (3, N'Marketing', N'marketing'),
    (4, N'Healthcare', N'healthcare'),
    (5, N'Engineering', N'engineering'),
    (6, N'Education', N'education'),
    (7, N'Sales', N'sales'),
    (8, N'Design & Creative', N'design-creative');

    DECLARE @missingCategories int = (SELECT COUNT(*) FROM #wj_categories r
        WHERE NOT EXISTS (SELECT 1 FROM dbo.JobCategories c WHERE c.Id = r.Id OR c.Slug = r.Slug));
    DECLARE @conflicts int = (SELECT COUNT(*) FROM #wj_categories r
        WHERE EXISTS (SELECT 1 FROM dbo.JobCategories c WHERE (c.Id = r.Id AND c.Slug <> r.Slug) OR (c.Slug = r.Slug AND c.Id <> r.Id)));
    PRINT N'Categories: ' + CONVERT(nvarchar(10), 8 - @missingCategories) + N' of 8 present, ' + CONVERT(nvarchar(10), @missingCategories) + N' to insert.';
    IF @conflicts > 0
        PRINT N'NOTE: ' + CONVERT(nvarchar(10), @conflicts) + N' category id/slug pair(s) differ from the reference list; they are left unchanged.';
    DECLARE @hasPlatform bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.Companies WHERE IsPlatform = 1) THEN 1 ELSE 0 END;
    DECLARE @hasMarker bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.PlatformState WHERE [Key] = N'SchemaBaseline') THEN 1 ELSE 0 END;
    PRINT N'Platform company: ' + CASE WHEN @hasPlatform = 1 THEN N'present.' ELSE N'missing, to insert.' END;
    PRINT N'Schema baseline marker: ' + CASE WHEN @hasMarker = 1 THEN N'present.' ELSE N'missing, to insert.' END;
END

IF @problems > 0
BEGIN
    RAISERROR(N'03_reference_data.sql STOPPED: %d problem(s) found (see above). No change was made.', 16, 1, @problems);
    SET NOEXEC ON;
END
ELSE IF @apply <> 1
    PRINT N'DRY RUN complete: no change was made. To apply, set wj.Apply = 1 in the operator input and run the whole script again.';
ELSE
    EXEC sys.sp_set_session_context @key = N'wj.Approved', @value = 1;
GO

-- ===== APPLY (insert missing rows only) ===============================================================================
SET NOCOUNT ON;
IF ISNULL(CONVERT(int, SESSION_CONTEXT(N'wj.Approved')), 0) <> 1
   OR DB_NAME() <> CONVERT(sysname, SESSION_CONTEXT(N'wj.ExpectedDatabase'))
BEGIN
    PRINT N'Apply step skipped.';
    RETURN;
END

BEGIN TRANSACTION;

SET IDENTITY_INSERT dbo.JobCategories ON;
INSERT INTO dbo.JobCategories (Id, Name, Slug, IsActive)
SELECT r.Id, r.Name, r.Slug, 1 FROM #wj_categories r
WHERE NOT EXISTS (SELECT 1 FROM dbo.JobCategories c WHERE c.Id = r.Id OR c.Slug = r.Slug);
DECLARE @insertedCategories int = @@ROWCOUNT;
SET IDENTITY_INSERT dbo.JobCategories OFF;

DECLARE @insertedCompany int = 0;
IF NOT EXISTS (SELECT 1 FROM dbo.Companies WHERE IsPlatform = 1)
BEGIN
    INSERT INTO dbo.Companies (Id, Name, Slug, VerificationStatus, Status, IsPlatform, IsDemo, CreatedAt)
    VALUES ('6F1D2C3B-8A47-4E5F-9B0A-1C2D3E4F5A6B', N'WazzufJobs', N'wazzufjobs', 2, 0, 1, 0, '2026-11-01T00:00:00');
    SET @insertedCompany = 1;
END

DECLARE @insertedMarker int = 0;
IF NOT EXISTS (SELECT 1 FROM dbo.PlatformState WHERE [Key] = N'SchemaBaseline')
BEGIN
    INSERT INTO dbo.PlatformState ([Key], [Value], UpdatedAt) VALUES (N'SchemaBaseline', N'v2', '2026-10-31T00:00:00');
    SET @insertedMarker = 1;
END

COMMIT TRANSACTION;

EXEC sys.sp_set_session_context @key = N'wj.Approved', @value = 0;
PRINT N'REFERENCE DATA APPLIED: inserted ' + CONVERT(nvarchar(10), @insertedCategories) + N' categor(ies), '
    + CONVERT(nvarchar(10), @insertedCompany) + N' platform company, ' + CONVERT(nvarchar(10), @insertedMarker) + N' baseline marker.';
GO
