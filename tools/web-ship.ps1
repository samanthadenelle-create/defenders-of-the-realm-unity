# =============================================================================
# web-ship.ps1 - THE ONE FILE that knows which Vercel surfaces serve this game,
# deploys the ones the release train does not already cover, and PROVES the
# public production domains all serve the same bytes.
#
# WO-1316. ASCII-only. Windows PowerShell 5.1 compatible (no &&, no ||, no
# ternary, no ??).
#
# ---------------------------------------------------------------------------
# WHY THIS FILE EXISTS
# ---------------------------------------------------------------------------
# FOUR Vercel projects serve this game. `vercel deploy --target production` from
# this repo updates `defenders-of-the-realm-v2` and NOTHING ELSE, because
# `.vercel/project.json` links only that one. It prints a success message and a
# ready deployment while `echoes-of-elarion.vercel.app` - the domain named after
# the game, the one this repo's own canon treats as production - keeps serving
# whatever it last got.
#
#   THE SUCCESS SIGNAL COMES FROM THE COPY YOU TOUCHED, NOT THE COPY USERS HIT.
#
# That is the same failure class as CLAUDE.md section 16 (content pushed for the
# wrong platform) and section 2's stale WO-number block: a second copy of state
# that nothing keeps in sync.
#
# Measured 2026-09-02 (WO-1316): after a successful --prod deploy,
# echoes-of-elarion served a 7,396-byte pre-gate index.html while
# defenders-of-the-realm-v2 served the new 26,443-byte one. Both were then
# patched BY HAND. Measured again 2026-09-03, ONE DAY LATER: v2 served 40,100
# bytes and echoes-of-elarion served 32,609 - DIVERGED AGAIN, with different
# loader/data/wasm content hashes, i.e. two different builds of the game live on
# two production domains. The hand patch decayed in a day. That is the whole
# argument for a gate.
#
# ---------------------------------------------------------------------------
# THE REGISTRY BELOW IS THE SINGLE SOURCE OF TRUTH FOR THE SURFACE LIST.
# ---------------------------------------------------------------------------
# Do NOT copy these hosts, ids or roles into a chain, a doc, a work order or a
# second script. CLAUDE.md section 16 records what happened when the R2
# push+verify pair was copy-pasted into two ship chains: the copies DRIFTED, and
# one degraded into printing "FIX: run this by hand". A gate whose remedy is
# "a human remembers a second command" is not a gate. Every caller calls THIS
# FILE. `-ListSurfaces` exists so a caller can read the registry instead of
# restating it.
#
# ---------------------------------------------------------------------------
# JUDGE BY THE MARKER ON A FRESH LOG, NEVER BY THE EXIT CODE
# ---------------------------------------------------------------------------
# This repo's runners exit 0 on refusals and FAILs (CLAUDE.md section 8, memory
# `gates-report-success-without-proving-it`). Markers, in Builds\web-parity.log:
#
#   WEB_SURFACES_OK      - the registry was listed (-ListSurfaces)
#   WEB_DEPLOY_OK        - one non-chain production surface was deployed
#   WEB_SHIP_PUSH_OK     - every non-chain production surface was deployed
#   WEB_PARITY_OK        - EVERY public production domain serves identical bytes
#
# Marker ABSENCE on a fresh log is a FAILURE, not an unknown.
#
# There is deliberately NO -Force and NO -WarnOnly. Every incident this ticket
# documents was a human expected to remember a second command or to read a
# warning; an override flag would restore that exact hole.
#
# ---------------------------------------------------------------------------
# USAGE
# ---------------------------------------------------------------------------
#   powershell -NoProfile -File tools\web-ship.ps1 -ListSurfaces
#   powershell -NoProfile -File tools\web-ship.ps1 -VerifyOnly
#   powershell -NoProfile -File tools\web-ship.ps1 -VerifyOnly -AgainstLocal Builds\WebGL
#   powershell -NoProfile -File tools\web-ship.ps1            # deploy + verify
#
# Exit codes: 0 = the requested marker was emitted. 16 = failure. 20 = refused
# before doing any work. They are diagnostics; the marker is the verdict.
# =============================================================================

