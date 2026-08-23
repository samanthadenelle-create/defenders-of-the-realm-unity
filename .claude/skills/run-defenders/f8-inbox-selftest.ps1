# f8-inbox-selftest.ps1 -- WO-1018 regression suite for the F8 inbox queue.
#
# Runs entirely in a THROWAWAY inbox under $env:TEMP. It never reads, writes or acks anything in
# logs/f8-inbox, so it is safe to run at any time, including while the daemon is live.
#
# It reproduces the real 2026-08-15 defect: capture-20260815-183806-seq2329.md (the owner's flag
# "[Main_Castle_Overworld] look at the overcrowding") and capture-20260815-210117-seq2329.md (an
# unrelated scene-open error) shared seq 2329, so acking the number closed the flag and no seat ever
# read it. Case A proves that can no longer happen going forward, B that a missing queue still
# yields a COMPLETE pending list, C that an ALREADY-buried capture can be recovered, D that an empty
# sweep refuses rather than reading as clean, and E that the prune step archives without deleting.
#
# MARKER (judge by this, never by the exit code -- this repo's runners exit 0 on failure):
#   F8_SELFTEST_OK <passed>/<total>       every case passed
#   F8_SELFTEST_FAIL <passed>/<total>     at least one case failed; each FAIL line names the case
param([switch]$KeepFixture)

