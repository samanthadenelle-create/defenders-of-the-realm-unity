# f8-device-bridge.ps1 -- WO-1227. The DEVICE half of the CLAUDE.md section 14 chain.
#
# WHY THIS EXISTS (the defect it fixes):
#   BreakCaptureHarness writes break-log.jsonl + break_*.png correctly ON DEVICE. But
#   f8-watch-daemon.ps1 only ever watched the DESKTOP persistentDataPath
#   (%LOCALAPPDATA%Low\DeNelle\...). NOTHING moved a capture off the phone. So on the exe the
#   passive-listener chain was whole, and on the ONE PLATFORM THE OWNER ACTUALLY PLAYS it was
#   severed at the first link.
#   Proven 2026-08-26: f8-check-inbox.ps1 read NO_CAPTURE ack=3607 ping=3607 unchanged all day
#   while the Seeker held a 450 KB break-log.jsonl with 736 entries going back to 2026-07-20 --
#   588 error, 25 exception, 8 possible_softlock, 8 flagged. Among them NINE
#   BATTLE_QUIESCENCE_FAIL events (a P0 softlock class our own instrumentation diagnosed
#   correctly) that reached NOBODY for weeks. The owner pressed the on-screen FLAG and asked
#   "did it reach you?". It had not. The button was necessary and not sufficient -- it wrote to
#   a place the listener had never looked.
#
# WHAT THIS IS:
#   A pull-side bridge. It reads the device log over adb and publishes each NEW, SIGNAL-BEARING
#   entry through the EXISTING queue library (f8-inbox-lib.ps1 -> Publish-F8Capture), so a flag
#   on the Seeker surfaces through f8-check-inbox.ps1 EXACTLY like a flag on the exe.
#
# WHAT THIS IS DELIBERATELY NOT:
#   NOT a second inbox. NOT a second ack state. NOT a second queue file. WO-965 made the inbox an
#   append-only QUEUE precisely because a single-slot inbox silently buried the owner's seq 2307
#   and 2308 on 2026-08-10. device-state.json below holds ONE thing only: how far into the
#   DEVICE-SIDE log we have read. It is the exact analogue of daemon-state.json's breakOffset for
#   the desktop log. It never records triage state; ACK.json remains the only ack authority.
#
# THE WATERMARK (why a pull must be incremental):
#   The device log is APPEND-ONLY and already 450 KB. A naive poll would republish all 736 entries
#   every 30 seconds and bury the queue as thoroughly as the current silence does. So state carries
#   per-device:
#     lineOffset - how many lines of THIS device's log we have consumed (primary watermark)
#     lastUtc    - the newest entry utc we have published (secondary, rotation-proof: if the app
#                  clears or rotates the log the offset resets to 0 but lastUtc still suppresses
#                  everything already seen, so a rotation cannot cause a 736-entry replay)
#     seen       - rolling hashes of kind+message, so one repeating error (the MagentaGuard sweep
#                  fires dozens of times a session) contributes ONE capture, not dozens
#   FIRST RUN AGAINST A DEVICE BASELINES and publishes nothing, the same way f8-watch-daemon.ps1
#   baselines a break-log it has never seen. The five weeks of history already on the phone are
#   handled ONCE, as a digest, by f8-device-backfill-digest.ps1 -- importing them live would bury
#   today's captures.
#
# FILTER: the same one the desktop daemon uses. session_start / scene_loaded / note are startup
#   and lifecycle noise and must NEVER wake a triage seat. Fire on flagged / error / exception /
#   possible_softlock (and softlock) ONLY.
#
# adb: NOT ON PATH on this machine. It ships inside the Unity Hub Android SDK
#   (.../PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe). Resolve-Adb below searches
#   env overrides, PATH, the Android SDK env vars and every installed Unity Hub editor.
#   NOTE: always invoke adb from POWERSHELL, never from Git Bash -- MSYS rewrites a device path
#   like /sdcard/... into C:/Program Files/Git/sdcard/... and the pull fails. (That cost a failed
#   pull on 2026-08-26.) From PowerShell there is no path translation layer at all.
#
# NO DEVICE = SILENT NO-OP, EXIT 0. The phone is usually unplugged. A bridge that shouts on every
#   poll trains the seat to ignore the daemon, which recreates the exact failure this WO fixes.
#
# ENCODING: this file is PURE ASCII on purpose. Windows PowerShell 5.1 reads a BOM-less file as
#   ANSI, and CP1252 turns smart-quote bytes into string delimiters -- silently swallowing whole
#   statements while every gate stays green (WO-1187, POWERSHELL_ENCODING_FAIL).
#
# USAGE
#   powershell -File .claude\skills\run-defenders\f8-device-bridge.ps1            # one pass
#   powershell -File .claude\skills\run-defenders\f8-device-bridge.ps1 -Loop      # poll forever
#   ... -ReplayLast N   on a device with NO state yet, seed the watermark N eligible entries back
#                       instead of at the end (used to demonstrate the chain end to end)
#   ... -Quiet          no output at all unless something was published

