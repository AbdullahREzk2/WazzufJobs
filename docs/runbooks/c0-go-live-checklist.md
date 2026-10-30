# C0 go-live checklist (print this)

> Condensed from `c0-emergency-deployment.md` (the full procedure). **Not authorized until D20.**
> Default reaction to any problem: **upload `app_offline.htm`** (API offline; no vulnerable code running).

## A. Before deployment day: gates
| ✔ | Gate | Owner check |
|---|---|---|
| ✔ | G1 | **Waived by owner:** disposable test data; no backup required (accepted risk: loss would be permanent) |
| ✔ | G2 | Owner confirmed: disposable test data |
| ☐ | G3 | Admin password rotated (the old one was public in README); all Admin-role accounts reviewed, unknown ones reported (not deleted) |
| ☐ | G4 | Admin email **unchanged** (rollback hazard) |
| ☐ | G5 | Secrets location and `ASPNETCORE_ENVIRONMENT` known |
| ☐ | G6 | Current site files downloaded (zip) |
| ☐ | G7 | Server config does not enable `Features:CvUploadsEnabled` |
| ✔ | G8 | GitHub ↔ Vercel not connected; manual `npx vercel --prod`. Note the current deployment id for rollback ☐ |
| ☐ | G9 | Two test candidates with applications; ids noted |
| ☐ | G11 | D24 decided: ship C0 together with the earlier uncommitted features (not yet live) or C0-only |
| ☐ | G12 | `Google:ClientId` on server known (empty means Google sign-in is disabled) |
| ☐ | Cloudinary | Credentials entered via user-secrets (`configuration-and-secrets.md` §2); `inventory` + `spike` run; summaries shared |
| ☐ | G10 | D20 approval received |

## B. Deployment (≈15 min)
1. ☐ `certutil -hashfile WazzufJobs.API-C0-win-x86.zip SHA256` gives `a088f67b207d5226193d5cf3dd47e5b1f0cbe7e2887044893569cb8927da803b`
2. ☐ Upload `app_offline.htm` to the site root
3. ☐ Upload the **contents** of `backend-package\`, overwriting; delete nothing (`web.config`, `appsettings*.json`, `App_Data`, `logs`, `.well-known` stay). **Never** use the VS publish profile for this release.
4. ☐ Delete `app_offline.htm`
5. ☐ `https://wazzufjobs.runasp.net/health` returns `Healthy` within 2 min. If not, go to **D**.
6. ☐ Frontend: `npx vercel --prod` (or the approved Git route)

## C. Security verification (≈15 min)
| ✔ | Check | Expected |
|---|---|---|
| ☐ | `post-deploy-checks.ps1` anonymous part | Swagger 404, `/index.html` 404, `/hangfire` 404, `/health` Healthy |
| ☐ | `post-deploy-checks.ps1 -OtherApplicationId … -JobId …` (test candidate) | 403 ×3; no AI fields; CV metadata without URL; upload returns 503 |
| ☐ | `verify-rate-limit.ps1` (non-existent emails only) | First 429 after request 20; wait 60 s before logging in |
| ☐ | Admin → Applications → Download CV | PDF or a clear "unavailable" message |
| ☐ | Admin → Account security → change password → sign in again | Works |
| ☐ | `App_Data/lifecycle.log` | `started  cvUploadsEnabled=False` and a `first-request` line (note `remoteAddressClass`) |
| ☐ | Frontend | Demo notice visible; no score anywhere; core flows work |

## D. Failure / rollback
| Situation | Action |
|---|---|
| Anything fails | **1. Upload `app_offline.htm` immediately.** 2. Diagnose (`lifecycle.log`, `logs\`). 3. Fix forward with the C0 package. |
| Service must return before a fix | **Restricted rollback** (runbook §5.2): confirm `admin@wazzuf.com` still exists → restore the pre-C0 site files except `web.config` → merge `restricted-rollback.web.config.snippet.xml` into the server `web.config` → remove `app_offline.htm` → curl checks return **404** for `/api/Applications/1`, `/api/applications/job/1`, `/api/CV`, `/swagger/v1/swagger.json`, `/index.html`, `/hubs/scoring`. If any is not 404, go back offline. |
| Admin email already changed | **Never** restore the pre-C0 build (it would re-create `admin@wazzuf.com` with the former default password). Stay offline and fix forward. |
| Frontend only | Vercel → previous deployment → *Promote to Production*. Safe: it does not re-expose data. |
| Unrestricted rollback | Only with the owner's explicit written acceptance of re-exposing S1/S3/S4/S13 |
| Database | No C0 schema or data changes, so nothing to roll back. No backup exists (G1 waived): unrelated data loss would be permanent. |

## E. After go-live
- ☐ Record results in `docs/audit/c0-containment-report.md` (Production verification)
- ☐ Monitor `/health` + `lifecycle.log` for 7 days
- ☐ Keep CV uploads disabled until spike PASS + D21b
- ☐ D21 remediation of existing public CVs (next priority)
- ☐ After 7 stable days: admin email change (runbook §7); obsolete-file review (runbook §6)
