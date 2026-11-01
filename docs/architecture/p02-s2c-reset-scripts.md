# Phase 02 S2c: database reset, Baseline_v2 and rollback scripts

Status: **implemented and rehearsed on throwaway local databases (2026-11-01). Nothing has run against production or the development database.**
- The production reset requires **D-RESET** approval and is performed only in the S11 deployment.

## Scripts (`tools/p02/db/`)
Every script starts with the required session header (`session-options.sql`). It works with plain `sqlcmd` (no `-I`); `-b` (stop on the first error) is still recommended.

| Script | Kind | Default | Purpose |
|---|---|---|---|
| `01_reset_drop_application_schema.sql` | **Destructive** | **Dry run** | Drops the P01 application tables (allowlist of 15) and the Hangfire schema (allowlist of 11 tables) |
| `02_baseline_v2.sql` | Schema (generated) | — | Idempotent `Baseline_v2` migration (`New-MigrationScript.ps1`; must equal the current migration, enforced by a test). Its own transaction. |
| `03_reference_data.sql` | Data, insert-only | **Dry run** | Restores missing categories (1–8), the platform company and the `SchemaBaseline` marker. Never updates or deletes. Values are checked against the EF seed by a test. |
| `04_verify_schema.sql` | **Read-only** | — | Checks history, all 24 tables, no legacy tables, the audit trigger, the 4 invariant indexes, the ownership keys and the reference data. Prints `VERIFY: PASS` or raises `VERIFY: FAIL`. |
| `90_rollback_drop_baseline_v2.sql` | **Destructive** | **Dry run** | Rollback step 1: drops the Baseline_v2 tables (allowlist of 25) and Hangfire |
| `91_p01_schema.sql` | Schema (generated) | — | Rollback step 2: the **empty P01 schema**, generated from tag `pre-baseline-v2` (both P01 migrations and their seed). Idempotent. |

**Operator input.** Each guarded script has an *operator input* block with the only lines to edit:
- `wj.ExpectedDatabase`: the **exact** target database name, required;
- `wj.Apply`: `0` for a dry run (default), `1` to apply. `04` has only the database name.

**Guards in `01` and `90`.** Nothing changes unless **all** of these pass:
1. The connected database equals `wj.ExpectedDatabase` (never `master`, `model`, `msdb` or `tempdb`).
2. The migration history is as expected:
   - `01`: only P01 migrations, and `20260429160411_initailCreate` must be present (`AddCVExtractedText` is optional, because the development database lacks it and production is unknown);
   - `90`: exactly `20261101054955_Baseline_v2`.
3. Only allowlisted tables are dropped. **Every other table is preserved and listed.**
4. **The script stops** if an unknown object depends on a table to be dropped (foreign key or schema-bound view or function), if an unknown table sits in the `HangFire` schema, or if an unknown table would collide with a table the next step creates.
5. Views, procedures, functions and database DDL triggers are reported, never dropped.

**Stopping and atomicity.**
- On a guard failure the script raises an error and sets `NOEXEC ON`, so no later batch runs, even in tools that continue after errors.
- Apply runs in **one transaction with `XACT_ABORT ON`**: any error, including one raised by a database DDL trigger, rolls back everything.
- A second run stops cleanly. For `01` the history table is gone, so it reports "already reset".

## Rehearsal (throwaway local databases only)
**Automated** (`ResetScriptTests`, 29 cases, every run):

| Area | Covered |
|---|---|
| Dry run | Reports the full inventory with row counts; fingerprint (tables, row counts, keys, triggers, schemas) unchanged |
| Database-name guard | Empty, development name, production-like name: refused, unchanged |
| History guard | Baseline_v2 row, unknown row, base migration missing, no history table: refused, unchanged. A P01 history without the second migration is accepted. |
| Unsafe objects | Unknown FK to a P01 table, schema-bound view, unknown table in `HangFire`, unknown `dbo.Companies`: refused, unchanged |
| Apply | Drops exactly 26 tables (15 P01 + 11 real Hangfire 1.8.23 tables) and the `HangFire` schema; preserves `dbo.Zz_Unrelated` and `ops.Notes` **with their data** |
| Atomicity | A database DDL trigger that blocks one drop makes the reset fail; **fingerprint identical to before** (all tables, rows, keys, Hangfire) |
| Repeatability | Second reset stops without change; `02` twice; `03` twice (inserts nothing); `91` twice |
| Forward path | Reset → `02` → `03` → `04` PASS → the API serves the database (`/health` Healthy) |
| Reference data | Restores deleted categories and the platform company without touching an admin-renamed category; `04` PASS afterwards |
| Verification | Fails on: dropped audit trigger, unknown migration, dropped invariant index, missing platform company, legacy `CVs` table |
| Wrong state | `03` and `04` refuse a P01 database; `90` refuses a P01 database and an unset name |
| Rollback | `90` dry run unchanged; apply leaves only the unrelated table; `91` twice gives the P01 history and 0 accounts; schema **identical to a fresh P01 schema**; forward again → `04` PASS with the unrelated table intact |
| Drift | Every script starts with the header; `02` equals the current migration; `03`/`04`/`90` reference the current migration id; `03` values equal the EF seed data |

**Manual, end to end** (plain `sqlcmd -b` without `-I`, real processes, database `WazzufJobs_S2cRehearsal_20261101101850`, dropped afterwards):

