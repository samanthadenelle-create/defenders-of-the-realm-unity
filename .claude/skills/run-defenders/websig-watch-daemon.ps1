# =============================================================================
# websig-watch-daemon.ps1 - the WEB half of the F8 live-triage watcher (CLAUDE.md §14).
# -----------------------------------------------------------------------------
# WHY THIS EXISTS (2026-07-15, the magenta-ground post-mortem):
# The deployed WebGL build streams FlowTrace to Neon (WebTrace: FeatureFlags.cs:117
# defaultOn=TRUE + WebTrace.cs TraceEndpoint set = BOTH gates open). On 2026-07-15 a real
# session recorded, at error level:
#     TERRAINDIAG 'ExteriorTerrain' material='NULL' shader='NULL'
#     [Flow:MagentaGuard] recovered MAGENTA renderer ...   (x8)
# and it sat unread for a DAY until the owner spotted the magenta ground WITH HER EYES —
# a direct violation of §14 ("the owner is NEVER the bug detector"). f8-watch-daemon.ps1
# tails the LOCAL Editor/Player logs and structurally cannot see this: a web player's logs
# are not on this machine. This daemon closes that hole.
#
# ── WHY THE DB AND NOT `vercel logs` (PROVEN 2026-07-15, do NOT "simplify" this back) ──
# api/trace.js:66-67 logs a summary line AND then one `  [sig] <line>` per signal line.
# The summary IS retrievable; the [sig] lines ARE NOT — `vercel logs`, even with --json,
# returns exactly ONE message per request (the summary). Verified: 100 json rows, 0 matching
# [sig]. So the canon read-path "the [sig] echo in Vercel runtime logs" yields `signal=18`
# but never the 18 lines. A daemon grepping `vercel logs` for [sig] matches NOTHING and fires
# NEVER — which is worse than no watcher, because it looks like coverage.
# THEREFORE: the summary is only a cheap TRIGGER (it carries signal=N); the actual lines are
# fetched from Neon through the key-gated admin endpoint (api/admin/db.js).
#
# NOTE ON THE ENDPOINT: WebTrace posts to the PROD domain from EVERY build (previews too) by
# design, so one stream covers everything. Since 2026-07-15 each batch carries
# build=<version>@<host>, so a capture names WHICH deployment it came from.
#
# Emits into the SAME inbox (logs/f8-inbox) with the SAME PING seq contract as the local
# daemon, so f8-check-inbox.ps1 and .cursor/rules/f8-auto-triage.mdc see web captures with
# NO changes. One inbox, two sources.
#
# BASELINE: the first poll records current state and fires NOTHING - otherwise starting the
# daemon replays days of history as "new".
# =============================================================================
param(
    [int]$PollSeconds = 60
)

$ErrorActionPreference = 'Continue'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
$PingFile = Join-Path $Inbox 'PING.json'
$Latest   = Join-Path $Inbox 'LATEST_CAPTURE.md'
$PidFile  = Join-Path $Inbox 'websig-daemon.pid'
$KeyFile  = Join-Path $RepoRoot '.admin-dash-key'
# Preview URLs rotate on every deploy, so the admin base is read from this file EVERY poll
# (the deploy chain rewrites it). Never bake a preview URL into the daemon.
$UrlFile  = Join-Path $RepoRoot 'Builds\admin-preview-url.txt'
$BypassQ  = 'x-vercel-protection-bypass=z5Q9cJNC4JpMoxgXDsddkK8oe7BFGlyP'

New-Item -ItemType Directory -Force -Path $Inbox | Out-Null
Set-Content -Path $PidFile -Value "$PID" -Encoding UTF8

if (-not (Test-Path $KeyFile)) {
    Write-Host '[websig] no .admin-dash-key - cannot read the trace DB. Exiting.'
    exit 1
}
$Key = (Get-Content $KeyFile -Raw).Trim()

# Lines worth waking a human for. Deliberately NOT every Warn: this must fire on real
# breakage only, or it becomes noise and gets ignored - which is how the magenta was missed.
$SignalRx = 'MAGENTA|material=.?NULL|Exception|NullReference|FAILED|not found in Resources|InternalError|softlock'

function Get-AdminBase {
    if (-not (Test-Path $UrlFile)) { return $null }
    $u = (Get-Content $UrlFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($u)) { return $null }
    if ($u -notmatch '^https?://') { $u = 'https://' + $u }
    return $u.TrimEnd('/')
}

