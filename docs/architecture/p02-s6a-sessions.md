# Phase 02 S6a: server-side sessions, refresh-token rotation and the session check

Status: **implemented locally on `phase/02-domain-identity` (2026-11-01). Not deployed.**
- Only throwaway local SQL Server databases were used. The development and production databases were not touched.
- Transport-neutral: no proxy, hand-off, cookie or Angular change (S6b).

## 0. Owner decision: interim transport (D-S6A-TRANSPORT, 2026-11-01)
S6a works under an explicit stop condition about the login and refresh contract. It was raised before any code was written. The approved end state never puts the new refresh token in a response body (hand-off plus httpOnly cookie, S6b), and body transport is never selected automatically (D-FAILCLOSED).

**Owner's choice: "Stop after the engine."**
- **Built:** the session engine, rotation and reuse detection, the session check, the session API and the revocation hooks.
- **Left exactly as today:** `POST /api/auth/login`, `/refresh` and `/revoke-refresh-token`, including the legacy plaintext refresh tokens, until S6b decides the transport.
- **Consequences:**
  - **Tasks 1 and 7** (hashed refresh tokens on the live endpoints; refresh after access-token expiry) are implemented and tested in the engine, but **not wired to the live endpoints**. S6b wires them through the cookie.
  - The legacy refresh endpoint is still broken (F-S5-1).
  - Body transport for the new tokens was **not** adopted.

## 1. What S6a adds
| Part | Where |
|---|---|
| `ISessionService`: `StartAsync`, `IssueRefreshTokenAsync`, `RotateAsync`, `IsActiveAsync`, `ListActiveAsync`, `RevokeAsync` | `BLL/Sessions/SessionService.cs` |
| Lifetimes, grace, cache window, revocation reasons | `BLL/Sessions/SessionPolicy.cs` |
| Compare-and-swap consume, session touch, revocation | `DAL ISessionRepository` / `SessionRepository` |
| Session check on every authenticated request | `JwtBearerEvents.OnTokenValidated` (`API/DependencyInjection.cs`) |
| Session API | `MySessionsController` (`/api/v1/me/sessions`) |
| Errors | `Auth.RefreshInvalid` (401), `Auth.RefreshRaced` (409), `Session.NotFound` (404) |
| Clock | `TimeProvider` (tests move it for the grace, expiry and cache boundaries) |

No schema change: `UserSessions` and `RefreshTokens` (with `UX_RefreshTokens_ActivePerSession`) exist since S2a.

## 2. Sessions and access tokens
- **Every sign-in starts a server-side session:** login, Google sign-in, and the legacy refresh's success path.
  - **Recorded:** id, user, created and last seen, `AbsoluteExpiresAt`, `Amr` (`pwd`/`ext`), user agent (≤ 200), and the HMAC of the client address (the S5 hasher; never the address).
  - **Lifetimes (D-SESSION-TTL):**

    | Account | Absolute | Refresh token (sliding) |
    |---|---|---|
    | Candidate, company | 30 days | 14 days |
    | Staff | 24 hours | 8 hours |

- **Access tokens:**
  - new claims `sid` (the session) and `amr`;
  - lifetime **capped at 15 minutes in code**, whatever `Jwt:ExpiryMinutes` says (the committed default is still 30, so `expiresIn` is 900).
- **Session check (P02-5):** every authenticated request (controllers and the SignalR hub) must carry a `sid` that names an **active session of the token's own user**: not revoked, not past its absolute expiry, account not disabled. Otherwise the response is **401**.
  - Tokens without a `sid` are refused (fail closed).
  - Results are reused for at most **30 s**, measured on the session clock. A revocation in this process evicts the entry immediately; one made elsewhere takes effect within 30 s (tested on both sides of the boundary).

