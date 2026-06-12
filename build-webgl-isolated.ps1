# =============================================================================
# build-webgl-isolated.ps1 - build (and optionally ship) the WebGL player from a
# PERSISTENT SIBLING worktree so the heavy IL2CPP/WebGL build never touches the
# dev project's editor or Library.
#
# WHY THIS EXISTS
#   The dev project keeps ONE Library import-cache tied to the ACTIVE platform.
#   Switching Standalone (dev) <-> WebGL (build) forces a full project re-import
#   every time (the multi-minute stall). A second working copy with its own
#   Library permanently on WebGL removes that thrash: dev stays on Standalone,
#   this clone stays on WebGL, and the build runs from origin while you keep
#   working. Keep it WARM - do NOT delete it between builds (a cold clone means a
#   full re-import + cold IL2CPP every time, and loses the synced art packs).
#
#   The build clone is a CONSUMER of origin: it fetches the latest pushed commit,
#   builds, and deploys the artifact. It never commits/pushes code.
#
# WHAT IT DOES (idempotent)
#   1. Ensure a detached-HEAD worktree at <BuildRoot> (default ..\defenders-webgl-build).
#      (Detached because git forbids checking out the same branch in two worktrees;
#       a build consumer at a fixed commit wants detached HEAD anyway.)
#   2. git fetch + hard-reset the worktree to origin/<Branch> (exact pushed code).
#   3. Sync the GITIGNORED art packs from the dev project into the worktree
#      (Models / polyperfect / TripoStructures / Structures ...). A fresh checkout
#      lacks these -> black/magenta art (the "fresh clone missing Models" trap).
#   4. Headless WebGL build into <BuildRoot>\Builds\WebGL (its own warm Library).
#   5. -Ship: butler push the output to itch.
#
# LICENSE NOTE (read once): a Unity Personal license generally runs ONE editor at
#   a time. By default this script REFUSES to launch if any Unity is already
#   running, so it will not fight your open dev editor for the license. Pass
#   -AllowConcurrent only if you have confirmed your license tolerates two editors
#   on this machine. For true hands-off concurrent builds, move to CI
#   (game-ci/unity-builder) - that is the only way to fully decouple from the laptop.
#
# USAGE
#   powershell -ExecutionPolicy Bypass -File .\build-webgl-isolated.ps1
#   powershell -ExecutionPolicy Bypass -File .\build-webgl-isolated.ps1 -Ship
#   powershell -ExecutionPolicy Bypass -File .\build-webgl-isolated.ps1 -Branch feat/x -Ship
#
# ASCII-only on purpose (Windows PowerShell 5.1 reads BOM-less files as ANSI;
# non-ASCII chars corrupt and break the parse).
# =============================================================================
param(
    [string]$Branch    = 'feat/tower-core-loop',
    [string]$BuildRoot = '',                                   # default: sibling of the dev repo
    [switch]$Ship,                                             # also butler-push to itch
    [string]$Target    = 'denellestudios/defenders-of-the-realm-defend-the-tower',
    [string]$Channel   = 'html5',
    [switch]$Brotli,                                           # default builds itch-safe uncompressed
    [switch]$AllowConcurrent,                                  # launch even if a Unity is already running
    [int]$TimeoutMin   = 90
)

$ErrorActionPreference = 'Stop'
$dev = $PSScriptRoot
if (-not $BuildRoot) { $BuildRoot = Join-Path (Split-Path $dev -Parent) 'defenders-webgl-build' }
$hubEditors = 'C:\Program Files\Unity\Hub\Editor'
$pinned     = '6000.4.8f1'

function Step($m) { Write-Host "[iso] $m" }

# --- 0) Guard: do not fight the dev editor for the license --------------------
if (-not $AllowConcurrent -and (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue)) {
    Write-Error "[iso] A Unity editor is running. Close it, or pass -AllowConcurrent if your license allows two editors. (See LICENSE NOTE in the header.)"
    exit 3
}

# --- 1) Locate Unity ----------------------------------------------------------
$candidates = Get-ChildItem $hubEditors -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName 'Editor\Unity.exe') }
if (-not $candidates) { Write-Error "[iso] No Unity editor under '$hubEditors'."; exit 1 }
$chosen = $candidates | Where-Object { $_.Name -eq $pinned } | Select-Object -First 1
if (-not $chosen) { $chosen = $candidates | Where-Object { $_.Name -like '6000.*' } | Sort-Object Name -Descending | Select-Object -First 1 }
if (-not $chosen) { $chosen = $candidates | Sort-Object Name -Descending | Select-Object -First 1 }
$unity = Join-Path $chosen.FullName 'Editor\Unity.exe'
$wgl   = Join-Path $chosen.FullName 'Editor\Data\PlaybackEngines\WebGLSupport'
if (-not (Test-Path $wgl)) { Write-Warning "[iso] WebGL Build Support module NOT found at $wgl - build will fail. Install it via Unity Hub." }
Step "editor=$($chosen.Name)  buildRoot=$BuildRoot  branch=$Branch"

