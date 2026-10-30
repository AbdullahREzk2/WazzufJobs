<#
  READ-ONLY comparison of a downloaded copy of the server site folder with the C0 package manifest.
  Produces a review list; it deletes nothing. See "Obsolete file cleanup plan" in the deployment runbook.
#>
param(
    [Parameter(Mandatory)] [string]$ServerCopy,          # folder with the downloaded site files
    [Parameter(Mandatory)] [string]$PackageManifest      # backend-package-manifest.txt (sha256  relative/path)
)

$protected = @('^web\.config$', '^appsettings.*\.json$', '^App_Data[\\/]', '^logs[\\/]', '^\.well-known[\\/]', '^app_offline\.htm$')
$packageFiles = Get-Content $PackageManifest | ForEach-Object { ($_ -split '\s+', 2)[1].TrimStart('.', '/', '\').Replace('/', '\') }
$serverFiles = Get-ChildItem -Recurse -File $ServerCopy | ForEach-Object { $_.FullName.Substring((Resolve-Path $ServerCopy).Path.Length).TrimStart('\') }

$report = foreach ($f in $serverFiles) {
    $class = if ($packageFiles -contains $f) { 'IN-PACKAGE' }
             elseif ($protected | Where-Object { $f -match $_ }) { 'PROTECTED (never delete)' }
             elseif ($f -match '\.(dll|exe|pdb|deps\.json|runtimeconfig\.json)$') { 'OBSOLETE-CANDIDATE (review)' }
             else { 'UNKNOWN (keep unless proven unused)' }
    [pscustomobject]@{ Class = $class; File = $f }
}
$report | Sort-Object Class, File | Format-Table -AutoSize
$report | Group-Object Class | Select-Object Name, Count | Format-Table -AutoSize
