# Phase C0: Emergency Containment Report

Status: **implemented, tested locally, release artifacts prepared. NOT deployed.** Production deployment requires final approval (D20).
Revision 3 (2026-10-30): final readiness pass (dependency scans, validated tools, Google sign-in hardening, local branch prepared).

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
| S2 | Hardcoded seeded admin `admin@wazzuf.com` / `Admin@123456` | Code-verified; production password status unknown | Seeder reads `AdminBootstrap:*` from host config; no default; never modifies an existing account. **Rotation is an owner action.** |
| S2b | `wazzuf.com` is a third-party registered domain (NS `dyna-ns.net`, no MX today) | DNS-verified | Owner changes the admin email **after** deployment (runbook §7) |
| S2c | **New:** the admin "Edit user" path deleted all roles before re-adding them, with no transaction and no last-admin protection, so production could be left without an administrator | Code-verified | Roles change by difference only, atomically on SQL Server; the **last active admin cannot lose the Admin role or be disabled** |
| S5 | Hangfire dashboard query-string credentials | Live: 401 (shadowed by an earlier default-protected mapping) | Dashboard mappings and hardcoded filter removed |
| S3 / B15 | Public CV uploads; URLs returned to clients; deletes probably never removed raw files | Code-verified; inventory pending | No public upload path; new uploads `authenticated`-only and **disabled by default**; URLs removed from every DTO; files served via authorized API (60 s signed server-side download); legacy assets untouched until D21 |
| S13 | Swagger publicly served | Live: `/swagger/v1/swagger.json` returns 200 | Swagger mapped only in Development |
| S7 | Rate limiting not configured | Code-verified | 20 req/min per client IP on `/api/Auth/*` |
| S8 | Google sign-in (part of the previously uncommitted work shipped with this release) accepted unverified Google emails and linked to unconfirmed local accounts (pre-hijack) | Code-verified; active only if `Google:ClientId` is set on the server | Requires `email_verified`; refuses to link to unconfirmed accounts (409); audience check kept |
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
- **Hazard found:** the pre-C0 seeder re-creates `admin@wazzuf.com` / `Admin@123456` if that email no longer exists, so the admin email must not change until rollback is no longer expected.

### 2.4 Safe rate-limit verification (item 4)
`verify-rate-limit.ps1` uses random non-existent `@example.invalid` emails. Unknown emails are rejected before any password check, so **no account lockout counter can change**; an automated test proves this. The caller's IP is throttled for ≤ 60 s; instructions cover that, plus the proxy case.

### 2.5 Admin email change (item 5)
Verified on a throwaway **real SQL Server** database (`WazzufJobs_C0Test_*`, created and dropped by the tests; `WazzufJobsDB` untouched; 0 leftovers confirmed):
- email change keeps the Admin role and EmailConfirmed; UserName follows the email; login with the new email works and still grants admin permissions; the old email fails;
- the last active administrator can be neither demoted nor disabled;
- role changes apply only differences, in a transaction.

**Pre-existing limitation found:** the existing role validation rejects the default "User" role, so the admin form cannot save any account that holds it. In practice, admins cannot edit candidates. It fails safely, before any change. If the admin account also holds "User" (SQL result 3), the email change returns "Invalid Roles" without modifying anything. Fix planned for Phase 03.

### 2.6 Backup gate (item 6)
Gate **G1** requires a backup downloaded **and restored successfully** locally, with the assessment script run on the restore, before deployment.

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

## 3. Tests

| Suite | Result |
|---|---|
| Backend build | Success. 0 errors; only the 6 pre-existing `NU1701` (PdfiumViewer) warnings. |
| `WazzufJobs.Tests` | **54/54 passed**: 41 (rev 1) + 5 (rev 2: admin safety on SQL Server, rate-limit probe safety, endpoint leak sweep) + 8 (rev 3: Google sign-in hardening against a fake tokeninfo endpoint) |
| Mutation checks | S1 reverted: 5 authorization tests + the leak sweep fail. Swagger guard removed: 2 tests fail. Google guards removed: 4 tests fail. All fixes restored. |
| Dependency scans | `dotnet list package --vulnerable --include-transitive`: 0. `npm audit --omit=dev`: 0. Dev-only tooling advisories (test runner, jsdom, CLI) are not shipped; deferred to Phase 02. |
| Frontend production build | Success on Angular 21.2.25 (463 kB initial) |
| SQL assessment script | Validated without errors on a throwaway database built from the project's EF migrations (same schema as production) with synthetic data, with and without a Hangfire schema; database dropped. Made portable (no `STRING_AGG`); needs `sqlcmd -I` or SSMS. |
| Cloudinary tool | Verified end to end against a local fake Cloudinary (`tools/c0/verification`): inventory sent 0 mutating requests and joined correctly; spike passed all 10 checks and touched only its own synthetic assets. Real-account behaviour still needs your run. |
| Release artifacts | Zip SHA-256 `9e4d14eb…8d405cfa83b` matches the 94-file manifest; no config, `.pdb` or test files; runtime, `web.config` and file set identical to pre-C0; no legacy credential strings in the C0 DLLs; frontend bundle has the production API URL, no localhost, and the demo notice; `aiScore` only in the admin chunk. |
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
- [ ] **G1**: back up the production DB, **download it, restore it locally**, and run the assessment SQL
- [ ] **G2**: classify the production data (test-only vs real)
- [ ] **G3**: rotate the production admin password now (live Swagger or later the Account security page); review admin login history
- [ ] **G5**: confirm the production secrets location and `ASPNETCORE_ENVIRONMENT`
- [ ] **G6**: download a copy of the current site files
- [ ] **G7**: confirm the server config does not enable CV uploads
- [ ] **G8**: confirm the Vercel deployment method; note the current production deployment
- [ ] **G9**: prepare two test candidate accounts with applications
- [ ] Run the Cloudinary `inventory` (read-only) and `spike` (synthetic) with credentials entered in your own terminal; share summaries only
- [ ] Do **not** change the admin email before deployment (G4)
- [ ] **G11**: confirm the release content (whether the 44 earlier uncommitted changes are already live: look for `POST /api/Auth/google` in production Swagger)
- [ ] **G12**: tell me whether `Google:ClientId` is set on the server
- [ ] Push the prepared branch only after D20 (pushing may trigger a Vercel preview build)

### Unresolved risks (accepted or tracked)
| Risk | Status |
|---|---|
| Existing public CVs remain public until D21 remediation | Highest priority after deployment; depends on the inventory |
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
| Production API comparison not done: this machine lost network access during the session | Owner check G11 |
| Dev-only npm advisories (22, tooling) | Phase 02 |

### Approvals required
| ID | Decision |
|---|---|
| **D20** | Final approval to deploy C0 (after gates G1–G9) |
| D20-copy | Demo banner wording |
| D20-cleanup | Later: deletion of reviewed obsolete server files (runbook §6) |
| **D21** | Per-class action for existing public CV assets, after the inventory; breach assessment outcome |
| D21b | Enable CV uploads after the spike passes |
| D23 | Publish the prepared Vercel preview probe |
| D-push | Push `security/c0-emergency-containment` to GitHub (and open a PR) |
| Next phase | Start Phase 00 after C0 is completed |
