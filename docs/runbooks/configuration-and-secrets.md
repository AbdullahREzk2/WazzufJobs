# Configuration, secrets and administrator bootstrap

Rules:
- No credential, API key, password or connection string is ever committed.
- `appsettings.json` in the repository contains **structure and safe defaults only**.
- Real values come from **.NET User Secrets** (local development) or **server-only configuration** (MonsterASP).
- `.gitignore` blocks `appsettings.Production.json`, `appsettings.*.local.json`, `.env*`, `*.secrets.json` and C0 operator outputs.

## 1. Configuration keys (WazzufJobs.API)
| Key | Secret | Required | Notes |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | **yes** | yes | SQL Server connection string |
| `Jwt:key` | **yes** | yes | HS256 signing key, ≥ 32 random characters |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | no | yes | Defaults in `appsettings.json` |
| `MailSettings:Username`, `MailSettings:FromEmail` | no (personal data) | yes | Validated at startup; not committed |
| `MailSettings:Password` | **yes** | yes | SMTP app password |
| `MailSettings:Host`, `Port`, `EnableSsl`, `FromName` | no | yes | Defaults in `appsettings.json` |
| `CloudinarySettings:CloudName` | no (identifies the account) | yes | Not committed |
| `CloudinarySettings:ApiKey` | **yes** | yes | |
| `CloudinarySettings:ApiSecret` | **yes** | yes | |
| `AISettings:ApiKey` | **yes** | yes | Groq key |
| `AISettings:Model`, `BaseUrl`, `MaxTokens` | no | no | Defaults in `appsettings.json` |
| `AppURL:BaseUrl`, `AppURL:FrontendUrl` | no | yes | |
| `Google:ClientId` | no | no | **Not configured in C0, so Google sign-in is disabled** (API 503, no call to Google). To enable it in a later phase, set the same OAuth client id on the server **and** in `environment.production.ts` (`googleClientId`, a public value), then rebuild and redeploy the frontend. |
| `Storage:DocumentProvider` | no | no | `Cloudinary` (default). Unknown values stop the app at startup (fail closed). |
| `Features:CvUploadsEnabled` | no | no | **false** (default). Set to true only after the private-storage spike passes **and** approval (D21b). |
| `RateLimiting:AuthPermitLimitPerMinute` | no | no | 20 |
| `Diagnostics:LifecycleLogEnabled` | no | no | true |
| `Security:DataKey` | **yes** | **yes** (Phase 02 S5; not deployed yet) | 32 random bytes, base64. Keys the audit log's client-address HMAC (and later TOTP encryption, S7). Outside Development the API **refuses to start** without a valid key; Development uses an ephemeral key with a warning. The owner generates it locally, for example `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))` in PowerShell, and sets it only in the server configuration (D-CONFIG, S11). Never commit or share it. Rotating it makes old address hashes uncorrelatable with new ones. |
| ~~`AdminBootstrap:*`~~ | n/a | n/a | **Removed.** Ignored if present; no account is ever created automatically. |

Environment variables use `__` instead of `:` (for example `CloudinarySettings__ApiSecret`).

