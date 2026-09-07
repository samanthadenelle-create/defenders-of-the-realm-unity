# =============================================================================
# command-centre.ps1 - gate, preview, promote, prove, and roll back production.
#
# PowerShell is deliberate: the Unity and R2 authorities are PowerShell scripts,
# and Windows PowerShell can decode the UTF-16 R2 log without introducing a
# second runner. This file is ASCII-only.
#
# Scope: WO-1199 steps 1-8 only. Sales, store, and status modes are excluded.
# Secrets are read only from environment variables. Never pass a token here.
# =============================================================================

[CmdletBinding()]
param(
    [string]$ProductionUrl = 'https://defenders-of-the-realm-v2.vercel.app',
    [string]$ProofWallet = '11111111111111111111111111111111',
    [int]$UnityTimeoutMin = 45,
    [int]$AliasTimeoutSec = 180,
    [switch]$AcknowledgeTreasuryRpcFailure,
    [switch]$LibraryOnly,
    # WO-1243 operator kill switches. -Maintenance runs the toggle surface and
    # EXITS; it never touches the ship chain below. Sealing an area must be
    # seconds of work under fire, not a release.
    [switch]$Maintenance,
    [ValidateSet('farming', 'raiding', 'arena', 'dungeons', 'store', 'server')]
    [string]$Area,
    [switch]$Seal,
    [switch]$Open,
    [string]$Message,
    # PROD-022 remote knobs. -Tunables runs the knob surface and EXITS; it never
    # touches the ship chain below. The whole point is flipping a candidate
    # mitigation without paying a 30-minute WebGL rebuild, so it gates on
    # DATABASE_URL and nothing else - the same reasoning as -Maintenance.
    [switch]$Tunables,
    [string]$Key,
    [string]$Value,
    [switch]$Clear,
    # WO-1576. Print the step order and every decision this run WOULD take, and
    # change NOTHING - no unity, no python, no node, no vercel, no upload. It is a
    # preview, never an override: it needs no secret, it writes to its OWN log, and
    # it can never emit COMMAND_CENTRE_OK. Mirrors tools\web-ship.ps1 -DryRun.
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$builds = Join-Path $root 'Builds'
# A dry run writes to its OWN log. command-centre.log is the artifact an operator
# reads to judge a real release, and a preview that changed nothing must not be
# able to overwrite the record of a run that changed production.
if ($DryRun) {
    $runLog = Join-Path $builds 'command-centre-dryrun.log'
} else {
    $runLog = Join-Path $builds 'command-centre.log'
}
$rollbackFile = Join-Path $builds 'PROD_ROLLBACK.txt'
$productionHost = ([uri]$ProductionUrl).Host

New-Item -ItemType Directory -Force -Path $builds | Out-Null
Set-Content -LiteralPath $runLog -Encoding ascii -Value "COMMAND_CENTRE_START $(Get-Date -Format o)"

function Write-Run {
    param([string]$Text)
    Write-Host $Text
    $Text | Out-File -LiteralPath $runLog -Encoding ascii -Append
}

function Refuse {
    param(
        [int]$Step,
        [string]$Marker,
        [string]$Log,
        [string]$Reason,
        [int]$Code = 20
    )
    Write-Run "COMMAND_CENTRE_REFUSED step=$Step wanted=$Marker log=$Log reason=$Reason"
    exit $Code
}

function Invoke-Captured {
    param(
        [scriptblock]$Command,
        [string]$Log
    )
    $priorPreference = $ErrorActionPreference
    try {
        # Native stderr is surfaced as ErrorRecord objects by Windows PowerShell.
        # Continue is required here so one diagnostic line cannot truncate all
        # later stderr/stdout. The caller judges structured output or a marker.
        $ErrorActionPreference = 'Continue'
        & $Command *>&1 | ForEach-Object { $_.ToString() } | Tee-Object -FilePath $Log
        $nativeExit = $LASTEXITCODE
        return $nativeExit
    } catch {
        $_.Exception.Message | Out-File -LiteralPath $Log -Encoding ascii -Append
        return 1
    } finally {
        $ErrorActionPreference = $priorPreference
    }
}

function Read-MixedJson {
    param([string]$Log)
    $text = Get-Content -LiteralPath $Log -Raw
    $start = $text.IndexOf('{')
    $end = $text.LastIndexOf('}')
    if ($start -lt 0 -or $end -le $start) { throw 'JSON object absent' }
    return $text.Substring($start, $end - $start + 1) | ConvertFrom-Json
}

function Wait-ProductionDeployment {
    param(
        [string]$ExpectedId,
        [string]$Log,
        [int]$TimeoutSec
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        Invoke-Captured { vercel inspect $productionHost --format=json --no-color } $Log | Out-Null
        try {
            $seen = [string](Read-MixedJson $Log).id
            if ($seen -eq $ExpectedId) { return $true }
        } catch {
            # A transient inspect response is not success. Keep polling until the
            # bounded deadline, then the caller emits a named refusal.
        }
        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)
    return $false
}

function Assert-FreshMarker {
    param(
        [int]$Step,
        [string]$Marker,
        [string]$Log,
        [datetime]$Started,
        [switch]$Utf16
    )
    if (-not (Test-Path -LiteralPath $Log)) {
        Refuse $Step $Marker $Log 'LOG_MISSING'
    }
    $item = Get-Item -LiteralPath $Log
    if ($item.LastWriteTime -lt $Started.AddSeconds(-2)) {
        Refuse $Step $Marker $Log 'LOG_STALE_FROM_EARLIER_RUN'
    }
    try {
        if ($Utf16) {
            $text = [System.IO.File]::ReadAllText($Log, [System.Text.Encoding]::Unicode)
        } else {
            $text = [System.IO.File]::ReadAllText($Log)
        }
    } catch {
        Refuse $Step $Marker $Log "LOG_READ_FAILED_$($_.Exception.GetType().Name)"
    }
    if ($text -notmatch $Marker) {
        Refuse $Step $Marker $Log 'MARKER_ABSENT'
    }
    Write-Run "STEP_${Step}_OK marker=$Marker log=$Log"
}

# =============================================================================
# WO-1576 - THE ADDRESSABLES BUILT-STATE, AND WHY THE STEP ORDER CHANGED.
#
# tools\r2_sync.py --verify-catalog reads, for every target ServerData holds,
#   Library\com.unity.addressables\aa\<target>\settings.json
# (r2_sync.py:321-322) and sys.exit()s when it is absent, because that file is
# the ONLY authority on which catalog the shipped player will ask the CDN for.
# r2-ship.ps1 catches that as `R2_PARITY_THREW target=<t>` (r2-ship.ps1:209-212)
# and withholds the aggregate marker.
#
# A FAILED WebGL content build DELETES that file (measured 2026-09-07). The chain
# used to run r2-ship at step 2 and build-webgl at step 5, so the next run refused
# at step 2 for a missing file the step it never reached would have restored. The
# refusal named `R2_PARITY_THREW`, i.e. the symptom, and the operator's only way
# out was to run build-webgl.ps1 by hand, then r2-ship.ps1 by hand, then the chain.
# A gate whose remedy is "a human remembers two commands" is the exact shape
# CLAUDE.md section 16 says is not a gate.
#
# AND THE ORDER WAS DISHONEST EVEN ON A HEALTHY MACHINE. DeNelle.Editor.
# AddressablesContentBuild.EnsureBuilt (Assets\Editor\AddressablesContentBuild.cs,
# read 2026-09-07) calls AddressableAssetSettings.BuildPlayerContent
# UNCONDITIONALLY - there is no skip-when-already-built branch - and WebGLBuild.cs
# calls it on every build. Bundle names are CONTENT-HASHED, so the step-5 build
# re-hashed every WebGL bundle AFTER step 2 had pushed and verified the previous
# generation. The parity marker was TRUE about bytes the deployed build no longer
# names. That is occurrence-shape identical to the four incidents catalogued in
# r2-ship.ps1's header.
#
# So the build now runs BEFORE the one r2-ship call, and r2-ship pushes the
# content that this run actually built. The cheap gates (compile, regression,
# schema, treasury, rollback id) still run FIRST, so a refusable release still
# refuses before paying for a 30-60 minute WebGL build.
#
# tools\r2-ship.ps1 stays the ONE push+verify path. Nothing here pushes, and
# nothing here verifies a catalog (CLAUDE.md section 16).
# =============================================================================
$webglStateFile = Join-Path $root 'Library\com.unity.addressables\aa\WebGL\settings.json'
$webglBuildOneLiner = 'powershell -NoProfile -ExecutionPolicy Bypass -File build-webgl.ps1'

function Get-R2ParityCause {
    # r2-parity.log is UTF-16LE (r2-ship.ps1 writes it with -Encoding Unicode so the
    # pre-push hook can parse it). Pull the FIRST line that names a real cause so a
    # refusal reports `R2_PARITY_THREW target=Android ...` instead of a bare
    # MARKER_ABSENT. r2-ship verifies EVERY target under ServerData, so the target
    # that failed is the single most useful fact in the refusal line.
    param([string]$Log)
    if (-not (Test-Path -LiteralPath $Log)) { return 'PARITY_LOG_MISSING' }
    try {
        $text = [System.IO.File]::ReadAllText($Log, [System.Text.Encoding]::Unicode)
    } catch {
        return "PARITY_LOG_UNREADABLE_$($_.Exception.GetType().Name)"
    }
    foreach ($line in ($text -split "`r?`n")) {
        if ($line -match 'R2_PARITY_(THREW|FAIL)') {
            return ($line.Trim() -replace '\s+', '_')
        }
    }
    return 'MARKER_ABSENT'
}

if ($LibraryOnly) { return }

# =============================================================================
# WO-1576 - -DryRun. PRINT THE STEP ORDER AND THE DECISIONS, CHANGE NOTHING.
#
# Placed after the function library and BEFORE the -Maintenance / -Tunables
# surfaces and the secret checks, so it runs on any machine with no VERCEL_TOKEN,
# no DATABASE_URL, no Unity and no network. The one decision it computes is
# computed the SAME way the real run computes it - a Test-Path on the file
# r2_sync.py actually reads - because a hand-written plan would be a second copy
# of state and would drift (CLAUDE.md sections 2, 5 and 16).
# =============================================================================
if ($DryRun) {
    $stateOk = Test-Path -LiteralPath $webglStateFile
    Write-Run ("COMMAND_CENTRE_DRYRUN_PLAN productionUrl={0} maintenance={1} tunables={2}" -f $ProductionUrl, $Maintenance, $Tunables)
    Write-Run ("WEBGL_ADDRESSABLES_STATE present={0} file={1}" -f $stateOk, $webglStateFile)
    if ($Maintenance) {
        Write-Run 'WOULD_REQUIRE env:DATABASE_URL'
        Write-Run 'WOULD_RUN node tools\maintenance-toggle.mjs <list|seal|open> - then EXIT; the ship chain is never reached'
        Write-Run 'COMMAND_CENTRE_DRYRUN_OK mode=Maintenance'
        exit 0
    }
    if ($Tunables) {
        Write-Run 'WOULD_REQUIRE env:DATABASE_URL'
        Write-Run 'WOULD_RUN node tools\client-tunables.mjs <list|set|clear> - then EXIT; the ship chain is never reached'
        Write-Run 'COMMAND_CENTRE_DRYRUN_OK mode=Tunables'
        exit 0
    }
    Write-Run 'WOULD_REQUIRE env:VERCEL_TOKEN env:DATABASE_URL'
    Write-Run 'WOULD_RUN step=1 run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run       # expect COMPILE_GATE_OK'
    Write-Run 'WOULD_RUN step=1 run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll # expect REGRESSION_OK <n>/<n> suites'
    Write-Run 'WOULD_RUN step=3 node tools\schema-parity.mjs                                     # expect SCHEMA_PARITY_OK'
    Write-Run 'WOULD_RUN step=3 node tools\treasury-verify.mjs <vault> --multisig <multisig>      # expect TREASURY_VERIFY_OK'
    Write-Run 'WOULD_RUN step=4 vercel inspect <production host> --format=json                   # expect ROLLBACK_ID_CAPTURED'
    if ($stateOk) {
        Write-Run 'WOULD_EMIT step=5 (no rebuild-needed marker; the WebGL Addressables built-state is present)'
    } else {
        Write-Run ("WOULD_EMIT step=5 R2_PARITY_REBUILD_NEEDED file={0} - the content build below restores it BEFORE parity is judged" -f $webglStateFile)
    }
    Write-Run 'WOULD_RUN step=5 build-webgl.ps1                                                  # expect WEBGL_BUILD_OK (index.html + fresh Builds\webgl-build.log)'
    Write-Run ("WOULD_ASSERT step=5 {0} exists after the build, else REFUSE naming it and the one-liner: {1}" -f $webglStateFile, $webglBuildOneLiner)
    Write-Run 'WOULD_RUN step=2 tools\r2-ship.ps1                                                # the ONE push+verify path; expect R2_PARITY_OK (UTF-16 log)'
    Write-Run '  order note: the build runs BEFORE r2-ship so the content-hashed bundles THIS run built are the ones pushed and verified'
    Write-Run 'WOULD_RUN step=5 vercel deploy --target production --skip-domain --yes            # expect CANDIDATE_URL_CAPTURED + CANDIDATE_CONTENT_MATCH'
    Write-Run 'WOULD_RUN step=6 vercel promote <candidate id> --yes                              # expect PRODUCTION_ALIAS_MATCH'
    Write-Run 'WOULD_RUN step=6 tools\web-ship.ps1 -VerifyOnly -AgainstLocal Builds\WebGL        # expect WEB_PARITY_OK'
    Write-Run 'WOULD_RUN step=7 GET <production>/api/auth/nonce?wallet=<proof wallet>            # expect PRODUCTION_DB_WRITE_OK'
    Write-Run 'WOULD_RUN step=8 vercel promote <rollback id> --yes  # ONLY if step 7 fails       # expect AUTO_ROLLBACK_COMPLETED'
    Write-Run 'COMMAND_CENTRE_DRYRUN_OK mode=ShipChain changed=nothing'
    exit 0
}

# =============================================================================
# WO-1243 - THE OPERATOR KILL SWITCHES. Runs and EXITS; the ship chain below is
# never reached from here.
#
# Owner ruling 2026-08-27, verbatim: "mine allows if we see someone finds a hack,
# we seal that area and patch". So this path deliberately gates on NOTHING but
# DATABASE_URL - no compile, no regression, no R2, no deploy. Requiring a green
# release train to close an area under active exploitation would make the control
# useless at exactly the moment it is needed.
#
#   tools\command-centre.ps1 -Maintenance
#   tools\command-centre.ps1 -Maintenance -Area raiding -Seal -Message "Raids are closed while we fix an exploit."
#   tools\command-centre.ps1 -Maintenance -Area raiding -Open
#
# Judge by the MARKER on a fresh log, never the exit code (CLAUDE.md section 8).
# =============================================================================
if ($Maintenance) {
    if ([string]::IsNullOrWhiteSpace($env:DATABASE_URL)) {
        Refuse 3 'DATABASE_URL_SET' 'environment' 'DATABASE_URL_MISSING'
    }
    if ($Seal -and $Open) {
        Refuse 9 'MAINTENANCE_TOGGLE_OK' 'arguments' 'SEAL_AND_OPEN_BOTH_GIVEN'
    }
    if (($Seal -or $Open) -and [string]::IsNullOrWhiteSpace($Area)) {
        Refuse 9 'MAINTENANCE_TOGGLE_OK' 'arguments' 'AREA_REQUIRED'
    }

    $toggleScript = Join-Path $root 'tools\maintenance-toggle.mjs'
    $toggleLog = Join-Path $builds 'maintenance-toggle.log'
    $started = Get-Date

    if ($Seal) {
        if ([string]::IsNullOrWhiteSpace($Message)) {
            # A seal with no message puts an unexplained wall in front of a paying
            # player. The banner has nothing to say without it.
            Refuse 9 'MAINTENANCE_TOGGLE_OK' 'arguments' 'MESSAGE_REQUIRED_TO_SEAL'
        }
        Write-Run "MAINTENANCE_INTENT action=seal area=$Area"
        Invoke-Captured { node $toggleScript seal $Area $Message } $toggleLog | Out-Null
        Assert-FreshMarker 9 'MAINTENANCE_TOGGLE_OK' $toggleLog $started
    }
    elseif ($Open) {
        Write-Run "MAINTENANCE_INTENT action=open area=$Area"
        Invoke-Captured { node $toggleScript open $Area } $toggleLog | Out-Null
        Assert-FreshMarker 9 'MAINTENANCE_TOGGLE_OK' $toggleLog $started
    }
    else {
        Invoke-Captured { node $toggleScript list } $toggleLog | Out-Null
        Assert-FreshMarker 9 'MAINTENANCE_LIST_OK' $toggleLog $started
    }

    Write-Run "COMMAND_CENTRE_MAINTENANCE_OK log=$toggleLog"
    exit 0
}

# =============================================================================
# PROD-022 - THE REMOTE KNOBS. Runs and EXITS; the ship chain below is never
# reached from here.
#
# Owner ruling 2026-09-02, verbatim: "make the testing as robust as possible with
# as many solutions as possible... all we really have to do is just flip a flag
# and possibly redeploy". So this path gates on NOTHING but DATABASE_URL - no
# compile, no regression, no R2, no deploy. Requiring a green release train to
# flip a diagnostic knob would defeat the reason the knobs exist.
#
#   tools\command-centre.ps1 -Tunables
#   tools\command-centre.ps1 -Tunables -Key pi.awaitInitBeforeFirstLoad -Value 1
#   tools\command-centre.ps1 -Tunables -Key pi.awaitInitBeforeFirstLoad -Clear
#
# -Clear REMOVES the override, returning that knob to the value the BUILD
# hardcodes. That is NOT the same as -Value 0.
#
# Judge by the MARKER on a fresh log, never the exit code (CLAUDE.md section 8).
# =============================================================================
if ($Tunables) {
    if ([string]::IsNullOrWhiteSpace($env:DATABASE_URL)) {
        Refuse 3 'DATABASE_URL_SET' 'environment' 'DATABASE_URL_MISSING'
    }
    if ($Clear -and -not [string]::IsNullOrWhiteSpace($Value)) {
        Refuse 9 'TUNABLES_CLEAR_OK' 'arguments' 'CLEAR_AND_VALUE_BOTH_GIVEN'
    }
    if (($Clear -or -not [string]::IsNullOrWhiteSpace($Value)) -and [string]::IsNullOrWhiteSpace($Key)) {
        Refuse 9 'TUNABLES_SET_OK' 'arguments' 'KEY_REQUIRED'
    }

    $tunableScript = Join-Path $root 'tools\client-tunables.mjs'
    $tunableLog = Join-Path $builds 'client-tunables.log'
    $started = Get-Date

    if ($Clear) {
        Write-Run "TUNABLES_INTENT action=clear key=$Key"
        Invoke-Captured { node $tunableScript clear $Key } $tunableLog | Out-Null
        Assert-FreshMarker 9 'TUNABLES_CLEAR_OK' $tunableLog $started
    }
    elseif (-not [string]::IsNullOrWhiteSpace($Value)) {
        Write-Run "TUNABLES_INTENT action=set key=$Key value=$Value"
        Invoke-Captured { node $tunableScript set $Key $Value } $tunableLog | Out-Null
        Assert-FreshMarker 9 'TUNABLES_SET_OK' $tunableLog $started
    }
    else {
        Invoke-Captured { node $tunableScript list } $tunableLog | Out-Null
        Assert-FreshMarker 9 'TUNABLES_LIST_OK' $tunableLog $started
    }

    Write-Run "COMMAND_CENTRE_TUNABLES_OK log=$tunableLog"
    exit 0
}

$activeStep = 0
$activeMarker = 'COMMAND_CENTRE_STEP_OK'
$activeLog = $runLog
trap {
    Refuse $activeStep $activeMarker $activeLog "UNHANDLED_$($_.Exception.GetType().Name)"
}

$activeStep = 5; $activeMarker = 'VERCEL_TOKEN_SET'; $activeLog = 'environment'
if ([string]::IsNullOrWhiteSpace($env:VERCEL_TOKEN)) {
    Refuse 5 'VERCEL_TOKEN_SET' 'environment' 'VERCEL_TOKEN_MISSING'
}
$activeStep = 3; $activeMarker = 'DATABASE_URL_SET'; $activeLog = 'environment'
if ([string]::IsNullOrWhiteSpace($env:DATABASE_URL)) {
    Refuse 3 'DATABASE_URL_SET' 'environment' 'DATABASE_URL_MISSING'
}

Set-Location $root

# Step 1: compile, then the registered data regression gate. The wrapper deletes
# each old log, but this chain independently checks freshness and marker shape.
$compileLog = Join-Path $builds 'compile-gate.log'
$activeStep = 1; $activeMarker = 'COMPILE_GATE_OK'; $activeLog = $compileLog
$started = Get-Date
& (Join-Path $root 'run-unity-method.ps1') `
    -Method 'DeNelle.Editor.CompileGate.Run' `
    -LogName 'compile-gate.log' `
    -TimeoutMin $UnityTimeoutMin `
    -ExpectMarker 'COMPILE_GATE_OK'
Assert-FreshMarker 1 'COMPILE_GATE_OK' $compileLog $started

$regressionLog = Join-Path $builds 'data-regression.log'
$activeStep = 1; $activeMarker = 'REGRESSION_OK \d+/\d+ suites'; $activeLog = $regressionLog
$started = Get-Date
& (Join-Path $root 'run-unity-method.ps1') `
    -Method 'DeNelle.Editor.DataRegression.RunAll' `
    -LogName 'data-regression.log' `
    -TimeoutMin $UnityTimeoutMin `
    -ExpectMarker 'REGRESSION_OK'
Assert-FreshMarker 1 'REGRESSION_OK \d+/\d+ suites' $regressionLog $started

# Step 3: prove the production database shape before uploading another backend.
$schemaLog = Join-Path $builds 'schema-parity-production.log'
$activeStep = 3; $activeMarker = 'SCHEMA_PARITY_OK'; $activeLog = $schemaLog
$started = Get-Date
Invoke-Captured { node (Join-Path $root 'tools\schema-parity.mjs') } $schemaLog | Out-Null
Assert-FreshMarker 3 'SCHEMA_PARITY_OK' $schemaLog $started

# WO-1159: compute treasury safety from mainnet before building or uploading. Read the
# public vault + multisig from canonical data so this chain authors no second address list.
# Owner policy: proven bad configuration BLOCKS; an unreachable RPC may continue only
# when the operator explicitly acknowledges degraded proof on this invocation.
$treasuryLog = Join-Path $builds 'treasury-verify.log'
$activeStep = 3; $activeMarker = 'TREASURY_VERIFY_OK'; $activeLog = $treasuryLog
$walletsPath = Join-Path $root 'Assets\Resources\Data\Canonical\wallets.json'
try { $wallets = Get-Content -LiteralPath $walletsPath -Raw | ConvertFrom-Json }
catch { Refuse 3 'TREASURY_VERIFY_OK' $walletsPath 'WALLET_CANON_INVALID' }
$vault = [string]$wallets.mainnetPurchaseRecipient.address
$multisig = [string]$wallets.mainnetPurchaseRecipient.squadsMultisig
if ([string]::IsNullOrWhiteSpace($vault) -or [string]::IsNullOrWhiteSpace($multisig)) {
    Refuse 3 'TREASURY_VERIFY_OK' $walletsPath 'VAULT_OR_MULTISIG_ABSENT'
}
$started = Get-Date
Invoke-Captured {
    node (Join-Path $root 'tools\treasury-verify.mjs') $vault --multisig $multisig
} $treasuryLog | Out-Null
$treasuryText = Get-Content -LiteralPath $treasuryLog -Raw
if ($treasuryText -match 'TREASURY_VERIFY_OK') {
    Assert-FreshMarker 3 'TREASURY_VERIFY_OK' $treasuryLog $started
} elseif ($treasuryText -match 'TREASURY_VERIFY_UNREACHABLE') {
    if (-not $AcknowledgeTreasuryRpcFailure) {
        Refuse 3 'TREASURY_VERIFY_OK' $treasuryLog 'RPC_UNREACHABLE_REQUIRES_-AcknowledgeTreasuryRpcFailure'
    }
    Write-Run "STEP_3_WARN marker=TREASURY_VERIFY_UNREACHABLE acknowledged=true log=$treasuryLog"
} else {
    # Includes TREASURY_VERIFY_FAIL. An acknowledgement can never downgrade a query that
    # successfully proved the production configuration wrong.
    Refuse 3 'TREASURY_VERIFY_OK' $treasuryLog 'TREASURY_CONFIGURATION_NOT_PROVEN'
}

# Step 4: resolve the alias BEFORE promotion and persist its immutable deployment
# id. This file is the rollback authority if the database proof later fails.
$inspectLog = Join-Path $builds 'vercel-production-before.json'
$activeStep = 4; $activeMarker = 'ROLLBACK_ID_CAPTURED'; $activeLog = $inspectLog
$started = Get-Date
Invoke-Captured { vercel inspect $productionHost --format=json --no-color } $inspectLog | Out-Null
try {
    $before = Read-MixedJson $inspectLog
    $rollbackId = [string]$before.id
} catch {
    Refuse 4 'ROLLBACK_ID_CAPTURED' $inspectLog 'INVALID_INSPECT_JSON'
}
if ([string]::IsNullOrWhiteSpace($rollbackId)) {
    Refuse 4 'ROLLBACK_ID_CAPTURED' $inspectLog 'DEPLOYMENT_ID_ABSENT'
}
Set-Content -LiteralPath $rollbackFile -Encoding ascii -Value $rollbackId
Write-Run "STEP_4_OK marker=ROLLBACK_ID_CAPTURED log=$rollbackFile"

# Build after the CHEAP pre-deploy gates pass and BEFORE R2 parity (WO-1576; the
# reasoning is written out in full at the top of this file). build-webgl removes
# stale output and rebuilds Addressables content unconditionally, so it both
# RESTORES a deleted built-state and re-hashes every bundle - which is exactly why
# the one r2-ship call below must come after it, never before it.
$webglLog = Join-Path $builds 'webgl-build.log'
$activeStep = 5; $activeMarker = 'WEBGL_BUILD_OK'; $activeLog = $webglLog
if (-not (Test-Path -LiteralPath $webglStateFile)) {
    # Name the real cause HERE, before anything can refuse on the symptom. This is
    # the state a failed content build leaves behind; the build below restores it.
    Write-Run "R2_PARITY_REBUILD_NEEDED file=$webglStateFile reason=NO_BUILT_ADDRESSABLES_STATE_FOR_WebGL action=BUILDING_CONTENT_FIRST"
}
$started = Get-Date
& (Join-Path $root 'build-webgl.ps1')
if (-not (Test-Path -LiteralPath (Join-Path $builds 'WebGL\index.html'))) {
    Refuse 5 'WEBGL_BUILD_OK' $webglLog 'INDEX_MISSING'
}
if ((Get-Item -LiteralPath $webglLog).LastWriteTime -lt $started.AddSeconds(-2)) {
    Refuse 5 'WEBGL_BUILD_OK' $webglLog 'LOG_STALE_FROM_EARLIER_RUN'
}
if (-not (Test-Path -LiteralPath $webglStateFile)) {
    # The build produced an index.html but no Addressables built-state, so the
    # parity step below could only ever report the symptom. Refuse HERE, naming the
    # file and the one command that restores it.
    Write-Run "STEP_5_FIX missing=$webglStateFile restore_with=$webglBuildOneLiner"
    Refuse 5 'WEBGL_BUILD_OK' $webglLog 'ADDRESSABLES_STATE_STILL_ABSENT_FOR_WebGL'
}
Write-Run "STEP_5_OK marker=WEBGL_BUILD_OK log=$webglLog state=$webglStateFile"

# Step 2: every command-centre run ships Builds/WebGL and api/, so it always
# touches shipped content. r2-ship.ps1 is the sole push/verify authority - this
# chain never inlines a push or a verify (CLAUDE.md section 16). It runs AFTER the
# content build above so the content-hashed bundles it pushes are the ones this
# run just built, and a missing built-state has already been restored and named.
$r2Log = Join-Path $builds 'r2-parity.log'
$activeStep = 2; $activeMarker = 'R2_PARITY_OK'; $activeLog = $r2Log
$started = Get-Date
& (Join-Path $root 'tools\r2-ship.ps1')
if (-not (Test-Path -LiteralPath $r2Log)) {
    Refuse 2 'R2_PARITY_OK' $r2Log 'LOG_MISSING'
}
if ((Get-Item -LiteralPath $r2Log).LastWriteTime -lt $started.AddSeconds(-2)) {
    Refuse 2 'R2_PARITY_OK' $r2Log 'LOG_STALE_FROM_EARLIER_RUN'
}
if ([System.IO.File]::ReadAllText($r2Log, [System.Text.Encoding]::Unicode) -notmatch 'R2_PARITY_OK') {
    # Report the LINE r2-ship wrote, not a bare MARKER_ABSENT. r2-ship verifies
    # EVERY target ServerData holds, so which target failed - and why - is the one
    # fact that turns this refusal into an action.
    Refuse 2 'R2_PARITY_OK' $r2Log (Get-R2ParityCause $r2Log)
}
Write-Run "STEP_2_OK marker=R2_PARITY_OK log=$r2Log"

# ---------------------------------------------------------------------------
# WO-1578 SEAM - THE LEGAL PAGES BELONG HERE, AND NOWHERE LATER.
#
# build-webgl.ps1 WIPES Builds\WebGL (build-webgl.ps1:74-78), and the candidate
# deploy below hashes Builds\WebGL\index.html and ships that exact tree. So
# site\privacy.html and site\terms.html must be staged into Builds\WebGL AFTER the
# build above and BEFORE the deploy below - this line is that seam. Staging them
# any later (which is what web-ship.ps1 does today, after production is already
# live) is the WO-1578 defect: production served 404 on /privacy.
#
# WO-1578 owns the implementation and is queued after WO-1576. It must not inline
# a copy list here: tools\web-ship.ps1 already holds $LegalSources as the single
# registry, and -StageOnly already stages it. Call that, judge its marker on a
# fresh log, and add nothing to this chain that could drift from it.
# ---------------------------------------------------------------------------

# Step 5: create a production-target CANDIDATE without assigning its domains.
# This is the explicit design correction to preview promotion: Vercel rebuilds a
# preview when promoting it. A production-target candidate can be promoted as the
# exact deployment verified here, while --skip-domain keeps it non-live for proof.
# Vercel reads VERCEL_TOKEN from the environment; it is never an argument.
$deployLog = Join-Path $builds 'vercel-production-candidate.log'
$activeStep = 5; $activeMarker = 'CANDIDATE_URL_CAPTURED'; $activeLog = $deployLog
$started = Get-Date
Invoke-Captured { vercel deploy --target production --skip-domain --yes --no-color } $deployLog | Out-Null
$deployText = Get-Content -LiteralPath $deployLog -Raw
$candidateUrl = [regex]::Matches($deployText, 'https://[a-z0-9-]+\.vercel\.app') |
    ForEach-Object { $_.Value } |
    Where-Object { $_ -notmatch 'api\.vercel\.com' } |
    Select-Object -Last 1
if ([string]::IsNullOrWhiteSpace($candidateUrl)) {
    Refuse 5 'CANDIDATE_URL_CAPTURED' $deployLog 'DEPLOYMENT_URL_ABSENT'
}
$candidateInspectLog = Join-Path $builds 'vercel-candidate-inspect.json'
Invoke-Captured { vercel inspect $candidateUrl --format=json --no-color } $candidateInspectLog | Out-Null
try { $candidateId = [string](Read-MixedJson $candidateInspectLog).id }
catch { Refuse 5 'CANDIDATE_ID_CAPTURED' $candidateInspectLog 'INVALID_INSPECT_JSON' }
if ([string]::IsNullOrWhiteSpace($candidateId)) {
    Refuse 5 'CANDIDATE_ID_CAPTURED' $candidateInspectLog 'DEPLOYMENT_ID_ABSENT'
}
$candidateProofLog = Join-Path $builds 'vercel-candidate-proof.log'
$remoteIndex = Join-Path $builds 'vercel-candidate-index.html'
$activeStep = 5; $activeMarker = 'CANDIDATE_CONTENT_MATCH'; $activeLog = $candidateProofLog
try {
    $localBytes = [System.IO.File]::ReadAllBytes((Join-Path $builds 'WebGL\index.html'))
    Remove-Item -LiteralPath $remoteIndex -Force -ErrorAction SilentlyContinue
    # vercel curl authenticates with VERCEL_TOKEN and automatically bypasses
    # deployment protection. The response body goes to a file; CLI prose cannot
    # be mistaken for the artifact being hashed.
    $curlExit = Invoke-Captured {
        vercel curl /index.html --deployment $candidateUrl --yes -- `
            --silent --show-error --output $remoteIndex
    } $candidateProofLog | Select-Object -Last 1
    if ($curlExit -ne 0) { throw "vercel curl exit $curlExit" }
    if (-not (Test-Path -LiteralPath $remoteIndex)) { throw 'vercel curl produced no index file' }
    $remoteBytes = [System.IO.File]::ReadAllBytes($remoteIndex)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $localHash = [BitConverter]::ToString($sha.ComputeHash($localBytes)).Replace('-', '')
    $remoteHash = [BitConverter]::ToString($sha.ComputeHash($remoteBytes)).Replace('-', '')
    "local=$localHash remote=$remoteHash id=$candidateId url=$candidateUrl" |
        Out-File -LiteralPath $candidateProofLog -Encoding ascii -Append
} catch {
    Refuse 5 'CANDIDATE_CONTENT_MATCH' $candidateProofLog "HTTP_PROOF_FAILED_$($_.Exception.GetType().Name)"
}
if ($localHash -ne $remoteHash) {
    Refuse 5 'CANDIDATE_CONTENT_MATCH' $candidateProofLog 'INDEX_HASH_MISMATCH'
}
Write-Run "STEP_5_OK marker=CANDIDATE_CONTENT_MATCH log=$candidateProofLog id=$candidateId"

# Step 6: promote the exact production-target candidate byte-proven above. The
# preview-rebuild branch is structurally unreachable because candidateId already
# names a production-target deployment. Outcome is the alias id, never CLI prose.
# Because /api is re-included, this ships the backend too; it is not WebGL-only.
Write-Run 'STEP_6_NOTICE shipping=Builds/WebGL+api reason=.vercelignore_reincludes_api'
$promoteLog = Join-Path $builds 'vercel-promote.log'
$activeStep = 6; $activeMarker = 'PROMOTION_COMPLETED'; $activeLog = $promoteLog
$started = Get-Date
Invoke-Captured { vercel promote $candidateId --yes --no-color } $promoteLog | Out-Null
$promotionPollLog = Join-Path $builds 'vercel-promotion-poll.json'
if (-not (Wait-ProductionDeployment $candidateId $promotionPollLog $AliasTimeoutSec)) {
    Refuse 6 'PRODUCTION_ALIAS_MATCH' $promotionPollLog 'ALIAS_POLL_TIMEOUT'
}
Write-Run "STEP_6_OK marker=PRODUCTION_ALIAS_MATCH log=$promotionPollLog id=$candidateId"

# Step 6b (WO-1316): PROVE THE COPY USERS HIT, NOT THE COPY WE TOUCHED.
#
# Steps 5 and 6 prove the CANDIDATE deployment's bytes and the production ALIAS
# id - both scoped to the ONE project .vercel/project.json links. FOUR Vercel
# projects serve this game, and TWO of them are public production domains. A
# deploy from this repo updates `defenders-of-the-realm-v2` and nothing else,
# so every marker above can go green while `echoes-of-elarion.vercel.app`
# keeps serving an older build of the game. Measured 2026-09-03: 40,100 bytes
# on one, 32,609 on the other, different Unity payload hashes.
#
# So this step fetches the PUBLIC production domains over plain HTTPS - not
# `vercel curl`, not a preview URL (previews are SSO-gated and 302 to sso-api,
# so they are not what a player or the Pi validator gets) - and refuses unless
# every one of them serves byte-identical content.
#
# The surface list lives in tools\web-ship.ps1 and NOWHERE ELSE. Do not restate
# a host, a project id or a role here; that duplication is the defect this step
# exists to catch (CLAUDE.md section 16's account of the copy-pasted R2
# push+verify pair, and section 2's stale WO-number block).
#
# -VerifyOnly is deliberate while the owner has not yet stated which domain the
# Pi Developer Portal points at (WO-1316 criterion 1): until then, deploying to
# the sibling production project is HER call, not the chain's. Once she answers,
# drop -VerifyOnly and this same one call both deploys the sibling surface and
# proves parity - one call, never "and also remember to do the other one".
$parityLog = Join-Path $builds 'web-parity.log'
$activeStep = 6; $activeMarker = 'WEB_PARITY_OK'; $activeLog = $parityLog
$started = Get-Date
Invoke-Captured {
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'web-ship.ps1') `
        -VerifyOnly -AgainstLocal (Join-Path $builds 'WebGL')
} (Join-Path $builds 'web-parity-invoke.log') | Out-Null
Assert-FreshMarker 6 'WEB_PARITY_OK' $parityLog $started

# Step 7: this endpoint writes an auth_nonces row. A 200 therefore proves the
# production function reached the live database, not merely that it booted.
$proofLog = Join-Path $builds 'production-db-proof.log'
$activeStep = 7; $activeMarker = 'PRODUCTION_DB_WRITE_OK'; $activeLog = $proofLog
$proofOk = $false
try {
    $proofUri = "$($ProductionUrl.TrimEnd('/'))/api/auth/nonce?wallet=$ProofWallet"
    $proof = Invoke-RestMethod -Method Get -Uri $proofUri -TimeoutSec 90
    $proofOk = ($proof.ok -eq $true -and -not [string]::IsNullOrWhiteSpace([string]$proof.nonce))
    "status=200 ok=$($proof.ok) noncePresent=$(-not [string]::IsNullOrWhiteSpace([string]$proof.nonce))" |
        Out-File -LiteralPath $proofLog -Encoding ascii
} catch {
    "requestFailed=$($_.Exception.GetType().Name)" | Out-File -LiteralPath $proofLog -Encoding ascii
}
if ($proofOk) {
    Write-Run "STEP_7_OK marker=PRODUCTION_DB_WRITE_OK log=$proofLog"
    Write-Run "COMMAND_CENTRE_OK deployment=$candidateId rollback=$rollbackId"
    exit 0
}

# Step 8: a failed database proof automatically restores the id captured before
# promotion. Rollback success does not turn the failed release green.
$rollbackLog = Join-Path $builds 'vercel-rollback.log'
$activeStep = 8; $activeMarker = 'AUTO_ROLLBACK_COMPLETED'; $activeLog = $rollbackLog
Invoke-Captured { vercel promote $rollbackId --yes --no-color } $rollbackLog | Out-Null
$rollbackPollLog = Join-Path $builds 'vercel-rollback-poll.json'
if (Wait-ProductionDeployment $rollbackId $rollbackPollLog $AliasTimeoutSec) {
    Write-Run "STEP_8_OK marker=AUTO_ROLLBACK_COMPLETED log=$rollbackPollLog target=$rollbackId"
    Refuse 7 'PRODUCTION_DB_WRITE_OK' $proofLog 'POST_DEPLOY_DB_PROOF_FAILED_ROLLED_BACK' 27
}
Refuse 8 'AUTO_ROLLBACK_COMPLETED' $rollbackPollLog 'ROLLBACK_ALIAS_POLL_TIMEOUT' 28
