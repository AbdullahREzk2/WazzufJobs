<#
.SYNOPSIS
  Generates an idempotent EF Core migration script for review and deployment, prefixed with the required session
  options (session-options.sql). Phase 02; finding F-S2a-1.

.DESCRIPTION
  Writes a file only. It never connects to, migrates or changes any database. Deployment scripts are reviewed and
  then run by the operator with explicit approval (Phase 02 S2c and later).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\p02\db\New-MigrationScript.ps1 -OutputPath C:\temp\02_baseline_v2.sql
#>
param(
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$FromMigration = '0',
    [string]$ToMigration
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$apiDir = Join-Path $repoRoot 'SrcCode\WazzufJobs\WazzufJobs.API'
$efOutput = [IO.Path]::Combine([IO.Path]::GetTempPath(), "wj-ef-script-$([guid]::NewGuid().ToString('N')).sql")

try {
    Push-Location $apiDir
    $efArgs = @('tool', 'run', 'dotnet-ef', 'migrations', 'script', $FromMigration)
    if ($ToMigration) { $efArgs += $ToMigration }
    $efArgs += @('--idempotent', '--project', '..\WazzufJobs.DAL\WazzufJobs.DAL.csproj',
                 '--startup-project', 'WazzufJobs.API.csproj', '--output', $efOutput)
    # Windows PowerShell 5.1 turns a native command's stderr (e.g. build warnings) into terminating errors under
    # ErrorActionPreference=Stop, so success is judged by the exit code only
    $ErrorActionPreference = 'Continue'
    & dotnet @efArgs 2>&1 | ForEach-Object { "$_" } | Where-Object { $_ -notmatch 'warning NU1701' } | Out-Host
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($exitCode -ne 0) { throw "dotnet ef migrations script failed (exit code $exitCode)." }
}
finally {
    Pop-Location
}

$header = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'session-options.sql'))
$body = [IO.File]::ReadAllText($efOutput)
Remove-Item $efOutput -Force
[IO.File]::WriteAllText($OutputPath, $header + "`r`n" + $body, (New-Object Text.UTF8Encoding($false)))

$hash = (Get-FileHash $OutputPath -Algorithm SHA256).Hash
Write-Host "Written: $OutputPath"
Write-Host "SHA-256: $hash"
