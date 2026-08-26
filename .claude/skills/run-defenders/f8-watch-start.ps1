# Starts the persistent F8 daemon in a hidden background PowerShell (one per machine).
param([int]$PollSeconds = 5)

$Daemon = Join-Path $PSScriptRoot 'f8-watch-daemon.ps1'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox = Join-Path $RepoRoot 'logs\f8-inbox'
New-Item -ItemType Directory -Force -Path $Inbox | Out-Null

# WO-1227 -- START THE DEVICE HALF TOO, BEFORE the desktop-daemon early-exit below.
# f8-watch-daemon.ps1 watches ONLY the desktop persistentDataPath. Nothing carried a capture off
# the phone, so on the one platform the owner actually plays the section 14 chain was severed at
# the first link (736 unread device entries, 8 of them her own FLAG presses). The bridge is an
# ADDITIVE SECOND PRODUCER into the SAME queue; it is a silent no-op with no phone attached, and
# it must be launched even when the desktop daemon is already running.
try {
    & (Join-Path $PSScriptRoot 'f8-device-bridge-start.ps1') | Out-Null
} catch {
    Write-Host "[f8-start] device bridge did not start: $($_.Exception.Message)"
}

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