## 3. Refresh tokens and rotation (engine; transport in S6b)
- **Tokens:** 32 random bytes, base64url (43 characters). The database stores **only SHA-256(token)**; tested that no column contains the token.
- **One usable token per session:** the filtered unique index refuses a second (tested through `IssueRefreshTokenAsync`). The first token is issued by `IssueRefreshTokenAsync`, which S6b calls at hand-off establishment.
- **`RotateAsync(token)`**, in one transaction:
  1. **Compare-and-swap:** `UPDATE … SET ConsumedAt = now WHERE TokenHash = @h AND ConsumedAt IS NULL AND RevokedAt IS NULL AND ExpiresAt > now` (EF `ExecuteUpdateAsync`). Concurrent callers serialize on the row lock; exactly one updates the row.
  2. **Session touch:** `LastSeenAt` is updated only if the session is unrevoked, unexpired and the account enabled. Otherwise everything rolls back (401, token not consumed).
  3. **Successor insert:** `ParentId` = the consumed token; expiry = min(now + sliding, session absolute). Any database error rolls everything back (401, token not consumed).

  The winner gets the successor and a new access token. **No access token is needed**, so rotation works long after the access token expired (task 7).
- **Losers and other cases:**

  | Presented token | Result | Issued |
  |---|---|---|
  | Unknown, garbage, oversized, expired, revoked, or its session revoked or expired | 401 `Auth.RefreshInvalid` | nothing |
  | Consumed **≤ 10 s** ago | 409 `Auth.RefreshRaced` | **nothing** |
  | Consumed **> 10 s** ago (replay) | Session and all its tokens revoked, cache evicted, audit `auth.refresh.reuse_detected` (Denied); 401 `Auth.RefreshInvalid` | nothing |

- **Revocation is protective:** a replay revocation commits even if its audit entry can't be written (logged; S5 rule for denials).

## 4. Revocation
| Trigger | Sessions ended | Reason | Atomic with |
|---|---|---|---|
| Password change | **all** of the account, including the caller's | `password_changed` | the change and its audit entry (`revokedSessions` detail) |
| Password reset | all | `password_reset` | the reset and its audit entry |
| Account disabled | all | `disabled` | the disable and its audit entry (inside the Super Admin lock when it applies) |
| Roles added or removed | all | `roles_changed` | the update and its audit entry. An edit that keeps the roles keeps the sessions. |
| Refresh-token replay | that session | `refresh_reuse` | — (audited separately) |
| `DELETE /api/v1/me/sessions/current` | the caller's session (sign-out) | `logout` | its audit entry `auth.session.revoked` |
| `DELETE /api/v1/me/sessions/{id}` | that session, if it is the caller's (otherwise 404) | `signed_out_elsewhere` | `auth.session.revoked` |
| `POST /api/v1/me/sessions/revoke-others` | all but the current | `signed_out_elsewhere` | `auth.sessions.revoked_others` (`revokedSessions`) |

Revocation is a compare-and-swap on `RevokedAt IS NULL` (concurrent sign-outs end a session once and audit it once). It also revokes the session's usable refresh tokens and evicts the session-check cache. Staff 2FA reset (S7) will reuse it.

## 5. Session API (`/api/v1/me/sessions`, any signed-in account)
| Endpoint | Response |
|---|---|
| `GET` | The caller's active sessions: `id`, `createdAt`, `lastSeenAt`, `expiresAt`, `method`, `userAgent`, `current`. No address hash or token material. |
| `DELETE current` | 204; the token stops working immediately |
| `DELETE {sessionId}` | 204, or 404 `Session.NotFound` for another user's session or an inactive one |
| `POST revoke-others` | `{ "revoked": n }` |

The authorization matrix has the four endpoints (`-YYYYY`). Matrix requests now use a fresh session each, because these endpoints end the caller's session.

## 6. API contract implications
| Area | Change |
|---|---|
| `login`, `refresh`, `revoke-refresh-token` | **Request and response shapes unchanged** (owner decision §0). Legacy refresh tokens still issued, stored and handled as before. |
| Access token | + `sid`, `amr` claims; `expiresIn` 900 (was 1800 with the committed default) |
| Every authenticated endpoint | A token is refused (401) once its session is revoked or expired or the account disabled (≤ 30 s; immediate in this process). **Previously a disabled user's token worked until it expired** (S4 R-1, S5 noted). |
| Password change | The caller's own session ends too: the SPA's next call gets 401 and it signs out. Intended (architecture §5.1). |
| New | `GET /api/v1/me/sessions`, `DELETE /api/v1/me/sessions/current`, `DELETE /api/v1/me/sessions/{id}`, `POST /api/v1/me/sessions/revoke-others` |

