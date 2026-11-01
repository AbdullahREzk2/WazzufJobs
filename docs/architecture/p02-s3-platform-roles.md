# Phase 02 S3: platform roles, permission catalog and account types

Status: **implemented locally on `phase/02-domain-identity` (2026-11-01). Not deployed.**
- Only throwaway local databases were used. The development and production databases were not touched.
- No administrator is created automatically, and there are no default credentials.
- Super Admin bootstrap (S8) and mandatory staff TOTP (S7) are **not** part of S3.

## 1. `PermissionCatalog`: the single source of truth
`WazzufJobs.DAL/Authorization/PermissionCatalog.cs` replaces `Seeders/Permissions.cs` and `Seeders/AppRoles.cs` (both deleted). It defines:
- the permission strings;
- the five fixed roles, each with its id, account type, demo rule and permissions;
- the legacy aliases;
- the assignment and resolution rules.

Everything else derives from it:

| Consumer | How |
|---|---|
| Role rows and role claims | `RoleConfiguration` / `RoleClaimConfiguration` seed them with `HasData` from the catalog (Baseline_v2). `RoleSeeder` creates only missing roles at startup and never changes existing ones. |
| Access tokens | `PermissionCatalog.Resolve(accountType, isDemo, storedRoles)` at login, refresh and Google sign-in. Permissions are **no longer read from the database**. |
| Roles API | Reads the catalog |
| SQL scripts | `03` restores missing roles and claims, and `04` verifies they equal the catalog exactly. A drift test checks both scripts against the catalog. |
| Tests | `TestPersonas` builds tokens with `Resolve`, exactly like sign-in. `RoleCatalogTests` holds a **hand-written** specification, so a catalog change must also change the reviewed test. |

## 2. Roles
| Role | Account type | Demo | Legacy alias in token | Permissions |
|---|---|---|---|---|
| `SuperAdmin` | Staff | real only | `Admin` | Every permission except the retired `roles:create/update/delete` (21) |
| `Moderator` | Staff | real only | — | `jobs:read`, `categories:read`, `users:read` |
| `Candidate` | Candidate | real or demo | `User` | The C0 `User` set, unchanged (9) |
| `CompanyUser` | Company | real or demo | — | none: rights come from company membership (S4) |
| `DemoAdmin` | Staff | demo only | — | `jobs:read`, `categories:read` |

**Permission matrix** (35 role claims):

| Permission | SuperAdmin | Moderator | Candidate | CompanyUser | DemoAdmin |
|---|:-:|:-:|:-:|:-:|:-:|
| `jobs:read` | ✓ | ✓ | ✓ | | ✓ |
| `jobs:create` / `jobs:update` / `jobs:delete` | ✓ | | | | |
| `applications:read` / `applications:create` (own) | ✓ | | ✓ | | |
| `applications:update` (applicants, CVs, AI scores) | ✓ | | | | |
| `categories:read` | ✓ | ✓ | ✓ | | ✓ |
| `categories:create` / `update` / `delete` | ✓ | | | | |
| `users:read` | ✓ | ✓ | | | |
| `users:create` / `update` / `delete` | ✓ | | | | |
| `roles:read` | ✓ | | | | |
| `roles:create` / `update` / `delete` (retired) | | | | | |
| `cv:upload` / `cv:delete` (own) | ✓ | | ✓ | | |
| `savedjobs:read` / `create` / `delete` | ✓ | | ✓ | | |

- All 24 existing permission strings are kept unchanged (API contract, and the Angular `permission-catalog.ts`). No new strings were added in S3. Each later stage appends the strings its endpoints need (append-only).
- **Moderator and DemoAdmin** never hold `applications:update` (the C0 gate for applicants, CV download and AI scores), `cv:*`, `applications:read/create`, any user/role/category/job mutation, or `roles:read`.
- **DemoAdmin has no `users:read`** until demo scope exists (S9). Identity tables are not demo-filtered, so `users:read` would show real accounts to holders of the shared, published demo credentials.

## 3. Account-type compatibility (domain rule)
- A role is **compatible** only with its account type.
  - `DemoAdmin` requires a demo account.
  - `SuperAdmin` and `Moderator` require a real (non-demo) account.