# --- 2) Ensure + refresh the worktree ----------------------------------------
git -C $dev fetch origin $Branch --quiet
if (-not (Test-Path (Join-Path $BuildRoot '.git'))) {
    Step "creating warm worktree (first run - the initial import + IL2CPP build will be long)..."
    git -C $dev worktree add --detach $BuildRoot "origin/$Branch"
    if ($LASTEXITCODE -ne 0) { Write-Error "[iso] worktree add failed."; exit 1 }
} else {
    Step "refreshing existing worktree to origin/$Branch ..."
}
# Reset tracked files to the pushed commit; do NOT 'git clean -x' (would nuke the
# warm Library and the synced art packs). Clean only tracked-but-stray files.
git -C $BuildRoot checkout --detach --quiet
git -C $BuildRoot reset --hard "origin/$Branch" --quiet
$head = (git -C $BuildRoot rev-parse --short HEAD).Trim()
Step "worktree at $head"

# --- 3) Sync the gitignored art packs from dev -> worktree --------------------
# These are gitignored (owner-imported) and absent from a fresh checkout; without
# them the WebGL build ships black/magenta art.
$packs = @(
    'Assets\Models',
    'Assets\polyperfect',
    'Assets\Art\TripoStructures',
    'Assets\Resources\Structures',
    'Assets\Spells Pack',
    'Assets\Lana Studio'
)
foreach ($p in $packs) {
    $src = Join-Path $dev $p
    if (-not (Test-Path $src)) { continue }
    $dst = Join-Path $BuildRoot $p
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    Step "sync $p ..."
    robocopy $src $dst /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
    # carry the folder's own .meta (sibling file, not copied by the dir mirror)
    $m = "$src.meta"; if (Test-Path $m) { Copy-Item $m "$dst.meta" -Force }
}

# --- 4) Headless WebGL build --------------------------------------------------
$outDir = Join-Path $BuildRoot 'Builds\WebGL'
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
$log = Join-Path $BuildRoot 'Builds\webgl-build.log'
New-Item -ItemType Directory -Force -Path (Join-Path $BuildRoot 'Builds') | Out-Null
if (Test-Path $log) { Remove-Item $log -Force }

$args = @('-batchmode','-quit','-projectPath',$BuildRoot,'-buildTarget','WebGL',
          '-executeMethod','DeNelle.Editor.WebGLBuild.BuildWebGL','-logFile',$log)
if (-not $Brotli) { $args += '-noBrotli' }   # itch-safe uncompressed (size held down by WO-408 textures)
Step "launching WebGL batchmode (first IL2CPP compile can take 30-60 min)..."
& $unity @args | Out-Null

# Unity forks on launch; poll for the real editor to exit.
$deadline = (Get-Date).AddMinutes($TimeoutMin)
Start-Sleep -Seconds 6
while ((Get-Date) -lt $deadline) {
    if (-not (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue)) {
        Start-Sleep -Seconds 4
        if (-not (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue)) { break }
    }
    Start-Sleep -Seconds 15
}
$lock = Join-Path $BuildRoot 'Temp\UnityLockfile'
if (Test-Path $lock) { Remove-Item $lock -Force -ErrorAction SilentlyContinue }

$index = Join-Path $outDir 'index.html'
if (-not (Test-Path $index)) {
    Write-Error "[iso] BUILD FAILED - no index.html. Last 50 log lines:"
    if (Test-Path $log) { Get-Content $log -Tail 50 }
    exit 1
}
$mb = [math]::Round(((Get-ChildItem $outDir -Recurse -File | Measure-Object Length -Sum).Sum)/1MB,1)
Step "SUCCESS -> $index  (~$mb MB on disk)"

# --- 5) Ship to itch ----------------------------------------------------------
if ($Ship) {
    $butler = (Get-Command butler.exe -ErrorAction SilentlyContinue).Source
    if (-not $butler) { $fb = Join-Path $env:USERPROFILE 'butler\butler.exe'; if (Test-Path $fb) { $butler = $fb } }
    if (-not $butler) { Write-Error "[iso] butler not found - build is at $outDir; run 'butler login' then push manually."; exit 2 }
    Step "butler push -> ${Target}:${Channel} ..."
    & $butler push $outDir "${Target}:${Channel}"
    if ($LASTEXITCODE -ne 0) { Write-Error "[iso] PUSH FAILED. Build is fine at $outDir; retry."; exit $LASTEXITCODE }
    Step "DONE - shipped. Status: $butler status ${Target}:${Channel}"
}
Step "worktree left WARM at $BuildRoot for the next build."
exit 0
