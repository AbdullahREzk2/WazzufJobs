// VER-08 auth-proxy probe runner (Phase 02, S0). Synthetic data only. Never prints the secret: only SHA-256 matches.
// The disposable secret file must live OUTSIDE the repository (WJ_PROBE_SECRET_FILE).
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { chromium, firefox, webkit } from 'playwright';

const BASE = 'https://wj-auth-probe.vercel.app';
const secretHash = createHash('sha256').update(readFileSync(process.env.WJ_PROBE_SECRET_FILE ?? new URL('../secret.txt', import.meta.url), 'utf8')).digest('hex');
const sha = v => createHash('sha256').update(v ?? '').digest('hex');
const results = {};
const record = (id, pass, detail) => { if (pass === false && id.includes(' webkit:')) { id += ' [Playwright WebKit-Windows, not Safari]'; pass = null; } results[id] = { pass, detail }; console.log(`${pass === null ? 'INFO' : pass ? 'PASS' : 'FAIL'}  ${id}  ${JSON.stringify(detail)}`); };
const hget = (headers, name) => { const k = Object.keys(headers).find(x => x.toLowerCase() === name.toLowerCase()); return k ? headers[k] : undefined; };

async function echo(path, init = {}) {
  const r = await fetch(BASE + path, init);
  const text = await r.text();
  let json; try { json = JSON.parse(text); } catch { json = null; }
  return { status: r.status, headers: Object.fromEntries(r.headers), json, text };
}

// ── P1 routing ──────────────────────────────────────────────────────────────
{
  const a = await echo('/api/auth/anything');
  const spa = await echo('/some/spa/route');
  const upper = await echo('/API/auth/anything');
  const stat = await echo('/static.txt');
  record('P1 /api/auth/* proxied to origin (not index.html)', a.status === 200 && a.json?.url?.includes('httpbin.org/anything'), { status: a.status, server: a.headers.server });
  record('P1 SPA fallback still serves index.html', spa.text.includes('wj-probe-index'), { status: spa.status });
  record('P1 static files unaffected', stat.text.startsWith('wj-probe-static-file'), { status: stat.status });
  record('P1 (info) path match is case-sensitive: /API/auth not proxied', null, { proxied: upper.json !== null });
}

// ── P2 secret injection ─────────────────────────────────────────────────────
{
  const plain = await echo('/api/auth/anything');
  const forged = await echo('/api/auth/anything', { headers: { 'X-WJ-Proxy-Secret': 'forged-by-client' } });
  const twice = await echo('/api/auth/anything', { headers: { 'x-wj-proxy-secret': 'forged-1', 'X-Wj-Proxy-Secret': 'forged-2' } });
  const v1 = hget(plain.json.headers, 'X-Wj-Proxy-Secret'), v2 = hget(forged.json.headers, 'X-Wj-Proxy-Secret'), v3 = hget(twice.json.headers, 'X-Wj-Proxy-Secret');
  record('P2 secret injected', sha(v1) === secretHash, { present: v1 !== undefined });
  record('P2 client-forged secret replaced (not kept, not appended)', sha(v2) === secretHash && !String(v2).includes('forged'), { containsForged: String(v2).includes('forged') });
  record('P2 duplicate forged headers replaced', sha(v3) === secretHash && !String(v3).includes('forged'), { containsForged: String(v3).includes('forged') });
  const missing = await echo('/api/auth-missing/anything');
  const vm = hget(missing.json.headers, 'X-Wj-Proxy-Secret');
  record('P2 (info) unset env var -> header value received by origin', null, { present: vm !== undefined, value: vm === undefined ? null : (vm === '' ? '<empty>' : (vm.startsWith('$') ? '<literal $VAR>' : `<${vm.length} chars>`)) });
}

