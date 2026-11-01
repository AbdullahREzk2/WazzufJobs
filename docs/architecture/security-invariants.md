# Security invariants

These properties must hold after every stage of every phase.
- Each invariant is enforced by automated tests. A change that requires weakening an assertion is not allowed; it needs a documented owner decision first.
- **Status:** "C0" means enforced since Phase C0 (test exists). "P02" means introduced by Phase 02 (the test is added in the stage named).
- Test classes live in `SrcCode/WazzufJobs/WazzufJobs.Tests`.

## Inherited from Phase C0 (must never regress)
| # | Invariant | Enforced by | Status |
|---|---|---|---|
| C0-1 | A candidate can never read, list or modify another candidate's application, nor list a job's applicants | `ApplicationAuthorizationTests` | C0 |
| C0-2 | No API response ever contains a storage URL. CVs are streamed through the API with `attachment`, `no-store` and `nosniff`. | `CvProtectionTests`, `LeakSweepTests` | C0 |
| C0-3 | Candidates never receive AI assessment data (scores, feedback or flags) | `AiAssessmentExposureTests`, `LeakSweepTests` | C0 |
| C0-4 | No administrator is ever created automatically, and legacy bootstrap settings are ignored. Without an administrator, admin endpoints fail closed. | `PlatformHardeningTests` | C0 |
| C0-5 | The last active administrator cannot lose the role or be disabled | `AdminAccountSafetyTests` | C0. **Generalized in S3:** the last active Super Admin (Staff, real account), checked under a role lock so concurrent changes cannot both succeed (`SuperAdminGuardTests`) |
| C0-6 | Swagger and the Hangfire dashboard are unreachable in Production | `PlatformHardeningTests` | C0 |
| C0-7 | Every endpoint that accepts a guessable credential or sends email (login, 2FA, register, password reset, confirmation resend, Google, bootstrap) is rate limited per **real TCP client address** (20/min in production; IPv4 per address, IPv6 per /64). **No client IP is ever read from a request header.** Proxied cookie endpoints accept only ≥ 256-bit secrets and use per-token and failure-budget limits. Unknown-email probes never affect a real account. *(Re-worded 2026-10-31, D-INVARIANTS-P02; the C0 protection is unchanged and extended to IPv6 /64.)* | `AuthRateLimitTests` (+ S6b forgery, IPv6 and failure-budget tests) | C0 |
| C0-8 | Google sign-in is refused with 503 and contacts nobody unless `Google:ClientId` is configured. Only verified Google emails are accepted. | `GoogleLoginDisabledTests`, `GoogleLoginHardeningTests` | C0 |
| C0-9 | CV uploads are refused with 503 while disabled, and nothing is stored. When enabled, uploads are authenticated assets, signature-checked. | `CvProtectionTests`, `CvUploadsEnabledTests` | C0 |
| C0-10 | `/health` is anonymous and reveals no details | `PlatformHardeningTests` | C0 |
| C0-11 | Release packages never contain `appsettings*.json`, `web.config` or `.pdb` files. No secret is committed. | Release checklist (package manifest), secret scan | C0 |

