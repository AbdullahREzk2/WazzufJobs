# VER-08 authentication-proxy probe

This is the Phase 02 S0 probe that checks whether the Vercel proxy route can carry the httpOnly refresh cookie. Results are in `docs/architecture/ver-08-auth-proxy-probe.md`.

**Scope and safety:**
- Runs only in a **separate, temporary Vercel project** (`wj-auth-probe`). The production project `wazzuf-jobs`, MonsterASP, Cloudinary and the database are never involved.
- The origin is a public echo service (`httpbin.org`, plus `postman-echo.com` on `/api/auth-pm/*`). Only synthetic data is used.
- `site/vercel.json` reproduces the planned production route: delete-then-set secret transform, caching off, and the SPA catch-all after it. A second route references an unset env var, to observe misconfiguration behaviour.

## Run
1. Generate a disposable secret into a file **outside the repository**, and never print it. For example:
   ```
   python -c "import secrets;open(r'%TEMP%\wj-auth-probe\secret.txt','w',newline='').write(secrets.token_hex(32))"
   ```
2. In a temporary copy of `site/`, run:
   ```
   vercel project add wj-auth-probe
   vercel link --yes --project wj-auth-probe
   vercel env add WJ_PROXY_SECRET production --sensitive < <secret file>
   vercel deploy --prod --yes
   ```
   Then add a second production domain for the cross-site check: `vercel domains add wj-auth-probe-xsite.vercel.app`.
   - The probe uses the throwaway project's **production** domains, because preview and deployment URLs sit behind Vercel SSO protection.
3. In `runner/`, run:
   ```
   npm i playwright@1
   npx playwright install chromium firefox webkit
   ```
   Then run `node probe.mjs` with `WJ_PROBE_SECRET_FILE=<secret file>`, and `node selftest.mjs`.
4. **Real Safari (manual):**
   1. Open `https://wj-auth-probe.vercel.app/selftest.html` and press **Run test**.
   2. Follow the link to the `-xsite` page and press **Run test** again.
   3. Both pages must show `RESULT: ALL PASS`.
5. Clean up:
   ```
   vercel project rm wj-auth-probe
   ```
   Delete the temporary folder (secret, browsers, `.vercel`).

Playwright's WebKit build on Windows is not Safari. It does not enforce SameSite, so only real Safari counts for Apple browsers.
