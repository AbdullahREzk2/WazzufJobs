# Phase 02: domain model, identity & authorization core (architecture)

> Status: **APPROVED for staged local implementation (2026-10-31).** Stages S0–S5 complete (local); S6a and later not started.
> Branch `phase/02-domain-identity` (from `master` `282a7a2`). Decisions: `docs/decisions/p02-decision-log.md`. Invariants: `docs/architecture/security-invariants.md`. VER-08 results: `docs/architecture/ver-08-auth-proxy-probe.md`.
> Production reset (D-RESET), deployment and publication each still require separate approval.

## Context
Production runs C0 + P01: .NET 10, a single-company job board with two roles (`Admin` and `User`) and no company concept.

Several things today block the multi-portal target:
- Jobs belong to a user (`Job.PostedById`).
- CVs are 1:1 per user.
- Refresh tokens are stored in plaintext.
- The refresh flow is broken: `GetRefreshTokenQueryHandler` calls `JwtProvider.validateToken`, which validates the lifetime, so a refresh after the access token expires always fails.
- Staff accounts have no 2FA.
- There is no audit trail.

Phase 02 builds the foundation that P03–P10 depend on:
- the domain schema;
- platform and company roles;
- server-side isolation;
- secure sessions;
- staff TOTP;
- a one-time Super Admin bootstrap;
- the audit log;
- demo isolation.

Today's features keep working throughout. The phase ends with an **approved** reset of the disposable production DB to `Baseline_v2`.

**Owner answers (2026-10-31):**
1. **Minimal Angular changes** are allowed in P02: a 2FA login step, TOTP enrollment, the session/auth service and interceptor, and a read-only Roles page. No redesign.
2. **Synthetic verification data** for post-deploy checks. CV uploads stay disabled.
3. **httpOnly refresh cookie now**: built in P02, gated by the VER-08 probe.

**Non-negotiable C0 guarantees, each kept by its existing test:**
- object-level authorization on applications;
- no storage URL in any response; streamed CVs with safe headers;
- no AI data for candidates;
- no seeded or default admin, and fail-closed when no admin exists;
- last-admin guard (it becomes the last-SuperAdmin guard);
- Swagger and Hangfire unreachable in production;
- rate limit of 20/min per **real client IP** on `/api/Auth/*`;
- Google sign-in disabled when no ClientId is configured;
- CV uploads disabled (503);
- lifecycle log and `/health`;
- the package never contains config files;
- no secrets in the repo.

C0 test assertions are never weakened. Tests adapted for the new model (e.g. "Admin" → SuperAdmin with TOTP) are listed in the release notes.

---

## 1. Schema: `Baseline_v2` (SQL Server, EF Core 10)
Ids:
- Identity keeps `nvarchar(450)` string GUIDs.
- `Jobs`, `Applications`, `JobCategories` and `Resumes` keep `int` ids, so the frontend routes stay unchanged.
- New aggregates use `uniqueidentifier`: `Companies`, `UserSessions`, `AuthChallenges`.

Every mutable aggregate has `RowVersion`. All timestamps are UTC `datetime2`. Enums are stored as `tinyint` with **append-only numbering**.

| Module | Table | Key columns / rules |
|---|---|---|
| Identity | `AspNetUsers` (+) | `FirstName`, `LastName`, `AccountType` (Candidate/Company/Staff), `IsDisabled`, `IsDemo`, `CreatedAt`, `LastLoginAt`, `ProfilePhotoUrl/PublicId`, `TotpLastTimeStep bigint null`. Onboarding fields move to `CandidateProfiles`. |
| | `AspNetRoles` (+ `IsSystem`, `Description`) / `AspNetRoleClaims` | Fixed platform roles and permission claims seeded with `HasData` from the code catalog. A test asserts that the DB matches the catalog. |
| | `UserSessions` | `Id` (GUID, the JWT `sid`), `UserId`, `CreatedAt`, `LastSeenAt`, `AbsoluteExpiresAt`, `RevokedAt`, `RevokedReason`, `Amr` (pwd/mfa), `UserAgent` (≤200), `IpHash` (HMAC), `RowVersion` |
| | `RefreshTokens` (rebuilt) | `Id bigint`, `SessionId` FK, `ParentId` (the token it replaced), `TokenHash binary(32)` (SHA-256, **unique**), `CreatedAt`, `ExpiresAt`, `ConsumedAt`, `RevokedAt`. **Plaintext is never stored.** **Filtered unique index `UX_RefreshTokens_ActivePerSession (SessionId) WHERE ConsumedAt IS NULL AND RevokedAt IS NULL`**: the database itself guarantees **at most one usable refresh token per session** (§5.2). |
| | `AuthChallenges` | `Id`, `UserId`, `Purpose` (TwoFactor/Enroll), `TokenHash`, `ExpiresAt` (5 min), `ConsumedAt`, `Attempts` (max 5) |
| | `UserRecoveryCodes` | `UserId`, `CodeHash`, `UsedAt` (10 codes, single use) |
| Candidates | `CandidateProfiles` (PK = `UserId`) | Headline, location, experience years, career level, `ShowSalary`, `IsProfileComplete`, preferences (JSON columns, from `UserPreference`), `IsDemo` |
| | `Resumes` (replaces `CVs`) | `Id`, `CandidateId`, `FileName`, `StorageKey`, `StorageAccess` (Authenticated/LegacyPublic), `SizeBytes`, `Sha256`, `IsDefault` (filtered unique: one default per candidate), `Status` (Active/Archived), `UploadedAt`, `IsDemo` |
| | `ResumeTexts` (PK = `ResumeId`) | `ExtractedText`, kept off the hot row |
| | `SavedJobs` | PK (`CandidateId`, `JobId`) |
| Companies | `Companies` | `Id`, `Name`, `Slug` (unique), website, industry, size, location, description, `LogoKey`, `VerificationStatus` (Unverified/Pending/Verified/Rejected), `Status` (Active/Suspended), `IsPlatform`, `IsDemo`, `CreatedAt`, `CreatedById` |
| | `CompanyMembers` | PK (`CompanyId`, `UserId`), `Role` (Owner/Admin/Recruiter), `Status` (Active/Removed), `AddedById`, `CreatedAt`. **Filtered unique `UserId WHERE Status=Active`** (D6: one company per account). |
| Jobs | `JobCategories` | Unchanged, plus `IsActive` |
| | `Jobs` | `CompanyId` FK, `CreatedById`. Existing fields kept, including `Skills` JSON (moves to `JobSkills` in P04). `Status` (existing values kept; P04 appends PendingReview/Rejected/Suspended), `PublishedAt`, `ExpiresAt`, `IsDemo` (copied from the company, immutable). Indexes (`Status`, `IsDemo`, `PublishedAt`) and (`CompanyId`, `Status`). |
| Applications | `Applications` | `JobId`, `CandidateId`, `CompanyId` (copied from the job, for scoping), `ResumeId` FK (Restrict), `Status` (existing values kept; P08 appends), `AIScore/AIFeedback/IsAIScored` (kept until P07), `AppliedAt`, `UpdatedAt`, `IsDemo`. **Unique (`JobId`, `CandidateId`) → 409.** |
| | `ApplicationStatusHistory` | `ApplicationId`, `From`, `To`, `ChangedById`, `ChangedAt`, `Reason` |
| Platform | `AuditLogs` | §7. Append-only (trigger). |
| | `PlatformState` | Key/value: `SchemaBaseline=v2`, `SuperAdminBootstrappedAt`, … |
| (Hangfire) | `HangFire.*` | Recreated by Hangfire at startup; verified in the rehearsal |

