# Phase C0: Emergency Containment Report

Status: **implemented, tested locally, release artifacts prepared. NOT deployed.** Production deployment requires final approval (D20).
Revision 4 (2026-10-30): owner decisions applied (no administrator seeding, database backup waived, Cloudinary configuration and storage abstraction, manual Vercel deployments).

| Item | Location |
|---|---|
| Working branch | `security/c0-emergency-containment` (local commits only; **nothing pushed**) |
| Pre-C0 backup | git ref `refs/backups/pre-c0-2026-10-30` (`975b860`, includes the 44 modified + untracked files) and `../WazzufJobs_backups/WazzufJobs_pre-C0_2026-10-30.tar.gz` |
| C0 state backup | git ref `refs/backups/c0-ready-2026-10-30` (snapshot of this revision) |
| Release artifacts | `../WazzufJobs_release/C0-2026-10-30/` |
| Runbook | `docs/runbooks/c0-emergency-deployment.md` (full) and `docs/runbooks/c0-go-live-checklist.md` (one page) |

## 1. Findings addressed

| ID | Finding | Live status before C0 (2026-10-30) | C0 change |
|---|---|---|---|
| S1 | Any job seeker could read every application (name, email, CV link, AI score, salary) | Code-verified; live exploitation not tested | Admin application endpoints require `applications:update` (Admin only); temporary until Phase 03 |
| S4 | AI score/feedback shown to candidates (API, dashboard, SignalR, email, copy) | Code-verified | Removed from the candidate DTO and UI; email and push removed; copy rewritten; fabricated metrics removed |
| S2 | Hardcoded seeded admin `admin@wazzuf.com` with the former default password | Code-verified; production password status unknown | **Automatic administrator seeding removed entirely.** No account is ever created at startup; the former `AdminBootstrap:*` settings are ignored; `AdminSeeder` is disabled at compile level. Without an administrator the system fails closed. The first administrator comes from the controlled bootstrap (`configuration-and-secrets.md` §4). |
| S2b | `wazzuf.com` is a third-party registered domain (NS `dyna-ns.net`, no MX today) | DNS-verified | Owner changes the admin email **after** deployment (runbook §7) |
| S2d | **The default admin credentials were published in the public README** (`README.md`, "Default Admin Credentials") from commit `7815df6` (2026-05-21) until C0 | Verified in git history | Section removed and replaced by the no-default-credentials policy. **The production admin account must be treated as compromised:** rotate its password and review all Admin-role accounts (G3). The text remains in git history, which is acceptable because the password is rotated. |
| Config | Account identifiers committed in `appsettings.json` (Cloudinary cloud name, personal Gmail address) | Code-verified | Blanked; supplied via user secrets / server-side configuration. `.gitignore` now blocks `appsettings.Production.json`, `.env*`, `*.secrets.json` and C0 operator outputs. |
| Storage | CV storage code was tied to Cloudinary | n/a | Provider-neutral `IDocumentStorage` (Cloudinary implementation; `Storage:DocumentProvider`; unknown provider stops startup). Images keep `ICloudinaryService`. |
| S2c | **New:** the admin "Edit user" path deleted all roles before re-adding them, with no transaction and no last-admin protection, so production could be left without an administrator | Code-verified | Roles change by difference only, atomically on SQL Server; the **last active admin cannot lose the Admin role or be disabled** |
| S5 | Hangfire dashboard query-string credentials | Live: 401 (shadowed by an earlier default-protected mapping) | Dashboard mappings and hardcoded filter removed |
| S3 / B15 | Public CV uploads; URLs returned to clients; deletes probably never removed raw files | Code-verified; inventory 2026-10-30: 13 raw/upload test assets (owner's own); anonymous access to public raw PDFs returns 401 on this account (free-tier PDF block) | No public upload path; new uploads `authenticated`-only and **disabled by default**; URLs removed from every DTO; files served via authorized API (60 s signed server-side download); legacy assets untouched until D21 **Remediated 2026-10-30: all 13 legacy assets deleted (D21); 0 raw assets remain.** |
| S13 | Swagger publicly served | Live: `/swagger/v1/swagger.json` returns 200 | Swagger mapped only in Development |
| S7 | Rate limiting not configured | Code-verified | 20 req/min per client IP on `/api/Auth/*` |
| S8 | Google sign-in (part of the previously uncommitted work shipped with this release) accepted unverified Google emails and linked to unconfirmed local accounts (pre-hijack) | Code-verified; active only if `Google:ClientId` is set on the server | Requires `email_verified`; refuses to link to unconfirmed accounts (409); audience check kept. **Google sign-in is not configured on MonsterASP for C0, so it is disabled end to end:** the API returns 503 without contacting Google or creating accounts (tested, including a whitespace-only client id); the login page hides the button and never loads Google's script (6 frontend tests); startup logs `googleSignInEnabled=False`. Email/password login is unaffected (tested). |
| Deps | `System.Security.Cryptography.Pkcs 8.0.1` (High, CVE-2026-84008, via MimeKit; not reachable: no S/MIME verification) and Angular 21.2.13 (High: XSS sanitization bypasses, DoS) | Scanner-verified | Pkcs pinned to 8.0.2; Angular patched to 21.2.25 (build 21.2.29); unused `@microsoft/signalr` (+ vulnerable `ws`) removed. Both scans now 0 for shipped dependencies. |
| (new) | Password change/reset did not revoke refresh tokens | Code-verified | All active refresh tokens revoked |
| (new) | No admin password-change UI | Code-verified | Admin → **Account security** |
| VER-03 | Host lifecycle unknown | n/a | `App_Data/lifecycle.log`: start/stop + `cvUploadsEnabled` state + one-time `first-request` line with remote address class (loopback/private/public) and whether `X-Forwarded-For` is present (no IP stored) |
| VER-06 | HTTPS | Live: Let's Encrypt, valid to 2026-12-22 | none needed |
| Demo | Real candidates uploading CVs | n/a | Dismissible demo banner (wording needs approval) |

## 2. Release-readiness verification (this revision)

### 2.1 Runtime and server compatibility (item 1)
The pre-C0 snapshot was published the same way and compared with the C0 package (details in runbook §0.1):
- same target framework (`net9.0`, ASP.NET Core 9.0.0 shared framework), `win-x86` in-process, identical generated `web.config`;
- **identical NuGet dependency set** (60 libraries) and identical file set; only the application DLLs/EXE changed;
- production answers as `Microsoft-IIS/10.0` and already runs the .NET 9 build;
- no new configuration required (safe defaults). The package excludes server configuration files.

Not verifiable without server access: exact .NET 9 patch, `ASPNETCORE_ENVIRONMENT` value, secret storage location. Covered by gate G5 and post-deploy check 1.

### 2.2 Obsolete files (item 2)
The package adds no files and removes none relative to the current build, so deployment cannot leave a C0-specific orphan. Leftovers from **earlier** deployments (`SkipExtraFilesOnServer=true`) are handled by a separate, approval-gated cleanup: the read-only `compare-server-files.ps1` classifies files as IN-PACKAGE / PROTECTED / OBSOLETE-CANDIDATE / UNKNOWN (runbook §6). It was dry-run on a simulated server copy: config, `App_Data` and `logs` were classified PROTECTED, and stale `.pdb` files plus an unknown DLL were flagged for review only.

### 2.3 Rollback without re-exposure (item 3)
- Default failure action: **take the API offline** (`app_offline.htm` maintenance page).
- If service must return before a fix: **restricted rollback**, meaning the pre-C0 build plus IIS request-filtering rules that return 404 for `/api/Applications*`, `/api/CV*`, `/swagger*`, `/index.html`, `/hangfire`, `/hubs/scoring`. The rules must be verified with curl, falling back to offline if the host ignores them.
- Unrestricted rollback only with your explicit written acceptance.
- **Hazard found:** the pre-C0 seeder re-creates `admin@wazzuf.com` with the former default password if that email no longer exists, so the admin email must not change until rollback is no longer expected.

### 2.4 Safe rate-limit verification (item 4)
`verify-rate-limit.ps1` uses random non-existent `@example.invalid` emails. Unknown emails are rejected before any password check, so **no account lockout counter can change**; an automated test proves this. The caller's IP is throttled for ≤ 60 s; instructions cover that, plus the proxy case.

### 2.5 Admin email change (item 5)
Verified on a throwaway **real SQL Server** database (`WazzufJobs_C0Test_*`, created and dropped by the tests; `WazzufJobsDB` untouched; 0 leftovers confirmed):
- email change keeps the Admin role and EmailConfirmed; UserName follows the email; login with the new email works and still grants admin permissions; the old email fails;
- the last active administrator can be neither demoted nor disabled;
- role changes apply only differences, in a transaction.

**Pre-existing limitation found:** the existing role validation rejects the default "User" role, so the admin form cannot save any account that holds it. In practice, admins cannot edit candidates. It fails safely, before any change. If the admin account also holds "User" (SQL result 3), the email change returns "Invalid Roles" without modifying anything. Fix planned for Phase 03.

### 2.6 Backup gate (item 6)
**Waived by the owner on 2026-10-30.** The production database contains disposable test data and no backup is required for preservation. **Accepted risk:** without a backup, any loss or corruption of the existing data is permanent. C0 makes no database changes, and any reset, deletion or migration still requires separate approval. The new SQL Server schema is designed in the modernization phases according to the approved architecture.

### 2.6a Administrator policy (owner decision 2026-10-30)
- **No automatic account creation** and **no default credentials** anywhere: code, configuration, README and tests. Tests prove that startup creates no administrator even when the old bootstrap settings are present, and that the shipped DLLs contain no default credentials.
- **Fail-safe without an administrator.** Admin endpoints return 401/403 for everyone, self-registration never grants Admin, and startup logs a warning. Tests cover all three.
- **Does deferring administrator creation block anything? No, for C0.**
  - Production verification uses the **existing** production administrator (left untouched, as you instructed; password rotation G3).
  - Automated tests create their own administrators in isolated databases.
  - Moderation features do not exist yet; they arrive in later phases.
  - Only a **new or reset** environment would have no administrator. For that, the interim **controlled bootstrap** promotes an existing, email-confirmed account through a guarded, dry-run-first SQL script (`tools/c0/sql/02_bootstrap_promote_confirmed_user_to_admin.sql`, validated on a throwaway database). Phase 03 replaces it with an in-application, one-time, audited bootstrap.
- **Rollback hazard unchanged:** the pre-C0 build re-creates `admin@wazzuf.com` with the former default password if that email is missing. Keeping the production admin record, as you instructed, keeps the restricted rollback viable.

### 2.7 Leak audit (item 7)
- **Contracts:** AI fields exist only in `ApplicationResponse` / `ApplicationDetailResponse`, which are reachable only through the two admin-gated endpoints. `CV.Url` is now read only internally, to determine the delivery type. No DTO, mapping (Mapster), email, SignalR send, header or log carries a CV URL.
- **Frontend:** admin-only API calls are made only from admin components; candidate models contain no AI or URL fields; nothing uses SignalR.
- **Automated sweep (`LeakSweepTests`):** every GET endpoint of the running API is called as a job seeker and anonymously, with route values pointing at another applicant's data. It asserts there is no other-applicant email, assessment text, AI field name, CV storage path, or Cloudinary header. A mutation check (S1 reintroduced) made the sweep fail on both admin endpoints.
- **Not covered by the sweep:** non-GET responses. Reviewed manually: apply returns only a Location header; upload returns `{id, fileName, uploadedAt}`; other mutations return 204 or ProblemDetails.

### 2.8 CV uploads stay disabled (item 8)
- Code default `false`; `appsettings.json` `false`.
- Gate G7 checks the server config.
- The state is logged at startup and in `lifecycle.log`.
- Post-deploy check expects HTTP 503.
- Re-enabling requires the synthetic spike to pass **and** D21b approval.

### 2.9 Preservation (item 9)
No existing work was discarded. The pre-C0 snapshot and archive are unchanged. The C0 state is additionally saved as `refs/backups/c0-ready-2026-10-30`. No configuration file on any server or in `appsettings.Development.json` was changed. The local dev database `WazzufJobsDB` was not touched.

### 2.10 Cloudinary remediation (2026-10-30)
| Step | Result |
|---|---|
| Private-storage spike (real account) | **10/10 PASS**: authenticated upload, anonymous access refused (401), signed download, legacy signed download, rename to authenticated, old public URL 404, cleanup of the synthetic assets. Public raw PDFs return 401 anonymously on this account (free-tier PDF block). |
| Read-only inventory | 13 raw assets, all `type=upload` (12 in `wazzuf-jobs/cvs/`, 1 in `wazzuf-jobs/debug/`); no other raw assets |
| Owner decision (D21) | All 13 confirmed as own disposable test files; deletion of exactly these 13 authorized |
| Safeguards | New `delete-confirmed` command (dry run by default, `--expect` count, exact IDs from the reviewed manifest, verified against a fresh listing for ID/type/size, raw+upload only, invalidate, stop on first failure). Verified first on the local fake: size mismatch, missing ID and wrong count each aborted with 0 deletions. |
| Fresh inventory vs reviewed manifest | Identical (13 IDs, types, sizes); dry run verified |
| Deletion | **13/13 `ok`** |
| Verification inventory | **0 raw assets** |
| Not touched | Images (profile photos, category icons), all database records, application configuration; CV uploads remain **disabled** |
| Next | D21b: enable private uploads (`Features:CvUploadsEnabled=true`) is unblocked technically (spike passed) but still needs your approval; recommended after C0 is deployed and verified |

## 3. Tests

| Suite | Result |
|---|---|
| Backend build | Success. 0 errors; only the 6 pre-existing `NU1701` (PdfiumViewer) warnings. |
| `WazzufJobs.Tests` | **58/58 passed** (rev 5: +2 for disabled Google sign-in and email/password login; frontend `login.spec.ts` 6/6, run with `ng test --include`) (rev 4: seeder tests replaced by fail-safe tests: no automatic admin even with old bootstrap settings, admin endpoints fail closed without an administrator, self-registration never grants Admin, no default credentials in the shipped DLLs). Previously 54/54: 41 (rev 1) + 5 (rev 2: admin safety on SQL Server, rate-limit probe safety, endpoint leak sweep) + 8 (rev 3: Google sign-in hardening against a fake tokeninfo endpoint) |
| Mutation checks | S1 reverted: 5 authorization tests + the leak sweep fail. Swagger guard removed: 2 tests fail. Google guards removed: 4 tests fail. All fixes restored. |
| Dependency scans | `dotnet list package --vulnerable --include-transitive`: 0. `npm audit --omit=dev`: 0. Dev-only tooling advisories (test runner, jsdom, CLI) are not shipped; deferred to Phase 02. |
| Frontend production build | Success on Angular 21.2.25 (463 kB initial) |
| SQL assessment script | Validated without errors on a throwaway database built from the project's EF migrations (same schema as production) with synthetic data, with and without a Hangfire schema; database dropped. Made portable (no `STRING_AGG`); needs `sqlcmd -I` or SSMS. |
| Cloudinary tool | Verified end to end against a local fake Cloudinary (`tools/c0/verification`): inventory sent 0 mutating requests and joined correctly; spike passed all 10 checks and touched only its own synthetic assets. Real-account behaviour still needs your run. |
| Release artifacts | Zip SHA-256 `6d6cc460…0a68ea76` matches the 94-file manifest; no config, `.pdb` or test files; runtime, `web.config` and file set identical to pre-C0; no legacy credential strings in the C0 DLLs; frontend bundle has the production API URL, no localhost, and the demo notice; `aiScore` only in the admin chunk. |
| Frontend unit tests | Pre-existing breakage unchanged (does not compile at baseline); planned for Phase 02/04 |
| Script syntax | 3 PowerShell scripts parse OK; IIS snippet is well-formed XML |

## 4. Final C0 release-readiness checklist

### Completed (local)
- [x] Backups of all prior work (git ref + archive); isolated branch; nothing committed or pushed
- [x] S1, S2 (code), S2c, S3 (new uploads, URL removal), S4, S5, S7, S13 fixed; refresh-token revocation; admin Account security page
- [x] CV uploads disabled by default and logged; never-public upload path
- [x] 46 automated security regression tests, including the full-API leak sweep and real-SQL-Server admin tests
- [x] Release artifact built; compatibility verified against the pre-C0 build; checksum and manifest
- [x] Offline / restricted-rollback procedure + IIS deny rules; seeder rollback hazard documented
- [x] Safe rate-limit probe; post-deploy check script; obsolete-file comparison script (read-only)
- [x] Read-only SQL assessment; Cloudinary inventory and spike tool; Vercel probe (unpublished)
- [x] Read-only production checks: Swagger exposed (200), Hangfire 401, HTTPS valid
- [x] Dependency vulnerabilities fixed (Pkcs pin, Angular patch, SignalR client removed); scans clean for shipped dependencies
- [x] Google sign-in hardened (S8) with 8 tests
- [x] SQL assessment validated on a migration-built schema; Cloudinary tool verified against a fake Cloudinary
- [x] Local branch prepared with separated commits (pre-existing work vs C0); not pushed

### Owner actions (before D20)
- [x] ~~G1~~ backup gate **waived** by the owner (accepted risk: data loss would be permanent)
- [x] G2 owner confirmed disposable test data
- [ ] **G3**: rotate the production admin password **now** (it was public in the README) and review all Admin-role accounts
- [ ] **G5**: confirm the production secrets location and `ASPNETCORE_ENVIRONMENT`
- [ ] **G6**: download a copy of the current site files
- [ ] **G7**: confirm the server config does not enable CV uploads
- [x] G8 GitHub ↔ Vercel not connected; manual Vercel CLI deployment (note the current deployment id on deployment day)
- [ ] Enter Cloudinary credentials with user secrets (`configuration-and-secrets.md` §2)
- [ ] **G9**: prepare two test candidate accounts with applications
- [ ] Run the Cloudinary `inventory` (read-only) and `spike` (synthetic) with credentials entered in your own terminal; share summaries only
- [ ] Do **not** change the admin email before deployment (G4)
- [ ] **G11 / D24**: decide the release content. Verified: the 44 earlier uncommitted changes are **not** in production (production API spec has no `POST /api/auth/google`), so this release would ship them for the first time. Options: ship together (recommended; security-relevant paths reviewed and tested) or a C0-only rebuild on `master`.
- [x] G12 Google sign-in not configured on MonsterASP, so it is disabled (backend 503, no frontend button), with tests
- [ ] Push the prepared branch only after the phase is approved (pushing does not deploy)

### Unresolved risks (accepted or tracked)
| Risk | Status |
|---|---|
| ~~Existing public CVs~~ | **Closed 2026-10-30:** all 13 owner test assets deleted and verified (0 raw assets). DB rows still reference them; downloads show "unavailable" until re-upload. |
| Whether public CV URLs were actually reachable (free-tier PDF block) | Unknown until spike/probe |
| Host may sit behind a proxy, making the per-IP rate limit effectively global | Detected by `first-request` telemetry; mitigated by probe timing |
| Server `ASPNETCORE_ENVIRONMENT=Development` would keep Swagger exposed | Detected by post-deploy check 1; fix via host setting |
| Request filtering may be locked on the shared host | Restricted rollback verifies with curl; otherwise stay offline |
| Issued access tokens remain valid ≤ 30 min after a password change | Accepted (refresh tokens revoked) |
| Refresh flow likely always fails (pre-existing) | Phase 03 |
| Admin cannot edit accounts holding the default "User" role (pre-existing; fails safely) | Phase 03 |
| Candidate status emails fail in production (template not published, B3) | Out of C0 scope |
| CV uploads unavailable, so new users cannot apply | Accepted for demo until D21b |
| The release ships the 44 earlier uncommitted changes, which I only reviewed for security-relevant paths (Google sign-in hardened; admin views covered by the leak sweep) | Owner confirmation G11 |
| Earlier uncommitted features go live for the first time with this release (production runs `master`) | Decision D24 |
| No production database backup (G1 waived) | Accepted by owner: unrelated data loss would be permanent |
| Default admin password was publicly readable for ~5 months | G3: rotate and review Admin-role accounts before deployment |
| Dev-only npm advisories (22, tooling) | Phase 02 |

### Approvals required
| ID | Decision |
|---|---|
| **D20** | Final approval to deploy C0 (after gates G3–G9, G11, G12; G1 waived) |
| D20-copy | Demo banner wording |
| D20-cleanup | Later: deletion of reviewed obsolete server files (runbook §6) |
| ~~D21~~ | **Done 2026-10-30:** owner confirmed all 13 assets as own test files and authorized deletion; deleted and verified. |
| D21b | Enable CV uploads after the spike passes |
| D23 | Publish the prepared Vercel preview probe |
| D-push | Push `security/c0-emergency-containment` to GitHub after C0 approval (no deployment is triggered) |
| D-bootstrap | Any use of the admin bootstrap script against a shared or production database |
| **D24** | Release content: ship C0 together with the earlier uncommitted features, or rebuild C0-only on `master` |
| Next phase | Start Phase 00 after C0 is completed |
