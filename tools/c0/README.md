# Phase C0 operator tools

Nothing here is deployed or referenced by the application. All tools are run manually by the project owner.

| Tool | Purpose | Modifies anything? |
|---|---|---|
| `sql/01_readonly_production_assessment.sql` | VER-01 production data classification, admin login review, VER-03 Hangfire latency | **No** (SELECT only) |
| `CloudinaryC0Tool inventory` | Lists raw (CV) assets; optionally joins with the CVs table of a **local restore** | **No** (Cloudinary list API only) |
| `CloudinaryC0Tool spike` | Synthetic test of authenticated storage and rename remediation | Creates and then deletes **synthetic** PDFs in `wazzuf-jobs/c0-spike/` only |
| `deploy/post-deploy-checks.ps1` | Post-deployment security checks (anonymous + test-candidate) | **No** |
| `deploy/verify-rate-limit.ps1` | Safe rate-limit probe using non-existent e-mails (no account lockout possible) | **No** (throttles only the caller's IP for ≤ 60 s) |
| `deploy/compare-server-files.ps1` | Classifies a downloaded server copy against the package manifest | **No** |
| `deploy/app_offline.htm` | Maintenance page: takes the API offline during deploy/failure | Only when uploaded by you |
| `deploy/restricted-rollback.web.config.snippet.xml` | IIS deny rules for an emergency pre-C0 rollback | Only when merged by you |
| `verification/fake_cloudinary.py` | Local fake Cloudinary to verify the tool without an account (localhost only) | No (local process) |

## Supplying Cloudinary credentials securely
Never paste secrets into chats, tickets or committed files. In **your own** PowerShell window:

```powershell
$env:CLOUDINARY_CLOUD_NAME = Read-Host "Cloud name"
$env:CLOUDINARY_API_KEY    = Read-Host "API key"
$sec = Read-Host "API secret" -AsSecureString
$env:CLOUDINARY_API_SECRET = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
```

The variables live only in that window. Close it when finished.

## Running
```powershell
cd tools\c0\CloudinaryC0Tool

# 1) read-only inventory (Cloudinary only)
dotnet run -- inventory

# 2) read-only inventory joined with the LOCALLY RESTORED production backup
dotnet run -- inventory --db "Server=.;Database=WazzufJobs_ProdRestore;Trusted_Connection=True;TrustServerCertificate=True"

# optional: record anonymous HEAD status of public CV URLs (no document content is downloaded)
dotnet run -- inventory --db "..." --probe-public

# 3) synthetic storage spike (decides whether CV uploads can be re-enabled)
dotnet run -- spike
```

The output is written to `%USERPROFILE%\WazzufJobs-C0\` (outside the repository). The manifest can contain user IDs, so keep it local.

## What happens next
- The inventory manifest plus your classification (own/test vs real third-party) is presented for **approval (D21)** before any asset is modified.
- Remediation (delete, or rename `to_type=authenticated` + `invalidate=true`) is a separate step that requires explicit approval. It is not implemented in this tool on purpose.
