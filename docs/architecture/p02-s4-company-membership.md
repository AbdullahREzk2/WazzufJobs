# Phase 02 S4: company membership, company access and tenant isolation

Status: **implemented locally on `phase/02-domain-identity` (2026-11-01). Not deployed.**
- Only throwaway local SQL Server databases were used. The development and production databases were not touched.
- Company registration, invitations, company job posting and the applicant pipeline are **not** enabled (P04/P08).

## 1. What S4 adds
| Part | Where |
|---|---|
| Company permissions per membership role, in code | `DAL/Authorization/CompanyPermissionCatalog.cs` |
| `ICompanyAccessService.RequireAsync(companyId, permission)` | `BLL/Authentication/CompanyAccessService.cs` |
| Company-scoped repository | `ICompanyRepository` / `CompanyRepository` |
| Two read-only endpoints | `GET /api/v1/companies/{companyId}` and `GET /api/v1/companies/{companyId}/members` (`CompaniesController`) |
| Team-management handlers, **not exposed over HTTP** | `ChangeCompanyMemberRoleCommand`, `RemoveCompanyMemberCommand`. P04 adds the endpoints with invitations; S4 implements and tests the rules and the last-owner guard they need. |
| Membership consistency checks | `04_verify_schema.sql` |

No schema change: `Baseline_v2` (`20261101075350_Baseline_v2`) already has `Companies`, `CompanyMembers` and the one-active-membership index.

## 2. Company permissions
| Permission | Owner | Admin | Recruiter |
|---|:-:|:-:|:-:|
| `ViewCompany` | ✓ | ✓ | ✓ |
| `ViewMembers` | ✓ | ✓ | ✓ |
| `EditProfile` | ✓ | ✓ | |
| `ManageTeam` | ✓ | ✓ (not owners) | |
| `TransferOwnership` | ✓ | | |
| `DeleteCompany` | ✓ | | |
| `ManageJobs` | ✓ | ✓ | ✓ |
| `ManageApplicants` | ✓ | ✓ | ✓ |

A **suspended** company's members keep only `ViewCompany`, so they can see the company and its status. Everything else answers 403 `Company.Suspended`.

## 3. `ICompanyAccessService`
Every company handler calls it first, with the `companyId` from the route; a structure test enforces this. It succeeds only for an **active membership that can grant access**:
- the membership is in **this** company and its status is `Active`;
- the company is visible in the request's data scope, so **demo companies are invisible** until demo scope exists (S9);
- the company is **not the platform company**, which is staff-operated;
- the account is a **Company** account, **enabled**, and in the company's partition (real with real, demo with demo).

| Situation | Response |
|---|---|
| No such membership: unknown company, **another tenant**, **removed member**, staff (SuperAdmin, Moderator, DemoAdmin), candidates, disabled or wrong-type accounts | **404 `Company.NotFound`**, the same body as for an unknown id (D-404) |
| Suspended company, anything except viewing it | 403 `Company.Suspended` |
| Role lacks the permission | 403 `Company.PermissionDenied` |

Company access never comes from a platform permission. `CompanyUser` has none (S3), and staff roles grant none. Staff company administration (verification, suspension) arrives with P04/P09.

## 4. Tenant isolation rules
- **Company id only from the route.** The controller binds `companyId` with `[FromRoute]` (`{companyId:guid}`) and has no body parameters (structure test). Job creation still ignores any company in the body (S2b, `DomainPortingTests`).
- **Scoped repository.** Every company-module method of `ICompanyRepository` takes `Guid companyId` as its first parameter and filters by it (structure test). The only exception is the platform-company lookup used by job creation. Updates load the member by (`companyId`, `userId`, `Active`), so a member of another company can never be changed.
- **One active company per account:** the filtered unique index `UX_CompanyMembers_ActivePerUser` (S2a). Two concurrent joins of the same account leave exactly one membership (tested 8×).
- **Responses:**
  - **Company:** `id`, `name`, `slug`, `website`, `industry`, `size`, `location`, `description`, `verificationStatus`, `status`, `createdAt`, `myRole`.
  - **Members:** active members only, each with `userId`, `firstName`, `lastName`, `email`, `role`, `joinedAt`.
  - **Never included:** the logo storage key, demo or platform flags, row versions, or removed members (tested).
- **Server-side only.** All rules are enforced in the API and tested over HTTP without the Angular app.

## 5. Team management and the last-owner guard
Both handlers check `ManageTeam` before taking the lock and again under it:
1. Check `ManageTeam`.
2. Take a transaction with an `UPDLOCK, HOLDLOCK` on the **company row**.
3. Check `ManageTeam` again: a concurrent change may have removed or demoted the caller.

