# C0 read-only SQL

`01_readonly_production_assessment.sql` supports **VER-01** (is production data test-only?) and **VER-03** (background-job latency on the host). It contains `SELECT` statements only.

Recommended procedure:
1. In the MonsterASP control panel, create a database backup and **download** the `.bak` file to your machine.
2. Restore it locally under a separate name, for example in SSMS: *Databases → Restore Database → Device*, target name `WazzufJobs_ProdRestore`. Your local SQL Server version must be the same or newer than the host's.
3. Run the script against `WazzufJobs_ProdRestore`, in SSMS (New Query, choose the database, Execute) or with:
   `sqlcmd -S . -E -I -W -d WazzufJobs_ProdRestore -i 01_readonly_production_assessment.sql -o assessment-results.txt`
   (`-I` is required: sqlcmd defaults to `QUOTED_IDENTIFIER OFF`.)
   Results 3 and 5 contain personal data. Keep them on your machine.
4. Report the aggregated counts (results 1, 2, 4, 8 and 9) and your classification decision, not raw personal data.

Running the script on a local restore also exercises the restore procedure (VER-02).

Validation (2026-10-30): the script was run without errors against a throwaway database created from the project's EF migrations (the same schema as production) with synthetic rows, with and without a Hangfire schema. The database was dropped afterwards. It contains no write or DDL statements and no `STRING_AGG`, so it works on SQL Server 2016+.
