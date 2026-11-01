# Phase 02 S2b: handlers on the Baseline_v2 model

Status: **implemented locally on `phase/02-domain-identity` (2026-11-01). Not deployed.**
- Only throwaway local databases were used.
- The development and production databases were not connected to or changed.
- No frontend change.

## Resolved transitional deviations (from `p02-s2a-schema.md`)
| # | Deviation | Resolution |
|---|---|---|
| **T1** | Jobs, application company and application resume were optional | **Required.** Two composite foreign keys let the database enforce ownership. `Applications (JobId, CompanyId) → Jobs (Id, CompanyId)`: an application always belongs to its job's company. `Applications (ResumeId, CandidateId) → Resumes (Id, CandidateId)`: an application can only use the applicant's own resume. A job's company is part of the `AK_Jobs_Id_CompanyId` key, so it can't be changed after creation. |
| **T2** | `CVs`, `UserPreferences` and the onboarding columns on `AspNetUsers` remained | **Removed.** CV endpoints use `Resumes`/`ResumeTexts`, and onboarding and profile data use `CandidateProfiles`. The `AppUser` onboarding properties, the `CV` and `UserPreference` entities, and `ICVRepository` are deleted. |
| **T6** | Legacy enum columns were stored as strings | **`tinyint`** for `Jobs.JobType/WorkplaceType/Status`, `Applications.Status`, `ApplicationStatusHistory.FromStatus/ToStatus` and `CandidateProfiles.CareerLevel`. The API still returns names (`"Active"`, `"UnderReview"`, …) because handlers map with `ToString()`. Comparisons in queries are enum-to-enum. |

**Still open:**
- **T3:** `LegacyRefreshTokens` stays until S6a, so login, refresh and revoke are unchanged.
- **T4:** Job → Application cascade stays until P04.
- **T5:** fixed roles arrive in S3.