## 7. Tests
**394/394** pass (375 before S6a, +19 new cases).

**Adapted tests:** no assertion was weakened.
- `TestPersonas.As` now creates a real session for each token (storing the user first if a test never did), because tokens without sessions are refused.
- `CompanyAccessTests`: a disabled company account now gets **401** (session check), not 404. This is stricter, and was deferred to S6a in S4 (R-1).
- `SuperAdminGuardTests` and `AuditLogTests` racing Super Admins: the loser is either refused by the guard (400) or already signed out by the winner's disable (401). Exactly one Super Admin remains active and exactly one `user.disabled` entry is written, as before.
- `AuthorizationMatrixTests`: four new rows; a fresh session per request.

| Class | Cases | Covers |
|---|---|---|
| `RotationConcurrencyTests` (SQL Server) | 1 | 20 parallel rotations of one token, 3 rounds: **exactly one winner, 19 × `Auth.RefreshRaced`**, exactly one usable token, nothing inserted for losers, the winner's tokens work |
| `RotationConcurrencyRcsiTests` (SQL Server, `READ_COMMITTED_SNAPSHOT ON`) | 2 | The same under RCSI, plus a check that RCSI is really on |
| `SessionTests` (SQL Server, test clock) | 16 | Sign-in keeps its contract, creates a session, 15-minute token, `amr`. Staff 24 h / 8 h. Unknown session, another user's session and no `sid` → 401. **Hash-only storage**, one usable token per session, rotation chain (`ParentId`). Rotation needs no access token, works 2 days later. Garbage tokens. **Grace boundary:** exactly 10 s → 409 with nothing issued; 10 s + 1 ms → replay → session revoked, successor and access token dead, audit row. **Attacker-first ordering.** Sliding and absolute expiry (no false replay). Disabled account can't rotate (rolled back); direct DB revocation applies after 30 s, not before. **Successor-insert failure rolls back** the consumption. Password change, reset, disable and role change end sessions (an unchanged-role edit doesn't). Session API: list, current flag, fields, other user's session 404, revoke one, revoke others, sign-out, audit entries. **Concurrent sign-outs** end the session once (one audit row); **10 concurrent sign-ins** → 10 sessions. |

**Mutation checks:**
- Read-then-write consumption instead of compare-and-swap → the 20-way rotation test fails.
- Every consumed token treated as a race (no reuse detection) → the replay and attacker-first tests fail.

**Other checks:**
- `has-pending-model-changes`: no changes.
- Vulnerability scan: 0 in all 5 projects.
- Build warnings unchanged (CS8618, NU1701).
- Secret scan: 0 matches.
- No throwaway databases left.

## 8. Findings and remaining issues
| # | Finding / issue | Handling |
|---|---|---|
| F-S6a-1 | The live refresh endpoint is still the broken legacy one (F-S5-1), and legacy plaintext refresh tokens are still issued at login (owner decision §0). Access tokens now last 15 minutes, so a locally running SPA signs users out every 15 minutes until S6b. | S6b wires the engine through the cookie, removes the legacy tokens and the token from the login body, and runs one transport only. Nothing deploys before S6b (S11). |
| F-S6a-2 | The committed `appsettings.json` still says `Jwt:ExpiryMinutes: 30`; the code caps it at 15 | Harmless. The production value is owner configuration (not in the package). Optionally align it at S11. |
| R-1 | The session check costs one indexed query per session per 30 s | Bounded by the cache; MonsterASP runs one worker process |
| R-2 | Rotation and grace are exercised in tests through `ISessionService`; `Retry-After`, cookie clearing and CSRF are transport concerns | S6b |
| R-3 | The legacy `LegacyRefreshTokens` table (T3) remains until S6b removes the legacy transport | S6b |
