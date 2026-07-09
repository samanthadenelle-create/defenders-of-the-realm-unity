# Acknowledge the latest F8 inbox ping after triage (so check-inbox stops firing).
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
$PingFile = Join-Path $Inbox 'PING.json'
$AckFile  = Join-Path $Inbox 'ACK.json'

$seq = 0
if (Test-Path $PingFile) {
    $seq = [int]((Get-Content $PingFile -Raw | ConvertFrom-Json).seq)
}
@{ lastAckSeq = $seq; ackedAtUtc = (Get-Date).ToUniversalTime().ToString('o') } |
    ConvertTo-Json | Set-Content -Path $AckFile -Encoding UTF8
Write-Host "[f8-ack] Acknowledged seq=$seq"