# =============================================================================
# r2-ship.ps1 - THE one way content reaches players. Push, then PROVE it is hosted.
# -----------------------------------------------------------------------------
# Usage (from repo root or anywhere - it resolves the root itself):
#   powershell -File tools\r2-ship.ps1                 # push + verify ALL targets, BLOCKS on failure
#   powershell -File tools\r2-ship.ps1 -WarnOnly       # push + verify, warns instead of failing
#   powershell -File tools\r2-ship.ps1 -VerifyOnly     # prove only, upload nothing
#   powershell -File tools\r2-ship.ps1 -Prune          # + LIST stale generations (deletes NOTHING)
#   powershell -File tools\r2-ship.ps1 -Prune -Confirm # + actually delete them, after a green verify
#
# (-Target is retained so old callers keep binding, but since PROD-021 it narrows
#  NOTHING: the push is always the ServerData PARENT and the verify always covers
#  EVERY target ServerData holds. There is no way to verify less than everything.)
#
# Exit codes: 0 = R2_PARITY_OK (or -WarnOnly with a failure). 16 = parity failed
#             for ANY target (the marker is withheld unless ALL targets verify).
#
# STOP WHY THIS FILE EXISTS - the failure it is built to make impossible.
#
# Enemy and structure ART is served REMOTELY from R2. It is NOT in the APK. There is
# no local fallback: Assets/Resources/Enemies and Assets/Resources/Structures no
# longer exist. So an APK whose bundles were never uploaded installs perfectly,
# launches perfectly, and shows tinted capsules where the enemies should be and
# placeholders where the buildings should be - WITH NO ERROR ON SCREEN. The player
# just sees a broken world.
#
# !! BUNDLE NAMES ARE CONTENT-HASHED. Every content build produces new filenames, so
# EVERY build needs ITS OWN push. A push from a previous build can never cover this
# one. That single sentence is the whole trap: the bucket looks full, the previous
# build works, and the new one is broken.
#
# THIS HAS NOW HAPPENED FOUR TIMES:
#   2026-08-18  an APK sat ready to install whose enemy bundle had never been
#               uploaded. Caught by hand. Commit 16e22dba3 conceded in its own body:
#               "NO GATE COULD HAVE CAUGHT THIS."
#   2026-08-19  a real Android APK shipped carrying StandaloneWindows64 content,
#               every other marker green (WO-1124).
#   2026-08-20  the owner played a build where EVERY enemy was a capsule. The CLI
#               re-grouped and re-packed enemy content that day, which re-hashed
#               every bundle, and never pushed. Two wrong causes were proposed
#               (a duplicated [BuildTarget] token, then a stale content build) before
#               the DEVICE named it in one line:
#                 RemoteProviderException : Unable to load asset bundle from :
#                   https://pub-...r2.dev/Android/enemy_art_assets_enemyfam-hollow_...bundle
#                 UnityWebRequest result : ProtocolError : HTTP/1.1 404 Not Found
#               Owner ruling that day: "wire the r2 push into the ship chain."
#   2026-08-31  (PROD-021, occurrence FOUR) THIS SCRIPT was the defect: the push was
#               parent-wide but the verify covered ONE explicit target, so a run
#               that pushed ServerData and verified Android emitted the parity
#               marker while StandaloneWindows64/catalog_2026.08.31.349579.hash
#               404'd on R2 and the Windows player sat on the Title screen
#               ("an internet connection error", 93 F8 captures, seq 4081-4224).
#               The marker was TRUE and the build was still broken. Fix: enumerate
#               and verify EVERY target ServerData holds; withhold the marker
#               unless ALL of them verify.
#
# STOP AND THE REASON IT IS ONE FILE. Before this, the push+verify pair was COPY-PASTED
# into overnight-apk-build.ps1 and morning-ship-chain.ps1, and they had ALREADY
# drifted apart: overnight pushed then verified; morning ONLY VERIFIED and then told
# a human to go push by hand. A gate whose remedy is "a human remembers to run a
# second command" is not a gate - that is precisely the step that got skipped on
# 08-20. Same fact in three files is how this repo lost a WO number block and a
# dependency table; here it would keep costing whole play sessions.
#
# STOP PUSH THE PARENT, ALWAYS. '--push ServerData/Android' FLATTENS the keys to the
# bucket root, where the game never looks. It reports R2_PUSH_OK while uploading 103
# objects nobody can read - observed on 2026-08-20. The correct form is
# '--push ServerData'. Verify, however, needs an EXPLICIT target per call
# ('--verify-catalog ServerData/Android') because ServerData holds several platforms
# and the tool refuses to guess. Since PROD-021 this script ENUMERATES the actual
# subdirectories of ServerData/ (never a hardcoded platform list - a future target
# must be impossible to forget), skips any subdir holding no catalog files, and
# verifies EVERY enumerated target with its own explicit call. The aggregate parity
# marker is emitted only when ALL of them verify - one green platform can no longer
# vouch for a broken one. The two commands genuinely take different arguments; that
# asymmetry is why they are hard-coded here exactly once.
#
# STOP -Prune IS LOCAL ONLY, AND THAT IS WHY IT IS SAFE (WO-1486). ServerData/Android
# had never been pruned: 466 files, 597 MB, 168 catalog generations back to 2026-08-18,
# because bundle names are content-hashed so every build ADDS a set and none is ever
# retired. -Prune removes local generations the newest catalog does not name. It does
# NOT touch the bucket: cmd_push in tools/r2_sync.py walks the local tree and calls
# put_object per file - it never enumerates remote keys for deletion (the only
# delete_object in that file is the CORS probe cleanup, line 161). So an installed APK
# that requests an older baked catalog keeps resolving from R2 after a local prune. The
# cost of a prune is only the local rollback set, which is why deleting demands BOTH
# -Confirm and a green verify, and why the default is a dry run.
#
# STOP JUDGE BY THE MARKER, NEVER THE EXIT CODE. The runners in this repo exit 0 on
# refusals and FAILs (memory: gates-report-success-without-proving-it). Marker
# absence on a fresh log is a FAILURE, not an unknown.
# =============================================================================

