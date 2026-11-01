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
-- 90_rollback_drop_baseline_v2.sql  (WazzufJobs Phase 02, S2c)
-- Rollback, step 1 of 2: drops the Baseline_v2 schema (and Hangfire) so that the empty P01 schema (91) can be applied.
-- DESTRUCTIVE in apply mode: all accounts and all data are deleted. Use only with an approved rollback.
-- Guards (all must pass; otherwise nothing runs after the guard batch):
--   * the connected database is exactly wj.ExpectedDatabase (never master/model/msdb/tempdb)
--   * the migration history is exactly as expected (see the check below)
--   * only allowlisted tables are dropped; every other table is preserved and reported
--   * the reset stops if an unknown object depends on an allowlisted table (foreign key or schema-bound reference),
--     if an unknown table sits in the HangFire schema, or if an unknown table would collide with a P01 table
-- Default: DRY RUN. Apply mode runs in one transaction with XACT_ABORT ON: on any error nothing is changed.
-- =====================================================================================================================

-- ===== OPERATOR INPUT: the only lines to edit =========================================================================
EXEC sys.sp_set_session_context @key = N'wj.ExpectedDatabase', @value = N'';   -- REQUIRED: exact name of the target database
EXEC sys.sp_set_session_context @key = N'wj.Apply', @value = 0;                -- 0 = DRY RUN (default, changes nothing); 1 = apply
GO

-- ===== GUARDS AND INVENTORY (no changes) ==============================================================================
SET NOCOUNT ON;
EXEC sys.sp_set_session_context @key = N'wj.Approved', @value = 0;

DECLARE @expected sysname = CONVERT(sysname, SESSION_CONTEXT(N'wj.ExpectedDatabase'));
DECLARE @apply int = ISNULL(CONVERT(int, SESSION_CONTEXT(N'wj.Apply')), 0);
DECLARE @problems int = 0;
DECLARE @line nvarchar(4000);

PRINT N'90_rollback_drop_baseline_v2.sql: database ' + QUOTENAME(DB_NAME()) + N', mode ' + CASE WHEN @apply = 1 THEN N'APPLY' ELSE N'DRY RUN' END;

IF @expected IS NULL OR @expected = N''
BEGIN SET @problems += 1; PRINT N'STOP: wj.ExpectedDatabase is not set.'; END
ELSE IF DB_NAME() <> @expected
BEGIN SET @problems += 1; PRINT N'STOP: connected to ' + QUOTENAME(DB_NAME()) + N' but wj.ExpectedDatabase is ' + QUOTENAME(@expected) + N'.'; END
IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN SET @problems += 1; PRINT N'STOP: system database.'; END

IF OBJECT_ID(N'tempdb..#wj_allow') IS NOT NULL DROP TABLE #wj_allow;
CREATE TABLE #wj_allow (SchemaName sysname NOT NULL, TableName sysname NOT NULL);
INSERT INTO #wj_allow (SchemaName, TableName) VALUES
    (N'dbo', N'__EFMigrationsHistory'),
    (N'dbo', N'ApplicationStatusHistory'),
    (N'dbo', N'Applications'),
    (N'dbo', N'AspNetRoleClaims'),
    (N'dbo', N'AspNetRoles'),
    (N'dbo', N'AspNetUserClaims'),
    (N'dbo', N'AspNetUserLogins'),
    (N'dbo', N'AspNetUserRoles'),
    (N'dbo', N'AspNetUserTokens'),
    (N'dbo', N'AspNetUsers'),
    (N'dbo', N'AuditLogs'),
    (N'dbo', N'AuthChallenges'),
    (N'dbo', N'CandidateProfiles'),
    (N'dbo', N'Companies'),
    (N'dbo', N'CompanyMembers'),
    (N'dbo', N'JobCategories'),
    (N'dbo', N'Jobs'),
    (N'dbo', N'LegacyRefreshTokens'),
    (N'dbo', N'PlatformState'),
    (N'dbo', N'RefreshTokens'),
    (N'dbo', N'ResumeTexts'),
    (N'dbo', N'Resumes'),
    (N'dbo', N'SavedJobs'),
    (N'dbo', N'UserRecoveryCodes'),
    (N'dbo', N'UserSessions'),
    (N'HangFire', N'AggregatedCounter'),
    (N'HangFire', N'Counter'),
    (N'HangFire', N'Hash'),
    (N'HangFire', N'Job'),
    (N'HangFire', N'JobParameter'),
    (N'HangFire', N'JobQueue'),
    (N'HangFire', N'List'),
    (N'HangFire', N'Schema'),
    (N'HangFire', N'Server'),
    (N'HangFire', N'Set'),
    (N'HangFire', N'State');

