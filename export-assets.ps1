# =============================================================================
# export-assets.ps1 - copy the LARGE, gitignored art into a staging folder you
# can ZIP and transfer to another machine (e.g. your laptop). Run on the PC that
# HAS the art (this one). Then zip the printed folder and send it over; on the
# laptop, clone/pull the repo and run import-assets.ps1 to drop the art back in.
#
#   powershell -ExecutionPolicy Bypass -File .\export-assets.ps1
#   (optional)  -OutDir "D:\some\folder"   to choose where the staging folder goes
#
# What it copies (these are gitignored, so git does NOT carry them):
#   Assets/Models/               - KayKit packs + Cathedral + Adventurers (~941 MB)
#   Assets/Art/TripoStructures/  - owner Tripo building models (~180 MB)
#   Assets/Resources/Structures/ - owner Tripo dungeon Portal (~30 MB)
#   + the three folders' own .meta files (so Unity keeps the same folder GUIDs)
#
# It PRESERVES the Assets/... path layout inside the staging folder, so
# import-assets.ps1 can put everything back exactly where it belongs (and the
# committed Village scene's GUID references resolve on the laptop).
#
# NOTE: Resources/Heroes (the hero FBX) is tracked in git via LFS, so it travels
# with the repo and is NOT included here.
#
# ASCII-only on purpose (Windows PowerShell 5.1 reads BOM-less files as ANSI).
# =============================================================================
param(
    [string]$OutDir = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'DOTR-assets-export')
)

$ErrorActionPreference = 'Stop'
$proj = $PSScriptRoot

$folders = @(
    'Assets\Models',
    'Assets\Art\TripoStructures',
    'Assets\Resources\Structures'
)
$metas = @(
    'Assets\Models.meta',
    'Assets\Art\TripoStructures.meta',
    'Assets\Resources\Structures.meta'
)

Write-Host "[export] staging folder: $OutDir"
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

foreach ($f in $folders) {
    $src = Join-Path $proj $f
    if (-not (Test-Path $src)) { Write-Warning "  MISSING (skipped): $f"; continue }
    $dst = Join-Path $OutDir $f
    New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
    robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
    Write-Host "  copied $f"
}
foreach ($m in $metas) {
    $src = Join-Path $proj $m
    if (Test-Path $src) {
        $dst = Join-Path $OutDir $m
        New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
        Copy-Item $src $dst -Force
        Write-Host "  copied $m"
    }
}

$bytes = (Get-ChildItem $OutDir -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ""
Write-Host ("[export] DONE - {0:N2} GB staged." -f ($bytes / 1GB))
Write-Host "[export] Next: ZIP this whole folder and copy it to the laptop:"
Write-Host "           $OutDir"
Write-Host "[export] On the laptop (after cloning/pulling the repo), run:"
Write-Host "           .\import-assets.ps1 -ExportDir '<unzipped DOTR-assets-export folder>'"
