# VER-08: authentication proxy probe results (Phase 02, stage S0)

Date: 2026-10-31.
Probe sources: `tools/p02/ver-08-probe/`.
Design under test: architecture §5.3–§5.5.

## Verdict
**The httpOnly-cookie design is viable.**
- Amendment A1 (trusting forwarded client-IP headers) was **rejected** after security review on 2026-10-31.
- The adopted architecture is **D-PROXY-IP v2**: no client IP is ever derived from headers. Credential endpoints connect directly to MonsterASP, keyed by the TCP peer. Only the high-entropy cookie endpoints use the authenticated proxy, with a browser-bound single-use session hand-off. See architecture §5.3–5.4.

**Passed:**
- Cookie handling in Chromium and Firefox.
- Secret injection, including resistance to client forgery.
- Routing and path scoping.
- Pass-through of the CSRF header, `Origin` and preflight.
- CDN caching disabled.
- Secret confidentiality.

**Open:**
- **Finding F1:** one Vercel header, `X-Vercel-Forwarded-For`, is client-spoofable. The design must not use it (A1).
- **Real Safari** is not yet verified. The available WebKit engine is not representative (F3). This is an optional owner check.
- **`X-Real-IP` / `X-Forwarded-For`** (the headers Vercel documents) could not be observed, because both public echo origins strip them. They are verified at the real API in S6b (A2).

## Setup
- **Vercel project:** a separate, temporary project `wj-auth-probe` (team `abdullahrezk2s-projects`), on its production domains `wj-auth-probe.vercel.app` and `wj-auth-probe-xsite.vercel.app`.
  - Preview and deployment URLs are behind Vercel SSO protection, which blocks automation, so the throwaway project's production domains were used.
  - **The production project `wazzuf-jobs` was not modified or deployed.**
- **Secret:** a disposable 256-bit `WJ_PROXY_SECRET` (Vercel env var marked Sensitive, Production only). It was never printed; checks compare SHA-256 values.
- **Origins:** `httpbin.org`, plus `postman-echo.com` for header visibility. Synthetic data only.
- **Clients:**
  - Node 22 `fetch` for header-level checks;
  - Playwright 1.x with Chromium 156, Firefox 157 and WebKit 27.2 (Windows build).

## Results
| # | Check | Result |
|---|---|---|
| P1 | `/api/auth/*` is proxied ahead of the SPA catch-all. SPA routes and static files are unaffected. | **PASS** |
| P1 | Path matching is **case-sensitive**: `/API/auth/*` is not proxied and returns `index.html` | Confirmed (SPA must use lowercase) |
| P2 | Secret injected. A client-forged value (single or duplicated) is **replaced**, never kept or appended. | **PASS** |
| P2 | If the env var is unset, the origin receives the **literal `$VAR` string** | Confirmed. The API's "invalid secret → 403" rule fails closed on misconfiguration. |
| P3 | `Forwarded: for=<client>` is always the real client IP, despite forged `Forwarded`, `X-Forwarded-For`, `X-Real-IP`, `X-Vercel-*` | **PASS** (12/12 in the final run; 48/48 and 30/30 in exploratory runs) |
| P3 | `X-Vercel-Proxied-For` is always the real client IP, single-valued, and not spoofable | **PASS** (same runs) |
| P3 | **F1: `X-Vercel-Forwarded-For` passes a client-forged value through to the origin** | **Spoofable**: 6/12 in the final run, 14/24 earlier (varies by edge node). **Must never be trusted.** |
| P3 | `X-Real-IP` / `X-Forwarded-For` | Not observable (filtered by both echo origins). Forged values never appeared in any header the origin received. |
| P5 | POST JSON body, `X-WJ-CSRF`, `Origin` and `Sec-Fetch-Site` reach the origin unchanged | **PASS** |
| P5 | `OPTIONS` preflight is answered by the **origin** (Vercel does not answer it), so the API's CORS policy decides | **PASS** |
| P6 | Never served from the CDN cache (`x-vercel-cache: MISS`); `Cache-Control: no-store` overrides the origin's `public, max-age` | **PASS** |
| P7 | Secret absent from static files, from `vercel.json` (which is publicly served but holds only `$WJ_PROXY_SECRET`), and from all response headers | **PASS** |
| P4/P8 | Chromium and Firefox, each of: cookie stored `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`, host-only; hidden from JS; sent back same-origin; not sent outside `/api/auth`; two `Set-Cookie` headers both stored; `Max-Age=0` deletes; **not sent on a cross-site fetch or a cross-site top-level navigation**; also not sent between two `*.vercel.app` hosts (public suffix) | **PASS** |
| P4/P8 | **F3: Playwright WebKit (Windows)** stores `SameSite=Strict` as `None` and sends the cookie cross-site | Engine limitation (WinCairo network stack), **not Safari**. Real Safari pending (optional owner check). |
| P9 | 10 s origin delay through the proxy | 200 in 10.3 s. Longer delays untested (echo cap). |
| P10 | Header names received by the origin | `Accept`, `Accept-Encoding`, `Accept-Language`, `Forwarded`, `Host`, `Sec-Fetch-Mode`, `User-Agent`, `X-Forwarded-Host`, `X-Vercel-Forwarded-For`, `X-Vercel-Id`, `X-Vercel-Ja4-Digest`, `X-Vercel-Proxied-For`, `X-Vercel-Proxy-Signature`, `X-Vercel-Proxy-Signature-Ts`, `X-Wj-Proxy-Secret` |

