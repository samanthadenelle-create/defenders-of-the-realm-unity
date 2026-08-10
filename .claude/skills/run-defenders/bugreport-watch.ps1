# =============================================================================
# bugreport-watch.ps1 - NEW BUG REPORT watcher (WO-846, the third leg of CLAUDE.md sec 14).
# -----------------------------------------------------------------------------
# Owner ruling 2026-08-02 (tester program): "when they submit a bug from settings
# it calls something to save stack trace to the db and lets us know to review it."
# The client half (BugReportVM, WO-596 + WO-846) saves the report + playerId +
# trace tail into Neon bug_reports via api/bug-report.js. THIS daemon is the
# "lets us know" half: it polls the key-gated admin endpoint (api/admin/db.js)
# for new bug_reports rows and, on each new report, writes a LATEST_CAPTURE-style
# md into logs/f8-inbox and bumps the SAME PING.json seq contract the f8/websig
# daemons use - so f8-check-inbox.ps1 and .cursor/rules/f8-auto-triage.mdc
# surface bug reports with NO changes. One inbox, three sources.
#
# AUTH (mirrors websig-watch-daemon.ps1 exactly):
#   * key   = contents of .admin-dash-key (gitignored) sent as header x-admin-key
#   * base  = Builds\admin-preview-url.txt, re-read EVERY poll (preview URLs
#             rotate on deploy; never bake one in)
#   * plus the Vercel protection-bypass query param
#
# CURSOR: bug_reports.report_id (BIGINT IDENTITY, api/schema.sql:432). State is
# persisted in logs/f8-inbox/bugreport-watch.state.json so a daemon restart does
# not replay already-triaged reports. First successful poll BASELINES (records
# the newest id and fires nothing) - same rule as the websig daemon.
#
# =============================================================================
# TODO (api/ is FENCED for this lane - orchestrator must add this ONE view):
# api/admin/db.js has views overview | players | metrics | traces but NO
# bug_reports view, so this daemon's primary path 400s until the following block
# is added to api/admin/db.js (house style: static tagged-template SQL,
# clampLimit, screenshot as a PRESENCE FLAG only - never SELECT the b64 blob):
#
#   // ------------------------------------------------------------- bugreports
#   // WO-846: newest bug reports for the bugreport-watch daemon. after_id => the
#   // incremental cursor (rows STRICTLY newer, ascending); without it => latest
#   // rows descending (baseline read). screenshotB64 is returned as a presence
#   // flag only - the blob can be ~420K chars and never belongs in a poll.
#   if (view === 'bugreports') {
#       const limit = clampLimit(q.limit, 20, 100);
#       const afterId = parseInt(q.after_id, 10);
#       const cols = 'report_id, created_at, description, route, app_version, player_id';
#       const rows = (Number.isFinite(afterId) && afterId > 0)
#           ? await sql`
#               SELECT report_id, created_at, description, route, app_version, player_id,
#                      context->>'platform'  AS platform,
#                      context->>'sessionId' AS session_id,
#                      context->'traceTail'  AS trace_tail,
#                      (context ? 'screenshotB64' AND context->>'screenshotB64' IS NOT NULL) AS has_screenshot
#               FROM bug_reports
#               WHERE report_id > ${afterId}
#               ORDER BY report_id ASC
#               LIMIT ${limit}`
#           : await sql`
#               SELECT report_id, created_at, description, route, app_version, player_id,
#                      context->>'platform'  AS platform,
#                      context->>'sessionId' AS session_id,
#                      context->'traceTail'  AS trace_tail,
#                      (context ? 'screenshotB64' AND context->>'screenshotB64' IS NOT NULL) AS has_screenshot
#               FROM bug_reports
#               ORDER BY report_id DESC
#               LIMIT ${limit}`;
#       return res.status(200).json({ view: 'bugreports', rows: rows });
#   }
#   // (and extend the final "Unknown view" hint string with "| bugreports")
#   // NOTE: if the LIVE table predates api/schema.sql and lacks report_id,
#   // substitute created_at as the cursor (after_ts) - schema.sql:432 says
#   // report_id BIGINT GENERATED ALWAYS AS IDENTITY, so report_id is expected.
#
# UNTIL THAT VIEW EXISTS the daemon degrades gracefully: it detects NEW rows via
# view=overview (bug_reports row count + latest created_at are already exposed)
# and pings with a "content unavailable - add view=bugreports" capture, so the
# notify path works from day one even before the view lands.
# =============================================================================
param(
    [int]$PollSeconds = 120
)

