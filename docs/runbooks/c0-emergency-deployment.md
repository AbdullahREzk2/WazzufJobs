# Runbook: Phase C0 emergency deployment

> **Production deployment is NOT authorized.** Execute only after D20 final approval. Revision 3, 2026-10-30.
> Condensed version for deployment day: `c0-go-live-checklist.md`.
> Golden rule: **a failed deployment must never silently restore the known vulnerabilities.** On failure, the default action is to take the API **offline** (§5).

## 0. Artifacts (built locally, outside the repository)
Location: `F:\.NET study\Projects\WazzufJobs_release\C0-2026-10-30\`

| Artifact | Content |
|---|---|
| `WazzufJobs.API-C0-win-x86.zip` | Release build. SHA-256 `9e4d14ebddd77b0af7f63eb553b9e1f24370647c102675252b77d8a405cfa83b` (also in `.sha256`). Verify before uploading: `certutil -hashfile WazzufJobs.API-C0-win-x86.zip SHA256`. |
| `backend-package\` | Same content unzipped: 94 files. **Excludes** `appsettings*.json`, `web.config` and `*.pdb`. |
| `backend-package-manifest.txt` | SHA-256 + path of every package file (input for §6) |
| `frontend-dist\browser\` | Angular production build, Angular 21.2.25 (API base `https://wazzufjobs.runasp.net/api`). Reference only: Vercel builds from source. |
| `reference\web.config.generated` | The `web.config` the publish would generate. For comparison only. |
| `_verify\baseline-publish\` | Pre-C0 build published the same way (used for the compatibility check) |
| `tools\c0\deploy\` (in repo) | `app_offline.htm`, `restricted-rollback.web.config.snippet.xml`, `post-deploy-checks.ps1`, `verify-rate-limit.ps1`, `compare-server-files.ps1` |

### 0.1 Compatibility verification (done locally)
| Check | Result |
|---|---|
| Target runtime | `net9.0`; requires `Microsoft.NETCore.App` 9.0.0 + `Microsoft.AspNetCore.App` 9.0.0 (roll-forward to any installed 9.0.x). **Identical to the pre-C0 build**, which production (IIS 10, `Microsoft-IIS/10.0` header) currently runs. |
| Architecture / hosting | `win-x86` (PE32 apphost), framework-dependent, in-process ANCM. Same as the pre-C0 publish profile `site69433`. |
| NuGet dependency set | **Identical**: 60 libraries in `deps.json`, none added or removed. C0 uses only shared-framework features (rate limiting, health checks). `System.Security.Cryptography.Pkcs` is pinned to 8.0.2 (CVE-2026-84008) to clear the scanner; at runtime the host's .NET 9 shared-framework copy is used, so keep the host on the latest 9.0.x patch. |
| File set | Same 94 files as the pre-C0 publish, minus the excluded config and `.pdb` files. No file is removed or renamed. |
| Changed files | `WazzufJobs.API.dll/.exe`, `WazzufJobs.BLL.dll`, `WazzufJobs.DAL.dll`, `WazzufJobs.Shared.dll`; 3 email templates differ only in line endings |
| Generated `web.config` | Byte-identical to the pre-C0 one (`processPath=".\WazzufJobs.API.exe"`, `hostingModel="inprocess"`), so the server's existing `web.config` remains valid |
| New configuration | None required. Safe code defaults: `Features:CvUploadsEnabled=false`, `RateLimiting:AuthPermitLimitPerMinute=20`, `Diagnostics:LifecycleLogEnabled=true`; `AdminBootstrap:*` absent, so the admin seeder is skipped. |
| Server writes | Only `App_Data\lifecycle.log` (failures are caught and logged; the app never crashes on it) |
| Scans | `dotnet list package --vulnerable --include-transitive`: **0** for all projects. `npm audit --omit=dev`: **0** (Angular 21.2.13 → 21.2.25 patch, which fixes the XSS sanitization bypasses; unused `@microsoft/signalr`/`ws` removed). Dev-only tooling advisories remain (not shipped). The C0 DLLs contain no legacy credential strings (UTF-16 scan); the pre-C0 DAL contained `Admin@123456`. |
| Not verifiable from here [U] | Exact .NET 9 patch installed on the host; server `ASPNETCORE_ENVIRONMENT` value (if it is `Development`, Swagger stays exposed, which post-deploy check 1 detects); where production secrets are stored |

## 1. Deployment gates (ALL required; record evidence in the containment report)
| Gate | Requirement | Evidence |
|---|---|---|
| **G1** | **Production DB backup taken in the control panel, DOWNLOADED, and SUCCESSFULLY RESTORED locally** (`WazzufJobs_ProdRestore`), with `tools/c0/sql/01_readonly_production_assessment.sql` run on the restore | Backup file name, size and time; restore completed without error; result 1 row counts recorded |
| G2 | VER-01 classification recorded (test-only vs real third-party data) | Your written statement |
| G3 | Production admin password **rotated**; admin login history (result 5) reviewed with no unknown logins, or an incident declared | Date of rotation; review outcome |
| G4 | **Do not change the admin email before deployment** (see §5.4 hazard) | n/a |
| G5 | Location of production secrets confirmed (server `appsettings.json` / `web.config` env vars / control panel), and the server `ASPNETCORE_ENVIRONMENT` value known | Your note |
| G6 | Downloaded copy of the **current site files** (zip of the site root), used for §5 and §6 | Zip name and size |
| G7 | Server `appsettings.json` does **not** set `Features:CvUploadsEnabled=true` | Checked |
| G8 | Vercel deployment method known (CLI vs Git integration); current production deployment noted for instant rollback | Deployment id |
| G9 | Two test accounts on production: a candidate with an application, and a second candidate with an application whose id you note | Ids noted (no passwords recorded) |
| G11 | **Content of this release confirmed.** Besides C0, the build contains the 44 previously uncommitted changes (Google sign-in, profile photo, admin user details, status-update email template). Check whether they are already live: open `https://wazzufjobs.runasp.net/swagger` and look for `POST /api/Auth/google`. If it is absent, these features go live for the first time with C0, so confirm that is intended. | Yes/No + decision |
| G12 | Google sign-in configuration known: if the server sets `Google:ClientId`, Google sign-in becomes active. It is now hardened (verified emails only; no linking to unconfirmed accounts). | Note |
| G10 | D20 final approval given | Approval message |

