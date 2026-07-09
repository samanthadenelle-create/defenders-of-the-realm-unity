# Returns exit 0 when a NEW F8 capture is waiting for triage (stdout = paths).
# Claude runs this each turn (see .cursor/rules/f8-auto-triage.mdc).
param([switch]$Quiet)

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
$PingFile = Join-Path $Inbox 'PING.json'
$AckFile  = Join-Path $Inbox 'ACK.json'
$Latest   = Join-Path $Inbox 'LATEST_CAPTURE.md'

if (-not (Test-Path $PingFile)) {
    if (-not $Quiet) { Write-Host 'NO_CAPTURE' }
    exit 1
}

$ping = Get-Content $PingFile -Raw | ConvertFrom-Json
$lastAck = 0
if (Test-Path $AckFile) {
    try { $lastAck = [int]((Get-Content $AckFile -Raw | ConvertFrom-Json).lastAckSeq) } catch { }
}

if ([int]$ping.seq -le $lastAck) {
    if (-not $Quiet) { Write-Host "NO_CAPTURE ack=$lastAck ping=$($ping.seq)" }
    exit 1
}

Write-Host 'NEW_CAPTURE'
Write-Host "seq=$($ping.seq)"
Write-Host "kind=$($ping.kind)"
Write-Host "firedAt=$($ping.firedAtUtc)"
Write-Host "latest=$Latest"
if ($ping.capturePath) { Write-Host "capture=$($ping.capturePath)" }
exit 0