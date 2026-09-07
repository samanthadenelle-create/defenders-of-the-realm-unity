# WORK ORDER 1365 - The AAB has no ship chain: no wrapper, no R2 push, no size guard

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:00:41, build 2026.09.07.359076). PRIOR STATUS: FIXED - implemented in da9694c86 (2026-09-04), on the Seeker in build 2026.09.05.355872; RCA re-verified 2026-09-04 (see the appended block). Awaiting owner felt-test: none on the device - this is a build chain; the machine evidence is google-play-aab-build.ps1 (repo root) emitting AAB_SIZE_OK (Builds/aab-status.txt: AAB_SIZE_OK 469202267) and calling tools/r2-ship.ps1 at :316. Gap: no captured RED for "no fresh R2 push FAILS".
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
**31 MB appeared in two days with every marker green, because no marker measures size.**

### ⭐ NO LONGER AN ESTIMATE - MEASURED 2026-09-04 WITH bundletool

```
$ java -jar bundletool-all-1.17.2.jar build-apks --bundle=EchoesOfElarion-GooglePlay.aab        --output=aab-size-measure.apks --mode=default
$ java -jar bundletool-all-1.17.2.jar get-size total --apks=aab-size-measure.apks
MIN,MAX
510443276,510523099
```

**510,443,276 - 510,523,099 bytes against a 500,000,000-byte ceiling = OVER BY ~10.5 MB.**
This closes the estimate/ratio argument: the RC's AAB-to-download ratio was accurate, and the
artifact on disk cannot be uploaded.

⭐ **AND THE TOOLING WAS ALREADY ON THIS MACHINE** - no install needed, which is why item 1 of THE
WORK is cheap:
- `<UnityEditor>/Data/PlaybackEngines/AndroidPlayer/Tools/bundletool-all-1.17.2.jar`
- `<UnityEditor>/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe` (OpenJDK 17.0.18)

⚠ `java` is **NOT on PATH** - the wrapper must invoke Unity's bundled JDK by full path, and must
resolve the editor root rather than hardcoding a version (the pinned editor is `6000.4.8f1`; a
hardcoded path breaks on the next upgrade, and the repo root is machine-dependent per CLAUDE.md §0).
⚠ `build-apks` writes a ~1.4 GB `.apks` intermediate. **Delete it after measuring** - three stale
ones already sit in `Builds/Android/` (`EchoesOfElarion-GooglePlay.apks` and `-policy.apks`, 1.49 GB
each, dated 08-30). The size guard must clean up after itself or it becomes its own disk problem.

## ⭐ MEASURED 2026-09-04 - WHERE THE BYTES ACTUALLY ARE. READ BEFORE PROPOSING PAD.

Measured directly off `Builds/Android/EchoesOfElarion-GooglePlay.aab` with `zipfile` (compressed
sizes - the only ones that count against the ceiling):

| Compressed | Category |
|---|---|
| **418.40 MiB** | `base/assets/bin/Data` serialized + resource - **scenes + `Assets/Resources/`** |
|  33.93 MiB | native `.so` (`libil2cpp.so` 21.42 · `libunity.so` 10.38) |
|  **15.87 MiB** | `base/assets/aa/` - **ALL local Addressables** (gear 12.61, dungeon 3.12, rest ~0) |
|   8.33 MiB | all three `.dex` |
|   5.54 MiB | `bin/Data/Managed` (`global-metadata.dat` 5.40) |

⛔ **THE CDN IS WORKING, AND IT IS NOT THE ANSWER TO THIS TICKET.** Addressables ships only
**15.87 MiB** locally - `Enemy_Art` and `Structure_Art` really are remote (§16). But the remote
groups only ever covered ~84 MiB of art. **The 418 MiB is scenes + `Assets/Resources/`, which
Addressables never touches and which is compiled into the player by construction**
(`LocalJsonCatalogSource.Read` resolves `Resources.Load` FIRST on every platform;
`Assets/Resources/` ships unconditionally).

**`Assets/Resources` measured on disk: 388.4 MiB**, and it is dominated by UI art:

