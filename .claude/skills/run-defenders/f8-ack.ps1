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
param(
    [int]$Seq = 0,
    [string]$File = '',
    [switch]$All,
    [string]$InboxOverride = ''   # tests only
)

. (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')

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

    $targets = @()
    if (-not [string]::IsNullOrWhiteSpace($File)) {
        $leaf  = Split-Path $File -Leaf
        $match = @($pending | Where-Object { (Split-Path $_.capturePath -Leaf) -eq $leaf })
        if ($match.Count -eq 1) { $targets = @($match[0]) }
        else { $targets = @([pscustomobject]@{ seq = 0; kind = 'manual'; capturePath = $File; ackKey = ('file:' + $leaf) }) }
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
        else { $targets = @([pscustomobject]@{ seq = $Seq; kind = 'manual'; capturePath = ''; ackKey = "seq:$Seq" }) }
    } else {
        $targets = @($pending[0])   # OLDEST first - never the newest
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
