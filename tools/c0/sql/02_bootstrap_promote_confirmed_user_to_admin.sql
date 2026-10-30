/* =====================================================================================
   WazzufJobs - controlled administrator bootstrap (interim, until Phase 03)
   -------------------------------------------------------------------------------------
   Promotes ONE existing, email-confirmed, enabled account to the Admin role.
   * Creates no account, sets no password, contains no credentials.
   * The person registers normally in the app (own password, own mailbox), confirms the
     email, and only then is promoted here.
   * DRY RUN by default (@Apply = 0). Set @Apply = 1 only after reviewing the dry-run output.
   * Running it against production requires explicit approval and is recorded in the docs.
   Run in SSMS, or: sqlcmd -S <server> -d <database> -I -i 02_bootstrap_promote_confirmed_user_to_admin.sql
   After promotion the user must sign out and sign in again to receive admin permissions.
   ===================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Email nvarchar(256) = N'<<REPLACE WITH THE CONFIRMED ACCOUNT EMAIL>>';
DECLARE @Apply bit = 0;   -- 0 = dry run (report only), 1 = apply

IF @Email LIKE N'<<%'
    THROW 50001, 'Set @Email to the e-mail of an existing, confirmed account first.', 1;

DECLARE @UserId nvarchar(450), @Confirmed bit, @Disabled bit;
SELECT @UserId = Id, @Confirmed = EmailConfirmed, @Disabled = IsDisabled
FROM dbo.AspNetUsers
WHERE NormalizedEmail = UPPER(LTRIM(RTRIM(@Email)));

IF @UserId IS NULL
    THROW 50002, 'No account with this e-mail. Register through the application first.', 1;
IF @Confirmed = 0
    THROW 50003, 'The e-mail of this account is not confirmed. Confirm it through the application first.', 1;
IF @Disabled = 1
    THROW 50004, 'This account is disabled.', 1;

DECLARE @AdminRoleId nvarchar(450) = (SELECT Id FROM dbo.AspNetRoles WHERE NormalizedName = N'ADMIN');
IF @AdminRoleId IS NULL
    THROW 50005, 'The Admin role does not exist in this database.', 1;

PRINT 'Current administrators:';
SELECT u.Email, u.IsDisabled
FROM dbo.AspNetUserRoles ur JOIN dbo.AspNetUsers u ON u.Id = ur.UserId
WHERE ur.RoleId = @AdminRoleId;

IF EXISTS (SELECT 1 FROM dbo.AspNetUserRoles WHERE UserId = @UserId AND RoleId = @AdminRoleId)
    PRINT 'No change: the account is already an administrator.';
ELSE IF @Apply = 0
    PRINT 'DRY RUN: the account would be promoted to Admin. Set @Apply = 1 to apply.';
ELSE
BEGIN
    BEGIN TRANSACTION;
        INSERT dbo.AspNetUserRoles (UserId, RoleId) VALUES (@UserId, @AdminRoleId);
        UPDATE dbo.AspNetUsers SET SecurityStamp = CONVERT(nvarchar(36), NEWID()) WHERE Id = @UserId;
    COMMIT TRANSACTION;
    PRINT 'Promoted to Admin. Sign out and sign in again to receive admin permissions.';
END