- An account holds **at most one** role (`ValidateAssignment`).
- **Fail closed at sign-in:** `Resolve` grants nothing for unknown role names (including a stored `Admin` or `User`) or for incompatible roles. Only compatible catalog roles contribute; the rest are reported in `IgnoredRoles`.
- `04_verify_schema.sql` fails if any account holds more than one role or a mismatching one.
- **No mass assignment:** accounts created by self-registration, Google sign-in or `POST /api/Users` are always real `Candidate` accounts. The request types have no account-type or demo field, and extra JSON fields (`accountType`, `isDemo`, `emailConfirmed`, `isDisabled`) are ignored (tested).

## 4. Tokens and legacy aliases
- Claims are `roles` (the effective roles **plus** legacy aliases), new `acct` (the account type) and `permissions` (from the catalog).
  - Example (Super Admin): `roles = ["SuperAdmin", "Admin"]`.
- **Aliases are display-only and never authorization grants:**
  1. The API authorizes **only** by permission (`[HasPermission]`).
  2. JWT validation sets `RoleClaimType = "wj:no-role-authorization"`, a claim type no token carries. `IsInRole` and `[Authorize(Roles = …)]` therefore never succeed, even for `Admin` or `SuperAdmin` (tested on the validated principal).
  3. A source scan fails if API, BLL or DAL code ever authorizes by role.
  4. Aliases are never accepted as input: `Admin` or `User` in a Users request → 400 `User.InvalidRoles`.
  5. A validly signed token that claims every role but carries candidate permissions gets 403 on every admin endpoint (tested).
- **Legacy Angular guards (unchanged frontend):** `isAdmin()` checks `roles.includes('Admin')`; `isUser()` checks `roles.includes('User')` or an empty list. Tested per persona against real sign-in tokens:

  | Persona | `isAdmin()` | `isUser()` |
  |---|---|---|
  | SuperAdmin | true | false |
  | Candidate | false | true |
  | Moderator, CompanyUser, DemoAdmin | false | false |

  No Angular change was needed for S3.

## 5. Legacy Users API (D-STAFF-ROLES)
| Rule | Response |
|---|---|
| Role name not in the catalog (including `Admin`, `User`) | 400 `User.InvalidRoles` |
| Removing `SuperAdmin` from the **last active Super Admin** | 400 `User.LastAdministrator` (checked first, so the C0 assertion is unchanged) |
| Any change to the account's **staff** roles (grant, revoke or swap), or creating an account with a staff role | **403 `Users.StaffRolesManagedSeparately`** |
| Role not compatible with the account type, or more than one role | 400 `User.RoleAccountTypeMismatch` |
| Unchanged staff role (the admin "Edit user" form sends the current roles) | Accepted: email can still be changed (C0 owner action) |

- **Still possible:** giving or removing `Candidate` on a candidate account (the empty list stays a valid C0 contract), and enabling, disabling or unlocking accounts.
- **Moderator management** arrives in P09.
- `GET /api/Users` keeps its legacy semantics: accounts holding a role other than `Candidate`.

## 6. Roles API: read-only
- `GET /api/Roles` lists the five roles from the catalog.
  - Response fields: `id`, `name`, `isDeleted` (always `false`, kept for compatibility), and the additive fields `description` and `accountType`.
- `GET /api/Roles/{id}` returns a role with its catalog permissions, or 404 `Role.NotFound`.
- `POST /api/Roles`, `PUT /api/Roles/{roleId}` and `PUT /api/Roles/{roleId}/toggle-status`:
  - stay mapped and require `roles:read`;
  - always answer **403 `Roles.Fixed`**;
  - never bind or read the request body;
  - change nothing (tested with escalation payloads such as "Candidate gets every permission").
- The role mutation commands, validators, `RoleRequest` and `RoleRepository` were removed.
- The existing Angular Roles page still shows its edit buttons, which now receive 403. The approved read-only Roles page (P02-FE) ships with the frontend work in S6b/S7.

