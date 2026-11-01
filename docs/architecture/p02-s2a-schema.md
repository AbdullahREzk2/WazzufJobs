# Phase 02 S2a: domain schema, Baseline_v2 migration and schema guard

Status: **implemented locally on `phase/02-domain-identity` (2026-10-31). Not deployed.**
- No production, development or shared database was connected to, migrated or modified.
- All schema work ran on throwaway local databases (`WazzufJobs_*Test_*`, `WazzufJobs_S2aCli_*`), dropped afterwards.

## Approach: additive, transitional model
S2a adds the Phase 02 data model **without breaking any existing handler**:
- every new table is created;
- existing entities only gain columns;
- three columns are renamed through `HasColumnName`, so the C# property names that handlers use are unchanged.

Anything that would need handler changes is a recorded **transitional deviation** (below), resolved in S2b, S3 or S6a.

**Result:** the full C0 suite and all S0/S1 tests pass unchanged. The relational tests now run on databases built by the real `Baseline_v2` migration.

`Baseline_v2` is **regenerated** (re-squashed) as later stages complete the model, and is frozen at S10. Nothing reaches production before the approved reset (D-RESET).

## Tables (26 + `__EFMigrationsHistory`)
| Module | Table | Notes |
|---|---|---|
| Identity | `AspNetUsers` (+ `AccountType` tinyint, `IsDemo`, `LastLoginAt`, `TotpLastTimeStep`) and the other `AspNet*` tables | `AspNetRoles` + `IsSystem`, `Description`. Roles and permission claims unchanged (2 roles, 33 claims). |
| | `UserSessions` | GUID id (`sid`), absolute expiry, revocation, `Amr`, `IpHash binary(32)`, rowversion. Cascade from user (owned). |
| | `RefreshTokens` | `TokenHash binary(32)` **unique**. **`UX_RefreshTokens_ActivePerSession`**: filtered unique on `SessionId` where `ConsumedAt IS NULL AND RevokedAt IS NULL`. `ParentId` self-reference (Restrict). Cascade from session. |
| | `AuthChallenges` | TOTP, enrollment and session hand-off challenges. `TokenHash` unique; `BindingHash binary(32)`. |
| | `UserRecoveryCodes` | Unique (`UserId`, `CodeHash`) |
| Candidates | `CandidateProfiles` (PK `UserId`) | Preferences as EF Core primitive collections (JSON); rowversion |
| | `Resumes`, `ResumeTexts` | `StorageKey` unique, `Sha256 binary(32)`. **`UX_Resumes_DefaultPerCandidate`** (filtered unique). Candidate FK Restrict; text cascades. |
| | `SavedJobs` | Column `UserId` → **`CandidateId`**; + `IsDemo` |
| Companies | `Companies` | `Slug` unique. **`UX_Companies_SinglePlatform`** (filtered unique on `IsPlatform`). Rowversion. |
| | `CompanyMembers` | PK (`CompanyId`, `UserId`). **`UX_CompanyMembers_ActivePerUser`** (filtered unique where `Status = 0`: one active company per account, D6). Restrict FKs. |
| Jobs | `JobCategories` (+ `IsActive`), `Jobs` | Jobs: + `CompanyId` (Restrict), column `PostedById` → **`CreatedById`**, `PublishedAt`, `UpdatedAt`, `IsDemo`, rowversion, indexes (`Status`, `IsDemo`, `PublishedAt`) and (`CompanyId`, `Status`) |
| Applications | `Applications` | Column `UserId` → **`CandidateId`**; unique (`JobId`, `CandidateId`); + `CompanyId`, `ResumeId` (both Restrict), `UpdatedAt`, `IsDemo`, rowversion. Candidate FK changed **Cascade → Restrict**. |
| | `ApplicationStatusHistory` | Cascades with its application |
| Platform | `AuditLogs` | No FKs (survives account changes). **Trigger `TR_AuditLogs_AppendOnly`** rejects `UPDATE`/`DELETE` (error 51000); declared with `HasTrigger`, so EF avoids `OUTPUT`. |
| | `PlatformState` | Seeded `SchemaBaseline = v2` |
| Transitional | `CVs`, `UserPreferences`, `LegacyRefreshTokens` | Kept until handlers are ported (see below) |

New enums (`WazzufJobs.DAL.Enums/Phase02Enums.cs`) are stored as **tinyint**, with append-only numbering.

## Demo isolation (named query filters, not enabled)
- `IDataScope` (DAL) with modes `Real` / `Demo` / `All` and `IncludePublicDemo`. It is registered in the API as `DefaultDataScope`: **real data only, no public demo listings**. Demo functionality stays off until P10.
- `ApplicationDBContext` defines the EF Core 10 named filter **`"Demo"`** on `Companies`, `Jobs` (both also honour `IncludePublicDemo`), and on `CompanyMembers`, `Applications`, `ApplicationStatusHistory`, `Resumes`, `ResumeTexts`, `CandidateProfiles` and `SavedJobs`.
- Dependents follow their principal's partition. This removed EF warning 10622 for `ApplicationStatusHistory` and `ResumeTexts`.
- Identity tables are not filtered, so `UserManager` is unaffected.
- No production code calls `.IgnoreQueryFilters(` (enforced by a source-scan test; allow-list empty).

## Fail-closed schema guard (invariant P02-11)
- **Implementation:** `API/Services/SchemaCompatibility.cs`. The schema is **compatible only if the database's applied migrations equal, in order, the migrations compiled into the build**.
- **When the check runs:** at startup (before seeding), from `/health`, and from a middleware placed after CORS and before the rate limiter and authentication.
- **When the schema is incompatible** (no history, older, newer, or foreign):
  - every endpoint except `/health` returns **503** `application/problem+json` with `code: Schema.Incompatible` (`Database.Unavailable` if unreachable) and no details;
  - `/health` returns **Unhealthy**;
  - **startup seeding is skipped**;
  - the error log records counts only.
