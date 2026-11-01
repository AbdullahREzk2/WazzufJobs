# Phase 02 decision log

Each entry records what was decided, by whom and when, and where the design is specified. "Owner" means the project owner's written approval in the working session. Section references (§) point to `docs/architecture/p02-domain-identity.md`.

**Status values:**
- **Approved**: approved in principle with the architecture on 2026-10-31.
- **Gated**: needs a separate, explicit approval at the named stage.
- **Pending**: waiting for evidence, such as a probe result.

## Inherited (Phase 00, unchanged)
| ID | Decision | Status |
|---|---|---|
| P00-DEMO | Shared demo accounts per role, plus a nightly reset of an isolated demo dataset | Approved (Phase 00) |
| P00-COMPANIES | Company self-registration, then verification by a Super Admin or Moderator before jobs can be submitted | Approved (Phase 00) |
| P00-STAFF | Staff roles are Super Admin and Moderator. A Moderator never sees CVs or AI assessments. | Approved (Phase 00) |
| P00-UI | Angular Material with a custom WazzufJobs theme, delivered in P03 | Approved (Phase 00) |
| P00-NOADMIN | No default or seeded administrator, ever | Approved (C0) |

## Phase 02 owner answers (2026-10-31)
| ID | Decision | Status |
|---|---|---|
| P02-FE | Minimal Angular changes in P02 only: 2FA login step, TOTP enrollment, auth service and interceptor, read-only Roles page. No redesign. | Approved |
| P02-VERIFY | Post-deployment checks use synthetic verification data. CV uploads stay disabled. | Approved |
| P02-COOKIE | Move the refresh token to an httpOnly cookie in P02, gated by VER-08 | Approved. VER-08 (S0): **viable** under D-PROXY-IP v2 (`docs/architecture/ver-08-auth-proxy-probe.md`) |

