# distribute-android.ps1 - push (optionally build first) the Android APK to Firebase App Distribution testers.
#
# One-time setup (interactive, do these once - see docs/TESTER_APK_DISTRIBUTION.md):
#   1) firebase login
#   2) Create a Firebase project + register an Android app with package
#      com.denellestudios.echoesofelarion  ->  copy its App ID (1:...:android:...)
#   3) In the console, App Distribution -> Get started (once).
#
# Then, per build:
#   .\distribute-android.ps1 -AppId 1:1234567890:android:abc -Testers "friend@email.com"
#   .\distribute-android.ps1 -AppId 1:... -Groups testers -Build       # build fresh first
#
# App ID resolution order: -AppId param  ->  $env:FIREBASE_APP_ID  ->  firebase-appid.txt (gitignored).

param(
  [string]$AppId   = $env:FIREBASE_APP_ID,
  [string]$Notes   = "",
  [string]$Testers = "",
  [string]$Groups  = "testers",
  [string]$Apk     = "Builds/Android/DefendersOfTheRealm.apk",
  [switch]$Build
)
$ErrorActionPreference = "Stop"

# WO-1173: this script can distribute an existing APK, so it needs its own gate
# even when -Build is absent. A green build from yesterday cannot prove today's DB.
$schemaLog = Join-Path $PSScriptRoot 'Builds\schema-parity.log'
New-Item -ItemType Directory -Force -Path (Split-Path $schemaLog) | Out-Null
& node (Join-Path $PSScriptRoot 'tools\schema-parity.mjs') 2>&1 | Tee-Object -FilePath $schemaLog
if ($LASTEXITCODE -ne 0 -or
    -not (Select-String -Path $schemaLog -Pattern '^SCHEMA_PARITY_OK ' -Quiet)) {
  Write-Error "SCHEMA_PARITY_OK absent. Refusing Firebase distribution; see $schemaLog"
  exit 4
}
Write-Host "[distribute] SCHEMA_PARITY_OK - production dependency matches api/schema.sql"

# Resolve the App ID (param > env > gitignored file).
if (-not $AppId -and (Test-Path "firebase-appid.txt")) { $AppId = (Get-Content firebase-appid.txt -Raw).Trim() }
if (-not $AppId) {
  Write-Error "No Firebase App ID. Pass -AppId, set `$env:FIREBASE_APP_ID, or create firebase-appid.txt (gitignored)."
  exit 1
}

if (-not (Get-Command firebase -ErrorAction SilentlyContinue)) {
  Write-Error "firebase CLI not found. Install: npm install -g firebase-tools ; then: firebase login"
  exit 1
}

# Optional: build a fresh APK first.
if ($Build) {
  Write-Host "[distribute] Building Android APK (release, dev-menu-free)..."
  & powershell -ExecutionPolicy Bypass -File .\run-unity-method.ps1 -Method DeNelle.Editor.AndroidBuild.BuildSeekerApk -LogName android-build.log
}

if (-not (Test-Path $Apk)) {
  Write-Error "APK not found: $Apk  (run with -Build, or build it first)"
  exit 1
}

if (-not $Notes) { $Notes = "build $(Get-Date -Format 'yyyy-MM-dd HH:mm')" }

$fbArgs = @("appdistribution:distribute", $Apk, "--app", $AppId, "--release-notes", $Notes)
if ($Testers) { $fbArgs += @("--testers", $Testers) }
if ($Groups)  { $fbArgs += @("--groups",  $Groups)  }

Write-Host "[distribute] firebase $($fbArgs -join ' ')"
& firebase @fbArgs
if ($LASTEXITCODE -ne 0) { Write-Error "firebase distribute failed (exit $LASTEXITCODE)"; exit $LASTEXITCODE }
Write-Host "[distribute] DONE -> testers notified."