[CmdletBinding()]
param(
    # Verify the live domains only. Touches nothing. This is the mode the
    # release train calls, because it cannot change production.
    [switch]$VerifyOnly,

    # Print the registry and exit. For callers that need the host list.
    [switch]$ListSurfaces,

    # Additionally assert every production domain serves byte-identical
    # index.html and validation-key.txt to this local build directory.
    # Only meaningful immediately after a deploy: a later local rebuild
    # re-hashes the Unity payload and the local copy legitimately moves on.
    [string]$AgainstLocal = '',

    # Which document to compare across domains. The Unity shell names its
    # content-hashed loader/data/wasm inside index.html, so index.html changing
    # is a faithful proxy for the whole payload changing.
    [string]$ParityPath = '/index.html',

    [int]$TimeoutSec = 60
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$builds = Join-Path $root 'Builds'
$log = Join-Path $builds 'web-parity.log'

# =============================================================================
# THE REGISTRY - hardcoded EXACTLY ONCE, here, and nowhere else in the repo.
#
# Role meanings, and what each project is FOR (WO-1316 acceptance criterion 2):
#
#   production  - a public domain real players and/or the Pi validator hit. MUST
#                 serve identical bytes to every other production surface. A
#                 divergence withholds WEB_PARITY_OK.
#   dormant     - a live URL that nothing deploys to. Kept only because deleting
#                 or pausing a Vercel project is the OWNER'S call. It is still
#                 gated: a dormant domain serving a DIFFERENT validation key
#                 than production is a hazard on its own (that is precisely how
#                 defenders-webgl sat on the retired July key for ~7 weeks with
#                 nobody noticing), so it withholds the marker too.
#   api         - a backend-only surface with no game shell. Reported, never
#                 compared: it has no index.html payload and no validation key.
#
# DeployedByChain - $true means tools\command-centre.ps1 already deploys this
#                 surface through its candidate/promote/rollback path, which is
#                 stronger than a plain deploy and must not be duplicated here.
#                 $false means THIS file is the only thing that will ever ship
#                 it, which is the hole WO-1316 exists to close.
# =============================================================================
$Surfaces = @(
    [pscustomobject]@{
        Name            = 'defenders-of-the-realm-v2'
        Url             = 'https://defenders-of-the-realm-v2.vercel.app'
        ProjectId       = 'prj_qUmuwr8BN492oZH8yRuvPZMN3e0J'
        Role            = 'production'
        DeployedByChain = $true
        Purpose         = 'The repo-linked project (.vercel/project.json). Ships Builds/WebGL + api/ via command-centre.ps1.'
    },
    [pscustomobject]@{
        Name            = 'echoes-of-elarion'
        Url             = 'https://echoes-of-elarion.vercel.app'
        ProjectId       = 'prj_rnbaJwN6CsuNGuRLtagf6oMFO3sY'
        Role            = 'production'
        DeployedByChain = $false
        Purpose         = 'The domain named after the game; canon and site/terms.html point at it. NOT linked in this repo, so only this file can ship it.'
    },
    [pscustomobject]@{
        Name            = 'defenders-webgl'
        Url             = 'https://defenders-webgl.vercel.app'
        ProjectId       = ''
        Role            = 'dormant'
        DeployedByChain = $false
        Purpose         = 'Pre-rename WebGL host. Nothing deploys to it. PROPOSED FOR RETIREMENT (owner decision) - it served the retired July validation key for ~7 weeks.'
    },
    [pscustomobject]@{
        Name            = 'defenders-backend'
        Url             = 'https://defenders-backend.vercel.app'
        ProjectId       = ''
        Role            = 'api'
        DeployedByChain = $false
        Purpose         = 'Standalone API stub, superseded by api/ riding the main deploy (.vercelignore:17 re-includes /api). No game shell.'
    }
)

$TeamScope = 'team_2PrmHqE5mM52aIrzPJNHmyEt'

# -----------------------------------------------------------------------------
# Logging. .NET WriteAllText/AppendAllText with a no-BOM encoding, because
# Set-Content -Encoding utf8 writes a BOM and Add-Content defaults to the ANSI
# codepage - both corrupt a file another tool parses.
# -----------------------------------------------------------------------------
$NoBom = New-Object System.Text.UTF8Encoding($false)

New-Item -ItemType Directory -Force -Path $builds | Out-Null
[System.IO.File]::WriteAllText($log, "WEB_SHIP_START $(Get-Date -Format o)`r`n", $NoBom)

function Write-Line {
    param([string]$Text)
    Write-Host $Text
    [System.IO.File]::AppendAllText($log, ($Text + "`r`n"), $NoBom)
}

function Deny {
    param([string]$Reason, [int]$Code = 16)
    Write-Line "WEB_SHIP_REFUSED reason=$Reason log=$log"
    exit $Code
}

function Get-Sha256Hex {
    param([byte[]]$Bytes)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

# Plain HTTPS GET of a PUBLIC production domain. Deliberately not `vercel curl`
# and deliberately not a preview URL: Vercel PREVIEW urls are SSO-gated (they
# 302 to sso-api and cannot be opened by a phone or by Pi's validator), so a
# check that fetches a preview and calls it proof is proving the wrong thing.
# Only the prod domain is what a user actually gets. No CLI is needed here.
function Get-Public {
    param([string]$Uri, [int]$Timeout)
    $result = [pscustomobject]@{ Ok = $false; Bytes = $null; Length = 0; Hash = ''; Error = '' }
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $client = New-Object System.Net.WebClient
        # No Accept-Encoding is sent, so the body is the raw bytes on the wire -
        # a transparently-gunzipped body would not hash to the served artifact.
        $client.Headers['Cache-Control'] = 'no-cache'
        $client.Headers['Pragma'] = 'no-cache'
        $bytes = $client.DownloadData($Uri)
        $result.Ok = $true
        $result.Bytes = $bytes
        $result.Length = $bytes.Length
        $result.Hash = Get-Sha256Hex $bytes
    } catch {
        $result.Error = $_.Exception.GetType().Name
        try {
            $response = $_.Exception.Response
            if ($null -ne $response) {
                $result.Error = 'HTTP_' + [int]$response.StatusCode
            }
        } catch {
            # Keep the exception type name. A missing status is still a failure.
        }
    } finally {
        if ($null -ne $client) { $client.Dispose() }
    }
    return $result
}

function Read-LocalBytes {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return [System.IO.File]::ReadAllBytes($Path)
}

# -----------------------------------------------------------------------------
# -ListSurfaces: the registry-reading path for other callers.
# -----------------------------------------------------------------------------
if ($ListSurfaces) {
    foreach ($s in $Surfaces) {
        Write-Line ("WEB_SURFACE name={0} role={1} chain={2} url={3}" -f $s.Name, $s.Role, $s.DeployedByChain, $s.Url)
        Write-Line ("  purpose={0}" -f $s.Purpose)
    }
    $prod = @($Surfaces | Where-Object { $_.Role -eq 'production' })
    Write-Line ("WEB_SURFACES_OK total={0} production={1}" -f $Surfaces.Count, $prod.Count)
    exit 0
}

$production = @($Surfaces | Where-Object { $_.Role -eq 'production' })
$dormant = @($Surfaces | Where-Object { $_.Role -eq 'dormant' })

if ($production.Count -lt 1) { Deny 'REGISTRY_HAS_NO_PRODUCTION_SURFACE' 20 }

# =============================================================================
# PHASE 1 - DEPLOY the production surfaces the release train does not cover.
#
# One call, in one file. Never "and also remember to do the other one".
# Skipped entirely under -VerifyOnly.
# =============================================================================
if (-not $VerifyOnly) {
    if ([string]::IsNullOrWhiteSpace($env:VERCEL_TOKEN)) {
        Deny 'VERCEL_TOKEN_MISSING_FROM_ENVIRONMENT' 20
    }

    $pending = @($production | Where-Object { -not $_.DeployedByChain })
    if ($pending.Count -eq 0) {
        Write-Line 'WEB_SHIP_PUSH_OK pending=0 reason=ALL_PRODUCTION_SURFACES_DEPLOYED_BY_CHAIN'
    } else {
        $deployed = New-Object System.Collections.Generic.List[string]
        foreach ($s in $pending) {
            if ([string]::IsNullOrWhiteSpace($s.ProjectId)) {
                Deny ("PROJECT_ID_MISSING_FOR_" + $s.Name) 20
            }
            Write-Line ("WEB_DEPLOY_INTENT name={0} id={1}" -f $s.Name, $s.ProjectId)

            # Target BY EXPLICIT PROJECT ID, never by whatever
            # .vercel/project.json happens to hold. VERCEL_PROJECT_ID +
            # VERCEL_ORG_ID override the link for exactly this reason.
            $priorProject = $env:VERCEL_PROJECT_ID
            $priorOrg = $env:VERCEL_ORG_ID
            $deployLog = Join-Path $builds ("vercel-deploy-" + $s.Name + ".log")
            try {
                $env:VERCEL_PROJECT_ID = $s.ProjectId
                $env:VERCEL_ORG_ID = $TeamScope
                $ErrorActionPreference = 'Continue'
                $out = & vercel deploy --target production --yes --no-color *>&1
                $ErrorActionPreference = 'Stop'
                $text = ($out | ForEach-Object { $_.ToString() }) -join "`r`n"
                [System.IO.File]::WriteAllText($deployLog, $text, $NoBom)
            } catch {
                $ErrorActionPreference = 'Stop'
                [System.IO.File]::WriteAllText($deployLog, $_.Exception.Message, $NoBom)
                Deny ("DEPLOY_THREW_" + $s.Name + "_" + $_.Exception.GetType().Name)
            } finally {
                $env:VERCEL_PROJECT_ID = $priorProject
                $env:VERCEL_ORG_ID = $priorOrg
            }

            # Judged on the log, not on the CLI's exit status.
            if ($text -notmatch 'https://[a-z0-9-]+\.vercel\.app') {
                Deny ("DEPLOY_URL_ABSENT_" + $s.Name)
            }
            Write-Line ("WEB_DEPLOY_OK name={0} log={1}" -f $s.Name, $deployLog)
            $deployed.Add($s.Name)
        }
        Write-Line ("WEB_SHIP_PUSH_OK deployed={0}" -f ($deployed -join ','))
    }
}

# =============================================================================
# PHASE 2 - VERIFY. Fetch every PUBLIC production domain and prove they all
# serve the same bytes. This is the step that makes a divergence LOUD.
# =============================================================================
$localIndexHash = ''
$localKeyHash = ''
if (-not [string]::IsNullOrWhiteSpace($AgainstLocal)) {
    $localDir = $AgainstLocal
    if (-not [System.IO.Path]::IsPathRooted($localDir)) { $localDir = Join-Path $root $localDir }
    $localIndex = Join-Path $localDir 'index.html'
    $localIndexBytes = Read-LocalBytes $localIndex
    if ($null -eq $localIndexBytes) { Deny "LOCAL_INDEX_MISSING_$localIndex" 20 }
    $localIndexHash = Get-Sha256Hex $localIndexBytes
    Write-Line ("WEB_LOCAL index={0} bytes={1} sha={2}" -f $localIndex, $localIndexBytes.Length, $localIndexHash)

    $localKey = Join-Path $localDir 'validation-key.txt'
    $localKeyBytes = Read-LocalBytes $localKey
    if ($null -ne $localKeyBytes) {
        $localKeyHash = Get-Sha256Hex $localKeyBytes
        Write-Line ("WEB_LOCAL key={0} bytes={1} sha={2}" -f $localKey, $localKeyBytes.Length, $localKeyHash)
    }
}

$indexHashes = @{}
$keyHashes = @{}

foreach ($s in $production) {
    $indexUri = $s.Url.TrimEnd('/') + $ParityPath
    $got = Get-Public $indexUri $TimeoutSec
    if (-not $got.Ok) {
        Write-Line ("WEB_FETCH_FAIL name={0} uri={1} error={2}" -f $s.Name, $indexUri, $got.Error)
        Deny ("PRODUCTION_FETCH_FAILED_" + $s.Name + "_" + $got.Error)
    }
    Write-Line ("WEB_SERVED name={0} path={1} bytes={2} sha={3}" -f $s.Name, $ParityPath, $got.Length, $got.Hash)
    $indexHashes[$s.Name] = $got.Hash

    $keyUri = $s.Url.TrimEnd('/') + '/validation-key.txt'
    $key = Get-Public $keyUri $TimeoutSec
    if ($key.Ok) {
        Write-Line ("WEB_SERVED name={0} path=/validation-key.txt bytes={1} sha={2}" -f $s.Name, $key.Length, $key.Hash)
        $keyHashes[$s.Name] = $key.Hash
    } else {
        Write-Line ("WEB_FETCH_FAIL name={0} uri={1} error={2}" -f $s.Name, $keyUri, $key.Error)
        Deny ("PRODUCTION_VALIDATION_KEY_UNREACHABLE_" + $s.Name + "_" + $key.Error)
    }
}

# --- Cross-domain agreement on the payload shell -----------------------------
$distinctIndex = @($indexHashes.Values | Sort-Object -Unique)
if ($distinctIndex.Count -ne 1) {
    foreach ($name in ($indexHashes.Keys | Sort-Object)) {
        Write-Line ("WEB_DIVERGENCE name={0} sha={1}" -f $name, $indexHashes[$name])
    }
    Deny 'PRODUCTION_INDEX_DIVERGENT'
}
$agreedIndex = $distinctIndex[0]

# --- Cross-domain agreement on the Pi validation key -------------------------
$distinctKey = @($keyHashes.Values | Sort-Object -Unique)
if ($distinctKey.Count -ne 1) {
    foreach ($name in ($keyHashes.Keys | Sort-Object)) {
        Write-Line ("WEB_DIVERGENCE name={0} keySha={1}" -f $name, $keyHashes[$name])
    }
    Deny 'PRODUCTION_VALIDATION_KEY_DIVERGENT'
}
$agreedKey = $distinctKey[0]

# --- Agreement with the local build, when asked ------------------------------
if (-not [string]::IsNullOrWhiteSpace($localIndexHash)) {
    if ($localIndexHash -ne $agreedIndex) {
        Write-Line ("WEB_DIVERGENCE local={0} served={1}" -f $localIndexHash, $agreedIndex)
        Deny 'LOCAL_INDEX_NOT_SERVED_BY_PRODUCTION'
    }
    Write-Line 'WEB_LOCAL_MATCH index=1'
}
if (-not [string]::IsNullOrWhiteSpace($localKeyHash)) {
    if ($localKeyHash -ne $agreedKey) {
        Write-Line ("WEB_DIVERGENCE localKey={0} servedKey={1}" -f $localKeyHash, $agreedKey)
        Deny 'LOCAL_VALIDATION_KEY_NOT_SERVED_BY_PRODUCTION'
    }
    Write-Line 'WEB_LOCAL_MATCH validationKey=1'
}

# --- Dormant surfaces: a live URL serving a DIFFERENT key is a hazard --------
# This is not a courtesy warning. defenders-webgl served the retired July key
# for ~7 weeks precisely because nothing deployed to it and nothing checked it.
# Two ways to clear this: bring the surface up to date, or the OWNER retires the
# project (delete/pause is her call, never a script's).
foreach ($s in $dormant) {
    $keyUri = $s.Url.TrimEnd('/') + '/validation-key.txt'
    $key = Get-Public $keyUri $TimeoutSec
    if (-not $key.Ok) {
        Write-Line ("WEB_DORMANT name={0} validationKey=ABSENT error={1} verdict=HARMLESS" -f $s.Name, $key.Error)
        continue
    }
    Write-Line ("WEB_DORMANT name={0} keyBytes={1} keySha={2}" -f $s.Name, $key.Length, $key.Hash)
    if ($key.Hash -ne $agreedKey) {
        Write-Line ("WEB_DIVERGENCE dormant={0} keySha={1} productionKeySha={2}" -f $s.Name, $key.Hash, $agreedKey)
        Deny ("DORMANT_SERVES_DIVERGENT_VALIDATION_KEY_" + $s.Name)
    }
}

Write-Line ("WEB_PARITY_OK surfaces={0} path={1} sha={2}" -f (($production | ForEach-Object { $_.Name }) -join ','), $ParityPath, $agreedIndex)
exit 0
