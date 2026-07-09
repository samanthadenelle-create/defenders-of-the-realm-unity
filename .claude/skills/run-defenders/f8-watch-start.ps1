# Starts the persistent F8 daemon in a hidden background PowerShell (one per machine).
param([int]$PollSeconds = 5)

$Daemon = Join-Path $PSScriptRoot 'f8-watch-daemon.ps1'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox = Join-Path $RepoRoot 'logs\f8-inbox'
New-Item -ItemType Directory -Force -Path $Inbox | Out-Null

$PidFile = Join-Path $Inbox 'daemon.pid'
if (Test-Path $PidFile) {
    $old = (Get-Content $PidFile -Raw).Trim()
    $proc = Get-Process -Id ([int]$old) -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "[f8-start] Daemon already running (pid=$old). Inbox: $Inbox"
        exit 0
    }
}

$argList = "-NoProfile -ExecutionPolicy Bypass -File `"$Daemon`" -PollSeconds $PollSeconds"
Start-Process -FilePath 'powershell.exe' `
    -ArgumentList $argList `
    -WindowStyle Hidden `
    -WorkingDirectory $RepoRoot

Start-Sleep -Seconds 1
if (Test-Path $PidFile) {
    Write-Host "[f8-start] Daemon started. pid=$((Get-Content $PidFile -Raw).Trim())"
} else {
    Write-Host "[f8-start] Daemon launch requested (pid file pending)."
}
Write-Host "[f8-start] Inbox: $Inbox"
Write-Host "[f8-start] Claude: .cursor/rules/f8-auto-triage.mdc polls PING.json automatically."