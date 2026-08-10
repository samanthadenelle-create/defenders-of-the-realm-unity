# f8-inbox-lib.ps1 -- the F8 inbox QUEUE (WO-965, 2026-08-10).
#
# WHY THIS EXISTS (the defect it fixes):
#   Every producer (f8-watch-daemon / websig-watch-daemon / bugreport-watch) used to publish a
#   capture by OVERWRITING two single-slot files: LATEST_CAPTURE.md and PING.json. A burst of
#   captures between two seat looks therefore collapsed to the NEWEST one, and f8-ack.ps1 then
#   acked that newest seq -- which silently marked every skipped seq as triaged.
#   Proven 2026-08-10: the seat acked seq 2306, the next ping it ever saw was seq 2309, and
#   seq 2307 ("both NPC and echo but no movement") + seq 2308 (a Tutorial STEP-STUCK error)
#   were never surfaced to any seat. The owner is NEVER the bug detector (CLAUDE.md s14), so a
#   harness that eats her flags defeats the whole passive-listener design.
#
# THE FIX: an APPEND-ONLY backlog, QUEUE.jsonl -- one JSON line per capture, never rewritten.
#   LATEST_CAPTURE.md + PING.json stay exactly as they were (the whole existing contract keeps
#   working); they are now a VIEW of the newest entry, and the QUEUE is the record. Consumers
#   walk the queue oldest-first, and f8-ack.ps1 acks ONE capture at a time.
#   An append-only log was chosen over "make LATEST_CAPTURE.md append" because a queue keeps each
#   capture's auto-harvest block intact in its own per-seq file, keeps a capture's identity (seq)
#   machine-readable, and cannot be corrupted by a concurrent producer the way a rewritten
#   aggregate file can. Per-seq capture files already existed -- they were simply unreachable.
#
# LOUDNESS: nothing here fails quietly. A superseded-but-unacked capture, a producer that did not
#   queue, and a seq gap all write to queue-events.log AND to the console.

Set-StrictMode -Off

$script:F8Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Write-F8Text([string]$Path, [string]$Text) {
    # PowerShell 5.1 `Set-Content -Encoding UTF8` emits a BOM (and has bitten this repo before).
    [System.IO.File]::WriteAllText($Path, $Text, $script:F8Utf8NoBom)
}

function Append-F8Text([string]$Path, [string]$Text) {
    [System.IO.File]::AppendAllText($Path, $Text, $script:F8Utf8NoBom)
}

function Get-F8Paths([string]$Inbox) {
    return @{
        Inbox  = $Inbox
        Ping   = (Join-Path $Inbox 'PING.json')
        Ack    = (Join-Path $Inbox 'ACK.json')
        Latest = (Join-Path $Inbox 'LATEST_CAPTURE.md')
        Queue  = (Join-Path $Inbox 'QUEUE.jsonl')
        Events = (Join-Path $Inbox 'queue-events.log')
    }
}

function Write-F8Event([string]$Inbox, [string]$Level, [string]$Message) {
    $p = Get-F8Paths $Inbox
    $line = ('{0} [{1}] {2}' -f (Get-Date).ToUniversalTime().ToString('o'), $Level, $Message)
    try { Append-F8Text $p.Events ($line + [Environment]::NewLine) } catch { }
    if ($Level -ne 'info') { Write-Host ('[f8-queue] {0}: {1}' -f $Level.ToUpper(), $Message) }
}

# -- lock: producers and ackers serialise through one named mutex ------------------------------
function Enter-F8Lock {
    $m = $null
    try { $m = New-Object System.Threading.Mutex($false, 'EoaF8InboxQueue') } catch { return $null }
    try { [void]$m.WaitOne(5000) } catch { }
    return $m
}
function Exit-F8Lock($m) {
    if ($null -eq $m) { return }
    try { $m.ReleaseMutex() } catch { }
    try { $m.Dispose() } catch { }
}

function Get-F8PingSeq([string]$Inbox) {
    $p = Get-F8Paths $Inbox
    if (-not (Test-Path $p.Ping)) { return 0 }
    try { return [int]((Get-Content $p.Ping -Raw | ConvertFrom-Json).seq) } catch { return 0 }
}