[CmdletBinding()]
param(
    # RETAINED FOR CALLER COMPATIBILITY ONLY (PROD-021). This used to select the ONE
    # target the verify covered - which is exactly how a run pushed ServerData,
    # verified Android, and emitted the parity marker while the Windows catalog
    # 404'd. The verify now always covers every target ServerData holds; this
    # parameter narrows nothing and must never again be wired into the verify.
    [ValidateSet('Android', 'WebGL', 'Windows')]
    [string]$Target = 'Android',
    # Warn and continue instead of failing. For deliberately-offline or experimental
    # sideloads, where a mismatched bucket is a known and accepted state.
    [switch]$WarnOnly,
    # Prove only - upload nothing. Use when you know the push already happened and
    # you want the proof without the transfer.
    [switch]$VerifyOnly,
    # WO-1486. List every file under ServerData/<target>/ that the NEWEST catalog of
    # that target does not name. DRY RUN BY DEFAULT - -Prune alone deletes NOTHING; it
    # prints the plan and the R2_PRUNE_PLAN line. Deletion needs -Prune -Confirm AND a
    # green verify (see the STOP note below).
    [switch]$Prune,
    # Arms the deletion. Meaningless without -Prune.
    [switch]$Confirm
)

$ErrorActionPreference = 'Stop'

# Repo root = this script's parent's parent (tools\r2-ship.ps1 -> tools -> root).
$root    = Split-Path -Parent $PSScriptRoot
$sync    = Join-Path $root 'tools\r2_sync.py'
$logDir  = Join-Path $root 'Builds'
$pushLog = Join-Path $logDir 'r2-push.log'
$verLog  = Join-Path $logDir 'r2-parity.log'

