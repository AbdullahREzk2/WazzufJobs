# Database deployment scripts (Phase 02)

Full procedure, guards, rehearsal results and risks: `docs/architecture/p02-s2c-reset-scripts.md`.
**Nothing here may run against production without explicit approval (D-RESET).**

| File | Purpose |
|---|---|
| `session-options.sql` | **Required header of every SQL deployment script.** It sets `QUOTED_IDENTIFIER`, `ANSI_NULLS` and the other options filtered indexes need, then verifies them. If any option is wrong it raises an error and sets `NOEXEC ON`, so no later batch runs. Findings F-S2a-1 and F-S2c-1. |
| `New-MigrationScript.ps1` | Generates the idempotent EF Core migration script with the header and prints its SHA-256. It writes a file only and never connects to a database. |
| `01_reset_drop_application_schema.sql` | **Destructive; dry run by default.** Guarded reset of the P01 schema and Hangfire (allowlists; unknown objects preserved or stop) |
| `02_baseline_v2.sql` | Generated `Baseline_v2` migration (idempotent). Regenerate with `New-MigrationScript.ps1 -OutputPath tools\p02\db\02_baseline_v2.sql`. |
| `03_reference_data.sql` | Insert-only reference data (fixed platform roles and permission claims, categories, platform company, baseline marker); dry run by default. Stops if a role's id and name differ from `PermissionCatalog`. |
| `04_verify_schema.sql` | Read-only verification: `VERIFY: PASS` or `VERIFY: FAIL`. Includes the exact roles and permission claims of `PermissionCatalog`, account-type compatibility of every account's role (S3) and company-membership consistency (S4). |
| `90_rollback_drop_baseline_v2.sql` | **Destructive; dry run by default.** Rollback step 1: guarded drop of Baseline_v2 and Hangfire. |
| `91_p01_schema.sql` | Rollback step 2: the empty P01 schema, generated from tag `pre-baseline-v2` |

## Rules for all deployment scripts
1. Every script starts with the contents of `session-options.sql`. Hand-written scripts include it verbatim; generated scripts get it from `New-MigrationScript.ps1`. **Ad-hoc SQL must start with it too** (Identity's filtered indexes).
2. Edit only the *operator input* block: the exact database name and `wj.Apply`. Run the whole file and keep its output.
3. Scripts run only with explicit owner approval. `sqlcmd -b` is recommended; the scripts don't depend on `-I` or other client settings.

## Regenerating `91_p01_schema.sql`
1. Export the P01 sources: `git archive pre-baseline-v2 SrcCode/WazzufJobs`, extracted to a temporary folder.
2. In that folder's `WazzufJobs.API`, run `dotnet restore`, then `dotnet tool run dotnet-ef migrations script --idempotent --project ..\WazzufJobs.DAL\WazzufJobs.DAL.csproj --startup-project WazzufJobs.API.csproj --output p01.sql`.
3. Write `session-options.sql` + the description block + `p01.sql`.

Tests: `MigrationScriptTests` (header) and `ResetScriptTests` (guards, atomicity, rehearsal, rollback, drift).