function Invoke-Admin([string]$query) {
    $base = Get-AdminBase
    if (-not $base) { return $null }
    try {
        return Invoke-RestMethod -Uri "$base/api/admin/db?$query&$BypassQ" `
            -Headers @{ 'x-admin-key' = $Key } -TimeoutSec 45
    } catch {
        Write-Host ('[websig] admin query failed ({0}): {1}' -f $query, $_.Exception.Message)
        return $null
    }
}

# WO-965: publishing goes through the shared inbox lib so web captures land in QUEUE.jsonl too
# (this daemon shares PING.json/LATEST_CAPTURE.md with the F8 daemon, so it shared the drop bug).
. (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')

function Emit-WebCapture([string]$kind, [string]$session, [string]$build, [string]$trigger, [string[]]$context) {
    $nl = [Environment]::NewLine

    $md = @(
        '# WEB Trace Capture (auto-inbox seq=__F8SEQ__)'
        ''
        ('**Time (local):** {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
        ('**Kind:** {0}' -f $kind)
        ('**Session:** {0}' -f $session)
        ('**Build:** {0}   <- <version>@<host> names WHICH deployment (prod vs a preview)' -f $build)
        '**Source:** Neon analytics_events (web_trace) via api/admin/db.js - a REAL player on the deployed build.'
        ''
        '## Trigger'
        '```'
        $trigger
        '```'
        ''
        '## Signal lines in this batch'
        '```'
        (($context | Select-Object -First 40) -join $nl)
        '```'
        ''
        '## Triage (CLAUDE.md §12 / §14)'
        '- READ THESE LINES FIRST - before any code-read or theory. This IS the captured data.'
        '- Scene-load evidence (TERRAINDIAG / MagentaGuard / FloorDiag / catalog + Resources'
        '  resolution) is in the FIRST batches. The tail is gameplay spam - page to the HEAD:'
        ('    GET <base>/api/admin/db?view=traces&session={0}&order=asc&limit=50' -f $session)
        '  Header: x-admin-key = contents of .admin-dash-key   (base = Builds\admin-preview-url.txt)'
        '- Route per docs/TICKET_PIPELINE.md. Ack when done: f8-ack.ps1'
        ''
    ) -join $nl

    $seq = Publish-F8Capture -Inbox $Inbox -Kind $kind -Md $md -Source 'websig' -BaseName 'capture-web' `
        -Summary $trigger.Substring(0, [Math]::Min(120, $trigger.Length)) `
        -PingMessage 'WEB trace capture - triage now (read LATEST_CAPTURE.md or run f8-check-inbox.ps1)'
    try { [System.Media.SystemSounds]::Exclamation.Play() } catch { }
    Write-Host ''
    Write-Host '============================================================'
    Write-Host (' WEB INBOX PING seq={0} kind={1} - TRIAGE NOW' -f $seq, $kind)
    Write-Host (' {0}' -f $Latest)
    Write-Host '============================================================'
    Write-Host ''
}

Write-Host ('[websig] daemon up. pid={0} poll={1}s' -f $PID, $PollSeconds)
Write-Host ('[websig] inbox: {0}' -f $Inbox)

$lastLines = @{}     # session -> total_lines seen
$baselined = $false

while ($true) {
    try {
        $s = Invoke-Admin 'view=traces'
        if ($s -and $s.sessions) {
            if (-not $baselined) {
                foreach ($sess in $s.sessions) { $lastLines[$sess.session] = [int]$sess.total_lines }
                $baselined = $true
                Write-Host ('[websig] baselined {0} session(s) - watching for NEW trace lines.' -f $s.sessions.Count)
            }
            else {
                foreach ($sess in $s.sessions) {
                    $id   = $sess.session
                    $now  = [int]$sess.total_lines
                    $prev = if ($lastLines.ContainsKey($id)) { [int]$lastLines[$id] } else { 0 }
                    if ($now -le $prev) { continue }
                    $lastLines[$id] = $now

                    # Session grew -> pull its newest batches and look for real breakage.
                    $d = Invoke-Admin ("view=traces&session={0}&order=desc&limit=3" -f $id)
                    if (-not $d -or -not $d.rows) { continue }

                    $hits = @()
                    $build = $sess.build
                    foreach ($row in $d.rows) {
                        if ($row.build) { $build = $row.build }
                        foreach ($ln in $row.lines) {
                            $text = if ($ln -is [string]) { $ln } else { ($ln | ConvertTo-Json -Compress) }
                            if ($text -match $SignalRx) { $hits += $text }
                        }
                    }
                    if ($hits.Count -eq 0) { continue }

                    $worst = $hits | Where-Object { $_ -match 'MAGENTA|material=.?NULL' } | Select-Object -First 1
                    if (-not $worst) { $worst = $hits | Where-Object { $_ -match 'Exception|NullReference' } | Select-Object -First 1 }
                    if (-not $worst) { $worst = $hits[0] }

                    $kind = if ($worst -match 'MAGENTA|material=.?NULL') { 'web-magenta' }
                            elseif ($worst -match 'Exception|NullReference') { 'web-exception' }
                            elseif ($worst -match 'not found in Resources') { 'web-missing-asset' }
                            else { 'web-error' }

                    Emit-WebCapture -kind $kind -session $id -build $build -trigger $worst -context $hits
                }
            }
        }
        elseif (-not (Get-AdminBase)) {
            Write-Host '[websig] no Builds\admin-preview-url.txt yet - waiting for a deploy to write it.'
        }
    }
    catch {
        Write-Host ('[websig] poll threw: {0}' -f $_.Exception.Message)
    }
    Start-Sleep -Seconds $PollSeconds
}
