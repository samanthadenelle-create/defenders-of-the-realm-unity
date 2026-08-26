# f8-device-bridge-stop.ps1 -- WO-1227. Stops the background device bridge started by
# f8-device-bridge-start.ps1. Leaves the desktop daemon (f8-watch-daemon.ps1) untouched.
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$PidFile  = Join-Path $RepoRoot 'logs\f8-inbox\device-bridge.pid'

if (-not (Test-Path $PidFile)) {
    Write-Host '[f8-device-bridge-stop] No pid file. Nothing running.'
    exit 0
}
$old = (Get-Content $PidFile -Raw).Trim()
if ($old) {
    $proc = Get-Process -Id ([int]$old) -ErrorAction SilentlyContinue
    if ($proc) { Stop-Process -Id ([int]$old) -Force -ErrorAction SilentlyContinue }
}
Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
Write-Host "[f8-device-bridge-stop] Stopped (pid=$old)."
