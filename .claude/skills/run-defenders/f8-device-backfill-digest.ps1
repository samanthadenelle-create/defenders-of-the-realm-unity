# f8-device-backfill-digest.ps1 -- WO-1227. The ONE-SHOT read of everything the phone accumulated
# while nothing was carrying device captures to a seat.
#
# WHY A DIGEST AND NOT AN IMPORT:
#   The device log held 736 entries going back to 2026-07-20 -- five weeks of real, unread evidence
#   (588 error, 25 exception, 8 possible_softlock, 8 flagged). Publishing all of them into the live
#   queue would bury today's captures exactly as thoroughly as the silence did, and the WO-965
#   lesson is that a buried capture is a lost capture. So the history is read ONCE, as a document,
#   and the owner plus the lead decide what becomes a ticket. The live bridge
#   (f8-device-bridge.ps1) baselines past this history and only carries what is NEW.
#
# OUTPUT: every flagged / possible_softlock / exception in full, newest first, then deduped error
#   messages with counts and first/last seen. Nothing is summarised away.
#
# ENCODING: pure ASCII on purpose (WO-1187 / POWERSHELL_ENCODING_FAIL).
param(
    [string]$LogPath = '',
    [string]$OutPath = ''
)

Set-StrictMode -Off
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
if ([string]::IsNullOrWhiteSpace($LogPath)) { $LogPath = Join-Path $RepoRoot 'tmp\f8pull\break-log.jsonl' }
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $OutPath = Join-Path $RepoRoot ('logs\f8-inbox\DEVICE_BACKFILL_{0}.md' -f (Get-Date -Format 'yyyy-MM-dd'))
}

if (-not (Test-Path $LogPath)) {
    Write-Host "F8_DIGEST_FAIL log not found: $LogPath"
    exit 1
}

$entries = @()
$badLines = 0
$lineNo = 0
foreach ($raw in (Get-Content $LogPath)) {
    $lineNo++
    $t = ([string]$raw).TrimStart([char]0xFEFF).Trim()
    if ([string]::IsNullOrWhiteSpace($t)) { continue }
    $e = $null
    try { $e = $t | ConvertFrom-Json } catch { $badLines++; continue }
    if ($null -eq $e) { $badLines++; continue }
    $entries += [pscustomobject]@{
        Line    = $lineNo
        Kind    = [string]$e.kind
        Message = [string]$e.message
        Stack   = [string]$e.stack
        Scene   = [string]$e.scene
        Utc     = [string]$e.utc
    }
}

$byKind = @{}
foreach ($e in $entries) {
    if (-not $byKind.ContainsKey($e.Kind)) { $byKind[$e.Kind] = 0 }
    $byKind[$e.Kind]++
}

function Get-Fence([string]$Text, [int]$Max = 1600) {
    $s = [string]$Text
    if ([string]::IsNullOrWhiteSpace($s)) { return '(empty)' }
    if ($s.Length -gt $Max) { $s = $s.Substring(0, $Max) + ' ... [TRUNCATED - full text is in the source log]' }
    return $s
}

$nl = [Environment]::NewLine
$out = New-Object System.Collections.Generic.List[string]

$out.Add('# DEVICE F8 BACKFILL DIGEST -- WO-1227')
$out.Add('')
$out.Add('One-shot read of the break-log that had been accumulating on the Seeker while NOTHING')
$out.Add('carried device captures to a seat. `f8-watch-daemon.ps1` only ever watched the DESKTOP')
$out.Add('persistentDataPath, so on the one platform the owner actually plays, the CLAUDE.md')
$out.Add('section 14 chain was severed at the first link. None of what follows has ever reached a seat.')
$out.Add('')
$out.Add('This is a DOCUMENT, not a queue import. Importing five weeks of history into')
$out.Add('`logs/f8-inbox/QUEUE.jsonl` would bury today captures as thoroughly as the silence did')
$out.Add('(the WO-965 lesson). The live bridge baselines past all of this and carries only what is NEW.')
$out.Add('')
$out.Add(('**Source log:** `{0}`' -f $LogPath))
$out.Add(('**Generated:** {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')))
$out.Add(('**Entries parsed:** {0}   (unparseable lines: {1})' -f $entries.Count, $badLines))
$out.Add('')
$out.Add('| kind | count |')
$out.Add('|---|---|')
foreach ($k in ($byKind.Keys | Sort-Object { -$byKind[$_] })) {
    $out.Add(('| {0} | {1} |' -f $k, $byKind[$k]))
}
$out.Add('')
if ($entries.Count -gt 0) {
    $span = @($entries | Where-Object { $_.Utc } | Sort-Object Utc)
    if ($span.Count -gt 0) {
        $out.Add(('**Span:** {0}  ->  {1}' -f $span[0].Utc, $span[$span.Count - 1].Utc))
        $out.Add('')
    }
}

