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
| C0-5 | The last active administrator cannot lose the role or be disabled | `AdminAccountSafetyTests` | C0 (generalized to the last SuperAdmin in S3) |
| C0-6 | Swagger and the Hangfire dashboard are unreachable in Production | `PlatformHardeningTests` | C0 |
| C0-7 | `/api/Auth/*` is rate limited per **real client IP** (20/min in production). Unknown-email probes never affect a real account. | `AuthRateLimitTests` | C0 (extended to the proxy path in S6b) |
| C0-8 | Google sign-in is refused with 503 and contacts nobody unless `Google:ClientId` is configured. Only verified Google emails are accepted. | `GoogleLoginDisabledTests`, `GoogleLoginHardeningTests` | C0 |
| C0-9 | CV uploads are refused with 503 while disabled, and nothing is stored. When enabled, uploads are authenticated assets, signature-checked. | `CvProtectionTests`, `CvUploadsEnabledTests` | C0 |
| C0-10 | `/health` is anonymous and reveals no details | `PlatformHardeningTests` | C0 |
| C0-11 | Release packages never contain `appsettings*.json`, `web.config` or `.pdb` files. No secret is committed. | Release checklist (package manifest), secret scan | C0 |

## Introduced by Phase 02
| # | Invariant | Stage |
|---|---|---|
| P02-1 | Every endpoint has an explicit authorization-matrix entry for every persona. A new endpoint without an entry fails the build. | S3 |
| P02-2 | Company data is reachable only through a membership check with the `companyId` taken from the route. A foreign tenant's resource returns 404. | S4 |
| P02-3 | Every privileged action writes exactly one append-only audit row. Audit details never contain secrets, tokens or passwords. | S5 |
| P02-4 | Refresh tokens are stored only as hashes. **At most one usable refresh token exists per session** (DB-enforced). Concurrent rotation yields exactly one winner. Replay after the grace period revokes the session. The grace period never issues credentials. | S6a |
| P02-5 | A revoked session or disabled user is rejected within 30 s, even with an unexpired access token | S6a |
| P02-6 | The refresh token never appears in a response body. The cookie is `HttpOnly; Secure; SameSite=Strict`, host-only and path-scoped. Auth mutations require the CSRF header and an allowed `Origin`. | S6b |
| P02-7 | Forwarded client-IP headers are honored **only** with a valid proxy secret. A forged or unconfigured secret means 403 (fail closed). Without a secret, forwarded headers are ignored. | S6b |
| P02-8 | Staff (Super Admin and Moderator) never receive tokens without a valid TOTP. A TOTP code can't be reused. Recovery codes are single-use and hashed. Authenticator keys are encrypted at rest. | S7 |
| P02-9 | The Super Admin bootstrap succeeds at most once (even under concurrency), returns 404 when inactive, and requires a confirmed account, its password and the one-time token | S8 |
| P02-10 | Demo users never see or alter non-demo data, and real users never see demo data (except demo-badged public jobs when `Demo:Enabled`) | S9 |
| P02-11 | The API refuses to run against an unexpected schema (`/health` Unhealthy, fail closed). `Database.Migrate()` never runs at startup. | S2a |
| P02-12 | The legacy `/api/*` contract changes only as documented in the contract snapshot | S2b |

## Test infrastructure (S0)
- `ApiFactory`: the production pipeline with fakes and the InMemory store. `SqlServerApiFactory`: throwaway SQL Server databases, dropped on dispose and refused for any non-prefixed name.
  - `C0WebApplicationFactory` and `SqlServerC0Factory` are kept as named subclasses, so the C0 suite runs unchanged.
- `TestPersonas` defines the role-based identities used by matrix and leak-sweep tests. `InfrastructureTests` asserts that persona permissions equal the role claims seeded in the database.