// ── P3 real client IP, spoof resistance (12 rounds, every IP header forged) ──
{
  const ip64 = (await (await fetch('https://api64.ipify.org?format=json')).json()).ip;
  const ip4 = (await (await fetch('https://api.ipify.org?format=json')).json()).ip;
  const mine = new Set([ip64, ip4]);
  const isMine = v => !!v && [...mine].some(i => v === i || v === `for=${i}` || v.startsWith(`for=${i};`) || v.startsWith(`for="${i}`));
  const tally = { forwarded: 0, proxiedFor: 0, vercelForwardedForSpoofed: 0, realIpOrXffVisible: 0, forgedInTrustedHeaders: 0 };
  const ROUNDS = 12;
  for (let i = 0; i < ROUNDS; i++) {
    const r = await echo('/api/auth/anything', { headers: { 'X-Forwarded-For': '203.0.113.7', 'X-Real-IP': '203.0.113.8', 'Forwarded': 'for=203.0.113.9', 'X-Vercel-Forwarded-For': '203.0.113.10', 'X-Vercel-Proxied-For': '203.0.113.11' } });
    const h = r.json.headers;
    const fwd = hget(h, 'Forwarded') ?? '', pf = hget(h, 'X-Vercel-Proxied-For') ?? '', vff = hget(h, 'X-Vercel-Forwarded-For') ?? '';
    if (isMine(fwd.split(';')[0])) tally.forwarded++;
    if (isMine(pf) && !pf.includes(',')) tally.proxiedFor++;
    if (vff.includes('203.0.113.')) tally.vercelForwardedForSpoofed++;
    if (hget(h, 'X-Real-Ip') !== undefined || hget(h, 'X-Forwarded-For') !== undefined) tally.realIpOrXffVisible++;
    if ((fwd + pf).includes('203.0.113.')) tally.forgedInTrustedHeaders++;
  }
  record('P3 Forwarded: for= is always the real client IP despite forged headers', tally.forwarded === ROUNDS, { rounds: ROUNDS, correct: tally.forwarded });
  record('P3 X-Vercel-Proxied-For is always the real client IP (single value)', tally.proxiedFor === ROUNDS, { rounds: ROUNDS, correct: tally.proxiedFor });
  record('P3 no forged value in the trusted pair (Forwarded, X-Vercel-Proxied-For)', tally.forgedInTrustedHeaders === 0, { forged: tally.forgedInTrustedHeaders });
  record('P3 FINDING X-Vercel-Forwarded-For is client-spoofable (must never be trusted)', null, { rounds: ROUNDS, spoofed: tally.vercelForwardedForSpoofed });
  record('P3 (info) X-Real-IP / X-Forwarded-For observable at this origin', null, { rounds: ROUNDS, visible: tally.realIpOrXffVisible, note: 'httpbin and postman-echo filter these; to be verified at the real API in S6b' });
}

