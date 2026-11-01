# Phase 02 S5: audit log

Status: **implemented locally on `phase/02-domain-identity` (2026-11-01). Not deployed.**
- Only throwaway local SQL Server databases were used. The development and production databases were not touched.
- No audit viewing API or UI exists; that arrives with the Super Admin console.
- No schema change: `AuditLogs` and its append-only trigger come from `Baseline_v2` (S2a) and are unchanged.

## 1. Design
| Part | Where |
|---|---|
| `IAuditLog` (`RecordAsync`, `WriteAsync`) and `AuditLogService` | `BLL/Audit/IAuditLog.cs` |
| `AuditEvent`, `AuditTargets` | `BLL/Audit/AuditEvent.cs` |
| Action names (single list) | `BLL/Audit/AuditActions.cs` |
| Details allowlist | `BLL/Audit/AuditDetails.cs` |
| Client-address HMAC | `BLL/Audit/IpHasher.cs`, `SecurityOptions` (`Security:DataKey`) |
| Append-only storage | `IAuditRepository` / `AuditRepository`: append only, no read, update or delete |
| Request facts (TCP peer, user agent, correlation id) | `IRequestInfo` (Shared) / `HttpRequestInfo` (API) |
| Authorization denials | `AuditingAuthorizationResultHandler` (wraps the default `IAuthorizationMiddlewareResultHandler`) |
| Nested-safe transactions | `TransactionExtensions.RunInTransactionAsync` (DAL). The Super Admin lock, the company lock and role replacement now join an enclosing transaction. |

