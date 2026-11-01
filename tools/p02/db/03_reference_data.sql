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
-- 03_reference_data.sql  (WazzufJobs Phase 02, S2c; fixed platform roles added in S3)
-- Reference data for a Baseline_v2 database: the fixed platform roles and their permission claims (PermissionCatalog),
-- job categories, the platform company and the schema-baseline marker.
-- Baseline_v2 already seeds these rows; this script verifies them and restores any that are missing.
-- INSERT-ONLY and idempotent: existing rows are never updated or deleted. Running it again changes nothing.
-- Guards: exact database name and migration history = 20261101075350_Baseline_v2. Default: DRY RUN.
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
BEGIN SET @problems += 1; PRINT N'STOP: no migration history; the migration history must be exactly 20261101075350_Baseline_v2.'; END
ELSE IF ISNULL((SELECT STRING_AGG(MigrationId, N', ') FROM dbo.__EFMigrationsHistory), N'') <> N'20261101075350_Baseline_v2'
BEGIN SET @problems += 1; PRINT N'STOP: the migration history must be exactly 20261101075350_Baseline_v2.'; END

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

    -- the fixed platform roles and their permissions (generated from PermissionCatalog; checked by a drift test)
    IF OBJECT_ID(N'tempdb..#wj_roles') IS NOT NULL DROP TABLE #wj_roles;
    CREATE TABLE #wj_roles (Id nvarchar(450) NOT NULL, Name nvarchar(256) NOT NULL, ConcurrencyStamp nvarchar(max) NOT NULL,
        AccountType tinyint NOT NULL, DemoOnly bit NOT NULL, RealOnly bit NOT NULL, Description nvarchar(256) NOT NULL);
    INSERT INTO #wj_roles (Id, Name, ConcurrencyStamp, AccountType, DemoOnly, RealOnly, Description) VALUES
    (N'bf766c90-9e7c-4798-99c6-e90fe3a7960f', N'SuperAdmin', N'17291d98-8355-4440-a359-d4138485b8e6', 2, 0, 1, N'Platform owner: every platform permission, including users, roles (read-only) and applications.'),
    (N'21eb5d41-fffb-4b7d-8c09-abb15d40187b', N'Moderator', N'0de9d4f3-4fb2-4bac-bf7e-f5cd60efc2b0', 2, 0, 1, N'Platform moderation and the user list (read-only). Never CVs, applications or AI assessments.'),
    (N'eb935101-a57b-48a7-ad2d-ee21fc5e6ad1', N'Candidate', N'304deecb-d38e-43eb-aad3-b0476d91b2a3', 0, 0, 0, N'Job seeker: own profile, CV, saved jobs and applications.'),
    (N'f6d38004-b93a-4e15-80c0-824e87e50339', N'CompanyUser', N'7dcedc97-bb94-4df4-9744-3fa4b6870d12', 1, 0, 0, N'Company account: no platform permissions; rights come from company membership.'),
    (N'6b2d257a-0e04-49aa-9d22-e893759ed0c9', N'DemoAdmin', N'cb1d55b7-5d07-4845-9c1f-fba303e9f9c4', 2, 1, 0, N'Demo staff: public reference data only until demo scope exists. Never real users, CVs or AI assessments.');
    IF OBJECT_ID(N'tempdb..#wj_role_permissions') IS NOT NULL DROP TABLE #wj_role_permissions;
    CREATE TABLE #wj_role_permissions (RoleName nvarchar(256) NOT NULL, Permission nvarchar(100) NOT NULL);
    INSERT INTO #wj_role_permissions (RoleName, Permission) VALUES
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

    DECLARE @roleConflicts nvarchar(max) = (SELECT STRING_AGG(r.Name, N', ') FROM #wj_roles r
        WHERE EXISTS (SELECT 1 FROM dbo.AspNetRoles x WHERE (x.Id = r.Id AND x.NormalizedName <> UPPER(r.Name))
                                                         OR (x.NormalizedName = UPPER(r.Name) AND x.Id <> r.Id)));
    IF @roleConflicts IS NOT NULL
    BEGIN SET @problems += 1; PRINT N'STOP: role id/name pairs differ from the catalog: ' + @roleConflicts + N'. Nothing is changed; investigate.'; END
    DECLARE @missingRoles int = (SELECT COUNT(*) FROM #wj_roles r WHERE NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles x WHERE x.Id = r.Id));
    DECLARE @missingPermissions int = (SELECT COUNT(*) FROM #wj_role_permissions p JOIN #wj_roles r ON r.Name = p.RoleName
        WHERE NOT EXISTS (SELECT 1 FROM dbo.AspNetRoleClaims c WHERE c.RoleId = r.Id AND c.ClaimType = N'permissions' AND c.ClaimValue = p.Permission));
    DECLARE @extraClaims int = (SELECT COUNT(*) FROM dbo.AspNetRoleClaims c
        WHERE NOT EXISTS (SELECT 1 FROM #wj_role_permissions p JOIN #wj_roles r ON r.Name = p.RoleName
                          WHERE r.Id = c.RoleId AND c.ClaimType = N'permissions' AND c.ClaimValue = p.Permission));
    PRINT N'Platform roles: ' + CONVERT(nvarchar(10), 5 - @missingRoles) + N' of 5 present, ' + CONVERT(nvarchar(10), @missingRoles) + N' to insert.';
    PRINT N'Role permissions: ' + CONVERT(nvarchar(10), 35 - @missingPermissions) + N' of 35 present, ' + CONVERT(nvarchar(10), @missingPermissions) + N' to insert.';
    IF @extraClaims > 0
        PRINT N'NOTE: ' + CONVERT(nvarchar(10), @extraClaims) + N' role claim(s) are not in the catalog; they are left unchanged (they grant nothing; 04 reports them).';
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

INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp, IsSystem, Description)
SELECT r.Id, r.Name, UPPER(r.Name), r.ConcurrencyStamp, 1, r.Description FROM #wj_roles r
WHERE NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles x WHERE x.Id = r.Id OR x.NormalizedName = UPPER(r.Name));
DECLARE @insertedRoles int = @@ROWCOUNT;

