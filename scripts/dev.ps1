<#
.SYNOPSIS
  Runs the API in Development demo mode with the database chosen by -Database.

.DESCRIPTION
  -Database Memory   Sample data in memory (default). Nothing to start; data is lost on exit.
  -Database MariaDb  Sample data stored in the Compose MariaDB (and Redis when reachable).
               Reads MARIADB_PASSWORD, MARIADB_PORT and REDIS_PORT from the git-ignored .env file.

  Start the services first with `docker compose up -d`. Passwords are never printed.

.EXAMPLE
  ./scripts/dev.ps1 -Database MariaDb -Port 5080
#>
[CmdletBinding()]
param(
    [ValidateSet('Memory', 'MariaDb')]
    [string]$Database = 'Memory',
    [int]$Port = 5080,
    # MariaDB schema to use (default: MARIADB_DATABASE from .env, else flashsale).
    [string]$DatabaseName = '',
    [switch]$NoRedis,
    # Demo mode writes synthetic sample data. Required to point it at a database that is not on this machine.
    [switch]$AllowRemote,
    # Settings file to read instead of the repository .env.
    [string]$EnvFile = '',
    # Used by automated checks: leave the rate limiter wide open so repeated page loads are not throttled.
    [int]$RateLimit = 0
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Read-DotEnv([string]$path) {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        $text = $line.TrimStart([char]0xFEFF).Trim()   # files saved with a BOM would otherwise corrupt the first key
        if ($text -eq '' -or $text.StartsWith('#')) { continue }
        $index = $text.IndexOf('=')
        if ($index -lt 1) { continue }
        $values[$text.Substring(0, $index).Trim()] = $text.Substring($index + 1).Trim().Trim('"').Trim("'")
    }
    return $values
}

function Test-TcpPort([string]$hostName, [int]$port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $pending = $client.BeginConnect($hostName, $port, $null, $null)
        return $pending.AsyncWaitHandle.WaitOne(1500) -and $client.Connected
    } catch { return $false } finally { $client.Close() }
}

# Start from a clean slate so a previous shell's settings cannot leak into this run.
foreach ($name in 'Demo__Database', 'ConnectionStrings__MariaDb', 'ConnectionStrings__Redis', 'RateLimit__PermitLimit') {
    Remove-Item -Path "Env:$name" -ErrorAction SilentlyContinue
}
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Demo__Enabled = 'true'
if ($RateLimit -gt 0) { $env:RateLimit__PermitLimit = "$RateLimit" }

$cache = 'Memory'
if ($Database -eq 'MariaDb') {
    $envFile = if ($EnvFile) { $EnvFile } else { Join-Path $root '.env' }
    if (-not (Test-Path -LiteralPath $envFile)) {
        throw "Missing $envFile. Copy .env.example to .env and set local passwords."
    }
    $settings = Read-DotEnv $envFile
    # Either a ready-made ConnectionStrings__MariaDb entry, or the separate MARIADB_* keys.
    $connection = $settings['ConnectionStrings__MariaDb']
    if (-not $connection -and -not $settings['MARIADB_PASSWORD']) {
        throw 'Set ConnectionStrings__MariaDb or MARIADB_PASSWORD in the settings file.'
    }
    if ($connection) {
        $dbHost = if ($connection -match '(?i)(?:Server|Host|Data Source)=([^;]+)') { $Matches[1] } else { throw 'ConnectionStrings__MariaDb has no Server=.' }
        $dbPort = if ($connection -match '(?i)Port=(\d+)') { [int]$Matches[1] } else { 3306 }
        $dbName = if ($connection -match '(?i)(?:Database|Initial Catalog)=([^;]+)') { $Matches[1] } else { '' }
        if ($DatabaseName) {
            $connection = if ($dbName) { $connection -replace '(?i)((?:Database|Initial Catalog)=)[^;]+', "`${1}$DatabaseName" } else { "$connection;Database=$DatabaseName" }
            $dbName = $DatabaseName
        }
    } else {
        $dbName = if ($DatabaseName) { $DatabaseName } elseif ($settings['MARIADB_DATABASE']) { $settings['MARIADB_DATABASE'] } else { 'flashsale' }
        $dbHost = if ($settings['MARIADB_HOST']) { $settings['MARIADB_HOST'] } else { 'localhost' }
        $dbUser = if ($settings['MARIADB_USER']) { $settings['MARIADB_USER'] } else { 'flashsale' }
        $dbPort = if ($settings['MARIADB_PORT']) { [int]$settings['MARIADB_PORT'] } else { 3306 }
        $ssl = if ($settings['MARIADB_SSL_MODE']) { ";SslMode=$($settings['MARIADB_SSL_MODE'])" } else { '' }
        $connection = "Server=$dbHost;Port=$dbPort;Database=$dbName;User=$dbUser;Password=$($settings['MARIADB_PASSWORD'])$ssl"
    }
    $isLocal = @('localhost', '127.0.0.1', '::1') -contains $dbHost
    if (-not $isLocal -and -not $AllowRemote) {
        throw "Demo mode writes synthetic sample data, and the database host is $dbHost (not this machine). Pass -AllowRemote only if this is a scratch database you are happy to fill with sample rows."
    }
    if (-not (Test-TcpPort $dbHost $dbPort)) {
        $hint = if ($isLocal) { 'Start it with: docker compose up -d' } else { 'Check the host, port, firewall and that this machine is allowed to connect.' }
        throw "MariaDB is not reachable on ${dbHost}:$dbPort. $hint"
    }
    $env:Demo__Database = 'MariaDb'
    $env:ConnectionStrings__MariaDb = $connection

    $redisHost = if ($settings['REDIS_HOST']) { $settings['REDIS_HOST'] } else { 'localhost' }
    $redisPort = if ($settings['REDIS_PORT']) { [int]$settings['REDIS_PORT'] } else { 6379 }
    if (-not $NoRedis -and (Test-TcpPort $redisHost $redisPort)) {
        $env:ConnectionStrings__Redis = "${redisHost}:$redisPort"
        $cache = "Redis (${redisHost}:$redisPort)"
    }
    Write-Host "Database: MariaDB $dbName (${dbHost}:$dbPort)  Cache: $cache"
} else {
    Write-Host 'Database: in-memory sample data  Cache: memory'
}

if (-not (Test-Path -LiteralPath (Join-Path $root 'src/FlashSale.Api/wwwroot/index.html'))) {
    Write-Warning 'The web client is not built. Run: cd src/FlashSale.Web; npm ci; npm run build'
}

Write-Host "Starting http://localhost:$Port (Ctrl+C to stop)"
& dotnet run --project (Join-Path $root 'src/FlashSale.Api') --no-launch-profile --urls "http://localhost:$Port"
exit $LASTEXITCODE
