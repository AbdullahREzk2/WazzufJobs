# C0 deployment instructions (deployment day)

> **Status: EXECUTED 2026-10-30/31: deployed and verified; all checks PASS.** Results and references: `docs/audit/c0-containment-report.md` §3b.
> Full background, rollback details and rationale: `c0-emergency-deployment.md`.
> Default reaction to any problem: **upload `app_offline.htm`** (API offline; no vulnerable code running).

## Release
| Item | Value |
|---|---|
| Source | Branch `security/c0-emergency-containment`. Code commit `d1d27d9` (later commits change docs only). |
| Backend package | `F:\.NET study\Projects\WazzufJobs_release\C0-2026-10-30\WazzufJobs.API-C0-win-x86.zip` |
| SHA-256 | `afaa79026432e86e5c2d79e4e542f9eb80a17a842154d439d92e7ce5c9e7f09e` |
| Contents | 94 files; no `appsettings*.json`, `web.config` or `.pdb` (server configuration is never overwritten). Reproducible build (2 builds: 0 differences). Same runtime, dependency set and file set as the current production build. |
| Scope | All C0 security fixes **plus** the earlier uncommitted features (D24 approved): profile photo, admin user/application details, status-update e-mail template. Google sign-in is included but disabled. |
| Settings in force | CV uploads **disabled**, Google sign-in **disabled** (no `Google:ClientId`), no admin seeding, rate limit 20/min on `/api/Auth/*` |

## Owner decisions and confirmations (2026-10-30)
| ✔ | Item |
|---|---|
| ✔ | D24: ship the earlier uncommitted features with C0 |
| ✔ | G3: production admin password changed |
| ✔ | G1/G2: database is disposable test data; no backup required (accepted risk: loss would be permanent); no database changes in C0 |
| ✔ | G5: `ASPNETCORE_ENVIRONMENT=Production` on MonsterASP; Cloudinary credentials configured on the server |
| ✔ | G7: CV uploads stay disabled until separately approved |
| ✔ | G8: GitHub and Vercel are not connected; deployment is manual |
| ✔ | G12: Google sign-in stays disabled |
| ✔ | D21: 13 old test CV files deleted; Cloudinary raw inventory empty; private-storage spike 10/10 |
| ✔ | Demo banner wording approved |
| ✔ | The disabled test account stays until the future database reset |

## A. Preparation (before the maintenance window, ≈15 min)
1. **G6: back up the current site files** (needed for the restricted rollback). In MonsterASP → File Manager, select all files in the site root → *Compress* → download the zip. Alternatively download via FTP. Keep it until C0 has been stable for 7 days.
2. **Vercel rollback point:** Vercel dashboard → project `wazzuf-jobs` → Deployments → note the current **Production** deployment.
3. **G9: test data for the security checks.** You need:
   - one **test candidate** you can log in as (not the admin);
   - the **id of an application that belongs to a different user**, and its **job id**.

   Find the ids read-only (MonsterASP → Databases → your database → query tool):
   ```sql
   SELECT TOP 10 a.Id AS ApplicationId, a.JobId, u.Email FROM dbo.Applications a JOIN dbo.AspNetUsers u ON u.Id = a.UserId ORDER BY a.Id DESC;
   ```
   **Do not upload CVs on the current (pre-C0) site**: it would store them publicly.
4. Verify the package checksum (PowerShell):
   ```powershell
   (Get-FileHash "F:\.NET study\Projects\WazzufJobs_release\C0-2026-10-30\WazzufJobs.API-C0-win-x86.zip" -Algorithm SHA256).Hash
   # must equal AFAA79026432E86E5C2D79E4E542F9EB80A17A842154D439D92E7CE5C9E7F09E
   ```

## B. Deploy the backend (≈10 min, brief downtime)
1. File Manager → upload `F:\.NET study\Projects\WazzufJobs\tools\c0\deploy\app_offline.htm` to the **site root**. The API now shows the maintenance page.
2. Upload `WazzufJobs.API-C0-win-x86.zip` to the site root and **extract it there, overwriting existing files**. Alternatively upload the contents of the `backend-package\` folder via FTP. **Delete nothing.** `web.config`, `appsettings*.json`, `App_Data`, `logs` and `.well-known` stay as they are. Do **not** use the Visual Studio publish profile. Remove the uploaded zip from the server afterwards.
3. Delete `app_offline.htm` from the site root.
4. Within 2 minutes, `https://wazzufjobs.runasp.net/health` must show **`Healthy`**. If not, go to **E**.

## C. Deploy the frontend (≈5 min, after B4 is healthy)
```powershell
cd "F:\.NET study\Projects\WazzufJobs"
git status                     # must be clean, on branch security/c0-emergency-containment
cd "SrcCode\Frontend\wazzuf-jobs-frontend"
npx vercel --prod              # project is already linked (.vercel/project.json)
```

