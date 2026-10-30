<#
  Phase C0 post-deployment security checks (read-only; no data is modified; synthetic data only).
  Works in Windows PowerShell 5.1 and PowerShell 7+.

  Anonymous checks always run. Authenticated checks need a TEST candidate account (never the admin) and the
  ids of an application (and its job) that belong to ANOTHER account. The password is prompted for (hidden),
  used only for the login request and cleared; the access token is never printed or stored.

  Exit codes: 0 = all checks passed, 1 = at least one check failed, 2 = the script stopped on an error
  (network/TLS/runtime). An error never produces a PASS/FAIL line, so there are no misleading "HTTP 0" results.
#>
param(
    [string]$Api = "https://wazzufjobs.runasp.net",
    [int]$OtherApplicationId = 0,
    [int]$JobId = 0,
    # optional, for non-interactive use; otherwise e-mail and password are prompted for
    [System.Management.Automation.PSCredential]$Credential
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
trap {
    Write-Host ("[STOPPED] {0}" -f $_.Exception.Message) -ForegroundColor Yellow
    Write-Host "No result was recorded for the remaining checks. Fix the cause and run the script again."
    exit 2
}

# Windows PowerShell 5.1 does not load System.Net.Http by default and may not enable TLS 1.2
Add-Type -AssemblyName System.Net.Http
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$Api = $Api.TrimEnd('/')
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromSeconds(90)
$script:failures = 0
$script:token = $null

function Invoke-Api {
    param([string]$Method, [string]$Path, [System.Net.Http.HttpContent]$Content = $null, [switch]$Authenticated)

    $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::new($Method), "$Api$Path")
    if ($Content) { $request.Content = $Content }
    if ($Authenticated) {
        $request.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $script:token)
    }
    # GetAwaiter().GetResult() surfaces the real exception (not an AggregateException); network errors stop the script
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    try {
        [pscustomobject]@{
            Status = [int]$response.StatusCode
            Body   = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }
    finally { $response.Dispose(); $request.Dispose() }
}

function Check([string]$Name, [bool]$Ok, [string]$Detail) {
    if (-not $Ok) { $script:failures++ }
    $tag = if ($Ok) { "PASS" } else { "FAIL" }
    $color = if ($Ok) { "Green" } else { "Red" }
    Write-Host ("[{0}] {1} - {2}" -f $tag, $Name, $Detail) -ForegroundColor $color
}

function Json([hashtable]$Value) {
    New-Object System.Net.Http.StringContent(($Value | ConvertTo-Json -Compress), [Text.Encoding]::UTF8, "application/json")
}

# ── anonymous checks ─────────────────────────────────────────────────────────
$r = Invoke-Api GET "/swagger/v1/swagger.json";   Check "Swagger spec hidden" ($r.Status -eq 404) "HTTP $($r.Status)"
$r = Invoke-Api GET "/index.html";                Check "Swagger UI hidden" ($r.Status -eq 404) "HTTP $($r.Status)"
$r = Invoke-Api GET "/hangfire";                  Check "Hangfire dashboard gone" ($r.Status -eq 404) "HTTP $($r.Status)"
$r = Invoke-Api GET "/hangfire?u=admin&p=2003";   Check "Hangfire hardcoded login gone" ($r.Status -eq 404) "HTTP $($r.Status)"
$r = Invoke-Api GET "/health";                    Check "Health" ($r.Status -eq 200 -and $r.Body -eq "Healthy") "HTTP $($r.Status) $($r.Body)"
$r = Invoke-Api POST "/api/Auth/google" (Json @{ idToken = "c0-synthetic-probe" })
Check "Google sign-in disabled (Google:ClientId not configured)" ($r.Status -eq 503 -and $r.Body -match 'Google\.Disabled') "HTTP $($r.Status)"

# ── authenticated checks ─────────────────────────────────────────────────────
if ($OtherApplicationId -le 0 -or $JobId -le 0) {
    Write-Host "Skipping authenticated checks (pass -OtherApplicationId and -JobId)."
}
else {
    if (-not $Credential) {
        $email = Read-Host "TEST candidate e-mail (not the admin)"
        $secure = Read-Host "TEST candidate password" -AsSecureString
        $Credential = New-Object System.Management.Automation.PSCredential($email, $secure)
    }

    $loginBody = Json @{ email = $Credential.UserName; password = $Credential.GetNetworkCredential().Password }
    $login = Invoke-Api POST "/api/Auth/login" $loginBody
    $loginBody.Dispose(); Remove-Variable loginBody, Credential -ErrorAction SilentlyContinue
    if ($login.Status -ne 200) { throw "Login of the test candidate failed (HTTP $($login.Status)). No authenticated check was run." }
    $script:token = ($login.Body | ConvertFrom-Json).token
    Remove-Variable login
    if (-not $script:token) { throw "Login response did not contain a token. No authenticated check was run." }

    try {
        $r = Invoke-Api GET "/api/Applications/$OtherApplicationId" -Authenticated
        Check "S1 other applicant's application detail blocked" ($r.Status -eq 403) "HTTP $($r.Status)"
        $r = Invoke-Api GET "/api/Applications/job/$JobId" -Authenticated
        Check "S1 job applicant list blocked" ($r.Status -eq 403) "HTTP $($r.Status)"
        $r = Invoke-Api GET "/api/Applications/$OtherApplicationId/cv" -Authenticated
        Check "S1 other applicant's CV blocked" ($r.Status -eq 403) "HTTP $($r.Status)"

        $r = Invoke-Api GET "/api/Applications/my-applications" -Authenticated
        Check "S4 no AI fields in candidate's applications" ($r.Status -eq 200 -and $r.Body -notmatch '(?i)aiScore|aiFeedback|isAIScored') "HTTP $($r.Status)"

        $r = Invoke-Api GET "/api/CV" -Authenticated
        if ($r.Status -eq 404) { Check "S3 CV metadata has no storage URL" $true "HTTP 404 (no CV on this account)" }
        else { Check "S3 CV metadata has no storage URL" ($r.Status -eq 200 -and $r.Body -notmatch '(?i)"url"|cloudinary') "HTTP $($r.Status)" }

        # synthetic PDF (no personal data); with uploads disabled the API must refuse before storing anything
        $form = New-Object System.Net.Http.MultipartFormDataContent
        $pdf = [Text.Encoding]::ASCII.GetBytes("%PDF-1.4`n% C0 synthetic probe - no personal data`n%%EOF`n")
        $part = New-Object System.Net.Http.ByteArrayContent(, $pdf)
        $part.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf")
        $form.Add($part, "file", "c0-synthetic-probe.pdf")
        $r = Invoke-Api POST "/api/CV" $form -Authenticated
        Check "CV uploads disabled (authenticated upload refused)" ($r.Status -eq 503 -and $r.Body -match 'CV\.UploadsDisabled') "HTTP $($r.Status)"
        $form.Dispose()
    }
    finally {
        $script:token = $null
    }
}

$client.Dispose()
if ($script:failures -gt 0) {
    Write-Host ("{0} check(s) FAILED." -f $script:failures) -ForegroundColor Red
    exit 1
}
Write-Host "All checks passed." -ForegroundColor Green
exit 0
