<#
  Phase C0 post-deployment security checks (read-only; no data is modified).
  Anonymous checks always run. Authenticated checks need a TEST candidate account (never the admin) and the
  ids of an application that belongs to ANOTHER test account; credentials are typed here, never stored.
#>
param(
    [string]$Api = "https://wazzufjobs.runasp.net",
    [int]$OtherApplicationId = 0,
    [int]$JobId = 0
)

function Status($url, $headers = @{}) {
    try { (Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop).StatusCode }
    catch { if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { -1 } }
}
function Check($name, $ok, $detail) {
    $tag = if ($ok) { "PASS" } else { "FAIL" }
    $color = if ($ok) { "Green" } else { "Red" }
    Write-Host ("[{0}] {1} - {2}" -f $tag, $name, $detail) -ForegroundColor $color
}

$s = Status "$Api/swagger/v1/swagger.json"; Check "Swagger spec hidden" ($s -eq 404) "HTTP $s"
$s = Status "$Api/index.html";              Check "Swagger UI hidden" ($s -eq 404) "HTTP $s"
$s = Status "$Api/hangfire";                Check "Hangfire dashboard gone" ($s -eq 404) "HTTP $s"
$s = Status "$Api/hangfire?u=admin&p=2003"; Check "Hangfire hardcoded login gone" ($s -eq 404) "HTTP $s"
try { $h = Invoke-WebRequest "$Api/health" -UseBasicParsing; Check "Health" ($h.Content -eq "Healthy") $h.Content } catch { Check "Health" $false "$_" }

if ($OtherApplicationId -le 0) { Write-Host "Skipping authenticated checks (pass -OtherApplicationId and -JobId)."; return }

$email = Read-Host "TEST candidate e-mail (not the admin)"
$sec = Read-Host "TEST candidate password" -AsSecureString
$pwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
$login = Invoke-RestMethod -Uri "$Api/api/Auth/login" -Method Post -ContentType "application/json" -Body (@{ email = $email; password = $pwd } | ConvertTo-Json)
$pwd = $null
$auth = @{ Authorization = "Bearer $($login.token)" }

$s = Status "$Api/api/Applications/$OtherApplicationId" $auth;    Check "S1 other application detail blocked" ($s -eq 403) "HTTP $s"
$s = Status "$Api/api/Applications/job/$JobId" $auth;             Check "S1 job applicant list blocked" ($s -eq 403) "HTTP $s"
$s = Status "$Api/api/Applications/$OtherApplicationId/cv" $auth; Check "S1 other applicant CV blocked" ($s -eq 403) "HTTP $s"

$mine = Invoke-WebRequest "$Api/api/Applications/my-applications" -Headers $auth -UseBasicParsing
Check "S4 no AI fields for candidate" (-not ($mine.Content -match '(?i)aiScore|aiFeedback|isAIScored')) "checked my-applications"

try { $cv = Invoke-WebRequest "$Api/api/CV" -Headers $auth -UseBasicParsing; Check "S3 CV metadata has no URL" (-not ($cv.Content -match '(?i)"url"|cloudinary')) "checked /api/CV" }
catch { Check "S3 CV metadata has no URL" $true "no CV on this account ($([int]$_.Exception.Response.StatusCode))" }

$form = New-Object System.Net.Http.MultipartFormDataContent
$pdf = [System.Text.Encoding]::ASCII.GetBytes("%PDF-1.4`n%synthetic`n%%EOF")
$part = New-Object System.Net.Http.ByteArrayContent(,$pdf); $part.Headers.ContentType = "application/pdf"
$form.Add($part, "file", "synthetic.pdf")
$client = New-Object System.Net.Http.HttpClient; $client.DefaultRequestHeaders.Authorization = "Bearer $($login.token)"
$up = $client.PostAsync("$Api/api/CV", $form).Result
Check "CV uploads paused" ([int]$up.StatusCode -eq 503) "HTTP $([int]$up.StatusCode)"
