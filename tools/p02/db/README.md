# Database deployment scripts (Phase 02)

| File | Purpose |
|---|---|
| `session-options.sql` | **Required header of every SQL deployment script.** It sets `QUOTED_IDENTIFIER`, `ANSI_NULLS` and the other options that filtered indexes need, then verifies them. If any option is wrong it raises an error and sets `NOEXEC ON`, so no later batch runs. Finding F-S2a-1: `sqlcmd` starts sessions with `QUOTED_IDENTIFIER OFF`. |
| `New-MigrationScript.ps1` | Generates the idempotent EF Core migration script with the header prepended, and prints its SHA-256. It writes a file only and never connects to a database. |

## Rules for all deployment scripts (S2c and later)
1. Every script starts with the contents of `session-options.sql`. Hand-written scripts (reset, reference data, verification, rollback) include it verbatim, and generated migration scripts get it from `New-MigrationScript.ps1`.
2. Scripts are reviewed, then run only with explicit owner approval. The reset (D-RESET) is a separate decision.
3. `sqlcmd -b` is still recommended (stop on the first error), but the scripts no longer depend on `-I` or any other client setting.

The header is covered by `MigrationScriptTests`:
- the script succeeds in a session that started with `QUOTED_IDENTIFIER OFF`;
- the same script without the header fails;
- the verification stops all later batches.