$ErrorActionPreference = 'Continue'
$RepoRoot  = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox     = Join-Path $RepoRoot 'logs\f8-inbox'
$PingFile  = Join-Path $Inbox 'PING.json'
$Latest    = Join-Path $Inbox 'LATEST_CAPTURE.md'
$PidFile   = Join-Path $Inbox 'bugreport-daemon.pid'
$StateFile = Join-Path $Inbox 'bugreport-watch.state.json'
$KeyFile   = Join-Path $RepoRoot '.admin-dash-key'
# Preview URLs rotate on every deploy, so the admin base is read from this file
# EVERY poll (the deploy chain rewrites it). Never bake a preview URL in here.
$UrlFile   = Join-Path $RepoRoot 'Builds\admin-preview-url.txt'
$BypassQ   = 'x-vercel-protection-bypass=z5Q9cJNC4JpMoxgXDsddkK8oe7BFGlyP'

New-Item -ItemType Directory -Force -Path $Inbox | Out-Null
Set-Content -Path $PidFile -Value "$PID" -Encoding UTF8

if (-not (Test-Path $KeyFile)) {
    Write-Host '[bugreport] no .admin-dash-key - cannot read the bug_reports table. Exiting.'
    exit 1
}
$Key = (Get-Content $KeyFile -Raw).Trim()

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
        Write-Host ('[bugreport] admin query failed ({0}): {1}' -f $query, $_.Exception.Message)
        return $null
    }
}

