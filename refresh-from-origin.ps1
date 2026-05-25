# =============================================================================
# refresh-from-origin.ps1 - morning sync. Pulls ALL pushed changes from
# origin/master and wipes the Unity Library so the editor reimports fresh.
#
#   powershell -ExecutionPolicy Bypass -File .\refresh-from-origin.ps1
#   powershell -ExecutionPolicy Bypass -File .\refresh-from-origin.ps1 -Full
#
# What it does:
#   1. Refuses to run while the Unity editor is open (a Library wipe while open
#      corrupts the project). Close Unity first.
#   2. git fetch; STASHES any uncommitted tracked changes (Unity reimport churn)
#      to a timestamped stash as a safety net -- nothing is silently lost -- then
#      hard-resets master to origin/master for an exact, full sync.
#   3. Wipes the Library reimport caches (default) or the WHOLE Library (-Full)
#      plus stale Builds, so the next Unity open does a clean reimport that picks
#      up every code / asset / shader change pushed overnight.
#   4. Prints the new HEAD + recent commits.
#
# After it finishes: open the project in Unity 6000.4.8f1 -- the first open after
# a wipe takes a few minutes while it reimports.
#
# NOTE: keep this file ASCII-only (Windows PowerShell 5.1 reads BOM-less files as
# ANSI; non-ASCII chars corrupt and break parse).
# =============================================================================

param(
    [switch]$Full  # wipe the ENTIRE Library (truly-fresh full reimport, slower) instead of just the caches
)

$ErrorActionPreference = 'Stop'
$proj = $PSScriptRoot
Set-Location $proj

# --- 0) Don't wipe Library while the editor is open ---------------------------
if (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue) {
    Write-Error "Unity editor is running. Close it first, then re-run (wiping Library while open corrupts the project)."
    exit 3
}

# --- 1) Sync to origin/master -------------------------------------------------
Write-Host "[refresh] Fetching origin..."
git fetch origin --prune

# Preserve uncommitted TRACKED changes (reimport churn) in a stash so nothing is
# lost; the hard reset then matches origin exactly. (No -u: untracked files such
# as the WORK_ORDER_*.md / *.png evidence are left in place.)
$dirty = git status --porcelain --untracked-files=no
if ($dirty) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    Write-Host "[refresh] Stashing local tracked changes -> 'pre-refresh-$stamp' (recover with: git stash list ; git stash pop)."
    git stash push -m "pre-refresh-$stamp" | Out-Null
}

Write-Host "[refresh] Resetting master to origin/master..."
git checkout master
git reset --hard origin/master

# --- 2) Wipe Library for a fresh reimport -------------------------------------
if ($Full) {
    Write-Host "[refresh] -Full: wiping the ENTIRE Library (full reimport, slower)..."
    foreach ($t in 'Library','Builds','Temp\UnityLockfile') {
        $p = Join-Path $proj $t
        if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "  removed $t" }
    }
} else {
    Write-Host "[refresh] Wiping Library reimport caches (recompile + reimport changed assets)..."
    $targets = 'Library\ScriptAssemblies','Library\Bee','Library\PlayerDataCache',
               'Library\BuildPlayerData','Library\ShaderCache','Library\StateCache',
               'Builds','Temp\UnityLockfile'
    foreach ($t in $targets) {
        $p = Join-Path $proj $t
        if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "  removed $t" }
    }
}

# --- 3) Report ----------------------------------------------------------------
Write-Host ""
Write-Host "[refresh] Done. HEAD is now:"
git log --oneline -8
Write-Host ""
Write-Host "[refresh] Open the project in Unity 6000.4.8f1 - it will reimport fresh. (Use -Full for a complete Library rebuild if anything looks stale.)"
