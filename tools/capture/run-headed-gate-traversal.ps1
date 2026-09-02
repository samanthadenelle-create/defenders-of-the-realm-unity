param(
    [string]$ExePath = "",
    [string]$OutputDir = "",
    [int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $ExePath) { $ExePath = Join-Path $repo 'Builds\Windows\DefendersOfTheRealm.exe' }
if (-not $OutputDir) { $OutputDir = Join-Path $repo ('docs\proof\' + (Get-Date -Format 'yyyy-MM-dd') + '-gate-traversal') }
if (-not (Test-Path $ExePath)) { throw "No headed player at $ExePath" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$args = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720',
          '-bootScene','Main_Castle_Overworld','-gateProofDir',$OutputDir)
$proc = Start-Process -FilePath $ExePath -ArgumentList $args -PassThru
if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
    $proc.Kill(); throw "Headed gate proof timed out after $TimeoutSeconds seconds"
}
if ($proc.ExitCode -ne 0 -or -not (Test-Path (Join-Path $OutputDir 'PASS.txt'))) {
    throw "Headed gate proof failed (exit $($proc.ExitCode)); see $OutputDir"
}

Add-Type -AssemblyName System.Drawing
function Get-MeanDelta([string]$a,[string]$b) {
    $ia = [System.Drawing.Bitmap]::FromFile($a); $ib = [System.Drawing.Bitmap]::FromFile($b)
    try {
        $sx=[Math]::Max(1,[int]($ia.Width/64)); $sy=[Math]::Max(1,[int]($ia.Height/36));
        [double]$sum=0; [int]$n=0
        for($x=0;$x -lt [Math]::Min($ia.Width,$ib.Width);$x+=$sx){
            for($y=0;$y -lt [Math]::Min($ia.Height,$ib.Height);$y+=$sy){
                $ca=$ia.GetPixel($x,$y); $cb=$ib.GetPixel($x,$y)
                $sum += [Math]::Abs($ca.R-$cb.R)+[Math]::Abs($ca.G-$cb.G)+[Math]::Abs($ca.B-$cb.B); $n+=3
            }
        }
        return [Math]::Round($sum/[Math]::Max(1,$n),3)
    } finally { $ia.Dispose(); $ib.Dispose() }
}
$rows = Import-Csv (Join-Path $OutputDir 'gate-traversal.csv')
$md = @('# Headed continuous gate traversal proof','',
        'The development Windows player performed ordinary `NavMeshAgent.Move` pulses over the single-scene ground. No gate NavMeshLink or hero warp was present. Every pulse produced a screenshot.','',
        '| Entrance | Moves | Exit time (s) | Start to final image delta | Consecutive deltas |','|---|---:|---:|---:|---|')
foreach($side in @('north','south','east','west')) {
    $set=@($rows | Where-Object side -eq $side | Sort-Object {[int]$_.move})
    $d=@(); for($i=1;$i -lt $set.Count;$i++) { $d += Get-MeanDelta (Join-Path $OutputDir $set[$i-1].image) (Join-Path $OutputDir $set[$i].image) }
    $sf=Get-MeanDelta (Join-Path $OutputDir $set[0].image) (Join-Path $OutputDir $set[-1].image)
    $md += "| $side | $($set[-1].move) | $($set[-1].elapsed_seconds) | $sf | $($d -join ', ') |"
}
$md += ''; $md += 'Raw positions/timings: [gate-traversal.csv](gate-traversal.csv)'
[IO.File]::WriteAllLines((Join-Path $OutputDir 'README.md'),$md)
Write-Output "GATE_HEADED_PROOF_OK -> $OutputDir"