$ErrorActionPreference = 'Continue'
$Skill = $PSScriptRoot
$Root  = Join-Path $env:TEMP ('f8-selftest-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$Empty = Join-Path $env:TEMP ('f8-selftest-empty-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force $Root  | Out-Null
New-Item -ItemType Directory -Force $Empty | Out-Null

$script:Pass = 0
$script:Fail = 0
function Assert([string]$Name, [bool]$Cond, [string]$Detail = '') {
    if ($Cond) { $script:Pass++; Write-Host ("  PASS  {0}" -f $Name) }
    else { $script:Fail++; Write-Host ("  FAIL  {0}  {1}" -f $Name, $Detail) }
}
function Utf8([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}
function New-Cap([string]$Name, [int]$Seq, [string]$Kind, [string]$Msg) {
    $path = Join-Path $Root $Name
    Utf8 $path (@(
        "# F8 Capture (auto-inbox seq=$Seq)"
        ''
        '**Time (local):** 2026-08-15 18:38:06'
        "**Kind:** $Kind"
        ''
        '## Trigger'
        '```'
        ('{{"kind":"{0}","message":"{1}","scene":"Main_Castle_Overworld"}}' -f $Kind, $Msg)
        '```'
    ) -join "`r`n")
    return $path
}
function Set-Ack([int]$Wm) { Utf8 (Join-Path $Root 'ACK.json') ('{"lastAckSeq":' + $Wm + ',"acked":[],"ackedFiles":[]}') }

# These scripts report through Write-Host, which in PowerShell 5.1 goes to the CONSOLE and not to
# the output stream -- `$x = & script` captures NOTHING, and every assertion against it silently
# passes or silently fails. (That bug produced a 5/24 run against a working implementation while
# this suite was being written.) Running each one in a CHILD process captures what a seat actually
# sees, which is the thing under test. It also gives real argument binding: splatting an array into
# a scripts's -Seq/-File would bind the flag NAME as the value.
function Run-Script([string]$Name, [string[]]$ExtraArgs) {
    $argv = @('-NoProfile','-ExecutionPolicy','Bypass','-File', (Join-Path $Skill $Name), '-InboxOverride', $Root)
    if ($ExtraArgs) { $argv += $ExtraArgs }
    return ((& powershell.exe @argv 2>&1 | Out-String))
}
function Check() { return (Run-Script 'f8-check-inbox.ps1' @()) }
function Ack([string[]]$ExtraArgs) { return (Run-Script 'f8-ack.ps1' $ExtraArgs) }
function Sweep([string]$Inbox2) {
    $argv = @('-NoProfile','-ExecutionPolicy','Bypass','-File', (Join-Path $Skill 'f8-backfill-sweep.ps1'), '-InboxOverride', $Inbox2)
    return ((& powershell.exe @argv 2>&1 | Out-String))
}

# -- fixture ---------------------------------------------------------------------------------------
$capA = New-Cap 'capture-20260815-100000-seq10.md' 10 'flagged' 'ordinary capture ten'
$capB = New-Cap 'capture-20260815-183806-seq11.md' 11 'flagged' 'look at the overcrowding'
$capC = New-Cap 'capture-20260815-210117-seq11.md' 11 'error'   'Problem opening the Scene file'
$queueText = @(
  ('{{"source":"f8","capturePath":"{0}","kind":"flagged","seq":10,"utc":"2026-08-15T15:00:00Z","summary":"ordinary capture ten"}}' -f ($capA -replace '\\','\\')),
  ('{{"source":"f8","capturePath":"{0}","kind":"error","seq":11,"utc":"2026-08-16T02:01:17Z","summary":"Problem opening the Scene file"}}' -f ($capC -replace '\\','\\'))
) -join "`r`n"
Utf8 (Join-Path $Root 'QUEUE.jsonl') ($queueText + "`r`n")
Utf8 (Join-Path $Root 'PING.json') ('{"seq":11,"kind":"error","capturePath":"' + ($capC -replace '\\','\\') + '","firedAtUtc":"2026-08-16T02:01:17Z","summary":"x"}')
Utf8 (Join-Path $Root 'LATEST_CAPTURE.md') 'latest'

Write-Host '[f8-selftest] WO-1018 -- F8 inbox queue regression'
Write-Host ("[f8-selftest] fixture: {0}" -f $Root)
Write-Host ''
Write-Host 'A. collision ABOVE the watermark'
Set-Ack 9
$out = Check
Assert 'a collided seq surfaces BOTH captures (pending=3, not 2)' ($out -match 'pending=3') $out
Assert 'the collision is announced, never silent' ($out -match 'COLLIDED seq') ''

$out = Ack @()
Assert 'a bare ack still takes the OLDEST capture' ($out -match 'Acknowledged seq=10') $out
Assert 'and says what remains' ($out -match 'STILL PENDING: 2') ''

$out = Ack @('-Seq','11')
Assert 'acking a collided seq BY NUMBER is refused' ($out -match 'REFUSED') $out
$out = Check
Assert 'the refusal closed nothing' ($out -match 'pending=2') $out

$out = Ack @('-File','capture-20260815-210117-seq11.md')
Assert 'acking one collided capture by file leaves the other open' ($out -match 'STILL PENDING: 1') $out
$out = Check
Assert 'and the one still open is the OWNER FLAG' ($out -match 'capture-20260815-183806-seq11\.md') $out

[void](Ack @('-File','capture-20260815-183806-seq11.md'))
$out = Check
Assert 'only when both are acked does the inbox read clean' ($out -match 'NO_CAPTURE') $out

Write-Host ''
Write-Host 'B. a capture file with NO queue row at all'
Set-Ack 9
Rename-Item (Join-Path $Root 'QUEUE.jsonl') 'QUEUE.jsonl.bak'
Utf8 (Join-Path $Root 'QUEUE.jsonl') ''
$out = Check
Assert 'the pending list rebuilds from the capture FILES (all 3)' ($out -match 'pending=3') $out
Remove-Item (Join-Path $Root 'QUEUE.jsonl') -Force
Rename-Item (Join-Path $Root 'QUEUE.jsonl.bak') 'QUEUE.jsonl'

Write-Host ''
Write-Host 'C. a capture BURIED below the watermark'
Set-Ack 11
Remove-Item (Join-Path $Root 'ACK_BACKFILL.json')  -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $Root 'capture-index.json') -Force -ErrorAction SilentlyContinue
Utf8 (Join-Path $Root 'queue-events.log') ("x [info] acked seq=10 (watermark now 10)`r`nx [info] acked seq=11 (watermark now 11)`r`n")

$out = Check
Assert 'an unswept inbox never claims to be clean silently' ($out -match 'WARN_NO_SWEEP') $out

$out = Sweep $Root
Assert 'the sweep reports the duplicate sequence' ($out -match 'F8_SWEEP_DUPSEQ seq=11') $out
Assert 'the sweep finds exactly the one buried capture' ($out -match 'F8_SWEEP_OK .*orphans=1') $out
Assert 'and does NOT re-open the whole history' (-not ($out -match 'orphans=[2-9]')) $out

$out = Check
Assert 'the buried owner flag is now reachable again' (($out -match 'NEW_CAPTURE') -and ($out -match 'capture-20260815-183806-seq11\.md')) $out
Assert 'the sweep did not auto-close it' ($out -match 'pending=1') $out

[void](Ack @('-File','capture-20260815-183806-seq11.md'))
$out = Check
Assert 'acking the recovered capture clears the inbox' ($out -match 'NO_CAPTURE') $out

Write-Host ''
Write-Host 'D. an empty sweep must never look like a clean one'
$out = Sweep $Empty
Assert 'sweeping an empty inbox REFUSES' ($out -match 'F8_SWEEP_FAIL') $out
Assert 'and emits no success marker' (-not ($out -match 'F8_SWEEP_OK')) $out

Write-Host ''
Write-Host 'E. the prune step'
. (Join-Path $Skill 'f8-inbox-lib.ps1')
Get-ChildItem $Root -Filter 'capture-*.md' | ForEach-Object { $_.LastWriteTime = (Get-Date).AddDays(-30) }
$r = Invoke-F8InboxArchive -Inbox $Root -Days 14 -WhatIf
Assert 'a dry run moves nothing and emits no _OK marker' ($r.Moved -eq 0) ''
Assert 'the inbox still holds every capture after a dry run' ((@(Get-ChildItem $Root -Filter 'capture-*.md')).Count -eq 3) ''
$r = Invoke-F8InboxArchive -Inbox $Root -Days 14
Assert 'a real run archives the acked captures' ($r.Moved -eq 3) ("moved=$($r.Moved)")
Assert 'NOTHING is deleted - every file is still on disk, in archive/' ((@(Get-ChildItem (Join-Path $Root 'archive') -Filter 'capture-*.md')).Count -eq 3) ''
Clear-F8IndexCache $Root
Assert 'an archived capture is still resolvable by seq' ((@(Resolve-F8CaptureFiles $Root 11)).Count -eq 2) ''

$total = $script:Pass + $script:Fail
Write-Host ''
if (-not $KeepFixture) {
    Remove-Item $Root  -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $Empty -Recurse -Force -ErrorAction SilentlyContinue
}
if ($script:Fail -gt 0) { Write-Host ("F8_SELFTEST_FAIL {0}/{1}" -f $script:Pass, $total); exit 0 }
if ($total -eq 0)       { Write-Host 'F8_SELFTEST_FAIL 0/0 - no cases ran'; exit 0 }
Write-Host ("F8_SELFTEST_OK {0}/{1}" -f $script:Pass, $total)
exit 0