## 7. Last-active-Super-Admin guard (C0-5, generalized)
- **Who counts:** active (not disabled) accounts that **effectively** hold `SuperAdmin`, meaning Staff and not demo. The database counts them (`CountActiveUsersInRoleAsync(PlatformRole)`). Disabled holders and incompatible holders (e.g. a candidate-type account with a `SuperAdmin` row) do not count.
- **Disabling** (`toggle-status`) a Super Admin runs inside `RunExclusiveForRoleAsync`:
  1. Begin a transaction.
  2. Take an `UPDLOCK, HOLDLOCK` on the `SuperAdmin` row of `AspNetRoles`. Update locks conflict with each other but not with readers, so sign-in and queries are not blocked.
  3. Count the active Super Admins.
  4. Apply the change and commit.

  Concurrent changes therefore run one after another.
- **Demotion** through the Users API is refused anyway (403). The 400 check above keeps the C0 response for the last Super Admin.
- **Finding F-S3-1 (fixed):** the C0 guard counted and then updated without a lock. Two administrators disabling each other at the same time could **both** succeed and leave no active administrator. The new concurrency test fails without the lock (both requests returned 204; verified by temporarily removing it) and passes with it in 8/8 rounds.
- S8 (bootstrap) and P09 (staff management) reuse the same lock.

## 8. Authorization matrix (invariant P02-1)
`AuthorizationMatrixTests` holds one reviewed row per endpoint (53 endpoints) for six personas: anonymous, SuperAdmin, Moderator, Candidate, CompanyUser and DemoAdmin.
- An endpoint without a row, or a row without an endpoint, fails the build.
- Each cell is sent over HTTP: allowed means the request passed authentication and authorization; refused means 401 (anonymous) or 403 (signed in).
- Multipart endpoints are sent with their declared content type. Otherwise routing answers 415 before authorization; nothing runs either way.
- A mutation check (moving one cell) makes the test fail.

**Summary:**
- **Public:** `/health` and the 9 `Auth` endpoints.
- **Any signed-in account (own data):** `Account/*`, `Onboarding/*`, `GET Jobs`, the scoring hub.
- **Categories read:** every role except CompanyUser.
- **Candidate self-service** (apply, my applications, CV, saved jobs): Candidate and SuperAdmin.
- **Applicants, CVs and AI** (`Applications/job/{jobId}`, `/{id}`, `/{id}/status`, `/{id}/cv`): **SuperAdmin only**.
- **Users:** read for SuperAdmin and Moderator; changes for SuperAdmin only.
- **Roles:** SuperAdmin only, read-only.
- **Job and category administration:** SuperAdmin only.

## 9. Schema and scripts
- `Baseline_v2` was regenerated as **`20261101075350_Baseline_v2`**. The only differences are the role data and the removal of the `AspNetRoles.IsDefault` / `IsDeleted` columns. The audit-trigger block was kept.
  - **T5 resolved:** fixed roles, no default or deleted flags.
- `02_baseline_v2.sql` was regenerated with `New-MigrationScript.ps1` (SHA-256 of the working copy `83C151A4…6B83E`). `03`, `04` and `90` now reference the new migration id.
- **`03`** also restores **missing** fixed roles and permission claims (insert-only). It **stops** without changing anything if a role's id and name differ from the catalog, and reports claims that are not in the catalog without touching them.
- **`04`** now requires:
  - the 5 roles with exact ids and names;
  - exactly the 35 catalog claims (missing or extra claims fail);
  - every account to hold at most one role, matching its account type and demo flag.
- **Real tools check:** plain `sqlcmd -b`, no `-I`, throwaway database, dropped afterwards.
  - `02` ran twice (exit 0).
  - `03` dry run: `Platform roles: 5 of 5`, `Role permissions: 35 of 35`. Apply inserted 0.
  - `04` → `VERIFY: PASS`.
  - After deleting `DemoAdmin` and every `users:read` claim, `03` inserted 1 role and 4 permissions, and `04` → PASS again.

## 10. API contract changes (documented, P02-12)
| Endpoint | Before | After |
|---|---|---|
| Token `roles` claim | `["Admin"]` / `["User"]` | `["SuperAdmin","Admin"]` / `["Candidate","User"]`; other roles without alias; + `acct` claim |
| `GET /api/Roles` | Non-default roles (`Admin`) | The 5 fixed roles; + `description`, `accountType` |
| `GET /api/Roles/{id}` | DB permissions | Catalog permissions; + `description`, `accountType` |
| `POST/PUT /api/Roles…` | Create, update, disable | 403 `Roles.Fixed` |
| `POST/PUT /api/Users` | Could grant or revoke `Admin` | Staff roles → 403; mismatching roles → 400; aliases → 400 |
| `GET /api/Users`, user details | Role names `Admin`/`User` | Platform role names |

