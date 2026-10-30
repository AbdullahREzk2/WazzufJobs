<#
  Safe production rate-limit probe (Phase C0 post-deployment check 9).
  - Uses ONLY random, non-existent e-mail addresses (example.invalid). The login handler rejects unknown
    e-mails before any password check, so NO real account (including the administrator) can be locked out
    or have its failed-attempt counter changed. This is covered by the automated test
    "Rate_limit_probe_with_unknown_emails_never_touches_a_real_account_lockout".
  - Side effect: the CALLER'S IP is throttled on /api/Auth/* for up to 60 seconds afterwards. Run it from a
    network you are not about to log in from, or wait 60 s before logging in.
  - If lifecycle.log shows remoteAddressClass=private/loopback, the host is behind a proxy and the limit is
    effectively shared by everyone: run the probe at a quiet time and expect all logins to pause for 60 s.
#>
param(
    [string]$Api = "https://wazzufjobs.runasp.net",
    [int]$Requests = 25,
    [int]$ExpectedLimit = 20
)

$codes = @()
for ($i = 1; $i -le $Requests; $i++) {
    $body = @{ email = "ratelimit-probe-$([guid]::NewGuid().ToString('N'))@example.invalid"; password = "Probe#NotAPassword1" } | ConvertTo-Json
    try {
        $r = Invoke-WebRequest -Uri "$Api/api/Auth/login" -Method Post -Body $body -ContentType "application/json" -UseBasicParsing -ErrorAction Stop
        $codes += [int]$r.StatusCode
    } catch {
        $codes += [int]$_.Exception.Response.StatusCode
    }
}

$first429 = [array]::IndexOf($codes, 429) + 1
Write-Host "Status codes: $($codes -join ', ')"
if ($first429 -gt 0 -and $first429 -gt $ExpectedLimit) {
    Write-Host "PASS: first 429 at request $first429 (limit $ExpectedLimit/min)." -ForegroundColor Green
} elseif ($first429 -gt 0) {
    Write-Host "WARN: 429 already at request $first429 - limit may be shared (proxy) or other traffic consumed it." -ForegroundColor Yellow
} else {
    Write-Host "FAIL: no 429 received - rate limiting is not active." -ForegroundColor Red
}
Write-Host "Wait 60 seconds before logging in from this network."
