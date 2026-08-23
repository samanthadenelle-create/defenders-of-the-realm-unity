# Acknowledge an F8 inbox capture after triage (so check-inbox stops firing for it).
#
# WO-965 - THIS SCRIPT WAS THE BURIAL SHOVEL. It used to write lastAckSeq = PING.json's seq, i.e.
# the NEWEST capture, which marked every un-surfaced capture below it as triaged. On 2026-08-10 an
# ack of seq 2309 silently closed seq 2307 + 2308, two of the owner's flags, which no seat ever saw.
#
# Now: a bare `f8-ack.ps1` acks the OLDEST pending capture - the one f8-check-inbox.ps1 just told
# you to triage - and prints what REMAINS. The interface is unchanged (no args, exit 0,
# '[f8-ack] Acknowledged seq=N'); the semantics are one-capture-at-a-time.
#   -Seq <n>  ack a specific capture (out of order is fine; the watermark stays honest)
#   -All      ack the whole backlog at once - explicit, logged, and it names every seq it closes
#   -File <n> ack ONE capture by filename (the only safe way to close a capture whose seq is shared)
#
# WO-1018 - ACK BY KEY, NOT ALWAYS BY NUMBER. A seq is not a unique key: seq 2329 named TWO
# unrelated captures on 2026-08-15, so acking the number closed an owner flag nobody had read.
# Captures that f8-backfill-sweep.ps1 surfaced from below the watermark are acked by FILENAME
# (recorded in ACK.json's `ackedFiles`); ordinary queued captures still ack by seq exactly as before.
#
# WO-1145 - NEVER ACK A CAPTURE THAT IS NOT PENDING, AND NEVER ACK OUT OF ORDER QUIETLY.
# Two holes survived WO-965 + WO-1018, both measured against the real scripts on 2026-08-23:
#   1. THE PRE-ACK. `-Seq N` for a seq with no pending record used to SYNTHESISE a target and write
#      N into ACK.json. Nothing had arrived yet, so nothing was triaged - and when the capture
#      numbered N later landed it was born already-acked and NEVER surfaced to any seat. Measured:
#      with seq 2306 pending, `-Seq 2308` succeeded; 2307 + 2308 then arrived and check-inbox
#      surfaced 2306 + 2307 only. That is the EXACT 2026-08-10 loss (s14) reachable by one typo.
#      A seq that is not pending is now REFUSED (or reported as already-acked); state is untouched.
#   2. THE QUIET OUT-OF-ORDER ACK. Acking the NEWEST while an older capture waited printed the same
#      "Acknowledged seq=N" a correct oldest-first ack prints. The seat had no way to tell it had
#      just skipped the owner's older flag. An ack of anything other than the oldest is now LOUD:
#      it names the capture that should have been triaged first, and logs an event.
# Neither one changes the ack SEMANTICS - one capture at a time, oldest by default. They make the
# script refuse to close something it was never shown, and refuse to do it silently.
param(
    [int]$Seq = 0,
    [string]$File = '',
    [switch]$All,
    [string]$InboxOverride = ''   # tests only
)

