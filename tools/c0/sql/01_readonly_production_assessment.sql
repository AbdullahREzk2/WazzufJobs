/* =====================================================================================
   WazzufJobs - Phase C0 / VER-01  -  READ-ONLY production data assessment
   -------------------------------------------------------------------------------------
   * Contains SELECT statements only. It does not modify any data or schema.
   * Preferred: run against a LOCAL RESTORE of the downloaded production backup
     (e.g. database "WazzufJobs_ProdRestore"), not against the live database.
   * Output contains personal data (emails). Keep it on your machine; do not commit,
     upload, or paste it into chats. Share only the aggregated counts if needed.
   ===================================================================================== */
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

PRINT '--- 0. Server and schema version ---';
SELECT @@VERSION AS SqlServerVersion,
       (SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) AS CompatibilityLevel,
       DB_NAME() AS DatabaseName;
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;

PRINT '--- 1. Row counts ---';
SELECT 'Users' AS [Table], COUNT(*) AS [Rows] FROM dbo.AspNetUsers
UNION ALL SELECT 'Jobs', COUNT(*) FROM dbo.Jobs
UNION ALL SELECT 'Applications', COUNT(*) FROM dbo.Applications
UNION ALL SELECT 'CVs', COUNT(*) FROM dbo.CVs
UNION ALL SELECT 'SavedJobs', COUNT(*) FROM dbo.SavedJobs
UNION ALL SELECT 'UserPreferences', COUNT(*) FROM dbo.UserPreferences
UNION ALL SELECT 'RefreshTokens', COUNT(*) FROM dbo.RefreshTokens;

PRINT '--- 2. Users by email domain (aggregated) ---';
SELECT LOWER(SUBSTRING(Email, CHARINDEX('@', Email) + 1, 255)) AS EmailDomain,
       COUNT(*) AS Users,
       SUM(CASE WHEN EmailConfirmed = 1 THEN 1 ELSE 0 END) AS Confirmed,
       MIN(CreatedAt) AS FirstCreated,
       MAX(CreatedAt) AS LastCreated
FROM dbo.AspNetUsers
GROUP BY LOWER(SUBSTRING(Email, CHARINDEX('@', Email) + 1, 255))
ORDER BY Users DESC;

PRINT '--- 3. All accounts (for YOUR classification: own/test vs real third party) ---';
-- (FOR XML PATH instead of STRING_AGG so the script also runs on SQL Server 2016 / low compatibility levels)
SELECT u.Id, u.Email, u.FirstName, u.LastName, u.EmailConfirmed, u.CreatedAt, u.IsDisabled,
       STUFF((SELECT ',' + r.Name
              FROM dbo.AspNetUserRoles ur
              JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
              WHERE ur.UserId = u.Id
              FOR XML PATH('')), 1, 1, '') AS Roles,
       (SELECT COUNT(*) FROM dbo.Applications a WHERE a.UserId = u.Id) AS Applications,
       (SELECT COUNT(*) FROM dbo.CVs c WHERE c.UserId = u.Id) AS CVs
FROM dbo.AspNetUsers u
ORDER BY u.CreatedAt;

PRINT '--- 4. Administrator accounts and their login history (refresh tokens = successful logins) ---';
SELECT u.Email, u.EmailConfirmed, u.LockoutEnd, u.AccessFailedCount, u.TwoFactorEnabled,
       COUNT(t.Id) AS LoginsRecorded, MIN(t.CreatedOn) AS FirstLogin, MAX(t.CreatedOn) AS LastLogin
FROM dbo.AspNetUsers u
JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId AND r.Name = 'Admin'
LEFT JOIN dbo.RefreshTokens t ON t.UserId = u.Id
GROUP BY u.Email, u.EmailConfirmed, u.LockoutEnd, u.AccessFailedCount, u.TwoFactorEnabled;

PRINT '--- 5. Admin login timestamps (review for logins you do not recognise) ---';
SELECT u.Email, t.CreatedOn AS LoginAtUtc, t.ExpiresOn, t.RevokedOn
FROM dbo.RefreshTokens t
JOIN dbo.AspNetUsers u ON u.Id = t.UserId
JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId AND r.Name = 'Admin'
ORDER BY t.CreatedOn DESC;

PRINT '--- 6. Seeded default admin present? (S2) ---';
SELECT Email, CreatedAt, EmailConfirmed FROM dbo.AspNetUsers WHERE NormalizedEmail = 'ADMIN@WAZZUF.COM';

PRINT '--- 7. CV references (input for the Cloudinary inventory join) ---';
SELECT c.UserId, c.PublicId, c.Url, c.UploadedAt,
       CASE WHEN c.Url LIKE '%/raw/authenticated/%' THEN 'authenticated' ELSE 'upload (public)' END AS DeliveryType
FROM dbo.CVs c
ORDER BY c.UploadedAt;

PRINT '--- 8. Jobs and applications overview ---';
SELECT Status, COUNT(*) AS Jobs, MIN(CreatedAt) AS FirstCreated, MAX(CreatedAt) AS LastCreated FROM dbo.Jobs GROUP BY Status;
SELECT Status, COUNT(*) AS Applications, SUM(CASE WHEN IsAIScored = 1 THEN 1 ELSE 0 END) AS Scored,
       MIN(AppliedAt) AS FirstApplied, MAX(AppliedAt) AS LastApplied
FROM dbo.Applications GROUP BY Status;

PRINT '--- 9. VER-03: historical background-job latency on the host (Hangfire) ---';
IF OBJECT_ID('HangFire.Job') IS NOT NULL
BEGIN
    SELECT TOP (200)
        j.Id, j.CreatedAt AS EnqueuedAtUtc, s.CreatedAt AS SucceededAtUtc,
        DATEDIFF(SECOND, j.CreatedAt, s.CreatedAt) AS LatencySeconds, j.StateName
    FROM HangFire.Job j
    OUTER APPLY (SELECT TOP 1 st.CreatedAt FROM HangFire.State st
                 WHERE st.JobId = j.Id AND st.Name = 'Succeeded' ORDER BY st.CreatedAt) s
    ORDER BY j.CreatedAt DESC;

    SELECT StateName, COUNT(*) AS Jobs FROM HangFire.Job GROUP BY StateName;
END
ELSE
    PRINT 'Hangfire schema not present.';
