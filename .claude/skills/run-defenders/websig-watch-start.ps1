# Starts the persistent WEB trace watcher (the web half of §14) in a hidden background
# PowerShell. Idempotent - one per machine. Pairs with f8-watch-start.ps1 (local logs);
# BOTH feed the SAME logs/f8-inbox, so f8-check-inbox.ps1 covers desktop AND web.
#
# Reads real player traces out of Neon via the key-gated admin endpoint. (It does NOT grep
# `vercel logs` for [sig] - proven 2026-07-15 that the CLI returns only the summary line per
# request, never the [sig] lines, so such a watcher fires never. See the daemon header.)
param([int]$PollSeconds = 60)

$Daemon   = Join-Path $PSScriptRoot 'websig-watch-daemon.ps1'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
New-Item -ItemType Directory -Force -Path $Inbox | Out-Null

$PidFile = Join-Path $Inbox 'websig-daemon.pid'
if (Test-Path $PidFile) {
    $old = (Get-Content $PidFile -Raw).Trim()
    if (Get-Process -Id ([int]$old) -ErrorAction SilentlyContinue) {
        Write-Host "[websig-start] Already running (pid=$old). Inbox: $Inbox"
        exit 0
    }
}

# Hard prerequisites - fail LOUD rather than start a watcher that can never fire.
if (-not (Test-Path (Join-Path $RepoRoot '.admin-dash-key'))) {
    Write-Host '[websig-start] NO .admin-dash-key - cannot read the trace DB. Not started.'
    Write-Host '               Set ADMIN_DASH_KEY in Vercel env + save the value to .admin-dash-key.'
    exit 1
}
if (-not (Test-Path (Join-Path $RepoRoot 'Builds\admin-preview-url.txt'))) {
    Write-Host '[websig-start] WARNING: no Builds\admin-preview-url.txt - the daemon will idle until a'
    Write-Host '               deploy writes one (preview URLs rotate, so the base is read per poll).'
}

Start-Process -FilePath 'powershell.exe' `
    -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$Daemon`" -PollSeconds $PollSeconds" `
    -WindowStyle Hidden -WorkingDirectory $RepoRoot

Start-Sleep -Seconds 2
if (Test-Path $PidFile) { Write-Host "[websig-start] Started. pid=$((Get-Content $PidFile -Raw).Trim())" }
else { Write-Host '[websig-start] Launch requested (pid file pending).' }
Write-Host "[websig-start] Source: Neon analytics_events (web_trace) via api/admin/db.js"
Write-Host "[websig-start] Inbox: $Inbox  - f8-check-inbox.ps1 sees web captures too."
