# =============================================================================
# seat-mail-check.ps1 - CLI seat: is a message from the UI seat waiting?
# WO-1200 return path (UI -> CLI). Mirrors f8-check-inbox.ps1 but QUEUE-based:
# surfaces the OLDEST un-acked message (never "the latest" - the F8 loss bug).
# Exit 0 => a message is pending (stdout = the framed message). Exit 1 => empty.
# Read-only against the fetched ref; only the local cursor is written (by ack).
# =============================================================================
param([switch]$Quiet)

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Ref      = 'seat-mail/ui-to-cli'
$Py       = Join-Path $RepoRoot 'seat-mail\seatmail.py'
$Cursor   = Join-Path $RepoRoot '.claude\seat-mail-cursor.json'
$Tmp      = Join-Path $env:TEMP ('seatmail_queue_{0}.jsonl' -f $PID)

# Fetch the ref the UI seat pushes to (best-effort; offline => read last fetch).
& git -C $RepoRoot fetch -q origin $Ref 2>$null

# Materialize QUEUE.jsonl from the ref (read-only). Empty file if the ref/queue is absent.
$queue = & git -C $RepoRoot show ("origin/{0}:QUEUE.jsonl" -f $Ref) 2>$null
if ($LASTEXITCODE -ne 0 -or $null -eq $queue) { '' | Set-Content -Path $Tmp -Encoding ascii }
else { $queue | Set-Content -Path $Tmp -Encoding ascii }

# Single-source queue logic (python3 is present on this box - CLAUDE.md sec.1 gate).
$out = & python3 $Py surface --queue $Tmp --cursor $Cursor 2>$null
$code = $LASTEXITCODE
Remove-Item $Tmp -ErrorAction SilentlyContinue

if ($code -eq 0) {
    Write-Host $out
    exit 0
}
if (-not $Quiet) { Write-Host 'SEATMAIL_EMPTY' }
exit 1
