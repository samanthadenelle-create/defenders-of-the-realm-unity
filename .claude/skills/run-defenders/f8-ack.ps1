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
param(
    [int]$Seq = 0,
    [switch]$All
)

. (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'

$lock = Enter-F8Lock
try {
    $pending = @(Get-F8Pending $Inbox)
    $state   = Get-F8AckState $Inbox

    if ($pending.Count -eq 0 -and $Seq -le 0) {
        Write-Host "[f8-ack] Nothing pending. Acknowledged seq=$($state.lastAckSeq)"
        return
    }

    $targets = @()
    if ($All) {
        $targets = @($pending | ForEach-Object { [int]$_.seq })
    } elseif ($Seq -gt 0) {
        $targets = @($Seq)
    } else {
        $targets = @([int]$pending[0].seq)   # OLDEST first - never the newest
    }

    $acked = @($state.acked)
    foreach ($t in $targets) { $acked += [int]$t }
    $state.acked = $acked
    $watermark = Save-F8AckState $Inbox $state

    foreach ($t in $targets) {
        Write-F8Event $Inbox 'info' ("acked seq=$t (watermark now $watermark)")
    }
    Write-Host ("[f8-ack] Acknowledged seq={0}" -f ($targets -join ','))

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
