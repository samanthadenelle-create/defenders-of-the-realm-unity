<#
    build-ui-review.ps1  --  UI Review Drop Assembler
    ------------------------------------------------------------------
    Idempotent generator that assembles the owner's UI review drop:
    reads a mapping file + two image sources (Blink templates + runtime
    shots) and produces one folder per screen plus a single scrollable
    contact sheet (INDEX.html). Nobody hand-copies PNGs again.

    Reusable pattern across projects:
        1. drop a UI_REVIEW/_mapping.json
        2. run  powershell -ExecutionPolicy Bypass -File build-ui-review.ps1
        3. open UI_REVIEW/INDEX.html to fast-compare
        4. mark each screen's FEEDBACK.md (PASS/FIX + Notes)

    Windows PowerShell 5.1 compatible. Standalone tooling script only:
    touches nothing but the UI_REVIEW folder. No Unity, no git.
#>

[CmdletBinding()]
param(
    # Repo root (defaults to the folder this script lives in)
    [string]$RepoRoot = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

# --- Robust root/reviewDir resolution (never assume cwd) --------------
# The script lives at <repoRoot>\build-ui-review.ps1, so $PSScriptRoot IS
# the repo root. Prefer it over any passed/derived value or the cwd.
if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $RepoRoot = $PSScriptRoot
} elseif ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Get-Location).Path
}
$RepoRoot = (Resolve-Path $RepoRoot).Path

# Normalize: if the resolved root already points AT the UI_REVIEW folder
# (e.g. someone passed the review dir), step up so we never double-nest
# into C:\EoA\UI_REVIEW\UI_REVIEW.
if ((Split-Path $RepoRoot -Leaf) -ieq 'UI_REVIEW') {
    $RepoRoot = (Split-Path $RepoRoot -Parent)
}

$ReviewDir   = Join-Path $RepoRoot 'UI_REVIEW'
$MappingPath = Join-Path $ReviewDir '_mapping.json'

Write-Host ""
Write-Host "=== UI Review Drop Assembler ===" -ForegroundColor Cyan
Write-Host "Repo root : $RepoRoot"
Write-Host "Review dir: $ReviewDir"

# --- 1. Mapping gate -------------------------------------------------
if (-not (Test-Path $MappingPath)) {
    Write-Host ""
    Write-Host "No _mapping.json found at:" -ForegroundColor Yellow
    Write-Host "  $MappingPath"
    Write-Host "Another step produces it (screen -> panelId/frame/template/shot)."
    Write-Host "Nothing to assemble yet. Exiting cleanly."
    exit 0
}

try {
    # NOTE: assign the parse to a variable FIRST. In Windows PowerShell 5.1,
    # `@(... | ConvertFrom-Json)` hands the whole JSON array to @() as a single
    # object (it doesn't enumerate), collapsing N rows into 1. Assigning to a
    # var yields a real Object[], which @() then enumerates correctly.
    $parsed = Get-Content $MappingPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $rows = @($parsed)
} catch {
    Write-Host ""
    Write-Host "ERROR: _mapping.json exists but could not be parsed as JSON:" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)"
    exit 1
}

if ($null -eq $rows -or $rows.Count -eq 0) {
    Write-Host ""
    Write-Host "_mapping.json parsed but contains zero rows. Nothing to do." -ForegroundColor Yellow
    exit 0
}

# --- 2. Resolve runtime-shot source (LocalLow) -----------------------
# "$env:LOCALAPPDATA\..\LocalLow" -> AppData\LocalLow  (robust resolve)
$localLowRoot = Join-Path $env:LOCALAPPDATA '..\LocalLow'
try { $localLowRoot = (Resolve-Path $localLowRoot).Path } catch { }
# productName became "Echoes of Elarion" 2026-08-08, which moves LocalLow\DeNelle\<productName>.
$shotsDir = Join-Path $localLowRoot 'DeNelle\Echoes of Elarion\ui-shots'
$shotsDirLegacy = Join-Path $localLowRoot 'DeNelle\Defenders of the Realm\ui-shots'
if ((-not (Test-Path $shotsDir)) -and (Test-Path $shotsDirLegacy)) { $shotsDir = $shotsDirLegacy }
Write-Host "Shots dir : $shotsDir"
if (-not (Test-Path $shotsDir)) {
    Write-Host "  (shots dir not present yet -- delivered shots will be marked pending)" -ForegroundColor Yellow
}

if (-not (Test-Path $ReviewDir)) {
    New-Item -ItemType Directory -Path $ReviewDir -Force | Out-Null
}

# --- 3. Per-screen assembly -----------------------------------------
$index = 0
$summary = @()
$cards = New-Object System.Collections.Generic.List[string]

