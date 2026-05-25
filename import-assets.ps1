# =============================================================================
# import-assets.ps1 - place the art exported by export-assets.ps1 back into THIS
# project, exactly where it belongs. Run on the LAPTOP after you have:
#   1. cloned/pulled the repo (gets code + scenes + LFS hero FBX),
#   2. unzipped the DOTR-assets-export folder you transferred over.
#
#   powershell -ExecutionPolicy Bypass -File .\import-assets.ps1 -ExportDir "C:\Downloads\DOTR-assets-export"
#
# It copies (gitignored, so they only arrive via this transfer):
#   Assets/Models/               Assets/Art/TripoStructures/   Assets/Resources/Structures/
# plus those folders' .meta files, so the committed Village scene's GUID
# references resolve (no magenta). Then open Unity 6000.4.8f1 - it reimports the
# art - and build (tick "Development Build" in Build Settings for the dev gear).
#
# ASCII-only on purpose.
# =============================================================================
param(
    [Parameter(Mandatory = $true)][string]$ExportDir
)

$ErrorActionPreference = 'Stop'
$proj = $PSScriptRoot

if (-not (Test-Path $ExportDir)) {
    Write-Error "ExportDir not found: $ExportDir  (point it at the unzipped DOTR-assets-export folder)"
    exit 1
}
# Don't import while Unity is open (asset churn while open can corrupt the project).
if (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue) {
    Write-Error "Unity editor is running. Close it first, then re-run."
    exit 3
}

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

$placed = 0
foreach ($f in $folders) {
    $src = Join-Path $ExportDir $f
    if (-not (Test-Path $src)) { Write-Warning "  not in export (skipped): $f"; continue }
    $dst = Join-Path $proj $f
    New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
    robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
    Write-Host "  placed $f"
    $placed++
}
foreach ($m in $metas) {
    $src = Join-Path $ExportDir $m
    if (Test-Path $src) { Copy-Item $src (Join-Path $proj $m) -Force; Write-Host "  placed $m" }
}

Write-Host ""
if ($placed -eq 0) {
    Write-Warning "[import] Nothing placed - is -ExportDir the folder that CONTAINS the 'Assets' subfolder?"
    exit 1
}
Write-Host "[import] DONE. Open the project in Unity 6000.4.8f1 (it will reimport the art),"
Write-Host "[import] then File > Build Settings > tick 'Development Build' > Build (for the dev gear / force-wave)."
