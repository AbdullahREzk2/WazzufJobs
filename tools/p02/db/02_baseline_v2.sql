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
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [IsDefault] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [IsSystem] bit NOT NULL,
        [Description] nvarchar(256) NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [ProfilePhotoUrl] nvarchar(500) NULL,
        [ProfilePhotoPublicId] nvarchar(200) NULL,
        [IsDisabled] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [AccountType] tinyint NOT NULL,
        [IsDemo] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [TotpLastTimeStep] bigint NULL,
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
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [OccurredAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [ActorUserId] nvarchar(450) NULL,
        [ActorKind] tinyint NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [TargetType] nvarchar(100) NULL,
        [TargetId] nvarchar(100) NULL,
        [CompanyId] uniqueidentifier NULL,
        [IsDemo] bit NOT NULL,
        [Outcome] tinyint NOT NULL,
        [IpHash] binary(32) NULL,
        [UserAgent] nvarchar(200) NULL,
        [CorrelationId] nvarchar(100) NULL,
        [Details] nvarchar(4000) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [JobCategories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(100) NOT NULL,
        [IconUrl] nvarchar(500) NULL,
        [IconPublicId] nvarchar(200) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_JobCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [PlatformState] (
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PlatformState] PRIMARY KEY ([Key])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
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
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
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
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
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
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
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
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
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
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [CandidateProfiles] (
        [UserId] nvarchar(450) NOT NULL,
        [Headline] nvarchar(200) NULL,
        [Location] nvarchar(200) NULL,
        [ExperienceYears] int NOT NULL,
        [CareerLevel] tinyint NOT NULL,
        [ShowSalary] bit NOT NULL,
        [IsProfileComplete] bit NOT NULL,
        [PreferredJobTypes] nvarchar(max) NOT NULL,
        [PreferredWorkplaceTypes] nvarchar(max) NOT NULL,
        [InterestedCategoryIds] nvarchar(max) NOT NULL,
        [InterestedJobTitles] nvarchar(max) NOT NULL,
        [MinSalary] decimal(18,2) NULL,
        [IsDemo] bit NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CandidateProfiles] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_CandidateProfiles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Slug] nvarchar(120) NOT NULL,
        [Website] nvarchar(300) NULL,
        [Industry] nvarchar(100) NULL,
        [Size] nvarchar(50) NULL,
        [Location] nvarchar(200) NULL,
        [Description] nvarchar(max) NULL,
        [LogoKey] nvarchar(300) NULL,
        [VerificationStatus] tinyint NOT NULL,
        [Status] tinyint NOT NULL,
        [IsPlatform] bit NOT NULL,
        [IsDemo] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(450) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Companies_AspNetUsers_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [LegacyRefreshTokens] (
        [Id] int NOT NULL IDENTITY,
        [Token] nvarchar(500) NOT NULL,
        [ExpiresOn] datetime2 NOT NULL,
        [CreatedOn] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [RevokedOn] datetime2 NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_LegacyRefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LegacyRefreshTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [Resumes] (
        [Id] int NOT NULL IDENTITY,
        [CandidateId] nvarchar(450) NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [StorageKey] nvarchar(300) NOT NULL,
        [StorageAccess] tinyint NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Sha256] binary(32) NOT NULL,
        [IsDefault] bit NOT NULL,
        [Status] tinyint NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        [IsDemo] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Resumes] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Resumes_Id_CandidateId] UNIQUE ([Id], [CandidateId]),
        CONSTRAINT [FK_Resumes_AspNetUsers_CandidateId] FOREIGN KEY ([CandidateId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [UserRecoveryCodes] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [CodeHash] binary(32) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UsedAt] datetime2 NULL,
        CONSTRAINT [PK_UserRecoveryCodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserRecoveryCodes_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [UserSessions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastSeenAt] datetime2 NOT NULL,
        [AbsoluteExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedReason] nvarchar(50) NULL,
        [Amr] nvarchar(16) NOT NULL,
        [UserAgent] nvarchar(200) NULL,
        [IpHash] binary(32) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_UserSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserSessions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [CompanyMembers] (
        [CompanyId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Role] tinyint NOT NULL,
        [Status] tinyint NOT NULL,
        [AddedById] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CompanyMembers] PRIMARY KEY ([CompanyId], [UserId]),
        CONSTRAINT [FK_CompanyMembers_AspNetUsers_AddedById] FOREIGN KEY ([AddedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompanyMembers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompanyMembers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [Jobs] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Location] nvarchar(200) NOT NULL,
        [Skills] nvarchar(1000) NOT NULL,
        [JobType] tinyint NOT NULL,
        [WorkplaceType] tinyint NOT NULL,
        [Status] tinyint NOT NULL,
        [SalaryMin] decimal(18,2) NULL,
        [SalaryMax] decimal(18,2) NULL,
        [CategoryId] int NOT NULL,
        [CreatedById] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [ExpiresAt] datetime2 NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [PublishedAt] datetime2 NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDemo] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Jobs] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Jobs_Id_CompanyId] UNIQUE ([Id], [CompanyId]),
        CONSTRAINT [FK_Jobs_AspNetUsers_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Jobs_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Jobs_JobCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [JobCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [ResumeTexts] (
        [ResumeId] int NOT NULL,
        [ExtractedText] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ResumeTexts] PRIMARY KEY ([ResumeId]),
        CONSTRAINT [FK_ResumeTexts_Resumes_ResumeId] FOREIGN KEY ([ResumeId]) REFERENCES [Resumes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [AuthChallenges] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Purpose] tinyint NOT NULL,
        [TokenHash] binary(32) NOT NULL,
        [SessionId] uniqueidentifier NULL,
        [BindingHash] binary(32) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [ConsumedAt] datetime2 NULL,
        [Attempts] int NOT NULL,
        CONSTRAINT [PK_AuthChallenges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuthChallenges_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AuthChallenges_UserSessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [UserSessions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] bigint NOT NULL IDENTITY,
        [SessionId] uniqueidentifier NOT NULL,
        [ParentId] bigint NULL,
        [TokenHash] binary(32) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [ConsumedAt] datetime2 NULL,
        [RevokedAt] datetime2 NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_RefreshTokens_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [RefreshTokens] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RefreshTokens_UserSessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [UserSessions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [Applications] (
        [Id] int NOT NULL IDENTITY,
        [CandidateId] nvarchar(450) NOT NULL,
        [JobId] int NOT NULL,
        [Status] tinyint NOT NULL,
        [AIScore] real NULL,
        [AIFeedback] nvarchar(max) NULL,
        [IsAIScored] bit NOT NULL,
        [AppliedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [CompanyId] uniqueidentifier NOT NULL,
        [ResumeId] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDemo] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Applications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Applications_AspNetUsers_CandidateId] FOREIGN KEY ([CandidateId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Applications_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Applications_Jobs_JobId_CompanyId] FOREIGN KEY ([JobId], [CompanyId]) REFERENCES [Jobs] ([Id], [CompanyId]) ON DELETE CASCADE,
        CONSTRAINT [FK_Applications_Resumes_ResumeId_CandidateId] FOREIGN KEY ([ResumeId], [CandidateId]) REFERENCES [Resumes] ([Id], [CandidateId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [SavedJobs] (
        [CandidateId] nvarchar(450) NOT NULL,
        [JobId] int NOT NULL,
        [SavedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [IsDemo] bit NOT NULL,
        CONSTRAINT [PK_SavedJobs] PRIMARY KEY ([CandidateId], [JobId]),
        CONSTRAINT [FK_SavedJobs_AspNetUsers_CandidateId] FOREIGN KEY ([CandidateId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SavedJobs_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE TABLE [ApplicationStatusHistory] (
        [Id] bigint NOT NULL IDENTITY,
        [ApplicationId] int NOT NULL,
        [FromStatus] tinyint NULL,
        [ToStatus] tinyint NOT NULL,
        [ChangedById] nvarchar(450) NULL,
        [ChangedAt] datetime2 NOT NULL,
        [Reason] nvarchar(500) NULL,
        CONSTRAINT [PK_ApplicationStatusHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ApplicationStatusHistory_Applications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [Applications] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ApplicationStatusHistory_AspNetUsers_ChangedById] FOREIGN KEY ([ChangedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Description', N'IsDefault', N'IsDeleted', N'IsSystem', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] ON;
    EXEC(N'INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [Description], [IsDefault], [IsDeleted], [IsSystem], [Name], [NormalizedName])
    VALUES (N''C1E2A3D4-E5F6-7890-ABCD-EF1234567890'', N''A1B2C3D4-E5F6-7890-ABCD-EF1234567891'', NULL, CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), N''Admin'', N''ADMIN''),
    (N''D2E3B4C5-F6A7-8901-BCDE-F01234567892'', N''B2C3D4E5-F6A7-8901-BCDE-F01234567893'', NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), N''User'', N''USER'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Description', N'IsDefault', N'IsDeleted', N'IsSystem', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedById', N'Description', N'Industry', N'IsDemo', N'IsPlatform', N'Location', N'LogoKey', N'Name', N'Size', N'Slug', N'Status', N'VerificationStatus', N'Website') AND [object_id] = OBJECT_ID(N'[Companies]'))
        SET IDENTITY_INSERT [Companies] ON;
    EXEC(N'INSERT INTO [Companies] ([Id], [CreatedAt], [CreatedById], [Description], [Industry], [IsDemo], [IsPlatform], [Location], [LogoKey], [Name], [Size], [Slug], [Status], [VerificationStatus], [Website])
    VALUES (''6f1d2c3b-8a47-4e5f-9b0a-1c2d3e4f5a6b'', ''2026-11-01T00:00:00.0000000Z'', NULL, NULL, NULL, CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, N''WazzufJobs'', NULL, N''wazzufjobs'', CAST(0 AS tinyint), CAST(2 AS tinyint), NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedById', N'Description', N'Industry', N'IsDemo', N'IsPlatform', N'Location', N'LogoKey', N'Name', N'Size', N'Slug', N'Status', N'VerificationStatus', N'Website') AND [object_id] = OBJECT_ID(N'[Companies]'))
        SET IDENTITY_INSERT [Companies] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IconPublicId', N'IconUrl', N'IsActive', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[JobCategories]'))
        SET IDENTITY_INSERT [JobCategories] ON;
    EXEC(N'INSERT INTO [JobCategories] ([Id], [IconPublicId], [IconUrl], [IsActive], [Name], [Slug])
    VALUES (1, NULL, NULL, CAST(1 AS bit), N''Software & IT'', N''software-it''),
    (2, NULL, NULL, CAST(1 AS bit), N''Banking & Finance'', N''banking-finance''),
    (3, NULL, NULL, CAST(1 AS bit), N''Marketing'', N''marketing''),
    (4, NULL, NULL, CAST(1 AS bit), N''Healthcare'', N''healthcare''),
    (5, NULL, NULL, CAST(1 AS bit), N''Engineering'', N''engineering''),
    (6, NULL, NULL, CAST(1 AS bit), N''Education'', N''education''),
    (7, NULL, NULL, CAST(1 AS bit), N''Sales'', N''sales''),
    (8, NULL, NULL, CAST(1 AS bit), N''Design & Creative'', N''design-creative'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IconPublicId', N'IconUrl', N'IsActive', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[JobCategories]'))
        SET IDENTITY_INSERT [JobCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'UpdatedAt', N'Value') AND [object_id] = OBJECT_ID(N'[PlatformState]'))
        SET IDENTITY_INSERT [PlatformState] ON;
    EXEC(N'INSERT INTO [PlatformState] ([Key], [UpdatedAt], [Value])
    VALUES (N''SchemaBaseline'', ''2026-10-31T00:00:00.0000000Z'', N''v2'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'UpdatedAt', N'Value') AND [object_id] = OBJECT_ID(N'[PlatformState]'))
        SET IDENTITY_INSERT [PlatformState] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
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
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Applications_CandidateId] ON [Applications] ([CandidateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Applications_CompanyId_Status] ON [Applications] ([CompanyId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Applications_JobId_CandidateId] ON [Applications] ([JobId], [CandidateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Applications_JobId_CompanyId] ON [Applications] ([JobId], [CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Applications_ResumeId_CandidateId] ON [Applications] ([ResumeId], [CandidateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_ApplicationStatusHistory_ApplicationId_ChangedAt] ON [ApplicationStatusHistory] ([ApplicationId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_ApplicationStatusHistory_ChangedById] ON [ApplicationStatusHistory] ([ChangedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_AccountType] ON [AspNetUsers] ([AccountType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action_OccurredAt] ON [AuditLogs] ([Action], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ActorUserId_OccurredAt] ON [AuditLogs] ([ActorUserId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CompanyId_OccurredAt] ON [AuditLogs] ([CompanyId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_OccurredAt] ON [AuditLogs] ([OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AuthChallenges_ExpiresAt] ON [AuthChallenges] ([ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AuthChallenges_SessionId] ON [AuthChallenges] ([SessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AuthChallenges_TokenHash] ON [AuthChallenges] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_AuthChallenges_UserId_Purpose] ON [AuthChallenges] ([UserId], [Purpose]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Companies_CreatedById] ON [Companies] ([CreatedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Companies_Slug] ON [Companies] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Companies_Status_VerificationStatus] ON [Companies] ([Status], [VerificationStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Companies_SinglePlatform] ON [Companies] ([IsPlatform]) WHERE [IsPlatform] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_CompanyMembers_AddedById] ON [CompanyMembers] ([AddedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_CompanyMembers_ActivePerUser] ON [CompanyMembers] ([UserId]) WHERE [Status] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JobCategories_Slug] ON [JobCategories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Jobs_CategoryId] ON [Jobs] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Jobs_CompanyId_Status] ON [Jobs] ([CompanyId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Jobs_CreatedById] ON [Jobs] ([CreatedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Jobs_Location] ON [Jobs] ([Location]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Jobs_Status] ON [Jobs] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Jobs_Status_IsDemo_PublishedAt] ON [Jobs] ([Status], [IsDemo], [PublishedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Jobs_Title] ON [Jobs] ([Title]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_LegacyRefreshTokens_UserId] ON [LegacyRefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_ParentId] ON [RefreshTokens] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_RefreshTokens_ActivePerSession] ON [RefreshTokens] ([SessionId]) WHERE [ConsumedAt] IS NULL AND [RevokedAt] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_Resumes_CandidateId_Status] ON [Resumes] ([CandidateId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Resumes_StorageKey] ON [Resumes] ([StorageKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Resumes_DefaultPerCandidate] ON [Resumes] ([CandidateId]) WHERE [IsDefault] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_SavedJobs_JobId] ON [SavedJobs] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserRecoveryCodes_UserId_CodeHash] ON [UserRecoveryCodes] ([UserId], [CodeHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    CREATE INDEX [IX_UserSessions_UserId_RevokedAt] ON [UserSessions] ([UserId], [RevokedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    EXEC(N'CREATE TRIGGER [dbo].[TR_AuditLogs_AppendOnly] ON [dbo].[AuditLogs] INSTEAD OF UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 51000, N''AuditLogs is append-only.'', 1; END')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101054955_Baseline_v2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101054955_Baseline_v2', N'10.0.14');
END;

COMMIT;
GO