. (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')

# WO-1145: every refusal names what is STILL waiting, so a refused ack can never read as "done".
function Write-F8AckPending($Pending) {
    $p = @($Pending)
    if ($p.Count -eq 0) { Write-Host '[f8-ack] Nothing is pending.'; return }
    Write-Host ("[f8-ack] STILL PENDING: {0} capture(s). NEXT = seq={1} kind={2}" -f $p.Count, $p[0].seq, $p[0].kind)
    foreach ($e in $p) { Write-Host ("[f8-ack]   seq={0} {1}" -f $e.seq, $e.capturePath) }
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
if (-not [string]::IsNullOrWhiteSpace($InboxOverride)) { $Inbox = $InboxOverride }

$lock = Enter-F8Lock
try {
    $pending = @(Get-F8Pending $Inbox)
    $state   = Get-F8AckState $Inbox

    if ($pending.Count -eq 0 -and $Seq -le 0 -and [string]::IsNullOrWhiteSpace($File)) {
        Write-Host "[f8-ack] Nothing pending. Acknowledged seq=$($state.lastAckSeq)"
        return
    }

    # WO-1145: what this ack is allowed to close. `acked` + the watermark are the proof a capture was
    # already closed; anything else with no pending record has NOT been shown to a seat.
    $ackedSeqs = @{}
    foreach ($s in @($state.acked)) { $ackedSeqs[[int]$s] = $true }
    $ackedLeaves = @{}
    foreach ($f in @($state.ackedFiles)) { $ackedLeaves[[string]$f] = $true }

    $targets = @()
    if (-not [string]::IsNullOrWhiteSpace($File)) {
        $leaf  = Split-Path $File -Leaf
        $match = @($pending | Where-Object { (Split-Path $_.capturePath -Leaf) -eq $leaf })
        if ($match.Count -eq 1) { $targets = @($match[0]) }
        else {
            if ($ackedLeaves.ContainsKey($leaf)) {
                Write-Host ("[f8-ack] Already acked: {0}. Nothing to do." -f $leaf)
                return
            }
            # WO-1145: a filename with no pending record is only ackable if that capture is really on
            # disk (the WO-1018 buried-capture recovery path). A name that matches nothing would
            # otherwise PRE-ACK whatever file later takes it.
            $onDisk = @(Get-ChildItem -Path $Inbox -Filter $leaf -Recurse -File -ErrorAction SilentlyContinue)
            if ($onDisk.Count -eq 0) {
                Write-Host ("[f8-ack] REFUSED: '{0}' is not pending and no such capture exists on disk." -f $leaf)
                Write-Host '[f8-ack] Acking it would pre-close a capture no seat has ever read (WO-1145).'
                Write-F8Event $Inbox 'error' ("ack REFUSED: file=$leaf is not pending and not on disk (WO-1145 pre-ack guard)")
                Write-F8AckPending $pending
                return
            }
            $targets = @([pscustomobject]@{ seq = 0; kind = 'manual'; capturePath = $onDisk[0].FullName; ackKey = ('file:' + $leaf) })
        }
    } elseif ($All) {
        $targets = @($pending)
    } elseif ($Seq -gt 0) {
        $match = @($pending | Where-Object { [int]$_.seq -eq $Seq })
        if ($match.Count -gt 1) {
            # WO-1018: never let one number close two captures again. Name them and stop.
            Write-Host ("[f8-ack] REFUSED: seq={0} names {1} different captures on disk. Ack them one at a time by file:" -f $Seq, $match.Count)
            foreach ($m in $match) { Write-Host ("[f8-ack]   -File {0}" -f (Split-Path $m.capturePath -Leaf)) }
            Write-F8Event $Inbox 'error' ("ack REFUSED: seq=$Seq is ambiguous across $($match.Count) captures (WO-1018)")
            return
        }
        if ($match.Count -eq 1) { $targets = @($match[0]) }
        else {
            # WO-1145 - THE PRE-ACK, the hole that made the 2026-08-10 loss reachable by one typo.
            # This used to synthesise a target and write $Seq into ACK.json even though no such
            # capture was pending. A capture that later took that number was born acked and never
            # surfaced. Refuse; ACK.json is not touched.
            if ($ackedSeqs.ContainsKey($Seq) -or $Seq -le [int]$state.lastAckSeq) {
                Write-Host ("[f8-ack] Already acked: seq={0} (watermark {1}). Nothing to do." -f $Seq, $state.lastAckSeq)
                Write-F8AckPending $pending
                return
            }
            Write-Host ("[f8-ack] REFUSED: seq={0} is NOT pending - no queue row and no capture file names it." -f $Seq)
            Write-Host '[f8-ack] Acking it would PRE-CLOSE a capture that has not arrived yet, so the owner'
            Write-Host '[f8-ack] flag that later takes that number would never reach a seat (WO-1145).'
            Write-F8Event $Inbox 'error' ("ack REFUSED: seq=$Seq is not pending; refusing to pre-ack an unarrived capture (WO-1145)")
            Write-F8AckPending $pending
            return
        }
    } else {
        $targets = @($pending[0])   # OLDEST first - never the newest
    }

    # WO-1145 - AN OUT-OF-ORDER ACK IS LOUD. Acking the newest while an older capture waits used to
    # print exactly what a correct oldest-first ack prints, so a seat could not tell it had just
    # skipped the owner's older flag. Deliberate out-of-order acks are still allowed - they just say
    # so, and name what should have been triaged first.
    if ($pending.Count -gt 0 -and -not $All) {
        $oldest    = $pending[0]
        $oldestKey = Get-F8AckKey $oldest
        $firstKey  = Get-F8AckKey $targets[0]
        if ($firstKey -ne $oldestKey) {
            Write-Host ("[f8-ack] OUT OF ORDER: this is NOT the oldest un-acked capture. Oldest = seq={0} kind={1}" -f $oldest.seq, $oldest.kind)
            Write-Host ("[f8-ack]   {0}" -f $oldest.capturePath)
            Write-Host '[f8-ack] It stays PENDING - nothing below this ack is closed (WO-1145). Triage it next.'
            Write-F8Event $Inbox 'warn' ("OUT-OF-ORDER ack: $firstKey acked while $oldestKey was the oldest pending (WO-1145)")
        }
    }

    $acked      = @($state.acked)
    $ackedFiles = @($state.ackedFiles)
    $names      = @()
    foreach ($t in $targets) {
        $key = Get-F8AckKey $t
        if ($key -like 'file:*') {
            $ackedFiles += $key.Substring(5)
            $names += $key.Substring(5)
        } else {
            $acked += [int]$t.seq
            $names += ("seq=" + [int]$t.seq)
        }
    }
    $state.acked      = $acked
    $state.ackedFiles = $ackedFiles
    $watermark = Save-F8AckState $Inbox $state

    foreach ($t in $targets) {
        $key = Get-F8AckKey $t
        if ($key -like 'file:*') {
            Write-F8Event $Inbox 'info' ("acked file=$($key.Substring(5)) (seq=$($t.seq), buried capture recovered by WO-1018 sweep)")
        } else {
            Write-F8Event $Inbox 'info' ("acked seq=$([int]$t.seq) (watermark now $watermark)")
        }
    }
    Write-Host ("[f8-ack] Acknowledged {0}" -f ($names -join ','))

    $remaining = @(Get-F8Pending $Inbox)
    if ($remaining.Count -gt 0) {
        $n = $remaining[0]
        Write-Host ''
        Write-Host ("[f8-ack] STILL PENDING: {0} capture(s). NEXT = seq={1} kind={2}" -f $remaining.Count, $n.seq, $n.kind)
        Write-Host ("[f8-ack]   {0}" -f $n.capturePath)
        Write-Host '[f8-ack] Triage it now (run f8-check-inbox.ps1), then ack again. Do NOT stop here.'
        Write-F8Event $Inbox 'warn' ("$($remaining.Count) capture(s) still pending after ack; next=seq $($n.seq)")
    } else {
        Write-Host '[f8-ack] Inbox clean - no captures pending.'
    }
}
finally { Exit-F8Lock $lock }