| On disk | Folder |
|---|---|
| 96.58 MiB | `RpgUi` |
| 89.70 MiB | `VFX` |
| 69.81 MiB | `UI` |
| 25.32 MiB | `Portraits` |
| 21.25 MiB | `Heroes` |
| 20.95 MiB | `HudIcons` |
| 15.34 MiB | `ItemIcons` |

**RpgUi + UI + HudIcons + ItemIcons + Portraits ~ 228 MiB of UI/icon textures.**
⚠ Those are SOURCE bytes; what lands in `bin/Data` depends on import settings - **which is exactly
the lever**. The RC doc credits its 20.6 MB margin to *"a conservative Android texture pass, 65
eligible overrides"*, and 31 MB reappeared within two days. ⭐ **So item 2 below is very likely
"the texture pass was lost, or new art landed in `Resources/` after it" - a settings problem, not
an architecture problem. Establish that before anyone proposes PAD.**

⚠ `Resources/VFX` is **89.70 MiB**. Canon records ~23.85 MB deliberately mirrored to
`Assets/Resources/VFX/_Shared/` (the 2026-08-06 gitignored-art fix). It is now roughly four times
that. **Worth an explicit look** - that mirror was a correctness fix, not a licence to grow.

### ⭐ THE AUTHORITATIVE PLAY LIMITS - fetched live 2026-09-04, cited

⛔ **`docs/GOOGLE_PLAY_READINESS_AUDIT_2026-08-30.md`'s headline "200 MB, THE CONSOLE REFUSES THE
UPLOAD" IS STALE.** The current base-module ceiling is **500 MB**. Any 150 MB / 200 MB figure - from
a blog, StackOverflow, an LLM, or our own audit - is dead. Do not act on one.

| Limit | Value | Source |
|---|---|---|
| **Base module compressed download** | **500 MB** | `support.google.com/googleplay/android-developer/answer/9859372`; `developer.android.com/guide/app-bundle/faq` (page updated 2026-07-28) |
| Total download for ONE device (base + config APKs) | 4 GB | `developer.android.com/guide/app-bundle` |
| Individual feature module | 500 MB | support 9859372 |
| Individual asset pack (any delivery mode) | 1.5 GB | support 9859372 - Google publishes ONE per-pack cap, not per-mode |
| All modules + install-time asset packs | 4 GB | support 9859372 |
| On-demand / fast-follow packs, cumulative | 30 GB | support 9859372 |
| Total app | 34 GB | support 9859372 |
| Max asset packs per bundle | 100 | support 9859372 |
| User-facing "large app" warning | 200 MB | **non-blocking dialog on mobile data - NOT a ceiling** |

- **Enforced at UPLOAD, by Play Console**, not at review: *"as calculated by Play Console upon
  uploading your app bundle"* + *"If the Play Console finds any of the possible downloads ... to be
  more than the maximum size limits, you will get an error."*
- ⚠ **`bundletool` is "a similar (but not identical) calculation"** to the Console's, in Google's own
  words. Treat our measurement as a close estimate, never as the Console's verdict.
- ⚠ **MIN vs MAX: NOT DOCUMENTED.** *"any of the possible downloads"* implies the worst case, so
  **treat MAX as binding**. Stated as the inference it is.
- minSdk only affects size rules **above 1 GB** (API 21+ required there). Irrelevant to us at
  minSdk 26.
- If Texture Compression Format Targeting is ever adopted, **the limits apply separately per
  texture format** - relevant if WO-1367's ASTC pass ever grows a second format.

### ⛔ THE MB-vs-MiB AMBIGUITY IS REAL, WORTH ~14 MB, AND DELIBERATELY NOT RESOLVED

**No Google page defines whether "500MB" is 500,000,000 or 524,288,000 bytes.** Neither the Console
help page nor any `developer.android.com` page writes "MiB", "mebibyte", or a byte count. Searched
and reported **NOT FOUND** rather than inferred - an inferred ceiling is the expensive kind of wrong.

