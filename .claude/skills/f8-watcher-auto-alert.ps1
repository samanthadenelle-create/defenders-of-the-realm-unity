# =============================================================================
# DEPRECATED - use .claude/skills/run-defenders/f8-watch-start.ps1 (persistent daemon + inbox).
# f8-watcher-auto-alert.ps1 - Auto-alert on F8 break-log captures
#
# Monitors the Editor.log / Player.log for F8 flags, errors, exceptions.
# When a capture fires, automatically extracts context and alerts Claude.
# No manual pinging needed - the CLI gets notified instantly.
# =============================================================================

param(
    [string]$LogPath = "$env:LOCALAPPDATA\Unity\Editor-5.x\Editor.log",
    [int]$PollIntervalMs = 2000,
    [int]$ContextLines = 50
)

$ErrorActionPreference = 'Stop'

# Baseline: capture the current log end so we only watch for NEW entries
$logExists = Test-Path $LogPath
if (-not $logExists) {
    Write-Host "[F8-Watcher] Log not found at $LogPath - waiting for first session..."
    Start-Sleep -Seconds 5
}

$lastPos = 0
if (Test-Path $LogPath) {
    $lastPos = (Get-Item $LogPath).Length
}

Write-Host "[F8-Watcher] === Starting F8 capture monitor ==="
Write-Host "[F8-Watcher] Log: $LogPath"
Write-Host "[F8-Watcher] Polling every ${PollIntervalMs}ms for: flagged / error / exception / softlock"
Write-Host ""

$sessionStartTime = Get-Date
$capturesSeen = @()
$lastSessionStartLine = ""

while ($true) {
    Start-Sleep -Milliseconds $PollIntervalMs

    if (-not (Test-Path $LogPath)) { continue }

    $currentSize = (Get-Item $LogPath).Length

    # Log rotated or truncated - reset position
    if ($currentSize -lt $lastPos) {
        Write-Host "[F8-Watcher] Log rotated/truncated (was $lastPos, now $currentSize); resetting."
        $lastPos = 0
    }

    # No new data
    if ($currentSize -eq $lastPos) { continue }

    # Read new tail
    $content = Get-Content $LogPath -Encoding UTF8 -Raw
    $newContent = $content.Substring($lastPos)
    $lastPos = $currentSize

    $lines = @($newContent -split "`r`n" | Where-Object { $_ })

    foreach ($line in $lines) {
        # Detect session start (baseline-reset point for this play session)
        if ($line -match "Initialize engine version|Starting with UI Toolkit") {
            $lastSessionStartLine = $line
            $capturesSeen = @()  # reset captures for new session
            continue
        }

        # F8 flag: "[BreakCapture] flagged"
        $isFlagged = $line -match "\[BreakCapture\].*flagged"

        # Error/Exception: "error CS|Exception|NullReferenceException|ArgumentException"
        $isError = $line -match "error CS\d+|Exception|NullReferenceException|ArgumentException|AssertionException"

        # Softlock: "Infinite loop|stack overflow|timeout|hang"
        $isSoftlock = $line -match "Infinite loop|stack overflow|timeout|hang|Deadlock"

        if ($isFlagged -or $isError -or $isSoftlock) {
            $captureKey = "$isFlagged|$isError|$isSoftlock|$line"

            # Dedupe: only alert once per unique capture
            if ($capturesSeen -contains $captureKey) { continue }
            $capturesSeen += $captureKey

            Write-Host ""
            Write-Host "+============================================================+"
            Write-Host "| !!  F8 CAPTURE DETECTED - AUTO-ALERT                      |"
            Write-Host "+============================================================+"
            Write-Host "[F8-Watcher] Capture type:"
            if ($isFlagged) { Write-Host "  * F8 flagged (manual break)" }
            if ($isError) { Write-Host "  * Compiler error or runtime exception" }
            if ($isSoftlock) { Write-Host "  * Potential softlock/hang" }
            Write-Host ""
            Write-Host "[F8-Watcher] Trigger line:"
            Write-Host "  $line"
            Write-Host ""

            # Extract context: last N lines before this capture
            $allLines = $content -split "`r`n"
            $triggerIdx = $allLines.IndexOf($line)
            $start = [Math]::Max(0, $triggerIdx - $ContextLines)
            $contextLines = $allLines[$start..($triggerIdx + 5)]

            Write-Host "[F8-Watcher] Context (last $ContextLines lines + capture):"
            $contextLines | ForEach-Object {
                if ($_ -match "\[Flow:|\[FeatureFlags\]|\[Guard\]") {
                    Write-Host "  > $_"
                }
            }
            Write-Host ""
            Write-Host "[F8-Watcher] * Alert sent at $(Get-Date -Format 'HH:mm:ss')"
            Write-Host "[F8-Watcher] Claude will triage this instantly. Waiting for next capture..."
            Write-Host ""
        }
    }
}
