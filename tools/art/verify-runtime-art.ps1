<#
.SYNOPSIS
    Verify the CRITICAL runtime art fallbacks + committed People pack exist on disk,
    and warn about any gitignored source pack that has not been copied in.

.DESCRIPTION
    Authority: docs/PAIN_POINTS_2026-07-26.md  section 1.2 (RULING "Tracked runtime + zip travel").
    Manifest : tools/art/REQUIRED_PACKS.md

    The big character/environment packs are gitignored and travel by zip, not by `git pull`.
    A fresh clone must still RUN with DISTINCT enemies/NPCs off the tracked Resources/ fallback,
    else the build renders capsules ("Bryn is a pill"), untextured bodies, or magenta.

    This script is READ-ONLY. It does NOT download or import anything. It classifies each
    checked path into one of three tiers and prints a report:

      CRITICAL   - tracked runtime key that MUST exist on a bare clone. Missing => hard fail.
      COMMITTED  - the tracked People pack body/textures. Missing => hard fail (LFS not hydrated?).
      PACK       - gitignored source pack that travels by zip. Missing => WARN only (fallback covers).

    FAILS SOFT: exit code 1 if any CRITICAL or COMMITTED item is missing (a build would break),
    exit code 0 if only PACK warnings remain (game runs on the fallback). Warnings are never fatal.

.EXAMPLE
    pwsh tools/art/verify-runtime-art.ps1
    powershell -File tools\art\verify-runtime-art.ps1
#>

