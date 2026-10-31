# Phase 02 decision log

Each entry records what was decided, by whom and when, and where the design is specified. "Owner" means the project owner's written approval in the working session. Section references (§) point to `docs/architecture/p02-domain-identity.md`.

**Status values:**
- **Approved**: approved in principle with the architecture on 2026-10-31.
- **Gated**: needs a separate, explicit approval at the named stage.
- **Pending**: waiting for evidence, such as a probe result.

## Inherited (Phase 00, unchanged)
| ID | Decision | Status |
|---|---|---|
| P00-DEMO | Shared demo accounts per role, plus a nightly reset of an isolated demo dataset | Approved (Phase 00) |
| P00-COMPANIES | Company self-registration, then verification by a Super Admin or Moderator before jobs can be submitted | Approved (Phase 00) |
| P00-STAFF | Staff roles are Super Admin and Moderator. A Moderator never sees CVs or AI assessments. | Approved (Phase 00) |
| P00-UI | Angular Material with a custom WazzufJobs theme, delivered in P03 | Approved (Phase 00) |
| P00-NOADMIN | No default or seeded administrator, ever | Approved (C0) |

## Phase 02 owner answers (2026-10-31)
| ID | Decision | Status |
|---|---|---|
| P02-FE | Minimal Angular changes in P02 only: 2FA login step, TOTP enrollment, auth service and interceptor, read-only Roles page. No redesign. | Approved |
| P02-VERIFY | Post-deployment checks use synthetic verification data. CV uploads stay disabled. | Approved |
| P02-COOKIE | Move the refresh token to an httpOnly cookie in P02, gated by VER-08 | Approved. VER-08 (S0): **viable** under D-PROXY-IP v2 (`docs/architecture/ver-08-auth-proxy-probe.md`) |

## Architecture decisions
| ID | Decision | § | Status |
|---|---|---|---|
| D-IDS | `int` ids for jobs, applications, categories and resumes; GUIDs for companies, sessions and challenges | 1 | Approved |
| D-PLATFORM-CO | A seeded "WazzufJobs" platform company owns admin-created jobs until P04 | 1 | Approved |
| D-STAFF-ROLES | Staff roles cannot be assigned through the legacy Users endpoints. Moderator management comes in P09. | 2 | Approved |
| D-404 | A resource outside the caller's tenant or ownership returns 404. A missing permission returns 403. | 4 | Approved |
| D-SESSION-TTL | Access token 15 min. Candidate/company refresh 14 d sliding, 30 d absolute. Staff refresh 8 h sliding, 24 h absolute. Rotation grace 10 s with no credentials issued. Session-check cache 30 s. | 5.1–5.2 | Approved |
| D-ROTATION | Compare-and-swap rotation. A filtered unique index allows at most one usable refresh token per session. Reuse after the grace period revokes the session. | 5.2 | Approved |
| D-PROXY | Only the four cookie endpoints (`session/prepare`, `session`, `refresh`, `logout`) go through a static `vercel.json` route (delete-then-set secret transform, caching off, no middleware). Cookie endpoints require the secret, otherwise 403. | 5.4 | Approved; verified by VER-08 |
| ~~A1~~ | Trust `Forwarded` / `X-Vercel-Proxied-For` as the client IP when the proxy secret is valid | 5.4 | **Rejected 2026-10-31.** Forwarded client-IP headers can't be established as a reliable security boundary (undocumented; a related documented header proved spoofable). |
| **D-PROXY-IP v2** | **No client IP is ever read from headers.** **Split routing:** credential and email endpoints direct to MonsterASP, keyed by the TCP peer (IPv6 /64); cookie endpoints proxied behind the secret gate. Credential endpoints reached with a secret or `X-Vercel-Id` → 403. | 5.3–5.4 | **Approved 2026-10-31** |
| D-HANDOFF | Browser-bound, single-use session hand-off (`__Secure-wj_bind` + `bindingHash`, 60 s, CAS consume). The first refresh token is issued only via the proxy. | 5.3 | **Approved**, subject to full local security and concurrency tests (S6b) |
| D-INVARIANTS-P02 | Re-word C0-7 and replace P02-7 without weakening protections | invariants | **Approved 2026-10-31** |
| D-WAF-RATELIMIT | Vercel WAF per-IP rate-limit rule on `/api/auth/*` (documented; Hobby: one rule) | 5.4 | Optional. **Not to be configured in production yet.** |
| R-SAFARI | Real Safari / iOS cookie behaviour unverified (VER-08 F3) | 5.5 | **Open risk.** Test before production deployment where possible. |
| D-FAILCLOSED | If VER-08 fails, the owner decides the transport. Body transport is **not** selected automatically, and none of the prohibited fallbacks is ever used. | 5.5 | Approved |
| D-VER08 | The probe runs in a throwaway Vercel project with a disposable secret and synthetic data only | 5.5 | Approved (S0) |
| D-2FA | Mandatory TOTP for Super Admin and Moderator (Demo Admin exempt). Replay protection, encrypted key, 10 hashed recovery codes, break-glass SQL script. | 6 | Approved |
| D-BOOTSTRAP | One-time Super Admin bootstrap: hashed token plus password plus a confirmed account. The endpoint returns 404 when inactive. The C0 SQL script is archived. | 7 | Approved |
| D-AUDIT | Append-only audit log (trigger). Unknown-email login failures are not audited. IP addresses are stored as HMACs. | 7 | Approved |
| D-VALIDATION | `ValidationBehavior` is carried into P02 as stage S1. Existing validators are made compatible with current clients before enabling them; responses use RFC 9457 `ValidationProblemDetails` with `detail` = the first message. | 12 | Approved; **implemented in S1** (`docs/architecture/p02-s1-validation.md`) |
| D-CATEGORIES | Seed the current category list without icons | 9 | Approved |
| D-REHEARSAL-MAIL | Pickup-directory email sender for the local rehearsal only. Startup refuses it in Production. | 9.1 | Approved |
| D-CONFIG | New server secrets (`Security:DataKey`, `Proxy:SharedSecret`) and the Vercel env var `WJ_PROXY_SECRET`. The owner generates and sets them; the values are never shared. | 5.4, 6 | Approved; owner action at S11 |
| D-ORPHANS | After the reset, Cloudinary image orphans are inventoried read-only. Deleting them is a separate approval. | 9 | Approved |
| **D-RESET** | Reset the production database to `Baseline_v2` | 9, 13 | **Gated**: after the S10 rehearsal report |
| **D-FIXTURE** | Upload 2 synthetic PDFs to Cloudinary for verification data | 13 | **Gated**: at S11 |
| D-BACKUP | Optional MonsterASP backup before the reset | 13 | Recommended; owner's choice at S11 |
| D-DEPLOY / D-PUSH | Production deployment, then pushing and merging | 13 | **Gated**: S11 / S12 |