## 11. Tests
**311/311** pass (206 before S3, +105 cases). All C0 tests are unchanged, except the adaptations listed below.

| Class | Cases | Covers |
|---|---|---|
| `RoleCatalogTests` | 46 | Hand-written role spec; permission strings preserved; retired never granted; Moderator/DemoAdmin exclusions; compatibility matrix; assignment rules; aliases only for SuperAdmin/Candidate; unknown/incompatible roles grant nothing |
| `RoleCatalogSeedTests` (SQL Server) | 2 | Seeded roles and claims equal the catalog; no `IsDefault`/`IsDeleted` columns |
| `RoleSeederTests` | 1 | The in-memory host seeds exactly the fixed roles; no `Admin`/`User` role exists |
| `AuthorizationMatrixTests` | 3 | Matrix completeness; every cell over HTTP; no applicant/CV/role/user-change cell for Moderator or DemoAdmin |
| `PlatformRoleTests` | 39 | Sign-in tokens per persona; Angular guard compatibility; stored aliases or incompatible roles grant nothing; role claims never authorize (403 + `IsInRole` false); source scan; Users API staff-role, alias, mismatch and mass-assignment refusals; Moderator read-only; registration → Candidate; Roles API read-only |
| `SuperAdminGuardTests` (SQL Server) | 4 | Last Super Admin; disabled and incompatible holders don't count; disable/enable with two; **concurrent mutual disable → exactly one remains** |
| `ResetScriptTests` | +9 | `03` role restore and conflict stop; `04` fails on extra/missing claims, legacy or renamed roles, mismatching or multiple roles; `04` passes with matching accounts; catalog drift test |

**Adapted C0 tests** (role names follow the new model; no assertion weakened):
- `AdminAccountSafetyTests`, `PlatformHardeningTests`, `LeakSweepTests`, `InfrastructureTests`: `Admin` → `SuperAdmin`. Administrators are created as Staff accounts. `TestPersonas.Admin` is the SuperAdmin persona.
- `AdminAccountSafetyTests.Role_changes_apply_only_differences_when_another_admin_exists` is replaced by two tests:
  - revoking another Super Admin's role through the Users API is now **refused** (403, role kept), as D-STAFF-ROLES requires;
  - "apply only differences" is tested on the Candidate role.

**Other checks:**
- `has-pending-model-changes`: no changes.
- Vulnerability scan: 0 in all 5 projects.
- Build warnings unchanged (CS8618, NU1701).
- Secret scan: 0 matches.
- No throwaway databases left.

## 12. Findings and remaining risks
| # | Finding / risk | Handling |
|---|---|---|
| F-S3-1 | C0 last-administrator guard was racy (see §7) | **Fixed** (role lock + concurrency test) |
| F-S3-2 | Pre-existing: the legacy admin create/edit user handlers never set first or last name. Mapster's exact name matching doesn't map the request's `firstName`/`lastName` to `FirstName`/`LastName`. | Not a security issue; unchanged in S3 (out of scope). Recommend a fix with the Users UI work. |
| F-S3-3 | `Account/*` and `Onboarding/*` are open to every signed-in account. A Moderator or company account could complete candidate onboarding **for itself**. | Own data only; no cross-account access. Restrict to candidates when company and staff UIs arrive (P04/P09). |
| R-1 | Access tokens issued before a role or status change stay valid until expiry (30 min today) | S6a session check (≤ 30 s) and revocation on role changes |
| R-2 | Staff accounts have no TOTP yet | S7 (mandatory TOTP). No staff account exists before the S8 bootstrap after the reset. |
| R-3 | The Angular Roles page still offers edits (they get 403 `Roles.Fixed`) | Read-only page with the frontend stages |
| R-4 | Baseline_v2 changes again in later stages | `02`/`03`/`04`/`90` and the drift tests move with it; frozen at S10 |
