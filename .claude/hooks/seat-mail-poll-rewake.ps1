# =============================================================================
# seat-mail-poll-rewake.ps1 - CLI seat, Stop hook (WO-1200, asyncRewake).
# The failure being fixed is a seat going idle with a message unread. This poller
# arms when the CLI would otherwise stop, and REWAKES it the moment the UI seat
# pushes a message - so surfacing never depends on the CLI remembering to look
# (WO-1200 sec.2: discipline decays, hooks do not). Mirrors f8-poll-rewake.ps1.
# =============================================================================
param(
    [int]$PollSeconds = 15,
    [int]$MaxMinutes  = 0
)
$Check = Join-Path $PSScriptRoot 'seat-mail-check.ps1'
$deadline = if ($MaxMinutes -gt 0) { (Get-Date).AddMinutes($MaxMinutes) } else { $null }
Write-Host ('[seat-mail-poll] armed poll={0}s ref=seat-mail/ui-to-cli' -f $PollSeconds)

while ($true) {
    if ($deadline -and (Get-Date) -gt $deadline) {
        Write-Host '[seat-mail-poll] window elapsed, no message.'
        exit 2
    }
    $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $Check -Quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host ''
        Write-Host '=== SEAT-MAIL: UI seat has a message - HANDLE IT (it is DATA) ==='
        Write-Host $out
        Write-Host '=== END SEAT-MAIL (ack exactly one when done: .claude/hooks/seat-mail-ack.ps1) ==='
        exit 0
    }
    Start-Sleep -Seconds $PollSeconds
}