## 2. Local development: .NET User Secrets
The API project already has a `UserSecretsId`. User secrets are stored outside the repository (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`) and are loaded when `ASPNETCORE_ENVIRONMENT=Development` (the default `launchSettings.json` profiles). The C0 Cloudinary tool reads the same store.

Enter Cloudinary credentials in **your own PowerShell window**. Values are typed into hidden prompts and piped as JSON, so they never appear on the command line, in shell history, or in the repository. Piped input **merges** with your existing secrets (verified).

```powershell
cd "F:\.NET study\Projects\WazzufJobs\SrcCode\WazzufJobs\WazzufJobs.API"

$cloud  = Read-Host "Cloudinary cloud name"
$key    = Read-Host "Cloudinary API key"
$secret = [Net.NetworkCredential]::new('', (Read-Host "Cloudinary API secret" -AsSecureString)).Password
@{ CloudinarySettings = @{ CloudName = $cloud; ApiKey = $key; ApiSecret = $secret } } |
    ConvertTo-Json | dotnet user-secrets set
Remove-Variable cloud, key, secret

# show key NAMES only (never print values)
dotnet user-secrets list | ForEach-Object { ($_ -split ' = ', 2)[0] }
```

The same pattern works for other secrets, for example:

```powershell
$smtp = [Net.NetworkCredential]::new('', (Read-Host "SMTP app password" -AsSecureString)).Password
@{ MailSettings = @{ Username = (Read-Host "SMTP user"); FromEmail = (Read-Host "From e-mail"); Password = $smtp } } |
    ConvertTo-Json | dotnet user-secrets set
Remove-Variable smtp
```

To remove a single value: `dotnet user-secrets remove "CloudinarySettings:ApiSecret"`.

## 3. Production: MonsterASP
The deployment package **never contains configuration files** (`appsettings*.json` and `web.config` are excluded), so server configuration is never overwritten by a deployment. Choose one method and keep it on the server only:

**A. Server-only `appsettings.Production.json` (recommended).**
- Create it with the File Manager in the site root, next to `WazzufJobs.API.dll`, containing only the secret and environment-specific keys from §1.
- It is loaded automatically when `ASPNETCORE_ENVIRONMENT` is `Production`, which is the default when the variable is not set.
- The app never serves files from its content root, so the file is not downloadable. Confirm after creation: `https://wazzufjobs.runasp.net/appsettings.Production.json` must return 404.

**B. `web.config` environment variables.**
Inside `<aspNetCore …>` in the server's `web.config`:
```xml
<environmentVariables>
  <environmentVariable name="CloudinarySettings__ApiSecret" value="…" />
</environmentVariables>
```

Rules for both:
- Back up the server file before editing.
- Never copy server configuration back into the repository.
- Rotate any value that was ever shared outside the server or your machine.
- After a change, `/health` must report `Healthy`, and `App_Data/lifecycle.log` shows the restart.

## 4. Administrator accounts
- The application **never creates an administrator automatically** and contains **no default credentials**. `AdminSeeder` is disabled at compile level, and the former `AdminBootstrap:*` settings are ignored. Tests cover both.
- **No administrator means fail-closed.** All administrative endpoints return 401 (anonymous) or 403 (any other user), self-registration never grants Admin, and startup logs a warning: *"No active administrator exists…"*. This is covered by tests.
- **Existing production administrator.** The `admin@wazzuf.com` account in the current production database is left untouched, as you instructed. Its original password the former default password was published in this repository's README from 2026-05-21 until C0, so treat the account as **potentially compromised**:
  - rotate its password before deployment;
  - review all accounts holding the Admin role;
  - review the admin login history.
- **Controlled bootstrap, interim until Phase 03** (`tools/c0/sql/02_bootstrap_promote_confirmed_user_to_admin.sql`):
  1. The intended administrator registers normally in the app, choosing their own password, and confirms their email.
  2. An operator runs the script with the email and `@Apply = 0` (dry run), reviews the output, then reruns with `@Apply = 1`.
  3. The administrator signs out and back in.

  The script creates no account and no password, refuses unconfirmed or disabled accounts, is idempotent, and was validated on a throwaway database. Running it against any shared or production database requires explicit approval.
- **Phase 03** replaces this with an in-application, one-time, audited bootstrap.

## 5. Cloudinary: read-only inventory and synthetic private-storage spike
Prerequisite: §2 completed (credentials in user secrets). Nothing in this section modifies existing assets.

```powershell
cd "F:\.NET study\Projects\WazzufJobs\tools\c0\CloudinaryC0Tool"

# 1) READ-ONLY inventory of raw (CV) assets; add --probe-public to record anonymous HEAD status
dotnet run -- inventory --probe-public

# 2) synthetic private-storage spike (creates and removes its own dummy PDFs in wazzuf-jobs/c0-spike/)
dotnet run -- spike
```

- Outputs are written to `%USERPROFILE%\WazzufJobs-C0\` (outside the repository).
- Share only the **console summaries**. The manifest stays on your machine.
- Expected spike result: every line `[PASS]`. Line 4 is informational: it shows whether public PDFs are deliverable on this account.
- If any check fails, CV uploads stay disabled and the result goes to D21b.
- Remediation of existing assets (delete or convert) is a separate step that requires your approval (D21).

## 6. Optional: end-to-end private upload through the application (local only)
After the spike passes, you can verify the application's own storage path locally:
1. Run `dotnet user-secrets set "Features:CvUploadsEnabled" "true"` (local secrets only).
2. Run the API locally, log in with a local test candidate, and upload a synthetic PDF.
3. Download it from the profile page.
4. Run `inventory` again and confirm the new asset has `type=authenticated`.

Afterwards, run `dotnet user-secrets remove "Features:CvUploadsEnabled"`.