## 2. Deploy backend (≈10 min)
1. Upload `tools/c0/deploy/app_offline.htm` to the site root. The app stops and visitors see a maintenance page.
2. Upload **the contents** of `backend-package\`, overwriting files. **Do not delete anything.** `web.config`, `appsettings*.json`, `App_Data\`, `logs\` and `.well-known\` must remain.
3. Delete `app_offline.htm`.
4. Within 2 min, `GET https://wazzufjobs.runasp.net/health` must return `Healthy`. Otherwise go to **§5**.

Visual Studio publish (`site69433`) is allowed **only if G5 confirmed the server `appsettings.json` contains no secrets**, because VS uploads the repository `appsettings.json` (empty secrets) and a regenerated `web.config`.

## 3. Deploy frontend (≈5 min, after the backend is healthy)
- CLI-linked: in `SrcCode/Frontend/wazzuf-jobs-frontend` run `npx vercel --prod`.
- Git integration: merge the approved branch and push (only if that is the established route).

The old frontend works against the new backend: score widgets and CV links are simply empty. So a frontend failure is never a security regression.

## 4. Post-deployment verification
1. `powershell -File tools\c0\deploy\post-deploy-checks.ps1 -OtherApplicationId <G9 id> -JobId <job id>`. Anonymous checks always run. Authenticated checks ask for the **test candidate** credentials interactively (never the admin).
2. Rate limit, **safely**: `powershell -File tools\c0\deploy\verify-rate-limit.ps1`.
   - The script uses random **non-existent** `@example.invalid` addresses. The login handler rejects them before any password check, so **no real account, including the administrator, can be locked out** (proved by the automated test `Rate_limit_probe_with_unknown_emails_never_touches_a_real_account_lockout`).
   - Your own IP is throttled on `/api/Auth/*` for ≤ 60 s afterwards. Run it from a different network, or wait 60 s before logging in.
   - If `lifecycle.log` shows `remoteAddressClass=private|loopback`, the host is behind a proxy and the limit is effectively shared by everyone. Run the probe at a quiet time.
3. Manual checks:

| # | Check | Expected |
|---|---|---|
| M1 | Admin → Applications → applicant → **Download CV** | PDF downloads (or a clear "unavailable" message if the spike showed legacy signed download fails) |
| M2 | Candidate dashboard | No score anywhere; status and applied date shown |
| M3 | Candidate Profile → upload CV | "CV uploads are temporarily unavailable…" |
| M4 | Admin → **Account security** → change password → sign out → sign in | Works |
| M5 | File Manager → `App_Data/lifecycle.log` | `started  cvUploadsEnabled=False` and a `first-request` line |
| M6 | `https://wazzuf-jobs.vercel.app` | Demo notice visible and dismissible |
| M7 | Core flows: register/login, browse jobs, apply with an existing CV, admin job/category management | Unchanged |

Record all results in the containment report under "Production verification".

## 5. Failure handling (rollback without re-exposure)

### 5.1 Decision tree
```
Failure detected (health not OK, 5xx, core flow broken, security check failing)
  │
  ├─► STEP 1 (always, immediately): upload app_offline.htm  → API offline, NO vulnerable code running
  │
  ├─► STEP 2: diagnose from App_Data/lifecycle.log, logs\, control-panel logs
  │      └─ fixable (missing file, config) → fix-forward with the C0 package → remove app_offline.htm → §4
  │
  ├─► STEP 3 (only if service must return before a fix): RESTRICTED ROLLBACK (§5.2)
  │
  └─► Unrestricted rollback (pre-C0 build with nothing blocked) ONLY with your explicit written
      acceptance of re-exposing S1/S3/S4/S13. Never the default.
```