INSERT INTO dbo.AspNetRoleClaims (RoleId, ClaimType, ClaimValue)
SELECT r.Id, N'permissions', p.Permission FROM #wj_role_permissions p JOIN #wj_roles r ON r.Name = p.RoleName
WHERE EXISTS (SELECT 1 FROM dbo.AspNetRoles x WHERE x.Id = r.Id)
  AND NOT EXISTS (SELECT 1 FROM dbo.AspNetRoleClaims c WHERE c.RoleId = r.Id AND c.ClaimType = N'permissions' AND c.ClaimValue = p.Permission);
DECLARE @insertedPermissions int = @@ROWCOUNT;

DECLARE @insertedMarker int = 0;
IF NOT EXISTS (SELECT 1 FROM dbo.PlatformState WHERE [Key] = N'SchemaBaseline')
BEGIN
    INSERT INTO dbo.PlatformState ([Key], [Value], UpdatedAt) VALUES (N'SchemaBaseline', N'v2', '2026-10-31T00:00:00');
    SET @insertedMarker = 1;
END

COMMIT TRANSACTION;

EXEC sys.sp_set_session_context @key = N'wj.Approved', @value = 0;
PRINT N'REFERENCE DATA APPLIED: inserted ' + CONVERT(nvarchar(10), @insertedCategories) + N' categor(ies), '
    + CONVERT(nvarchar(10), @insertedCompany) + N' platform company, ' + CONVERT(nvarchar(10), @insertedMarker) + N' baseline marker, '
    + CONVERT(nvarchar(10), @insertedRoles) + N' role(s), ' + CONVERT(nvarchar(10), @insertedPermissions) + N' role permission(s).';
GO