[CmdletBinding()]
param(
    # Treat gitignored-pack warnings as failures too (strict CI mode).
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'

# Resolve repo root = two levels up from tools/art/ (this script's folder).
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = Split-Path -Parent (Split-Path -Parent $scriptDir)

Write-Host ""
Write-Host "=== verify-runtime-art :: Defenders of the Realm ==="
Write-Host "Repo root: $repoRoot"
Write-Host "Ruling   : docs/PAIN_POINTS_2026-07-26.md 1.2  |  Manifest: tools/art/REQUIRED_PACKS.md"
Write-Host ""

# ---------------------------------------------------------------------------
# The checklist. Each entry: Tier, RelPath, Needed-by note, and MatchKind:
#   'Dir'     - directory must exist
#   'File'    - exact file must exist
#   'AnyPng'  - directory must exist AND contain at least one *.png (textures)
#   'AnyFbx'  - directory must exist AND contain at least one *.fbx/*.gltf (bodies)
# ---------------------------------------------------------------------------
$checks = @(
    # ---- CRITICAL: tracked AccuRig runtime fallback (the live cast on a bare clone) ----
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Enemies/Skeleton_Warrior.fbx';  Note='Hollow Warrior body (AccuRig)' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Enemies/Skeleton_Rogue.fbx';    Note='Hollow Skirmisher body' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Enemies/Skeleton_Mage.fbx';     Note='Hollow Caster body' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Enemies/Skeleton_Healer.fbx';   Note='Hollow Acolyte body' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Enemies/Skeleton_Golem.fbx';    Note='Hollow Brute/Golem body' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Enemies/Skeleton_Minion.fbx';   Note='Hollow Walker body' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Enemies/Necromancer.fbx';       Note='Necromancer of the Wound body' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Enemies/Boss_Dragon.prefab';    Note='Alduin/dragon boss prefab' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Enemies/Orc_Warrior.fbx';       Note='Orc family body' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Enemies/SkeletonHumanoid.controller'; Note='shared humanoid animator path' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/NPCs/NPC_Blacksmith.prefab';    Note='Blacksmith NPC' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/NPCs/NPC_Merchant.prefab';      Note='Merchant NPC' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/NPCs/NPC_Peasant_Mevina.prefab';Note='Peasant NPC (Mevina)' }
    @{ Tier='CRITICAL';  Kind='File'; Path='Assets/Resources/Heroes/KnightV3.fbx';           Note='hero body (CC/AccuRig)' }

    # ---- COMMITTED: the tracked People pack (LFS) - Bryn-class NPC bodies + their textures ----
    @{ Tier='COMMITTED'; Kind='File';   Path='Assets/Models/People/0_FighterClass_High_High_1024_LOD0.Fbx'; Note='FighterClass body (People pack)' }
    @{ Tier='COMMITTED'; Kind='AnyPng'; Path='Assets/Models/People/Blacksmith/Textures';     Note='Blacksmith textures' }
    @{ Tier='COMMITTED'; Kind='AnyPng'; Path='Assets/Models/People/Peasant/Textures';        Note='Peasant textures' }

    # ---- PACK: gitignored source packs (travel by zip) - WARN only, fallback covers ----
    @{ Tier='PACK'; Kind='AnyFbx'; Path='Assets/Models/KayKit/KayKit Skeletons 1.1/characters'; Note='KayKit Skeletons source bodies' }
    @{ Tier='PACK'; Kind='AnyFbx'; Path='Assets/Models/KayKit Adventurers 2.0/Characters';      Note='KayKit Adventurers troop/hero bodies' }
    @{ Tier='PACK'; Kind='AnyFbx'; Path='Assets/Models/KayKit/dungeon';                         Note='KayKit Dungeon Remastered geometry' }
    @{ Tier='PACK'; Kind='Dir';    Path='Assets/Models/People/textures';                        Note='People shared skin textures (the untextured-Bryn gap)' }
)

function Test-Entry {
    param($entry)
    $full = Join-Path $repoRoot $entry.Path
    switch ($entry.Kind) {
        'File'   { return (Test-Path -LiteralPath $full -PathType Leaf) }
        'Dir'    { return (Test-Path -LiteralPath $full -PathType Container) }
        'AnyPng' {
            if (-not (Test-Path -LiteralPath $full -PathType Container)) { return $false }
            return @(Get-ChildItem -LiteralPath $full -Filter *.png -File -ErrorAction SilentlyContinue).Count -gt 0
        }
        'AnyFbx' {
            if (-not (Test-Path -LiteralPath $full -PathType Container)) { return $false }
            $n = @(Get-ChildItem -LiteralPath $full -Recurse -Include *.fbx,*.gltf -File -ErrorAction SilentlyContinue).Count
            return $n -gt 0
        }
        default  { return $false }
    }
}

$missingCritical  = @()
$missingCommitted = @()
$missingPack      = @()

foreach ($c in $checks) {
    $ok = Test-Entry $c
    if ($ok) {
        Write-Host ("  [ OK ]   {0,-9} {1}" -f $c.Tier, $c.Path)
    }
    else {
        Write-Host ("  [MISS]   {0,-9} {1}   <- {2}" -f $c.Tier, $c.Path, $c.Note)
        switch ($c.Tier) {
            'CRITICAL'  { $missingCritical  += $c }
            'COMMITTED' { $missingCommitted += $c }
            'PACK'      { $missingPack       += $c }
        }
    }
}

Write-Host ""
Write-Host "--- summary ---"

if ($missingPack.Count -gt 0) {
    Write-Host ""
    Write-Host ("WARN: {0} gitignored source pack(s) not copied in (game runs on the tracked fallback):" -f $missingPack.Count)
    foreach ($m in $missingPack) {
        Write-Host ("   - {0}  ({1})" -f $m.Path, $m.Note)
    }
    Write-Host "   Fix: copy the pack in from the owner's zip / source folder (NOT git). See tools/art/REQUIRED_PACKS.md."
}

$hardMissing = $missingCritical.Count + $missingCommitted.Count

if ($hardMissing -gt 0) {
    Write-Host ""
    Write-Host ("FAIL: {0} tracked runtime asset(s) missing - a build would render pills/untextured/magenta:" -f $hardMissing)
    foreach ($m in ($missingCritical + $missingCommitted)) {
        Write-Host ("   - {0,-9} {1}  ({2})" -f $m.Tier, $m.Path, $m.Note)
    }
    if ($missingCommitted.Count -gt 0) {
        Write-Host "   Hint: COMMITTED misses often mean LFS did not hydrate -> run 'git lfs pull'."
    }
    Write-Host ""
    Write-Host "RESULT: RUNTIME-ART FAIL"
    exit 1
}

if ($Strict -and $missingPack.Count -gt 0) {
    Write-Host ""
    Write-Host "RESULT: RUNTIME-ART FAIL (strict mode: gitignored packs required)"
    exit 1
}

Write-Host ""
if ($missingPack.Count -gt 0) {
    Write-Host "RESULT: RUNTIME-ART OK (fallback intact; source packs missing but non-fatal)"
} else {
    Write-Host "RESULT: RUNTIME-ART OK (all tracked fallbacks + all packs present)"
}
exit 0