if (-not (Test-Path $sync)) {
    Write-Host "R2_SHIP_FAIL: tools\r2_sync.py not found at $sync" -ForegroundColor Red
    exit 16
}
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }

# ---- 1. PUSH (the parent - see the header) -----------------------------------
if (-not $VerifyOnly) {
    & python $sync --ensure-cors
    Write-Host "[r2-ship] pushing ServerData (the PARENT - never ServerData/Android) ..." -ForegroundColor Cyan
    try {
        & python $sync --push ServerData *>&1 | Tee-Object -FilePath $pushLog
    } catch {
        # A throw here is not fatal on its own: the verify below is the authority on
        # whether the bucket actually holds what this build needs. Record and continue.
        "R2_PUSH_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $pushLog
        Write-Host "[r2-ship] push threw: $($_.Exception.Message) - continuing to verify, which is the authority." -ForegroundColor Yellow
    }
    if ((Test-Path $pushLog) -and (Select-String -Path $pushLog -Pattern 'R2_PUSH_OK' -Quiet)) {
        Write-Host "  $((Select-String -Path $pushLog -Pattern 'R2_PUSH_OK' | Select-Object -First 1).Line.Trim())"
    }
} else {
    Write-Host "[r2-ship] -VerifyOnly: uploading nothing." -ForegroundColor Yellow
}

# ---- 2. VERIFY (EVERY target, each with its own explicit call - PROD-021) ----
# The push above is parent-wide; a verify that covers less than the whole parent is
# the PROD-021 defect. Enumerate the ACTUAL subdirectories of ServerData/ - never a
# hardcoded platform list, so a future target cannot be forgotten - skip any subdir
# with no catalog files (never content-built, a player can request nothing from it),
# and verify each remaining one explicitly. The aggregate marker line is written
# ONLY when every target verifies; per-target passing lines are rewritten to
# R2_PARITY_TARGET_OK so the literal aggregate marker (which .githooks/pre-push and
# the ship chains grep for) can never appear in the log or on the console unless
# ALL targets passed.
Write-Host "[r2-ship] verifying every remote object, for EVERY target under ServerData ..." -ForegroundColor Cyan
if (Test-Path $verLog) { Remove-Item $verLog -Force }   # a STALE log must never read as a pass

$serverData = Join-Path $root 'ServerData'
if (-not (Test-Path $serverData)) {
    "R2_PARITY_FAIL no ServerData directory at $serverData - build Addressables content first" |
        Out-File -FilePath $verLog -Encoding Unicode
    Write-Host "  R2 CONTENT PARITY FAILED: $serverData does not exist - build Addressables content first." -ForegroundColor Red
    if ($WarnOnly) {
        Write-Host "  -WarnOnly was set: continuing anyway. Do not treat this build as shippable." -ForegroundColor Yellow
        exit 0
    }
    exit 16
}

$targets = @(Get-ChildItem -Path $serverData -Directory | Where-Object {
        @(Get-ChildItem -Path $_.FullName -Filter 'catalog_*' -File -ErrorAction SilentlyContinue).Count -gt 0
    } | Sort-Object Name)

if ($targets.Count -eq 0) {
    "R2_PARITY_FAIL no target under $serverData holds catalog files - nothing to verify is a FAILURE, not a pass" |
        Out-File -FilePath $verLog -Encoding Unicode
    Write-Host "  R2 CONTENT PARITY FAILED: no target under ServerData holds catalog files." -ForegroundColor Red
    if ($WarnOnly) {
        Write-Host "  -WarnOnly was set: continuing anyway. Do not treat this build as shippable." -ForegroundColor Yellow
        exit 0
    }
    exit 16
}

$passed = @()
$failed = @()
$totalObjects = 0
$allLines = New-Object System.Collections.Generic.List[string]

