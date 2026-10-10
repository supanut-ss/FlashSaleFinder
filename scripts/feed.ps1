<#
.SYNOPSIS
  Previews or imports a price file exported from a marketplace portal (Shopee, TikTok, Lazada).

.DESCRIPTION
  -Action Probe   Shows the columns matched and the first rows each file would import. Changes nothing.
  -Action Import  One import into the database named by ConnectionStrings__MariaDb.

  -Path is a .csv/.json/.jsonl file or a folder of them. If a column is matched wrongly, name it in an env file:
    FileFeeds__Shopee__Columns__Price=Sale Price
  Settings files are KEY=VALUE lines loaded into this process only (never printed).

.EXAMPLE
  ./scripts/feed.ps1 -Marketplace Shopee -Action Probe -Path C:\exports\shopee
  ./scripts/feed.ps1 -Marketplace Shopee -Action Import -Path C:\exports\shopee -EnvFile .env.production.local
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Shopee', 'TikTok', 'Lazada')]
    [string]$Marketplace,
    [Parameter(Mandatory = $true)]
    [ValidateSet('Probe', 'Import')]
    [string]$Action,
    [string]$Path = '',
    [string[]]$EnvFile = @()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

foreach ($file in $EnvFile) {
    $envPath = if ([System.IO.Path]::IsPathRooted($file)) { $file } else { Join-Path $root $file }
    if (-not (Test-Path -LiteralPath $envPath)) { throw "Settings file not found: $envPath" }
    foreach ($line in Get-Content -LiteralPath $envPath) {
        $text = $line.TrimStart([char]0xFEFF).Trim()   # tolerate a BOM
        if ($text -eq '' -or $text.StartsWith('#')) { continue }
        $index = $text.IndexOf('=')
        if ($index -lt 1) { continue }
        Set-Item -Path ("Env:" + $text.Substring(0, $index).Trim()) -Value $text.Substring($index + 1).Trim().Trim('"').Trim("'")
    }
}

if ($Path) {
    $resolved = if ([System.IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path (Get-Location).Path $Path }
    Set-Item -Path "Env:FileFeeds__${Marketplace}__Path" -Value $resolved
}

$flag = if ($Action -eq 'Probe') { '--feed-probe' } else { '--feed-import' }
& dotnet run --project (Join-Path $root 'src/FlashSale.Api') --configuration Release --no-launch-profile -- $flag $Marketplace
exit $LASTEXITCODE
