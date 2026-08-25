# =============================================================================
# rip-clips.ps1 - slice ONE audio file into many named clips.
# -----------------------------------------------------------------------------
# Two modes:
#   1) SILENCE  - auto-detect gaps (>= -MinSilence sec under -NoiseDb) and export
#                 each non-silent segment as its own clip. Name them in order via
#                 -Names (comma list) or they fall back to clip_01, clip_02, ...
#   2) TIMESTAMP - pass -Cuts "id=start-end,id2=start-end" (seconds) to cut exactly.
#
# Outputs 44.1kHz mono WAV (Unity-friendly) into -OutDir. Trims leading/trailing
# silence per clip and normalizes loudness lightly.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File Tools/AudioGen/rip-clips.ps1 `
#     -In Tools/AudioGen/drop/source.mp3 -OutDir Assets/Audio/SFX/Combat `
#     -Mode silence -Names "melee_swing,melee_hit_metal,cast_charge,cast_land,enemy_death"
#
#   powershell -ExecutionPolicy Bypass -File Tools/AudioGen/rip-clips.ps1 `
#     -In Tools/AudioGen/drop/source.mp3 -OutDir Assets/Audio/SFX/Combat `
#     -Mode timestamp -Cuts "melee_swing=2.0-2.7,melee_hit_metal=4.1-4.7"
# =============================================================================
param(
  [Parameter(Mandatory=$true)][string]$In,
  [Parameter(Mandatory=$true)][string]$OutDir,
  [ValidateSet('silence','timestamp')][string]$Mode = 'silence',
  [string]$Names = '',
  [string]$Cuts  = '',
  [double]$MinSilence = 0.4,
  [int]$NoiseDb = -35
)

# Resolve ffmpeg/ffprobe (winget Links alias dir, else PATH).
$link = "$env:LOCALAPPDATA\Microsoft\WinGet\Links"
$ffmpeg  = if (Test-Path "$link\ffmpeg.exe")  { "$link\ffmpeg.exe" }  else { 'ffmpeg' }
$ffprobe = if (Test-Path "$link\ffprobe.exe") { "$link\ffprobe.exe" } else { 'ffprobe' }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$nameList = @($Names -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })

function Export-Clip([string]$name, [double]$start, [double]$dur) {
  $out = Join-Path $OutDir "$name.wav"
  & $ffmpeg -hide_banner -loglevel error -y -i $In -ss $start -t $dur `
     -af "silenceremove=start_periods=1:start_threshold=-50dB,areverse,silenceremove=start_periods=1:start_threshold=-50dB,areverse,loudnorm=I=-16:TP=-1.5" `
     -ar 44100 -ac 1 $out
  if (Test-Path $out) { Write-Host "OK  $name.wav ($([math]::Round((Get-Item $out).Length/1kb,1)) KB)  [$start..$([math]::Round($start+$dur,2))]" }
  else { Write-Host "FAIL $name" }
}

if ($Mode -eq 'timestamp') {
  foreach ($c in ($Cuts -split ',')) {
    if ($c -match '^\s*(.+?)=([\d.]+)-([\d.]+)\s*$') {
      Export-Clip $Matches[1].Trim() ([double]$Matches[2]) ([double]$Matches[3]-[double]$Matches[2])
    }
  }
  return
}

# SILENCE mode: detect silence boundaries, derive non-silent segments.
$log = & $ffmpeg -hide_banner -i $In -af "silencedetect=noise=${NoiseDb}dB:d=$MinSilence" -f null - 2>&1
$starts = @(); $ends = @()
foreach ($line in $log) {
  if ($line -match 'silence_start:\s*([\d.]+)') { $starts += [double]$Matches[1] }
  if ($line -match 'silence_end:\s*([\d.]+)')   { $ends   += [double]$Matches[1] }
}
$durTotal = [double](& $ffprobe -v error -show_entries format=duration -of csv=p=0 $In)

# Non-silent segments = [0..firstSilenceStart], [silenceEnd..nextSilenceStart], ... [lastSilenceEnd..end]
$segs = @()
$cursor = 0.0
for ($i=0; $i -lt $starts.Count; $i++) {
  if ($starts[$i] - $cursor -gt 0.15) { $segs += ,@($cursor, $starts[$i]) }
  if ($i -lt $ends.Count) { $cursor = $ends[$i] }
}
if ($durTotal - $cursor -gt 0.15) { $segs += ,@($cursor, $durTotal) }

Write-Host "Detected $($segs.Count) sound segment(s)."
for ($i=0; $i -lt $segs.Count; $i++) {
  $name = if ($i -lt $nameList.Count) { $nameList[$i] } else { "clip_{0:D2}" -f ($i+1) }
  Export-Clip $name $segs[$i][0] ($segs[$i][1]-$segs[$i][0])
}