| Reading | Ceiling | Our 510,523,099 B | Verdict |
|---|---|---|---|
| 500 MB decimal | 500,000,000 | +10.5 MB | **OVER** |
| 500 MiB binary | 524,288,000 | -13.8 MB | UNDER |

⭐ **DO NOT TRY TO SETTLE THIS, AND DO NOT SHIP ON THE FAVOURABLE READING.** WO-1367's texture pass
projects ~26.9 MB of headroom, which clears **both** readings with margin. Engineering to the
generous interpretation of an undocumented unit is a guess with a submission attached.
*(If it ever must be settled empirically: an internal-testing-track upload triggers the check at
upload and the Console names the exact overage. That is an outward-facing action and needs the
owner's word.)*

### MEASURED: base module == whole app, so splits will not save us

`get-size total --modules=base` and `get-size total` return the **identical** MIN,MAX
(510,443,276 / 510,523,099) because `BundleConfig.pb` declares modules `{base}` only - there are no
asset packs. And MIN/MAX differ by just **79,823 bytes**, because our payload lives in `assets/`,
which is **never split by density or language**. ⛔ **Conclusion: device-configuration splitting has
essentially nothing to give here. The bytes have to actually leave the artifact.**

### ⛔ TWO CODE-SIZE LEVERS - both smaller than they look. Do not chase these first.

**R8 / ProGuard is switched OFF entirely** (`Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build.gradle:68`
`minifyEnabled false`; `ProjectSettings/ProjectSettings.asset:292` `AndroidMinifyRelease: 0`;
`:268` `useCustomProguardFile: 0`). Enabling it is a genuine open question nobody had raised before
2026-09-04. **But the arithmetic caps it: all three DEX files total 8.33 MiB compressed**, so
deleting 100% of our Java bytecode saves less than the gap. Realistic 20-40% trim = **1.7-3.3 MiB**,
against real risk (IL2CPP/JNI and ad-SDK reflection are invisible to R8; Unity ships
`proguard-unity.txt` keeps precisely because of it). ⛔ **And R8 is USELESS for WO-1363** - it shrinks
Java bytecode into `classes*.dex`; our SKR literals live in `global-metadata.dat`, produced by
IL2CPP from C# and packaged as an ASSET. R8 never opens it.

**Managed stripping is ALREADY at Medium and deliberately defanged.**
`Assets/Editor/MobileSettings.cs:216-222` raises Android managed stripping Low -> Medium, and
`Assets/link.xml` carries `preserve="all"` on **every** runtime assembly - because Newtonsoft
deserialises every catalog by reflection and the cross-asmdef bridges resolve by name (183 files
under `Assets/_Modules` use reflection APIs; **zero** `[Preserve]` attributes). That preserve list is
load-bearing and correct: narrowing it produces the classic works-in-editor / silently-empty-in-build
failure. **This is why `libil2cpp.so` is 21.42 MiB and why there is little left to strip.**
⚠ `SESSION_CANON_LOADER.md:477` says *"Android stripping is at Low"* - **STALE**, corrected here.
⚠ `ProjectSettings.asset:891-893` `managedStrippingLevel:` lists only `WebGL: 4` because
`MobileSettings` applies Android at build time via the PlayerSettings API, not as persisted state -
**do not read the absence of an Android row as "unset"**.

### HONEST RANKING

| Lever | Realistic saving | Risk |
|---|---|---|
| **Android texture import pass over `Resources/`** | **10-40 MB** | Low - needs a visual check, owner's eyes |
| Enable R8 | 1.7-3.3 MiB | Moderate - JNI / ad-SDK reflection |
| Narrow the `link.xml` preserve list | small | ⛔ High - silent build-only breakage |
| Play Asset Delivery | large | ⛔ High - moves 423.94 MiB of `bin/Data` out of the base module and collides with our custom R2 remote `LoadPath`, which has NO local fallback |

**The texture pass alone plausibly closes the gap. Prove that before committing to PAD.**

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

- [x] ⛔ **RE-WORDED 2026-09-04 — the original criterion became UNFULFILLABLE while this ticket was
      open, and that is worth recording rather than quietly editing.** It asked to prove RED against
      "the current 514,062,537-byte artifact". **WO-1367's texture pass landed first**, so the artifact
      on disk is now 472,637,397 bytes measuring **469,202,267** — i.e. **30.8 MB UNDER** the ceiling.
      There is no oversized artifact left to fail against.
      **RED was therefore proven by LOWERING THE CEILING**, which tests the same code path:
      ```
      -MeasureOnly -SizeCeilingBytes 460000000  ->  AAB_SIZE_FAIL 469202267 (9202267 OVER 460000000)  exit 6
      -MeasureOnly                              ->  AAB_SIZE_OK   469202267 (30797733 under 500000000) exit 0
      ```
      **Both runs verified by the lead directly, not taken on the agent's word.** The `.apks`
      intermediate was removed by the script's own `finally` in both cases.
      ⭐ **And the exit codes are RIGHT** — 6 on failure, 0 on success. This repo's standing hazard is
      runners that exit 0 on refusals and FAILs; this one does not, so the marker AND the exit agree.
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

---
## RCA re-verified 2026-09-04 (QA read-only pass)
**Verdict:** SUPERSEDED
**Evidence:**
- Commit `da9694c86 2026-09-04 "WO-1365: the AAB finally has a ship chain..."` is an ancestor of HEAD (`git merge-base --is-ancestor`). Stat: `google-play-aab-build.ps1` (+350, NEW, at REPO ROOT - not under `tools/`, which is why a tools/ search misses it), `Assets/Editor/AndroidBuild.cs` (+4/-4), `docs/CLI_OPERATIONS_RUNBOOK.md` (+23), this WO (+16).
- `google-play-aab-build.ps1:270` `-ExpectMarker '[AndroidBuild] SUCCEEDED'`; `:316` `& powershell -NoProfile -File (Join-Path $root 'tools\r2-ship.ps1')` (the ONE-file call, not re-inlined); `:223-233` `AAB_SIZE_FAIL` / `AAB_SIZE_OK`; `:243` `exit 6`; `:62` `SizeCeilingBytes = 500000000`; signing preflight `:106-132`.
- `docs/CLI_OPERATIONS_RUNBOOK.md:189-190` (build-table rows), `:196-209` (marker list).
- `Builds/aab-status.txt` (Sep 4 13:12): `AAB_SIZE_OK 469202267 (30797733 under 500000000)`, `AAB_SIZE_TOOLS editor=6000.4.8f1`.
- `Assets/Editor/AndroidBuild.cs:10` and `:21` now read `6000.4.8f1` (the stale-comment item above is done). Markers still at `:196` PLAY_ARTIFACT_REJECTED, `:201` SUCCEEDED, `:206` FAILED, `:272` ANDROID_CATALOG_MISSING.
- `Builds/r2-parity.log` (Sep 4 22:21) tail: `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271`.
- No regression suite pins the size guard (`grep -i size Assets/Editor/Regression/GooglePlayPackagingRegression.cs` = 0 hits); it is a PS1 judged by marker.
- WO-1362 (untracked, dated 09-03, RECON COMPLETE) is the programme parent; its Wave 0 "size guard" and Wave 2 "catalog parity for the AAB" rows ARE this ticket. 1362 does not supersede 1365; 1365 executes 1362's rows.
**What changed since the RCA:** the whole ticket landed in `da9694c86`, but this WO's `**Status:**` line was never flipped (CLAUDE.md s2 requires the flip in the same commit). Still open, unproven: (a) "AAB with no fresh R2 push FAILS on the marker" is wired at `:316` but no captured RED run exists; (b) "31 MB accounted for" is WO-1367's lane (IN PROGRESS, acceptance unchecked); (c) "was the 09-01 catalog pushed?" is not answered anywhere in the tree.
**Ready for a lane?** no - implementation is done; what remains is the Status flip + a canon fix. Files a lane would touch: this WO (Status line), `KEY_FACTS.md:158` (still says "THE AAB LANE HAS NO SHIP CHAIN" - stale, s15).
**Pins/rulings needed:** none for the flip. The 09-01 catalog question stays a recorded unknown.
