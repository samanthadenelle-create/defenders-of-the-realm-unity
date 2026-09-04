# WORK ORDER 1365 - The AAB has no ship chain: no wrapper, no R2 push, no size guard

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Release engineering / build chain - `tools/*.ps1`, `Assets/Editor/AndroidBuild.cs`
**Type:** EXISTING lane, missing gates
**Minted:** 2026-09-04 (CLI)
**Blocks:** shipping any AAB safely. Independent of WO-1363/1364 - runs in its own lane.

## THE FINDING

**Every gate discipline this repo built for the APK lane is absent from the AAB lane.** The APK goes
through `overnight-apk-build.ps1` / `morning-ship-chain.ps1`, which block on `tools\r2-ship.ps1` and
assert `-ExpectMarker '[AndroidBuild] SUCCEEDED'`. The AAB goes through nothing.

Three holes, each independently able to ship a broken artifact.

### 1. ⛔ NO SANCTIONED WRAPPER - `BuildGooglePlayAab` IS INVOKED BY NO SCRIPT

`grep BuildGooglePlayAab` across the repo hits `Assets/Editor/AndroidBuild.cs:85`,
`Assets/Editor/Regression/GooglePlayPackagingRegression.cs:35`, and **prose** in
`CLI_LANES_WO_NUMBERS.md`, `docs/MASTER_CATALOG/economy-meta.md`,
`WorkOrders/WORK_ORDER_1255_*.md:152`, `WORK_ORDER_1282_*.md:16`. **No `.ps1` anywhere invokes it.**

- `overnight-apk-build.ps1:72` -> `DeNelle.Editor.AndroidBuild.BuildSeekerApk` (APK)
- `morning-ship-chain.ps1:109` -> `DeNelle.Editor.AndroidBuild.BuildSeekerApk` (APK)
- `tools/android/` holds only `assert-google-play-aab-clean.ps1` and `patch-solana-sdk.ps1` -
  neither builds.

The 2026-09-01 AAB was produced by a hand-assembled `Unity.exe` command line
(`Builds/ui-reskin-final-google-play-aab-v2.log:11-24`) - **`-buildTarget Android -batchmode -quit
-projectPath D:\eoa -executeMethod DeNelle.Editor.AndroidBuild.BuildGooglePlayAab -logFile ...`** -
with **no `-ExpectMarker`**, so it produced `VERDICT=PASS-UNASSERTED`-shaped evidence at best.
⛔ CLAUDE.md §16's lesson exactly: *a raw hand-built invocation bypasses every gate the scripts hold.*

**The terminal marker to assert is `[AndroidBuild] SUCCEEDED`** (`AndroidBuild.cs:201-202`; proven at
log `:38201`). Failure counterparts: `PLAY_SOURCE_ISOLATION_FAIL` (`GooglePlayPackagingGate.cs:60`),
`PLAY_ARTIFACT_REJECTED` (`AndroidBuild.cs:196`), `ANDROID_CATALOG_MISSING` (`AndroidBuild.cs:272`),
`[AndroidBuild] FAILED` (`AndroidBuild.cs:206`).

### 2. ⛔ THE AAB LANE NEVER PUSHES R2. THIS IS §16 OCCURRENCE FIVE WAITING TO HAPPEN.

- The AAB resolves the **same** remote catalog as the APK: `AddressableAssetSettings.asset:20`
  `m_BuildRemoteCatalog: 1`, `Remote.LoadPath` =
  `https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/[BuildTarget]` with `BuildTarget` =
  `activeBuildTarget`. **There is no per-artifact (Play vs Seeker) profile** - both resolve
  `.../Android/`. `PLAY_NEUTRAL_REWRITE_OK` rewrites catalogs for *token* neutrality, not for a
  different host.
- **`AndroidBuild.cs` contains no shell-out at all.** Callers of `tools/r2-ship.ps1` are exactly
  `overnight-apk-build.ps1:106`, `morning-ship-chain.ps1:158`,
  `install-apk-to-seeker.ps1:136` (`-WarnOnly`), `tools/command-centre.ps1:304`. **Every one is an
  APK/Windows lane. None is the AAB lane.**
- Each AAB build stamps a NEW version (see §3) and therefore requests a NEW content-hashed catalog -
  `AssertAndroidCatalogForThisBuild` (`AndroidBuild.cs:254-278`) requires
  `ServerData/Android/catalog_<newVersion>.bin`, built in the same run. **Without a push, an
  installed AAB 404s its art and the player gets capsule enemies with no error on screen.**
- ⚠ **NOT PROVEN:** whether the 2026-09-01 AAB was ever pushed. No `R2_PUSH_OK`/`R2_PARITY_OK`
  appears in its build log (the push is a separate process). WO-1362 finding 16 narrows it: the
  AAB's catalog `catalog_2026.09.01.350657` **does** resolve HTTP 200 on R2, but the freshest parity
  proof covers `catalog_2026.09.04.354315` - a different catalog. **Bundle-level parity for the
  AAB's own catalog is unproven.**

### 3. ⛔ NOTHING IN THE REPO ASSERTS AAB SIZE

