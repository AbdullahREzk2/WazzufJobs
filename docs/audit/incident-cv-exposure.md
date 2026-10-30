# Assessment: public CV exposure (S3/B15)

Status: **OPEN. Inventory not yet run.** This document must be completed before D21 remediation and closed after verification.
Not legal advice. Notification obligations under Egypt PDPL 151/2020 and UAE PDPL (Federal Decree-Law 45/2021) need counsel.

## Known facts (verified from code, 2026-10-30)
- Since the CV feature was introduced (commit `f9aa123`, 2026-05-14), CVs were uploaded to Cloudinary as `resource_type=raw`, `type=upload` (public delivery) under `wazzuf-jobs/cvs/`.
- The public URL was returned to the owner (`GET /api/cv`) and to admins (application detail; uncommitted user detail).
- Because of S1, any authenticated job seeker could obtain other applicants' CV URLs via `GET /api/applications/{id}`.
- Delete and replace called `DestroyAsync` without `resource_type=raw`, so the files were probably **not deleted** (B15).
- Cloudinary free accounts block PDF/ZIP delivery by default unless the security setting is enabled. Whether the public URLs actually served the files is **unknown** (spike step "anonymous GET of a public raw PDF" and `inventory --probe-public` answer this).

## To be completed
| Item | Value |
|---|---|
| Total raw CV assets (by type) | _from inventory_ |
| Assets referenced by the CVs table / orphaned | _from inventory + restore_ |
| Accounts owning assets: own/test vs third-party | _your classification_ |
| Were public URLs actually reachable? (HTTP status) | _spike / probe_ |
| Earliest and latest upload dates | _inventory_ |
| Evidence of access by others (Cloudinary reports, if available) | _check console_ |
| Third-party individuals affected | _count only_ |
| Counsel consulted? Notification required? | _decision_ |
| Remediation per class (D21 approval) | _delete / convert_ |
| Verification: 0 public raw CV assets; old URLs 404/401 at +15 min and +24 h | _results_ |
