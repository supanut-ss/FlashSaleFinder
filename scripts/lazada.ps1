<#
.SYNOPSIS
  Runs the one-off Lazada affiliate commands with settings read from env files.

.DESCRIPTION
  -Action Probe   One real feed request. Prints a summary and saves the raw response (-Output, default lazada-probe.json)
                  so the response format can be checked before importing.
  -Action Import  One import of the feed into the database named by ConnectionStrings__MariaDb.

  Settings are KEY=VALUE lines loaded into this process only (never printed):
    Lazada__Affiliate__AppKey / __AppSecret / __UserToken        from Integration > Open API in the affiliate portal
    Lazada__Affiliate__CategoryIds__0, __ProductIds__0, __PageSize, __MaxPages, __MaxTrackingLinks, __BaseUrl   (optional)
    ConnectionStrings__MariaDb                                    for Import (e.g. in .env.production.local)

.EXAMPLE
  ./scripts/lazada.ps1 -Action Probe -EnvFile .env.lazada
  ./scripts/lazada.ps1 -Action Import -EnvFile .env.lazada, .env.production.local
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Probe', 'Import')]
    [string]$Action,
    [string[]]$EnvFile = @('.env.lazada'),
    [string]$Output = 'lazada-probe.json'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

foreach ($file in $EnvFile) {
    $path = if ([System.IO.Path]::IsPathRooted($file)) { $file } else { Join-Path $root $file }
    if (-not (Test-Path -LiteralPath $path)) { throw "Settings file not found: $path" }
    foreach ($line in Get-Content -LiteralPath $path) {
        $text = $line.TrimStart([char]0xFEFF).Trim()   # tolerate a BOM
        if ($text -eq '' -or $text.StartsWith('#')) { continue }
        $index = $text.IndexOf('=')
        if ($index -lt 1) { continue }
        Set-Item -Path ("Env:" + $text.Substring(0, $index).Trim()) -Value $text.Substring($index + 1).Trim().Trim('"').Trim("'")
    }
}

$flag = if ($Action -eq 'Probe') { '--lazada-probe' } else { '--lazada-import' }
# dotnet runs from the project folder, so hand it an absolute path relative to where the user called this script.
$outputPath = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path (Get-Location).Path $Output }
# An explicit string array: a single string would be splatted one character at a time by Windows PowerShell 5.1.
[string[]]$extra = if ($Action -eq 'Probe') { $outputPath } else { @() }
& dotnet run --project (Join-Path $root 'src/FlashSale.Api') --configuration Release --no-launch-profile -- $flag @extra
exit $LASTEXITCODE