# Minimal HTML encoder (no System.Web dependency)
function Enc([string]$s) {
    if ($null -eq $s) { return "" }
    return $s.Replace('&','&amp;').Replace('<','&lt;').Replace('>','&gt;').Replace('"','&quot;')
}

foreach ($row in $rows) {
    $index++
    $screen = "$($row.screen)"
    if ([string]::IsNullOrWhiteSpace($screen)) { $screen = "screen$index" }

    # Folder name: NN_screen  (zero-padded index preserves mapping order)
    $safeScreen = ($screen -replace '[\\/:*?"<>|]', '_')
    $folderName = ('{0:00}_{1}' -f $index, $safeScreen)
    $screenDir  = Join-Path $ReviewDir $folderName
    if (-not (Test-Path $screenDir)) {
        New-Item -ItemType Directory -Path $screenDir -Force | Out-Null
    }

    # -- template.png ---------------------------------------------------
    $templateRel = "$($row.templatePng)"
    $templateSrc = ''
    $templateOk  = $false
    if (-not [string]::IsNullOrWhiteSpace($templateRel)) {
        $templateSrc = Join-Path $RepoRoot $templateRel
        if (Test-Path $templateSrc) {
            Copy-Item $templateSrc (Join-Path $screenDir 'template.png') -Force
            $templateOk = $true
        }
    }
    if (-not $templateOk) {
        Set-Content -Path (Join-Path $screenDir 'template_MISSING.txt') `
            -Value "Template not found at repo-relative path: $templateRel" -Encoding utf8
    }

    # -- delivered.png --------------------------------------------------
    $deliveredName = "$($row.deliveredShot)"
    $deliveredSrc  = ''
    $deliveredOk   = $false
    if (-not [string]::IsNullOrWhiteSpace($deliveredName)) {
        $deliveredSrc = Join-Path $shotsDir $deliveredName
        if (Test-Path $deliveredSrc) {
            Copy-Item $deliveredSrc (Join-Path $screenDir 'delivered.png') -Force
            $deliveredOk = $true
        }
    }
    if ($deliveredOk) {
        # clean any stale placeholder
        $ph = Join-Path $screenDir 'placeholder.txt'
        if (Test-Path $ph) { Remove-Item $ph -Force }
    } else {
        Set-Content -Path (Join-Path $screenDir 'placeholder.txt') `
            -Value "no runtime shot captured yet -- pending capture run`r`n(expected shot file: $deliveredName)" -Encoding utf8
    }

    # -- FEEDBACK.md (create only when missing; preserve owner markup) ---
    $feedbackPath = Join-Path $screenDir 'FEEDBACK.md'
    if (-not (Test-Path $feedbackPath)) {
        $templateLabel = if ([string]::IsNullOrWhiteSpace($templateRel)) { '(none)' } else { Split-Path $templateRel -Leaf }
        $fb = @"
# $folderName -- review

screen   : $screen
panelId  : $($row.panelId)
frame    : $($row.frame)
template : $templateLabel
delivered: $deliveredName

## Verdict (mark one)
- [ ] PASS
- [ ] FIX

## Notes

"@
        Set-Content -Path $feedbackPath -Value $fb -Encoding utf8
    }

    # -- summary + card -------------------------------------------------
    $status = if ($deliveredOk) { 'complete' } else { 'delivered-missing' }
    $summary += [pscustomobject]@{
        Screen    = $folderName
        Template  = $templateOk
        Delivered = $deliveredOk
        Status    = $status
    }

    $tImg = if ($templateOk)  { "$folderName/template.png" }  else { '' }
    $dImg = if ($deliveredOk) { "$folderName/delivered.png" } else { '' }

    $tCell = if ($templateOk) {
        "<a href='$(Enc $tImg)' target='_blank'><img src='$(Enc $tImg)' alt='template'></a>"
    } else {
        "<div class='missing'>no template</div>"
    }
    $dCell = if ($deliveredOk) {
        "<a href='$(Enc $dImg)' target='_blank'><img src='$(Enc $dImg)' alt='delivered'></a>"
    } else {
        "<div class='missing'>no runtime shot yet<br><small>pending capture</small></div>"
    }

    $badgeClass = if ($deliveredOk) { 'ok' } else { 'pending' }
    $badgeText  = if ($deliveredOk) { 'PAIR COMPLETE' } else { 'AWAITING SHOT' }

    $card = @"
<section class='card'>
  <header>
    <span class='num'>$('{0:00}' -f $index)</span>
    <h2>$(Enc $screen)</h2>
    <span class='badge $badgeClass'>$badgeText</span>
    <span class='meta'>$(Enc "$($row.panelId)") &middot; $(Enc "$($row.frame)")</span>
  </header>
  <div class='pair'>
    <figure><figcaption>Template (Blink)</figcaption>$tCell</figure>
    <figure><figcaption>Delivered (runtime)</figcaption>$dCell</figure>
  </div>
  <div class='fb'><a href='$(Enc "$folderName/FEEDBACK.md")' target='_blank'>open FEEDBACK.md &rarr;</a></div>
</section>
"@
    $cards.Add($card)
}

