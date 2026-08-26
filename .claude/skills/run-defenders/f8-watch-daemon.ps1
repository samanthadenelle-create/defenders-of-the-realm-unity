# f8-watch-daemon.ps1 - Persistent F8 / break-log watcher (auto-rearm forever).
# Start: f8-watch-start.ps1 | Poll: f8-check-inbox.ps1 | Stop: f8-watch-stop.ps1
#
# WO-965: every capture is now APPENDED to logs/f8-inbox/QUEUE.jsonl via f8-inbox-lib.ps1.
# LATEST_CAPTURE.md + PING.json still hold the newest capture (unchanged contract) but they are
# a VIEW; the queue is the record, so a burst can no longer collapse to its newest member.

param(
    [int]$PollSeconds = 5
)

$ErrorActionPreference = 'SilentlyContinue'

. (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
$PidFile  = Join-Path $Inbox 'daemon.pid'
$PingFile = Join-Path $Inbox 'PING.json'
$Latest   = Join-Path $Inbox 'LATEST_CAPTURE.md'
$StateFile = Join-Path $Inbox 'daemon-state.json'

# Desktop persistentDataPath = LocalLow\<companyName>\<productName>. productName became
# "Echoes of Elarion" on 2026-08-08 (store-listing match), which MOVES this folder. Prefer the
# new one; fall back to the legacy folder so captures made by an older player still triage.
$BreakLogDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\DeNelle\Echoes of Elarion'
$LegacyLogDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\DeNelle\Defenders of the Realm'
if ((-not (Test-Path $BreakLogDir)) -and (Test-Path $LegacyLogDir)) { $BreakLogDir = $LegacyLogDir }
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
Write-F8Text $PidFile "$myPid"

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
    # The seq is allocated INSIDE Publish-F8Capture (under the inbox lock) - __F8SEQ__ is the
    # placeholder it substitutes, so the header can be built before the number exists.
    $harvest = Harvest-Context
    $nl = [Environment]::NewLine

    $md = @(
        '# F8 Capture (auto-inbox seq=__F8SEQ__)'
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
        '- Ack when done: f8-ack.ps1  (acks THIS capture only; a queued backlog stays pending)'
        ''
    ) -join $nl

    $sumLen = [Math]::Min(120, $triggerLine.Length)
    $seq = Publish-F8Capture -Inbox $Inbox -Kind $kind -Md $md -Source 'f8' -BaseName 'capture' `
        -Summary $triggerLine.Substring(0, $sumLen)

    Write-Host ''
    Write-Host '============================================================'
    Write-Host (' F8 INBOX PING seq={0} - TRIAGE NOW' -f $seq)
    Write-Host (' {0}' -f $Latest)
    Write-Host '============================================================'
    Write-Host ''

    Alert-Owner -Title 'Defenders F8 Capture' -Body ('seq={0} {1}{2}{3}' -f $seq, $kind, $nl, $triggerLine)
}

# WO-965 second drop path: the daemon used to baseline $breakBase to the CURRENT line count on
# every start, so any capture the owner made while the daemon was down (machine reboot, seat
# restart, a crash) was skipped forever and silently. The break-log offset is now PERSISTED, so a
# restart resumes where it left off and the backlog is replayed - loudly.
$breakBase = 0
$curBreakLines = 0
if (Test-Path $BreakLog) { $curBreakLines = @(Get-Content $BreakLog -ErrorAction SilentlyContinue).Count }

$persisted = $null
if (Test-Path $StateFile) { try { $persisted = Get-Content $StateFile -Raw | ConvertFrom-Json } catch { } }
if ($persisted -and $persisted.breakLog -eq $BreakLog) {
    $breakBase = [int]$persisted.breakOffset
    if ($breakBase -gt $curBreakLines) {
        Write-F8Event $Inbox 'warn' ("break-log shrank ({0} -> {1} lines): rotated/cleared, replaying from 0" -f $breakBase, $curBreakLines)
        $breakBase = 0
    } elseif ($breakBase -lt $curBreakLines) {
        Write-F8Event $Inbox 'warn' ("daemon was DOWN for {0} break-log line(s) (offset {1} of {2}) - replaying them now, none dropped" -f ($curBreakLines - $breakBase), $breakBase, $curBreakLines)
    }
} else {
    # first ever run against this break-log: baseline to now (do not replay months of history)
    $breakBase = $curBreakLines
    Write-F8Event $Inbox 'info' ("first run for $BreakLog - baselined at $breakBase line(s)")
}

function Save-BreakOffset([int]$offset) {
    $obj = @{ breakLog = $BreakLog; breakOffset = $offset; updatedUtc = (Get-Date).ToUniversalTime().ToString('o') }
    try { Write-F8Text $StateFile ($obj | ConvertTo-Json -Depth 3) } catch { }
}
Save-BreakOffset $breakBase

$logPositions = @{}
foreach ($p in @($EditorLog, $PlayerLog)) {
    if (Test-Path $p) { $logPositions[$p] = (Get-Item $p).Length } else { $logPositions[$p] = 0 }
}

$seenKeys = @{}
# "note" = FlowTrace.Capture (audit 2026-08-15): an EXPECTED lifecycle state dump (hero death,
# scene handoff) that must land in break-log.jsonl for post-hoc reading but must NEVER wake a
# triage seat. Before this channel existed, those dumps were written as FlowTrace.Fail - the only
# severity that survived to device - so every hero death raised an F8 error capture.
$kindSkip = 'session_start|scene_loaded|note|idle'

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
                if ($line -match ('"kind"\s*:\s*"({0})"' -f $kindSkip)) { continue }
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $key = 'bl:' + $line.GetHashCode()
                if ($seenKeys.ContainsKey($key)) { continue }
                $seenKeys[$key] = $true

                # anchored on the "kind" FIELD: the old greedy 'kind.*:\s*"(\w+)"' walked past it and
                # captured the LAST quoted word on the line - which is why PING.json kind read
                # "Main_Castle_Overworld" (the scene) instead of "flagged" / "error".
                $capKind = 'break-log'
                if ($line -match '"kind"\s*:\s*"([^"]+)"') { $capKind = $Matches[1] }
                Emit-Capture -kind $capKind -body $line -triggerLine $line
            }
            $breakBase = $cur
            Save-BreakOffset $breakBase
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
