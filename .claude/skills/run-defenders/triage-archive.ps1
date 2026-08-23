# triage-archive.ps1 - after a run: archive raw logs (zip), append distinct signatures
# to the recurring-issues ledger, then CLEAR the live logs so the next run is clean.
# Keeps signal (recurrence), drops noise (raw logs of already-fixed stuff).
# Usage: powershell -NoProfile -File .claude/skills/run-defenders/triage-archive.ps1 [-Label "free-text"]
#
# WO-1018 (2026-08-22): this script is now ALSO the F8 inbox's prune step. logs/f8-inbox/ had never
# had one -- 2914 capture files had accumulated and f8-check-inbox.ps1 timed out walking them. ACKED
# captures older than -InboxDays are MOVED to logs/f8-inbox/archive/; nothing is ever deleted (this
# repo's rule is "never wipe a ticket"), un-acked captures and sweep orphans are left where they are,
# and the archive stays indexed so an archived capture is still resolvable by seq.
#   -InboxOnly  run ONLY the inbox prune - does not zip or clear any live log.
param(
    [string]$Label = "",
    [int]$InboxDays = 14,
    [switch]$InboxOnly,
    [switch]$SkipInbox
)

$ErrorActionPreference = "Stop"
$repo    = (Resolve-Path "$PSScriptRoot\..\..\..").Path

# -- 0) F8 INBOX PRUNE (WO-1018) -------------------------------------------------
if (-not $SkipInbox) {
    # the lib is defensive by design (try/catch + Set-StrictMode -Off); this script's global
    # ErrorActionPreference=Stop would turn its benign misses into a hard exit, so scope it down.
    $savedEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        . (Join-Path $PSScriptRoot 'f8-inbox-lib.ps1')
        $inbox = Join-Path $repo 'logs\f8-inbox'
        if (Test-Path $inbox) {
            [void](Invoke-F8InboxArchive -Inbox $inbox -Days $InboxDays)
        } else {
            Write-Host "F8_ARCHIVE_FAIL no inbox at $inbox"
        }
    } catch {
        Write-Host ("F8_ARCHIVE_FAIL inbox prune threw: {0}" -f $_.Exception.Message)
    }
    $ErrorActionPreference = $savedEap
}
if ($InboxOnly) { Write-Host "[triage-archive] -InboxOnly: live logs untouched"; exit 0 }
# LocalLow\<companyName>\<productName>; productName became "Echoes of Elarion" 2026-08-08.
$ll      = "$env:USERPROFILE\AppData\LocalLow\DeNelle\Echoes of Elarion"
$llLegacy = "$env:USERPROFILE\AppData\LocalLow\DeNelle\Defenders of the Realm"
if ((-not (Test-Path $ll)) -and (Test-Path $llLegacy)) { $ll = $llLegacy }
$archive = "$repo\logs\archive"
$ledger  = "$repo\logs\RECURRING_ISSUES.md"
$stamp   = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHHmmssZ")
New-Item -ItemType Directory -Force -Path $archive | Out-Null

$sources = @(
  "$ll\Player.log",
  "$ll\break-log.jsonl",
  "$repo\Builds\autopilot-tickets.md",
  "$repo\Builds\autopilot-tickets.json"
) | Where-Object { Test-Path $_ }

if ($sources.Count -eq 0) { Write-Host "[triage-archive] no run logs found - nothing to do"; exit 0 }

# 1) ARCHIVE (zip, never delete) ------------------------------------------------
$zip = "$archive\${stamp}_run.zip"
Compress-Archive -Path $sources -DestinationPath $zip -Force
Write-Host "[triage-archive] archived $($sources.Count) file(s) -> $zip"

# 2) LEDGER - append this run's DISTINCT error/Fail signatures (normalized) ------
$pl = "$ll\Player.log"
$sigs = @{}
if (Test-Path $pl) {
  Select-String -Path $pl -Pattern '\[Flow:[A-Za-z]+\][^"]*(FAILED|Fail|SEAM-UNREACHABLE|inside wall|NO panel|InvalidKey|magenta/error\b)' -AllMatches |
    ForEach-Object { $_.Line } |
    ForEach-Object {
      # normalize volatile bits so the same defect collapses to one signature
      ($_ -replace '\(?-?\d+(\.\d+)?,?\s*','' -replace "'[^']*'","'X'" -replace '\d+','N').Trim()
    } | ForEach-Object { if ($_){ $sigs[$_] = ($sigs[$_] + 1) } }
}
$bl = "$ll\break-log.jsonl"
if (Test-Path $bl) {
  Get-Content $bl | Where-Object { $_ -match '"kind":"(error|exception|possible_softlock)"' } |
    ForEach-Object { ($_ -replace '\d+','N').Trim() } | ForEach-Object { if($_){ $sigs[$_] = ($sigs[$_]+1) } }
}

if (-not (Test-Path $ledger)) {
@"
# RECURRING ISSUES LEDGER

Cross-run signal, deduped. Raw logs live zipped in `logs/archive/`. Curate **Status**
(open / fixed / false-alarm) by hand - a `fixed` signature that REAPPEARS is the real alert.

| First seen | Last seen | Runs | Status | Signature |
|---|---|---|---|---|
"@ | Set-Content -Encoding UTF8 $ledger
}

$ledgerText = Get-Content -Raw $ledger
$added = 0
foreach ($sig in $sigs.Keys) {
  $safe = ($sig -replace '\|','/')
  if ($ledgerText -notlike "*$safe*") {
    Add-Content -Encoding UTF8 $ledger ("| $stamp | $stamp | $($sigs[$sig]) | open | $safe |")
    $added++
  }
}
Write-Host "[triage-archive] ledger: $added new signature(s) appended (reappearing ones flagged by hand)"

# 3) CLEAR live logs so the next run starts from a clean baseline ----------------
foreach ($f in @("$ll\Player.log","$ll\break-log.jsonl")) {
  if (Test-Path $f) { Clear-Content -Force $f; Write-Host "[triage-archive] cleared $f" }
}
Write-Host "[triage-archive] DONE - folder clean; recurrence tracked in $ledger"
