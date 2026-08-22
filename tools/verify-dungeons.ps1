# =============================================================================
# verify-dungeons.ps1 - THE STANDING VERIFICATION FOR DUNGEONS
#   (owner ruling 2026-08-22: "yes make that the standing verification for dungeons")
#
# WHAT THIS ANSWERS, and why it is the bar:
#   "Content the player cannot REACH is not content."  The owner spent FIVE MONTHS
#   never once getting past room 1 of the legacy Healer's Cottage, and nothing
#   caught it - because nobody had ever asked "is this walkable?" in a form a
#   machine could answer. This asks it, per dungeon, every time.
#
# WHAT IT ACTUALLY DRIVES: AutoPilotDriver's AssertDungeonLoop phase, which
# performs the owner's own hand sequence headless -
#     hub -> resolve the portal AT RUNTIME -> tap the REAL Interact prompt ->
#     reach a scripted encounter -> win it -> survive the post-victory settle ->
#     TRY TO WALK -> return to the hub
# with five named assertions (A combat-capable, B on-navmesh after the fight,
# C the hero can ACTUALLY MOVE via the real D-pad seam, D the scene is not black,
# E the return). Assertion C is the one that matters most here: an on-mesh but
# PINNED hero passes B and fails C. That is "never past room 1", caught.
#
# ⛔ RUN THIS AFTER ANY COMPOSER OR BAKER CHANGE. The drift this exists to catch
# is real and already happened: HeroStartPoint_PlayerSpawn was added to
# DungeonBaker.cs on 2026-08-21 and EXACTLY ONE of five scenes was re-baked, so
# four dungeons silently lost their arrival safety net. A per-dungeon sweep after
# every baker change turns that from a five-month bug into a same-day one.
#
# ⚠ JUDGE THE MARKER, NEVER THE EXIT CODE. This repo's runners exit 0 on refusals
# and FAILs (CLAUDE.md s8; memory gates-report-success-without-proving-it).
# Marker ABSENCE on a fresh log is a FAILURE, not an unknown.
#
# Usage:
#   powershell -File tools\verify-dungeons.ps1                 # every dungeon
#   powershell -File tools\verify-dungeons.ps1 -Only dg_bonecrypt
#   powershell -File tools\verify-dungeons.ps1 -TimeoutMin 12
# =============================================================================
param(
    [string]$Only = '',                # verify one dungeon id instead of all
    [int]$TimeoutMin = 10,
    [string]$ExePath = 'Builds\Windows\DefendersOfTheRealm.exe'
)

$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

# ── The dungeon set is DERIVED from the composed layouts on disk, never typed.
# A hand-maintained list is the duplicated state this project keeps paying for:
# a dungeon added to the composer and forgotten here would be unverified and
# nothing would say so. Probe/rig/demo fixtures are excluded by name - they are
# tool scaffolding, not player content.
$layoutDir = Join-Path $repo 'Assets\Resources\Data\Canonical\dungeon-layouts'
$ids = Get-ChildItem $layoutDir -Filter *.json -ErrorAction SilentlyContinue |
       ForEach-Object { $_.BaseName } |
       Where-Object { $_ -notmatch 'probe|rig|catalog|demo' }

if ($Only -ne '') { $ids = @($Only) }

if (-not $ids -or $ids.Count -eq 0) {
    Write-Host "DUNGEON_VERIFY_FAIL - no composed dungeon layouts found under $layoutDir" -ForegroundColor Red
    Write-Host "  A sweep that verifies NOTHING must not look like a sweep that passed."
    exit 16
}

if (-not (Test-Path $ExePath)) {
    Write-Host "DUNGEON_VERIFY_FAIL - no Windows player at $ExePath" -ForegroundColor Red
    Write-Host "  The DungeonLoop probe drives a BUILT player. Run .\build-windows.ps1 first;"
    Write-Host "  a stale or absent exe cannot prove anything about today's composer output."
    exit 16
}

Write-Host "[verify-dungeons] $($ids.Count) dungeon(s): $($ids -join ', ')" -ForegroundColor Cyan
Write-Host "[verify-dungeons] exe = $ExePath   timeout = ${TimeoutMin}m per dungeon"
Write-Host ""

$results = @()
foreach ($id in $ids) {
    Write-Host "───────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "[verify-dungeons] $id" -ForegroundColor Cyan

    & .\run-autopilot-fleet.ps1 -ExePath $ExePath -Graphics `
        -Phases 'DungeonLoop' -Dungeon $id -TimeoutMin $TimeoutMin 2>&1 | Out-Null

    # Judge by the probe's own verdict line, harvested from the per-run logs.
    $logRoot = Join-Path $env:LOCALAPPDATA '..\LocalLow\DeNelle\Echoes of Elarion'
    $verdicts = @()
    if (Test-Path $logRoot) {
        $verdicts = Get-ChildItem $logRoot -Recurse -Filter 'player.log' -ErrorAction SilentlyContinue |
                    ForEach-Object { Select-String -LiteralPath $_.FullName -Pattern 'DUNGEON_LOOP_PROBE ::' -ErrorAction SilentlyContinue } |
                    ForEach-Object { $_.Line.Trim() }
    }

    $verdict = if ($verdicts.Count -gt 0) { $verdicts[-1] } else { '' }
    $pass = ($verdict -ne '') -and ($verdict -notmatch 'FAIL|entered=False|walked=False')

    if ($verdict -eq '') {
        # MARKER ABSENT IS A FAILURE, NOT AN UNKNOWN.
        Write-Host "  DUNGEON_VERIFY_FAIL $id - no DUNGEON_LOOP_PROBE verdict on a fresh log." -ForegroundColor Red
        Write-Host "    The probe did not reach its verdict. That is NOT the same as passing."
    } elseif ($pass) {
        Write-Host "  DUNGEON_VERIFY_OK $id" -ForegroundColor Green
        Write-Host "    $verdict" -ForegroundColor DarkGray
    } else {
        Write-Host "  DUNGEON_VERIFY_FAIL $id" -ForegroundColor Red
        Write-Host "    $verdict" -ForegroundColor DarkGray
    }
    $results += [pscustomobject]@{ Dungeon = $id; Pass = $pass; Verdict = $verdict }
    Write-Host ""
}

Write-Host "═══════════════════════════════════════════════" -ForegroundColor DarkGray
$ok = @($results | Where-Object { $_.Pass }).Count
$total = $results.Count
$results | ForEach-Object {
    $mark = if ($_.Pass) { 'OK  ' } else { 'FAIL' }
    $col  = if ($_.Pass) { 'Green' } else { 'Red' }
    Write-Host ("  {0}  {1}" -f $mark, $_.Dungeon) -ForegroundColor $col
}
Write-Host ""
if ($ok -eq $total) {
    Write-Host "DUNGEON_VERIFY_OK $ok/$total walkable end to end" -ForegroundColor Green
    exit 0
}
Write-Host "DUNGEON_VERIFY_FAIL $ok/$total - $($total - $ok) dungeon(s) are NOT walkable" -ForegroundColor Red
Write-Host "  Each failure names its own dungeon and assertion. Read the verdict line above,"
Write-Host "  then the per-run player.log - the probe's five assertions each carry their own text."
exit 16