- **Recovery:** an unreachable database is re-checked at most every 15 s and recovers without a restart. An incompatible schema stays closed.
- **No automatic migration.** `Database.Migrate()` never runs in the application. Schema changes ship only as reviewed scripts (S2c).
- **Not applicable** to the in-memory test store, so existing tests are unaffected.
- **Lifecycle log:** the `started` line now includes `schema=<status>`.

## Transitional deviations (incompatibilities recorded for later stages)
| # | Deviation from the target model | Why it is still there | Resolved in |
|---|---|---|---|
| T1 | `Jobs.CompanyId`, `Applications.CompanyId`, `Applications.ResumeId` are **nullable** | Existing job and apply handlers don't set them. No implicit default was added: a default tenant FK would silently assign rows to a company. | S2b (handlers set them; columns become required) — **resolved in S2b** (`p02-s2b-port.md`) |
| T2 | `CVs`, `UserPreferences` and the onboarding columns on `AspNetUsers` remain | CV, onboarding and profile handlers use them | S2b (port to `Resumes` / `CandidateProfiles`, then drop) — **resolved in S2b** (`p02-s2b-port.md`) |
| T3 | `LegacyRefreshTokens` (plaintext tokens) remains | Login, refresh and revoke handlers use it | S6a (session-based rotation) |
| T4 | `Applications → Jobs` stays **Cascade** | `DeleteJob` deletes jobs that have applications | P04 (jobs are closed, not deleted) → Restrict |
| T5 | `AspNetRoles.IsDefault/IsDeleted` remain; only Admin/User roles exist | Roles admin endpoints use them | S3 (fixed platform roles) — **resolved in S3** (`p02-s3-platform-roles.md`) |
| T6 | Status and enum columns of legacy tables stay as strings; `CandidateProfiles.CareerLevel` is int | Legacy storage kept to avoid churn | S2b (aligned when ported) — **resolved in S2b** (`p02-s2b-port.md`) |

## Findings
> F-S2a-1 follow-up (S2b): every deployment script now starts with `tools/p02/db/session-options.sql`, which sets and verifies the session options itself (see `p02-s2b-port.md`).

- **F-S2a-1, deployment script settings.** `sqlcmd` runs with `QUOTED_IDENTIFIER OFF` by default, and SQL Server refuses to create filtered indexes under that setting. Running the generated idempotent script through plain `sqlcmd` **fails**. The failure is atomic: the migration transaction rolls back, leaving only the empty history table.
  - With `sqlcmd -I` it applies cleanly.
  - **S2c requirement:** the reviewed `02_baseline_v2.sql` must begin with `SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;`, and the runbook must use `sqlcmd -I`. The MonsterASP SQL tool's settings must also be verified before D-RESET.
  - ADO.NET, SSMS and EF connections are not affected.
- **F-S2a-2, local development database.** The guard correctly refuses the old schema. A local API run against the existing development database (`WazzufJobsDB`, old migration history) now returns 503, by design.
  - Local development needs a database built from `Baseline_v2`. That's the owner's choice, and the development database was not changed.

## Verification
| Check | Result |
|---|---|
| Build | 0 errors; warnings unchanged (NU1701, CS8618 only) |
| Tests | **164/164**: the 95 earlier tests (C0, S0, S1) unchanged, plus 69 S2a test cases |
| `dotnet-ef migrations has-pending-model-changes` | No changes since the last migration |
| Idempotent script (automated, `IMigrator`, ADO.NET) | Applied twice to an empty database; the second run is a no-op; schema fingerprint (columns, types, nullability, indexes and filters, FK delete rules, triggers) **identical** to `Migrate()` |
| Idempotent script (CLI `dotnet ef migrations script --idempotent`, `sqlcmd -I`, twice) | Exit 0 twice. 26 tables + history, 1 history row (`Baseline_v2`), the trigger, 6 filtered unique indexes (4 + Identity's 2), 2 roles, 33 role claims, 8 categories, 0 users. Index checksum unchanged by the second run. |
| Vulnerability scan | 0 in all 5 projects |
| Throwaway databases | None left behind |

**Tests added:**

| File | Coverage |
|---|---|
| `SchemaBaselineTests` | Single baseline history row; all tables present; the 4 filtered unique indexes with exact filters; 6 unique constraints; rowversion on 7 aggregates; 7 fixed `binary(32)` hash columns; 12 FK delete rules; **audit log append-only** (UPDATE/DELETE → 51000, row unchanged); **one usable refresh token per session** (second rejected, rotation allowed); **one active membership per user**; one default resume per candidate; single platform company; platform state seeded |
| `MigrationScriptTests` | Idempotent script twice and equivalent to `Migrate()` |
| `SchemaGuardTests` | No history, unknown newer migration, old schema → `/health` Unhealthy, 4 endpoints 503 with no internals, status Incompatible, **database not migrated**; migrated database → Healthy and served |
| `DemoIsolationFilterTests` | Real, demo and all scopes across all 9 filtered entities; public-demo listing adds only jobs and companies; API default scope is real; no `.IgnoreQueryFilters(` in production code |
| Test infrastructure | `SqlServerApiFactory` now builds databases with `Migrate()` (virtual `PrepareDatabase` for guard cases) |

The old migrations (`initailCreate`, `AddCVExtractedText`) are preserved at local tag **`pre-baseline-v2`** (commit `ad5460d`). S2c uses that tag to generate the rollback script.
