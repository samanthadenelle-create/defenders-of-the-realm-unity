<#
.SYNOPSIS
  Drive the BUILT Windows player with REAL keyboard input and screenshot each beat.

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

.WHAT IT CANNOT DO
  It proves what the PLAYER RENDERS. It does not judge whether that looks right - open the PNGs.
  Pair it with the Player.log [Flow:*] heartbeats from the SAME run; a screenshot without its
  matching trace is half the evidence (memory: screenshots-are-primary-evidence-for-visual-defects).

.EXAMPLE
  .\tools\capture\headed-dungeon-capture.ps1 -Scene Dungeon_HealersCottage -Tag camera-framing
#>
[CmdletBinding()]
param(
    [string] $Scene      = 'Dungeon_HealersCottage',
    [string] $Tag        = 'headed',
    [int]    $Width      = 1280,
    [int]    $Height     = 720,
    [int]    $LoadWaitSec = 25,
    [string] $ExePath    = 'Builds\Windows\DefendersOfTheRealm.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repoRoot

if (-not (Test-Path $ExePath)) {
    Write-Error "CAPTURE_FAIL: no player at $ExePath. Run .\build-windows.ps1 first."
    exit 2
}

$stamp   = Get-Date -Format 'yyyy-MM-dd'
$outDir  = Join-Path $repoRoot ("docs\proof\{0}-{1}" -f $stamp, $Tag)
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

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
        throw "CAPTURE_ABORT at '$name': the game window is NOT frontmost (foreground=$fg, game=$h). " +
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

    $path = Join-Path $outDir ($name + '.png')
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

# --- launch ------------------------------------------------------------------
Get-Process DefendersOfTheRealm -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Write-Output "[capture] launching '$Scene' at ${Width}x${Height} (windowed)"
$args = @('-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height,'-bootScene',$Scene)
$proc = Start-Process -FilePath $ExePath -ArgumentList $args -PassThru

# Poll the process's OWN window handle rather than FindWindow-by-title: the title is a
# productName that creative can rename at any time, and a null lpClassName marshals badly
# from PowerShell. The handle appears ~5s in; we keep waiting for the SCENE after that.
$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 24; $i++) {
    Start-Sleep -Seconds 1
    $proc.Refresh()
    if ($proc.HasExited) { Write-Error "CAPTURE_FAIL: player exited during load - read Builds\Windows\...\Player.log"; exit 3 }
    if ($proc.MainWindowHandle -ne 0) { $hwnd = $proc.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) {
    Write-Error "CAPTURE_FAIL: no window handle after 24s. Raise -LoadWaitSec."
    $proc | Stop-Process -Force
    exit 3
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
    Write-Error ("CAPTURE_FAIL: could not hold the foreground after 5 attempts. Driven capture " +
                 "needs focus - without it SendInput never reaches the game, the hero does not " +
                 "move, and the game pauses, so every frame would be a Pause overlay. NOTHING " +
                 "was captured. Close/minimise other windows and re-run.")
    exit 4
}
Write-Output "[capture] foreground held - input will reach the game"

# --- the beats ---------------------------------------------------------------
# Named so a diff against a prior run is meaningful. Keep these names stable.
Write-Output "[capture] driving"
Save-Shot $hwnd '01_idle'          | Out-Null
Hold-Key 'W' 1.2 $hwnd; Save-Shot $hwnd '02_forward'     | Out-Null
Hold-Key 'W' 1.2 $hwnd; Save-Shot $hwnd '03_forward_far' | Out-Null
Hold-Key 'A' 1.0 $hwnd; Save-Shot $hwnd '04_left'        | Out-Null
Hold-Key 'D' 1.8 $hwnd; Save-Shot $hwnd '05_right'       | Out-Null
Hold-Key 'S' 1.2 $hwnd; Save-Shot $hwnd '06_back'        | Out-Null
Start-Sleep -Seconds 1
Save-Shot $hwnd '07_settled'       | Out-Null

# ?? WALK OUT AND LOOK BACK (owner method, 2026-08-14) ?????????????????????????
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

# --- harvest the matching trace ----------------------------------------------
$logSrc = Join-Path $env:USERPROFILE 'AppData\LocalLow\DeNelle\Echoes of Elarion\Player.log'
if (Test-Path $logSrc) {
    Copy-Item $logSrc (Join-Path $outDir 'Player.log') -Force
    Write-Output "  trace copied alongside the shots (same run)"
} else {
    Write-Warning "Player.log not found at $logSrc - shots have NO matching trace, which is half the evidence"
}

Start-Sleep -Seconds 1
$proc | Stop-Process -Force -ErrorAction SilentlyContinue

$n = (Get-ChildItem $outDir -Filter *.png | Measure-Object).Count
Write-Output "HEADED_CAPTURE_OK $n shots -> $outDir"
Write-Output "Now OPEN them. A green marker proves a frame rendered, never that it looks right."