**Deferred to their phases as additive migrations:**
- P04: `CompanyInvitations`, `CompanyVerificationRequests`, `JobRevisions`, `JobSkills`, `JobScreeningQuestions`.
- P07: `ScreeningTasks`, `AIAssessments`.
- P06: `BackgroundTasks`, `EmailOutbox`, `Notifications`.

**Relationships:**
- User 1–0..1 CandidateProfile; User 1–* Resume; User 0..1 active CompanyMember; Company 1–* CompanyMember.
- Company 1–* Job; Job 1–* Application; Application *–1 Resume.
- Deletes are `Restrict` everywhere except owned rows: `ResumeTexts`, `RefreshTokens` → Session, and Session → User.

**Continuity:** reference data seeds one `IsPlatform` company called "WazzufJobs". Until P04, jobs created through the existing admin UI belong to it.

## 2. Platform roles and permissions
**Code is the single source:** `PermissionCatalog` replaces `DAL/Persistence/Seeders/Permissions.cs` and `AppRoles.cs`.
- Existing permission strings (`jobs:read`, …) are kept for compatibility, and new ones are added (`platform:*`, `companies:*`, `audit:read`, `staff:2fa-reset`).
- `RoleClaimConfiguration` seeds from the catalog. `PermissionAuthorizationPolicyProvider`/`Handler` are reused unchanged.

| Role | AccountType | Scope (P02) |
|---|---|---|
| `SuperAdmin` | Staff | Everything, including users, audit, staff 2FA reset, bootstrap state. TOTP required. |
| `Moderator` | Staff | Verifications, moderation and the user list (read-only). **No CVs or AI.** TOTP required. Not assignable until P09. |
| `Candidate` | Candidate | Today's `User` permissions |
| `CompanyUser` | Company | No platform permissions. Rights come from membership (§3). Registration opens in P04. |
| `DemoAdmin` | Staff (demo) | Moderator-like, **demo scope only**, exempt from TOTP (shared published credentials). No UI until P10. |

**Rules:**
- Roles must match the account type (domain invariant + test).
- Legacy `/api/Users` cannot grant or revoke staff roles: 403 `Users.StaffRolesManagedSeparately`.
- The last active SuperAdmin cannot be demoted or disabled (generalizes `UserRepository.ReplaceUserRolesAsync`).
- `/api/Roles` becomes **read-only**; mutations return 403 `Roles.Fixed`.

## 3. Company membership
**Company permissions in code:**

| | Owner | Admin | Recruiter |
|---|---|---|---|
| Edit profile | ✓ | ✓ | |
| Manage team | ✓ | ✓ (not owners) | |
| Transfer ownership / delete company | ✓ | | |
| Manage jobs | ✓ | ✓ | ✓ |
| View and manage applicants, notes | ✓ | ✓ | ✓ |

The last-owner guard applies. P02 ships only `GET /api/v1/companies/{companyId}` and `.../members`, to exercise the pipeline. Invitations and registration come in P04.

## 4. Server-side authorization and isolation (three layers)
1. **Permission policy:** `[HasPermission]`, the existing mechanism.
2. **Company access:**
   - `ICompanyAccessService.RequireAsync(companyId, CompanyPermission)` is called in **every** company handler.
   - `companyId` comes only from the route, never from the body.
   - Company repositories expose **only `companyId`-scoped methods**, so there is no unscoped `GetById` in the company module.
3. **Resource ownership:**
   - Candidate handlers use only `ICurrentUserService.UserId`.
   - Applications are readable by their candidate (candidate DTO, no AI or internal fields), by members of the owning company, and by staff through admin endpoints.
   - Moderators never get CVs or AI.

**Responses:**
- Missing permission → **403** (C0 tests unchanged).
- Resource outside the caller's tenant or ownership → **404**, so ids can't be enumerated.

The existing C0 admin gate on `/api/Applications/*` stays as SuperAdmin-only until P08 adds the company pipeline.

## 5. Sessions: JWT, refresh-token rotation and the httpOnly cookie

### 5.1 Access token and session check
**Access token:**
- HS256 with the existing key, **15 min** (previously 30).
- Claims: `sub`, `sid`, `acct`, `amr`, `roles` (+ legacy aliases: SuperAdmin → also `Admin`, Candidate → also `User`), `permissions`.
- Returned in the response body and kept **in memory only** by the SPA.

**Session check:**
- `JwtBearerEvents.OnTokenValidated` rejects a token if its session is revoked or past its absolute expiry, or its user is disabled.
- Results are cached for at most 30 s in `IMemoryCache`. Revocations in this process evict the entry immediately; MonsterASP runs one worker process (verified in the rehearsal), so the effect is immediate in practice.

**Revoking all of a user's sessions** happens on any of these:
- the user is disabled;
- roles change;
- the password is changed or reset;
- 2FA is reset;
- refresh-token reuse is detected (that session only).

### 5.2 Rotation: atomic, at most one usable successor
A refresh token is 32 random bytes (base64url). The DB stores only SHA-256(token). Lifetimes are in D-SESSION-TTL.

**Algorithm** (`RotateRefreshTokenCommand`, one DB transaction, READ COMMITTED; also correct under RCSI because `UPDATE` predicates always read the latest committed row and take an exclusive lock):

1. **Compare-and-swap.** The update succeeds only if the token is unused, unrevoked and unexpired:
   ```sql
   UPDATE RefreshTokens SET ConsumedAt = @now
   OUTPUT inserted.Id, inserted.SessionId
   WHERE TokenHash = @hash AND ConsumedAt IS NULL AND RevokedAt IS NULL AND ExpiresAt > @now
   ```
   (EF Core 10 `ExecuteUpdateAsync`, or this SQL via `FromSql`.) Two concurrent requests with the same token serialize on the row lock. **Exactly one** sees 1 row, and the other re-evaluates the predicate after the lock is released and sees 0 rows.
2. **Winner (1 row):**
   - The session update must also succeed, or the transaction rolls back and the result is 401:
     ```sql
     UPDATE UserSessions SET LastSeenAt=@now
     WHERE Id=@sid AND RevokedAt IS NULL AND AbsoluteExpiresAt>@now
     ```
     It also confirms the user is enabled.
   - **INSERT** the successor: `ParentId` = the old token, `ExpiresAt` = min(now + sliding, session absolute expiry).
   - Commit. The response sets the new cookie and returns a new access token.
   - The filtered unique index means a second active token for the session **cannot be inserted**, even through a bug. An insert violation leads to rollback and 401.
3. **Loser (0 rows):** look the token up by hash.

   | State of the presented token | Response | Credentials issued |
   |---|---|---|
   | Unknown, expired, revoked, or its session revoked | **401**, cookie cleared | none |
   | Consumed **≤ 10 s ago** (grace) | **409 `Auth.RefreshRaced`**, `Retry-After: 1`, cookie untouched | **none**: no access token, no refresh token |
   | Consumed **> 10 s ago** (replay) | **Reuse detected**: revoke the session and all its tokens, evict the session cache, audit `auth.refresh.reuse_detected`; **401**, cookie cleared | none |

**Why the grace period cannot create usable tokens and doesn't weaken replay detection:**
- **During the grace period nothing is ever issued.** The 409 only tells a legitimate racer that rotation has already happened.
- In a browser, the winning response's `Set-Cookie` updates the **shared cookie jar**. The racer retries once after about 1 s and automatically presents the *successor*.
- An attacker replaying a stolen token inside the window gets nothing. Outside the window, the replay revokes the whole session.
- If an attacker wins the race with a stolen token, the legitimate client's later presentation of the consumed token (after 10 s, or on its own retry) revokes the session. The attacker's successor and access tokens die with it, within the session-check window.
- **Accepted edge case:** if the winner's response is lost on the network, the browser keeps the consumed token, and the next refresh after 10 s is treated as a replay. The user must sign in again. That is the price of strict detection, and it is rare.