param(
    [switch]$Loop,
    [int]$PollSeconds = 30,
    [string]$Serial = '',
    [int]$MaxPublish = 20,
    [int]$ReplayLast = 0,
    [switch]$Quiet,
    [string]$InboxOverride = '',   # tests only
    [string]$LogOverride = ''      # tests only: read this local jsonl instead of pulling
)

Set-StrictMode -Off
$ErrorActionPreference = 'SilentlyContinue'

. (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Inbox    = Join-Path $RepoRoot 'logs\f8-inbox'
if (-not [string]::IsNullOrWhiteSpace($InboxOverride)) { $Inbox = $InboxOverride }
$StateFile = Join-Path $Inbox 'device-state.json'
$StageRoot = Join-Path $Inbox 'device'

$DevicePkg = 'com.denellestudios.echoesofelarion'
$DeviceDir = "/sdcard/Android/data/$DevicePkg/files"

# The desktop daemon's kindSkip is 'session_start|scene_loaded|note'. This is the same rule stated
# as an allow-list, which is safer: a NEW noise kind added to the harness cannot leak in here.
$SignalKinds = @('flagged', 'error', 'exception', 'possible_softlock', 'softlock')

function Write-Bridge([string]$Text) {
    if ($Quiet) { return }
    Write-Host $Text
}

# WO-1460. Every pass stamps HEARTBEAT.json, INCLUDING the passes that publish nothing - those are
# the dangerous ones. On 2026-09-06 this bridge polled healthily all day while its kind+message
# dedupe suppressed 319 signal entries (2 possible_softlock and one of the owner's own FLAG
# presses among them), and the inbox looked exactly like a dead daemon. reason= names WHY a pass
# was quiet, so "no phone" / "no new signal" / "all deduped" are never again the same silence.
function Beat-Bridge([string]$Reason, [string]$DevSerial, [string]$LastDeviceUtc, [string]$Detail) {
    Write-F8Heartbeat $Inbox 'device' @{
        reason        = $Reason
        serial        = $DevSerial
        lastDeviceUtc = $LastDeviceUtc
        detail        = $Detail
        pollSeconds   = $PollSeconds
    }
}

# -- adb resolution ----------------------------------------------------------------------------
function Resolve-Adb {
    $cands = @()
    if (-not [string]::IsNullOrWhiteSpace($env:EOA_ADB)) { $cands += $env:EOA_ADB }
    $onPath = Get-Command 'adb.exe' -ErrorAction SilentlyContinue
    if ($onPath) { $cands += $onPath.Source }
    foreach ($sdk in @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME, (Join-Path $env:LOCALAPPDATA 'Android\Sdk'))) {
        if ([string]::IsNullOrWhiteSpace($sdk)) { continue }
        $cands += (Join-Path $sdk 'platform-tools\adb.exe')
    }
    $hubs = @()
    foreach ($pf in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, 'C:\Program Files', 'D:\Program Files')) {
        if ([string]::IsNullOrWhiteSpace($pf)) { continue }
        $hubs += (Join-Path $pf 'Unity\Hub\Editor')
    }
    foreach ($hub in ($hubs | Select-Object -Unique)) {
        if (-not (Test-Path $hub)) { continue }
        foreach ($ed in (Get-ChildItem -Path $hub -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
            $cands += (Join-Path $ed.FullName 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe')
        }
    }
    foreach ($c in $cands) {
        if ([string]::IsNullOrWhiteSpace($c)) { continue }
        if (Test-Path $c) { return (Resolve-Path $c).Path }
    }
    return ''
}

function Get-AttachedSerial([string]$Adb, [string]$Want) {
    $out = & $Adb devices 2>$null
    $ready = @()
    foreach ($line in @($out)) {
        $t = ([string]$line).Trim()
        if ([string]::IsNullOrWhiteSpace($t)) { continue }
        if ($t -match '^List of devices') { continue }
        if ($t -match '^\*') { continue }
        $parts = $t -split '\s+'
        if ($parts.Count -lt 2) { continue }
        # 'unauthorized' / 'offline' / 'recovery' are NOT usable. They are also not errors worth
        # shouting about on a poll loop -- the phone is simply not ready.
        if ($parts[1] -ne 'device') { continue }
        $ready += $parts[0]
    }
    if (-not [string]::IsNullOrWhiteSpace($Want)) {
        if ($ready -contains $Want) { return $Want }
        return ''
    }
    if ($ready.Count -gt 0) { return $ready[0] }
    return ''
}

# -- device-side read watermark ----------------------------------------------------------------
# This file is NOT triage state. It records only how far into the device log we have read, so a
# pull is incremental and idempotent. ACK.json stays the single ack authority (WO-965/WO-1018).
function Get-DeviceState {
    $empty = @{ devices = @{} }
    if (-not (Test-Path $StateFile)) { return $empty }
    try {
        $j = Get-Content $StateFile -Raw | ConvertFrom-Json
        $map = @{}
        if ($j.devices) {
            foreach ($prop in $j.devices.PSObject.Properties) {
                $v = $prop.Value
                $map[[string]$prop.Name] = @{
                    lineOffset = [int]$v.lineOffset
                    lastUtc    = [string]$v.lastUtc
                    seen       = @(@($v.seen) | ForEach-Object { [string]$_ })
                    pulled     = $(if ($v.pulled) { $v.pulled } else { $null })
                    updatedUtc = [string]$v.updatedUtc
                }
            }
        }
        return @{ devices = $map }
    } catch { return $empty }
}

function Save-DeviceState($State) {
    $devices = @{}
    foreach ($k in @($State.devices.Keys)) {
        $d = $State.devices[$k]
        $devices[$k] = @{
            lineOffset = [int]$d.lineOffset
            lastUtc    = [string]$d.lastUtc
            seen       = @(@($d.seen))
            pulled     = $d.pulled
            updatedUtc = (Get-Date).ToUniversalTime().ToString('o')
        }
    }
    $obj = @{ note = 'WO-1227 device READ watermark only. Not triage state - ACK.json is the only ack authority.'; devices = $devices }
    New-Item -ItemType Directory -Force -Path $Inbox | Out-Null
    Write-F8Text $StateFile ($obj | ConvertTo-Json -Depth 6)
}

function Get-EntryKey([string]$Kind, [string]$Message) {
    $m = [string]$Message
    if ($m.Length -gt 200) { $m = $m.Substring(0, 200) }
    $raw = ($Kind + '|' + $m)
    $sha = [System.Security.Cryptography.SHA1]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($raw))
        return (($bytes | ForEach-Object { $_.ToString('x2') }) -join '').Substring(0, 16)
    } finally { $sha.Dispose() }
}

