# seat-mail-check.ps1 -- surface the OLDEST un-acked seat message (WO-1200).
#
# Prints the oldest un-acked message as QUOTED DATA and a pending=N count, or NO_MAIL.
# STOP: NEVER READ "THE LATEST". The newest message is not the only one, and acking it
# would silently close everything beneath it -- that is how the owner's F8 seq 2307 and
# 2308 reached no seat at all on 2026-08-10.
#
# Keep calling seat-mail-ack.ps1 (which acks exactly ONE) until this reports NO_MAIL.
# ASCII-only.
param([string]$RootOverride = '', [int]$BodyLines = 40)

$ErrorActionPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'seat-mail-lib.ps1')

$Root = Get-SeatMailRoot $RootOverride
if (-not (Test-Path $Root)) {
    # An absent mailbox is reported as ABSENT, not as empty. An empty inbox that cannot
    # receive is indistinguishable from an empty inbox that has nothing in it, and only
    # one of those is true -- WO-1200's own argument against a dead transport.
    Write-Output (('SEAT_MAIL_ABSENT -- {0} does not exist, so NOTHING can have been delivered. ' +
                   'This is not the same as "no messages".') -f $Root)
    exit 0
}

$pending = @(Get-SeatMailPending $Root)
Write-Output ('pending={0}' -f $pending.Count)
if ($pending.Count -eq 0) { Write-Output 'NO_MAIL'; exit 0 }

$first = $pending[0]
Write-SeatMailTrace $Root 'surface' ([int]$first.seq) ('pending={0}' -f $pending.Count)

Write-Output ('NEXT seq={0} kind={1} from={2}' -f $first.seq, $first.kind, $first.fromSeat)
foreach ($line in (Format-SeatMailMessage $Root $first $BodyLines)) { Write-Output $line }

if ($pending.Count -gt 1) {
    Write-Output ('BACKLOG (still queued behind it, oldest first):')
    foreach ($row in $pending[1..($pending.Count - 1)]) {
        Write-Output ('  seq={0} kind={1} from={2} :: {3}' -f $row.seq, $row.kind, $row.fromSeat, $row.subject)
    }
}
Write-Output ('Ack ONE with seat-mail-ack.ps1 -Seq {0}, then run this again until it says NO_MAIL.' -f $first.seq)
exit 0