**Legitimate parallelism is avoided at the source (minimal frontend):**
- **Within a tab**, refresh is single-flight: one in-flight refresh `Observable`, shared by all 401s.
- **Across tabs**, refreshes are serialized with the Web Locks API (`navigator.locks.request('wj-refresh', …)`). Tab B runs after tab A and presents the already-rotated cookie, so it never races.
- Responses:
  - **409** → one retry after `Retry-After`;
  - **401** → sign-out;
  - **5xx or network error** → **no sign-out**; the request fails and the next action retries. A slow cold start on MonsterASP or a Vercel 504 must not destroy sessions.

**Other session operations:**
- **Login and 2FA completion:** create the session and a single-use, browser-bound **hand-off**. The first refresh token is issued only when the hand-off is redeemed through the proxy (§5.3), under the same index guarantee.
- **Logout:** revoke the session (CAS on `RevokedAt IS NULL`) and clear the cookie.
- **Session list and "sign out other sessions"** under `/api/v1/me/sessions`.

**Tests (SQL Server, real concurrency):**
- 20 parallel refreshes with one token → exactly one 200 and nineteen 409; exactly one active token in the DB.
- Replay after the grace period → session revoked, audit row written, all tokens of the session unusable, the access token rejected by the session check.
- Attacker-first ordering.
- A direct insert of a second active token violates the index.
- RCSI on and off.
- Grace boundary (fake clock).
- Login and logout concurrency.

### 5.3 Split routing, session hand-off, cookies and CSRF (D-PROXY-IP v2)
**Principle: the API never derives a client IP from a request header,** on any path, under any condition. The evidence and the rejection of amendment A1 are in `docs/architecture/ver-08-auth-proxy-probe.md`.

**Split routing:**

| Path | Endpoints | Client identity for rate limiting |
|---|---|---|
| **Direct** to `wazzufjobs.runasp.net` (existing CORS) | All **credential-accepting and email-sending** endpoints: `login`, `2fa/verify`, `2fa/enroll*`, `Register`, `forget-Password`, `Reset-Password`, `Resend-Confirm-Email`, `Confirm-Email`, `google`, bootstrap | **TCP peer address** (`Connection.RemoteIpAddress`): IPv4 full address, **IPv6 /64 prefix**. Production evidence (lifecycle log): `remoteAddressClass=public`, `xForwardedForPresent=False`, so IIS sees the real client directly. |
| **Proxied** through `https://wazzuf-jobs.vercel.app/api/auth/…` | **Only** `session/prepare`, `session`, `refresh`, `logout` (`POST`/`OPTIONS`) | Not IP-based. These accept only ≥ 256-bit random secrets. Abuse controls are below. |

**Browser-bound session hand-off (no refresh token ever in a body):**
1. **Prepare** (proxied `POST /api/auth/session/prepare`, stateless, no DB write):
   - sets `__Secure-wj_bind=N` (N is 256-bit random; `HttpOnly; Secure; SameSite=Strict; Path=/api/auth; Max-Age=600`, host-only);
   - returns `bindingHash = base64url(SHA-256(N))`.
2. **Authenticate** (direct `login`, or `2fa/verify` for staff) with `bindingHash`:
   - creates the session;
   - returns the access token plus a **hand-off** code (256-bit random, single-use, expires after 60 s). It is stored only as SHA-256, together with `SessionId` and `bindingHash`, in `AuthChallenges` (`Purpose = SessionHandoff`).
3. **Establish** (proxied `POST /api/auth/session` with the hand-off):
   - check SHA-256(cookie N) == the stored `bindingHash` (constant-time);
   - consume the hand-off atomically (CAS on `ConsumedAt IS NULL AND ExpiresAt > now`);
   - in the same transaction, issue the session's **first refresh token** (filtered unique index, §5.2);
   - `Set-Cookie: __Secure-wj_rt`, and clear `__Secure-wj_bind`.
4. **Refresh and logout** (proxied) work as in §5.2, using the cookie.

**Why this is safe:**
- An XSS that steals the hand-off and `bindingHash` can't redeem them in another browser, because N is httpOnly and exists only in the victim's browser.
- A hand-off that is never redeemed leaves a session without a refresh token, which ends when its 15-minute access token expires.
- Non-browser tools (post-deploy checks) use direct login and the access token only.

**Cookies:**
- **Refresh cookie:** `__Secure-wj_rt=<token>; Path=/api/auth; HttpOnly; Secure; SameSite=Strict; Max-Age=<token lifetime>`, with no `Domain` attribute.
- **Binding cookie:** `__Secure-wj_bind`, same attributes, 10 min.
- Both are host-only on `wazzuf-jobs.vercel.app`. `vercel.app` is a public suffix, so other `*.vercel.app` sites are cross-site.
- **Paths are case-sensitive.** The SPA always calls lowercase `/api/auth/*`, the API sets `Path=/api/auth`, and the Vercel route maps it to the API's `/api/Auth/*`.
- **Neither the refresh token nor N ever appears in a response body** (leak-sweep assertion).

**CSRF:**
- Proxied endpoints require all of:
  1. `X-WJ-CSRF: 1`. It is not CORS-safelisted, so a cross-origin request needs a preflight, which reaches the API (VER-08 P5), and the API's CORS policy rejects foreign origins;
  2. an allowlisted `Origin` (`https://wazzuf-jobs.vercel.app`, `http://localhost:4200`) or `Sec-Fetch-Site: same-origin`;
  3. SameSite=Strict cookies.
- Direct credential endpoints keep the existing CORS allowlist and also require `X-WJ-CSRF` (login-CSRF defense).
- Refresh is POST-only, so a cross-site navigation can never trigger it.
- **CSRF protection therefore doesn't rely on SameSite alone.** This matters because real Safari is not yet verified (VER-08 F3).

**Caching:** every `/api/Auth/*` response carries `Cache-Control: no-store`, `Pragma: no-cache` and `Vary: Cookie`.

### 5.4 Vercel authentication proxy (D-PROXY, D-PROXY-IP v2)
Sources: the Vercel docs on rewrites, request headers, `vercel.json` and WAF rate limiting, read 2026-10-31. The behaviour was verified by VER-08.

**`vercel.json` (added before the existing SPA catch-all):**
```json
"routes": [{
  "src": "^/api/auth/(session/prepare|session|refresh|logout)$", "methods": ["POST","OPTIONS"],
  "dest": "https://wazzufjobs.runasp.net/api/Auth/$1",
  "transforms": [
    { "type": "request.headers", "op": "delete", "target": { "key": "x-wj-proxy-secret" } },
    { "type": "request.headers", "op": "set", "target": { "key": "x-wj-proxy-secret" }, "args": "$WJ_PROXY_SECRET", "env": ["WJ_PROXY_SECRET"] }
  ],
  "headers": { "Cache-Control": "no-store", "x-vercel-enable-rewrite-caching": "0" }
}]
```
- **Delete-then-set** guarantees that client-supplied secrets, single or duplicated, are replaced (VER-08 P2).
- **CDN caching is disabled** (VER-08 P6).
- **No Routing Middleware.**
- **`WJ_PROXY_SECRET`** is a Vercel env var marked Sensitive, with separate Production and Preview values. It is never in the bundle (VER-08 P7).
- The route matches **only** the four cookie endpoints. A proxied request to any other path falls through to the SPA, so it is never forwarded.

