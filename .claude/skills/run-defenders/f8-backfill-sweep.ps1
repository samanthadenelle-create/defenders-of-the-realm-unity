# f8-backfill-sweep.ps1 -- WO-1018. Reconcile every capture FILE on disk against what was actually
# acked, and surface anything that was buried. Run this ONCE before trusting the below-watermark
# scan; re-running it is safe and idempotent.
#
# WHY IT EXISTS, AND WHY IT MUST RUN FIRST:
#   ACK.json carries `lastAckSeq` (a contiguous watermark) and an `acked` set that is EMPTY BY
#   DESIGN -- Save-F8AckState folds contiguous acks into the watermark and drops them from the set.
#   So the watermark is the only ack state that survives, and making the `acked` set the authority
#   would instantly re-open every capture ever taken (~1273 on 2026-08-22) and read as a
#   catastrophic regression. The real per-ack record is the APPEND-ONLY queue-events.log, which
#   writes one 'acked seq=N' line every time f8-ack.ps1 closes something. That is what this sweep
#   reconciles against, and its output (ACK_BACKFILL.json) is what gates Get-F8Pending's deep scan.
#
# WHAT IT NEVER DOES: auto-close anything. Orphans are LISTED for a seat to triage, and acked by
# file (f8-ack.ps1 -File <name>). No capture is deleted or modified.
#
# MARKERS (judge by these, never by the exit code -- this repo's runners exit 0 on failure):
#   F8_SWEEP_OK files=<n> orphans=<k> preQueue=<m> noFile=<j>   success
#   F8_SWEEP_FAIL <reason>                                      refused; nothing was written
#   F8_SWEEP_DUPSEQ seq=<n> ...                                 one line per colliding sequence
param(
    [string]$InboxOverride = '',
    [switch]$Quiet
)

$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')

