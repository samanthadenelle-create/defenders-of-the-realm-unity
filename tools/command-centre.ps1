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
    [switch]$LibraryOnly
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$builds = Join-Path $root 'Builds'
$runLog = Join-Path $builds 'command-centre.log'
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

if ($LibraryOnly) { return }

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

# Step 2: every command-centre run ships Builds/WebGL and api/, so it always
# touches shipped content. r2-ship.ps1 is the sole push/verify authority.
$r2Log = Join-Path $builds 'r2-parity.log'
$activeStep = 2; $activeMarker = 'R2_PARITY_OK'; $activeLog = $r2Log
$started = Get-Date
& (Join-Path $root 'tools\r2-ship.ps1')
Assert-FreshMarker 2 'R2_PARITY_OK' $r2Log $started -Utf16

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

# Build only after all pre-deploy gates pass. build-webgl removes stale output.
$webglLog = Join-Path $builds 'webgl-build.log'
$activeStep = 5; $activeMarker = 'WEBGL_BUILD_OK'; $activeLog = $webglLog
$started = Get-Date
& (Join-Path $root 'build-webgl.ps1')
if (-not (Test-Path -LiteralPath (Join-Path $builds 'WebGL\index.html'))) {
    Refuse 5 'WEBGL_BUILD_OK' $webglLog 'INDEX_MISSING'
}
if ((Get-Item -LiteralPath $webglLog).LastWriteTime -lt $started.AddSeconds(-2)) {
    Refuse 5 'WEBGL_BUILD_OK' $webglLog 'LOG_STALE_FROM_EARLIER_RUN'
}

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