## Introduced by Phase 02
| # | Invariant | Stage |
|---|---|---|
| P02-1 | Every endpoint has an explicit authorization-matrix entry for every persona. A new endpoint without an entry fails the build. | **S3 (implemented):** `AuthorizationMatrixTests` |
| P02-2 | Company data is reachable only through a membership check with the `companyId` taken from the route. A foreign tenant's resource returns 404. | **S4 (implemented):** `CompanyAccessTests`, `CompanyModuleStructureTests`, `AuthorizationMatrixTests` |
| P02-3 | Every privileged action writes exactly one append-only audit row. Audit details never contain secrets, tokens or passwords. | **S5 (implemented):** `AuditLogTests`, `AuditUnitTests` (database-level append-only trigger since S2a, `SchemaBaselineTests`) |
| P02-4 | Refresh tokens are stored only as hashes. **At most one usable refresh token exists per session** (DB-enforced). Concurrent rotation yields exactly one winner. Replay after the grace period revokes the session. The grace period never issues credentials. | S6a (database-level guarantee implemented in S2a: `UX_RefreshTokens_ActivePerSession`, `SchemaBaselineTests`) |
| P02-5 | A revoked session or disabled user is rejected within 30 s, even with an unexpired access token | S6a |
| P02-6 | The refresh token never appears in a response body. The cookie is `HttpOnly; Secure; SameSite=Strict`, host-only and path-scoped. Auth mutations require the CSRF header and an allowed `Origin`. | S6b |
| P02-7 | **Proxy gate, fail closed:** cookie endpoints require exactly one valid proxy secret, otherwise 403. Credential endpoints reached with a proxy secret or `X-Vercel-Id` → 403. `Forwarded`, `X-Forwarded-For`, `X-Real-IP` and `X-Vercel-*-For` are never read, and `ForwardedHeaders` middleware is never enabled. Session hand-offs are single-use, expire after 60 s, are bound to the browser's binding cookie, and are redeemable only through the proxy. *(Replaced 2026-10-31, D-PROXY-IP v2.)* | S6b |
| P02-8 | Staff (Super Admin and Moderator) never receive tokens without a valid TOTP. A TOTP code can't be reused. Recovery codes are single-use and hashed. Authenticator keys are encrypted at rest. | S7 |
| P02-9 | The Super Admin bootstrap succeeds at most once (even under concurrency), returns 404 when inactive, and requires a confirmed account, its password and the one-time token | S8 |
| P02-10 | Demo users never see or alter non-demo data, and real users never see demo data (except demo-badged public jobs when `Demo:Enabled`) | S9 |
| P02-11 | The API refuses to run against an unexpected schema (`/health` Unhealthy, every other endpoint 503, no seeding; fail closed). `Database.Migrate()` never runs in the application. | **S2a (implemented):** `SchemaGuardTests`, `SchemaBaselineTests` |
| P02-12 | The legacy `/api/*` contract changes only as documented in the contract snapshot | S2b |
| P02-13 | Request validation runs for every request that has validators, **after** authentication, authorization and rate limiting. Failures return RFC 9457 400 with field names and fixed messages only. **Attempted values are never returned or logged.** A validator never rejects input the product, Identity or the schema accepts, except for the documented differences in `docs/architecture/p02-s1-validation.md`. | **S1 (implemented):** `RequestValidationTests`, `ValidationBehaviorTests`, `ValidationCompatibilityTests` |
| P02-14 | **Ownership is enforced by the database.** An application's company always equals its job's company, and its resume always belongs to the applicant (composite foreign keys). A job's owning company cannot change. Jobs are never assigned an owner from client input, and job creation fails closed without the platform company. | **S2b (implemented):** `DomainPortingTests`, `PlatformCompanyMissingTests` |
| P02-15 | Every SQL deployment script sets and verifies its required session options itself (`tools/p02/db/session-options.sql`). On a wrong setting no batch after the header runs. | **S2b (implemented):** `MigrationScriptTests` |
| P02-17 | **Roles never grant by themselves.** Permissions come only from `PermissionCatalog` for roles compatible with the account type; unknown names (including the legacy `Admin`/`User` aliases) and incompatible roles grant nothing. The API never authorizes by role (`IsInRole` cannot succeed). Staff roles are never granted or revoked through the Users API, and roles and permissions cannot be changed through the Roles API. Moderators and Demo Admins never receive CV, application or AI-assessment access. | **S3 (implemented):** `RoleCatalogTests`, `PlatformRoleTests`, `AuthorizationMatrixTests` |
| P02-18 | **Company membership is the only source of company access.** It requires an active membership of an enabled company account in the company's partition; removed members, other tenants, staff, candidates and the platform company get 404. A suspended company's members may only view it. An account has at most one active membership (DB-enforced). The last active owner can be neither demoted nor removed, under a company lock so concurrent changes cannot both succeed. | **S4 (implemented):** `CompanyAccessTests` |
| P02-19 | **No unaudited privilege.** A privileged change commits only together with its audit entry (one transaction); a sign-in or admin CV download fails if its entry cannot be written. Failure and denial entries never change a response, so an audit outage can't weaken a refusal or reveal an account. Details pass an explicit allowlist; client addresses are stored only as an HMAC keyed by `Security:DataKey`, without which the API does not start outside Development. No API reads the audit log. | **S5 (implemented):** `AuditLogTests`, `AuditUnitTests`, `AuditStartupTests` |
| P02-16 | Destructive database scripts change nothing unless the exact database name, the expected migration history and `wj.Apply = 1` are all given, and they default to a dry run. They drop only allowlisted tables, preserve and report everything else, stop on dependent or colliding unknown objects, and apply atomically (any error → no change). | **S2c (implemented):** `ResetScriptTests` |

## Test infrastructure (S0)
- `ApiFactory`: the production pipeline with fakes and the InMemory store. `SqlServerApiFactory`: throwaway SQL Server databases, dropped on dispose and refused for any non-prefixed name.
  - `C0WebApplicationFactory` and `SqlServerC0Factory` are kept as named subclasses, so the C0 suite runs unchanged.
- `TestPersonas` defines the role-based identities used by matrix and leak-sweep tests (S3: SuperAdmin, Moderator, Candidate, CompanyUser, DemoAdmin; tokens built with `PermissionCatalog.Resolve`, exactly like sign-in). `InfrastructureTests` asserts that persona permissions equal the role claims seeded in the database.