**API: proxy gate (endpoint filters; no client-IP logic anywhere):**

| Request | Result |
|---|---|
| Cookie endpoint **without** exactly one valid `X-WJ-Proxy-Secret`, which includes direct calls, a missing or wrong secret, two secret headers, an empty value, or the literal `$WJ_PROXY_SECRET` (unset env var) | **403 `Proxy.Required`** |
| Credential endpoint carrying `X-WJ-Proxy-Secret` (any value) **or** `X-Vercel-Id` (it came through some Vercel route, where all users share Vercel egress IPs) | **403 `Proxy.NotAllowedHere`** |
| Any endpoint | `Forwarded`, `X-Forwarded-For`, `X-Real-IP` and `X-Vercel-*-For` are **never read**. ASP.NET `ForwardedHeaders` middleware stays **off** (asserted by a test). |
| `Proxy:SharedSecret` missing in Production | Startup error, and the lifecycle log shows `proxy=unconfigured`. Cookie endpoints return 403; direct login still works but no session can be established, which the S11 check catches. |

- Secret comparison: SHA-256 digests compared with `CryptographicOperations.FixedTimeEquals`. The current and previous secret are accepted during rotation.

**Rate limiting:**
- **Direct credential and email endpoints:** the C0 `ipLimit` (20/min) keyed by the TCP peer, IPv4 full address or **IPv6 /64**. Header forgery has no effect because no header is read. Identity lockout per account plus a per-account limiter on `login` and `2fa/verify` add defense in depth.
- **Proxied cookie endpoints:** no IP partitioning, because every request arrives from Vercel egress IPs.
  - **Per-token limiter:** SHA-256 of the presented cookie or hand-off, 10/min.
  - **Failure-only global budget:** unknown, expired or invalid tokens share one bucket (e.g. 120/min). A garbage flood can't consume the capacity of valid refreshes.
  - The SPA treats a 429 as transient and never signs out.
- **Optional (D-WAF-RATELIMIT, not configured):** a Vercel WAF rate-limit rule on `/api/auth/*`. It is documented, available on Hobby (one rule per project), keyed by the visitor IP at Vercel's edge, fixed window, per-region counters.

**What a leaked proxy secret allows:** calling cookie endpoints directly, which still requires a valid cookie or a hand-off plus the binding cookie, and making credential endpoints refuse the attacker's own requests. **No rate-limit bypass and no IP spoofing.** The secret can be rotated without downtime.

**Known Vercel limitations and their mitigations:**

| Limitation | Mitigation |
|---|---|
| Hobby plan terms (non-commercial) | Already decision D1/D2 (P12) |
| External rewrite timeout (504) combined with MonsterASP cold starts | 10 s measured fine (VER-08 P9). The SPA never signs out on 5xx or 429. P06 keep-alive is optional. |
| Preview and deployment URLs are behind Vercel SSO | Verification uses production domains of throwaway projects only. No protection bypass is created. |
| Vercel egress IPs are not stable, and forwarded IP headers are not a reliable boundary (`X-Vercel-Forwarded-For` proved spoofable) | **No IP is derived from headers** (D-PROXY-IP v2). The shared secret is the only proxy trust anchor, and it gates only high-entropy endpoints. |
| Path matching is case-sensitive | Lowercase paths in the SPA; verified (VER-08 P1) |

### 5.5 VER-08 probe: outcome (S0, 2026-10-31)
- **Result:** cookie transport viable. All mandatory checks passed in Chromium and Firefox: routing, secret injection, CSRF pass-through, caching, confidentiality, and cookie attributes and scoping.
- **Findings:**
  - **F1:** `X-Vercel-Forwarded-For` is client-spoofable. This led to the rejection of amendment A1 and the adoption of **D-PROXY-IP v2** (§5.3–5.4).
  - **F3:** the Playwright WebKit build for Windows doesn't enforce SameSite. **Real Safari is an open compatibility risk**, to be tested with `tools/p02/ver-08-probe/site/selftest.html` before production deployment where possible.
- **If a future re-verification fails:** fail closed. Cookie endpoints return 403, and the owner decides the next step. **Body-based refresh transport is never selected automatically** (D-FAILCLOSED).
- **Prohibited in all cases:**
  - trusting any forwarded IP header;
  - `SameSite=None` without `Partitioned`;
  - relaxing CSRF;
  - running both refresh transports at once.
- Details: `docs/architecture/ver-08-auth-proxy-probe.md`.

## 6. Mandatory TOTP for staff (SuperAdmin, Moderator)
**Implementation:**
- Built on ASP.NET Identity's authenticator provider (RFC 6238, 6 digits, 30 s, ±1 step).
- **Replay protection:** a code is accepted only if its time step is greater than `TotpLastTimeStep`.
- **Authenticator key encrypted at rest:**
  - AES-GCM via a custom `UserStore` override of `Get/SetAuthenticatorKeyAsync`;
  - the key comes from new server config `Security:DataKey` (32 bytes, base64; HKDF subkeys for TOTP and IP/audit hashing);
  - key id prefix for rotation.

**Login flow:**
1. Staff password OK.
2. No session yet. Response `{ requiresTwoFactor, challengeToken }`, or `{ requiresTwoFactorSetup, challengeToken }` if not enrolled.
3. `POST /api/Auth/2fa/verify` with a code or recovery code → session + tokens.

   For enrollment:
   - `POST /api/Auth/2fa/enroll` returns the key + `otpauth://` URI (QR rendered client-side).
   - `POST /api/Auth/2fa/enroll/confirm` with a valid code enables 2FA.
   - **10 recovery codes** are shown once and stored hashed.

**Limits and recovery:**
- Challenges last 5 min, allow 5 attempts, and are single-use.
- Failures count toward Identity lockout (5 attempts) and the rate limiter.
- A SuperAdmin can reset another staff member's 2FA (audited).
- **Break-glass** for the sole SuperAdmin is a documented, reviewed SQL script with dry-run/apply, run by the operator with DB access. It clears 2FA and forces re-enrollment; the password is still required.

**Frontend (minimal):**
- a code step on the login page;
- an enrollment and recovery-codes panel on the existing Admin → Account security page;
- `qrcode` npm (MIT) for the QR.

## 7. Super Admin bootstrap (one-time) and audit log
**Bootstrap:**
- The operator generates a token locally with `tools/ops/new-bootstrap-token.ps1`. It prints the token once to the operator's own console and outputs only its SHA-256 hash.
- **Only the hash** plus `ExpiresAtUtc` (≤ 24 h) go into server config: `Bootstrap:SuperAdmin:TokenSha256` and `Bootstrap:SuperAdmin:ExpiresAtUtc`.
- `POST /api/v1/bootstrap/super-admin` `{ email, password, token }` returns **404 unless** all of these hold:
  - the config is present and not expired;
  - `PlatformState.SuperAdminBootstrappedAt` is null;
  - no SuperAdmin exists.
- Further requirements:
  - matching token (constant-time compare);
  - a confirmed, enabled account whose password is correct (via `CheckPasswordSignInAsync` with lockout);
  - rate limited.
- On success, a serializable transaction:
  - sets the account type to Staff;
  - makes the account SuperAdmin (removing Candidate);
  - sets `SuperAdminBootstrappedAt`;
  - revokes sessions;
  - writes an audit entry.