function Get-F8Queue([string]$Inbox) {
    $p = Get-F8Paths $Inbox
    $out = @()
    if (-not (Test-Path $p.Queue)) { return $out }
    foreach ($line in (Get-Content $p.Queue -ErrorAction SilentlyContinue)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { $out += ($line | ConvertFrom-Json) } catch { }
    }
    return $out
}

# ACK state. `lastAckSeq` is the CONTIGUOUS watermark and keeps its old meaning, so every existing
# reader (f8-prompt-check, f8-poll-rewake, any other seat) keeps working untouched. `acked` records
# out-of-order acks above the watermark.
function Get-F8AckState([string]$Inbox) {
    $p = Get-F8Paths $Inbox
    $state = @{ lastAckSeq = 0; acked = @() }
    if (-not (Test-Path $p.Ack)) { return $state }
    try {
        $j = Get-Content $p.Ack -Raw | ConvertFrom-Json
        $state.lastAckSeq = [int]$j.lastAckSeq
        if ($j.PSObject.Properties.Name -contains 'acked' -and $j.acked) {
            $state.acked = @($j.acked | ForEach-Object { [int]$_ })
        }
    } catch { }
    return $state
}

function Save-F8AckState([string]$Inbox, $State) {
    $p = Get-F8Paths $Inbox
    $wm = [int]$State.lastAckSeq
    $set = @{}
    foreach ($s in @($State.acked)) { $set[[int]$s] = $true }
    # roll the contiguous watermark forward over any acked run sitting just above it
    while ($set.ContainsKey($wm + 1)) { $wm++ ; $set.Remove($wm) }
    $above = @($set.Keys | Where-Object { $_ -gt $wm } | Sort-Object)
    $obj = @{
        lastAckSeq = $wm
        acked      = $above
        ackedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    }
    Write-F8Text $p.Ack ($obj | ConvertTo-Json -Depth 4)
    return $wm
}

# Find the per-seq capture .md for a seq the queue never recorded (an OLD producer process still
# running, or a pre-WO-965 backlog). Bounded scan, newest first -- only ever runs on a gap.
function Resolve-F8CaptureFile([string]$Inbox, [int]$Seq) {
    $files = Get-ChildItem -Path $Inbox -Filter 'capture-*.md' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 400
    foreach ($f in $files) {
        $head = ''
        try { $head = (Get-Content $f.FullName -TotalCount 1 -ErrorAction SilentlyContinue) } catch { }
        if ($head -match ('seq={0}\b' -f $Seq)) { return $f.FullName }
    }
    return ''
}

# THE consumer entry point: every capture between the last ack and now, OLDEST FIRST.
# Synthesises entries for any seq the queue is missing and flags it LOUDLY -- a missing entry is
# never silently skipped, which is the exact failure this WO exists to kill.
function Get-F8Pending([string]$Inbox) {
    $ack   = Get-F8AckState $Inbox
    $queue = Get-F8Queue $Inbox
    $ping  = Get-F8PingSeq $Inbox
    $wm    = [int]$ack.lastAckSeq
    $ackedSet = @{}
    foreach ($s in @($ack.acked)) { $ackedSet[[int]$s] = $true }

    $bySeq = @{}
    $maxSeq = $ping
    foreach ($e in $queue) {
        $s = [int]$e.seq
        $bySeq[$s] = $e
        if ($s -gt $maxSeq) { $maxSeq = $s }
    }

    $pending = @()
    if ($maxSeq -le $wm) { return $pending }
    for ($s = $wm + 1; $s -le $maxSeq; $s++) {
        if ($ackedSet.ContainsKey($s)) { continue }
        if ($bySeq.ContainsKey($s)) {
            $pending += $bySeq[$s]
            continue
        }
        # not in the queue: a producer that predates the queue, or a real gap. Recover + shout.
        $cap = Resolve-F8CaptureFile $Inbox $s
        $synth = [pscustomobject]@{
            seq         = $s
            utc         = ''
            kind        = 'unqueued'
            source      = 'recovered'
            capturePath = $cap
            summary     = $(if ($cap) { 'RECOVERED from capture file (producer did not queue this seq)' }
                            else { 'NO QUEUE ENTRY AND NO CAPTURE FILE - capture content is LOST' })
            unqueued    = $true
        }
        if ($cap) {
            Write-F8Event $Inbox 'warn' ("seq=$s had no QUEUE.jsonl entry; recovered from $cap (producer running pre-WO-965 code?)")
        } else {
            Write-F8Event $Inbox 'error' ("seq=$s has NO queue entry and NO capture file - a capture was LOST")
        }
        $pending += $synth
    }
    return $pending
}

