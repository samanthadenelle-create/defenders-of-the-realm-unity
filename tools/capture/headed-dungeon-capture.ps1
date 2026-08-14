<#
.SYNOPSIS
  Drive the BUILT Windows player with REAL keyboard input and screenshot each beat,
  REFUSING to emit a marker or artifacts unless the run provably happened.

.WHY THIS EXISTS
  The 2026-08-10 headed dungeon proof (docs/proof/2026-08-10-dungeon-headed*) was produced by a
  driver written inline in a session and never committed. The evidence survived; the instrument
  did not. That is the tools/webbot/ failure mode - the eyes on the build living only in someone's
  scrollback - so this is the same capability, committed.

.WHY REAL INPUT AND NOT A SCRIPTED MOVE
  SendInput with KEYEVENTF_SCANCODE puts events on the same path a physical key takes, so Unity's
  new Input System (Keyboard.current) sees them. A scripted transform write would prove the mover
  works and nothing about whether INPUT reaches the mover - which is the exact seam WO-968/WO-1016
  were about. Do not "simplify" this to a scripted move.

.WO-988 - WHY THIS SCRIPT REFUSES SO MUCH
  On 2026-08-14 a run tagged wo1007-portal-camera printed "HEADED_CAPTURE_OK 10 shots" while the
  player sat in the TOWN with Time.timeScale=0.00 and the synthetic WASD typing into an open
  bug-report text field. All ten PNGs landed in docs/proof/ under a ticket's name. Preserved as
  docs/proof/2026-08-14-wo1007-portal-camera/ with an INVALID_CAPTURE_README.md.
  A capture that cannot fail does not merely omit evidence, it MANUFACTURES it. So this script now
  reads the LIVE Player.log and refuses - non-zero exit, NO marker, and nothing written into the
  tagged proof directory - unless all four of these hold:
     1. the ACTIVE scene equals -Scene          (accepting a parameter and ignoring it was the bug)
     2. Time.timeScale > 0                      (a stopped clock makes every drive beat a no-op)
     3. no in-game modal / text input has focus (keystrokes must reach the game, not a textbox)
     4. the hero POSITION CHANGED between 01_idle and 03_forward_far
  1-3 are preconditions, checked BEFORE a single shot is written. 4 is the outcome - the only
  end-to-end proof the drive actually drove - and the measured start/end positions are printed
  ON the marker line so a reader SEES the movement instead of trusting it.
  Shots are written to a STAGING directory and only published to docs/proof/<stamp>-<tag> once
  every check passes. A failed run publishes to <dir>-INVALID with a README naming the failure,
  so the evidence survives but can never be mistaken for proof.

.WHAT IT CANNOT DO
  It proves what the PLAYER RENDERS. It does not judge whether that looks right - open the PNGs.
  Pair it with the Player.log [Flow:*] heartbeats from the SAME run; a screenshot without its
  matching trace is half the evidence (memory: screenshots-are-primary-evidence-for-visual-defects).

.EXAMPLE
  .\tools\capture\headed-dungeon-capture.ps1 -Scene Dungeon_HealersCottage -Tag camera-framing

.EXAMPLE
  # Exercise the refusal logic itself, without a player. Same functions the live run calls.
  .\tools\capture\headed-dungeon-capture.ps1 -SelfTest SceneMismatch
  .\tools\capture\headed-dungeon-capture.ps1 -SelfTest All