- The next login forces TOTP enrollment. After that the endpoint returns 404 permanently, and startup warns until the config is removed.
- The operator runs it with `tools/ops/bootstrap-super-admin.ps1`, which prompts for hidden input.
- This **replaces** `tools/c0/sql/02_bootstrap_promote_confirmed_user_to_admin.sql`, which is archived.

**Audit log:**
- `AuditLogs` columns: `Id bigint`, `OccurredAt`, `ActorUserId`, `ActorKind` (User/System/Anonymous), `Action` (e.g. `auth.login.succeeded`, `auth.2fa.failed`, `user.roles.changed`, `platform.bootstrap.completed`, `applications.cv.accessed`), `TargetType/Id`, `CompanyId`, `IsDemo`, `Outcome`, `IpHash` (HMAC), `UserAgent`, `CorrelationId`, and `Details` (JSON allowlist, **no secrets, tokens or PII**).
- **Write path:** `IAuditLog` writes in the same `SaveChanges` as domain changes; auth events are written separately.
- **Volume control:**
  - unknown-email login failures are not audited (the rate limiter covers them);
  - failures for existing accounts and lockouts are audited.
- **Append-only:** a migration-created trigger rejects `UPDATE` and `DELETE`. Retention arrives in P11.

## 8. Demo isolation (flags and behavior only; no demo content in P02)
**Flags:**
- `IsDemo` on users and companies, copied (immutable) to jobs, applications, resumes, profiles and saved jobs.
- An `IDataScope` per request: `Real`, `Demo` or `All` (SuperAdmin with an explicit flag).

**Filtering:**
- **EF Core 10 named query filters** (`"Demo"`, and `"SoftDelete"` where applicable) on the non-Identity tables. Identity tables are filtered explicitly in admin queries, so `UserManager` lookups stay intact.
- Public job board:
  - anonymous users and real users: real jobs, plus demo jobs only when `Demo:Enabled` (false in production until P10);
  - demo users: demo jobs only.

**Business rules:**
- Real candidates can't apply to demo jobs, and demo candidates can't apply to real jobs (404).
- A `DemoAdmin` only ever operates within demo scope.

## 9. Migration and reset strategy
**Squash:**
- The two existing migrations are archived in git history (tag `pre-baseline-v2`).
- The new `Baseline_v2` migration contains the HasData roles and claims plus the audit trigger.
- `Database.Migrate()` never runs at startup.

**Schema guard:**
- `DatabaseHealthCheck` (reused) also verifies that `__EFMigrationsHistory` holds the expected migration id.
- On a mismatch, `/health` reports Unhealthy, the lifecycle log records `schema-mismatch`, and the app fails closed.

**Reviewed scripts in `db/p02/`:**
- All are idempotent and use `SET XACT_ABORT ON`.
- They follow the C0 pattern: dry-run by default and `@Apply = 1` to execute.

| Script | Purpose |
|---|---|
| `01_reset_drop_application_schema.sql` | Aborts unless `DB_NAME()` equals `@ExpectedDatabase` **and** `__EFMigrationsHistory` holds exactly the two known pre-P02 migration ids. The dry run lists objects and row counts. Apply mode, in one transaction: drop FKs, then the **allowlisted** tables (AspNet\*, CVs, Jobs, …, `__EFMigrationsHistory`), then the `HangFire` schema. Unknown objects are left untouched and reported. |
| `02_baseline_v2.sql` | `dotnet ef migrations script --idempotent` |
| `03_reference_data.sql` | Categories (the current local list, without icons) and the platform company (MERGE) |
| `04_verify_schema.sql` | Read-only: tables, migration id, role and claim counts, 0 users |
| `05_attach_synthetic_verification_resumes.sql` | Attaches 2 synthetic resumes to two owner-registered **test** candidates. Dry-run/apply; only for emails passed in and only for Candidate accounts. |
| `90_rollback_to_p01_schema.sql` | Drops the Baseline_v2 objects (guard: expects Baseline_v2) and applies the old migrations' idempotent script, giving an empty P01-compatible DB |

**Effects of the reset (disposable data, accepted):**
- All users are removed, including `admin@wazzuf.com` and the disabled test account.
- Old profile photo and category icon images on Cloudinary become orphans. They are listed by a read-only inventory; cleanup needs separate approval. The CV raw inventory is already empty.

### 9.1 Local rehearsal (exact procedure; stage S10, repeated on the final release-candidate commit)
**Where it runs:**
- local SQL Server `.`;
- Windows `dotnet.exe` and `sqlcmd`;
- throwaway databases only: `WazzufJobs_P02Reh_<yyyyMMddHHmm>` (forward) and `…_Guard` (negative cases).

The dev DB `WazzufJobsDB` and production are **never** touched.

**Configuration:**
- Rehearsal secrets (`Jwt:key`, `Security:DataKey`, `Proxy:SharedSecret`, the bootstrap token) are freshly generated per run.
- They are passed as **process environment variables** to the API process only. They are never written to files or user secrets, and never printed. The bootstrap token is shown only to the operator's console by `new-bootstrap-token.ps1`.

**Email:**
- Uses a pickup-directory sender (`MailSettings:PickupDirectory`) that writes `.eml` files.
- **Startup refuses this setting in Production** (unit test).
- This is the only new non-production code path, and it makes the real confirmation-link flow testable without SMTP.

**Script:** the runbook is `tools/ops/p02-rehearsal/README.md`, driven by `rehearse.ps1` with one switch per step. Every step records its output in `p02-rehearsal-<run>.md` (outside the repo).

