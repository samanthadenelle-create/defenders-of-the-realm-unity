# =============================================================================
# push-and-build.ps1 - push the branch, then auto-kick a Windows exe build so a
# fresh build-faithful test exe is always ready. The "Win build on every push"
# workflow.
#
# WHY NO CLONE FOR WIN: the dev project lives on the Standalone platform and a
# Win64 build is ALSO Standalone - same platform, no Library thrash - so the exe
# builds fast right in the main project. (The clone model is for WebGL only,
# which is a DIFFERENT platform and would thrash the dev Library; keeping WebGL
# in its own clone is what keeps this project permanently on Standalone, which is
# what makes these Win builds thrash-free.) Web builds stay on demand:
#   build-webgl-isolated.ps1 [-Ship]
#
# The push returns immediately; the Win build runs detached. build-windows.ps1
# REFUSES if a Unity editor is open (project lock, one license) - so if you are
# mid-playtest it simply no-ops; re-run build-windows.ps1 when the editor closes.
#
# Usage:  powershell -ExecutionPolicy Bypass -File .\push-and-build.ps1
#         powershell -ExecutionPolicy Bypass -File .\push-and-build.ps1 -NoBuild
#
# ASCII-only on purpose (Windows PowerShell 5.1 reads BOM-less files as ANSI).
# =============================================================================
param(
    [string]$Branch = 'feat/tower-core-loop',
    [switch]$NoBuild                    # push only, skip the Win build kick
)

$ErrorActionPreference = 'Stop'
$dev = $PSScriptRoot

Write-Host "[p&b] pushing $Branch ..."
git -C $dev push origin $Branch
if ($LASTEXITCODE -ne 0) { Write-Error "[p&b] push failed - not building."; exit 1 }

if ($NoBuild) { Write-Host "[p&b] pushed (-NoBuild: skipped the Win build)."; exit 0 }

$winScript = Join-Path $dev 'build-windows.ps1'
Start-Process powershell -ArgumentList @(
    '-ExecutionPolicy','Bypass','-File',$winScript
) -WindowStyle Hidden
Write-Host "[p&b] pushed + Win exe build kicked (detached, main project, fast same-platform build)."
Write-Host "[p&b] no-ops if a Unity editor is open. Result: Builds\Windows\DefendersOfTheRealm.exe (log: Builds\build.log)"
exit 0