Verified by grep across `Assets/Editor/` and `tools/` for `Length` / byte thresholds / `500 MB` /
`524288000` / `bundletool get-size`:
- `GooglePlayPackagingRegression.cs` - zero size assertions (all ~70 calls are substring greps).
- `GooglePlayPackagingGate.AssertBuiltArtifact` (`:126-165`) - opens the zip, scans tokens, no size.
- `assert-google-play-aab-clean.ps1` - the only `.Length` uses (`:70-71`) are string-tail bookkeeping.
- `AndroidBuild.cs:201` **prints** size and does not gate:
  `Debug.Log($"[AndroidBuild] SUCCEEDED - {summary.totalSize / (1024 * 1024)} MB ...")`.
- **No `bundletool get-size` invocation exists anywhere in `tools/`.**

**The consequence, measured:** `docs/releases/GOOGLE_PLAY_RC_2026-08-30.md:9-26` recorded a
482,843,623-byte candidate measuring 479.4 MB download - a 20.6 MB margin under Play's 500 MB
ceiling. The AAB on disk today is **514,062,537 bytes, +31.2 MB**, built two days later. Applying the
RC's own AAB-to-download ratio estimates **~510 MB, roughly 10 MB OVER the ceiling.**
⚠ That last number is an ESTIMATE - `GOOGLE_PLAY_RC_2026-08-30.md:15-16, :59-60` insists only Play
Console is authoritative. **31 MB appeared in two days with every marker green, because no marker
measures size.**

## THE WORK

1. **A size guard in the build chain.** Emit the measured artifact size and a download estimate, and
   FAIL below a configurable ceiling. `bundletool get-size total` (1.18.3 was used for the RC) is the
   honest measurement; raw AAB bytes are a cheap proxy. Marker + fresh log, judged by marker.
2. **Find the 31 MB.** The RC credits its margin to *"a conservative Android texture pass, 65
   eligible overrides."* ⭐ **First question is whether that pass is still applied or was lost in the
   09-01..09-03 wave.** If recovering it clears the ceiling, the size problem is a 1-day item and
   Play Asset Delivery is unnecessary. **Establish this before anyone commits to PAD** - PAD moves
   423.94 MiB of `bin/Data` out of the base module and collides with this project's custom R2 remote
   `LoadPath`, which has no local fallback.
3. **An AAB wrapper script that calls `tools\r2-ship.ps1` and BLOCKS**, exactly as
   `overnight-apk-build.ps1` does. ⛔ **Call the one file - do not re-inline the push or the verify**
   (CLAUDE.md §16: the copy-pasted pair had already drifted between two chains).
4. **Assert `[AndroidBuild] SUCCEEDED` via `-ExpectMarker`** so the run cannot report
   `PASS-UNASSERTED`.

## ACCEPTANCE

- [ ] An AAB build that exceeds the ceiling FAILS, proven by running the guard against the current
      514,062,537-byte artifact and quoting the failure. **Prove RED first.**
- [ ] An AAB build with no fresh R2 push FAILS or refuses, on the marker.
- [ ] The wrapper is one command, documented in `docs/CLI_OPERATIONS_RUNBOOK.md`'s build table in the
      SAME commit (§15).
- [ ] The 31 MB is ACCOUNTED FOR - a measured statement of where it went, not a theory.
- [ ] Whether the 09-01 AAB's catalog was ever pushed is ANSWERED with evidence, either way.

## RELEVANT, VERIFIED 2026-09-04 - saves the next session an hour

- ⛔ **`tools/android/patch-solana-sdk.ps1` IS NO LONGER NEEDED and would ERROR.** The SDK is now
  embedded, not a git-URL package: `Packages/manifest.json:3` reads
  `"com.solana.unity_sdk": "file:com.solana.unity_sdk"`, and `Library/PackageCache/*solana*` does not
  exist, so the script hits its `if (-not $pkg)` guard and exits 2. Both patches are permanently
  applied in-tree (commit `97e01b00e`). **Any doc still listing it as an APK/AAB precondition is
  stale.**
- **Release signing is proven working.** `androidUseCustomKeystore: 1`
  (`ProjectSettings/ProjectSettings.asset:286`), keystore `:273`, alias `dotr` `:274`. Passwords come
  from gitignored `keystore.properties` at repo root (311 bytes, present), read by
  `ApplyReleaseSigning()` (`AndroidBuild.cs:367-404`). ⚠ **If that file is absent or incomplete the
  build SILENTLY FALLS BACK TO DEBUG SIGNING** (`:372-374`, `:393-395`) - which Play rejects.
  Proven on 09-01: log `:578` `RELEASE signing: keystore='dotr-release.keystore' alias='dotr'`.
  **Worth its own assertion in the wrapper.**
- **No caller-supplied scripting define is needed** - `ArtifactScriptingDefines(isGooglePlay:true)`
  (`AndroidBuild.cs:212-225`) appends `GOOGLE_PLAY`, strips `DAPP_STORE` and `SOLANA_SDK`
  unconditionally.
- **Duration ~14-16 min wall clock** on a warm Library (three prior AAB logs: 15m32s / 14m08s /
  13m31s; `BuildPlayer` alone ~11 min). The AAB file lands ~3 min before the log closes - the tail is
  the artifact token scan.
- **`androidSplitApplicationBinary: 0`** (`ProjectSettings.asset:189`) - one 514 MB base module,
  which is why size lands where it does.
- ⚠ `AndroidBuild.cs:10`'s header comment says Unity `6000.4.7f1`; the pinned editor is `6000.4.8f1`.
  Stale comment - fix it while you are in the file (§15).