IF OBJECT_ID(N'tempdb..#wj_collide') IS NOT NULL DROP TABLE #wj_collide;
CREATE TABLE #wj_collide (TableName sysname NOT NULL);
INSERT INTO #wj_collide (TableName) VALUES
    (N'CVs'),
    (N'UserPreferences');

-- migration history
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL
BEGIN SET @problems += 1; PRINT N'STOP: no __EFMigrationsHistory: this is not a Baseline_v2 database.'; END
ELSE
BEGIN
    DECLARE @history nvarchar(max) = (SELECT STRING_AGG(MigrationId, N', ') WITHIN GROUP (ORDER BY MigrationId) FROM dbo.__EFMigrationsHistory);
    PRINT N'Migration history: ' + ISNULL(@history, N'(empty)');
    IF ISNULL(@history, N'') <> N'20261101075350_Baseline_v2'
    BEGIN SET @problems += 1; PRINT N'STOP: the history must be exactly 20261101075350_Baseline_v2.'; END
END

-- inventory of every user table
IF OBJECT_ID(N'tempdb..#wj_inv') IS NOT NULL DROP TABLE #wj_inv;
SELECT s.name AS SchemaName, t.name AS TableName, t.object_id AS ObjectId,
       (SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id = t.object_id AND p.index_id IN (0, 1)) AS RowCnt,
       CASE
           WHEN EXISTS (SELECT 1 FROM #wj_allow a WHERE a.SchemaName = s.name AND a.TableName = t.name) THEN N'DROP'
           WHEN s.name = N'HangFire' THEN N'STOP: unknown table in HangFire schema'
           WHEN s.name = N'dbo' AND EXISTS (SELECT 1 FROM #wj_collide c WHERE c.TableName = t.name) THEN N'STOP: name collides with a P01 table'
           ELSE N'PRESERVE'
       END AS Action
INTO #wj_inv
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0;

DECLARE inv CURSOR LOCAL FAST_FORWARD FOR
    SELECT N'  ' + LEFT(Action + N'            ', 9) + QUOTENAME(SchemaName) + N'.' + QUOTENAME(TableName) + N'  rows=' + CONVERT(nvarchar(20), ISNULL(RowCnt, 0))
           + CASE WHEN Action LIKE N'STOP%' THEN N'  <-- ' + Action ELSE N'' END
    FROM #wj_inv ORDER BY CASE WHEN Action = N'DROP' THEN 1 WHEN Action = N'PRESERVE' THEN 2 ELSE 0 END, SchemaName, TableName;
PRINT N'Tables:';
OPEN inv; FETCH NEXT FROM inv INTO @line;
WHILE @@FETCH_STATUS = 0 BEGIN PRINT @line; FETCH NEXT FROM inv INTO @line; END
CLOSE inv; DEALLOCATE inv;

SET @problems += (SELECT COUNT(*) FROM #wj_inv WHERE Action LIKE N'STOP%');

-- unknown objects that depend on a table to be dropped
DECLARE @deps int =
    (SELECT COUNT(*) FROM sys.foreign_keys fk
       JOIN #wj_inv child ON child.ObjectId = fk.parent_object_id AND child.Action <> N'DROP'
       JOIN #wj_inv parent ON parent.ObjectId = fk.referenced_object_id AND parent.Action = N'DROP')
  + (SELECT COUNT(*) FROM sys.sql_expression_dependencies d
       JOIN #wj_inv parent ON parent.ObjectId = d.referenced_id AND parent.Action = N'DROP'
       WHERE d.is_schema_bound_reference = 1
         AND NOT EXISTS (SELECT 1 FROM #wj_inv x WHERE x.ObjectId = d.referencing_id AND x.Action = N'DROP'));
IF @deps > 0
BEGIN SET @problems += 1; PRINT N'STOP: ' + CONVERT(nvarchar(10), @deps) + N' unknown object(s) depend on a table to be dropped (foreign key or schema-bound reference).'; END

-- unknown non-table objects in the HangFire schema
IF EXISTS (SELECT 1 FROM sys.objects o WHERE o.schema_id = SCHEMA_ID(N'HangFire') AND o.type NOT IN ('U', 'PK', 'F', 'D', 'UQ', 'C'))
BEGIN SET @problems += 1; PRINT N'STOP: unknown non-table objects in the HangFire schema.'; END

-- reported, never dropped
DECLARE @other int = (SELECT COUNT(*) FROM sys.objects o
                      WHERE o.is_ms_shipped = 0 AND o.parent_object_id = 0 AND o.type NOT IN ('U', 'PK', 'F', 'D', 'UQ', 'C', 'IT', 'S', 'SQ', 'TT')
                        AND o.schema_id <> ISNULL(SCHEMA_ID(N'HangFire'), -1));
IF @other > 0 PRINT N'NOTE: ' + CONVERT(nvarchar(10), @other) + N' other object(s) (views, procedures, functions, synonyms) are preserved as they are.';
IF EXISTS (SELECT 1 FROM sys.triggers WHERE parent_class = 0)
    PRINT N'NOTE: database-level DDL triggers exist; if one blocks a drop, the whole transaction is rolled back and nothing changes.';

IF @problems > 0
BEGIN
    RAISERROR(N'90_rollback_drop_baseline_v2.sql STOPPED: %d problem(s) found (see above). No change was made.', 16, 1, @problems);
    SET NOEXEC ON;
END
ELSE IF @apply <> 1
    PRINT N'DRY RUN complete: no change was made. To apply, set wj.Apply = 1 in the operator input and run the whole script again.';
ELSE
    EXEC sys.sp_set_session_context @key = N'wj.Approved', @value = 1;
GO

-- ===== APPLY (runs only after all guards passed with wj.Apply = 1) ====================================================
SET NOCOUNT ON;
IF ISNULL(CONVERT(int, SESSION_CONTEXT(N'wj.Approved')), 0) <> 1
   OR DB_NAME() <> CONVERT(sysname, SESSION_CONTEXT(N'wj.ExpectedDatabase'))
BEGIN
    PRINT N'Apply step skipped.';
    RETURN;
END

BEGIN TRANSACTION;

DECLARE @sql nvarchar(max) = N'';
-- foreign keys of the tables to be dropped (dependencies between allowlisted tables)
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + NCHAR(10)
FROM sys.foreign_keys fk
JOIN sys.tables t ON t.object_id = fk.parent_object_id
JOIN #wj_inv i ON i.ObjectId = t.object_id AND i.Action = N'DROP';
EXEC sys.sp_executesql @sql;

SET @sql = N'';
SELECT @sql += N'DROP TABLE ' + QUOTENAME(SchemaName) + N'.' + QUOTENAME(TableName) + N';' + NCHAR(10)
FROM #wj_inv WHERE Action = N'DROP';
EXEC sys.sp_executesql @sql;

IF SCHEMA_ID(N'HangFire') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(N'HangFire'))
    EXEC (N'DROP SCHEMA [HangFire]');

COMMIT TRANSACTION;

EXEC sys.sp_set_session_context @key = N'wj.Approved', @value = 0;
DECLARE @dropped int = (SELECT COUNT(*) FROM #wj_inv WHERE Action = N'DROP');
DECLARE @preserved int = (SELECT COUNT(*) FROM #wj_inv WHERE Action = N'PRESERVE');
PRINT N'ROLLBACK STEP 1 APPLIED: ' + CONVERT(nvarchar(10), @dropped) + N' table(s) dropped; '
    + CONVERT(nvarchar(10), @preserved) + N' preserved. Next: run 91_p01_schema.sql, then deploy the P01 backend package.';
GO