Then the target rules apply:
- **Target:** must be an active member of **this** company (otherwise 404 `Company.MemberNotFound`).
- **Owners are managed only by owners:**
  - an Admin cannot change or remove an owner, or make anyone an owner;
  - otherwise 403 `Company.OwnersManagedByOwners`.
- **Last active owner:**
  - the last active owner (Active, enabled account) can be neither demoted nor removed (400 `Company.LastOwner`);
  - a disabled owner doesn't count and may be demoted.
- **Removal** keeps the membership row as `Removed`, which immediately loses all access (404).

**Concurrency (tested 8 rounds each):**
- two owners stepping down at the same time → exactly one succeeds, the other gets `Company.LastOwner`;
- two owners removing each other → exactly one succeeds;
- in every round exactly one owner remains.

**Mutation checks:**
- Without the company lock, both concurrency tests fail.
- With the membership lookup not filtered by company, the tenant tests fail.

## 6. Authorization matrix
- The two endpoints were added with the new cell type **`M`**: the caller is signed in, and membership decides.
- Every persona (SuperAdmin, Moderator, Candidate, CompanyUser, DemoAdmin) gets **404 `Company.NotFound`** for a company it doesn't belong to, and anonymous callers get 401.
- The matrix now has 55 endpoints.
- The leak sweep also expands `companyId` (no CV URLs, AI fields or other applicants' data for candidates or anonymous callers).

## 7. Script change
`04_verify_schema.sql` (read-only) now also fails when:
- an active membership belongs to a non-company account, to an account of the other partition, or to the platform company;
- a company with active members has no active owner.

Real tools check, run with plain `sqlcmd -b` (no `-I`) on a throwaway database that was dropped afterwards:

| State | `04` result |
|---|---|
| Empty database | PASS, with `ok company memberships consistent` |
| A consistent company | PASS |
| The owner changed to Admin | `FAIL: 1 company(ies) with active members have no active owner`, exit 1 |

## 8. Tests
**332/332** pass: 311 before S4, plus 21 new cases. All C0 and S3 tests are unchanged, except:
- the matrix and leak sweep gained company entries;
- one S3 script test gained a consistent company.

| Class | Cases | Covers |
|---|---|---|
| `CompanyAccessTests` (SQL Server) | 15 | Members see their company and active team with their role. Documented fields only. Another tenant, a removed member, SuperAdmin, Moderator, DemoAdmin, candidate and a company account without membership all get the **same 404 as an unknown id**. Suspended → profile only. Candidate-type, disabled, wrong-partition and platform-company memberships grant nothing. Demo company invisible. **Access service, every role × permission × status** against a hand-written specification. Catalog equals the specification. Owner/Admin/Recruiter team rules. No cross-tenant changes. Last owner (including a disabled second owner). Suspended companies can't change their team. **Three concurrency tests.** |
| `CompanyModuleStructureTests` | 3 | Repository methods scoped by `companyId`; every company handler depends on `ICompanyAccessService`; company id only from the route, no body |
| `ResetScriptTests` | +3 | `04` fails on a candidate-account membership, a platform-company membership and an ownerless company; passes with a consistent company |
| `AuthorizationMatrixTests` | (updated) | Two company endpoints, cell type `M` |

**Other checks:**
- `has-pending-model-changes`: no changes.
- Vulnerability scan: 0 in all 5 projects.
- Build warnings unchanged (CS8618, NU1701).
- Secret scan: 0 matches.
- No throwaway databases left.

## 9. Findings and remaining issues
| # | Finding / issue | Handling |
|---|---|---|
| F-S4-1 | Company clients can't discover their `companyId`: there is no "my company" endpoint, and the token has no company claim | By design for S4 (the approved two endpoints). P04 registration and invitations return it, or P04 adds a discovery endpoint. |
| F-S4-2 | Members can't be added yet, so a company and its first owner can only be created by data (tests or SQL) | P04 (registration and invitations) |
| R-1 | A disabled company account loses company access immediately (the access check requires an enabled account), but its unexpired token is still accepted by the self-service endpoints until it expires | S6a session check |
| R-2 | Staff can't see companies at all (404) until company administration exists | P04/P09 |
| R-3 | Demo companies are unreachable, even for their demo members, until demo scope is implemented | S9 |
| R-4 | The applicant pipeline isn't company-scoped yet: `/api/Applications/*` stays SuperAdmin-only (C0 gate) | P08 |
