# Stops the bug-report watcher daemon (see bugreport-watch-start.ps1).
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$PidFile  = Join-Path $RepoRoot 'logs\f8-inbox\bugreport-daemon.pid'

if (-not (Test-Path $PidFile)) { Write-Host '[bugreport-stop] Not running (no pid file).'; exit 0 }

$id = (Get-Content $PidFile -Raw).Trim()
$proc = Get-Process -Id ([int]$id) -ErrorAction SilentlyContinue
if ($proc) {
    Stop-Process -Id ([int]$id) -Force -ErrorAction SilentlyContinue
    Write-Host "[bugreport-stop] Stopped pid=$id."
} else {
    Write-Host "[bugreport-stop] pid=$id not alive; clearing stale pid file."
}
Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