# One orphan row: enough for a seat to decide whether it is an owner flag or startup noise WITHOUT
# opening the file. The summary is lifted from the capture's own trigger line.
function New-F8OrphanRecord([int]$Seq, [string]$Leaf, [string]$Path, [string]$Reason) {
    $sum  = ''
    $kind = 'unknown'
    $utc  = ''
    try {
        $head = @(Get-Content -LiteralPath $Path -TotalCount 20 -ErrorAction SilentlyContinue)
        foreach ($l in $head) {
            if ($l -match '^\*\*Kind:\*\*\s*(.+)$') { $kind = $Matches[1].Trim() }
            if ($l -match '^\*\*Time \(local\):\*\*\s*(.+)$') { $utc = $Matches[1].Trim() }
            if ([string]::IsNullOrWhiteSpace($sum) -and $l -match '"message"\s*:\s*"([^"]*)"') { $sum = $Matches[1] }
        }
        if ([string]::IsNullOrWhiteSpace($sum)) {
            foreach ($l in $head) {
                if ($l -match '^\s*\{' ) { $sum = $l; break }
            }
        }
        if ([string]::IsNullOrWhiteSpace($sum) -and $head.Count -gt 0) { $sum = "$($head[0])" }
    } catch { }
    if ($sum.Length -gt 200) { $sum = $sum.Substring(0, 200) }
    return [pscustomobject]@{
        seq     = $Seq
        file    = $Leaf
        path    = $Path
        kind    = $kind
        utc     = $utc
        reason  = $Reason
        summary = $sum
    }
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = $InboxOverride
if ([string]::IsNullOrWhiteSpace($Inbox)) { $Inbox = Join-Path $RepoRoot 'logs\f8-inbox' }
$p = Get-F8Paths $Inbox

if (-not (Test-Path $Inbox)) { Write-Host "F8_SWEEP_FAIL no inbox at $Inbox"; exit 0 }

# -- 1) the queue is the authority for WHEN the queue era began -----------------------------------
# Captures numbered below the first queued seq predate QUEUE.jsonl entirely. No ack record could
# ever have existed for them, so calling them orphans would mean re-opening years of noise. They are
# counted and reported, never surfaced. Without a queue there is no floor and no sweep.
$queue = @(Get-F8Queue $Inbox)
if ($queue.Count -eq 0) {
    Write-Host 'F8_SWEEP_FAIL QUEUE.jsonl is missing or empty - there is no ack era to reconcile against. Refusing: an empty sweep must never look like a clean one.'
    exit 0
}
$queueFloor = [int]($queue[0].seq)
$queueByLeaf = @{}
$queueBySeq  = @{}
foreach ($e in $queue) {
    $s = [int]$e.seq
    if ($s -lt $queueFloor) { $queueFloor = $s }
    if (-not $queueBySeq.ContainsKey($s)) { $queueBySeq[$s] = @() }
    $queueBySeq[$s] += $e
    $leaf = ''
    try { if ($e.capturePath) { $leaf = Split-Path ([string]$e.capturePath) -Leaf } } catch { }
    if ($leaf) { $queueByLeaf[$leaf] = $s }
}

# -- 2) every capture file on disk, with its seq ---------------------------------------------------
# Files named '<base>-<stamp>-seq<N>.md' give up their seq for free. Pre-WO-965 files do not, so
# their first line is read ONCE here and cached in capture-index.json; nothing on the hot path ever
# reads them again.
Clear-F8IndexCache $Inbox
$idx = Get-F8CaptureIndex $Inbox
$files = @($idx.Files)
if ($files.Count -eq 0) {
    Write-Host 'F8_SWEEP_FAIL no capture files found on disk - refusing to report a clean sweep over nothing.'
    exit 0
}

$seqOf = @{}     # full path -> seq (-1 = unknown)
foreach ($k in $idx.BySeq.Keys) {
    foreach ($path in @($idx.BySeq[$k])) { $seqOf[[string]$path] = [int]$k }
}
$legacyMap = @{}
$unreadable = 0
foreach ($path in @($idx.NoSeq)) {
    if ($seqOf.ContainsKey([string]$path)) { continue }
    $s = -1
    try {
        $head = Get-Content -LiteralPath $path -TotalCount 1 -ErrorAction SilentlyContinue
        if ("$head" -match 'seq=(\d+)') { $s = [int]$Matches[1] }
    } catch { }
    $seqOf[[string]$path] = $s
    if ($s -ge 0) {
        if (-not $legacyMap.ContainsKey("$s")) { $legacyMap["$s"] = @() }
        $legacyMap["$s"] += [string]$path
    } else {
        $unreadable++
    }
}
# cache the legacy seq map so the hot path can resolve those files without re-reading them
try { Write-F8Text $p.Index (($legacyMap | ConvertTo-Json -Depth 4)) } catch { }
# fold them into this run's index too, so collision detection below sees legacy files as siblings
foreach ($k in $legacyMap.Keys) {
    $s = [int]$k
    if (-not $idx.BySeq.ContainsKey($s)) { $idx.BySeq[$s] = @() }
    foreach ($path in @($legacyMap[$k])) {
        if ($idx.BySeq[$s] -notcontains $path) { $idx.BySeq[$s] += $path }
    }
}

# -- 3) what was PROVABLY acked --------------------------------------------------------------------
$ack        = Get-F8AckState $Inbox
$wm         = [int]$ack.lastAckSeq
$ackedEvent = Get-F8AckedSeqsFromEvents $Inbox
foreach ($s in @($ack.acked)) { $ackedEvent[[int]$s] = $true }
$ackedFiles = @{}
foreach ($f in @($ack.ackedFiles)) { $ackedFiles[[string]$f] = $true }

# -- 4) classify ------------------------------------------------------------------------------------
$orphans  = @()
$preQueue = 0
$acked    = 0
$pendingAbove = 0
$dupSeqs  = @()

foreach ($f in $files) {
    $path = $f.FullName
    $leaf = $f.Name
    $s    = -1
    if ($seqOf.ContainsKey([string]$path)) { $s = [int]$seqOf[[string]$path] }

    if ($s -lt 0 -or $s -lt $queueFloor) { $preQueue++; continue }
    if ($ackedFiles.ContainsKey($leaf))  { $acked++; continue }

    # A sequence that names more than one FILE is the 2329 defect. Exactly one of them can be the
    # file a queue row points at; the rest were closed by an ack that no seat ever read them for.
    $siblings = @($idx.BySeq[$s])
    $isDup = ($siblings.Count -gt 1)
    $namedByQueue = $queueByLeaf.ContainsKey($leaf)

    if ($isDup -and (-not $namedByQueue)) {
        if ($dupSeqs -notcontains $s) { $dupSeqs += $s }
        $orphans += (New-F8OrphanRecord $s $leaf $path 'collided-seq')
        continue
    }
    if ($isDup) { if ($dupSeqs -notcontains $s) { $dupSeqs += $s } }

    if ($ackedEvent.ContainsKey($s)) { $acked++; continue }
    if ($s -gt $wm) { $pendingAbove++; continue }     # the normal above-watermark path already sees it
    $orphans += (New-F8OrphanRecord $s $leaf $path 'below-watermark-never-acked')
}

# queue rows whose capture file is gone -- reported, never silently dropped
$noFile = @()
foreach ($s in $queueBySeq.Keys) {
    if (-not $idx.BySeq.ContainsKey([int]$s)) { $noFile += [int]$s }
}

# -- 5) write the baseline ---------------------------------------------------------------------------
$out = @{
    sweptAtUtc       = (Get-Date).ToUniversalTime().ToString('o')
    watermarkAtSweep = $wm
    queueFloorSeq    = $queueFloor
    filesOnDisk      = $files.Count
    preQueueFiles    = $preQueue
    unreadableFiles  = $unreadable
    ackedFiles       = $acked
    pendingAbove     = $pendingAbove
    duplicateSeqs    = @($dupSeqs | Sort-Object)
    queueRowsNoFile  = @($noFile | Sort-Object)
    orphans          = @($orphans)
}
Write-F8Text $p.Backfill ($out | ConvertTo-Json -Depth 5)

foreach ($s in @($dupSeqs | Sort-Object)) {
    Write-Host ("F8_SWEEP_DUPSEQ seq={0} files={1}" -f $s, ((@($idx.BySeq[$s]) | ForEach-Object { Split-Path $_ -Leaf }) -join ' | '))
}
if ($noFile.Count -gt 0) {
    Write-Host ("F8_SWEEP_NOFILE {0} queue row(s) have no capture file on disk: {1}" -f $noFile.Count, ((@($noFile | Sort-Object) | Select-Object -First 20) -join ','))
}
if ($orphans.Count -gt 0) {
    Write-Host ''
    Write-Host ("BURIED CAPTURES - {0} file(s) were closed without ever being surfaced. They are now PENDING." -f $orphans.Count)
    foreach ($o in $orphans) {
        Write-Host ("  seq={0} [{1}] {2}" -f $o.seq, $o.reason, $o.file)
        Write-Host ("      {0}" -f $o.summary)
    }
    Write-Host '  Triage each with f8-check-inbox.ps1, then close it BY FILE: f8-ack.ps1 -File <name>'
    Write-F8Event $Inbox 'error' ("backfill sweep found $($orphans.Count) buried capture(s): " + ((@($orphans) | ForEach-Object { $_.file }) -join ', '))
}

Write-Host ("F8_SWEEP_OK files={0} orphans={1} preQueue={2} noFile={3} acked={4} pendingAbove={5} unreadable={6}" -f `
    $files.Count, $orphans.Count, $preQueue, $noFile.Count, $acked, $pendingAbove, $unreadable)
Write-F8Event $Inbox 'info' ("backfill sweep: files=$($files.Count) orphans=$($orphans.Count) preQueue=$preQueue floor=$queueFloor watermark=$wm")
exit 0
