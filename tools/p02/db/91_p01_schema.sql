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
-- 91_p01_schema.sql  (WazzufJobs Phase 02, S2c)  Rollback, step 2 of 2
-- Creates the EMPTY P01 schema (migrations 20260429160411_initailCreate and 20260517113104_AddCVExtractedText, with
-- their seed roles, role claims and categories) after 90_rollback_drop_baseline_v2.sql. Generated with
-- "dotnet ef migrations script --idempotent" from tag pre-baseline-v2; do not edit by hand. Idempotent.
-- After this script, the P01 backend package can run against the database (no administrator exists: fail closed).
-- =====================================================================================================================

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [IsDefault] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [ProfilePhotoUrl] nvarchar(500) NULL,
        [ProfilePhotoPublicId] nvarchar(200) NULL,
        [IsProfileComplete] bit NOT NULL,
        [IsDisabled] bit NOT NULL,
        [ExperienceYears] int NOT NULL,
        [CareerLevel] nvarchar(50) NOT NULL,
        [ShowSalary] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [JobCategories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(100) NOT NULL,
        [IconUrl] nvarchar(500) NULL,
        [IconPublicId] nvarchar(200) NULL,
        CONSTRAINT [PK_JobCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [CVs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Url] nvarchar(500) NOT NULL,
        [PublicId] nvarchar(200) NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [UploadedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_CVs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CVs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] int NOT NULL IDENTITY,
        [Token] nvarchar(500) NOT NULL,
        [ExpiresOn] datetime2 NOT NULL,
        [CreatedOn] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [RevokedOn] datetime2 NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [UserPreferences] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [PreferredJobTypes] nvarchar(200) NOT NULL,
        [PreferredWorkplaceTypes] nvarchar(100) NOT NULL,
        [InterestedCategoryIds] nvarchar(200) NOT NULL,
        [InterestedJobTitles] nvarchar(500) NOT NULL,
        [MinSalary] decimal(18,2) NULL,
        CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPreferences_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [Jobs] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Location] nvarchar(200) NOT NULL,
        [Skills] nvarchar(1000) NOT NULL,
        [JobType] nvarchar(50) NOT NULL,
        [WorkplaceType] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [SalaryMin] decimal(18,2) NULL,
        [SalaryMax] decimal(18,2) NULL,
        [CategoryId] int NOT NULL,
        [PostedById] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [ExpiresAt] datetime2 NULL,
        CONSTRAINT [PK_Jobs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Jobs_AspNetUsers_PostedById] FOREIGN KEY ([PostedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Jobs_JobCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [JobCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [Applications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [JobId] int NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [AIScore] real NULL,
        [AIFeedback] nvarchar(max) NULL,
        [IsAIScored] bit NOT NULL,
        [AppliedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_Applications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Applications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Applications_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE TABLE [SavedJobs] (
        [UserId] nvarchar(450) NOT NULL,
        [JobId] int NOT NULL,
        [SavedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_SavedJobs] PRIMARY KEY ([UserId], [JobId]),
        CONSTRAINT [FK_SavedJobs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SavedJobs_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'IsDefault', N'IsDeleted', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] ON;
    EXEC(N'INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [IsDefault], [IsDeleted], [Name], [NormalizedName])
    VALUES (N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890'', N''A1B2C3D4-E5F6-7890-ABCD-EF1234567891'', CAST(0 AS bit), CAST(0 AS bit), N''Admin'', N''ADMIN''),
    (N''D2E3B4C5-F6A7-8901-BCDE-F01234567892'', N''B2C3D4E5-F6A7-8901-BCDE-F01234567893'', CAST(1 AS bit), CAST(0 AS bit), N''User'', N''USER'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'IsDefault', N'IsDeleted', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IconPublicId', N'IconUrl', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[JobCategories]'))
        SET IDENTITY_INSERT [JobCategories] ON;
    EXEC(N'INSERT INTO [JobCategories] ([Id], [IconPublicId], [IconUrl], [Name], [Slug])
    VALUES (1, NULL, NULL, N''Software & IT'', N''software-it''),
    (2, NULL, NULL, N''Banking & Finance'', N''banking-finance''),
    (3, NULL, NULL, N''Marketing'', N''marketing''),
    (4, NULL, NULL, N''Healthcare'', N''healthcare''),
    (5, NULL, NULL, N''Engineering'', N''engineering''),
    (6, NULL, NULL, N''Education'', N''education''),
    (7, NULL, NULL, N''Sales'', N''sales''),
    (8, NULL, NULL, N''Design & Creative'', N''design-creative'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IconPublicId', N'IconUrl', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[JobCategories]'))
        SET IDENTITY_INSERT [JobCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClaimType', N'ClaimValue', N'RoleId') AND [object_id] = OBJECT_ID(N'[AspNetRoleClaims]'))
        SET IDENTITY_INSERT [AspNetRoleClaims] ON;
    EXEC(N'INSERT INTO [AspNetRoleClaims] ([Id], [ClaimType], [ClaimValue], [RoleId])
    VALUES (1, N''permissions'', N''jobs:read'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (2, N''permissions'', N''jobs:create'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (3, N''permissions'', N''jobs:update'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (4, N''permissions'', N''jobs:delete'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (5, N''permissions'', N''applications:read'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (6, N''permissions'', N''applications:create'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (7, N''permissions'', N''applications:update'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (8, N''permissions'', N''categories:read'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (9, N''permissions'', N''categories:create'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (10, N''permissions'', N''categories:update'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (11, N''permissions'', N''categories:delete'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (12, N''permissions'', N''users:read'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (13, N''permissions'', N''users:create'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (14, N''permissions'', N''users:update'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (15, N''permissions'', N''users:delete'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (16, N''permissions'', N''roles:read'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (17, N''permissions'', N''roles:create'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (18, N''permissions'', N''roles:update'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (19, N''permissions'', N''roles:delete'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (20, N''permissions'', N''cv:upload'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (21, N''permissions'', N''cv:delete'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (22, N''permissions'', N''savedjobs:read'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (23, N''permissions'', N''savedjobs:create'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (24, N''permissions'', N''savedjobs:delete'', N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890''),
    (25, N''permissions'', N''jobs:read'', N''D2E3B4C5-F6A7-8901-BCDE-F01234567892''),
    (26, N''permissions'', N''applications:read'', N''D2E3B4C5-F6A7-8901-BCDE-F01234567892''),
    (27, N''permissions'', N''applications:create'', N''D2E3B4C5-F6A7-8901-BCDE-F01234567892''),
    (28, N''permissions'', N''categories:read'', N''D2E3B4C5-F6A7-8901-BCDE-F01234567892''),
    (29, N''permissions'', N''cv:upload'', N''D2E3B4C5-F6A7-8901-BCDE-F01234567892''),
    (30, N''permissions'', N''cv:delete'', N''D2E3B4C5-F6A7-8901-BCDE-F01234567892''),
    (31, N''permissions'', N''savedjobs:read'', N''D2E3B4C5-F6A7-8901-BCDE-F01234567892''),
    (32, N''permissions'', N''savedjobs:create'', N''D2E3B4C5-F6A7-8901-BCDE-F01234567892''),
    (33, N''permissions'', N''savedjobs:delete'', N''D2E3B4C5-F6A7-8901-BCDE-F01234567892'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClaimType', N'ClaimValue', N'RoleId') AND [object_id] = OBJECT_ID(N'[AspNetRoleClaims]'))
        SET IDENTITY_INSERT [AspNetRoleClaims] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_Applications_JobId] ON [Applications] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Applications_UserId_JobId] ON [Applications] ([UserId], [JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CVs_UserId] ON [CVs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JobCategories_Slug] ON [JobCategories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_Jobs_CategoryId] ON [Jobs] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_Jobs_Location] ON [Jobs] ([Location]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_Jobs_PostedById] ON [Jobs] ([PostedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_Jobs_Status] ON [Jobs] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_Jobs_Title] ON [Jobs] ([Title]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE INDEX [IX_SavedJobs_JobId] ON [SavedJobs] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPreferences_UserId] ON [UserPreferences] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429160411_initailCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260429160411_initailCreate', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517113104_AddCVExtractedText'
)
BEGIN
    ALTER TABLE [CVs] ADD [ExtractedText] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517113104_AddCVExtractedText'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260517113104_AddCVExtractedText', N'10.0.14');
END;

COMMIT;
GO