## How each workflow maps to the new model
| Workflow (route unchanged) | Before | Now |
|---|---|---|
| `POST /api/Jobs` (admin) | Job without owner | Job owned by the **platform company**, looked up by `IsPlatform` (seeded by Baseline_v2). **Never taken from input**: a `companyId` in the body is ignored. If the platform company is missing, the API returns **503 `Job.PlatformCompanyMissing`** and creates nothing (fail closed; no default or guessed owner). Sets `PublishedAt`. |
| `PUT /api/Jobs/{id}`, `PUT …/toggle-status` | — | Also set `UpdatedAt`; `PublishedAt` on first activation |
| `POST /api/Onboarding/complete`, `GET …/status` | `AspNetUsers` columns + `UserPreferences` | `CandidateProfiles` (created on first completion; `IsDemo` copied from the user). A candidate without a profile reports the same defaults as before (incomplete, 0 years). Second completion → 409 as before. |
| `POST /api/cv` | Overwrote the `CVs` row | Replaces the candidate's **current resume in place** (C0 semantics), now with `Sha256`, `SizeBytes` and `StorageAccess`. Deletes the previous **authenticated** file explicitly and keeps legacy public files for approved remediation (unchanged). |
| `DELETE /api/cv` | Deleted the row | Deletes the file exactly as C0. The row is deleted, **unless an application references it**: then it stays as a `Removed` record without a file or text, so the application keeps a valid reference. `GET /api/cv` and `/file` → 404, and the admin CV view is unavailable, as in C0 after deletion. A new upload becomes the current resume next to the removed record. |
| `GET /api/cv`, `/api/cv/file` | `CVs` row | Current resume (default and active) of the **calling user** only |
| `POST /api/Applications/job/{id}/apply` | Required a CV | Requires the applicant's current resume. **Company and demo partition copied from the job; resume = the applicant's own.** Records the first status-history entry (null → Pending, by the applicant). Duplicate → 409, no CV → 400 `Application.CVNotFound` (unchanged). |
| `PUT /api/Applications/{id}/status` (admin) | Status only | Also writes `ApplicationStatusHistory` (from → to, **acting admin**, time) and `UpdatedAt` |
| `GET /api/Applications/{id}`, `/{id}/cv`, `GET /api/Users/{id}` (admin) | `CV`, `Preference`, `ShowSalary` | `HasCv`, CV file, desired salary and show-salary now come from the current resume and the candidate profile. The response shapes are unchanged. |
| AI scoring (background) | Applicant's CV text | Text of the application's resume (the applicant's current resume, as before) |

**Resume semantics until P05.** S2b deliberately keeps **one current resume per candidate, replaced in place**, and admins review the applicant's current CV. This is exactly C0's behaviour, and the C0 test `Replacing_an_authenticated_cv_deletes_the_previous_raw_asset_explicitly` relies on it.
- Each application now records its `ResumeId`, so P05 can introduce versions and per-application snapshots as a deliberate, separately tested change.

## Defect fixed along the way
`ICurrentUserService` (with `CurrentUserService`) existed but was **never registered** in DI. It was unused until now. Design-time DI validation reported it when the status-history handler started using it. It is now registered (`AddHttpContextAccessor` + scoped `ICurrentUserService`).

## Deployment script session options (follow-up to finding F-S2a-1)
`tools/p02/db/`:
- **`session-options.sql`** is the required header of every SQL deployment script:
  - it sets `ANSI_NULLS`, `ANSI_PADDING`, `ANSI_WARNINGS`, `ARITHABORT`, `CONCAT_NULL_YIELDS_NULL`, `QUOTED_IDENTIFIER` and `XACT_ABORT` to ON, and `NUMERIC_ROUNDABORT` to OFF;
  - it then verifies them, and on failure raises an error and sets **`NOEXEC ON`**, so no later batch runs, even in tools that continue after errors.
- **`New-MigrationScript.ps1`** generates the idempotent migration script with that header and prints its SHA-256. It writes a file only and never connects to a database. S2c will use it for `02_baseline_v2.sql`, and S2c's hand-written scripts include the header verbatim.
  - Fixed during verification: in Windows PowerShell 5.1, `dotnet`'s build warnings on stderr were terminating the script. Success is now judged by the exit code.

No reset or reference-data script was written. That is S2c.

## Verification
| Check | Result |
|---|---|
| Tests | **177/177**. All C0 tests pass; their fixtures moved from `CV` to `Resume` with identical meaning (a legacy CV is now expressed by `StorageAccess = LegacyPublic` instead of an `/raw/upload/` URL). |
| New S2b tests (`DomainPortingTests`, `PlatformCompanyMissingTests`) | Platform-owned jobs and an ignored client `companyId`; tinyint storage with name responses; onboarding into `CandidateProfiles`, defaults without a profile, 409 on repeat; apply records company, resume and history; no CV → 400; duplicate → 409; status history with the acting admin; in-place CV replacement deletes the previous authenticated file; delete of a referenced CV leaves a removed record, 404s and an unavailable admin view, and re-upload works; deleting an unused CV deletes the rows; each candidate sees only their own CV; **the database rejects another candidate's resume and a foreign company, and a job's company cannot change**; missing platform company → 503 and nothing stored |
| Script header tests (`MigrationScriptTests`) | Succeeds in a session started with `QUOTED_IDENTIFIER OFF` and `ANSI_NULLS OFF`. The same script without the header fails with error 1934. Verification failure stops later batches. |
| Generated script, real tools | `New-MigrationScript.ps1` output applied **twice with plain `sqlcmd -b` (no `-I`)**: exit 0 both times. 24 tables + history, 1 history row, no `CVs`/`UserPreferences`, platform company seeded, `AK_Jobs_Id_CompanyId` + `AK_Resumes_Id_CandidateId`, 6 filtered unique indexes, the audit trigger, 7 tinyint enum columns. |
| `has-pending-model-changes` | No changes |
| Vulnerability scan | 0 (5 projects) |
| Build | Warnings unchanged (NU1701, CS8618) |
| Throwaway databases | None left |