# --- 4. INDEX.html ---------------------------------------------------
$complete = @($summary | Where-Object { $_.Status -eq 'complete' }).Count
$missing  = @($summary | Where-Object { $_.Status -eq 'delivered-missing' }).Count
$stamp    = Get-Date -Format 'yyyy-MM-dd HH:mm'

$html = @"
<!doctype html>
<html lang='en'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<title>UI Review -- Defenders of the Realm</title>
<style>
  :root { color-scheme: dark; }
  * { box-sizing: border-box; }
  body { margin:0; font-family: 'Segoe UI', system-ui, sans-serif; background:#14110c; color:#e8e0cf; }
  header.top { position:sticky; top:0; z-index:5; padding:18px 24px; background:#1c1810;
               border-bottom:2px solid #c9a24b; box-shadow:0 2px 12px rgba(0,0,0,.5); }
  header.top h1 { margin:0 0 4px; font-size:20px; color:#e8c877; letter-spacing:.5px; }
  header.top .sub { font-size:13px; color:#a99a78; }
  .stats { display:flex; gap:18px; margin-top:8px; font-size:13px; }
  .stats span { padding:3px 10px; border-radius:4px; background:#241f15; border:1px solid #3a3221; }
  .stats .good { color:#8fd48f; } .stats .warn { color:#e6b45a; }
  main { padding:24px; display:flex; flex-direction:column; gap:22px; max-width:1400px; margin:0 auto; }
  .card { background:#1c1810; border:1px solid #3a3221; border-radius:10px; overflow:hidden; }
  .card > header { display:flex; align-items:center; gap:12px; padding:12px 16px;
                   background:#241f15; border-bottom:1px solid #3a3221; }
  .card .num { font-variant-numeric:tabular-nums; color:#8a7c5c; font-size:13px; }
  .card h2 { margin:0; font-size:16px; color:#e8c877; flex:0 1 auto; }
  .card .meta { margin-left:auto; font-size:12px; color:#8a7c5c; }
  .badge { font-size:11px; font-weight:600; padding:3px 9px; border-radius:20px; letter-spacing:.4px; }
  .badge.ok { background:#1e3a1e; color:#8fd48f; border:1px solid #2f6b2f; }
  .badge.pending { background:#3a2f14; color:#e6b45a; border:1px solid #6b551f; }
  .pair { display:grid; grid-template-columns:1fr 1fr; gap:0; }
  figure { margin:0; padding:14px; text-align:center; border-right:1px solid #2a251a; }
  figure:last-child { border-right:none; }
  figcaption { font-size:12px; color:#a99a78; margin-bottom:8px; text-transform:uppercase; letter-spacing:.6px; }
  figure img { max-width:100%; height:auto; border:1px solid #3a3221; border-radius:6px; background:#0c0a07; }
  .missing { padding:48px 12px; color:#8a7c5c; border:1px dashed #4a4130; border-radius:6px; font-size:14px; }
  .fb { padding:10px 16px; border-top:1px solid #2a251a; font-size:13px; }
  .fb a, header.top a { color:#c9a24b; text-decoration:none; }
  .fb a:hover { text-decoration:underline; }
  @media (max-width:760px){ .pair { grid-template-columns:1fr; } figure{border-right:none;border-bottom:1px solid #2a251a;} }
</style>
</head>
<body>
<header class='top'>
  <h1>UI Review -- Defenders of the Realm</h1>
  <div class='sub'>Template (Blink target) vs. Delivered (runtime capture). Generated $stamp.</div>
  <div class='stats'>
    <span>$($summary.Count) screens</span>
    <span class='good'>$complete pairs complete</span>
    <span class='warn'>$missing awaiting shot</span>
  </div>
</header>
<main>
$([string]::Join("`n", $cards))
</main>
</body>
</html>
"@

$indexPath = Join-Path $ReviewDir 'INDEX.html'
Set-Content -Path $indexPath -Value $html -Encoding utf8

# --- 5. Summary ------------------------------------------------------
Write-Host ""
Write-Host "--- Summary ---" -ForegroundColor Cyan
$summary | Format-Table Screen, Template, Delivered, Status -AutoSize | Out-String | Write-Host
Write-Host ("Screens built : {0}" -f $summary.Count)
Write-Host ("Pairs complete: {0}" -f $complete) -ForegroundColor Green
Write-Host ("Awaiting shot : {0}" -f $missing) -ForegroundColor Yellow
Write-Host ""
Write-Host "Contact sheet : $indexPath" -ForegroundColor Cyan
Write-Host "Open it to fast-compare, then mark each FEEDBACK.md."