foreach ($t in $targets) {
    $name = $t.Name
    Write-Host "[r2-ship]   verifying ServerData/$name ..." -ForegroundColor Cyan
    $lines = New-Object System.Collections.Generic.List[string]
    try {
        & python $sync --verify-catalog "ServerData/$name" *>&1 | ForEach-Object {
            # Rewrite the tool's own per-target pass marker on the way in: the literal
            # aggregate marker may exist ONLY on the final all-targets summary line.
            $line = ("$_" -replace 'R2_PARITY_OK', 'R2_PARITY_TARGET_OK')
            $lines.Add($line)
            Write-Host "    $line"
        }
    } catch {
        $lines.Add("R2_PARITY_THREW target=$name $($_.Exception.Message)")
        Write-Host "    verify threw for target $name : $($_.Exception.Message)" -ForegroundColor Yellow
    }
    $allLines.Add("===== target: $name =====")
    foreach ($l in $lines) { $allLines.Add($l) }

    $okLine = $null
    foreach ($l in $lines) {
        if ($l -match 'R2_PARITY_TARGET_OK') { $okLine = $l; break }
    }
    if ($null -ne $okLine) {
        $passed += $name
        if ($okLine -match 'R2_PARITY_TARGET_OK\s+(\d+)') { $totalObjects += [int]$Matches[1] }
        Write-Host "  target $name : verified" -ForegroundColor Green
    } else {
        $failed += $name
        Write-Host "  target $name : PARITY FAILED" -ForegroundColor Red
    }
}

$ok = ($failed.Count -eq 0) -and ($passed.Count -gt 0)

# ---- 3. PRUNE (WO-1486) - dry run unless -Confirm, and never before a green verify --
# Computing the plan is a read-only walk, so it prints whenever -Prune is set: a plan
# you cannot see is a plan you cannot review. DELETING is gated on -Confirm AND $ok,
# because a prune that ran on a FAILED push would destroy the rollback set for content
# the bucket does not hold. The plan is folded into $allLines BEFORE the parity log is
# written, so the log's timestamp still postdates every byte under ServerData/ and
# .githooks/pre-push (which compares file mtimes) stays satisfiable.
if ($Prune) {
    # Latin1 (28591) - .NET Framework 4.x has no Encoding::Latin1 property. It maps every
    # byte 0-255 to one char, so a byte-for-byte substring search is exact for the ASCII
    # filenames we look for, with no decoding loss on binary catalog data.
    $latin1     = [System.Text.Encoding]::GetEncoding(28591)
    $pruneList  = New-Object System.Collections.Generic.List[System.IO.FileInfo]
    $pruneLines = New-Object System.Collections.Generic.List[string]

    foreach ($t in $targets) {
        $dir  = $t.FullName
        $name = $t.Name
        # Newest generation = the lexical maximum catalog stem. The stem is
        # catalog_<yyyy.MM.dd>.<6-digit build>, so lexical order IS build order (verified
        # 2026-09-06: every catalog under all three targets uses a 6-digit build number).
        $stems = @(Get-ChildItem -Path $dir -Filter 'catalog_*' -File |
                   ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) } |
                   Sort-Object -Unique)
        if ($stems.Count -eq 0) { continue }
        $newestStem = $stems[$stems.Count - 1]
        $newestBin  = Join-Path $dir "$newestStem.bin"
        if (-not (Test-Path $newestBin)) {
            $pruneLines.Add("R2_PRUNE_SKIP target=$name - newest catalog stem $newestStem has no .bin; refusing to guess a keep set")
            continue
        }

        $catalogText = $latin1.GetString([System.IO.File]::ReadAllBytes($newestBin))
        $keptCount   = 0
        $targetFiles = @(Get-ChildItem -Path $dir -File)
        foreach ($f in $targetFiles) {
            $keep = $false
            # Keep the newest generation's own catalog files (.bin/.hash/.json share the stem).
            if ([System.IO.Path]::GetFileNameWithoutExtension($f.Name) -eq $newestStem) {
                $keep = $true
            }
            # Keep anything the newest catalog names. Ordinal IndexOf, never -match: a
            # bundle filename is not a regex and '.' would match anything.
            elseif ($catalogText.IndexOf($f.Name, [System.StringComparison]::Ordinal) -ge 0) {
                $keep = $true
            }
            if ($keep) { $keptCount++ } else { $pruneList.Add($f) }
        }
        $staleHere = $targetFiles.Count - $keptCount
        $line = "R2_PRUNE_TARGET $name newest=$newestStem keep=$keptCount stale=$staleHere of $($targetFiles.Count) file(s)"
        $pruneLines.Add($line)
        Write-Host "  $line" -ForegroundColor Cyan
    }

    $pruneBytes = 0
    foreach ($f in $pruneList) { $pruneBytes += $f.Length }
    $pruneMB    = [math]::Round($pruneBytes / 1MB, 1)
    $planLine   = "R2_PRUNE_PLAN $($pruneList.Count) file(s) $pruneMB MB"
    $pruneLines.Add($planLine)

    if ($Prune -and $Confirm -and $ok) {
        $removed = 0
        $failedDel = 0
        foreach ($f in $pruneList) {
            try {
                Remove-Item -LiteralPath $f.FullName -Force
                $removed++
            } catch {
                $failedDel++
                $pruneLines.Add("R2_PRUNE_ERROR $($f.FullName) - $($_.Exception.Message)")
            }
        }
        $doneLine = "R2_PRUNE_DONE removed=$removed failed=$failedDel freed=$pruneMB MB"
        $pruneLines.Add($doneLine)
        Write-Host "  $doneLine" -ForegroundColor Green
    } elseif ($Confirm -and -not $ok) {
        $pruneLines.Add("R2_PRUNE_SKIPPED verify failed - the rollback set is the only copy of content the bucket may not hold")
        Write-Host "  R2_PRUNE_SKIPPED verify failed - nothing deleted." -ForegroundColor Yellow
    } else {
        $pruneLines.Add("R2_PRUNE_DRYRUN nothing deleted - rerun with -Prune -Confirm after a green verify")
        Write-Host "  R2_PRUNE_DRYRUN nothing deleted - rerun with -Prune -Confirm after a green verify." -ForegroundColor Yellow
    }

    Write-Host "  $planLine" -ForegroundColor Green
    foreach ($l in $pruneLines) { $allLines.Add($l) }
}