# WO-965: publishing goes through the shared inbox lib so bug reports land in QUEUE.jsonl too
# (this daemon shares PING.json/LATEST_CAPTURE.md with the F8 daemon, so it shared the drop bug).
. (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')

function Read-State {
    try {
        if (-not (Test-Path $StateFile)) { return $null }
        return Get-Content $StateFile -Raw | ConvertFrom-Json
    } catch { return $null }
}

function Write-State([long]$lastReportId, [long]$lastCount, [string]$lastLatest) {
    @{
        lastReportId = $lastReportId
        lastCount    = $lastCount
        lastLatest   = $lastLatest
        updatedUtc   = (Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json | Set-Content -Path $StateFile -Encoding UTF8
}

# One md per NEW report - the report's identity + description + context tail, in
# the LATEST_CAPTURE house shape the f8/websig daemons write.
function Emit-ReportCapture($row) {
    $nl = [Environment]::NewLine

    $tail = @()
    if ($row.trace_tail) {
        foreach ($ln in $row.trace_tail) {
            $tail += $(if ($ln -is [string]) { $ln } else { ($ln | ConvertTo-Json -Compress) })
        }
    }
    $desc = if ([string]::IsNullOrWhiteSpace([string]$row.description)) { '(no note - the capture is the value)' } else { [string]$row.description }
    $player = if ($row.player_id) { [string]$row.player_id } else { '(none - pre-WO-846 client or no save loaded)' }
    $shot = if ($row.has_screenshot) { 'yes (fetch via db-viewer / view=bugreports single row)' } else { 'no' }

    $md = @(
        ('# NEW BUG REPORT (auto-inbox seq=__F8SEQ__, bug_reports id={0})' -f $row.report_id)
        ''
        ('**Time (local):** {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
        ('**Submitted (db):** {0}' -f $row.created_at)
        ('**Player:** {0}   <- the SAVE KEY (wallet / firebase / guest-local) - joins player_data.player_id' -f $player)
        ('**Scene (route):** {0}' -f $row.route)
        ('**Version:** {0}   **Platform:** {1}   **Session:** {2}' -f $row.app_version, $row.platform, $row.session_id)
        ('**Screenshot attached:** {0}' -f $shot)
        '**Source:** Neon bug_reports via api/admin/db.js - a REAL tester pressed Send report in Settings.'
        ''
        '## Tester note'
        '```'
        $desc
        '```'
        ''
        ('## Context tail ({0} lines, oldest first - the captured [Flow:*]/error lines at submit time)' -f $tail.Count)
        '```'
        (($tail | Select-Object -Last 40) -join $nl)
        '```'
        ''
        '## Triage (CLAUDE.md sec 12 / 13 / 14)'
        '- READ THE TAIL FIRST - this IS the captured data; no code-read before it.'
        '- The player id is the save key: pull their save via'
        ('    GET <base>/api/admin/db?view=players&player={0}' -f $player)
        '  Header: x-admin-key = contents of .admin-dash-key   (base = Builds\admin-preview-url.txt)'
        '- Route per docs/TICKET_PIPELINE.md (QA triage read-only -> CLI implements -> PO closes).'
        '- Ack when done: f8-ack.ps1'
        ''
    ) -join $nl

    $summary = ('bug_reports id={0} player={1} scene={2}: {3}' -f $row.report_id, $player, $row.route, $desc)
    $seq = Publish-F8Capture -Inbox $Inbox -Kind 'bug-report' -Md $md -Source 'bugreport' `
        -BaseName ('capture-bugreport-id{0}' -f $row.report_id) `
        -Summary $summary.Substring(0, [Math]::Min(160, $summary.Length)) `
        -PingMessage 'NEW BUG REPORT - review now (read LATEST_CAPTURE.md or run f8-check-inbox.ps1)'
    try { [System.Media.SystemSounds]::Exclamation.Play() } catch { }
    Write-Host ''
    Write-Host '============================================================'
    Write-Host (' BUG REPORT INBOX PING seq={0} id={1} - REVIEW NOW' -f $seq, $row.report_id)
    Write-Host (' {0}' -f $Latest)
    Write-Host '============================================================'
    Write-Host ''
}

# Degraded ping while view=bugreports does not exist yet: overview proves NEW
# rows landed but their content is unreachable from here. Still notify - the
# notify contract must hold from day one; the TODO names the one-view fix.
function Emit-DegradedCapture([long]$newCount, [long]$oldCount, [string]$latest) {
    $nl = [Environment]::NewLine
    $md = @(
        '# NEW BUG REPORT(S) - content pending admin view (auto-inbox seq=__F8SEQ__)'
        ''
        ('**Time (local):** {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
        ('**New rows:** {0} (bug_reports count {1} -> {2})' -f ($newCount - $oldCount), $oldCount, $newCount)
        ('**Newest created_at:** {0}' -f $latest)
        '**Source:** Neon bug_reports row count via api/admin/db.js view=overview.'
        ''
        '## Why no content'
        'api/admin/db.js has NO view=bugreports yet, so this daemon can prove NEW reports'
        'landed but cannot read them. Add the view speced in the TODO block at the top of'
        '.claude/skills/run-defenders/bugreport-watch.ps1 (api/ is the orchestrator''s lane).'
        'Until then read the rows via the [bug_report] echo in Vercel runtime logs, or the'
        'db-viewer with a manual SQL session.'
        ''
        '- Ack when triaged: f8-ack.ps1'
        ''
    ) -join $nl
    $seq = Publish-F8Capture -Inbox $Inbox -Kind 'bug-report' -Md $md -Source 'bugreport' `
        -BaseName 'capture-bugreport-degraded' `
        -Summary ('{0} new bug report(s) - view=bugreports missing, content pending' -f ($newCount - $oldCount)) `
        -PingMessage 'NEW BUG REPORT - review now (read LATEST_CAPTURE.md or run f8-check-inbox.ps1)'
    try { [System.Media.SystemSounds]::Exclamation.Play() } catch { }
    Write-Host ('[bugreport] DEGRADED PING seq={0}: {1} new report(s), content unreadable until view=bugreports exists.' -f $seq, ($newCount - $oldCount))
}

Write-Host ('[bugreport] daemon up. pid={0} poll={1}s' -f $PID, $PollSeconds)
Write-Host ('[bugreport] inbox: {0}' -f $Inbox)

$state = Read-State
$lastId     = if ($state -and $state.lastReportId) { [long]$state.lastReportId } else { -1 }
$lastCount  = if ($state -and $state.lastCount -ne $null) { [long]$state.lastCount } else { -1 }
$lastLatest = if ($state) { [string]$state.lastLatest } else { '' }
if ($state) { Write-Host ('[bugreport] resumed state: lastReportId={0} lastCount={1}' -f $lastId, $lastCount) }

while ($true) {
    try {
        # -- Primary path: view=bugreports (id cursor) ------------------------
        $r = $null
        if ($lastId -ge 0) { $r = Invoke-Admin ('view=bugreports&after_id={0}&limit=50' -f $lastId) }
        else               { $r = Invoke-Admin 'view=bugreports&limit=25' }

        if ($r -and $r.view -eq 'bugreports') {
            $rows = @($r.rows)
            if ($lastId -lt 0) {
                # BASELINE: record the newest id, fire nothing (rows came DESC).
                $max = 0
                foreach ($row in $rows) { if ([long]$row.report_id -gt $max) { $max = [long]$row.report_id } }
                $lastId = $max
                Write-State -lastReportId $lastId -lastCount $lastCount -lastLatest $lastLatest
                Write-Host ('[bugreport] baselined at report_id={0} - watching for NEW reports.' -f $lastId)
            }
            elseif ($rows.Count -gt 0) {
                # after_id rows come ASC - emit one capture per new report, oldest first.
                foreach ($row in ($rows | Sort-Object { [long]$_.report_id })) {
                    Emit-ReportCapture $row
                    if ([long]$row.report_id -gt $lastId) { $lastId = [long]$row.report_id }
                }
                Write-State -lastReportId $lastId -lastCount $lastCount -lastLatest $lastLatest
            }
        }
        else {
            # -- Fallback path: view=overview count detection (view missing or
            #    transient failure - only the COUNT MOVING fires anything). ----
            $o = Invoke-Admin 'view=overview'
            if ($o -and $o.tables) {
                $br = $o.tables | Where-Object { $_.table -eq 'bug_reports' } | Select-Object -First 1
                if ($br -and $br.rows -ne $null) {
                    $count  = [long]$br.rows
                    $latest = [string]$br.latest
                    if ($lastCount -lt 0) {
                        $lastCount = $count; $lastLatest = $latest
                        Write-State -lastReportId $lastId -lastCount $lastCount -lastLatest $lastLatest
                        Write-Host ('[bugreport] baselined (fallback) at bug_reports count={0}.' -f $count)
                    }
                    elseif ($count -gt $lastCount) {
                        Emit-DegradedCapture -newCount $count -oldCount $lastCount -latest $latest
                        $lastCount = $count; $lastLatest = $latest
                        Write-State -lastReportId $lastId -lastCount $lastCount -lastLatest $lastLatest
                    }
                    else {
                        $lastCount = $count
                    }
                }
            }
            elseif (-not (Get-AdminBase)) {
                Write-Host '[bugreport] no Builds\admin-preview-url.txt yet - waiting for a deploy to write it.'
            }
        }
    }
    catch {
        Write-Host ('[bugreport] poll threw: {0}' -f $_.Exception.Message)
    }
    Start-Sleep -Seconds $PollSeconds
}
