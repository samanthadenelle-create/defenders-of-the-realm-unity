# =============================================================================
# patch-solana-sdk.ps1 - WO-766: make the Solana Unity SDK v1.2.9 Android-buildable.
#
# The SDK is pinned in Packages/manifest.json as a GIT URL, so it resolves into
# Library/PackageCache and is RE-RESOLVED (wiping any local edit) on a clean
# import. Two patches are required for the Android player to build at all, so
# they must be re-appliable - hence this script rather than a hand edit.
#
# Run it AFTER packages resolve and BEFORE building the APK:
#   powershell -ExecutionPolicy Bypass -File .\tools\android\patch-solana-sdk.ps1
#
# IDEMPOTENT: re-running on an already-patched cache is a no-op and exits 0.
# Full rationale + captured build failures: Packages-vendoring notes in
# WorkOrders/WORK_ORDER_766_solana_mobile_wallet_real_connect.md.
#
# ---------------------------------------------------------------------------
# PATCH 1 - rename BouncyCastle.Crypto.dll -> BouncyCastle.Cryptography.dll
#   The file ships as BouncyCastle.Crypto.dll but its ASSEMBLY IDENTITY is
#   'BouncyCastle.Cryptography, Version=2.0.0.0' (BouncyCastle 2.x renamed the
#   assembly, kept the old file name). Solana.Unity.{Wallet,KeyStore,Dex} all
#   reference that identity. Unity's CIL Linker resolves by FILE NAME, so the
#   Android managed-strip step dies:
#     Fatal error in Unity CIL Linker
#     Mono.Cecil.AssemblyResolutionException: Failed to resolve assembly:
#       'BouncyCastle.Cryptography, Version=2.0.0.0, ... PublicKeyToken=null'
#   (The 'PublicKeyToken=null' is a red herring - Cecil's placeholder for a name
#   it never resolved. The real dll IS strong-named 072edcf4a5328938, and the
#   linker response file proves the file WAS passed: -a="...BouncyCastle.Crypto.dll".)
#
# PATCH 2 - delete the bundled AndroidX artifacts under Web3AuthSDK/Android/
#   Every one of them ALSO resolves from Maven via the Firebase/AndroidX graph in
#   Assets/Plugins/Android/mainTemplate.gradle, so D8 sees each class twice:
#     A failure occurred while executing CheckDuplicatesRunnable
#       Duplicate class androidx.concurrent.futures.AbstractResolvableFuture ...
#       Duplicate class androidx.versionedparcelable.VersionedParcel ...
#   SAFE because these back WEB3AUTH (social login), which this project does not
#   use - WO-766 settled on LoginWalletAdapter (Mobile Wallet Adapter / Seed
#   Vault) and there are ZERO Web3Auth references under Assets/_Modules/Wallet/.
#   The C# is left intact, so nothing fails to COMPILE; only the Android runtime
#   deps of a login path we never invoke are removed.
#   DIRECTION MATTERS: we drop the SDK's vendored copies and KEEP MAVEN, so
#   AndroidX versions stay under Gradle's normal resolution and Firebase can move
#   them. Excluding the Maven modules instead would pin us to the SDK's 2023 set.
#
# DO NOT "fix" this by embedding the package under Packages/ - that was tried
# (2026-08-02) and breaks compilation outright: Unity then scans the package's
# own nested Packages/ folder as project assets and NativeWebSocket.asmdef is
# defined twice ("Assembly with name 'NativeWebSocket' already exists").
#
# ASCII-only on purpose (PS 5.1 reads BOM-less files as ANSI).
# =============================================================================
$ErrorActionPreference = 'Stop'
$proj = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

$cacheRoot = Join-Path $proj 'Library\PackageCache'
$pkg = Get-ChildItem $cacheRoot -Directory -Filter 'com.solana.unity_sdk*' -ErrorAction SilentlyContinue |
       Select-Object -First 1
if (-not $pkg) {
    Write-Error "Solana SDK not found under '$cacheRoot'. Let Unity resolve packages first (e.g. run the compile gate), then re-run."
    exit 2
}
Write-Host "[patch-solana] package: $($pkg.FullName)"
$changes = 0

# --- PATCH 1 : dll file name must match its assembly identity -----------------
$dllDir = Join-Path $pkg.FullName 'Packages'
$oldDll = Join-Path $dllDir 'BouncyCastle.Crypto.dll'
$newDll = Join-Path $dllDir 'BouncyCastle.Cryptography.dll'
if (Test-Path $newDll) {
    Write-Host "[patch-solana] PATCH 1 already applied (BouncyCastle.Cryptography.dll present)."
} elseif (Test-Path $oldDll) {
    Rename-Item $oldDll 'BouncyCastle.Cryptography.dll'
    if (Test-Path "$oldDll.meta") { Rename-Item "$oldDll.meta" 'BouncyCastle.Cryptography.dll.meta' }
    Write-Host "[patch-solana] PATCH 1 applied: BouncyCastle.Crypto.dll -> BouncyCastle.Cryptography.dll"
    $changes++
} else {
    Write-Warning "[patch-solana] PATCH 1 SKIPPED: neither dll name found in '$dllDir' (SDK layout changed?)."
}

# --- PATCH 2 : drop bundled AndroidX that Maven already provides ---------------
$w3aAndroid = Join-Path $pkg.FullName 'Runtime\Plugins\Web3AuthSDK\Android'
if (Test-Path $w3aAndroid) {
    # Match EVERY bundled binary in this folder (+ its .meta), not just 'androidx.*'.
    # First pass removed the 9 AndroidX artifacts and the build then failed on
    # com.google.guava.listenablefuture-1.0.jar - same class, different vendor
    # prefix (WO-766's note says "AndroidX/Guava fixes", plural for a reason).
    # Every .jar/.aar in this folder is a WEB3AUTH runtime dependency, and Web3Auth
    # is the login path this project does not use - so the rule is the FOLDER, not a
    # name pattern. BrowserView.java (the only source file here) is deliberately left.
    $dupes = Get-ChildItem $w3aAndroid -File |
             Where-Object { $_.Extension -in '.jar', '.aar' -or $_.Name -like '*.jar.meta' -or $_.Name -like '*.aar.meta' }
    if ($dupes) {
        foreach ($f in $dupes) { Remove-Item $f.FullName -Force }
        Write-Host "[patch-solana] PATCH 2 applied: removed $($dupes.Count) bundled Web3Auth artifact/meta files."
        $changes++
    } else {
        Write-Host "[patch-solana] PATCH 2 already applied (no bundled jar/aar remains)."
    }
} else {
    Write-Warning "[patch-solana] PATCH 2 SKIPPED: '$w3aAndroid' not found (SDK layout changed?)."
}

Write-Host "[patch-solana] DONE ($changes patch(es) applied this run)."
exit 0
