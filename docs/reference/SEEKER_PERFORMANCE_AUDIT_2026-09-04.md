# Seeker Performance Audit - 2026-09-04 (read-only, captured data first)

Owner directive 2026-09-04: "I want a deep performance audit for the seeker phone."
Scope: device logcat under `logs/device/` (09-04 `freeze-20260904-095249.log` 138 MB and
`full-buffer-094110.log` 126 MB; the 08-18..20 set; 08-26/27 `logs/f8-inbox/device-stage/logcat-full.txt`),
F8 captures seq 4674-4680, then source. Nothing edited, Unity not launched, no adb writes; the APK was
inspected by reading its zip directory only.

Evidence classes: MEASURED (a captured line or a file read this session) / CONFIGURED (an asset or
source line read this session) / HEARSAY (a number from a doc, not re-measured - never used for ranking).

## A. Captured evidence

| # | Metric | Value | Exact line | Date |
|---|---|---|---|---|
| A1 | Build under test | Unity 6000.4.8f1, IL2CPP arm64, stripping on | `freeze-20260904-095249.log:372856` `Version '6000.4.8f1 (f8b72d3d7343)', Build type 'Release', Scripting Backend 'il2cpp', CPU 'arm64-v8a'` | 09-04 |
| A2 | Device | Seeker, 8 cores (4 big / 4 little), 7478 MB | `:372853-372854` `Cores = 8, Memory = 7478mb`; `4 big (mask: 0xf0), 4 little (mask: 0xf)` | 09-04 |
| A3 | Tier / frame target applied | Seeker_High, vsync 0, target 60 | `:373699` `[SeekerBootstrap] ... tier='Seeker_High', vSyncCount=0, targetFrameRate=60.` (same on 08-18 and 08-27) | all |
| A4 | Town fps today (game's 4 s sampler) | avg 29.9, min 0, max 58, n=276 | every `[Flow:Perf] ... scene=Main_Castle_Overworld` line; e.g. `:1229223` `LOW fps=29 ms=34.3 mem=506MB gc=25MB ... towers=0 enemies=9` | 09-04 |
| A5 | Town fps by enemy count today | 0 -> 29.3 (n=79); 1-4 -> 25.0 (n=13); 5-8 -> 30.4 (n=71); 9+ -> 30.7 (n=113) | same sampler, bucketed | 09-04 |
| A6 | Town fps 08-18..20 | avg 41.6 (n=4359); 0 enemies -> 40.7 (n=1033); 9+ -> 35.3 | `2026-08-20-town-freeze.log` `[Flow:Perf] fps=40 ms=24.7 mem=376MB` | 08-18..20 |
| A7 | Town fps 08-26/27 | 0 enemies -> 35.8 (n=176) | `logcat-full.txt:33912` | 08-26 |
| A8 | Title / HeroSelect fps | Title 53.9 avg (57-60 steady); HeroSelect 49-53 | `:385249` `fps=49 ms=20.3 ... scene=HeroSelect` | 09-04 |
| A9 | Dungeon fps | dg_folks_granary avg 24.8, min 15, 13 enemies | `full-buffer-094110.log:1104466` `LOW fps=23 ms=42.7 ... scene=dg_folks_granary ... enemies=13` | 09-04 |
| A10 | Town frame time vs live VFX loops (in-game gate) | baseline 20.7 ms at <=3 loops (n=3238); 8-11 loops -> 34.8 ms (n=11428 of 17405 frames); slope 1.179 ms/loop | `freeze...log` 09:52:48.565 `[Flow:VfxPerfGate] ... tier=village ceiling 24: occ 0-3: mean 20.7ms ... occ 8-11: mean 34.8ms ... MEASURED SLOPE 1.179ms per live loop ... Shed level None (ambient ring 8/8, enemy/pet ring 8/8). Hitches 313` | 09-04 |
| A11 | Dungeon frame time vs loops | baseline 22.9 ms (n=15139); 16-19 loops -> 39.9 ms; slope 0.279 | `full-buffer...log` 09:40:26.002 `[Flow:VfxPerfGate] ... tier=dungeon ceiling 48 ...` | 09-04 |
| A12 | Hitches (frames over 16.7 ms) | n=297; p50 66.3 ms, p90 149.2 ms | `:382162` `[Flow:VfxPerfGate] HITCH 149.2ms ... at loop occupancy 0/24 ... a stall from outside the VFX pool` | 09-04 |
| A13 | Boot frame | ~4.5 s first frame at Title, every launch (x3) | `:377352` `LOW fps=0 ms=4522.2 mem=300MB gc=11MB scene=Title` | 09-04 |
| A14 | Boot warm routines (coroutine spans) | StructureAssets 7214.9 ms; EnemyAssets 3086.1; OfflineContent 1510.2; Wallet 2848.4 | `:384382` `[Flow:StructureAssets] <- WarmRoutine (7214.9ms)`; `:379277` `[Flow:EnemyAssets] <- WarmRoutine (3086.1ms)` | 09-04 |
| A15 | Ad-return stall | 2746.7 ms frame after the LevelPlay activity closed | `:422879` `LOW fps=0 ms=2746.7`; preceded by `ControllerActivity => UnityPlayerGameActivity` at 07:46:24.220 | 09-04 |
| A16 | Town-load log burst | 13,956 Unity logcat lines in ONE second | second 09:44:44: 13052 I/Unity + 904 W/Unity (top tags `[Flow:VisualFactory]` 233, `[Flow:Structure]` 185, `[Flow:Cosmetics]` 126) | 09-04 |
| A17 | Steady logging rate (pid 30184, 549 s) | 257 logcat lines/s; 35 Debug.Log calls/s, each with a script stack trace | 141,263 `Unity (30184)` lines; 19,224 `DebugLogHandler:Internal_Log` frames | 09-04 |
| A18 | Memory (Unity allocator / managed) | 300 -> 507 MB total; gc 11 -> 34 MB | `mem=507MB`, `gc=34MB` max over log | 09-04 |
| A19 | Low-memory kills of the game | none (all 8 kill lines are installs / task removal) | `:608251` `Killing 22805:...echoesofelarion (adj 905): remove task` | 09-03/04 |
| A20 | Thermal | Android thermal status 3 (SEVERE) while foreground, x4 | `:411404` `07:43:12.792 D/BrightnessThermalClamper: New thermal throttling status = 3` (session 22805 ran 07:42:37-07:52:16); also 07:47:12, 07:47:24, 09:33:53 | 09-04 |
| A21 | Battery | 27 %, not charging, at session start | `:340825` `Battery changed: level: 27% status: Not charging` | 09-04 |
| A22 | Choreographer skips by the game pid | 1 line | `:410467` `I/Choreographer(22805): Skipped 30 frames!` (the million-frame skips are other pids) | 09-04 |
| A23 | CDN failures | 0 `RemoteProviderException`, 0 `HTTP/1.1 404` in every log inspected | counts | all |
| A24 | Lazy family bundle at first spawn | capsules spawn first, bundle fetched after; x3 "placeholder capsule outlived the grace and is now VISIBLE" | `:426551` `[Flow:Enemy] model 'Orc_Warrior' ... family 'Orc' bundle is NOT YET DOWNLOADED (familyDownloading=False, catalogState=Ready). Spawning the tinted capsule NOW` | 09-04 |
| A25 | Sync Addressables on main thread | present 08-18/20 (`WaitForCompletionHandler()` frames), absent 09-04 (content cached) | `2026-08-20-town-freeze.log:15379`, `:4575748` | 08-18/20 |
| A26 | Fractional timeScale | all combat feel holds, none stuck | `:444169` `WorldHold ACQUIRE 'fx:combat-dip' -> timeScale 0.05`; `:446040` `'fx:hit-stop' @ 0.04` | 09-04 |
| A27 | Stuck timeScale=0 | pause-menu hold overran 327 s and 8444 s (watchdog force-released) | `capture-device-20260903-190823-seq4679.md` `STUCK WORLD HOLD: 'pause-menu' (scale 0.00) ... 507.3s, past its 180.0s ceiling`; seq4676 `8444s` | 09-03/04 |
| A28 | APK on disk now | 483,366,578 bytes (461 MB); AAB 472,637,397 | `Builds/Android/DefendersOfTheRealm.apk` (09-04 12:55); `Builds/overnight-apk-status.txt` `APK_OK ... size=461MB` | 09-04 |
| A29 | libil2cpp.so | 81.93 MB uncompressed / 25.8 MB in-APK; libunity 23.5 MB; global-metadata 23.4 MB | APK zip directory | 09-04 |
| A30 | Build texture payload | 736.9 MB uncompressed = 81.6 % of 902.8 MB user assets; complete build 2.3 GB | `Builds/apk-build.log:27256,27264,27265` | 09-04 |
| A31 | Uncompressed UI sprites in the build | 22 entries at exactly 6.0 MiB, all `Assets/Resources/UI/ElarionMedieval/*` (1774x887 x 4 B = RGBA32) | `apk-build.log` rows e.g. `6.0 mb 0.3% .../frames/card-frame-empty.png` | 09-04 |
| A32 | Two 16 MiB particle sheets | 4096x4096, no Android override | `Mirza Beig/.../tex_vfx-ult_particle_spritesheet_smoke.png` + smokePuffs; `.meta:104-109` `maxTextureSize: 4096 textureFormat: -1` | 09-04 |
| A33 | Textures on disk under Assets/ | 7,351 files, 15,320.9 MB (12,383.6 MB is `Assets/Blink`, largely not in the build) | PowerShell sum by extension | 09-04 |
| A34 | Local bundles in APK | 7 bundles, 18.1 MB (gear 14.7, dungeon 3.3); all enemy/structure/hero art remote | APK zip `assets/aa/*.bundle` | 09-04 |

## B. What the device is actually doing (from A only)

- FRAME BUDGET. Title reaches the 60 target (A8). The town does not: ~30 fps today (A4), FLAT across
  enemy counts (A5) - the ceiling is not enemy AI. The game's own gate measured the town baseline at
  20.7 ms with almost no VFX (a 48 fps ceiling before any effect) and 34.8 ms in the state the town is
  in two-thirds of the time (8-11 live loops), 1.18 ms per loop (A10). Dungeon: 22.9 ms baseline, 39.9 ms
  at 16-19 loops (A11). Two measured budget-eaters: a ~21 ms scene baseline and ~14 ms of always-on
  looping VFX. What the 21 ms IS (CPU main / render thread / GPU) is NOT PROVEN - no split in any capture.
- REGRESSION OVER TIME. Same sampler, tier, device, 0 enemies: 40.7 (08-18..20) -> 35.8 (08-26/27) -> 29.3
  (09-04). Which commit range cost each step is not proven (build ids not in the grep window).
- HITCHES. 297 over-budget frames today, p50 66 ms, p90 149 ms (A12); a ~4.5 s first frame every launch
  (A13); 2.7 s on returning from an ad (A15). StructureAssets warm spans 7.2 s (A14) - a coroutine span,
  so its main-thread share beyond the 4.5 s frame is not proven.
- LOGGING AS LOAD. 257 logcat lines/s with 35 stack-traced Debug.Log calls/s in steady play (A17), 13,956
  lines in the town-load second (A16). Each call captures a managed stack trace and crosses JNI. Its share
  of the 21 ms baseline was not measured in isolation.
- TIMESCALE. Fractional values are combat feel holds behaving as designed (A26). The stuck `pause-menu`
  hold (327 s / 8444 s overruns, A27) is a freeze class, not a perf drop; the watchdog releases it.
- MEMORY. No pressure observed: 507 MB Unity-allocated on 7.5 GB, 34 MB managed heap, zero LMK kills
  (A18/A19). PSS and graphics memory NOT captured.
- LOAD / CDN. No 404s or provider exceptions (A23) - the 08-20 capsule storm is fixed. Family bundles are
  still fetched lazily at first spawn, so capsules are visible on the first encounter per family (A24).
  Sync `WaitForCompletion` was on the main thread on 08-18/20 and absent 09-04 only because content was
  cached (A25).
- THERMAL. Status 3 (severe) four times while foreground (A20), at 27 % battery unplugged (A21). Whether
  clocks were cut is not captured. Nothing in the game reacts to it (C).

## C. Configuration audit (source)

| Setting | Value | Where | Mobile-appropriate? |
|---|---|---|---|
| Scripting backend / arch | IL2CPP, ARM64 only | `ProjectSettings.asset:884-885`, `:269` | Yes |
| IL2CPP codegen | OptimizeSize | `:888-889`; `Assets/Editor/MobileSettings.cs:200-206` | Yes |
| Managed stripping | Low at build time (Medium breaks the Solana SDK BouncyCastle, WO-766); `link.xml` 22 x `preserve="all"` | `MobileSettings.cs:226-247` | Size only (libil2cpp 81.9 MB); not a frame-rate lever |
| Engine code stripping / incremental GC / Swappy / MT rendering | on / on / on / on | `:183`, `:899`, `:73`, `:55,586-587` | Yes |
| Sustained performance mode | OFF | `:10` `AndroidEnableSustainedPerformanceMode: 0` | Debatable given A20 - worth testing ON |
| Batching (Android) | static on, dynamic off | `:566-568` | Yes (SRP batcher) |
| Graphics API (Android) | no Android entry -> Automatic | `:575-578` | NOT PROVEN which API the Seeker runs |
| Stack traces on every log type | ScriptOnly for all 5 | `:59` `m_StackTraceTypes: 0100...` | NO for a release build at 35 logs/s |
| FlowTrace master switch in the SHIPPED build | ON | `Assets/_Modules/Core/Diagnostics/FlowTrace.cs:46` `Enabled = true`; `:365-369` "a remote flag/config service ... does NOT exist yet" | NO - s12 says flag OFF once stable; the calls stay |
| PerfReporter | samples every 4 s; scans `FindObjectsByType<MonoBehaviour>` each time | `Core/Diagnostics/PerfReporter.cs:51, ~208` | Minor spike; it is also the only sampler you have |
| Quality tier Android default | index 1 = Seeker_High | `QualitySettings.asset:169-170` | Yes |
| Seeker_High | shadows 2 (soft), res 1, dist 30, cascades 1, AA 2, vsync 0, lodBias 0.4, particleRaycastBudget 16 | `QualitySettings.asset:63-104` | Soft shadows + MSAA 2 are the tunable knobs at 30 fps |
| Runtime target | `targetFrameRate=60`, vsync 0 | `Core/SeekerBootstrap.cs:111-114` | Yes; unreachable in town |
| URP HDR / MSAA / renderScale | 0 / 2 / 1.0 | `Assets/Settings/DeNelle-URP.asset:26,28,29` | renderScale 1.0 at 2670x1200 is the cheapest untried lever |
| URP main light shadows | 1024, 30 m, 1 cascade, soft | `:45-46,57-58,66,69` | Acceptable |
| URP additional lights | per-pixel, 4/object, no shadows | `:47-49` | A cost; `vfx.maxParticleLights` = 4 bounds particle lights (`RemoteTunables.cs:290`) |
| URP depth texture | REQUIRED; renderer `m_CopyDepthMode: 1` | `:22`; `DeNelle-UniversalRenderer.asset:52` | Extra depth copy per frame; consumer not traced |
| URP Adaptive Performance | `m_UseAdaptivePerformance: 1` | `:79` | INERT - only the built-in module is in `Packages/manifest.json:37`; no provider package, so thermal 3 triggers nothing |
| Renderer intermediate texture | ALWAYS (1) | `DeNelle-UniversalRenderer.asset:56` | NO. `MobileSettings.cs:537-538` sets it to 0 on the PIPELINE asset where the property does not exist (0 hits in `DeNelle-URP.asset`) - the intended Auto never applied |
| Renderer features / mode | none, Forward, depth priming off | renderer `:27,50,51` | Yes |
| VFX pools | oneshots 40, loops 20 serialized | `Village/Vfx/VFXManager.cs:184,187` | Runtime gate reports village ceiling 24 / dungeon 48 - source of the override NOT READ; discrepancy, not proven |
| Addressables | R2 remote, `m_DisableCatalogUpdateOnStart: 0` | `AddressableAssetSettings.asset:96,20,22` | A network catalog check every launch, inside the 4.5 s |
| Addressables concurrency | `assets.maxConcurrentRequests` default 0 = unbounded off-Pi | `RemoteTunables.cs:596-598, 611` | Unbounded multi-MB downloads during first spawn |
| Sync Addressables call sites | `WaitForCompletion()` | `Core/Addressables/HeroAssetLoader.cs:95`, `HeroTextureLoader.cs:79`, `AudioAssetLoader.cs:158`, `EnemyEditorSyncResolver.cs:73,88` | Any uncached remote hit blocks the main thread (proven 08-18, A25) |
| Prewarm | enemy/structure warm at boot (A14); no family prewarm before first spawn (A24) | `EnemyContentWarmer.cs`, `StructureContentWarmer.cs` | Known PROD-009/010 gap |
| Android texture default format | `m_BuildTargetDefaultTextureCompressionFormat: []` | `ProjectSettings.asset:596` | Format not proven; OUTCOME proven: NPOT UI sprites ship RGBA32 (A31) |
| The 6 MiB sprites' import | `textureFormat: -1`, no Android override, NPOT | `Resources/UI/ElarionMedieval/frames/card-frame-empty.png.meta:97-102` | NO - 22 x 6 MiB = 132 MiB RGBA32 UI in build and GPU memory |
| Hero mesh | 50k decimation reverted `e07e1b860` | git | HEARSAY: Ranger 314,892 tris vs Knight 9,808 (`docs/MESH_DECIMATION_PROCESS.md:35-41`) |

## D. Top 10 risks, ranked (evidence class; cheapest proof)

1. Town baseline ~21 ms with no VFX (MEASURED A10). 60 fps unreachable before an effect plays.
   Proof: `adb shell dumpsys gfxinfo com.denellestudios.echoesofelarion framestats` + one Unity Profiler
   USB capture of the town at 0 enemies to split CPU main / render / GPU.
2. Always-on looping VFX: 8-11 loops live 66 % of town frames at 1.18 ms each, ~14 ms (MEASURED A10;
   "Shed level None, ambient ring 8/8"). Proof: ambient ring to 0 via the existing shed path / `vfx.*`
   tunables for one session; re-read the same VfxPerfGate summary line.
3. Release build logs 257 lines/s with stack traces (MEASURED A17; CONFIGURED `FlowTrace.cs:46`,
   `ProjectSettings.asset:59`). Proof: one session with `FlowTrace.Enabled=false`; read fps from
   `dumpsys gfxinfo` (PerfReporter goes silent when Enabled is false, `PerfReporter.cs:113`).
4. Town fps regressed 40.7 -> 35.8 -> 29.3 across three builds at 0 enemies (MEASURED A5-A7).
   Proof: bisect with the APKs on disk (`Builds/Android/prev-*.apk`, `Distribution/(Solana)/...`, current)
   via `install-apk-to-seeker.ps1 -Build:$false`, 5 min of `[Flow:Perf]` each.
5. Thermal status 3 during play with no in-game response (MEASURED A20; CONFIGURED: no Adaptive
   Performance provider, sustained mode off). Proof: `adb shell dumpsys thermalservice` +
   `/sys/class/thermal/thermal_zone*/temp` sampled beside a 10-min `[Flow:Perf]` run.
6. Intermediate texture forced Always (CONFIGURED `DeNelle-UniversalRenderer.asset:56`; `MobileSettings.cs:537`
   targets the wrong object). A full-screen intermediate at 2670x1200 every frame. Proof: flip to Auto,
   rebuild, compare A10's baseline.
7. 132 MiB of RGBA32 UI sprites + 736.9 MB texture payload (MEASURED A30/A31). Proof: editor inspector
   with Android target on one `.meta`; or `dumpsys meminfo` Graphics row with a panel open.
8. 4.5 s first frame every launch + 7.2 s structure warm (MEASURED A13/A14). Proof: Profiler on boot.
9. Sync `WaitForCompletion` on uncached remote content (CONFIGURED 5 sites; MEASURED 08-18). Proof:
   fresh install via the sanctioned script, grep fresh logcat for `WaitForCompletionHandler`.
10. Unbounded parallel bundle downloads at first spawn (CONFIGURED `RemoteTunables.cs:596-598`; capsules
    visible past grace A24). Proof: `assets.maxConcurrentRequests` = 2, compare the "outlived" count.

Hearsay NOT used for ranking: "98.9 MB textures" (`MESH_DECIMATION_PROCESS.md:277`) matches neither the
736.9 MB build figure nor the 15.3 GB on-disk figure; "libil2cpp 21.42 MiB" matches neither 81.9 MB
uncompressed nor 25.8 MB compressed; "572 MB APK on 08-08" - current is 461 MB, nearest artefact on disk
`prev-577mb.apk` = 605,662,133 bytes (08-17).

## E. Not provable from this machine - and exactly what would prove it

- CPU vs GPU split of the 21 ms baseline -> Unity Profiler over USB (Development Build + Autoconnect),
  60 s in town at 0 enemies; or `dumpsys gfxinfo ... framestats`.
- Which graphics API the Seeker runs (Vulkan vs GLES3) -> the gfx-init banner is in no capture (logcat ring
  evicted the boot window every time). `adb logcat -d | grep -i "Vulkan\|OpenGL ES"` within seconds of a
  cold launch, or `adb shell dumpsys SurfaceFlinger | grep -i "GLES\|Vulkan"`.
- Real process memory (PSS / graphics / native) -> `adb shell dumpsys meminfo com.denellestudios.echoesofelarion`.
- Whether thermal 3 cut clocks -> `dumpsys thermalservice`, `scaling_cur_freq`, GPU freq node, beside `[Flow:Perf]`.
- Actual texture format for the NPOT sprites and the 4096 sheets (ASTC 4x4 at 4096^2 is also 16 MiB) ->
  editor inspector with Android target, or `dumpsys meminfo` graphics row before/after opening Manage.
- Which commit range caused each fps step -> bisect (D4).
- Whether the 7.2 s / 3.1 s warm routines block the main thread beyond the 4.5 s frame -> Profiler on boot.
- Runtime source of the `village ceiling 24` / `dungeon ceiling 48` loop caps (serialized 20) -> grep the
  tier ceiling in `VFXManager.cs` / its perf gate.
- Hero triangle counts in the shipped build after the revert -> Profiler Rendering module with the Ranger equipped.

## Owner-facing summary

The phone is not memory-bound and the CDN is healthy. It is FRAME-TIME-bound in town by two measured
things - a ~21 ms scene baseline nobody has split into CPU/GPU yet, and ~14 ms of ambient VFX loops
that never shed - on top of a release build that logs 257 lines a second with stack traces and a
renderer forced to an intermediate texture by a setting applied to the wrong asset. It has lost ~11 fps
at idle since 08-20 across three builds. Every item above names the one capture that would settle it.
