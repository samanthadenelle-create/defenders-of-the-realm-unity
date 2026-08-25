# =============================================================================
# generate-sfx.ps1 - generate combat SFX via the ElevenLabs Sound Effects API.
# -----------------------------------------------------------------------------
# Reads the API key from .secrets/elevenlabs.key (gitignored) and a prompt set
# from a CSV (id,duration,text) or the inline default set below. Writes one
# .mp3 per row into Assets/Audio/SFX/Combat/.
#
# Usage (from repo root, editor CLOSED not required - pure HTTP):
#   powershell -ExecutionPolicy Bypass -File Tools/AudioGen/generate-sfx.ps1
#   powershell -ExecutionPolicy Bypass -File Tools/AudioGen/generate-sfx.ps1 -PromptCsv Tools/AudioGen/prompts.csv
#
# API notes: duration_seconds must be 0.5..30. prompt_influence 0..1 (0.5 = balanced).
# License: free-tier output needs ElevenLabs attribution for commercial use - see
# Assets/Audio/SFX/Combat/SOURCE_LICENSE.md. Regenerate on a paid tier before ship.
# =============================================================================
param(
  [string]$KeyFile   = "$PSScriptRoot/../../.secrets/elevenlabs.key",
  [string]$OutDir    = "$PSScriptRoot/../../Assets/Audio/SFX/Combat",
  [string]$PromptCsv = ""
)

$key = (Get-Content $KeyFile -Raw).Trim()
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

if ($PromptCsv -and (Test-Path $PromptCsv)) {
  $set = Import-Csv $PromptCsv
} else {
  $set = @(
    [pscustomobject]@{ id='melee_swing';     duration=0.6; text='fast sword swing whoosh through air, sharp and quick, no impact' },
    [pscustomobject]@{ id='melee_hit_metal'; duration=0.5; text='metal sword clashing on metal armor, sharp bright impact clang' },
    [pscustomobject]@{ id='melee_hit_flesh'; duration=0.5; text='sword blade striking flesh, heavy meaty thud impact' },
    [pscustomobject]@{ id='cast_charge';     duration=1.3; text='rising magical energy charging up, building hum and shimmer, gathering power' },
    [pscustomobject]@{ id='cast_land';       duration=0.9; text='powerful magic spell impact, deep magical boom with bright sparkle' },
    [pscustomobject]@{ id='enemy_death';     duration=0.8; text='monster death grunt and body collapse, short and guttural' }
  )
}

foreach ($s in $set) {
  $dur = [double]$s.duration
  if ($dur -lt 0.5) { $dur = 0.5 }
  $body = @{ text=$s.text; duration_seconds=$dur; prompt_influence=0.5 } | ConvertTo-Json
  $out  = Join-Path $OutDir "$($s.id).mp3"
  try {
    Invoke-RestMethod -Uri 'https://api.elevenlabs.io/v1/sound-generation' `
      -Headers @{'xi-api-key'=$key} -Method Post -ContentType 'application/json' `
      -Body $body -OutFile $out
    Write-Host "OK   $($s.id).mp3 ($((Get-Item $out).Length) bytes)"
  } catch {
    Write-Host "FAIL $($s.id): $($_.Exception.Message)"
    if ($_.ErrorDetails) { Write-Host $_.ErrorDetails.Message }
  }
}