## D. Verification (≈15 min)
From `F:\.NET study\Projects\WazzufJobs` in PowerShell:
```powershell
powershell -ExecutionPolicy Bypass -File tools\c0\deploy\post-deploy-checks.ps1 -OtherApplicationId <id from A3> -JobId <job id from A3>
powershell -ExecutionPolicy Bypass -File tools\c0\deploy\verify-rate-limit.ps1     # then wait 60 s before logging in
```

| Check | Before (baseline 2026-10-30) | Expected after |
|---|---|---|
| `GET /health` | 404 | **200 `Healthy`** |
| `GET /swagger/v1/swagger.json` | **200 (public)** | **404** |
| `GET /index.html` (Swagger UI) | **200 (public)** | **404** |
| `GET /hangfire` | 401 | **404** |
| `POST /api/Auth/google` | 404 | **503** (disabled) |
| Candidate → other applicant's application / job applicants / CV | 200 (S1 leak) | **403** ×3 |
| Candidate `my-applications` | AI score visible | **no AI fields** |
| Candidate `GET /api/CV` | contains URL | **no URL** |
| Candidate CV upload | public upload | **503** (disabled) |
| Rate limit (non-existent e-mails only) | none | **429 after 20** |

Manual checks:
| ✔ | Check | Expected |
|---|---|---|
| ☐ | Admin login → Admin → **Account security** page opens | Works (do not change credentials now) |
| ☐ | Admin → Applications → applicant → Download CV | "unavailable" message (the old test files were deleted, which is expected) |
| ☐ | `App_Data/lifecycle.log` (File Manager) | `started  cvUploadsEnabled=False  googleSignInEnabled=False` + `first-request` line |
| ☐ | `https://wazzuf-jobs.vercel.app` | Demo notice; no Google button; no AI score anywhere; email/password login, job browsing and applying with an existing CV work |

All PASS means C0 is deployed. Record the results (copy the script output) for the containment report.

## E. If something fails
| Situation | Action |
|---|---|
| Anything fails | **1. Upload `app_offline.htm` immediately.** 2. Diagnose (`App_Data/lifecycle.log`, `logs\`, MonsterASP logs). 3. Fix forward with the C0 package. |
| Service must return before a fix | *(For the 2026-10-30 C0 deployment no A1 backup exists, so this option requires rebuilding the pre-C0 build from `master` `4aa438c` with separate approval.)* **Restricted rollback** (`c0-emergency-deployment.md` §5.2): confirm `admin@wazzuf.com` still exists → restore the A1 site files except `web.config` → merge `tools\c0\deploy\restricted-rollback.web.config.snippet.xml` into the server `web.config` → remove `app_offline.htm` → `/api/Applications/1`, `/api/applications/job/1`, `/api/CV`, `/swagger/v1/swagger.json`, `/index.html`, `/hubs/scoring` must all return **404**, otherwise go back offline. |
| Frontend only | Vercel → Deployments → the deployment noted in A2 → *Promote to Production* (safe; it does not re-expose data). |
| Unrestricted rollback | Only with your explicit written acceptance of re-exposing S1/S3/S4/S13 |
| Database | Nothing to roll back (C0 makes no database changes) |

## F. After deployment
- Share the verification output, then I record it in `docs/audit/c0-containment-report.md`.
- With your approval: push `security/c0-emergency-containment` and merge to `master` (pushing does not deploy).
- Monitor `/health` and `lifecycle.log` for 7 days.
- Still gated, each needing separate approval: enabling CV uploads (D21b), configuring Google sign-in, admin email change (after 7 stable days), obsolete-file cleanup, Phase 00.

## G. Execution record (2026-10-30/31)
| Step | Result |
|---|---|
| A1 site-file backup | ✖ **Not taken** (owner): accepted rollback limitation; recovery = maintenance mode + fix forward (see `c0-containment-report.md` §3b) |
| A4 checksum | ✅ `AFAA7902…5C9E7F09E` matched |
| A2 Vercel rollback point | `https://wazzuf-jobs-ft0mo1g3a-abdullahrezk2s-projects.vercel.app` |
| B backend | ✅ Deployed by owner; `/health` Healthy |
| C frontend | ✅ `https://wazzuf-jobs-dyjyoank4-abdullahrezk2s-projects.vercel.app`, Ready, aliased to `wazzuf-jobs.vercel.app` |
| D verification | ✅ Anonymous checks, rate limit (429 from request 21), authenticated 12/12, frontend bundle and CORS, owner manual checks: all PASS |
| E failure handling | Not needed |
