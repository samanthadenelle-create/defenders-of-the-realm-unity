# WORK ORDER 281 — BUILD BLOCKER: Addressables SBP content build fails (Windows + WebGL)

**Status:** ✅ **CLOSED — the fix is in the tree.** `AddressableAssetSettings.asset:61` carries
`m_BuildAddressablesWithPlayerBuild: 0`, which is exactly this WO's preferred fix option, and player
builds have shipped since (Android bundle 316536, Windows exe 2026-08-08). No longer a P0, no longer a
blocker.

> ⚠ **§15 STALENESS FLAG (2026-08-09).** This read `READY TO IMPLEMENT — P0, blocks ALL player builds`
> while builds were demonstrably shipping. A false P0 on the board is worse than no entry: it distorts
> every priority call made around it.
**Found:** overnight gatekeeper pipeline run. Compile + tests pass; the PLAYER BUILD fails.

## Symptom
`build-windows.ps1` / `DesktopBuild.BuildWindows` aborts with:
```
DisplayProgressbar: Processing Addressable Group
InvalidOperationException: Unable to build with the current configuration, please check the Build Settings.
SBP ErrorException
BuildFailedException: Failed to build Addressables content, content not included in Player Build.
```
The exe is never emitted (`Builds/Windows` empty, no `level0`).

## Diagnosis (verified from Builds/build.log)
1. **Compile is CLEAN** — batchmode script compile passes, 0 `error CS`.
2. **License is NOT the cause** — early handshake errors self-recover ("Successfully resolved entitlement details"). Transient, ignore.
3. **TagManager.asset parse warning is noise** — appears once, file is byte-identical to committed, non-fatal.
4. **ROOT CAUSE:** the Addressables **SBP (Scriptable Build Pipeline) content build** throws `InvalidOperationException: Unable to build with the current configuration` while "Processing Addressable Group". NOT a stale cache — reproduced after clearing `Library/BuildCache` + `Library/com.unity.addressables`.
5. **Key config contradiction:** `Assets/AddressableAssetsData/AddressableAssetSettings.asset` has `m_BuildAddressablesWithPlayerBuild: 0` (do NOT build with player), yet `AddressablesPlayerBuildProcessor` runs during the build anyway. The Addressables groups present are mostly the **Unity Localization** package's (Localization-Locales, Localization-Assets-Shared, Localization-String-Tables-English) + Default Local Group.

## Why this matters / is safe to decouple
Runtime gameplay loads catalogs via **CanonicalJson → Resources.Load** (MEMORY: webgl-canonical-json-loader), NOT via Addressables. Addressables here is dragged in by the Localization package. So the player build should be able to proceed without a full Addressables content build, or with the failing group fixed.

## Fix options (in order of preference)
1. **Decouple:** ensure the player build does not invoke the Addressables content build — set the global "Build Addressables on Player Build" preference to "Do not build" (Addressables → Settings, or `AddressableAssetSettingsDefaultObject.Settings.BuildAddressablesOnPlayerBuild = PlayerBuildOption.DoNotBuildWithPlayer`). Confirm Localization can resolve at runtime without a fresh content build (it ships a prior catalog), OR pre-build Addressables once via `AddressableAssetSettings.BuildPlayerContent()` in the editor and commit `ServerData`/`aa`.
2. **Fix the failing group:** run `Addressables → Build → New Build → Default Build Script` in the OPEN editor to surface the real per-asset SBP exception (batchmode swallows it), then repair the offending asset/group (often a missing/duplicate address or a script-less asset).
3. **Last resort:** strip the Localization Addressables groups if Localization isn't shipping in this build.

## Acceptance
- [ ] `DesktopBuild.BuildWindows` produces `Builds/Windows/DefendersOfTheRealm.exe` + `_Data/level0`
- [ ] `build-webgl.ps1` produces a loadable WebGL build
- [ ] Real diagnosis confirmed by an interactive Addressables build (not batchmode)
- [ ] No regression to runtime catalog/localization loading

## Environment note
Diagnosed with the editor CLOSED (bake-safe). The interactive Addressables build (option 2) needs the editor OPEN — do that as a focused editor session, then close before re-running batchmode.
