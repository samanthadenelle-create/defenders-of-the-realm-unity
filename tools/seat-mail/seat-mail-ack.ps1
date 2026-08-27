# seat-mail-ack.ps1 -- acknowledge EXACTLY ONE seat message (WO-1200).
#
# STOP: ONE. NOT "EVERYTHING UP TO HERE". The F8 inbox used a high watermark, and an ack of
# a newer sequence closed every older one under it -- two of the owner's captures were lost
# that way on 2026-08-10 and needed a whole backfill sweep (WO-1018) to be found again. Ack
# state here is a SET of sequences, so acking seq 7 leaves seq 5 pending and visible.
#
# With no -Seq, acks the OLDEST un-acked (the one seat-mail-check.ps1 just showed you).
# ASCII-only.
param([int]$Seq = 0, [string]$RootOverride = '')

$ErrorActionPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'seat-mail-lib.ps1')

$Root = Get-SeatMailRoot $RootOverride
if (-not (Test-Path $Root)) { Write-Output 'SEAT_MAIL_ABSENT -- nothing to acknowledge.'; exit 0 }

$pending = @(Get-SeatMailPending $Root)
if ($pending.Count -eq 0) { Write-Output 'pending=0'; Write-Output 'NO_MAIL'; exit 0 }

$target = $null
if ($Seq -gt 0) { $target = $pending | Where-Object { [int]$_.seq -eq $Seq } | Select-Object -First 1 }
else           { $target = $pending[0] }

if ($null -eq $target) {
    Write-Output (('SEAT_MAIL_ACK_FAIL -- seq={0} is not among the {1} un-acked message(s). ' +
                   'Run seat-mail-check.ps1; do not guess a sequence.') -f $Seq, $pending.Count)
    exit 1
}

$acked = Get-SeatMailAcked $Root
$acked[[int]$target.seq] = $true
Save-SeatMailAcked $Root $acked

$left = @(Get-SeatMailPending $Root).Count
Write-SeatMailTrace $Root 'ack' ([int]$target.seq) ('remaining={0}' -f $left)
Write-Output ('SEAT_MAIL_ACKED seq={0} kind={1}' -f $target.seq, $target.kind)
Write-Output ('pending={0}' -f $left)
if ($left -gt 0) { Write-Output 'STILL QUEUED -- keep going until seat-mail-check.ps1 says NO_MAIL.' }
exit 0
