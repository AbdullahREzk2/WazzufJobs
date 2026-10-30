# Assessment: public CV exposure (S3/B15)

Status: **CLOSED (2026-10-30).** All legacy CV assets were owner-confirmed test files and have been deleted (D21 approved); no third-party data was involved.
Not legal advice. Notification obligations under Egypt PDPL 151/2020 and UAE PDPL (Federal Decree-Law 45/2021) need counsel.

## Known facts (verified from code, 2026-10-30)
- Since the CV feature was introduced (commit `f9aa123`, 2026-05-14), CVs were uploaded to Cloudinary as `resource_type=raw`, `type=upload` (public delivery) under `wazzuf-jobs/cvs/`.
- The public URL was returned to the owner (`GET /api/cv`) and to admins (application detail; uncommitted user detail).
- Because of S1, any authenticated job seeker could obtain other applicants' CV URLs via `GET /api/applications/{id}`.
- Delete and replace called `DestroyAsync` without `resource_type=raw`, so the files were probably **not deleted** (B15).
- Cloudinary free accounts block PDF/ZIP delivery by default unless the security setting is enabled. Whether the public URLs actually served the files is **unknown** (spike step "anonymous GET of a public raw PDF" and `inventory --probe-public` answer this).

## Results (2026-10-30)
| Item | Value |
|---|---|
| Total raw CV assets (by type) | **13**, all `resource_type=raw`, `type=upload` (public delivery type); 5,039,361 bytes in total. No `authenticated` or `private` raw assets existed before the spike. |
| Location | 12 in `wazzuf-jobs/cvs/`, 1 in `wazzuf-jobs/debug/` (`byynalesdkb86pmd676f.pdf`, a debugging upload of a test CV) |
| Assets referenced by the CVs table / orphaned | Not determined: the database join was not run, because the production DB holds disposable test data and the backup/restore gate was waived. |
| Accounts owning assets | **Owner confirmed all 13 are the owner's own disposable test files** |
| Were public URLs actually reachable? | **Very likely not.** In the synthetic spike on this account, an anonymous GET of a public raw PDF returned **HTTP 401**: Cloudinary's free-tier PDF delivery restriction is active. The historical state of that setting cannot be proven, but no third-party data existed in any case. |
| Earliest / latest upload | 2026-05-14 / 2026-06-25 |
| Evidence of access by others | Not applicable (owner test data) |
| Third-party individuals affected | **0** |
| Counsel consulted? Notification required? | Not required: no personal data of third parties |
| Remediation (D21) | **Deleted all 13** with the guarded `CloudinaryC0Tool delete-confirmed` command (resource_type=raw, type=upload, invalidate=true), restricted to the exact IDs of the owner-reviewed manifest after verifying them against a fresh inventory (identical IDs, types and sizes; no other raw assets present). The command was first verified against a local fake Cloudinary (mismatch, missing-ID and wrong-count cases abort with zero deletions). |
| Deletion results | 13 of 13 returned `ok`; report `%USERPROFILE%\WazzufJobs-C0\cloudinary-delete-report-20261030-184410.json` (kept locally) |
| Verification | Fresh read-only inventory after deletion: **0 raw assets**. Images (profile photos, category icons) are image resources and were outside the command's scope; only 13 raw destroy calls were issued. |
| Remaining effect | The production `CVs` table still references the deleted files (database untouched, as instructed). CV downloads for those rows return "temporarily unavailable" (handled), AI scoring still uses the stored extracted text, and candidates re-upload once D21b enables private uploads. |