# THE producer entry point. Builds nothing: the caller hands over the finished markdown with a
# __F8SEQ__ placeholder wherever the seq belongs. Allocates the seq, writes the per-seq capture
# file, refreshes LATEST_CAPTURE.md + PING.json (unchanged contract) and APPENDS to QUEUE.jsonl.
# Returns the allocated seq.
function Publish-F8Capture {
    param(
        [string]$Inbox,
        [string]$Kind,
        [string]$Md,
        [string]$Summary,
        [string]$Source = 'f8',
        [string]$BaseName = 'capture',
        [string]$PingMessage = 'F8 capture - triage now (read LATEST_CAPTURE.md or run f8-check-inbox.ps1)'
    )
    $p = Get-F8Paths $Inbox
    New-Item -ItemType Directory -Force -Path $Inbox | Out-Null
    $lock = Enter-F8Lock
    try {
        # Seq allocation is now max(ping, queue)+1 under the lock, so two producers firing in the
        # same second can no longer mint the SAME seq (each used to read PING.json independently).
        $seq = (Get-F8PingSeq $Inbox) + 1
        foreach ($e in (Get-F8Queue $Inbox)) { if (([int]$e.seq + 1) -gt $seq) { $seq = [int]$e.seq + 1 } }

        # Loud supersede notice: LATEST_CAPTURE.md is about to be overwritten while the previous
        # capture is still un-acked. Not a loss any more (the queue holds it) but it must be VISIBLE.
        $ack = Get-F8AckState $Inbox
        $prevPing = Get-F8PingSeq $Inbox
        $backlog = 0
        if ($prevPing -gt [int]$ack.lastAckSeq) {
            $backlog = @(Get-F8Pending $Inbox).Count
            Write-F8Event $Inbox 'warn' ("seq=$seq SUPERSEDES un-acked seq=$prevPing in LATEST_CAPTURE.md - $backlog capture(s) now queued; they are NOT lost, walk them with f8-check-inbox.ps1")
        }

        # per-seq filename: the old 'capture-<yyyyMMdd-HHmmss>.md' collided (and overwrote!) when two
        # captures landed inside the same second. The seq makes it collision-proof.
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $capPath = Join-Path $Inbox ('{0}-{1}-seq{2}.md' -f $BaseName, $stamp, $seq)

        $body = $Md -replace '__F8SEQ__', "$seq"
        if ($backlog -gt 0) {
            $nl = [Environment]::NewLine
            $banner = ('> BACKLOG: {0} un-acked capture(s) are queued BEHIND this one. Triage OLDEST FIRST via f8-check-inbox.ps1; f8-ack.ps1 acks one at a time.{1}{1}' -f ($backlog + 1), $nl)
            $body = $banner + $body
        }

        Write-F8Text $capPath $body
        Write-F8Text $p.Latest $body

        $sum = $Summary
        if ($null -eq $sum) { $sum = '' }
        if ($sum.Length -gt 160) { $sum = $sum.Substring(0, 160) }

        $ping = @{
            seq         = $seq
            firedAtUtc  = (Get-Date).ToUniversalTime().ToString('o')
            kind        = $Kind
            capturePath = $capPath
            summary     = $sum
            source      = $Source
            message     = $PingMessage
        }
        Write-F8Text $p.Ping ($ping | ConvertTo-Json -Depth 4)

        $entry = @{
            seq         = $seq
            utc         = $ping.firedAtUtc
            kind        = $Kind
            source      = $Source
            capturePath = $capPath
            summary     = $sum
        }
        Append-F8Text $p.Queue (($entry | ConvertTo-Json -Depth 4 -Compress) + [Environment]::NewLine)
        Write-F8Event $Inbox 'info' ("queued seq=$seq kind=$Kind source=$Source file=$capPath")
        return $seq
    }
    finally { Exit-F8Lock $lock }
}
