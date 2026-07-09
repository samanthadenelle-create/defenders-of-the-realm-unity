# f8-watch-daemon.ps1 - Persistent F8 / break-log watcher (auto-rearm forever).
# Start: f8-watch-start.ps1 | Poll: f8-check-inbox.ps1 | Stop: f8-watch-stop.ps1

param(
    [int]$PollSeconds = 5
)

$ErrorActionPreference = 'SilentlyContinue'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
$PidFile  = Join-Path $Inbox 'daemon.pid'
$PingFile = Join-Path $Inbox 'PING.json'
$Latest   = Join-Path $Inbox 'LATEST_CAPTURE.md'

$BreakLogDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\DeNelle\Defenders of the Realm'
$BreakLog    = Join-Path $BreakLogDir 'break-log.jsonl'
$PlayerLog   = Join-Path $BreakLogDir 'Player.log'
$EditorLog   = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'

New-Item -ItemType Directory -Force -Path $Inbox | Out-Null

$myPid = $PID
if (Test-Path $PidFile) {
    $old = (Get-Content $PidFile -Raw).Trim()
    if ($old -and ($old -ne "$myPid")) {
        $proc = Get-Process -Id ([int]$old) -ErrorAction SilentlyContinue
        if ($proc -and $proc.ProcessName -match 'powershell|pwsh') {
            Write-Host "[f8-daemon] Already running (pid=$old). Exit."
            exit 0
        }
    }
}
Set-Content -Path $PidFile -Value "$myPid" -Encoding UTF8

function Read-PingSeq {
    if (-not (Test-Path $PingFile)) { return 0 }
    try {
        $j = Get-Content $PingFile -Raw | ConvertFrom-Json
        return [int]$j.seq
    } catch { return 0 }
}

function Write-Ping([int]$seq, [string]$kind, [string]$capturePath, [string]$summary) {
    $obj = @{
        seq         = $seq
        firedAtUtc  = (Get-Date).ToUniversalTime().ToString('o')
        kind        = $kind
        capturePath = $capturePath
        summary     = $summary
        message     = 'F8 capture - triage now (read LATEST_CAPTURE.md or run f8-check-inbox.ps1)'
    }
    $obj | ConvertTo-Json -Depth 4 | Set-Content -Path $PingFile -Encoding UTF8
}

function Harvest-Context {
    $blocks = @()
    foreach ($L in @($EditorLog, $PlayerLog)) {
        if (-not (Test-Path $L)) { continue }
        $hits = Select-String -Path $L -Pattern '\[Flow:|\[FeatureFlags\]|ff\.[a-z]+ =|\[Guard\]|EXCEPTION|NullReference' |
            Select-Object -Last 60
        if ($hits) {
            $blocks += ('--- {0} (last 60 signal lines) ---' -f $L)
            $blocks += ($hits | ForEach-Object { $_.Line })
        }
    }
    return $blocks
}

function Alert-Owner([string]$Title, [string]$Body) {
    try { [System.Media.SystemSounds]::Exclamation.Play() } catch { }
    Write-Host ('[f8-daemon] ALERT: {0} - {1}' -f $Title, $Body)
}

