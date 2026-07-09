# Agent-side inbox poller - exits on first un-acked F8 capture (for background notify).
# Pair with f8-watch-daemon.ps1 (persistent, never exits). Re-launch after each triage.
param(
    [int]$PollSeconds = 5,
    [int]$MaxMinutes = 0
)

$Check = Join-Path $PSScriptRoot 'f8-check-inbox.ps1'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Latest = Join-Path $RepoRoot 'logs\f8-inbox\LATEST_CAPTURE.md'

$deadline = if ($MaxMinutes -gt 0) { (Get-Date).AddMinutes($MaxMinutes) } else { $null }

Write-Host ('[f8-poll] armed poll={0}s inbox={1}' -f $PollSeconds, $Latest)

while ($true) {
    if ($deadline -and (Get-Date) -gt $deadline) {
        Write-Host '[f8-poll] window elapsed, no new capture.'
        exit 2
    }

    & $Check 2>&1 | ForEach-Object { $_ }
    if ($LASTEXITCODE -eq 0) {
        Write-Host ''
        Write-Host '=== F8 INBOX PING - TRIAGE NOW (read harvested context FIRST) ==='
        if (Test-Path $Latest) {
            Write-Host ''
            Get-Content $Latest -Raw
        }
        Write-Host '=== END F8 PING ==='
        exit 0
    }

    Start-Sleep -Seconds $PollSeconds
}