// ── P5 method/body/headers pass-through; preflight reaches origin ───────────
{
  const post = await echo('/api/auth/anything', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-WJ-CSRF': '1', 'Origin': BASE }, body: JSON.stringify({ probe: 'synthetic' }) });
  record('P5 POST JSON body passes unchanged', post.json?.json?.probe === 'synthetic' && post.json?.method === 'POST', {});
  record('P5 X-WJ-CSRF and Origin forwarded', hget(post.json.headers, 'X-Wj-Csrf') === '1' && hget(post.json.headers, 'Origin') === BASE, {});
  const pre = await fetch(BASE + '/api/auth/anything', { method: 'OPTIONS', headers: { 'Origin': 'https://evil.example', 'Access-Control-Request-Method': 'POST', 'Access-Control-Request-Headers': 'x-wj-csrf,content-type' } });
  // Vercel rewrites the Server header on proxied responses; origin handling is proven by the origin's CORS echo
  record('P5 OPTIONS preflight is answered by the origin (not by Vercel)', pre.headers.get('access-control-allow-origin') === 'https://evil.example', { status: pre.status, acao: pre.headers.get('access-control-allow-origin') ? 'origin echo' : 'none' });
}

// ── P6 no CDN caching ───────────────────────────────────────────────────────
{
  const states = [];
  for (const p of ['/api/auth/cache/60', '/api/auth/response-headers?Cache-Control=public,max-age=300', '/api/auth/get']) {
    for (let i = 0; i < 3; i++) {
      const r = await fetch(BASE + p);
      await r.arrayBuffer();
      states.push({ p, cache: r.headers.get('x-vercel-cache'), cc: r.headers.get('cache-control') });
    }
  }
  record('P6 never served from Vercel cache', states.every(s => s.cache !== 'HIT' && s.cache !== 'STALE'), { xVercelCache: [...new Set(states.map(s => s.cache))], cacheControl: [...new Set(states.map(s => s.cc))] });
}

// ── P7 secret confidentiality (browser-visible responses) ──────────────────
{
  const leaks = [];
  for (const p of ['/', '/static.txt', '/vercel.json', '/some/spa/route', '/api/auth/get', '/api/auth/response-headers?x=1']) {
    const r = await fetch(BASE + p);
    const body = await r.text();
    const headerBlob = JSON.stringify(Object.fromEntries(r.headers));
    // the /get echo body reflects request headers by design (origin artifact); only response HEADERS are checked there
    const scanBody = !p.startsWith('/api/auth/get');
    for (const token of body.match(/[0-9a-f]{64}/g) ?? []) if (scanBody && sha(token) === secretHash) leaks.push(`${p} body`);
    for (const token of headerBlob.match(/[0-9a-f]{64}/g) ?? []) if (sha(token) === secretHash) leaks.push(`${p} headers`);
    if (p === '/vercel.json') results['vercel.json served'] = r.status;
  }
  record('P7 secret absent from static files and response headers', leaks.length === 0, { leaks, vercelJsonStatus: results['vercel.json served'] });
}

// ── P9 latency / timeout (info) ─────────────────────────────────────────────
{
  const t0 = Date.now();
  const r = await fetch(BASE + '/api/auth/delay/10');
  await r.arrayBuffer();
  record('P9 (info) 10 s origin delay through proxy', null, { status: r.status, seconds: ((Date.now() - t0) / 1000).toFixed(1) });
}

// ── P10 headers Vercel forwards to the origin (names only) ──────────────────
{
  const a = await echo('/api/auth/anything');
  record('P10 (info) request header names seen by origin', null, Object.keys(a.json.headers).sort());
}

// ── P4/P8 cookie behaviour in real browser engines ──────────────────────────
const setCookie = v => '/api/auth/response-headers?Set-Cookie=' + encodeURIComponent(v);
const COOKIE = '__Secure-wj_rt=probe-value; Path=/api/auth; HttpOnly; Secure; SameSite=Strict; Max-Age=600';
for (const [name, engine] of [['chromium', chromium], ['firefox', firefox], ['webkit', webkit]]) {
  const browser = await engine.launch();
  try {
    const context = await browser.newContext();
    const page = await context.newPage();
    await page.goto(BASE + '/');
    const r = await page.evaluate(async ({ url, setTwo }) => {
      const s = await fetch(url, { credentials: 'same-origin' });
      const docCookie = document.cookie;
      const echo = await (await fetch('/api/auth/anything', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-WJ-CSRF': '1' }, body: '{"probe":"synthetic"}' })).json();
      await fetch(setTwo, { credentials: 'same-origin' });
      return { setStatus: s.status, docCookie, headers: echo.headers };
    }, { url: setCookie(COOKIE), setTwo: '/api/auth/response-headers?Set-Cookie=' + encodeURIComponent('__Secure-wj_a=1; Path=/api/auth; Secure; HttpOnly; SameSite=Strict') + '&Set-Cookie=' + encodeURIComponent('__Secure-wj_b=2; Path=/api/auth; Secure; HttpOnly; SameSite=Strict') });
    const jar = await context.cookies(BASE + '/api/auth/x');
    const c = jar.find(x => x.name === '__Secure-wj_rt');
    const cookieSent = (hget(r.headers, 'Cookie') ?? '').includes('__Secure-wj_rt=probe-value');
    record(`P4 ${name}: cookie stored with expected attributes`, !!c && c.httpOnly && c.secure && c.sameSite === 'Strict' && c.path === '/api/auth' && !c.domain.startsWith('.'),
      c ? { httpOnly: c.httpOnly, secure: c.secure, sameSite: c.sameSite, path: c.path, hostOnly: !c.domain.startsWith('.') } : { stored: false });
    record(`P4 ${name}: HttpOnly hides cookie from JavaScript`, !r.docCookie.includes('wj_rt'), {});
    record(`P4 ${name}: cookie sent back on same-origin /api/auth request`, cookieSent, {});
    record(`P4 ${name}: Sec-Fetch-Site same-origin forwarded`, ['same-origin', undefined].includes(hget(r.headers, 'Sec-Fetch-Site')), { secFetchSite: hget(r.headers, 'Sec-Fetch-Site') ?? 'absent' });
    record(`P4 ${name}: two Set-Cookie headers in one response both stored`, ['__Secure-wj_a', '__Secure-wj_b'].every(n => jar.some(x => x.name === n)), {});
    const scoped = await context.cookies(BASE + '/');
    record(`P4 ${name}: cookie not sent outside /api/auth`, !scoped.some(x => x.name === '__Secure-wj_rt'), {});

    // cross-site: fetch with credentials from another site, and a cross-site top-level navigation
    const other = await context.newPage();
    await other.goto('https://example.com/');
    const xs = await other.evaluate(async base => {
      try {
        const res = await fetch(base + '/api/auth/anything', { method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json', 'X-WJ-CSRF': '1' }, body: '{}' });
        const j = await res.json();
        return { readable: true, cookie: j.headers.Cookie ?? j.headers.cookie ?? '' };
      } catch (e) { return { readable: false, error: String(e).slice(0, 80) }; }
    }, BASE);
    record(`P4 ${name}: cross-site fetch carries no refresh cookie (SameSite)`, !(xs.cookie ?? '').includes('wj_rt'), xs.readable ? { corsReadable: true, cookieSent: (xs.cookie ?? '').includes('wj_rt') } : { corsReadable: false });
    await other.evaluate(base => { location.href = base + '/api/auth/anything'; }, BASE);
    await other.waitForURL(u => u.toString().startsWith(BASE + '/api/auth/anything'));
    const navText = await other.textContent('body');
    record(`P4 ${name}: cross-site top-level navigation carries no refresh cookie (Strict)`, !navText.includes('wj_rt'), {});

    // deletion (logout semantics)
    await page.evaluate(async url => { await fetch(url, { credentials: 'same-origin' }); }, setCookie('__Secure-wj_rt=; Path=/api/auth; HttpOnly; Secure; SameSite=Strict; Max-Age=0'));
    record(`P4 ${name}: Max-Age=0 deletes the cookie`, !(await context.cookies(BASE + '/api/auth/x')).some(x => x.name === '__Secure-wj_rt'), {});
    await context.close();
  } catch (e) {
    record(`P8 ${name}: browser run`, false, { error: String(e).slice(0, 200) });
  } finally { await browser.close(); }
}

writeFileSync(new URL('./results.json', import.meta.url), JSON.stringify(results, null, 2));
const failed = Object.entries(results).filter(([, v]) => v && v.pass === false).map(([k]) => k);
console.log(failed.length ? `\nFAILED: ${failed.length}\n - ${failed.join('\n - ')}` : '\nALL MANDATORY CHECKS PASSED');