| Step | Action | Must show |
|---|---|---|
| R1 Old schema with data | Create `…_P02Reh`. Apply `p01_schema.sql` (idempotent script generated from tag `pre-baseline-v2`). Run `r1_seed_synthetic_old.sql`: synthetic users (dummy hashes), roles, jobs, applications, CVs with fake keys, refresh tokens, and Hangfire jobs (via a short P01 API run). Also create a foreign table `dbo.Zz_Unrelated`. | Row counts > 0 in every app table |
| R2 Guard negatives | Run `01` with a wrong `@ExpectedDatabase`, then against `…_Guard` with an extra migration row, then against an already-reset DB | **All three abort** before any change. Row counts unchanged. |
| R3 Reset | `01` dry-run (output compared with the expected allowlist), then `@Apply=1` | All app tables, `__EFMigrationsHistory` and the `HangFire` schema are gone. **`Zz_Unrelated` is untouched and reported.** |
| R4 Baseline | `02` twice, `03` twice, then `04` | Second runs are no-ops. One migration id (`Baseline_v2`). Role and claim counts equal the catalog. 0 users. The platform company and categories exist. |
| R5 Schema guard | Start the API against `…_Guard` (old schema) | `/health` Unhealthy. Lifecycle shows `schema-mismatch`. Admin endpoints are unreachable. |
| R6 API start | Start the API (x64 build) against `…_P02Reh` with the bootstrap hash and expiry set | `/health` Healthy. Lifecycle shows `.NET 10`, `schema=Baseline_v2`, `bootstrap=armed`. The HangFire schema has been recreated. **No user exists.** Admin endpoints fail closed. |
| R7 Super Admin bootstrap | Register `reh.owner@example.test` and confirm via the `.eml` link. Then `bootstrap-super-admin.ps1` with: a wrong token (404), an unconfirmed second account (404/400), the correct token (204), the correct token again (**404**). | Exactly one SuperAdmin. `PlatformState.SuperAdminBootstrappedAt` set. Audit `platform.bootstrap.completed`. After the bootstrap config is removed and the API restarted: `bootstrap=disabled`. |
| R8 TOTP enrollment | Log in → `requiresTwoFactorSetup` (no tokens) → enroll. `totp.ps1` (local RFC 6238 helper) computes codes from the returned key → confirm → 10 recovery codes shown once. | DB: the authenticator value is **not** the base32 key (encrypted with a `kid` prefix). Recovery codes are hashes. Audit `auth.2fa.enrolled`. |
| R9 Staff authentication | Log in → `requiresTwoFactor` → wrong code ×5 → lockout. Wait for or unlock the lockout → correct code → tokens + cookie. **The same code again is rejected** (replay). One recovery code works once, then is rejected. | Challenges expire after 5 min (fake clock in the automated test; one real check). |
| R10 Candidate sessions | Register and confirm 2 synthetic candidates. Sign in via prepare → direct login → hand-off establishment (no refresh token in any body; cookie set). Refresh via the cookie container rotates. **10 parallel refreshes → 1×200 + 9×409** and one active token in the DB. Replay of the consumed token after 11 s → **session revoked**, audit row, access token rejected within 30 s. Logout works. Password change revokes all sessions. | All assertions pass |
| R11 Proxy gate and rate limit | Cookie endpoints: valid secret (simulated proxy) → accepted; no, wrong, duplicated or literal-`$VAR` secret → 403. Credential endpoints with a secret or `X-Vercel-Id` → 403. Forged `Forwarded`, `X-Forwarded-For`, `X-Real-IP`, `X-Vercel-*` on `login` → ignored (the 21st request from the TCP peer → 429). Garbage-token flood on `refresh` → only the failure budget is exhausted, and a valid refresh still succeeds. | C0 limit preserved per real TCP client |
| R12 SPA end to end | `ng serve` with `proxy.conf.json` (adds the secret locally). Manual checks: candidate login, reload (silent refresh), two tabs refreshing at once (Web Locks; no sign-out), admin login with TOTP, enrollment UI, Roles page read-only. | Owner-style manual checklist |
| R13 Verification data | SuperAdmin creates a job. `05` (dry-run, then apply) attaches synthetic resumes (placeholder keys) to the 2 candidates. Both apply. Run `post-deploy-checks.ps1` (P02 edition) against `localhost`. | **All PASS**, including candidate → other application/CV **403** and upload **503** |
| R14 Audit | Read-only query of `AuditLogs` | The expected actions are present. A secret/PII pattern scan of `Details` finds nothing. `UPDATE`/`DELETE` fail (trigger). |
| R15 x86 | Repeat R6, R7 (on a fresh DB), R9 and R10 with a temporary **self-contained win-x86** build | Lifecycle shows `arch=X86`. All pass. The temporary build is deleted. |
| R16 Rollback | Stop the API. `90` dry-run, then apply. | `__EFMigrationsHistory` holds the 2 old ids. Run the **P01 build** (tag, x64): `/health` Healthy, unknown login 401, admin endpoints fail closed (no admin). |
| R17 Forward again | `01` → `02` → `03` → start P02 → `/health` Healthy → bootstrap works again on the empty DB | The forward path is repeatable after a rollback |
| R18 Cleanup | Drop the rehearsal DBs. Delete the pickup folder and temporary builds. Clear the process environment. | Report with step results, script checksums and the package SHA-256 |

**Automation:** R2–R4, R7, R10, R14 and R16 also exist as `SqlServerFact` tests (`ResetRehearsalTests`) that run in the normal suite. The manual run adds the real process, x86, SPA and script tooling.

**Exit criterion:** every step passes on the exact release-candidate commit and package checksum. **Then** request D-RESET. The production reset is never part of the rehearsal and needs its own explicit approval.

## 10. Backward compatibility with the current Angular app
- All existing `/api/*` routes and DTO shapes are kept. They become thin adapters over the new handlers: `Job.CompanyId` = the platform company; CV ↔ the default `Resume`; onboarding/profile ↔ `CandidateProfiles`.
- New endpoints go under `/api/v1/*` or `/api/Auth/2fa/*`.
- **Contract snapshot test:**
  - the Development OpenAPI document for the legacy paths is compared with the committed `Tests/Contracts/legacy-api.json`;
  - deliberate changes are documented in the snapshot diff: the refresh token is removed from the login body, the login response gains 2FA fields, Roles mutations return 403, and the refresh body is ignored.
- **JWT compatibility:** role aliases keep `role-guard.ts`/`seeker-guard.ts` working, and the permission strings are unchanged.

**Frontend changes (the complete list):**

| File | Change |
|---|---|
| `core/services/auth.service.ts` | Access token in memory. Sign-in flow: `session/prepare` (proxied), then `login` or `2fa/verify` (direct, with `bindingHash`), then `session` (proxied, hand-off). Silent refresh on app start (`provideAppInitializer`). Cookie endpoints use same-origin lowercase `/api/auth/*`. `X-WJ-CSRF` header on all auth calls. No tokens in `localStorage`. |
| `core/interceptors/auth-interceptor.ts` | **Single-flight** refresh within a tab; Web Locks (`wj-refresh`) across tabs; 409 → one retry; sign-out only on 401, never on 5xx or network errors |
| `features/auth/login` | 2FA code step |
| `features/admin/account-security` | Enrollment and recovery codes; sessions list |
| `features/admin/manage-roles` | Read-only |
| `vercel.json`, `proxy.conf.json` | Proxy route for the four cookie endpoints only + secret transform (no middleware); local development proxy |
| `environment*.ts` | `authBaseUrl` |
| specs | `login.spec.ts` extended; interceptor and auth-service specs added |

## 11. Automated tests
**Harness:**
- The C0 factories are generalized into `ApiFactory` (EF InMemory) and `SqlServerApiFactory` (throwaway DBs). `C0WebApplicationFactory` and `SqlServerC0Factory` remain as thin subclasses.
- `TestPersonas` builds anonymous, candidate A/B, company A Owner/Admin/Recruiter, company B Owner, Moderator, SuperAdmin (TOTP via a test clock), DemoAdmin, demo candidate and a demo company.

**Suites:**

