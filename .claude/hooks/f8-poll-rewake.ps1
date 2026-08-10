# f8-poll-rewake.ps1 -- the passive F8 listener (owner directive 2026-08-10).
# Launched by the Stop hook (asyncRewake) every time a Claude Code seat goes idle:
# polls the F8 inbox every $IntervalSec seconds in the background and EXITS 2 the
# moment a new capture lands -- exit 2 re-wakes the model with this script's output,
# so the seat triages the capture without the owner ever saying "f8".
#
# Single-instance across ALL seats sharing this repo: a repo-level lock file makes
# exactly one poller the triage owner (mirrors the one-committer model). A poller
# whose PID is dead is stale and its lock is taken over.
#
# History: the .cursor/rules/f8-auto-triage.mdc "poll every turn" rule worked while
# seats obeyed it, then quietly stopped being implemented (owner, 2026-08-10). This
# version is executed by the HARNESS via .claude/settings.json, not by discipline.
param(
    [int]$IntervalSec = 10,
    [int]$MaxLoops = 300,            # ~50 min at 10s; Stop fires again on the next idle
    [string]$InboxOverride = ''      # tests only
)

$ErrorActionPreference = 'SilentlyContinue'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Inbox    = if ($InboxOverride) { $InboxOverride } else { Join-Path $RepoRoot 'logs\f8-inbox' }
$PingFile = Join-Path $Inbox 'PING.json'
$AckFile  = Join-Path $Inbox 'ACK.json'
$Latest   = Join-Path $Inbox 'LATEST_CAPTURE.md'
$LockFile = Join-Path $Inbox 'poll-rewake.lock'
$Lib      = Join-Path $RepoRoot '.claude\skills\run-defenders\f8-inbox-lib.ps1'

# -- single instance: first live poller wins the triage-owner seat ------------
if (Test-Path $LockFile) {
    $lockPid = 0
    try { $lockPid = [int](Get-Content $LockFile -Raw).Trim() } catch { }
    if ($lockPid -gt 0 -and (Get-Process -Id $lockPid -ErrorAction SilentlyContinue)) {
        exit 0   # another seat's poller is alive -- stay quiet
    }
}
New-Item -ItemType Directory -Force (Split-Path $LockFile) | Out-Null
Set-Content -Path $LockFile -Value $PID -Encoding ascii

try {
    for ($i = 0; $i -lt $MaxLoops; $i++) {
        if (Test-Path $PingFile) {
            $ping = $null
            try { $ping = Get-Content $PingFile -Raw | ConvertFrom-Json } catch { }
            if ($ping) {
                $lastAck = 0
                if (Test-Path $AckFile) {
                    try { $lastAck = [int]((Get-Content $AckFile -Raw | ConvertFrom-Json).lastAckSeq) } catch { }
                }
                if ([int]$ping.seq -gt $lastAck) {
                    # WO-965: wake with the FULL backlog, oldest first. LATEST_CAPTURE.md holds only
                    # the newest capture, so waking on it alone is how seq 2307/2308 were lost.
                    $pending = @()
                    if (Test-Path $Lib) { . $Lib ; $pending = @(Get-F8Pending $Inbox) }

                    if ($pending.Count -gt 0) {
                        $first = $pending[0]
                        Write-Output "NEW F8 CAPTURE(S) (passive listener): $($pending.Count) un-acked, oldest first."
                        foreach ($e in $pending) {
                            Write-Output ("  seq={0} kind={1} file={2}" -f $e.seq, $e.kind, $e.capturePath)
                            Write-Output ("      {0}" -f $e.summary)
                        }
                        Write-Output "Triage seq=$($first.seq) FIRST (CLAUDE.md section 14: harvest before theory) from $($first.capturePath), then f8-ack.ps1 (acks ONE), and repeat until f8-check-inbox.ps1 says NO_CAPTURE."
                        $head = $first.capturePath
                        if ([string]::IsNullOrWhiteSpace($head) -or -not (Test-Path $head)) { $head = $Latest }
                        if (Test-Path $head) {
                            Write-Output ('--- {0} (head) ---' -f (Split-Path $head -Leaf))
                            Get-Content $head -TotalCount 20 | ForEach-Object { Write-Output $_ }
                        }
                    } else {
                        Write-Output "NEW F8 CAPTURE (passive listener) seq=$($ping.seq) kind=$($ping.kind) firedAt=$($ping.firedAtUtc)"
                        Write-Output "Read $Latest FIRST (CLAUDE.md section 14: harvest before theory), triage from the captured lines, then run f8-ack.ps1."
                        if (Test-Path $Latest) {
                            Write-Output '--- LATEST_CAPTURE.md (head) ---'
                            Get-Content $Latest -TotalCount 20 | ForEach-Object { Write-Output $_ }
                        }
                    }
                    exit 2   # asyncRewake: exit 2 wakes the model with the lines above
                }
            }
        }
        Start-Sleep -Seconds $IntervalSec
    }
    exit 0   # quiet window ended; the next Stop re-arms
}
finally {
    # release only OUR lock (a takeover may have replaced it)
    try {
        if ((Get-Content $LockFile -Raw -ErrorAction SilentlyContinue).Trim() -eq "$PID") {
            Remove-Item $LockFile -Force -ErrorAction SilentlyContinue
        }
    } catch { }
}
