# Starts the persistent BUG REPORT watcher (WO-846 - the "lets us know to review
# it" half of the tester bug-report ruling) in a hidden background PowerShell.
# Idempotent - one per machine. Pairs with f8-watch-start.ps1 (local logs) and
# websig-watch-start.ps1 (web traces); ALL THREE feed the SAME logs/f8-inbox,
# so f8-check-inbox.ps1 covers desktop, web AND tester bug reports.
#
# Reads new bug_reports rows out of Neon via the key-gated admin endpoint
# (api/admin/db.js). NOTE: until view=bugreports is added there (see the TODO
# block in bugreport-watch.ps1), the daemon runs DEGRADED - it detects new rows
# via view=overview counts and pings without content.
param([int]$PollSeconds = 120)

$Daemon   = Join-Path $PSScriptRoot 'bugreport-watch.ps1'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
New-Item -ItemType Directory -Force -Path $Inbox | Out-Null

$PidFile = Join-Path $Inbox 'bugreport-daemon.pid'
if (Test-Path $PidFile) {
    $old = (Get-Content $PidFile -Raw).Trim()
    if (Get-Process -Id ([int]$old) -ErrorAction SilentlyContinue) {
        Write-Host "[bugreport-start] Already running (pid=$old). Inbox: $Inbox"
        exit 0
    }
}

# Hard prerequisites - fail LOUD rather than start a watcher that can never fire.
if (-not (Test-Path (Join-Path $RepoRoot '.admin-dash-key'))) {
    Write-Host '[bugreport-start] NO .admin-dash-key - cannot read bug_reports. Not started.'
    Write-Host '                  Set ADMIN_DASH_KEY in Vercel env + save the value to .admin-dash-key.'
    exit 1
}
if (-not (Test-Path (Join-Path $RepoRoot 'Builds\admin-preview-url.txt'))) {
    Write-Host '[bugreport-start] WARNING: no Builds\admin-preview-url.txt - the daemon will idle until'
    Write-Host '                  a deploy writes one (preview URLs rotate; the base is read per poll).'
}

Start-Process -FilePath 'powershell.exe' `
    -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$Daemon`" -PollSeconds $PollSeconds" `
    -WindowStyle Hidden -WorkingDirectory $RepoRoot

Start-Sleep -Seconds 2
if (Test-Path $PidFile) { Write-Host "[bugreport-start] Started. pid=$((Get-Content $PidFile -Raw).Trim())" }
else { Write-Host '[bugreport-start] Launch requested (pid file pending).' }
Write-Host "[bugreport-start] Source: Neon bug_reports via api/admin/db.js"
Write-Host "[bugreport-start] Inbox: $Inbox  - f8-check-inbox.ps1 sees bug reports too."