#>
[CmdletBinding()]
param(
    [string] $Scene      = 'Dungeon_HealersCottage',
    [string] $Tag        = 'headed',
    [int]    $Width      = 1280,
    [int]    $Height     = 720,
    [int]    $LoadWaitSec = 25,
    [string] $ExePath    = 'Builds\Windows\DefendersOfTheRealm.exe',
    # Minimum planar distance (metres) between the 01_idle and 03_forward_far hero samples that
    # counts as "it moved". Two forward holds of 1.2s each travel metres; 0.25 is noise-floor.
    [double] $MinMoveMeters = 0.25,
    # Run the precondition/outcome checks against fixtures instead of a live player. The fixtures
    # exercise THE SAME functions the live path calls, so a green self-test is about those checks
    # and nothing else - it does not prove the player launches.
    [ValidateSet('', 'SceneMismatch', 'FrozenClock', 'FocusStolen', 'NoMovement', 'Healthy', 'All')]
    [string] $SelfTest   = '',
    # Run the SAME gate against an already-captured Player.log and report what it would have done.
    # Point it at docs/proof/2026-08-14-wo1007-portal-camera/Player.log to watch the gate reject
    # the exact run that printed HEADED_CAPTURE_OK. No player, no artifacts.
    [string] $VerifyLog  = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repoRoot

# Exit codes. Distinct per cause: "capture failed" reproduces the WO-988 defect one level up.
$EXIT_NO_EXE        = 2
$EXIT_LAUNCH        = 3
$EXIT_NO_FOREGROUND = 4
$EXIT_SCENE         = 5
$EXIT_FROZEN_CLOCK  = 6
$EXIT_FOCUS_STOLEN  = 7
$EXIT_NO_MOVEMENT   = 8
$EXIT_UNREADABLE    = 9

$logSrc = Join-Path $env:USERPROFILE 'AppData\LocalLow\DeNelle\Echoes of Elarion\Player.log'

# Say why, then exit with THAT code.
# Do NOT use Write-Error here: with $ErrorActionPreference='Stop' it is TERMINATING, so the script
# dies at the Write-Error with exit code 1 and the named code below never runs. Observed on the
# first live run of this script - the foreground-loss abort reported 1 instead of 4, i.e. the
# refusal machinery had the very defect it exists to prevent (a failure that cannot report which
# failure it was). WriteErrorLine goes to stderr without terminating.
function Stop-Capture([string]$message, [int]$code) {
    # Do not leave an empty staging directory behind in %TEMP% on every refusal.
    if ($script:StageDir -ne $null -and (Test-Path $script:StageDir)) {
        if (@(Get-ChildItem $script:StageDir -Force -ErrorAction SilentlyContinue).Count -eq 0) {
            Remove-Item $script:StageDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    $host.UI.WriteErrorLine($message)
    exit $code
}

# =============================================================================
# THE FALSIFIABLE HALF - reading the live trace. Pure functions, so -SelfTest can
# drive the exact same code the live run drives.
# =============================================================================

# Unity holds Player.log open for writing, so a plain Get-Content -Raw can throw. Open with
# FileShare.ReadWrite and read what is there right now.
function Read-LiveLog([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    $fs = $null; $sr = $null
    try {
        $fs = New-Object System.IO.FileStream($path, [System.IO.FileMode]::Open,
                  [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        $sr = New-Object System.IO.StreamReader($fs)
        return $sr.ReadToEnd()
    } finally {
        if ($sr -ne $null) { $sr.Dispose() }
        elseif ($fs -ne $null) { $fs.Dispose() }
    }
}

# Player.log can carry more than one session. Everything downstream must look at THIS run only,
# or a stale dungeon session certifies a town run - which is half of what WO-988 documents.
function Get-SessionSlice([string]$text) {
    if ([string]::IsNullOrEmpty($text)) { return '' }
    $i = $text.LastIndexOf('Initialize engine version')
    if ($i -lt 0) { return $text }
    return $text.Substring($i)
}

# One [Flow:HeroOwner] heartbeat carries scene, timeScale AND hero pos, emitted ~1/s. Parsing the
# LAST one answers all three preconditions from a single line that the game itself wrote.
$script:HeartbeatRe = "\[Flow:HeroOwner\]\s+scene='([^']*)'.*?timeScale=([0-9]+\.[0-9]+).*?pos=\(\s*(-?[0-9]+\.[0-9]+),\s*(-?[0-9]+\.[0-9]+),\s*(-?[0-9]+\.[0-9]+)\)"

function Get-LastHeartbeat([string]$sessionText) {
    if ([string]::IsNullOrEmpty($sessionText)) { return $null }
    $ms = [regex]::Matches($sessionText, $script:HeartbeatRe)
    if ($ms.Count -eq 0) { return $null }
    $m = $ms[$ms.Count - 1]
    return @{
        Scene     = $m.Groups[1].Value
        TimeScale = [double]$m.Groups[2].Value
        X         = [double]$m.Groups[3].Value
        Y         = [double]$m.Groups[4].Value
        Z         = [double]$m.Groups[5].Value
        Index     = $m.Index            # so two samples can be proven to be DIFFERENT lines
    }
}

function Format-Pos($hb) {
    if ($hb -eq $null) { return '<no heartbeat>' }
    return ("({0:F2}, {1:F2}, {2:F2})" -f $hb.X, $hb.Y, $hb.Z)
}

# Overlays that eat keystrokes. Each row is an OPEN signature and its CLOSE signature; if the last
# open is newer than the last close, that overlay owns input and the WASD beats go into it.
# NOTE, stated plainly: this table is only as complete as the game's own traces. BugReportView is
# the one that produced the WO-988 run and it has a real open/close pair. ClanChatPanel logs an
# open and NO close (Assets/_Modules/HUD/ClanChatPanel.cs SetVisible), so its open is treated as
# sticky for the rest of the session - fail-closed is the correct bias for a tool whose job is to
# refuse. Add rows here as more overlays learn to trace themselves.
$script:InputStealers = @(
    @{ Name = "BugReportView (open text field)"; Open = "\[Flow:BugReport\]\s+open";               Close = "\[Flow:BugReport\]\s+close" },
    @{ Name = "ClanChatPanel (chat text field)"; Open = "\[Flow:ClanChat\]\s+SetVisible\(true\)";  Close = $null }
)

function Get-FocusStealer([string]$sessionText) {
    if ([string]::IsNullOrEmpty($sessionText)) { return $null }
    foreach ($s in $script:InputStealers) {
        $opens = [regex]::Matches($sessionText, $s.Open)
        if ($opens.Count -eq 0) { continue }
        $lastOpen = $opens[$opens.Count - 1].Index
        $lastClose = -1
        if ($s.Close -ne $null) {
            $closes = [regex]::Matches($sessionText, $s.Close)
            if ($closes.Count -gt 0) { $lastClose = $closes[$closes.Count - 1].Index }
        }
        if ($lastOpen -gt $lastClose) { return $s.Name }
    }
    return $null
}

# THE GATE. Returns Ok plus, on failure, the exact cause and its exit code. Never a generic
# "capture failed" - naming which precondition failed is the point of WO-988.
function Test-CapturePreconditions([string]$sessionText, [string]$wantScene) {
    $hb = Get-LastHeartbeat $sessionText
    if ($hb -eq $null) {
        return @{ Ok = $false; Code = $EXIT_UNREADABLE; Heartbeat = $null; Reason =
            ("PRECONDITION UNREADABLE: no [Flow:HeroOwner] heartbeat in this session's log, so the " +
             "active scene, the world clock and the hero position are all UNKNOWN. Either the " +
             "requested scene never loaded (the player is still on Title/boot, where no hero " +
             "exists) or FlowTrace is disabled. Refusing to capture - an unverifiable run is " +
             "exactly what this harness now exists to reject.") }
    }
    if ($hb.Scene -ne $wantScene) {
        return @{ Ok = $false; Code = $EXIT_SCENE; Heartbeat = $hb; Reason =
            ("SCENE MISMATCH: -Scene asked for '$wantScene' but the player is in '" + $hb.Scene +
             "'. This is the WO-988 defect verbatim: the parameter was accepted, printed in the " +
             "launch line and never enforced, so ten shots of the TOWN were filed as dungeon " +
             "proof. Check -bootScene '$wantScene' is in Build Settings (DevBootScene logs " +
             "'is not in Build Settings - ignoring' and boots the normal flow instead).") }
    }
    if ($hb.TimeScale -le 0.0) {
        return @{ Ok = $false; Code = $EXIT_FROZEN_CLOCK; Heartbeat = $hb; Reason =
            ("WORLD CLOCK FROZEN: Time.timeScale=" + ("{0:F2}" -f $hb.TimeScale) + " in scene '" +
             $hb.Scene + "'. Every hero writer scales by Time.deltaTime, so the hero cannot move, " +
             "turn or animate - each drive beat would be a no-op against a still frame. A freeze " +
             "owner (PauseController background auto-pause / an open modal / a BreakCaptureHarness " +
             "F8 note) has not restored it. Refusing to capture.") }
    }
    $stealer = Get-FocusStealer $sessionText
    if ($stealer -ne $null) {
        return @{ Ok = $false; Code = $EXIT_FOCUS_STOLEN; Heartbeat = $hb; Reason =
            ("INPUT FOCUS STOLEN by " + $stealer + ": an in-game overlay with a text field is open " +
             "and unclosed in this session, so the synthetic WASD would be TYPED INTO IT rather " +
             "than driving the hero. This is what 10_facing_exit.png in " +
             "docs/proof/2026-08-14-wo1007-portal-camera/ shows. Close it and re-run.") }
    }
    return @{ Ok = $true; Code = 0; Heartbeat = $hb; Reason = '' }
}

# The OUTCOME check. Preconditions say the run COULD move; only this says it DID.
function Test-HeroMoved($startHb, $endHb, [double]$minMeters) {
    if ($startHb -eq $null -or $endHb -eq $null) {
        return @{ Ok = $false; Dist = 0.0; Reason =
            "hero position could not be sampled at both ends of the drive (missing heartbeat)" }
    }
    if ($endHb.Index -le $startHb.Index) {
        return @{ Ok = $false; Dist = 0.0; Reason =
            ("both samples came from the SAME heartbeat line, so nothing was actually compared - " +
             "the trace did not advance during the drive") }
    }
    $dx = $endHb.X - $startHb.X
    $dz = $endHb.Z - $startHb.Z
    $d  = [Math]::Sqrt(($dx * $dx) + ($dz * $dz))
    if ($d -lt $minMeters) {
        return @{ Ok = $false; Dist = $d; Reason =
            ("hero moved {0:F2}m, under the {1:F2}m floor - the drive did not drive" -f $d, $minMeters) }
    }
    return @{ Ok = $true; Dist = $d; Reason = '' }
}

# =============================================================================
# SELF-TEST - drives the functions above with fixtures. No player, no artifacts.
# =============================================================================
function New-HeartbeatLine([string]$scene, [string]$timeScale, [string]$pos) {
    return ("[Flow:HeroOwner] scene='{0}' owner=HeroLocomotion ownerCC=none ownerAgent=on-mesh " +
            "scriptedMove=off velSelf=0.00 velRoot=0.00 animFeed=velSelf animSpeed=0.00 " +
            "rootYaw=90.0 basis=Camera.main(flattened) basisYaw=90.0 timeScale={1} dt=0.0167 " +
            "inputSuppressed=False autoWalk=False mainCamYaw=90.0 pos={2}`r`n") -f $scene, $timeScale, $pos
}

# ONE engine-init header per fixture session, exactly like a real Player.log. The drive samples
# are two heartbeats appended to the SAME session, so Test-HeroMoved is comparing distinct lines.
function New-FixtureLog([string]$scene, [string]$timeScale, [string]$pos, [string]$extra) {
    $head = "Initialize engine version: 6000.4.8f1 (f8b72d3d7343)`r`n"
    if (-not [string]::IsNullOrEmpty($extra)) { $head += $extra + "`r`n" }
    return $head + (New-HeartbeatLine $scene $timeScale $pos)
}

# Sets $script:SelfTestExit rather than RETURNING the code. In PowerShell a function's return
# value is everything it wrote to the output stream, so "exit (Invoke-SelfTestCase X)" would
# swallow every Write-Output line into the return value and print nothing - which is itself a
# hollow assertion (a check whose result you cannot see). Learned by running it.
function Invoke-SelfTestCase([string]$case) {
    $script:SelfTestExit = 1
    $want = 'Dungeon_HealersCottage'
    switch ($case) {
        'SceneMismatch' {
            $t = New-FixtureLog 'Main_Castle_Overworld' '1.00' '(-2.88, 0.08, 0.23)' ''
            $r = Test-CapturePreconditions (Get-SessionSlice $t) $want
            Write-Output ("[selftest] SceneMismatch  -> Ok=" + $r.Ok + " Code=" + $r.Code)
            Write-Output ("CAPTURE_REFUSED: " + $r.Reason)
            $script:SelfTestExit = $r.Code
            return
        }
        'FrozenClock' {
            $t = New-FixtureLog $want '0.00' '(-28.00, 0.08, 0.00)' ''
            $r = Test-CapturePreconditions (Get-SessionSlice $t) $want
            Write-Output ("[selftest] FrozenClock    -> Ok=" + $r.Ok + " Code=" + $r.Code)
            Write-Output ("CAPTURE_REFUSED: " + $r.Reason)
            $script:SelfTestExit = $r.Code
            return
        }
        'FocusStolen' {
            $t = New-FixtureLog $want '1.00' '(-28.00, 0.08, 0.00)' `
                 '[Flow:BugReport] open - capturing clean frame before the form draws'
            $r = Test-CapturePreconditions (Get-SessionSlice $t) $want
            Write-Output ("[selftest] FocusStolen    -> Ok=" + $r.Ok + " Code=" + $r.Code)
            Write-Output ("CAPTURE_REFUSED: " + $r.Reason)
            $script:SelfTestExit = $r.Code
            return
        }
        'NoMovement' {
            $t1 = New-FixtureLog $want '1.00' '(-28.00, 0.08, 0.00)' ''
            $t2 = $t1 + (New-HeartbeatLine $want '1.00' '(-28.00, 0.08, 0.00)')
            $a = Get-LastHeartbeat (Get-SessionSlice $t1)
            $b = Get-LastHeartbeat (Get-SessionSlice $t2)
            $m = Test-HeroMoved $a $b $MinMoveMeters
            Write-Output ("[selftest] NoMovement     -> Ok=" + $m.Ok)
            Write-Output ("CAPTURE_REFUSED: HERO DID NOT MOVE: " + $m.Reason)
            Write-Output ("  01_idle hero " + (Format-Pos $a) + "   03_forward_far hero " + (Format-Pos $b))
            if ($m.Ok) { $script:SelfTestExit = 0 } else { $script:SelfTestExit = $EXIT_NO_MOVEMENT }
            return
        }
        'Healthy' {
            $t1 = New-FixtureLog $want '1.00' '(-28.00, 0.08, 0.00)' ''
            $t2 = $t1 + (New-HeartbeatLine $want '1.00' '(-24.10, 0.08, 0.00)')
            $r  = Test-CapturePreconditions (Get-SessionSlice $t2) $want
            $a  = Get-LastHeartbeat (Get-SessionSlice $t1)
            $b  = Get-LastHeartbeat (Get-SessionSlice $t2)
            $m  = Test-HeroMoved $a $b $MinMoveMeters
            Write-Output ("[selftest] Healthy        -> preconditions Ok=" + $r.Ok + " moved Ok=" + $m.Ok)
            if ($r.Ok -and $m.Ok) {
                Write-Output ("SELFTEST_HEALTHY_OK  hero " + (Format-Pos $a) + " -> " + (Format-Pos $b) +
                              ("  moved {0:F2}m" -f $m.Dist))
                $script:SelfTestExit = 0
                return
            }
            Write-Output ("SELFTEST_HEALTHY_FAILED: " + $r.Reason + " " + $m.Reason)
            $script:SelfTestExit = 1
            return
        }
    }
}

if ($SelfTest -ne '') {
    if ($SelfTest -eq 'All') {
        $expect = @{ SceneMismatch = $EXIT_SCENE; FrozenClock = $EXIT_FROZEN_CLOCK
                     FocusStolen = $EXIT_FOCUS_STOLEN; NoMovement = $EXIT_NO_MOVEMENT; Healthy = 0 }
        $bad = 0
        foreach ($k in @('SceneMismatch','FrozenClock','FocusStolen','NoMovement','Healthy')) {
            Invoke-SelfTestCase $k
            $got = $script:SelfTestExit
            if ($got -ne $expect[$k]) {
                Write-Output ("  MISMATCH: " + $k + " expected exit " + $expect[$k] + " got " + $got)
                $bad++
            }
            Write-Output ''
        }
        if ($bad -gt 0) { Write-Output ("SELFTEST_FAILED " + $bad + " case(s)"); exit 1 }
        Write-Output 'SELFTEST_OK 5/5 cases'
        exit 0
    }
    Invoke-SelfTestCase $SelfTest
    exit $script:SelfTestExit
}

if ($VerifyLog -ne '') {
    if (-not (Test-Path $VerifyLog)) { Stop-Capture "VERIFY_FAIL: no log at $VerifyLog" $EXIT_UNREADABLE }
    $vt = Get-SessionSlice (Read-LiveLog $VerifyLog)
    $vr = Test-CapturePreconditions $vt $Scene
    if (-not $vr.Ok) {
        Write-Output ("[verify] " + $VerifyLog)
        Write-Output ("CAPTURE_WOULD_HAVE_BEEN_REFUSED: " + $vr.Reason)
        exit $vr.Code
    }
    # Preconditions hold; report the session's first-to-last hero travel as the movement proxy.
    $vms = [regex]::Matches($vt, $script:HeartbeatRe)
    $vFirst = @{ X = [double]$vms[0].Groups[3].Value; Y = [double]$vms[0].Groups[4].Value
                 Z = [double]$vms[0].Groups[5].Value; Index = $vms[0].Index }
    $vLast  = $vr.Heartbeat
    $vm = Test-HeroMoved $vFirst $vLast $MinMoveMeters
    Write-Output ("[verify] " + $VerifyLog)
    Write-Output ("  scene='" + $vr.Heartbeat.Scene + "' timeScale=" + ("{0:F2}" -f $vr.Heartbeat.TimeScale) +
                  "  hero " + (Format-Pos $vFirst) + " -> " + (Format-Pos $vLast) +
                  ("  moved {0:F2}m" -f $vm.Dist))
    if (-not $vm.Ok) { Write-Output ("CAPTURE_WOULD_HAVE_BEEN_REFUSED: " + $vm.Reason); exit $EXIT_NO_MOVEMENT }
    Write-Output 'CAPTURE_WOULD_HAVE_PASSED'
    exit 0
}

# =============================================================================
# LIVE CAPTURE
# =============================================================================

if (-not (Test-Path $ExePath)) {
    Stop-Capture "CAPTURE_FAIL: no player at $ExePath. Run .\build-windows.ps1 first." $EXIT_NO_EXE
}

$stamp   = Get-Date -Format 'yyyy-MM-dd'
$outDir  = Join-Path $repoRoot ("docs\proof\{0}-{1}" -f $stamp, $Tag)
# WO-988: shots are STAGED here and only published to $outDir once every check passes. The tagged
# proof directory is not even created until the run has earned it.
$stageDir = Join-Path $env:TEMP ("headed-capture-{0}-{1}" -f $PID, (Get-Date -Format 'HHmmss'))
$script:StageDir = $stageDir   # so Stop-Capture can tidy an empty stage on a refusal
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win {
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    // PrintWindow with PW_RENDERFULLCONTENT (0x2) asks the window to render ITSELF into our DC.
    // Unlike CopyFromScreen it does not read screen pixels, so an occluded or background window
    // still yields ITS OWN content - which is what makes it safe to run while the machine is in use.
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    // Name the thief. "not frontmost" that cannot say WHAT took the foreground sends the reader
    // hunting; the process + title turns a re-run lottery into one fix.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, System.Text.StringBuilder s, int max);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    public static string Describe(IntPtr h) {
        var sb = new System.Text.StringBuilder(256);
        GetWindowTextW(h, sb, 256);
        uint pid = 0; GetWindowThreadProcessId(h, out pid);
        string pname = "?";
        try { pname = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { }
        return "hwnd=" + h + " pid=" + pid + " proc='" + pname + "' title='" + sb.ToString() + "'";
    }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT {
        public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
    }
    [StructLayout(LayoutKind.Explicit)] public struct INPUT {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint n, INPUT[] p, int size);

    // KEYEVENTF_SCANCODE = 0x0008, KEYEVENTF_KEYUP = 0x0002. Scan codes, not VKs: games read raw.
    public static void Key(ushort scan, bool down) {
        INPUT[] i = new INPUT[1];
        i[0].type = 1; // INPUT_KEYBOARD
        i[0].ki.wScan = scan;
        i[0].ki.dwFlags = down ? (uint)0x0008 : (uint)(0x0008 | 0x0002);
        SendInput(1, i, Marshal.SizeOf(typeof(INPUT)));
    }
}
"@

# Scan codes (set 1). W/A/S/D are the locomotion keys the dungeon hero reads.
$SC = @{ W = 0x11; A = 0x1E; S = 0x1F; D = 0x20 }

function Get-ClientBounds([IntPtr]$h) {
    $r = New-Object Win+RECT
    [void][Win]::GetClientRect($h, [ref]$r)
    $p = New-Object Win+POINT
    [void][Win]::ClientToScreen($h, [ref]$p)
    return @{ X = $p.X; Y = $p.Y; W = ($r.R - $r.L); H = ($r.B - $r.T) }
}

# [STOP] THE GUARD THAT MUST NEVER BE REMOVED (added 2026-08-14 after a real incident).
# CopyFromScreen grabs whatever pixels sit at these SCREEN COORDINATES - not the window's own
# surface. SetForegroundWindow FAILS SILENTLY when the calling process does not own the
# foreground, so an occluded game window means you photograph whatever is in front of it. On
# 2026-08-14 that was the owner's live trading terminal, with account positions and balances,
# written into docs/proof/ - a directory that gets COMMITTED.
# So: every shot asserts the game is genuinely frontmost, and REFUSES rather than capturing.
# A capture tool whose failure mode is "wrong window" instead of "no file" is a privacy leak.
function Assert-Frontmost([IntPtr]$h, [string]$name) {
    $fg = [Win]::GetForegroundWindow()
    if ($fg -ne $h) {
        throw "CAPTURE_ABORT at '$name': the game window is NOT frontmost. STOLE FOCUS: " +
              [Win]::Describe($fg) + " ; game: " + [Win]::Describe($h) + ". " +
              "Refusing to screenshot - this would capture whatever is in front of it. " +
              "Close/minimise other windows and re-run; do not weaken this check."
    }
}

# Is the bitmap essentially one flat colour? PrintWindow returns an all-black surface for some
# GPU-composited windows, and a black PNG is a SILENT failure that reads as "the game rendered
# nothing" - the worst possible lie for a tool whose whole job is evidence. Sample a grid and
# demand real variance. This is the falsifiable half of the capture.
function Test-HasContent([System.Drawing.Bitmap]$bmp) {
    $seen = @{}
    for ($x = 4; $x -lt $bmp.Width;  $x += [Math]::Max(1, [int]($bmp.Width / 24))) {
        for ($y = 4; $y -lt $bmp.Height; $y += [Math]::Max(1, [int]($bmp.Height / 24))) {
            $c = $bmp.GetPixel($x, $y)
            $seen[("{0}_{1}_{2}" -f $c.R, $c.G, $c.B)] = $true
        }
    }
    return ($seen.Count -ge 8)   # 8+ distinct sampled colours = a real frame, not a flat fill
}

function Save-Shot([IntPtr]$h, [string]$name) {
    # Re-assert per shot: the run REQUIRES focus (input), so losing it mid-sequence means the
    # hero stopped responding and this game pauses. Catch it at the frame it happens rather
    # than shipping a folder of Pause overlays that look like gameplay.
    Assert-Frontmost $h $name
    $b = Get-ClientBounds $h
    if ($b.W -le 0 -or $b.H -le 0) { Write-Warning "shot '$name': zero client rect - skipped"; return $null }

    # PATH 1 - PrintWindow: the window's OWN surface, safe while the machine is in use.
    $bmp = New-Object System.Drawing.Bitmap($b.W, $b.H)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $okPw = [Win]::PrintWindow($h, $hdc, 0x2)   # PW_RENDERFULLCONTENT
    $g.ReleaseHdc($hdc)

    if (-not ($okPw -and (Test-HasContent $bmp))) {
        # PATH 2 - screen read. ONLY legal when the game is provably frontmost, because this
        # reads SCREEN PIXELS and would otherwise capture another application (see the guard).
        $g.Dispose(); $bmp.Dispose()
        Assert-Frontmost $h $name
        $bmp = New-Object System.Drawing.Bitmap($b.W, $b.H)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($b.X, $b.Y, 0, 0, (New-Object System.Drawing.Size($b.W, $b.H)))
        if (-not (Test-HasContent $bmp)) {
            $g.Dispose(); $bmp.Dispose()
            throw "CAPTURE_ABORT at '$name': both capture paths produced a flat image. Nothing written."
        }
        $via = 'screen(frontmost-proven)'
    } else {
        $via = 'PrintWindow'
    }

    $path = Join-Path $stageDir ($name + '.png')
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output ("  shot {0,-16} via {1}" -f $name, $via)
    return $path
}

function Hold-Key([string]$k, [double]$sec, [IntPtr]$h) {
    # [uint16], not [ushort] - the C# alias does not exist as a PowerShell type accelerator.
    [Win]::Key([uint16]$SC[$k], $true)
    Start-Sleep -Milliseconds ([int]($sec * 1000))
    [Win]::Key([uint16]$SC[$k], $false)
    Start-Sleep -Milliseconds 250   # let the mover settle before the shot
}

# Publish staged shots somewhere a reader cannot mistake for proof, with the reason attached.
function Publish-Invalid([string]$reason) {
    $files = @(Get-ChildItem $stageDir -Filter *.png -ErrorAction SilentlyContinue)
    if ($files.Count -eq 0) {
        Write-Output "[capture] nothing was staged - no artifacts written anywhere."
        return
    }
    $badDir = $outDir + '-INVALID'
    New-Item -ItemType Directory -Force -Path $badDir | Out-Null
    Move-Item (Join-Path $stageDir '*.png') $badDir -Force
    $readme = @(
        '# THIS CAPTURE IS NOT PROOF. The run failed its own checks.',
        '',
        'Written by tools/capture/headed-dungeon-capture.ps1 (WO-988). It is here rather than in',
        'the tagged proof directory because a failed run must never produce a plausible artifact.',
        '',
        '## Why it failed',
        '',
        $reason,
        '',
        'Fix the cause and re-run. Do not cite these PNGs.'
    ) -join "`r`n"
    [System.IO.File]::WriteAllText((Join-Path $badDir 'INVALID_CAPTURE_README.md'), $readme)
    Write-Output ("[capture] staged shots moved to " + $badDir + " (marked INVALID, not proof)")
}

# --- launch ------------------------------------------------------------------
Get-Process DefendersOfTheRealm -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Write-Output "[capture] launching '$Scene' at ${Width}x${Height} (windowed)"
$exeArgs = @('-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height,'-bootScene',$Scene)
$proc = Start-Process -FilePath $ExePath -ArgumentList $exeArgs -PassThru

# Poll the process's OWN window handle rather than FindWindow-by-title: the title is a
# productName that creative can rename at any time, and a null lpClassName marshals badly
# from PowerShell. The handle appears ~5s in; we keep waiting for the SCENE after that.
$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 24; $i++) {
    Start-Sleep -Seconds 1
    $proc.Refresh()
    if ($proc.HasExited) { Stop-Capture "CAPTURE_FAIL: player exited during load - read Builds\Windows\...\Player.log" $EXIT_LAUNCH }
    if ($proc.MainWindowHandle -ne 0) { $hwnd = $proc.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) {
    $proc | Stop-Process -Force
    Stop-Capture "CAPTURE_FAIL: no window handle after 24s. Raise -LoadWaitSec." $EXIT_LAUNCH
}
Write-Output ("[capture] window up: '{0}'  - waiting {1}s for the SCENE to finish loading" -f $proc.MainWindowTitle, $LoadWaitSec)
Start-Sleep -Seconds $LoadWaitSec
# Raise it, pin it topmost, and then PROVE it took. SetForegroundWindow alone is unreliable.
[void][Win]::ShowWindow($hwnd, 9)                                   # SW_RESTORE
[void][Win]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0003)  # HWND_TOPMOST | NOSIZE|NOMOVE
[void][Win]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 1200

# [STOP] REFUSE, do not warn (hardened 2026-08-14 after a run produced a PAUSE MENU).
# PrintWindow can photograph a BACKGROUND window's own surface, so the earlier version merely
# warned and carried on. That was wrong for a DRIVEN capture: SendInput only reaches the
# FOCUSED window, so without focus the hero never moves - and this game pauses on focus loss,
# so the shots came back as the Pause overlay. Seven files that looked like evidence and were
# not. A capture tool that emits misleading frames is worse than one that emits none.
$isFront = $false
for ($i = 0; $i -lt 5 -and -not $isFront; $i++) {
    [void][Win]::ShowWindow($hwnd, 9)
    [void][Win]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 700
    $isFront = ([Win]::GetForegroundWindow() -eq $hwnd)
}
if (-not $isFront) {
    $proc | Stop-Process -Force -ErrorAction SilentlyContinue
    Stop-Capture ("CAPTURE_FAIL: could not hold the foreground after 5 attempts. Driven capture " +
                 "needs focus - without it SendInput never reaches the game, the hero does not " +
                 "move, and the game pauses, so every frame would be a Pause overlay. NOTHING " +
                 "was captured. Close/minimise other windows and re-run.") $EXIT_NO_FOREGROUND
}
Write-Output "[capture] foreground held - input will reach the game"

# --- WO-988 GATE: prove the run before writing a single pixel ------------------
# Give the heartbeat (throttled ~1/s) a beat to land after the foreground dance.
Start-Sleep -Seconds 2
$sessionText = Get-SessionSlice (Read-LiveLog $logSrc)
if ([string]::IsNullOrEmpty($sessionText)) {
    $proc | Stop-Process -Force -ErrorAction SilentlyContinue
    Stop-Capture ("CAPTURE_REFUSED: could not read the live Player.log at $logSrc, so nothing about " +
                 "this run can be verified. NOTHING was captured.") $EXIT_UNREADABLE
}
$pre = Test-CapturePreconditions $sessionText $Scene
if (-not $pre.Ok) {
    $proc | Stop-Process -Force -ErrorAction SilentlyContinue
    Stop-Capture ("CAPTURE_REFUSED: " + $pre.Reason + " NOTHING was captured - no marker, no files " +
                 "in docs\proof\.") $pre.Code
}
$hb0 = $pre.Heartbeat
Write-Output ("[capture] preconditions PASS: scene='" + $hb0.Scene + "' timeScale=" +
              ("{0:F2}" -f $hb0.TimeScale) + " no modal owns input")

# --- the beats ---------------------------------------------------------------
# Named so a diff against a prior run is meaningful. Keep these names stable.
$startHb = $null
$endHb   = $null
try {
    Write-Output "[capture] driving"
    Save-Shot $hwnd '01_idle'          | Out-Null
    # Sample the hero the instant the idle frame is taken. This is one half of the ONLY
    # end-to-end proof that the drive drove.
    $startHb = Get-LastHeartbeat (Get-SessionSlice (Read-LiveLog $logSrc))

    Hold-Key 'W' 1.2 $hwnd; Save-Shot $hwnd '02_forward'     | Out-Null
    Hold-Key 'W' 1.2 $hwnd; Save-Shot $hwnd '03_forward_far' | Out-Null
    # The heartbeat is throttled to ~1/s; wait so the sample below is a DIFFERENT line than
    # $startHb (Test-HeroMoved rejects two reads of the same line outright).
    Start-Sleep -Milliseconds 1500
    $endHb = Get-LastHeartbeat (Get-SessionSlice (Read-LiveLog $logSrc))

    Hold-Key 'A' 1.0 $hwnd; Save-Shot $hwnd '04_left'        | Out-Null
    Hold-Key 'D' 1.8 $hwnd; Save-Shot $hwnd '05_right'       | Out-Null
    Hold-Key 'S' 1.2 $hwnd; Save-Shot $hwnd '06_back'        | Out-Null
    Start-Sleep -Seconds 1
    Save-Shot $hwnd '07_settled'       | Out-Null

    # WALK OUT AND LOOK BACK (owner method, 2026-08-14)
    # The dungeon EXIT is seated within ~4m of the hero spawn, so at t=0 the camera is
    # effectively inside it and the shots above show geometry filling the frame rather than
    # the object. The only way to actually SEE the exit is to walk away from it and turn
    # around. Measured from the trace: a 1s A/D hold swings hero yaw ~40-45deg, so ~4s of
    # sustained turn is roughly the 180 needed. Keep these beats - they are how the exit,
    # the portal and the beacon get judged at all.
    Hold-Key 'W' 2.5 $hwnd; Save-Shot $hwnd '08_walked_out'  | Out-Null
    Hold-Key 'D' 4.0 $hwnd
    Start-Sleep -Milliseconds 600
    Save-Shot $hwnd '09_turned_back'   | Out-Null
    Hold-Key 'D' 1.2 $hwnd
    Start-Sleep -Milliseconds 600
    Save-Shot $hwnd '10_facing_exit'   | Out-Null
} catch {
    $msg = $_.Exception.Message
    $proc | Stop-Process -Force -ErrorAction SilentlyContinue
    Publish-Invalid ("The drive aborted mid-sequence: " + $msg)
    Stop-Capture ("CAPTURE_REFUSED: " + $msg) $EXIT_NO_FOREGROUND
}

# --- WO-988 OUTCOME: did the hero actually move? ------------------------------
$moved = Test-HeroMoved $startHb $endHb $MinMoveMeters
$posLine = ("hero " + (Format-Pos $startHb) + " -> " + (Format-Pos $endHb))

# --- harvest the matching trace ----------------------------------------------
if (Test-Path $logSrc) {
    $logText = Read-LiveLog $logSrc
    [System.IO.File]::WriteAllText((Join-Path $stageDir 'Player.log'), $logText)
    Write-Output "  trace copied alongside the shots (same run)"
} else {
    Write-Warning "Player.log not found at $logSrc - shots have NO matching trace, which is half the evidence"
}

Start-Sleep -Seconds 1
$proc | Stop-Process -Force -ErrorAction SilentlyContinue

if (-not $moved.Ok) {
    $reason = ("HERO DID NOT MOVE across the drive: " + $moved.Reason + ". Measured 01_idle " +
               (Format-Pos $startHb) + " and 03_forward_far " + (Format-Pos $endHb) + ".")
    Publish-Invalid $reason
    Write-Output ("  01_idle hero " + (Format-Pos $startHb) + "   03_forward_far hero " + (Format-Pos $endHb))
    Stop-Capture ("CAPTURE_REFUSED: " + $reason + " No marker; nothing written to " + $outDir + ".") $EXIT_NO_MOVEMENT
}

# Everything passed - only now does the tagged proof directory come into existence.
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Move-Item (Join-Path $stageDir '*') $outDir -Force
Remove-Item $stageDir -Recurse -Force -ErrorAction SilentlyContinue

$n = (Get-ChildItem $outDir -Filter *.png | Measure-Object).Count
Write-Output ("HEADED_CAPTURE_OK {0} shots  scene='{1}' timeScale={2:F2}  {3}  moved {4:F2}m -> {5}" -f `
              $n, $hb0.Scene, $hb0.TimeScale, $posLine, $moved.Dist, $outDir)
Write-Output "Now OPEN them. A green marker proves a frame rendered and that the hero moved, never that it looks right."
exit 0