| Suite | Asserts |
|---|---|
| C0 (58) | Unchanged guarantees. Adaptations are listed and none weakens an assertion. |
| **Authorization matrix** | Every endpoint from `EndpointDataSource` has an entry in `Tests/authorization-matrix.json` (status per persona). An endpoint without an entry fails the build. |
| Tenant isolation | Company B (and a removed member) never reaches company A resources (404). `ICompanyAccessService` is tested exhaustively. Scoped-repository tests. |
| Demo isolation | Each persona sees only its partition. No cross-partition apply. `IgnoreQueryFilters` appears only in an allowlisted set of places (source scan test). |
| Sessions | The refresh token is hashed in the DB. CAS rotation under 20-way concurrency: exactly one success, the rest 409, one active token. The grace period issues no credentials. Reuse after the grace period → session revoked. Attacker-first ordering. The filtered unique index holds. RCSI on and off. Refresh works after access-token expiry. Revocation on disable, role change, password change and reset. Session check within 30 s. Cookie attributes. CSRF header and Origin required. No refresh token in any body. **Hand-off:** single use under 20-way concurrency; 60 s expiry; wrong or missing binding cookie → 401; another browser's binding fails; never redeemable directly. **Proxy gate:** cookie endpoints need exactly one valid secret (current or previous), otherwise 403; credential endpoints with a secret or `X-Vercel-Id` → 403. **IP:** forged, duplicated or conflicting forwarded headers never change the partition; `ForwardedHeaders` middleware absent; IPv6 same /64 shares a bucket. **Limits:** per-token limiter; garbage flood exhausts only the failure budget; 429 never signs out. |
| TOTP | Staff can't get tokens without TOTP. Enrollment; challenge expiry and attempt limits; replay rejected; recovery codes single-use; lockout; key not stored in plaintext. DemoAdmin exempt; candidates unaffected. |
| Bootstrap | Works once. 404 when unconfigured, expired, already done, or a SuperAdmin exists. Wrong token, unconfirmed or disabled account, wrong password rejected. **Two concurrent calls on SQL Server → exactly one wins.** Audited. |
| Audit | Each privileged action writes exactly one row. No secret or PII patterns in `Details`. `UPDATE`/`DELETE` fail on SQL Server. |
| Schema | `has-pending-model-changes` = none. The idempotent script applies twice cleanly. The reset rehearsal passes. DB role claims equal the catalog. The schema guard fails closed on a mismatch. |
| Contracts and leaks | Legacy OpenAPI snapshot. Leak sweep extended to all personas (no storage URLs, AI fields for candidates, other tenants' data, or refresh tokens). |
| Frontend | Vitest specs for login 2FA, single-flight interceptor, auth initializer; `ng build --configuration production` |

## 12. Stages (each a small commit series on `phase/02-domain-identity`; all tests green at every stage)
| # | Stage | Gate |
|---|---|---|
| S0 | Branch, ADRs (`docs/decisions`), test harness generalization (no behavior change). **VER-08 probe** (§5.5; throwaway Vercel project + httpbin; approval D-VER08). The owner reads the MonsterASP `remoteAddressClass` (result: `public`, no XFF). **Done 2026-10-31:** D-PROXY-IP v2 adopted. | 59 green; probe report |
| S1 | `ValidationBehavior` (validators run today in name only) + consistent ProblemDetails; review every validator for compatibility. **Done 2026-10-31** (`docs/architecture/p02-s1-validation.md`). | Full suite (95/95); contract unchanged for valid requests |
| S2a | Entities, configurations, named filters, `Baseline_v2` migration, schema guard. **Done 2026-10-31** (`docs/architecture/p02-s2a-schema.md`; transitional deviations T1–T6 recorded for S2b, S3, S6a and P04). | Model tests; idempotent script ×2 (164/164) |
| S2b | Port repositories and handlers to the new model behind the legacy adapters. **Done 2026-11-01** (`docs/architecture/p02-s2b-port.md`; T1, T2 and T6 resolved; deployment-script session header). | 177/177; contracts unchanged |
| S2c | `db/p02` scripts 01–04 and 90 + local rehearsal #1. **Done 2026-11-01**: scripts are in `tools/p02/db` (01–04, 90, 91; `docs/architecture/p02-s2c-reset-scripts.md`). | Rehearsal test (206/206) + manual end-to-end rehearsal |
| S3 | Permission catalog, platform roles, account types, legacy aliases, Users/Roles rules, last-SuperAdmin guard. **Done 2026-11-01** (`docs/architecture/p02-s3-platform-roles.md`; T5 resolved; Baseline_v2 regenerated as `20261101075350_Baseline_v2`). | Matrix test (311/311) |
| S4 | Company membership, `ICompanyAccessService`, scoped repositories, two read endpoints. **Done 2026-11-01** (`docs/architecture/p02-s4-company-membership.md`; team-management handlers and last-owner guard implemented, not exposed until P04). | Isolation tests (332/332) |
| S5 | Audit log + trigger + events on existing admin and auth actions. **Done 2026-11-01** (`docs/architecture/p02-s5-audit-log.md`; atomic privileged changes, keyed IP HMAC, `Security:DataKey` required outside Development). | Audit tests (375/375) |
| S6a | Sessions, CAS rotation, reuse detection, session check, logout and session API (transport-neutral) | Concurrency tests (SQL Server) |
| S6b | Split routing, proxy gate (`Proxy.Required` / `Proxy.NotAllowedHere`), browser-bound hand-off (`session/prepare`, `session`), cookies and CSRF, IPv6 /64 partitioning, per-token and failure-budget limiters + frontend auth service, interceptor (single-flight + Web Locks) and `vercel.json` | Proxy-gate, hand-off, header-forgery, rate-limit, cookie and CSRF tests; frontend specs |
| S7 | Staff TOTP (+ encryption, recovery, reset, break-glass script) + frontend login step and enrollment | TOTP tests |
| S8 | Super Admin bootstrap endpoint + tools + docs | Bootstrap tests incl. concurrency |
| S9 | Demo isolation behavior | Demo tests |
| S10 | Release candidate: full suite, frontend build and specs, vulnerability and secret scans, reproducible win-x86 package, **full rehearsal (forward + rollback)**, synthetic-PDF tool command, `post-deploy-checks.ps1` P02 edition, runbook, release notes. **STOP for approval.** | Report |
| S11 | *(approved)* Production deployment + verification | Checks pass |
| S12 | *(approved)* Docs, push and `--no-ff` merge | Remotes verified |

## 13. Deployment, rollback and production verification
**Pre-deployment:**
1. Record the Vercel rollback point. Keep the P01 backend package (rollback).
2. Optional but **recommended**: a MonsterASP DB backup.
3. The owner sets new config **without sharing values**:
   - MonsterASP: `Security:DataKey`, `Proxy:SharedSecret`, `Jwt:ExpiryMinutes=15`, and the bootstrap hash + expiry;
   - Vercel: env var `WJ_PROXY_SECRET`.

**Deployment:**
1. `app_offline.htm`.
2. Run `01` dry-run → review → `@Apply=1`.
3. Run `02`, `03`, `04`.
4. Upload the backend package (extract-and-overwrite).
5. Remove `app_offline`. `/health` must be Healthy (schema guard).
6. `vercel --prod` (frontend + `vercel.json` proxy route). Immediately afterwards, in the browser: prepare (proxied) → login (direct) → session establishment (proxied) → refresh. A cookie endpoint called directly on the API gets 403. A credential endpoint called with `X-Vercel-Id` gets 403.
7. The owner:
   - registers and confirms their account;
   - runs `bootstrap-super-admin.ps1`;
   - logs in, enrolls TOTP and stores the recovery codes;
   - **removes the bootstrap config**.
8. The owner registers two test candidates. The synthetic PDFs are uploaded (Cloudinary approval) and script `05` is applied. The SuperAdmin creates a test job, and both candidates apply.

**Verification (`post-deploy-checks.ps1` P02 edition + manual):**
- `/health`. Lifecycle log: `.NET 10`, `X86`, `schema=Baseline_v2`.
- C0 checks:
  - Swagger and Hangfire 404;
  - Google 503;
  - cross-applicant application, job and CV access 403;
  - no AI fields or storage URLs;
  - upload 503;
  - rate limit 429 at request 21 on direct `login`;
  - **two-network check**: right after network A is limited, a login from network B (for example a phone hotspot) gets 401, not 429. This proves per-client partitions.
  - forged `Forwarded`, `X-Forwarded-For`, `X-Real-IP` and `X-Vercel-*` sent directly to the API don't change the partition (still 429 for network A);
  - real Safari self-test where a device is available (VER-08 F3);
- Session checks:
  - login via the proxy sets the cookie;
  - refresh rotates;
  - parallel refreshes → one 200, the rest 409;
  - **reusing the old token after 10 s revokes the session**;
  - auth responses carry `Cache-Control: no-store` and never `x-vercel-cache: HIT`;
  - logout works.
- 2FA and bootstrap:
  - staff login without a code returns a challenge and no token;
  - the bootstrap endpoint returns 404.
- Audit rows exist (read-only SQL).
- The Cloudinary inventory is read-only and lists the orphaned images.
- Owner UI checks in the candidate and admin portals.

**Rollback:**

| Situation | Action |
|---|---|
| Before step 2 | Remove `app_offline` |
| After the reset | Fix forward (preferred), **or**: `app_offline` → `90_rollback_to_p01_schema.sql` → P01 package → promote the previous Vercel deployment → remove `app_offline`. The DB is empty and fail-closed (no admin); bootstrap with the archived C0 script if needed. |
| Always | **Never restore the pre-C0 build.** It re-seeds `Admin@123456` into an empty DB. |

## 14. Decisions requiring approval
Already decided: minimal Angular changes; synthetic verification data; httpOnly cookie now.

| ID | Decision | Recommendation |
|---|---|---|
| **D-RESET** | Reset the production DB to `Baseline_v2` during the S11 deployment (all users and data removed) | Approve at S10 |
| **D-VER08** | VER-08 probe in a **throwaway Vercel project** (`wj-auth-probe`, preview only, httpbin.org destination, throwaway secret, deleted afterwards); checks P1–P10 (§5.5) | Approve before S6b |
| **D-PROXY** | Only the four cookie endpoints go through a static `vercel.json` route (delete-then-set secret transform, no middleware, caching off) | **Approved** |
| **D-PROXY-IP v2** | **No client IP is ever read from headers.** Split routing: credential endpoints direct, keyed by TCP peer (IPv6 /64); cookie endpoints proxied with a secret gate. Browser-bound single-use hand-off. Amendment A1 **rejected**. | **Approved 2026-10-31** (hand-off subject to full local security and concurrency tests) |
| D-WAF-RATELIMIT | Vercel WAF per-IP rate-limit rule on `/api/auth/*` | Optional. **Not configured in production.** |
| **D-FAILCLOSED** | If a verification fails: fail closed and the owner decides. Body transport is **never** selected automatically; no insecure fallback. | Approved |
| D-CONFIG | New server secrets (`Security:DataKey`, `Proxy:SharedSecret`) + Vercel env var. The owner generates and sets them; values are never shared with Claude. | Approve |
| **D-BOOTSTRAP** | Hashed one-time token + password + confirmed account; 404 when inactive; C0 SQL script archived | Approve |
| **D-2FA** | Mandatory TOTP for SuperAdmin and Moderator; DemoAdmin exempt; replay protection; encrypted key; 10 hashed recovery codes; break-glass SQL | Approve |
| D-SESSION-TTL | Access 15 min. Candidate/company refresh 14 d sliding / 30 d absolute. Staff 8 h / 24 h. **Rotation grace 10 s, during which nothing is issued (409 + retry).** Session-check cache 30 s. | Approve |
| D-IDS | `int` ids for jobs, applications, categories and resumes (compatibility); GUIDs for companies and sessions | Approve |
| D-404 | Cross-tenant or foreign resource → 404; missing permission → 403 | Approve |
| D-PLATFORM-CO | A seeded "WazzufJobs" platform company owns admin-created jobs until P04 | Approve |
| D-STAFF-ROLES | No staff-role assignment through the legacy Users UI; Moderator management in P09 | Approve |
| D-FIXTURE | Upload 2 synthetic PDFs (authenticated, `wazzuf-jobs/verification/`) to Cloudinary + attach via script 05 | Approve at S11 |
| D-AUDIT | Append-only trigger; unknown-email login failures not audited; IPs stored as HMAC | Approve |
| D-VALIDATION | Bring `ValidationBehavior` (carried over from the Phase 00 P01 scope) into P02 (S1) | Approve |
| D-CATEGORIES | Seed the current category list without icons | Approve |
| D-BACKUP | Optional MonsterASP backup before the reset | Recommended |
| D-REHEARSAL-MAIL | Pickup-directory email sender for local rehearsal only (startup refuses it in Production) | Approve |
| D-ORPHANS | After the reset, Cloudinary image orphans are listed read-only; deletion is a separate approval | Approve |

## 15. Verification commands (every stage and release candidate)
```
dotnet.exe build / dotnet.exe test   (InMemory + SqlServer throwaway DBs)
dotnet-ef migrations has-pending-model-changes ; migrations script --idempotent (applied twice)
dotnet list package --vulnerable --include-transitive ; secret scan of diff
cmd.exe npx ng test --watch=false ; npx ng build --configuration production ; npm audit --omit=dev
local end-to-end rehearsal (db/p02 01→04, bootstrap, TOTP, 05, post-deploy-checks against localhost; then 90 rollback)
win-x86 package: reproducible ×2, no config/pdb, runtimeconfig net10.0, checksum
```

**Critical files:**
- `DAL/Entities/*`, `DAL/Persistence/ApplicationDBContext.cs`, `EntitiesConfiguration/*`, `Seeders/*` (→ `PermissionCatalog`), `Migrations/*` (squash);
- `BLL/Authentication/JwtProvider.cs`, `Features/Auth/*` (login, refresh, revoke), `Helpers/GenerateRefreshToken.cs`;
- `API/DependencyInjection.cs` (identity, JWT events, rate limiter, CORS), `API/Program.cs`, `API/Services/DatabaseHealthCheck.cs`, `LifecycleLog.cs`;
- `DAL/Repository/UserRepository.cs` (last-admin guard generalization);
- `Tests/Infrastructure/*`;
- frontend `core/services/auth.service.ts`, `core/interceptors/auth-interceptor.ts`, `features/auth/login`, `features/admin/account-security`, `vercel.json`.

**Constraints for the whole phase:**
- Nothing is deployed, pushed or merged, and no production data or Cloudinary asset is touched, without the stage approvals above.
- Never request or print secrets.

## 16. Remaining architectural risks (after this review)
| # | Risk | Severity | Handling |
|---|---|---|---|
| 1 | Real Safari / iOS cookie behavior unverified (VER-08 F3) | Medium | Test with the self-test page before production deployment where possible. CSRF defenses don't rely on SameSite alone. |
| 2 | MonsterASP front-end behavior changes (it is `public` with no XFF on 2026-10-31, but that is one sample) | Low | The S11 two-network check proves per-client partitions. Re-checked at each deployment. |
| 3 | External rewrite timeout combined with MonsterASP cold starts → 504 on login or refresh | Medium | The SPA never signs out on 5xx. P9 measures it. Optional keep-alive in P06. Premium AlwaysRunning (D1) if needed. |
| 4 | A lost rotation response forces a re-login after the 10 s grace period | Low | Accepted trade-off for strict replay detection; documented |
| 5 | Loss of `Security:DataKey` makes all staff TOTP secrets unreadable | Medium | The owner stores the key in a password manager. Key id prefix allows rotation. The break-glass script re-enrolls. |
| 6 | Leaked `Proxy:SharedSecret` | Low | Only gates ≥ 256-bit-secret endpoints. No IP trust, so no rate-limit bypass. Dual-secret rotation. |
| 7 | Session-cache revocation assumes one worker process (no web garden) | Low | Verified in the rehearsal (R15) and on MonsterASP. Otherwise revocation can take up to 30 s. |
| 8 | Vercel Hobby terms (non-commercial) | Policy | D1/D2 at P12 |
| 9 | Bootstrap depends on production email confirmation (Gmail SMTP) | Low | Verified first thing in the S11 deployment. If mail fails, stop before bootstrapping; no SQL shortcut without approval. |
| 10 | The production reset is irreversible (disposable data, backup optional) | Accepted | D-RESET + D-BACKUP. The rehearsal proves forward, rollback and forward again. |