## Security review of A1 and decision (2026-10-31)
**New evidence:** the production lifecycle log shows `remoteAddressClass=public` and `xForwardedForPresent=False`. MonsterASP has no proxy in front of the API, so `Connection.RemoteIpAddress` is the real client's TCP address. A TLS connection's source address can't be forged by headers.

**Why A1 was rejected:**
- `Forwarded` and `X-Vercel-Proxied-For` were correct in about 90 forged requests, but they are **not documented** as an origin-facing contract.
- Vercel's documentation for the related `X-Vercel-Forwarded-For` was **contradicted** by observation (F1).
- The documented headers (`X-Real-IP`, `X-Forwarded-For`) could not be observed at an origin.
- A per-IP limit derived from any of them can't be proven unbypassable, so it is not acceptable as a security boundary.

**Adopted: D-PROXY-IP v2** (owner approval 2026-10-31):
1. **No header is ever used as a client IP.** `ForwardedHeaders` middleware stays off.
2. **Split routing:**
   - credential and email endpoints are **direct**, with C0 `ipLimit` keyed by the TCP peer (IPv6 per /64);
   - only `session/prepare`, `session`, `refresh` and `logout` are **proxied**. They accept only ≥ 256-bit secrets and use per-token and failure-budget limits.
3. **Proxy gate:** cookie endpoints require exactly one valid secret, otherwise 403. Credential endpoints reached with a secret or `X-Vercel-Id` → 403.
4. **Browser-bound, single-use hand-off** (`__Secure-wj_bind` + `bindingHash`) replaces setting the cookie in the login response. Approved subject to full local security and concurrency tests.
5. **Vercel WAF per-IP rule:** optional and **not configured in production**.
6. **Real Safari:** an open compatibility risk, to be tested before production deployment where possible.

## Superseded proposal (kept for the record)
The following amendments were proposed before the review. **A1 is rejected.** A2–A4 are folded into D-PROXY-IP v2 as noted.
- **A1 (REJECTED), trusted client-IP source.** `TrustedProxyMiddleware` (with a valid secret) derives the client IP from **`Forwarded: for=`** and **`X-Vercel-Proxied-For`**.
  - Each must be present, single-valued and parseable as an `IPAddress`, and **both must be equal**. If `X-Real-IP` is present, it must also be equal.
  - **`X-Vercel-Forwarded-For` is never read.**
  - Any missing, multi-valued or disagreeing value → **403 `Proxy.Untrusted`** (fail closed). This replaces "`X-Real-IP`, falling back to `X-Vercel-Forwarded-For`" in §5.4.
- **A2 (replaced by the S11 two-network and header-forgery checks), verification at the real API.**
  - S6b integration tests cover A1, including disagreement → 403.
  - The S10 rehearsal cannot exercise Vercel. At S11, a production check sends forged `Forwarded`, `X-Vercel-*`, `X-Real-IP` and `X-Forwarded-For` through the proxy and confirms that the rate-limit partition stays on the real IP.
  - The API logs, without values, which of the three headers were present on the first proxied request, so the owner can confirm what IIS receives.
- **A3 (adopted: credential endpoints with `X-Vercel-Id` → 403), marker rule.** `X-Vercel-Id` reaches the origin. The rule "Vercel markers without a valid secret → 403" uses `X-Vercel-Id`.
- **A4, unchanged elements, now evidenced:**
  - delete-then-set transform;
  - caching disabled on the route plus `no-store` from the API;
  - lowercase `/api/auth` paths (cookie path and Vercel matching are both case-sensitive);
  - CSRF protection that doesn't depend on SameSite alone (custom header → preflight → API CORS; `Origin` allowlist; refresh is POST-only).

## Not adopted
`X-Vercel-Proxy-Signature` / `-Ts` is present but undocumented for origin verification, so it is not relied on. The shared secret remains the trust anchor.

## Remaining risks
| Risk | Handling |
|---|---|
| Vercel changes route or transform behaviour | Cookie endpoints fail closed (403). No IP trust exists to be subverted. S11 and post-deploy checks. |
| Real Safari / iOS unverified | Optional owner check with `selftest.html`. CSRF defences don't rely on SameSite alone. |
| Origin latency above 10 s through the proxy is unmeasured | The SPA never signs out on 5xx (§5.2). The cold-start risk stays in the risk register. |
| `vercel.json` is publicly downloadable (route shape and env var *name* visible) | Contains no secret value. Acceptable. |

## Probe lifecycle
The temporary project, its domains, the disposable secret and the local tooling remain until the owner finishes the optional Safari check or declines it. Cleanup is `vercel project rm wj-auth-probe` plus deleting the temporary folder.