### 5.2 Restricted rollback (pre-C0 build + IIS deny rules)
1. Keep `app_offline.htm` in place.
2. Check the **seeder hazard** (§5.4): an account with email `admin@wazzuf.com` must exist and carry the rotated password. If it does not, **do not roll back**; stay offline.
3. Restore the pre-C0 site files from the G6 zip, **except** `web.config`.
4. Download the server's current `web.config`. Insert the `<security>` block from `tools/c0/deploy/restricted-rollback.web.config.snippet.xml` inside `<system.webServer>`, keeping everything else (including any `<environmentVariables>`). Upload it.
5. Remove `app_offline.htm`. Verify that all of the following return **404**:
   - `/api/Applications/1`
   - `/api/applications/job/1`
   - `/api/CV`
   - `/api/cv/file`
   - `/swagger/v1/swagger.json`
   - `/index.html`
   - `/hubs/scoring`

   If **any** returns something else (request filtering is not honoured on the host), put `app_offline.htm` back and stay offline.
6. Effect: applications, applying and the CV feature are unavailable; login, jobs, saved jobs and admin job/category/user management still work. No applicant data, AI score, CV URL or API documentation is reachable.
7. Frontend: either keep the C0 frontend (it tolerates the missing endpoints) or promote the previous Vercel deployment. Neither re-exposes data on its own.

### 5.3 Database
C0 makes **no schema or data changes**, so no DB rollback is needed for C0. The G1 backup is only for unrelated corruption.

### 5.4 Rollback hazard: default-admin re-seeding
The **pre-C0** build creates `admin@wazzuf.com` / `Admin@123456` whenever no account with that email exists. So:
- do **not** change the admin email until C0 has run successfully for ≥ 7 days and rollback is no longer expected (§7);
- if the email has already been changed, **never** restore the pre-C0 build (stay offline and fix forward).

## 6. Obsolete file cleanup plan (separate maintenance step, after ≥ 7 stable days)
Uploading the package overwrites files but never deletes any. The publish profile also uses `SkipExtraFilesOnServer=true`, so files from earlier deployments may still be on the server.
1. Download a fresh copy of the site root. This is also the backup for this step.
2. Run `powershell -File tools\c0\deploy\compare-server-files.ps1 -ServerCopy <folder> -PackageManifest backend-package-manifest.txt`. It is **read-only** and classifies every file:
   - **IN-PACKAGE**: current files; keep.
   - **PROTECTED (never delete)**: `web.config`, `appsettings*.json`, `App_Data\`, `logs\`, `.well-known\` (Let's Encrypt), `app_offline.htm`.
   - **OBSOLETE-CANDIDATE (review)**: `.dll/.exe/.pdb/.deps.json/.runtimeconfig.json` not in the package. Expected: old `*.pdb` files from the VS publish (debug symbols; safe to remove, and they disclose build paths).
   - **UNKNOWN**: keep unless proven unused.
3. Present the list for approval. Delete only approved OBSOLETE-CANDIDATE files, with `app_offline.htm` in place. Verify `/health` and run §4 again.
4. Never delete a DLL that the current `WazzufJobs.API.deps.json` references; the script never flags those, because they are all in the package.

## 7. Changing the admin account email (after deployment, owner action)
Verified by automated tests on a real SQL Server database (`AdminAccountSafetyTests`):
- Changing the email through **Admin → Users → Edit** keeps the Admin role (the form pre-selects current roles). Role updates apply only differences, in one transaction.
- `UserName` follows the email, `EmailConfirmed` stays true, login with the new email works and returns admin permissions, and the old email no longer logs in.
- The **last active administrator cannot lose the Admin role or be disabled** (C0 guard).

Procedure:
1. Wait until §5.4 no longer applies (≥ 7 stable days).
2. Admin → Users → Edit your account → change only the email; **leave "Admin" checked** → Save.
3. Sign out, sign in with the new email, open Admin → Account security.
4. If Save returns **"Invalid Roles"**, your account also holds the default "User" role. Existing validation rejects it before anything changes, so nothing was modified. Report this and do not work around it.
5. Optionally trigger "Forgot password" for the new email to confirm the mailbox receives mail.

## 8. After deployment
- Monitor `/health` and `lifecycle.log` for ≥ 7 days (VER-03).
- **CV uploads stay disabled** until the synthetic spike passes **and** D21b is approved. Re-enabling means setting `Features:CvUploadsEnabled=true` in the server configuration; it is a separate approved change.
- Existing public CVs remain public until D21 remediation (the next priority).