## Architecture decisions
| ID | Decision | § | Status |
|---|---|---|---|
| D-IDS | `int` ids for jobs, applications, categories and resumes; GUIDs for companies, sessions and challenges | 1 | Approved |
| D-PLATFORM-CO | A seeded "WazzufJobs" platform company (Baseline_v2 seed) owns admin-created jobs until P04. It is looked up by `IsPlatform`, never taken from input; missing → 503, nothing created. | 1 | Approved; **implemented in S2b** |
| D-RESUME-S2B | Until resume versioning (P05) a candidate has one current resume, replaced in place, and admins review the current CV (exact C0 behaviour). Deleting a CV that an application references keeps a `Removed` record without a file. Applications record `ResumeId` for P05. | S2b doc | Implemented in S2b (preserves C0 tests); snapshot semantics deferred to P05 |
| D-SQL-SESSION | Every deployment script starts with `tools/p02/db/session-options.sql` (sets and verifies options; `NOEXEC` on failure) instead of relying on `sqlcmd -I` | S2b doc | Implemented in S2b (owner requirement) |
| D-STAFF-ROLES | Staff roles cannot be assigned through the legacy Users endpoints. Moderator management comes in P09. | 2 | Approved; **implemented in S3** (403 `Users.StaffRolesManagedSeparately`) |
| D-S3-CATALOG | `PermissionCatalog` (code) is the only source of role permissions. Tokens are built from it at sign-in; the database seed and `04` must equal it. Permission strings are append-only; no new strings in S3. | S3 doc | Implemented in S3 |
| D-S3-ALIASES | Legacy aliases (`SuperAdmin` → `Admin`, `Candidate` → `User`) appear only in the token's `roles` claim for the unchanged Angular guards. The API authorizes by permission only; the JWT role claim type is set to a type no token carries, so role checks never succeed. Aliases are rejected as input. | S3 doc | Implemented in S3 |
| D-S3-COMPAT | A role is valid only for its account type (SuperAdmin/Moderator: real Staff; DemoAdmin: demo Staff; Candidate; CompanyUser: Company). At most one role per account. Unknown or incompatible stored roles grant nothing at sign-in (fail closed). | S3 doc | Implemented in S3 |
| D-S3-ROLES-API | Roles are fixed. Role mutation routes stay mapped behind `roles:read` and always return 403 `Roles.Fixed`; `roles:create/update/delete` are kept as strings but granted to no role. | S3 doc | Implemented in S3 |
| D-S3-DEMOADMIN | DemoAdmin gets only public reference-data permissions (`jobs:read`, `categories:read`) until demo scope exists (S9): no `users:read`, because identity tables are not demo-filtered | S3 doc | Implemented in S3; revisit in S9 |
| D-S4-ACCESS | Company access only through `ICompanyAccessService` (active membership, Company account, enabled, same partition, not the platform company). Platform roles, including SuperAdmin, grant no company access; non-members get 404. | S4 doc | Implemented in S4 |
| D-S4-SUSPENDED | Members of a suspended company keep only `ViewCompany` (profile and status); everything else 403 `Company.Suspended` | S4 doc | Implemented in S4 |
| D-S4-TEAM | Team-management rules (owners managed only by owners; last active owner kept, under a company-row lock) implemented as handlers in S4; their endpoints ship with invitations in P04 | S4 doc | Implemented in S4 (not exposed) |
| D-S5-ATOMIC | Privileged changes and their audit entry share one transaction (`IAuditLog.RecordAsync`); the existing lock and role helpers join it. If the entry cannot be written, the change rolls back. | S5 doc | Implemented in S5 |
| D-S5-FAILCLOSED | Successful sign-in and admin CV download require their entry (no tokens or file otherwise). Failure, denial, sign-out and reset-request entries are best-effort and logged, never changing the response (no account-existence signal). | S5 doc | Implemented in S5 |
| D-S5-DETAILS | Audit details pass an explicit key allowlist with plain-token values; IP stored only as HMAC-SHA256 with an HKDF subkey of `Security:DataKey`; `Security:DataKey` required outside Development (startup fails otherwise) | S5 doc | Implemented in S5; owner sets the key at S11 (D-CONFIG) |
| D-S3-LASTADMIN | Changes that can remove an active Super Admin take an update lock on the SuperAdmin role row, then count and change in one transaction (fixes F-S3-1, a race in the C0 guard) | S3 doc | Implemented in S3 |
| D-404 | A resource outside the caller's tenant or ownership returns 404. A missing permission returns 403. | 4 | Approved |
| D-SESSION-TTL | Access token 15 min. Candidate/company refresh 14 d sliding, 30 d absolute. Staff refresh 8 h sliding, 24 h absolute. Rotation grace 10 s with no credentials issued. Session-check cache 30 s. | 5.1–5.2 | Approved |
| D-ROTATION | Compare-and-swap rotation. A filtered unique index allows at most one usable refresh token per session. Reuse after the grace period revokes the session. | 5.2 | Approved |
| D-PROXY | Only the four cookie endpoints (`session/prepare`, `session`, `refresh`, `logout`) go through a static `vercel.json` route (delete-then-set secret transform, caching off, no middleware). Cookie endpoints require the secret, otherwise 403. | 5.4 | Approved; verified by VER-08 |
| ~~A1~~ | Trust `Forwarded` / `X-Vercel-Proxied-For` as the client IP when the proxy secret is valid | 5.4 | **Rejected 2026-10-31.** Forwarded client-IP headers can't be established as a reliable security boundary (undocumented; a related documented header proved spoofable). |
| **D-PROXY-IP v2** | **No client IP is ever read from headers.** **Split routing:** credential and email endpoints direct to MonsterASP, keyed by the TCP peer (IPv6 /64); cookie endpoints proxied behind the secret gate. Credential endpoints reached with a secret or `X-Vercel-Id` → 403. | 5.3–5.4 | **Approved 2026-10-31** |
| D-HANDOFF | Browser-bound, single-use session hand-off (`__Secure-wj_bind` + `bindingHash`, 60 s, CAS consume). The first refresh token is issued only via the proxy. | 5.3 | **Approved**, subject to full local security and concurrency tests (S6b) |
| D-INVARIANTS-P02 | Re-word C0-7 and replace P02-7 without weakening protections | invariants | **Approved 2026-10-31** |
| D-WAF-RATELIMIT | Vercel WAF per-IP rate-limit rule on `/api/auth/*` (documented; Hobby: one rule) | 5.4 | Optional. **Not to be configured in production yet.** |
| R-SAFARI | Real Safari / iOS cookie behaviour unverified (VER-08 F3) | 5.5 | **Open risk.** Test before production deployment where possible. |
| D-FAILCLOSED | If VER-08 fails, the owner decides the transport. Body transport is **not** selected automatically, and none of the prohibited fallbacks is ever used. | 5.5 | Approved |
| D-VER08 | The probe runs in a throwaway Vercel project with a disposable secret and synthetic data only | 5.5 | Approved (S0) |
| D-2FA | Mandatory TOTP for Super Admin and Moderator (Demo Admin exempt). Replay protection, encrypted key, 10 hashed recovery codes, break-glass SQL script. | 6 | Approved |
| D-BOOTSTRAP | One-time Super Admin bootstrap: hashed token plus password plus a confirmed account. The endpoint returns 404 when inactive. The C0 SQL script is archived. | 7 | Approved |
| D-AUDIT | Append-only audit log (trigger). Unknown-email login failures are not audited. IP addresses are stored as HMACs. | 7 | Approved |
| D-VALIDATION | `ValidationBehavior` is carried into P02 as stage S1. Existing validators are made compatible with current clients before enabling them; responses use RFC 9457 `ValidationProblemDetails` with `detail` = the first message. | 12 | Approved; **implemented in S1** (`docs/architecture/p02-s1-validation.md`) |
| D-CATEGORIES | Seed the current category list without icons | 9 | Approved |
| D-REHEARSAL-MAIL | Pickup-directory email sender for the local rehearsal only. Startup refuses it in Production. | 9.1 | Approved |
| D-CONFIG | New server secrets (`Security:DataKey`, `Proxy:SharedSecret`) and the Vercel env var `WJ_PROXY_SECRET`. The owner generates and sets them; the values are never shared. | 5.4, 6 | Approved; owner action at S11 |
| D-ORPHANS | After the reset, Cloudinary image orphans are inventoried read-only. Deleting them is a separate approval. | 9 | Approved |
| **D-RESET** | Reset the production database to `Baseline_v2` | 9, 13 | **Gated**: after the S10 rehearsal report |
| VER-SQL | Verify how SQL will be run on MonsterASP (remote `sqlcmd`/SSMS preferred; the web tool only if it runs multi-batch scripts on one session) | S2c doc | **Prerequisite for D-RESET** (owner) |
| D-PROD-DRYRUN | Read-only dry run of `01` against production to confirm the migration history and inventory before reset day | S2c doc | **Requires separate approval** (read-only) |
| **D-FIXTURE** | Upload 2 synthetic PDFs to Cloudinary for verification data | 13 | **Gated**: at S11 |
| D-BACKUP | Optional MonsterASP backup before the reset | 13 | Recommended; owner's choice at S11 |
| D-DEPLOY / D-PUSH | Production deployment, then pushing and merging | 13 | **Gated**: S11 / S12 |
