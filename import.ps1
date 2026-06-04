param(
      [Parameter(Mandatory=$true)][string]$ExportDir,   # the unzipped DOTR-assets-export folder (the one CONTAINING "Assets")
      [string]$ProjectDir = (Get-Location).Path         # defaults to current dir; pass -ProjectDir if you run it elsewhere
  )
  $ErrorActionPreference = 'Stop'

  if (-not (Test-Path (Join-Path $ProjectDir 'Assets'))) {
      Write-Error "ProjectDir '$ProjectDir' has no Assets/ folder. Run this from the repo root, or pass -ProjectDir 'C:\path\to\defenders-of-the-realm-unity'."
      exit 1
  }
  if (-not (Test-Path $ExportDir)) { Write-Error "ExportDir not found: $ExportDir"; exit 1 }
  if (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue) { Write-Error "Unity is running. Close it first."; exit 3 }

  $folders = @('Assets\Models','Assets\Art\TripoStructures','Assets\Resources\Structures')
  $metas   = @('Assets\Models.meta','Assets\Art\TripoStructures.meta','Assets\Resources\Structures.meta')

  $placed = 0
  foreach ($f in $folders) {
      $src = Join-Path $ExportDir $f
      if (-not (Test-Path $src)) { Write-Warning "  not in export (skipped): $f"; continue }
      $dst = Join-Path $ProjectDir $f
      New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
      robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
      Write-Host "  placed $f"; $placed++
  }
  foreach ($m in $metas) {
      $src = Join-Path $ExportDir $m
      if (Test-Path $src) { Copy-Item $src (Join-Path $ProjectDir $m) -Force; Write-Host "  placed $m" }
  }

  if ($placed -eq 0) { Write-Warning "Nothing placed - is -ExportDir the folder that CONTAINS the 'Assets' subfolder?"; exit 1 }
  Write-Host ""
  Write-Host "DONE. Open the project in Unity 6000.4.8f1 (it reimports the art), then Build with 'Development Build' ticked."