if ($ok) {
    # The one and only place the literal aggregate marker is ever written. It names
    # every verified target so a log reader can SEE the coverage, not assume it.
    $marker = "R2_PARITY_OK targets=$($passed -join ',') objects=$totalObjects"
    $allLines.Add($marker)
    $allLines | Out-File -FilePath $verLog -Encoding Unicode   # UTF-16LE, what the pre-push hook parses
    Write-Host "  $marker" -ForegroundColor Green
    exit 0
}

$failList = ($failed -join ',')
$allLines.Add("R2_PARITY_FAIL targets=$failList passed=$($passed -join ',') - aggregate marker withheld")
$allLines | Out-File -FilePath $verLog -Encoding Unicode   # UTF-16LE, what the pre-push hook parses

Write-Host ""
Write-Host "  R2 CONTENT PARITY FAILED for target(s): $failList" -ForegroundColor Red
Write-Host "  The aggregate parity marker is withheld - it is emitted only when EVERY target verifies."
Write-Host "  This build references remote bundles the bucket does not hold."
Write-Host "  On device that is tinted-capsule enemies and placeholder buildings," -ForegroundColor Red
Write-Host "  with NO error shown to the player." -ForegroundColor Red
Write-Host "  Bundle names are content-hashed, so a push from an earlier build cannot cover this one."
Write-Host "  See $verLog"
Write-Host ""

if ($WarnOnly) {
    Write-Host "  -WarnOnly was set: continuing anyway. Do not treat this build as shippable." -ForegroundColor Yellow
    exit 0
}
exit 16