# -- screenshots ---------------------------------------------------------------------------------
# BreakCaptureHarness writes two families next to the log: break_<NN>_<kind>.png (a rotating slot
# per kind) and flag_<yyyyMMdd-HHmmss>_<NN>.png (the owner's on-screen FLAG). Screenshots are
# PRIMARY EVIDENCE for visual defects, so a device capture that cannot show one is half a capture.
# Pull is size+mtime keyed so a poll does not re-drag 30 MB off the phone every 30 seconds.
function Sync-DeviceShots([string]$Adb, [string]$Serial, $Dev) {
    $stage = Join-Path $StageRoot $Serial
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    $pulled = @{}
    if ($Dev.pulled) {
        foreach ($prop in $Dev.pulled.PSObject.Properties) { $pulled[[string]$prop.Name] = [string]$prop.Value }
    }
    $listing = & $Adb -s $Serial shell "ls -l $DeviceDir" 2>$null
    $got = 0
    foreach ($line in @($listing)) {
        $t = ([string]$line).Trim()
        if ($t -notmatch '(break_[^\s]+\.png|flag_[^\s]+\.png)$') { continue }
        $name = $Matches[1]
        $stampKey = $t
        if ($t -match '\s(\d+)\s+(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2})\s') { $stampKey = ($Matches[1] + '|' + $Matches[2]) }
        if ($pulled.ContainsKey($name) -and $pulled[$name] -eq $stampKey -and (Test-Path (Join-Path $stage $name))) { continue }
        # -a preserves the device mtime, which is what lets a capture name its nearest screenshot.
        & $Adb -s $Serial pull -a "$DeviceDir/$name" (Join-Path $stage $name) 2>&1 | Out-Null
        if (Test-Path (Join-Path $stage $name)) { $pulled[$name] = $stampKey; $got++ }
    }
    $Dev.pulled = ([pscustomobject]$pulled)
    return @{ Stage = $stage; New = $got }
}