**Every entry has:** `OccurredAt` (UTC), actor (`ActorUserId` and `ActorKind`: User, Anonymous or System), `Action`, `TargetType`/`TargetId`, `CompanyId` where it applies, `IsDemo`, `Outcome` (Success, Failure or Denied), `IpHash`, `UserAgent` (≤ 200 printable characters), `CorrelationId` (the request's trace id) and `Details` (allowlisted JSON).

## 2. Write rules (atomicity and failure behavior)
| Kind | API | Behavior |
|---|---|---|
| Privileged change (user, company, job, category or application administration; registration, email confirmation, password change and reset) | `RecordAsync(change, describe)` | Runs in **one transaction**. On success the entry is written in the same transaction. **If the entry cannot be written, the change rolls back** and the request fails (500): no unaudited change. On failure the change rolls back and its entry (if any) is written separately. A failed change is never recorded as a success (enforced in the service). |
| Successful sign-in, Google sign-in, admin CV download | `WriteAsync` (required) | Written on a separate context **before** any token or file is returned. If it cannot be written, the request fails: no tokens, no CV. |
| Failures and denials (failed sign-in, lockout, refused refresh, authorization and company-access denials, refused user and team changes, role-mutation refusals) | `WriteAsync` (not required) | Written on a separate context, so it survives the request's rollback and never flushes pending changes. If it cannot be written, the error is **logged** and the response is **unchanged**: the refusal stands, and an audit outage can't make existing accounts answer differently from unknown ones. |
| Privilege-reducing or existence-sensitive successes (sign-out, password-reset request) | `WriteAsync` with `Required = false` | Same as failures: never block the sign-out or reveal the account |

## 3. Recorded events
| Area | Action | Outcome | Target / details |
|---|---|---|---|
| Sign-in | `auth.login.succeeded` | Success | User (actor = the user) |
| | `auth.login.failed` | Failure | User; `reason` = `invalid_password`, `email_not_confirmed` or `disabled`. **Existing accounts only:** unknown emails are not audited (D-AUDIT). |
| | `auth.login.locked_out` | Failure | User |
| | `auth.refresh.denied` | Denied | User; `reason` = `disabled` or `locked_out` |
| | `auth.logout` | Success | User |
| | `auth.google.succeeded` | Success | User; `newAccount` |
| Passwords | `auth.password_reset.requested` | Success | User (existing confirmed accounts only) |
| | `auth.password_reset.succeeded` / `.failed` | Success / Failure | User; `revokedTokens` / `reason` |
| | `account.password_changed` / `account.password_change_failed` | Success / Failure | User; `revokedTokens` / `reason` |
| Accounts | `user.registered`, `user.email_confirmed` | Success | User; `roles` |
| User administration | `user.created` | Success | User; `roles` |
| | `user.updated`, or `user.roles.changed` when roles change (one entry per request) | Success | User; `emailChanged`, `rolesAdded`, `rolesRemoved` |
| | `user.disabled`, `user.enabled`, `user.unlocked` | Success | User |
| | `user.change.denied` | Denied | User; `reason` (`Users.StaffRolesManagedSeparately`, `User.LastAdministrator`, `User.RoleAccountTypeMismatch`, `User.InvalidRoles`) |
| | `roles.change.denied` | Denied | Role; `reason` = `Roles.Fixed` |
| Authorization | `authz.denied` | Denied | Endpoint; `method`, `route` (template only, never the path or query), `permissions`. Signed-in callers only; 401s are not audited. |
| Companies | `company.access.denied` | Denied | Company (`CompanyId` = requested id); `permission`, `reason` |
| | `company.member.role_changed` | Success | Member; `from`, `to`; `CompanyId` |
| | `company.member.removed` | Success | Member; `role`; `CompanyId` |
| | `company.member.change_denied` | Denied | Member; `reason` (`OwnersManagedByOwners`, `LastOwner`, `MemberNotFound`); `CompanyId` |
| Platform administration | `job.created/updated/deleted/status_changed`, `category.created/updated/deleted` | Success | Job / Category; `to` for status |
| | `application.status_changed` | Success | Application; `from`, `to`; `CompanyId` |
| | `applications.cv.accessed` | Success | Application; `CompanyId`. No storage key, file name or contents. |

The candidate's own CV upload, download and deletion, applying for a job, profile edits and read-only views are not audited: they are not privileged and only touch the caller's own data.

## 4. Redaction and client addresses
- **Details allowlist.** The only keys are `reason`, `roles`, `rolesAdded`, `rolesRemoved`, `emailChanged`, `from`, `to`, `role`, `permission`, `permissions`, `method`, `route`, `newAccount` and `revokedTokens`.
  - **Allowed values:** booleans, numbers, enums, and short plain tokens (`[A-Za-z0-9_.:-]`, ≤ 64), or lists of ≤ 10 of those.
  - **Only `route`** may contain `/ { }`, as an endpoint template, never `//`.
  - **Anything else is dropped** and counted (`dropped`). Email addresses, sentences, paths, URLs, tokens, passwords, CV text and storage keys are therefore never stored.
- **Client address:** `IpHash` = HMAC-SHA256 of the TCP peer address.
  - The key is derived with HKDF from `Security:DataKey` (purpose `WazzufJobs/audit/ip-hmac/v1`). IPv4-mapped IPv6 addresses are hashed as IPv4.
  - The plaintext address is never stored, and forwarded headers are never read (D-PROXY-IP v2).
- **`Security:DataKey`** (32 random bytes, base64) is an owner-provided server secret (D-CONFIG; `docs/runbooks/configuration-and-secrets.md`).
  - **Outside Development the API refuses to start without a valid key.** Development uses an ephemeral key and logs a warning.
  - **S11 deployment prerequisite:** the owner sets the key in MonsterASP before deploying.

## 5. Append-only
- **Database:** the trigger `TR_AuditLogs_AppendOnly` (S2a) rejects `UPDATE` and `DELETE` with error 51000. It is unchanged, and `04_verify_schema.sql` checks it.
- **Code:** only `AuditRepository` touches `AuditLogs`, and only with `Add` (source-scan test).
- **No read path:** `IAuditRepository` has append methods only, and no endpoint route mentions audit (tests).

## 6. Tests
**375/375** pass (332 before S5, +43 cases). All earlier tests pass unchanged, so no API contract changed. The only test-infrastructure changes are a test-only `Security:DataKey` in `ApiFactory` and a new `ActingUser` helper, which the company tests now use because the handlers take an audit log.

| Class | Cases | Covers |
|---|---|---|
| `AuditUnitTests` | 23 | Allowlist: unknown keys dropped; emails, passwords, JWTs, URLs, storage keys and multi-word values dropped; lists capped; `route` only for templates. Action names unique and plain. Success entries required, failures and denials not. HMAC: 32 bytes, deterministic, keyed (≠ plain SHA-256, ≠ other key), IPv4-mapped = IPv4, no address bytes; 32-byte key enforced. `SecurityOptions` key validation. |
| `AuditLogTests` (SQL Server, fixed TCP peer `203.0.113.7`) | 15 | **Completeness**, exactly one entry per action with actor, target, outcome, IP hash, user agent and correlation id: sign-in success, failure and lockout; unknown email → **no entry and the same response**; reset request; account lifecycle (register, confirm, password change both ways, reset failure, sign-out); every user-administration action and both denials; jobs, categories, application status and CV access with `CompanyId`; authorization denial (template, no query), anonymous → nothing; company access denial; team change and last-owner refusal. **Redaction scan** of every stored column (passwords, tokens, `@`, the plaintext address, storage key, CV text, AI feedback). **Append-only** through EF update, EF delete and `ExecuteDelete` (51000). **Code only appends, nothing reads.** **No audit endpoint.** **Atomicity**, using a test trigger that makes the audit store fail: a disable and a job creation roll back (500, unchanged); no sign-in tokens and no CV released without an entry; denials and failures keep their exact responses (403/404, identical 401 bodies, same reset answer). **Concurrency:** 20 parallel audited changes → 20 entries with distinct correlation ids; racing Super Admins → one `user.disabled` and one `user.change.denied` (5 rounds). |
| `AuditStartupTests` | 5 | Production refuses to start with a missing, empty, malformed or 16-byte key; Development starts without one |

**Mutation checks:**
- With the success entry written outside the transaction (and its failure ignored), the rollback test fails.
- With failure and denial entries required, the outage test fails.

**Other checks:**
- `has-pending-model-changes`: no changes.
- Vulnerability scan: 0 in all 5 projects.
- Build warnings unchanged (CS8618, NU1701).
- Secret scan: 0 matches. The test key is an obvious test value, documented in `ApiFactory`.

## 7. Findings and remaining issues
| # | Finding / issue | Handling |
|---|---|---|
| F-S5-1 | **Pre-existing (P01/C0):** the legacy `POST /api/auth/refresh` and `/revoke-refresh-token` always answer `User.RefreshTokenNotFound`. The handlers search `user.RefreshTokens` on a user loaded without that collection. Legacy refresh tokens can therefore neither be used nor revoked; users sign in again when the access token expires. | Not changed in S5: fixing it would change live sign-in behavior. S6a replaces the flow with sessions. `auth.logout` is tested through the handler. |
| F-S5-2 | **Pre-existing:** sign-in and password-reset responses reveal account state through distinct messages: disabled, unconfirmed and locked accounts, plus `User.NotFound` on reset and `DuplicatedEmail` on registration. Unknown emails also skip the password hash (timing). | API contract unchanged. S5 adds no new signal (failure audits are best-effort; unknown emails are not audited). Review with the S6/S7 sign-in rework. |
| F-S5-3 | A category icon uploaded to Cloudinary is not rolled back if the category's audit entry fails (external side effect before the transaction) | Rare (audit store down); the orphan is harmless and covered by the planned orphan inventory (D-ORPHANS) |
| R-1 | `Security:DataKey` must be set in production before the S11 deployment, or the API won't start (fail closed) | S11 checklist (D-CONFIG) |
| R-2 | Audit volume: authorization and company denials are recorded per request | Retention and alerting in P11. The rate limiter bounds credential endpoints. |
| R-3 | Background jobs record `System` actors; no background job writes audit entries yet | — |
