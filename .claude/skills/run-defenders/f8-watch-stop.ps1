# Stops the persistent F8 daemon.
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$PidFile = Join-Path $RepoRoot 'logs\f8-inbox\daemon.pid'
if (-not (Test-Path $PidFile)) {
    Write-Host '[f8-stop] No daemon pid file.'
    exit 0
}
$daemonPid = (Get-Content $PidFile -Raw).Trim()
Stop-Process -Id ([int]$daemonPid) -Force -ErrorAction SilentlyContinue
Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
Write-Host "[f8-stop] Stopped pid=$daemonPid"