# --- the standout finding, stated up front rather than left in the pile -------------------------
$quiescence = @($entries | Where-Object { $_.Message -match 'BATTLE_QUIESCENCE_FAIL' })
if ($quiescence.Count -gt 0) {
    $out.Add('---')
    $out.Add('')
    $out.Add(('## READ THIS FIRST: {0} x BATTLE_QUIESCENCE_FAIL' -f $quiescence.Count))
    $out.Add('')
    $out.Add('A P0 softlock class that our OWN instrumentation diagnosed correctly, and that reached')
    $out.Add('nobody for weeks because there was no transport off the phone.')
    $out.Add('')
    foreach ($q in @($quiescence | Sort-Object Utc -Descending)) {
        $out.Add(('- `{0}` [{1}] scene={2}' -f $q.Utc, $q.Kind, $q.Scene))
        $out.Add(('  - {0}' -f (($q.Message -replace '\s+', ' '))))
    }
    $out.Add('')
}

function Add-Section([string]$Title, $Rows, [string]$Note) {
    $out.Add('---')
    $out.Add('')
    $out.Add(('## {0} ({1})' -f $Title, @($Rows).Count))
    $out.Add('')
    if ($Note) { $out.Add($Note); $out.Add('') }
    if (@($Rows).Count -eq 0) { $out.Add('_none_'); $out.Add(''); return }
    foreach ($r in @($Rows)) {
        $out.Add(('### {0}  [{1}]  scene=`{2}`  (log line {3})' -f $r.Utc, $r.Kind, $r.Scene, $r.Line))
        $out.Add('')
        $out.Add('```')
        $out.Add((Get-Fence $r.Message))
        $out.Add('```')
        if (-not [string]::IsNullOrWhiteSpace($r.Stack)) {
            $out.Add('')
            $out.Add('<details><summary>stack</summary>')
            $out.Add('')
            $out.Add('```')
            $out.Add((Get-Fence $r.Stack 1200))
            $out.Add('```')
            $out.Add('')
            $out.Add('</details>')
        }
        $out.Add('')
    }
}

Add-Section 'FLAGGED - the owner pressed the button' `
    (@($entries | Where-Object { $_.Kind -eq 'flagged' } | Sort-Object Utc -Descending)) `
    'Every one of these is the owner telling us something was wrong, in full, newest first.'

Add-Section 'POSSIBLE SOFTLOCK' `
    (@($entries | Where-Object { $_.Kind -match 'softlock' } | Sort-Object Utc -Descending)) `
    'Highest severity class in the log. A softlock ends the session for the player.'

Add-Section 'EXCEPTION' `
    (@($entries | Where-Object { $_.Kind -eq 'exception' } | Sort-Object Utc -Descending)) `
    'Unhandled throws captured on device.'

# --- errors: deduped, because 588 raw rows is not readable and repetition is itself the signal ---
$errors = @($entries | Where-Object { $_.Kind -eq 'error' })
$groups = @{}
foreach ($e in $errors) {
    $sig = ($e.Message -replace '\s+', ' ')
    if ($sig.Length -gt 180) { $sig = $sig.Substring(0, 180) }
    if (-not $groups.ContainsKey($sig)) {
        $groups[$sig] = [pscustomobject]@{ Sig = $sig; Count = 0; First = $e.Utc; Last = $e.Utc; Scene = $e.Scene; Sample = $e.Message }
    }
    $g = $groups[$sig]
    $g.Count++
    if ([string]::Compare($e.Utc, $g.First, $true) -lt 0) { $g.First = $e.Utc }
    if ([string]::Compare($e.Utc, $g.Last, $true) -gt 0) { $g.Last = $e.Utc }
}
$ordered = @($groups.Values | Sort-Object Last -Descending)

$out.Add('---')
$out.Add('')
$out.Add(('## ERROR - deduped ({0} distinct messages across {1} occurrences)' -f $ordered.Count, $errors.Count))
$out.Add('')
$out.Add('Newest LAST-SEEN first. Repetition is itself the signal: a message with a high count and a')
$out.Add('recent last-seen is live and still firing, not history.')
$out.Add('')
$out.Add('| count | first seen (utc) | last seen (utc) | scene | message |')
$out.Add('|---:|---|---|---|---|')
foreach ($g in $ordered) {
    $m = ($g.Sig -replace '\|', '\|')
    $out.Add(('| {0} | {1} | {2} | {3} | {4} |' -f $g.Count, $g.First, $g.Last, $g.Scene, $m))
}
$out.Add('')
$out.Add('---')
$out.Add('')
$out.Add('## What happens next')
$out.Add('')
$out.Add('- This file is read ONCE. Anything worth a ticket becomes a WO; the rest is closed as history.')
$out.Add('- Going forward, `f8-device-bridge.ps1` carries NEW device captures into the same queue the')
$out.Add('  desktop daemon feeds, so this backlog can never build up silently again.')
$out.Add('')

$utf8 = New-Object System.Text.UTF8Encoding($false)
New-Item -ItemType Directory -Force -Path (Split-Path $OutPath -Parent) | Out-Null
[System.IO.File]::WriteAllText($OutPath, ($out -join $nl), $utf8)

Write-Host ('F8_DIGEST_OK entries={0} flagged={1} softlock={2} exception={3} errorDistinct={4} errorTotal={5} out={6}' -f `
    $entries.Count, `
    @($entries | Where-Object { $_.Kind -eq 'flagged' }).Count, `
    @($entries | Where-Object { $_.Kind -match 'softlock' }).Count, `
    @($entries | Where-Object { $_.Kind -eq 'exception' }).Count, `
    $ordered.Count, $errors.Count, $OutPath)
exit 0
