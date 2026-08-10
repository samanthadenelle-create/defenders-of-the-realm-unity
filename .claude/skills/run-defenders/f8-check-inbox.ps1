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
param([switch]$Quiet)

. (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
$PingFile = Join-Path $Inbox 'PING.json'
$AckFile  = Join-Path $Inbox 'ACK.json'
$Latest   = Join-Path $Inbox 'LATEST_CAPTURE.md'

if (-not (Test-Path $PingFile)) {
    if (-not $Quiet) { Write-Host 'NO_CAPTURE' }
    exit 1
}

$ack     = Get-F8AckState $Inbox
$pending = @(Get-F8Pending $Inbox)

if ($pending.Count -eq 0) {
    $ping = Get-F8PingSeq $Inbox
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
