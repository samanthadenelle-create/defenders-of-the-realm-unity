# f8-device-bridge-start.ps1 -- WO-1227. Starts the device half of the section 14 chain in a
# hidden background PowerShell, one per machine, alongside f8-watch-start.ps1's desktop daemon.
#
# ADDITIVE BY DESIGN: this does not touch the desktop watch path. f8-watch-daemon.ps1 keeps
# watching %LOCALAPPDATA%Low\DeNelle\... exactly as before; this adds the phone as a SECOND
# PRODUCER into the SAME queue (logs/f8-inbox/QUEUE.jsonl), so a flag on the Seeker surfaces
# through f8-check-inbox.ps1 exactly like a flag on the exe.
#
# No device attached is a silent no-op inside the bridge, so this is safe to leave running with
# the phone unplugged.
param([int]$PollSeconds = 30)

$Bridge   = Join-Path $PSScriptRoot 'f8-device-bridge.ps1'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
New-Item -ItemType Directory -Force -Path $Inbox | Out-Null

$PidFile = Join-Path $Inbox 'device-bridge.pid'
if (Test-Path $PidFile) {
    $old = (Get-Content $PidFile -Raw).Trim()
    if ($old) {
        $proc = Get-Process -Id ([int]$old) -ErrorAction SilentlyContinue
        if ($proc -and $proc.ProcessName -match 'powershell|pwsh') {
            Write-Host "[f8-device-bridge-start] Already running (pid=$old). Inbox: $Inbox"
            exit 0
        }
    }
}

$argList = "-NoProfile -ExecutionPolicy Bypass -File `"$Bridge`" -Loop -PollSeconds $PollSeconds -Quiet"
$p = Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -WindowStyle Hidden `
    -WorkingDirectory $RepoRoot -PassThru
[System.IO.File]::WriteAllText($PidFile, "$($p.Id)", (New-Object System.Text.UTF8Encoding($false)))

Write-Host "[f8-device-bridge-start] Device bridge started. pid=$($p.Id) poll=${PollSeconds}s"
Write-Host "[f8-device-bridge-start] Inbox: $Inbox  (same QUEUE.jsonl as the desktop daemon)"
Write-Host "[f8-device-bridge-start] Stop: f8-device-bridge-stop.ps1"
