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
# A DEPLOY IS NOT A SHIP UNTIL THE PUBLIC DOMAIN POINTS AT IT (WO-1577)
# ---------------------------------------------------------------------------
# `vercel deploy --target production` creates a deployment and aliases ONLY the
# project's own auto-generated production alias. It does NOT move a bare custom
# domain. Proven from Builds\vercel-deploy-echoes-of-elarion.log (2026-09-07):
#
#   :6   Production      https://echoes-of-elarion-hxj4s4bey-samanthadenelle-
#                        creates-projects.vercel.app
#   :91  Aliased         https://echoes-of-elarion-samanthadenelle-creates-
#                        projects.vercel.app
#
# `echoes-of-elarion.vercel.app` - the domain canon, publishing/config.yaml and
# the store listing all point at - appears NOWHERE in that log. It kept serving
# a 33-day-old deployment while this file printed WEB_DEPLOY_OK. On 2026-09-07
# the domain had to be moved BY HAND with
# `vercel alias set <deployment> echoes-of-elarion.vercel.app` (and the same for
# the dormant defenders-webgl domain) before WEB_PARITY_OK could ever be earned.
#
#   THE SUCCESS SIGNAL CAME FROM THE DEPLOYMENT, NOT FROM THE DOMAIN USERS HIT.
#
# That is the SAME failure this file was written to end, one layer down, and
# "a human remembers a second command" is not a gate (CLAUDE.md section 16).
# So the alias set + the alias PROOF now live here, in the one file, between the
# deploy and WEB_DEPLOY_OK - WEB_DEPLOY_OK cannot be reached until the domain
# has been proven to resolve to the deployment just made.
#
# ---------------------------------------------------------------------------
# JUDGE BY THE MARKER ON A FRESH LOG, NEVER BY THE EXIT CODE
# ---------------------------------------------------------------------------
# This repo's runners exit 0 on refusals and FAILs (CLAUDE.md section 8, memory
# `gates-report-success-without-proving-it`). `vercel alias set` is no different
# and is judged the same way: on what `vercel alias ls` reports afterwards,
# never on the CLI's exit status. Markers, in Builds\web-parity.log:
#
#   WEB_SURFACES_OK      - the registry was listed (-ListSurfaces)
#   WEB_ALIAS_OK         - one public domain PROVEN to resolve to the new deployment
#   WEB_ALIAS_FAIL       - an alias did NOT resolve to it; refuses, exit 16
#   WEB_DEPLOY_OK        - one non-chain production surface was deployed AND aliased
#   WEB_SHIP_PUSH_OK     - every non-chain production surface was deployed AND aliased
#   WEB_PARITY_OK        - EVERY public production domain serves identical bytes
#   WEB_DRYRUN_OK        - -DryRun printed the plan and changed nothing
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
#   powershell -NoProfile -File tools\web-ship.ps1 -DryRun    # prints, ships nothing
#   powershell -NoProfile -File tools\web-ship.ps1 -VerifyOnly
#   powershell -NoProfile -File tools\web-ship.ps1 -VerifyOnly -AgainstLocal Builds\WebGL
#   powershell -NoProfile -File tools\web-ship.ps1            # deploy + alias + verify
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

    # Print every command this run WOULD execute - the staging copies, the
    # deploy, the `vercel alias set` calls and the `vercel alias ls` proof - and
    # change NOTHING. Exists so the alias path can be reviewed without shipping;
    # it is a preview, never an override (there is still no -Force, no
    # -WarnOnly, and -DryRun cannot emit WEB_PARITY_OK).
    [switch]$DryRun,

    # Stage the store-compliance pages into Builds\WebGL and exit. For the
    # release chain, which deploys Builds\WebGL itself and only needs the
    # two legal pages present in the payload before it ships.
    [switch]$StageOnly,

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
# A dry run writes to its OWN log. web-parity.log is the artifact other gates
# judge freshness against, and a preview that changes nothing must not be able
# to overwrite the proof of a run that changed something.
if ($DryRun) {
    $log = Join-Path $builds 'web-parity-dryrun.log'
} else {
    $log = Join-Path $builds 'web-parity.log'
}

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
#
# Aliases       - the PUBLIC domains that must be re-pointed at the deployment
#                 THIS file just created for that surface, in the order listed.
#                 `vercel deploy` does not move them (see the WO-1577 block in
#                 the header), so an empty list means "nothing to re-point",
#                 never "it happens automatically". Every listed domain is
#                 MANDATORY: one that cannot be proven to resolve to the new
#                 deployment withholds WEB_DEPLOY_OK. A surface with
#                 DeployedByChain = $true has no deployment of its own here, so
#                 its aliases belong to the chain that made the deployment, and
#                 the list is empty by construction.
# =============================================================================
$Surfaces = @(
    [pscustomobject]@{
        Name            = 'defenders-of-the-realm-v2'
        Url             = 'https://defenders-of-the-realm-v2.vercel.app'
        ProjectId       = 'prj_qUmuwr8BN492oZH8yRuvPZMN3e0J'
        Role            = 'production'
        DeployedByChain = $true
        Aliases         = @()
        Purpose         = 'The repo-linked project (.vercel/project.json). Ships Builds/WebGL + api/ via command-centre.ps1.'
    },
    [pscustomobject]@{
        Name            = 'echoes-of-elarion'
        Url             = 'https://echoes-of-elarion.vercel.app'
        ProjectId       = 'prj_rnbaJwN6CsuNGuRLtagf6oMFO3sY'
        Role            = 'production'
        DeployedByChain = $false
        # The two domains moved BY HAND on 2026-09-07, in this order, to make
        # WEB_PARITY_OK reachable. Order matters only for readability; both are
        # mandatory.
        #  - echoes-of-elarion.vercel.app is the store/canon domain. It is the
        #    one the deploy log NEVER mentions, and the whole reason for WO-1577.
        #  - defenders-webgl.vercel.app is the dormant pre-rename host. It is
        #    listed HERE, against the deployment that actually exists, because
        #    Phase 2 already REFUSES when a dormant domain serves a validation
        #    key that differs from production (it sat on the retired July key for
        #    ~7 weeks). Pointing it at the same deployment is the only way to
        #    satisfy that check short of the OWNER retiring the project, which is
        #    her call and never a script's.
        #    UNPROVEN FROM THIS SEAT: whether that domain is owned by the
        #    `echoes-of-elarion` project or by a separate `defenders-webgl`
        #    project. If Vercel refuses to move it across projects, this run
        #    emits WEB_ALIAS_FAIL naming it - which is the correct, loud outcome
        #    and the cheapest possible proof. Do not "fix" that by skipping it.
        Aliases         = @('echoes-of-elarion.vercel.app', 'defenders-webgl.vercel.app')
        Purpose         = 'The domain named after the game; canon and site/terms.html point at it. NOT linked in this repo, so only this file can ship it.'
    },
    [pscustomobject]@{
        Name            = 'defenders-webgl'
        Url             = 'https://defenders-webgl.vercel.app'
        ProjectId       = ''
        Role            = 'dormant'
        DeployedByChain = $false
        # Nothing deploys to this project, so it has no deployment of its own to
        # alias. Its DOMAIN is re-pointed at the echoes-of-elarion deployment -
        # see that surface's Aliases list, which is where the deployment is.
        Aliases         = @()
        Purpose         = 'Pre-rename WebGL host. Nothing deploys to it. PROPOSED FOR RETIREMENT (owner decision) - it served the retired July validation key for ~7 weeks.'
    },
    [pscustomobject]@{
        Name            = 'defenders-backend'
        Url             = 'https://defenders-backend.vercel.app'
        ProjectId       = ''
        Role            = 'api'
        DeployedByChain = $false
        Aliases         = @()
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
            # WebClient.DownloadData surfaces through PowerShell as a
            # MethodInvocationException wrapping the WebException, so reading
            # .Response off the OUTER exception yields nothing and the log says
            # "MethodInvocationException" where it should say "HTTP_404".
            # Walk the inner chain and name the real status.
            $ex = $_.Exception
            while ($null -ne $ex) {
                if ($ex -is [System.Net.WebException]) {
                    $response = $ex.Response
                    if ($null -ne $response) {
                        $result.Error = 'HTTP_' + [int]$response.StatusCode
                    } else {
                        $result.Error = 'WEB_' + $ex.Status
                    }
                    break
                }
                $ex = $ex.InnerException
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
# ALIAS SUPPORT (WO-1577)
# -----------------------------------------------------------------------------

# Run a vercel subcommand and hand back its combined output as TEXT. Never
# throws and never lets the caller read $LASTEXITCODE as a verdict: this repo's
# runners exit 0 on refusals, so every caller here judges the TEXT. A thrown
# exception is folded into the text too, so "the command could not run" and
# "the command ran and said no" are both just absent proof.
function Invoke-VercelText {
    param([string[]]$CliArgs)
    $prior = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $out = & vercel @CliArgs *>&1
        return (($out | ForEach-Object { $_.ToString() }) -join "`r`n")
    } catch {
        return ('VERCEL_THREW ' + $_.Exception.GetType().Name + ' ' + $_.Exception.Message)
    } finally {
        $ErrorActionPreference = $prior
    }
}

# The DEPLOYMENT host, i.e. the immutable `<project>-<hash>-<scope>.vercel.app`
# the CLI just created - NOT any alias. Anchored on the CLI's own "Production"
# label first and its JSON "url" field second, because a bare
# first-URL-in-the-log grab would happily return the Inspect URL
# (vercel.com/...) or the auto-alias on the "Aliased" line, and aliasing a
# domain to an alias is not the same operation.
function Get-DeploymentHost {
    param([string]$Text)
    $patterns = @(
        'Production\s+https://([a-z0-9-]+\.vercel\.app)',
        '"url"\s*:\s*"https://([a-z0-9-]+\.vercel\.app)"'
    )
    foreach ($p in $patterns) {
        $m = [regex]::Match($Text, $p)
        if ($m.Success) { return $m.Groups[1].Value }
    }
    return ''
}

# The dpl_ id, logged alongside the host so the fresh log names the deployment
# in the same vocabulary the Vercel dashboard does. Absence is not fatal - the
# host is what `vercel alias set` takes.
function Get-DeploymentId {
    param([string]$Text)
    $m = [regex]::Match($Text, 'dpl_[A-Za-z0-9]+')
    if ($m.Success) { return $m.Value }
    return ''
}

# PROVE the alias resolves to this deployment. `vercel alias set` printing
# "Success" is the CLI's own claim about itself; this asks the platform.
# Primary evidence is `vercel alias ls`, which prints one row per alias naming
# the deployment it points at - so a row carrying BOTH the domain and the new
# deployment host is the proof. `vercel inspect <domain>` is consulted ONLY when
# alias ls produced no row for the domain at all (its listing is paginated, so
# "no row" is genuinely ambiguous, while "a row that names a different
# deployment" is not). That is corroboration of a missing reading, not a retry:
# nothing is re-attempted and no failure is re-tried (WO-1577 scope guard 2).
function Test-AliasResolves {
    param([string]$Alias, [string]$DeploymentHost)

    $verdict = [pscustomobject]@{ Ok = $false; Source = 'none'; Evidence = ''; Reason = 'NO_EVIDENCE' }

    $lsText = Invoke-VercelText @('alias', 'ls', '--scope', $TeamScope, '--no-color')
    $sawDomain = $false
    foreach ($line in ($lsText -split "`r?`n")) {
        if ($line -notmatch [regex]::Escape($Alias)) { continue }
        $sawDomain = $true
        if ($line -match [regex]::Escape($DeploymentHost)) {
            $verdict.Ok = $true
            $verdict.Source = 'alias-ls'
            $verdict.Evidence = $line.Trim()
            $verdict.Reason = 'RESOLVED'
            return $verdict
        }
        $verdict.Evidence = $line.Trim()
        $verdict.Source = 'alias-ls'
        $verdict.Reason = 'ALIAS_POINTS_ELSEWHERE'
    }

    if ($sawDomain) { return $verdict }

    $inspectText = Invoke-VercelText @('inspect', ('https://' + $Alias), '--scope', $TeamScope, '--no-color')
    if ($inspectText -match [regex]::Escape($DeploymentHost)) {
        $verdict.Ok = $true
        $verdict.Source = 'inspect'
        $verdict.Evidence = $DeploymentHost
        $verdict.Reason = 'RESOLVED'
        return $verdict
    }
    $verdict.Source = 'inspect'
    $verdict.Reason = 'ALIAS_ABSENT_FROM_LS_AND_INSPECT'
    $inspectId = Get-DeploymentId $inspectText
    if (-not [string]::IsNullOrWhiteSpace($inspectId)) { $verdict.Evidence = $inspectId }
    return $verdict
}

# -----------------------------------------------------------------------------
# -ListSurfaces: the registry-reading path for other callers.
# -----------------------------------------------------------------------------
if ($ListSurfaces) {
    foreach ($s in $Surfaces) {
        Write-Line ("WEB_SURFACE name={0} role={1} chain={2} url={3}" -f $s.Name, $s.Role, $s.DeployedByChain, $s.Url)
        Write-Line ("  aliases={0}" -f (($s.Aliases -join ',') + ''))
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
# PHASE 0 - STAGE the store-compliance pages into the deployed output.
#
# THE 2026-09-03 SOLANA DAPP STORE REJECTION. The reviewer rejected the app
# because https://echoes-of-elarion.vercel.app/privacy and /terms both returned
# HTTP 404. publishing/config.yaml (urls: license_url / copyright_url /
# privacy_policy_url) names those two exact URLs as the listing's legal links,
# so a 404 there is a hard store blocker, not a cosmetic miss.
#
# WHY THEY 404ed - proven, not inferred: the marketing/legal site lives in
# site/ and was ITS OWN Vercel project (site/.vercel/project.json ->
# echoes-of-elarion; site/vercel.json sets outputDirectory "." and
# cleanUrls:true, which is what produced /privacy and /terms). On 2026-09-02
# 17:30 THIS FILE deployed the REPO ROOT to that project id
# (Builds/vercel-deploy-echoes-run.log: "Deploying .../echoes-of-elarion",
# 221 files). The repo root serves outputDirectory Builds/WebGL, and
# .vercelignore excludes /site entirely, so the Unity WebGL shell replaced the
# landing site. Both production domains now serve byte-comparable Unity shells
# and NEITHER carries privacy.html or terms.html.
#
# THE FIX, and why it is a copy rather than a second project: both production
# domains serve Builds/WebGL, so the pages have to BE in Builds/WebGL. The
# repo-root vercel.json rewrites /privacy -> /privacy.html and /terms ->
# /terms.html. Staging happens HERE, in the one file that already owns web
# shipping, so it can never drift from the verification below - re-inlining
# either half into a chain is the duplication CLAUDE.md section 16 forbids.
# =============================================================================
$LegalSources = @(
    [pscustomobject]@{ From = 'site\privacy.html'; To = 'privacy.html' },
    [pscustomobject]@{ From = 'site\terms.html';   To = 'terms.html' },
    [pscustomobject]@{ From = 'site\styles.css';   To = 'styles.css' }
)
$LegalPages = @(
    [pscustomobject]@{ Path = '/privacy'; Expect = 'Privacy Policy' },
    [pscustomobject]@{ Path = '/terms';   Expect = 'Terms of Use' }
)

# =============================================================================
# -DryRun - PRINT THE PLAN, CHANGE NOTHING.
#
# Placed here, after the registry and the staging/legal lists are built, so the
# plan is generated FROM the same data the real run uses. A hand-written plan
# would be a second copy of state and would drift exactly like every other copy
# this file's header catalogues.
#
# It runs no vercel command at all - not even a read-only one - so it needs no
# token and can be reviewed on any machine. The deployment host is unknown until
# a deploy happens, so the alias commands print with a placeholder; that
# placeholder is the point, because it shows WHERE the parsed value lands.
# =============================================================================
if ($DryRun) {
    Write-Line ("WEB_DRYRUN_PLAN verifyOnly={0} stageOnly={1} parityPath={2}" -f $VerifyOnly, $StageOnly, $ParityPath)

    if ((-not $VerifyOnly) -or $StageOnly) {
        $stageDirPlan = Join-Path $root 'Builds\WebGL'
        foreach ($f in $LegalSources) {
            Write-Line ("WOULD_STAGE copy {0} -> {1}" -f (Join-Path $root $f.From), (Join-Path $stageDirPlan $f.To))
        }
    }
    if ($StageOnly) {
        Write-Line 'WEB_DRYRUN_OK mode=StageOnly'
        exit 0
    }

    if (-not $VerifyOnly) {
        $pendingPlan = @($production | Where-Object { -not $_.DeployedByChain })
        if ($pendingPlan.Count -eq 0) {
            Write-Line 'WOULD_DEPLOY none reason=ALL_PRODUCTION_SURFACES_DEPLOYED_BY_CHAIN'
        }
        foreach ($s in $pendingPlan) {
            Write-Line ("WOULD_SET_ENV VERCEL_PROJECT_ID={0} VERCEL_ORG_ID={1}" -f $s.ProjectId, $TeamScope)
            Write-Line ("WOULD_RUN vercel deploy --target production --yes --no-color   # name={0}" -f $s.Name)
            Write-Line '  then PARSE the deployment host from that output (the "Production" line, else the JSON "url")'
            foreach ($alias in $s.Aliases) {
                Write-Line ("WOULD_RUN vercel alias set <deployment-host> {0} --scope {1} --no-color" -f $alias, $TeamScope)
                Write-Line ("WOULD_RUN vercel alias ls --scope {0} --no-color   # PROOF: a row naming BOTH {1} and <deployment-host>" -f $TeamScope, $alias)
                Write-Line ("  if that row is absent from ls entirely: WOULD_RUN vercel inspect https://{0} --scope {1} --no-color" -f $alias, $TeamScope)
                Write-Line ("  if still unproven: WEB_ALIAS_FAIL alias={0} -> refuse, exit 16, WEB_DEPLOY_OK withheld" -f $alias)
            }
            if ($s.Aliases.Count -eq 0) {
                Write-Line ("  no aliases registered for {0}" -f $s.Name)
            }
        }
    }

    foreach ($s in $production) {
        Write-Line ("WOULD_FETCH {0}{1}" -f $s.Url.TrimEnd('/'), $ParityPath)
        Write-Line ("WOULD_FETCH {0}/validation-key.txt" -f $s.Url.TrimEnd('/'))
        foreach ($page in $LegalPages) {
            Write-Line ("WOULD_FETCH {0}{1}   # expect text '{2}'" -f $s.Url.TrimEnd('/'), $page.Path, $page.Expect)
        }
    }
    foreach ($s in $dormant) {
        Write-Line ("WOULD_FETCH {0}/validation-key.txt   # dormant: divergent key refuses" -f $s.Url.TrimEnd('/'))
    }

    Write-Line 'WEB_DRYRUN_OK changed=nothing'
    exit 0
}

if ((-not $VerifyOnly) -or $StageOnly) {
    $stageDir = Join-Path $root 'Builds\WebGL'
    if (-not (Test-Path -LiteralPath $stageDir)) { Deny 'STAGE_DIR_MISSING_Builds_WebGL' 20 }
    $staged = New-Object System.Collections.Generic.List[string]
    foreach ($f in $LegalSources) {
        $src = Join-Path $root $f.From
        if (-not (Test-Path -LiteralPath $src)) { Deny ('LEGAL_SOURCE_MISSING_' + $f.To) 20 }
        $dst = Join-Path $stageDir $f.To
        Copy-Item -LiteralPath $src -Destination $dst -Force
        $stagedBytes = (Get-Item -LiteralPath $dst).Length
        Write-Line ("WEB_STAGE file={0} bytes={1}" -f $f.To, $stagedBytes)
        $staged.Add($f.To)
    }
    Write-Line ("WEB_STAGE_OK files={0} dir={1}" -f ($staged -join ','), $stageDir)
}

if ($StageOnly) { exit 0 }

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
            $aliasLog = Join-Path $builds ("vercel-alias-" + $s.Name + ".log")
            $text = ''

            # The env override wraps the deploy AND the alias calls: an alias is
            # scoped to the project that owns the deployment, so running
            # `vercel alias` outside this block would target whatever
            # .vercel/project.json happens to hold - the exact bug this file's
            # deploy path already refuses to have.
            try {
                $env:VERCEL_PROJECT_ID = $s.ProjectId
                $env:VERCEL_ORG_ID = $TeamScope

                try {
                    $ErrorActionPreference = 'Continue'
                    $out = & vercel deploy --target production --yes --no-color *>&1
                    $ErrorActionPreference = 'Stop'
                    $text = ($out | ForEach-Object { $_.ToString() }) -join "`r`n"
                    [System.IO.File]::WriteAllText($deployLog, $text, $NoBom)
                } catch {
                    $ErrorActionPreference = 'Stop'
                    [System.IO.File]::WriteAllText($deployLog, $_.Exception.Message, $NoBom)
                    Deny ("DEPLOY_THREW_" + $s.Name + "_" + $_.Exception.GetType().Name)
                }

                # Judged on the log, not on the CLI's exit status.
                if ($text -notmatch 'https://[a-z0-9-]+\.vercel\.app') {
                    Deny ("DEPLOY_URL_ABSENT_" + $s.Name)
                }

                # ---------------------------------------------------------
                # PHASE 1b - PROMOTE the public domains onto THIS deployment.
                # Everything below runs BEFORE WEB_DEPLOY_OK, so the marker can
                # never again mean "a deployment exists somewhere" while the
                # domain a player types serves last month's build (WO-1577).
                # ---------------------------------------------------------
                $deployHost = Get-DeploymentHost $text
                if ([string]::IsNullOrWhiteSpace($deployHost)) {
                    Deny ("DEPLOY_HOST_UNPARSED_" + $s.Name)
                }
                $deployId = Get-DeploymentId $text
                Write-Line ("WEB_DEPLOY_TARGET name={0} host={1} id={2}" -f $s.Name, $deployHost, $deployId)

                $aliasTranscript = New-Object System.Collections.Generic.List[string]
                $aliasedNames = New-Object System.Collections.Generic.List[string]
                foreach ($alias in $s.Aliases) {
                    Write-Line ("WEB_ALIAS_INTENT name={0} alias={1} host={2}" -f $s.Name, $alias, $deployHost)

                    # Argument order is `set <deployment> <alias>`. The 2026-09-07
                    # hand fix used exactly this order; the WO's prose had it the
                    # other way round, which would try to alias the domain ONTO
                    # the deployment name and fail in a confusing way.
                    $setText = Invoke-VercelText @('alias', 'set', $deployHost, $alias, '--scope', $TeamScope, '--no-color')
                    $aliasTranscript.Add('$ vercel alias set ' + $deployHost + ' ' + $alias)
                    $aliasTranscript.Add($setText)

                    # `Success!` from the CLI is the CLI's claim about itself and
                    # is deliberately NOT the verdict. Ask the platform.
                    $proof = Test-AliasResolves $alias $deployHost
                    $aliasTranscript.Add('$ vercel alias ls  -> ' + $proof.Source + ' ' + $proof.Reason + ' :: ' + $proof.Evidence)
                    if (-not $proof.Ok) {
                        [System.IO.File]::WriteAllText($aliasLog, ($aliasTranscript -join "`r`n"), $NoBom)
                        Write-Line ("WEB_ALIAS_FAIL name={0} alias={1} host={2} source={3} reason={4} evidence={5} log={6}" -f $s.Name, $alias, $deployHost, $proof.Source, $proof.Reason, $proof.Evidence, $aliasLog)
                        Deny ("ALIAS_DID_NOT_RESOLVE_" + $alias + "_" + $proof.Reason)
                    }
                    Write-Line ("WEB_ALIAS_OK name={0} alias={1} host={2} source={3} evidence={4}" -f $s.Name, $alias, $deployHost, $proof.Source, $proof.Evidence)
                    $aliasedNames.Add($alias)
                }
                if ($aliasTranscript.Count -gt 0) {
                    [System.IO.File]::WriteAllText($aliasLog, ($aliasTranscript -join "`r`n"), $NoBom)
                }
                Write-Line ("WEB_ALIAS_SET_OK name={0} count={1} aliases={2}" -f $s.Name, $aliasedNames.Count, ($aliasedNames -join ','))
            } finally {
                $env:VERCEL_PROJECT_ID = $priorProject
                $env:VERCEL_ORG_ID = $priorOrg
            }

            Write-Line ("WEB_DEPLOY_OK name={0} host={1} aliases={2} log={3}" -f $s.Name, $deployHost, (($s.Aliases -join ',') + ''), $deployLog)
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

# --- STORE COMPLIANCE: /privacy and /terms MUST be HTTP 200 on production ----
# These two URLs are a Solana dApp Store SUBMISSION REQUIREMENT
# (publishing/config.yaml urls:). Their absence was invisible to every gate in
# this repo and surfaced only as a STORE REJECTION on 2026-09-03, a day after
# the deploy that removed them. That is the exact silent-failure class this
# file exists to end, so it is gated the same way: the check runs on every
# public production surface, and WEB_PARITY_OK is withheld unless it passes.
# Judged by the marker on a FRESH log, never by an exit code.
$legalChecks = 0
foreach ($s in $production) {
    foreach ($page in $LegalPages) {
        $legalUri = $s.Url.TrimEnd('/') + $page.Path
        $legalKey = $page.Path.Trim('/')
        $legalGot = Get-Public $legalUri $TimeoutSec
        if (-not $legalGot.Ok) {
            Write-Line ("WEB_LEGAL_FAIL name={0} uri={1} error={2}" -f $s.Name, $legalUri, $legalGot.Error)
            Deny ('LEGAL_PAGE_UNREACHABLE_' + $s.Name + '_' + $legalKey)
        }
        $legalBody = [System.Text.Encoding]::UTF8.GetString($legalGot.Bytes)
        # A rewrite that silently falls through to the Unity shell would still
        # be a 200. Require the page's own heading text, so only the real
        # document passes.
        if ($legalBody -notmatch [regex]::Escape($page.Expect)) {
            Write-Line ("WEB_LEGAL_FAIL name={0} uri={1} bytes={2} reason=EXPECTED_TEXT_ABSENT expect={3}" -f $s.Name, $legalUri, $legalGot.Length, $page.Expect)
            Deny ('LEGAL_PAGE_WRONG_DOCUMENT_' + $s.Name + '_' + $legalKey)
        }
        Write-Line ("WEB_LEGAL name={0} uri={1} bytes={2} sha={3}" -f $s.Name, $legalUri, $legalGot.Length, $legalGot.Hash)
        $legalChecks = $legalChecks + 1
    }
}
if ($legalChecks -lt ($production.Count * $LegalPages.Count)) {
    Deny ('LEGAL_CHECKS_INCOMPLETE_' + $legalChecks)
}
Write-Line ("WEB_LEGAL_OK checks={0} paths={1}" -f $legalChecks, (($LegalPages | ForEach-Object { $_.Path }) -join ','))

Write-Line ("WEB_PARITY_OK surfaces={0} path={1} sha={2}" -f (($production | ForEach-Object { $_.Name }) -join ','), $ParityPath, $agreedIndex)
exit 0