# Candidate screenshots for one entry. HONEST BY CONSTRUCTION: the harness rotates break_*.png
# through numbered slots per kind, so there is no reliable 1:1 entry->file mapping. We therefore
# name CANDIDATES rather than asserting "this is the shot", and never claim more than we know.
function Get-ShotCandidates([string]$Stage, [string]$Kind) {
    if (-not (Test-Path $Stage)) { return @() }
    $pat = @("break_*_$Kind.png")
    if ($Kind -eq 'flagged') { $pat = @('flag_*.png', 'break_*_flagged.png') }
    $files = @()
    foreach ($p in $pat) {
        $files += @(Get-ChildItem -Path $Stage -Filter $p -File -ErrorAction SilentlyContinue)
    }
    return @($files | Sort-Object LastWriteTime -Descending | Select-Object -First 3)
}

# -- one pass -------------------------------------------------------------------------------------
function Invoke-BridgePass {
    $adb = ''
    $devSerial = 'local'
    $localLog = ''
    $stage = ''

    if (-not [string]::IsNullOrWhiteSpace($LogOverride)) {
        # test/fixture mode: no adb, no phone, same publish path.
        if (-not (Test-Path $LogOverride)) {
            Beat-Bridge 'fixture-log-missing' 'fixture' '' 'fixture log path does not exist'
            Write-Bridge 'F8_DEVICE_BRIDGE noop reason=fixture-log-missing published=0'
            return 0
        }
        $localLog = (Resolve-Path $LogOverride).Path
        $devSerial = 'fixture'
        $stage = Join-Path $StageRoot $devSerial
    } else {
        $adb = Resolve-Adb
        if ([string]::IsNullOrWhiteSpace($adb)) {
            # Not an error: a machine with no Android SDK simply has no device half. Silent.
            Beat-Bridge 'no-adb' '' '' 'adb.exe not resolvable on this machine - the device half cannot run'
            Write-Bridge 'F8_DEVICE_BRIDGE noop reason=no-adb published=0'
            return 0
        }
        # $script:Serial, NOT $Serial. PowerShell variable names are CASE-INSENSITIVE, so the
        # local $devSerial used to be named $serial and SHADOWED the -Serial parameter: the
        # function was handed its own placeholder 'local' as the wanted serial, found no match,
        # and reported no-device WITH A PHONE PLUGGED IN. Measured on 2026-08-26. Keep the local
        # name distinct from the parameter name.
        $devSerial = Get-AttachedSerial $adb $script:Serial
        if ([string]::IsNullOrWhiteSpace($devSerial)) {
            Beat-Bridge 'no-device' '' '' 'no adb device in state device - phone unplugged, offline or unauthorized'
            Write-Bridge 'F8_DEVICE_BRIDGE noop reason=no-device published=0'
            return 0
        }
        $stage = Join-Path $StageRoot $devSerial
        New-Item -ItemType Directory -Force -Path $stage | Out-Null
        $localLog = Join-Path $stage 'break-log.jsonl'
        # Device paths are passed to adb.exe DIRECTLY from PowerShell. Never route this through Git
        # Bash: MSYS rewrites /sdcard/... to C:/Program Files/Git/sdcard/... and the pull fails.
        & $adb -s $devSerial pull -a "$DeviceDir/break-log.jsonl" $localLog 2>&1 | Out-Null
        if (-not (Test-Path $localLog)) {
            # The app may never have run on this phone, or storage is not readable. Not spam-worthy.
            Beat-Bridge 'no-break-log' $devSerial '' 'adb pull produced no break-log.jsonl'
            Write-Bridge 'F8_DEVICE_BRIDGE noop reason=no-break-log published=0'
            return 0
        }
    }

    $state = Get-DeviceState
    if (-not $state.devices.ContainsKey($devSerial)) {
        $state.devices[$devSerial] = @{ lineOffset = -1; lastUtc = ''; seen = @(); pulled = $null; updatedUtc = '' }
    }
    $dev = $state.devices[$devSerial]

    $lines = @(Get-Content $localLog -ErrorAction SilentlyContinue)
    $count = $lines.Count

    $seen = @{}
    foreach ($h in @($dev.seen)) { if ($h) { $seen[[string]$h] = $true } }
    $seenOrder = @(@($dev.seen) | Where-Object { $_ })

    # Which lines carry signal at all -- needed for both the first-run baseline and -ReplayLast.
    $eligible = @()
    for ($i = 0; $i -lt $count; $i++) {
        $raw = [string]$lines[$i]
        if ([string]::IsNullOrWhiteSpace($raw)) { continue }
        $raw = $raw.TrimStart([char]0xFEFF)   # the harness writes a BOM on the first line
        $e = $null
        try { $e = $raw | ConvertFrom-Json } catch { $e = $null }
        if ($null -eq $e) { continue }
        $k = [string]$e.kind
        if ($SignalKinds -notcontains $k) { continue }
        $eligible += $i
    }

    $offset = [int]$dev.lineOffset
    if ($offset -lt 0) {
        # FIRST RUN for this device. Baseline at the end exactly as f8-watch-daemon.ps1 does for a
        # break-log it has never seen: the five weeks already on the phone are handled ONCE by
        # f8-device-backfill-digest.ps1, because importing them live buries today's captures.
        $offset = $count
        if ($ReplayLast -gt 0 -and $eligible.Count -gt 0) {
            $take = [Math]::Min($ReplayLast, $eligible.Count)
            $offset = [int]$eligible[$eligible.Count - $take]
        }
        Write-F8Event $Inbox 'info' ("device bridge: first run for $devSerial - baselined at line $offset of $count (WO-1227)")
    } elseif ($offset -gt $count) {
        # log cleared or rotated. Replay from 0 is safe ONLY because lastUtc still suppresses
        # everything already published; without that this branch would republish all 736 entries.
        Write-F8Event $Inbox 'warn' ("device bridge: $devSerial break-log shrank ($offset -> $count lines) - rotated; rescanning from 0, lastUtc guard suppresses anything already published")
        $offset = 0
    }

    $shots = $null
    $published = 0
    $lastUtc = [string]$dev.lastUtc
    $newOffset = $offset
    $skippedDup = 0
    $deferred = 0

    for ($i = $offset; $i -lt $count; $i++) {
        $newOffset = $i + 1
        $raw = [string]$lines[$i]
        if ([string]::IsNullOrWhiteSpace($raw)) { continue }
        $raw = $raw.TrimStart([char]0xFEFF)
        $e = $null
        try { $e = $raw | ConvertFrom-Json } catch { $e = $null }
        if ($null -eq $e) { continue }

        $kind = [string]$e.kind
        if ($SignalKinds -notcontains $kind) { continue }

        $utc = [string]$e.utc
        # Secondary watermark. Survives a log rotation, a re-pull, and a state file restored from
        # backup -- all three of which would otherwise replay history into the live queue.
        if ($utc -and $lastUtc -and ([string]::Compare($utc, $lastUtc, $true) -le 0)) { continue }

        $msg = [string]$e.message
        $key = Get-EntryKey $kind $msg
        if ($seen.ContainsKey($key)) { $skippedDup++; continue }

        if ($published -ge $MaxPublish) {
            # stop cleanly and leave the rest for the next poll: a burst must not bury the queue.
            $newOffset = $i
            $deferred = $count - $i
            break
        }

        if ($null -eq $shots -and -not [string]::IsNullOrWhiteSpace($adb)) {
            $shots = Sync-DeviceShots $adb $devSerial $dev
        }

        $nl = [Environment]::NewLine
        $shotLines = @()
        foreach ($s in (Get-ShotCandidates $stage $kind)) {
            $shotLines += ('- {0}  ({1})' -f $s.FullName, $s.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))
        }
        if ($shotLines.Count -eq 0) { $shotLines = @('- (none pulled for this kind)') }

        $stackTxt = [string]$e.stack
        if ([string]::IsNullOrWhiteSpace($stackTxt)) { $stackTxt = '(no stack recorded)' }

        $md = @(
            '# F8 Capture - DEVICE (auto-inbox seq=__F8SEQ__)'
            ''
            ('**Source:** Android device `{0}`  (WO-1227 device bridge)' -f $devSerial)
            ('**Device time (utc):** {0}' -f $utc)
            ('**Bridged (local):** {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
            ('**Kind:** {0}' -f $kind)
            ('**Scene:** {0}' -f [string]$e.scene)
            ''
            '## Message'
            '```'
            $msg
            '```'
            ''
            '## Stack'
            '```'
            $stackTxt
            '```'
            ''
            '## Raw break-log entry'
            '```json'
            $raw
            '```'
            ''
            '## Screenshot candidates (pulled from the device)'
            '> The harness rotates break_<NN>_<kind>.png through numbered slots per kind, so these are'
            '> CANDIDATES by kind and recency, not a proven 1:1 match to this entry.'
            ($shotLines -join $nl)
            ''
            '## Triage'
            '- Read this file before any code-read or theory (CLAUDE.md section 12).'
            '- Screenshots are PRIMARY EVIDENCE for visual/spatial defects.'
            '- Route per docs/TICKET_PIPELINE.md.'
            '- Ack when done: f8-ack.ps1  (acks THIS capture only; a queued backlog stays pending)'
            ''
        ) -join $nl

        $sum = ('[device {0}] {1}' -f $kind, $msg)
        $sum = $sum -replace '\s+', ' '
        if ($sum.Length -gt 160) { $sum = $sum.Substring(0, 160) }

        $seq = Publish-F8Capture -Inbox $Inbox -Kind $kind -Md $md -Source 'device' `
            -BaseName 'capture-device' -Summary $sum `
            -PingMessage 'F8 DEVICE capture - triage now (read LATEST_CAPTURE.md or run f8-check-inbox.ps1)'

        $published++
        $seen[$key] = $true
        $seenOrder += $key
        if ($utc -and ([string]::Compare($utc, $lastUtc, $true) -gt 0)) { $lastUtc = $utc }
        Write-Bridge ('F8_DEVICE_CAPTURE seq={0} kind={1} utc={2}' -f $seq, $kind, $utc)
    }

    # bounded rolling dedupe memory: keep the newest 400 keys, drop the rest.
    if ($seenOrder.Count -gt 400) { $seenOrder = @($seenOrder[($seenOrder.Count - 400)..($seenOrder.Count - 1)]) }

    $dev.lineOffset = $newOffset
    $dev.lastUtc = $lastUtc
    $dev.seen = $seenOrder
    $state.devices[$devSerial] = $dev
    Save-DeviceState $state

    if ($published -gt 0) {
        Beat-Bridge 'published' $devSerial $lastUtc ('published={0} dupSuppressed={1} offset={2}/{3}' -f $published, $skippedDup, $newOffset, $count)
        Write-Host ('F8_DEVICE_BRIDGE_OK device={0} published={1} dupSuppressed={2} offset={3}/{4}' -f $devSerial, $published, $skippedDup, $newOffset, $count)
        if ($deferred -gt 0) {
            Write-Host ('F8_DEVICE_BRIDGE deferred={0} line(s) past -MaxPublish {1} - they surface on the next pass, oldest first.' -f $deferred, $MaxPublish)
        }
        Write-Host 'F8 INBOX PING (device) - TRIAGE NOW: run f8-check-inbox.ps1'
    } else {
        # NAME the silence. all-deduped is NOT the same as nothing-new: WO-1460's 319 suppressed
        # entries read identically to a quiet phone until this reason was recorded.
        $why = 'no-new-signal'
        if ($skippedDup -gt 0) { $why = 'all-deduped' }
        Beat-Bridge $why $devSerial $lastUtc ('published=0 dupSuppressed={0} offset={1}/{2}' -f $skippedDup, $newOffset, $count)
        Write-Bridge ('F8_DEVICE_BRIDGE noop reason={0} device={1} published=0 offset={2}/{3} dupSuppressed={4}' -f $why, $devSerial, $newOffset, $count, $skippedDup)
    }
    return $published
}

if ($Loop) {
    Write-Host ('[f8-device-bridge] armed poll={0}s dir={1}' -f $PollSeconds, $DeviceDir)
    while ($true) {
        try { [void](Invoke-BridgePass) } catch {
            Write-F8Event $Inbox 'warn' ("device bridge pass failed: " + $_.Exception.Message)
            # survive and keep beating: a thrown pass must not read as a dead bridge (WO-1460)
            Beat-Bridge 'pass-failed' '' '' $_.Exception.Message
        }
        Start-Sleep -Seconds $PollSeconds
    }
}

[void](Invoke-BridgePass)
exit 0