| Step | Result |
|---|---|
| R1 `91` creates the P01 schema | exit 0 |
| R2 **P01 API** (built from tag `pre-baseline-v2`) | `/health` **200 Healthy**. It installed the 11 Hangfire tables. Synthetic user, job and unrelated table (value 42) added. |
| R3 `01` dry run, then apply | Dry run: 27 tables before and after. Apply: **26 dropped, 1 preserved** (`Zz_Unrelated` = 42). |
| R3b `01` again | **Stopped**: "already reset", no change, exit 1 |
| R4 `02` ×2, `03` dry run + apply ×2, `04` | All exit 0. `03` inserted nothing (seeded by `02`). **`VERIFY: PASS`** (24 tables, trigger, 4 indexes, ownership keys, 2 roles, 33 claims, 8 categories, platform company, marker, 0 accounts). |
| R5 **current API** (S2b) | `/health` **200 Healthy**; anonymous `my-applications` and `Jobs` → 401; Hangfire recreated 11 tables |
| R6 `90` dry run + apply, `91` ×2, **P01 API** | 36 dropped (24 + history + 11 Hangfire), 1 preserved; P01 history restored, 0 accounts; **P01 API `/health` 200 Healthy**, anonymous application → 401 |
| R7 forward again (`01`, `02`, `03`, `04`) | **`VERIFY: PASS`**, `Zz_Unrelated` still 42 |
| R8 cleanup | 0 rehearsal or test databases left |

**Finding F-S2c-1.** The **P01** schema also contains filtered indexes: ASP.NET Identity's `UserNameIndex` and `RoleNameIndex`. Any **ad-hoc** write to `AspNetUsers` or `AspNetRoles` through plain `sqlcmd` therefore fails with error 1934. The reviewed scripts are unaffected (they carry the header), but **any manual SQL must start with the session header**. The C0 bootstrap script already instructs `sqlcmd -I`; S8 replaces it.

## Production procedure (S11, only after D-RESET approval)
**Prerequisites:**
1. **D-RESET** approved, and the S10 release candidate (package and scripts) built and verified. The script SHA-256 values are recorded in the S10 release notes.
2. **SQL access verified (VER-SQL)**:
   - **Preferred:** remote access from the owner's machine with `sqlcmd` or SSMS (server address, login and password entered by the owner only; never shared).
   - The MonsterASP web query tool may only be used if it is verified to run multi-batch scripts (`GO`) on one session and to show `PRINT` output.
3. The **exact production database name**, as shown in the MonsterASP control panel.
4. **Read-only dry run of `01` against production**, approved separately as a read. It shows the real migration history and table inventory, and must report only `DROP` lines for P01/Hangfire tables and no `STOP` line.
5. Optional, recommended: a database backup (D-BACKUP).
6. Maintenance window; `tools/c0/deploy/app_offline.htm` at hand.

**Steps** (each script: edit the operator input, run the whole file, keep the output):

| # | Action | Must show |
|---|---|---|
| 1 | Upload `app_offline.htm` | API shows the maintenance page |
| 2 | Verify the SHA-256 of `01`–`04` against the release notes | Match |
| 3 | `01` with `wj.Apply = 0` | Inventory as in the prerequisite dry run; `DRY RUN complete` |
| 4 | `01` with `wj.Apply = 1` | `RESET APPLIED: 26 table(s) dropped` (15 + 11), preserved tables as expected |
| 5 | `02_baseline_v2.sql` | Exit 0 |
| 6 | `03` dry run, then `wj.Apply = 1` | `inserted 0 …` (already seeded by `02`) |
| 7 | `04` | **`VERIFY: PASS`**, `info accounts: 0` |
| 8 | Deploy the backend package, remove `app_offline.htm` | `/health` Healthy; lifecycle `schema=Compatible` |
| 9 | Continue with the S11 steps (Super Admin bootstrap, verification) | — |

**Failure handling:**

| Situation | Database state | Action |
|---|---|---|
| `01` stops (guard) | Unchanged | Remove `app_offline.htm`; P01 keeps running. Investigate; no retry without review. |
| `01` apply error | **Unchanged** (rolled back) | As above |
| `02` error | P01 dropped, Baseline_v2 transaction rolled back | Re-run `02` after fixing the cause, or roll back: `91` → P01 package |
| `04` FAIL | Baseline_v2 incomplete | Do not deploy the new package. Investigate, or roll back. |
| Problem after deployment | Baseline_v2 | `app_offline.htm` → `90` (dry run, then apply) → `91` → P01 package → remove `app_offline.htm`. The database is empty and fail-closed (no administrator); the C0 bootstrap script (`sqlcmd -I`) creates one if needed. |
| Any time | — | **Never restore the pre-C0 build**: it re-seeds the default administrator into an empty database |

## Remaining risks
| # | Risk | Mitigation |
|---|---|---|
| 1 | Production's migration history is unknown (never recorded) | The prerequisite read-only dry run shows it; the guard refuses anything that is not P01 |
| 2 | How SQL will be run on MonsterASP is not yet verified (remote `sqlcmd`/SSMS vs the web tool) | Prerequisite VER-SQL before D-RESET. The scripts need one session across batches (session context, temporary tables). |
| 3 | The reset is irreversible: all accounts and data are deleted (disposable data, accepted) | D-RESET approval; optional backup (D-BACKUP); dry run first |
| 4 | Unknown production objects | Unknown tables preserved; dependent or colliding objects stop the reset; views, procedures and DDL triggers reported |
| 5 | Production's Hangfire tables differ from the 1.8.23 allowlist | An extra table in `HangFire` stops the reset (safe). A missing table is simply not dropped. |
| 6 | Ad-hoc SQL without the header fails on Identity's filtered indexes (F-S2c-1) | Every manual statement starts with `session-options.sql` |
| 7 | `Baseline_v2` is regenerated in later stages (S3 onwards) | `02`/`03`/`04`/`90` must be updated with it. Drift tests fail otherwise; frozen at S10. |
| 8 | Operator edits are text edits | Exact-name guard; dry run by default; outputs kept for review |
