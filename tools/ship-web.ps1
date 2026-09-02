# =============================================================================
# ship-web.ps1 - load the gitignored operator secrets, then hand off to the one
# sanctioned ship chain (tools\command-centre.ps1).
#
# WHY THIS FILE EXISTS: command-centre.ps1 reads VERCEL_TOKEN and DATABASE_URL
# from the ENVIRONMENT and never from an argument, so a secret is never written
# to a log or a process list. But shell state does not survive a freeze, a
# reboot, or a new agent turn - on 2026-09-02 a PC freeze lost an exported
# VERCEL_TOKEN and stranded a built, R2-verified WebGL payload undeployed.
# .env.local is gitignored (.gitignore:711 `.env*`) and already holds
# DATABASE_URL, so it is the durable home for both.
#
# This file adds NO gate, NO push and NO verify of its own. It loads secrets and
# delegates. Judge the run by the MARKERS on command-centre's fresh logs, never
# by an exit code (CLAUDE.md section 8).
#
# Usage:  powershell -NoProfile -File tools\ship-web.ps1 [-- <command-centre args>]
# =============================================================================
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $root '.env.local'

if (-not (Test-Path -LiteralPath $envFile)) {
    Write-Host "SHIP_WEB_REFUSED reason=ENV_LOCAL_MISSING path=$envFile"
    exit 20
}

# Parse KEY=VALUE. Skip comments and blanks. Strip one layer of matching quotes.
# Values are assigned into the process environment only - never echoed.
$loaded = New-Object System.Collections.Generic.List[string]
foreach ($line in Get-Content -LiteralPath $envFile) {
    $trimmed = $line.Trim()
    if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
    $split = $trimmed.IndexOf('=')
    if ($split -lt 1) { continue }
    $key = $trimmed.Substring(0, $split).Trim()
    $value = $trimmed.Substring($split + 1).Trim()
    if ($value.Length -ge 2 -and
        (($value.StartsWith('"') -and $value.EndsWith('"')) -or
         ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    if ($value -eq '') { continue }
    [Environment]::SetEnvironmentVariable($key, $value, 'Process')
    $loaded.Add($key)
}

# Report only the NAMES the chain requires, and only whether they are present.
foreach ($required in @('VERCEL_TOKEN', 'DATABASE_URL')) {
    $present = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($required))
    Write-Host ("SHIP_WEB_SECRET {0} present={1}" -f $required, $present)
    if (-not $present) {
        Write-Host "SHIP_WEB_REFUSED reason=${required}_MISSING_FROM_ENV_LOCAL"
        exit 20
    }
}
Write-Host ("SHIP_WEB_ENV_OK keys={0}" -f $loaded.Count)

& (Join-Path $PSScriptRoot 'command-centre.ps1') @args
exit $LASTEXITCODE
