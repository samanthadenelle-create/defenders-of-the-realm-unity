# f8-prompt-check.ps1 -- turn-start F8 surfacing (owner directive 2026-08-10).
# UserPromptSubmit hook: if a new (un-acked) F8 capture is waiting, inject it as
# additionalContext so the seat triages it FIRST (CLAUDE.md section 14) before the
# user's request. Silent (no output, exit 0) when the inbox is clean -- a chatty
# no-op would pollute every turn.
#
# WO-965: surfaces the WHOLE un-acked backlog (oldest first), not just PING.json's newest capture.
param([string]$InboxOverride = '')

$ErrorActionPreference = 'SilentlyContinue'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Inbox    = if ($InboxOverride) { $InboxOverride } else { Join-Path $RepoRoot 'logs\f8-inbox' }
$PingFile = Join-Path $Inbox 'PING.json'
$AckFile  = Join-Path $Inbox 'ACK.json'
$Latest   = Join-Path $Inbox 'LATEST_CAPTURE.md'
$Lib      = Join-Path $RepoRoot '.claude\skills\run-defenders\f8-inbox-lib.ps1'

if (-not (Test-Path $PingFile)) { exit 0 }
$ping = $null
try { $ping = Get-Content $PingFile -Raw | ConvertFrom-Json } catch { exit 0 }
if (-not $ping) { exit 0 }

$lastAck = 0
if (Test-Path $AckFile) {
    try { $lastAck = [int]((Get-Content $AckFile -Raw | ConvertFrom-Json).lastAckSeq) } catch { }
}
# lastAckSeq is the CONTIGUOUS watermark, so a gap below the newest seq still reads as pending here
# -- this comparison is the same one it always was, and it is still correct.
if ([int]$ping.seq -le $lastAck) { exit 0 }

$pending = @()
if (Test-Path $Lib) { . $Lib ; $pending = @(Get-F8Pending $Inbox) }

if ($pending.Count -eq 0) {
    $ctx = "UNACKNOWLEDGED F8 CAPTURE waiting: seq=$($ping.seq) kind=$($ping.kind) firedAt=$($ping.firedAtUtc). " +
           "Per CLAUDE.md section 14, read $Latest FIRST and triage from the harvested lines before other work; ack with f8-ack.ps1 after triage."
} else {
    $first = $pending[0]
    $list = ($pending | ForEach-Object { "seq=$($_.seq) kind=$($_.kind) file=$($_.capturePath) :: $($_.summary)" }) -join ' | '
    $ctx = "UNACKNOWLEDGED F8 CAPTURES waiting: $($pending.Count) queued (oldest first). NEXT = seq=$($first.seq) kind=$($first.kind) -> $($first.capturePath). " +
           "Per CLAUDE.md section 14, read that capture file FIRST (LATEST_CAPTURE.md only holds the NEWEST) and triage from the harvested lines before other work. " +
           "Ack ONE at a time with f8-ack.ps1 and keep going until f8-check-inbox.ps1 reports NO_CAPTURE. BACKLOG: $list"
}
@{ hookSpecificOutput = @{ hookEventName = 'UserPromptSubmit'; additionalContext = $ctx } } | ConvertTo-Json -Compress -Depth 4
exit 0
