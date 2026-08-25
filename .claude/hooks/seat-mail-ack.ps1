# =============================================================================
# seat-mail-ack.ps1 - CLI seat: acknowledge EXACTLY ONE message (WO-1200).
# Advances the local cursor to the OLDEST un-acked seq only - never to "the
# latest" (that was the F8 loss bug: acking the newest silently closed every
# message beneath it). Run this AFTER acting on the surfaced message. Idempotent
# when the box is empty. The cursor is CLI-local (.claude/seat-mail-cursor.json,
# gitignored) - acking does not push anything.
# =============================================================================
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Ref    = 'seat-mail/ui-to-cli'
$Py     = Join-Path $RepoRoot 'seat-mail\seatmail.py'
$Cursor = Join-Path $RepoRoot '.claude\seat-mail-cursor.json'
$Tmp    = Join-Path $env:TEMP ('seatmail_ack_{0}.jsonl' -f $PID)

& git -C $RepoRoot fetch -q origin $Ref 2>$null
$queue = & git -C $RepoRoot show ("origin/{0}:QUEUE.jsonl" -f $Ref) 2>$null
if ($LASTEXITCODE -ne 0 -or $null -eq $queue) { '' | Set-Content -Path $Tmp -Encoding ascii }
else { $queue | Set-Content -Path $Tmp -Encoding ascii }

& python3 $Py ack --queue $Tmp --cursor $Cursor
Remove-Item $Tmp -ErrorAction SilentlyContinue
