# Returns exit 0 when one or more UN-ACKED F8 captures are waiting for triage (stdout = paths).
# Claude runs this each turn (see .cursor/rules/f8-auto-triage.mdc + .claude/settings.json hooks).
#
# WO-965: this used to compare PING.json's seq to ACK.json and surface ONLY the newest capture.
# A burst therefore surfaced its last member and the ack buried the rest (2026-08-10: seq 2307 and
# 2308 never reached any seat). It now walks QUEUE.jsonl and surfaces EVERY un-acked capture,
# OLDEST FIRST. Contract preserved exactly:
#   exit 0 + 'NEW_CAPTURE' when work is waiting, exit 1 + 'NO_CAPTURE' when clean;
#   seq= / kind= / firedAt= / latest= / capture= lines still printed.
# What changed: seq=/kind=/capture= now name the OLDEST pending capture (the one to triage NEXT),
# and pending= / PENDING lines list the rest. latest= still points at LATEST_CAPTURE.md.
param([switch]$Quiet, [string]$InboxOverride = '')   # -InboxOverride: tests only

. (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
if (-not [string]::IsNullOrWhiteSpace($InboxOverride)) { $Inbox = $InboxOverride }
$PingFile = Join-Path $Inbox 'PING.json'
$AckFile  = Join-Path $Inbox 'ACK.json'
$Latest   = Join-Path $Inbox 'LATEST_CAPTURE.md'

if (-not (Test-Path $PingFile)) {
    if (-not $Quiet) { Write-Host 'NO_CAPTURE' }
    exit 1
}

$ack     = Get-F8AckState $Inbox
$pending = @(Get-F8Pending $Inbox)

# WO-1018 -- 'CLEAN' MUST BE PROVEN, NOT ASSUMED. Get-F8Pending only ever looked ABOVE the ack
# watermark, so a capture buried underneath it read as clean forever. Before printing NO_CAPTURE we
# reconcile against the capture files actually on disk; if any un-acked file exists, or the inbox
# has never been swept (so nothing under the watermark has ever been reconciled), we say so LOUDLY
# and do not claim the inbox is clean.
if ($pending.Count -eq 0) {
    $ping  = Get-F8PingSeq $Inbox
    $state = Test-F8InboxClean $Inbox

    if (@($state.Unacked).Count -gt 0) {
        Write-Host 'NEW_CAPTURE'
        Write-Host ("seq={0}" -f @($state.Unacked)[0])
        Write-Host 'kind=on-disk-unacked'
        Write-Host "latest=$Latest"
        Write-Host ("capture={0}" -f (Resolve-F8CaptureFile $Inbox ([int]@($state.Unacked)[0])))
        Write-Host ("pending={0}" -f @($state.Unacked).Count)
        Write-Host ''
        Write-Host ("ERROR_UNRECONCILED {0} capture file(s) on disk are above the ack watermark ({1}) and NOT acked: {2}" -f @($state.Unacked).Count, $ack.lastAckSeq, (@($state.Unacked) -join ','))
        Write-Host 'ERROR_UNRECONCILED The queue and the disk disagree. Run f8-backfill-sweep.ps1 before trusting any ack.'
        exit 0
    }

    if (-not $state.Swept) {
        Write-Host ("WARN_NO_SWEEP inbox has NEVER been reconciled below the watermark ({0} capture files on disk, {1} pre-queue). Nothing under ack={2} has been proven triaged." -f $state.Files, $state.Legacy, $ack.lastAckSeq)
        Write-Host 'WARN_NO_SWEEP Run: powershell -File .claude\skills\run-defenders\f8-backfill-sweep.ps1'
    }
    if (-not $Quiet) { Write-Host "NO_CAPTURE ack=$($ack.lastAckSeq) ping=$ping" }
    exit 1
}

$next = $pending[0]
$nextPath = $next.capturePath
if ([string]::IsNullOrWhiteSpace($nextPath) -or -not (Test-Path $nextPath)) { $nextPath = $Latest }

Write-Host 'NEW_CAPTURE'
Write-Host "seq=$($next.seq)"
Write-Host "kind=$($next.kind)"
Write-Host "firedAt=$($next.utc)"
Write-Host "latest=$Latest"
Write-Host "capture=$nextPath"
Write-Host "pending=$($pending.Count)"

if ($pending.Count -gt 1) {
    Write-Host ''
    Write-Host "BACKLOG - $($pending.Count) un-acked captures. TRIAGE OLDEST FIRST; f8-ack.ps1 acks ONE at a time."
    foreach ($e in $pending) {
        $p = $e.capturePath
        if ([string]::IsNullOrWhiteSpace($p)) { $p = '(no capture file)' }
        Write-Host ("  seq={0} kind={1} {2}" -f $e.seq, $e.kind, $p)
        Write-Host ("      {0}" -f $e.summary)
    }
}

# LOUD, never silent: a pending seq with no queue entry means a producer did not queue it (an old
# daemon process) or the capture content is gone. Get-F8Pending has already written queue-events.log.
$orphans = @($pending | Where-Object { $_.unqueued })
if ($orphans.Count -gt 0) {
    Write-Host ''
    Write-Host ("WARN_UNQUEUED {0} capture(s) had no QUEUE.jsonl entry: {1}" -f $orphans.Count, (($orphans | ForEach-Object { $_.seq }) -join ','))
    Write-Host 'WARN_UNQUEUED cause: a producer started before WO-965 is still running. Restart it: f8-watch-stop.ps1 then f8-watch-start.ps1.'
    $lost = @($orphans | Where-Object { [string]::IsNullOrWhiteSpace($_.capturePath) })
    if ($lost.Count -gt 0) {
        Write-Host ("ERROR_LOST_CAPTURE seq(s) {0} have NO capture file - content unrecoverable. Tell the owner." -f (($lost | ForEach-Object { $_.seq }) -join ','))
    }
}
exit 0
