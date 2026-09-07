# archive_canon_anchors.ps1
# -----------------------------------------------------------------------------
# Moves every STALE root CANON_GROUND_TRUTH_*.md into docs/_archive/ via `git mv`,
# keeping exactly two at root:
#
#   CANON_GROUND_TRUTH_2026-09-06.md  - the single LIVE anchor (CLAUDE.md section 15:
#                                       keep exactly ONE current, supersede by date)
#   CANON_GROUND_TRUTH_2026-07-22.md  - the DEEP MODULE anchor, still cited by
#                                       PROJECT_INDEX.md as the module-level reference
#                                       the dated anchors delta against
#
# Why a script and not a one-liner: the keep-list is the whole point. A hand-typed
# `git mv CANON_*` sweeps the live anchor into the archive and leaves the repo with
# no current ground truth. The two keepers are named here once, in code, so no seat
# retypes them.
#
# PowerShell 5.1. ASCII only. No `&&` (not a 5.1 operator).
#
#   powershell -NoProfile -File tools\archive_canon_anchors.ps1            # MOVES the files
#   powershell -NoProfile -File tools\archive_canon_anchors.ps1 -DryRun    # print only
#
# A BARE RUN MOVES FILES. That is deliberate: a script whose default is a no-op
# that still exits 0 is the `gates-report-success-without-proving-it` shape - the
# caller believes it ran and nothing happened. If you only want the list, ask for
# it with -DryRun. Either way the list is printed BEFORE anything moves.
# -----------------------------------------------------------------------------

[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# Repo root is MACHINE-DEPENDENT (CLAUDE.md section 0: C:\eoa on one seat, D:\eoa on
# another). Resolve it at runtime; never hardcode it.
$root = (git rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0) {
    Write-Host 'FAIL: not inside a git work tree.'
    exit 1
}
$root = $root.Trim()
Set-Location $root

$keep = @(
    'CANON_GROUND_TRUTH_2026-09-06.md',
    'CANON_GROUND_TRUTH_2026-07-22.md'
)

$dest = Join-Path $root 'docs\_archive'
if (-not (Test-Path $dest)) {
    Write-Host "FAIL: destination does not exist: $dest"
    exit 1
}

# Only git-TRACKED files can be `git mv`d. An untracked anchor (the live one usually
# is, on the night it is written) would fail the move and abort the run.
$tracked = @(git ls-files -- 'CANON_GROUND_TRUTH_*.md')
if ($LASTEXITCODE -ne 0) {
    Write-Host 'FAIL: git ls-files failed.'
    exit 1
}

$stale = @($tracked | Where-Object { $keep -notcontains (Split-Path $_ -Leaf) } | Sort-Object)

Write-Host ''
Write-Host '=== KEEPING AT ROOT ==='
foreach ($k in $keep) {
    if (Test-Path (Join-Path $root $k)) {
        Write-Host ("  KEEP   {0}" -f $k)
    }
    else {
        Write-Host ("  WARN   {0} - named as a keeper but NOT on disk" -f $k)
    }
}

Write-Host ''
Write-Host '=== TO ARCHIVE (docs/_archive/) ==='
foreach ($f in $stale) {
    Write-Host ("  MOVE   {0}" -f $f)
}
Write-Host ''
Write-Host ("  count: {0}" -f $stale.Count)
Write-Host ''

if ($stale.Count -eq 0) {
    Write-Host 'Nothing to do.'
    exit 0
}

if ($DryRun) {
    Write-Host 'DRY RUN (-DryRun) - nothing moved. Re-run without -DryRun to perform the git mv.'
    exit 0
}

foreach ($f in $stale) {
    $leaf = Split-Path $f -Leaf
    git mv -- $f ('docs/_archive/' + $leaf)
    if ($LASTEXITCODE -ne 0) {
        Write-Host ("FAIL: git mv failed on {0} - stopping, tree is partially moved." -f $f)
        exit 1
    }
    Write-Host ("  moved  {0}" -f $leaf)
}

Write-Host ''
Write-Host ("ARCHIVE_OK {0} moved to docs/_archive/" -f $stale.Count)
Write-Host 'Staged by git mv - review with `git status` before committing.'
exit 0