function Emit-Capture([string]$kind, [string]$body, [string]$triggerLine) {
    $seq = (Read-PingSeq) + 1
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $capPath = Join-Path $Inbox ('capture-{0}.md' -f $stamp)
    $harvest = Harvest-Context
    $nl = [Environment]::NewLine

    $md = @(
        ('# F8 Capture (auto-inbox seq={0})' -f $seq)
        ''
        ('**Time (local):** {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
        ('**Kind:** {0}' -f $kind)
        ''
        '## Trigger'
        '```'
        $triggerLine
        '```'
        ''
        '## Payload'
        $body
        ''
        '## Auto-harvested context (read FIRST - section 12)'
        '```'
        ($harvest -join $nl)
        '```'
        ''
        '## Triage'
        '- Read this file before code-read or theory.'
        '- Route per docs/TICKET_PIPELINE.md.'
        '- Ack when done: f8-ack.ps1'
        ''
    ) -join $nl

    Set-Content -Path $capPath -Value $md -Encoding UTF8
    Set-Content -Path $Latest -Value $md -Encoding UTF8

    $sumLen = [Math]::Min(120, $triggerLine.Length)
    Write-Ping -seq $seq -kind $kind -capturePath $capPath -summary $triggerLine.Substring(0, $sumLen)

    Write-Host ''
    Write-Host '============================================================'
    Write-Host (' F8 INBOX PING seq={0} - TRIAGE NOW' -f $seq)
    Write-Host (' {0}' -f $Latest)
    Write-Host '============================================================'
    Write-Host ''

    Alert-Owner -Title 'Defenders F8 Capture' -Body ('seq={0} {1}{2}{3}' -f $seq, $kind, $nl, $triggerLine)
}

$breakBase = 0
if (Test-Path $BreakLog) {
    $breakBase = @(Get-Content $BreakLog -ErrorAction SilentlyContinue).Count
}

$logPositions = @{}
foreach ($p in @($EditorLog, $PlayerLog)) {
    if (Test-Path $p) { $logPositions[$p] = (Get-Item $p).Length } else { $logPositions[$p] = 0 }
}

$seenKeys = @{}
$kindSkip = 'session_start|scene_loaded'

Write-Host ('[f8-daemon] armed pid={0} poll={1}s' -f $myPid, $PollSeconds)
Write-Host ('[f8-daemon] break-log: {0}' -f $BreakLog)
Write-Host ('[f8-daemon] inbox: {0}' -f $Inbox)
Write-Host '[f8-daemon] auto-rearm: INFINITE (no manual re-arm needed)'
Write-Host ''

while ($true) {
    Start-Sleep -Seconds $PollSeconds

    if (Test-Path $BreakLog) {
        $lines = @(Get-Content $BreakLog -ErrorAction SilentlyContinue)
        $cur = $lines.Count
        if ($cur -lt $breakBase) { $breakBase = 0 }
        if ($cur -gt $breakBase) {
            $newLines = $lines[$breakBase..($cur - 1)]
            foreach ($line in $newLines) {
                if ($line -match ('kind.*:\s*"({0})"' -f $kindSkip)) { continue }
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $key = 'bl:' + $line.GetHashCode()
                if ($seenKeys.ContainsKey($key)) { continue }
                $seenKeys[$key] = $true

                $capKind = 'break-log'
                if ($line -match 'kind.*:\s*"(\w+)"') { $capKind = $Matches[1] }
                Emit-Capture -kind $capKind -body $line -triggerLine $line
            }
            $breakBase = $cur
        }
    }

    foreach ($logPath in @($EditorLog, $PlayerLog)) {
        if (-not (Test-Path $logPath)) { continue }
        $len = (Get-Item $logPath).Length
        $pos = $logPositions[$logPath]
        if ($len -lt $pos) { $pos = 0 }
        if ($len -le $pos) { continue }

        $fs = [System.IO.File]::Open($logPath, 'Open', 'Read', 'FileShare.ReadWrite')
        $fs.Seek($pos, 'Begin') | Out-Null
        $sr = New-Object System.IO.StreamReader($fs)
        $chunk = $sr.ReadToEnd()
        $sr.Close()
        $fs.Close()
        $logPositions[$logPath] = $len

        foreach ($line in ($chunk -split "`r?`n")) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $isFlagged  = $line -match '\[BreakCapture\].*flagged|kind.*flagged'
            $isError    = $line -match 'error CS\d+|Exception:|NullReferenceException|AssertionException'
            $isSoftlock = $line -match 'Infinite loop|stack overflow|Deadlock|softlock'
            if (-not ($isFlagged -or $isError -or $isSoftlock)) { continue }

            $key = 'log:' + $line.GetHashCode()
            if ($seenKeys.ContainsKey($key)) { continue }
            $seenKeys[$key] = $true

            if ($isFlagged) { $capKind = 'flagged' }
            elseif ($isSoftlock) { $capKind = 'softlock' }
            else { $capKind = 'error' }
            Emit-Capture -kind $capKind -body $line -triggerLine $line
        }
    }
}