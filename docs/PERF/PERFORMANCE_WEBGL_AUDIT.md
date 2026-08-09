# Performance & WebGL / Pi-Browser Viability Audit

**Date:** 2026-06-28
**Scope:** this repo (Echoes of Elarion / Defenders of the Realm)
**Author:** CLI audit agent (read-only; no code changed)
**Method:** static scan of `Assets/_Modules`, `ProjectSettings`, Addressables config + asset-folder weighing, cross-checked against Unity WebGL/mobile best-practice docs (URLs cited at bottom).

> **Headline:** the C# runtime hot-path situation is **healthy** — no `Find*`/LINQ in the per-frame files audited; billboards cache their camera. The **viability risk is almost entirely on the BUILD/MEMORY axis**: `Resources/` is **374.9 MB (863 images, 259 MB)** force-loaded into the WASM heap at startup, `Models/` is **1.16 GB** of source meshes, the **Addressables remote catalog is disabled**, and **max WASM memory is set to 2 GB** — which will OOM-crash on a 128-256 MB mobile browser (Pi Browser). Fix the build/memory axis first; it is the difference between "loads on a phone" and "white screen / crash."

---

## 0. Measured facts (this repo, today)

| Thing | Value | Source |
|---|---|---|
| `Assets/Resources/` total | **374.9 MB** | folder weigh |
| `Assets/Resources/` images | **863 files, 259.3 MB** | folder weigh |
| `Assets/Models/` | **1,162.6 MB** | folder weigh |
| `Assets/polyperfect/` (gitignored) | 404.5 MB | folder weigh |
| `Assets/Quaternius/` | 124.0 MB | folder weigh |
| `Assets/Lana Studio/` (VFX) | 123.5 MB | folder weigh |
| `Find*ObjectsByType`/`FindFirstObjectByType`/`GameObject.Find` call sites | 757 across 250 files | grep |
| `GetComponent*` call sites (`_Modules`) | 934 across 250 files | grep |
| `Camera.main` call sites (`_Modules`) | 91 across 50 files | grep |
| `Update`/`LateUpdate`/`FixedUpdate` methods | 247 across 233 files | grep |

### Build config (`ProjectSettings/ProjectSettings.asset`) — what's already correct
- `webGLLinkerTarget: 1` → **Wasm** ✓
- `webGLCompressionFormat: 0` → **Brotli** ✓ (smallest download)
- `scriptingBackend.WebGL: 1` → **IL2CPP** ✓ (only valid option)
- `managedStrippingLevel.WebGL: 4` → **High** ✓ (aggressive code strip)
- `stripEngineCode: 1` ✓
- `webGLThreadsSupport: 0` ✓ (C# threads unsupported on WebGL anyway)
- `apiCompatibilityLevel: 6` → .NET Standard 2.1 ✓ (smaller surface)
- `webGLInitialMemorySize: 32`, geometric growth ✓ (small initial heap)
- `webGLDataCaching: 1` ✓ (browser-cache the bundles where allowed)

### Build config — what's WRONG or risky for mobile
- **`webGLMaximumMemorySize: 2048`** → allows the heap to grow to **2 GB**. Mobile browsers cap a WebGL tab at **~128-256 MB**; the heap will hit the device ceiling and the tab is killed *before* it ever reaches 2 GB. This masks the real OOM as a generic crash. **Cap at ~512 MB and profile against the real peak.**
- **`webGLExceptionSupport: 1`** (Explicitly-Thrown only) — fine for dev; for the shipping Pi build set to **None (0)** to shrink the wasm and speed it up (re-enable when debugging).
- **`webGLDecompressionFallback: 1`** — correct to KEEP **if** the Pi-Browser host can't guarantee `Content-Encoding: br` response headers (it adds a JS decompressor, small size cost). If you control the host and can set Brotli headers, turn it off for a smaller/faster load. **Decide based on the hosting story.**
- `m_BuildTargetDefaultTextureCompressionFormat: []` — **no per-platform texture-format override.** WebGL falls back to DXT/BC. For mobile WebGL, **ASTC** (8×8 block) gives much smaller textures; set a WebGL override.

### Addressables config — the remote-streaming path is NOT actually wired
- **`m_BuildRemoteCatalog: 0`** in `AddressableAssetSettings.asset` → **remote catalog disabled.** Despite the WO-545 "Addressables-remote streaming" direction, today **everything packs LOCAL** → it all ships in the initial download. The data-architecture memory ("seams first → WO-545 Addressables-remote") is **not yet realized in the project settings.**
- Groups use `m_Compression: 1` (LZ4) — good for runtime decode; the *download* shrink comes from Brotli on top. Fine.
- Only a handful of groups exist (Default Local, Gear, Localization×3). **The 374 MB of `Resources/` is not in Addressables at all** — see P0-1.

---

## P0 — WebGL / Pi-Browser viability blockers (do these or it won't run on a phone)

### P0-1. `Resources/` is 374.9 MB and is force-loaded into the WASM heap at startup
**Why it's fatal on mobile:** everything under any `Resources/` folder is (a) **always included** in the build regardless of references, and (b) its asset metadata + many of its objects are **loaded eagerly** at startup, never streamed and never unloaded by default. 259 MB of textures decompressing into the heap will exceed the 128-256 MB mobile cap immediately. This is the single biggest viability risk.

**Fix (high effort, highest payoff):**
1. Inventory `Resources/` — `ItemIcons`, `Heroes`, `Enemies/*Tex`, etc.
2. Move everything not needed at frame-0 into **Addressables groups**, loaded **on demand** (`Addressables.LoadAssetAsync` when a panel/character actually appears), then **released**. Unity's own guidance: "instead of loading all assets at startup, use Addressables to load assets only when your application needs them." ([Unity: optimize Web for mobile](https://docs.unity3d.com/6000.0/Documentation/Manual/web-optimization-mobile.html))
3. Target an **initial download < 5 MB of gameplay assets**, fetch the rest progressively. ([Unity blog: Addressables planning & best practices](https://unity.com/blog/engine-platform/addressables-planning-and-best-practices))
4. For the 863 icons specifically: pack into a **sprite atlas** (or a few), Crunch/ASTC compressed — this collapses both build size and draw calls.

### P0-2. `Models/` = 1.16 GB of source meshes; Tripo/pet meshes confirmed as "dominant WebGL build bloat"
`Assets/_Modules/Pets/PetBillboard.cs` header documents that pets alone were **208 MB of Tripo 3D meshes** — "the dominant WebGL build bloat" — and a lite **sprite-quad** path was built to dodge it. The same disease applies to the hero/enemy Tripo roster.

**Fix:**
- Ensure only the **shipped** meshes are referenced; un-referenced FBX in `Models/` should not enter the build (verify via the WebGL **Build Report / Analyze Build Size**, currently `webGLAnalyzeBuildSize: 0` — turn it on for one diagnostic build).
- Apply the **decimate + Mesh Baker (6 sub-meshes → 1)** plan already in project memory (`tripo-roster-knight-orcs-first`) to every shipped character.
- Mesh-compression + read/write **disabled** on all import settings (read/write doubles mesh memory).
- Keep heavy/optional characters behind **Addressables**, not in the always-loaded set.

### P0-3. Wire the Addressables **remote catalog** + remote groups (the WO-545 path is unbuilt)
`m_BuildRemoteCatalog: 0` means the "stream content from a server" architecture is aspirational, not real. To make the initial Pi-Browser download small:
- Enable **Build Remote Catalog**, set a Remote load path (your CDN/host), and move the non-day-1 groups (extra characters, cosmetics, later regions, VFX packs) to **remote** bundles.
- **Caveat (cite + plan around it):** Unity **disables the AssetBundle cache on WebGL** — downloaded remote bundles are **not** persisted via the Unity cache; you rely on the **browser HTTP cache** (and Safari-in-iframe can't use IndexedDB). So design remote groups to be **browser-cacheable** (stable hashed URLs, `webGLNameFilesAsHashes: 1` already on ✓) and don't assume re-visits are free. ([Unity: optimize WebGL for mobile](https://docs.unity3d.com/2022.3/Documentation/Manual/web-optimization-mobile.html), [Unity: Web technical limitations](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-technical-overview.html))

### P0-4. Cap WASM max memory; profile the real peak
- Set **`webGLMaximumMemorySize` ≈ 512** (not 2048). Keep `webGLInitialMemorySize: 32` + geometric growth so it starts tiny and grows only as needed.
- Mobile browsers cap a tab at ~128-256 MB; the build must run under the **lowest** target, not the average. Profile a real build against that ceiling. ([Memory in Unity Web](https://docs.unity3d.com/Manual/webgl-memory.html), [WASM OOM fix](https://gamineai.com/help/unity-webgl-build-out-of-memory-at-runtime-wasm-memory-asset-compression-fix))

### P0-5. Texture import discipline (the 259 MB number lives or dies here)
- Set a **WebGL platform override**: **ASTC 8×8** (good size/quality balance for mobile) per [Unity mobile-WebGL texture guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/web-optimization-mobile.html); enable **Crunch** compression on the icon/UI atlases for download shrink.
- Cap **Max Size** on UI icons (most do not need 1024/2048 — 128/256 is plenty for a slot plate). A single 2048 RGBA32 uncompressed icon is 16 MB in heap; 863 of anything near that is the whole budget.
- Generate **mipmaps OFF** for screen-space UI sprites (saves 33% memory + bandwidth).

---

## P1 — Runtime hot paths (CPU / frame time)

> The audited per-frame files (`FloatingHealthBar`, `ThreatSkullPlate`, `NodeFillIndicator`, `EnemyBrain`, `AwarenessSensor`, `BattleHud9Zone`, `WaveManager`) contain **no `Find*` and no LINQ inside `Update`** — diagnosis-resolution happens in `Awake`/`Start` and is cached. This is the right pattern and is mostly being followed. The items below are the residue.

### P1-1. `PetBillboard.LateUpdate` calls `Camera.main` every frame, uncached
`Assets/_Modules/Pets/PetBillboard.cs:28` — `var cam = Camera.main;` runs every `LateUpdate`. `Camera.main` does a `FindGameObjectsWithTag("MainCamera")` internally each call. Few pets exist so the cost is small, but it's the one un-cached offender found.
- **Fix:** cache the camera transform on first resolve (the pattern `FloatingHealthBar`/`ThreatSkullPlate`/`Billboard` already use), or better, introduce one shared **`BillboardService`/`CameraCache`** singleton that all billboards read from, so the 50 files touching `Camera.main` resolve it once globally.

### P1-2. `Find*ObjectsByType` audit — verify none are per-frame, then lint it shut
757 call sites is a lot, but the sampled ones (`AwarenessSensor.ResolveSceneRefs` in `Awake`, `WaveManager` lazy `_spawnPoints` cache) are **one-shot/cached**, which is correct. Risk is a future regression dropping a `FindFirstObjectByType` into an `Update`.
- **Fix:** add an EditMode lint (there's already a `PerFrameReentrancyLintTests.cs` precedent) that **fails the gate if `Find*ObjectsByType`/`GameObject.Find`/`FindWithTag` appears inside `Update`/`LateUpdate`/`FixedUpdate`**. Cheap insurance that keeps the 757 honest.

### P1-3. Spawn-time `GetComponentsInChildren` allocations
`WaveManager.VerifySpawnedEnemy` (`:1526`) does `GetComponentsInChildren<Renderer>(true)` + `GetComponentInChildren<NavMeshAgent>` per spawned enemy, and `WaveManager` adds `EnemyDamageable` via `GetComponent`+`AddComponent` per spawn (`:422`, `:1493`). Per-spawn (not per-frame), so not a frame-time killer, but each `GetComponentsInChildren` allocates an array → GC pressure during a wave burst (worse on WebGL where GC is single-threaded and stalls the main thread).
- **Fix:** bake `EnemyDamageable` + cached renderer lists onto the **enemy prefab/pool** (the code comment even says `EnemyPool` already guarantees it) so the runtime `Add`/scan is unnecessary; gate `VerifySpawnedEnemy` behind a dev/`FlowTrace.Enabled` flag so it doesn't run in the shipping build at all.

### P1-4. `FlowTrace` / instrumentation in the shipping build
Per CLAUDE.md §12 the codebase is heavily instrumented (`FlowTrace.Step/Warn/Fail`, `Guard.Try`). String interpolation in trace calls allocates even when the message is discarded.
- **Fix:** confirm `FlowTrace.Enabled=false` for the Pi/release build, and prefer `[Conditional]`-attributed trace methods or `if (FlowTrace.Enabled)` guards around any interpolated argument so the **string is never built** in release. (Single-threaded WASM GC makes every avoidable alloc matter.)

---

## P2 — Draw calls / materials / GPU

WebGL guidance: "avoid large numbers of draw calls per frame; make sure instancing and batching are used." ([Unity Web performance](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-performance.html))

### P2-1. SRP Batcher + GPU instancing
- Confirm the **URP SRP Batcher is ON** and that materials are SRP-Batcher-compatible (no per-renderer `MaterialPropertyBlock` thrash that breaks the batch). The many runtime "material fixer" scripts (`EnvironmentTreeMaterialFixer`, `TreeOfLifeMaterialFixer`, `GroundZFightFixer`, `MagentaGuard`) hint at lots of distinct materials — each unique material = a batch break.
- Enable **GPU instancing** on the repeated props (walls, fences, trees, crystals) and the enemy/troop materials so a wave of identical orcs draws in one instanced call.

### P2-2. Texture atlasing → fewer materials → fewer draws
The 863-icon problem (P0-5) is also a draw-call problem: atlas the UI and the world-prop textures so they share a material. Combined with **Mesh Baker (P0-2)** this is the biggest GPU win.

### P2-3. Lighting / shadows / post on mobile WebGL
- Bake lighting where possible; **realtime shadows are expensive** on mobile WebGL. The `NightTorchLightSystem` / multiple realtime point lights (torches) are a classic mobile killer — cap realtime lights, use baked/blob shadows, or light cookies.
- Set the **WebGL Quality level to the Fastest tier** for the shipping build (smaller build + fewer GPU features). ([Unity: optimize WebGL for mobile](https://docs.unity3d.com/2022.3/Documentation/Manual/web-optimization-mobile.html))

---

## Prioritized action list (do in this order)

| # | Action | Effort | Payoff | Axis |
|---|---|---|---|---|
| 1 | Empty `Resources/` → Addressables on-demand; atlas the 863 icons | High | **Critical** | Build+Mem |
| 2 | Cap `webGLMaximumMemorySize` to ~512; profile real peak | Trivial | **Critical** | Mem |
| 3 | WebGL texture override (ASTC 8×8) + Crunch + cap Max Size + mip-off on UI | Med | **Critical** | Build+Mem |
| 4 | Mesh Baker + decimate shipped Tripo chars; mesh read/write off | High | High | Build+Mem |
| 5 | Enable Build Remote Catalog + move non-day-1 groups remote | Med | High | Load time |
| 6 | Turn on `webGLAnalyzeBuildSize` for ONE diagnostic build; read the report | Trivial | High (visibility) | Build |
| 7 | `webGLExceptionSupport: 0` for the shipping Pi build | Trivial | Med | Build+CPU |
| 8 | Cache `Camera.main` (shared `BillboardService`); fix `PetBillboard` | Low | Low-Med | CPU |
| 9 | EditMode lint: ban `Find*`/`GameObject.Find` inside `Update`/`LateUpdate`/`FixedUpdate` | Low | Med (regression guard) | CPU |
| 10 | Bake `EnemyDamageable`/renderer cache onto enemy prefab; gate `VerifySpawnedEnemy` to dev | Low | Med | GC |
| 11 | Confirm `FlowTrace.Enabled=false` in release; guard interpolated trace args | Low | Med | GC |
| 12 | SRP Batcher + GPU instancing audit; atlas world-prop materials | Med | High | GPU |
| 13 | Bake lighting; cap realtime torch lights / blob shadows; Fastest quality tier | Med | High | GPU |

**Pi-Browser viability verdict:** *Conditionally viable, NOT today.* The C# is in good shape, but the project currently ships ~375 MB of always-loaded `Resources` with a 2 GB memory ceiling — that **will white-screen/crash on a phone**. Items **1-4** are the gate to "loads and runs in Pi Browser." Items 5-6 make the load *fast*; 7-13 make it *smooth*. None of the blockers are architectural dead-ends — they're asset-pipeline + 4 build settings.

---

## Sources
- [Unity — Optimize WebGL platform for mobile (2022.3)](https://docs.unity3d.com/2022.3/Documentation/Manual/web-optimization-mobile.html)
- [Unity — Optimize Web platform for mobile (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/web-optimization-mobile.html)
- [Unity — Web performance considerations (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-performance.html)
- [Unity — Memory in Unity Web](https://docs.unity3d.com/Manual/webgl-memory.html)
- [Unity — Web technical limitations](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-technical-overview.html)
- [Unity — Web browser compatibility](https://docs.unity3d.com/Manual/webgl-browsercompatibility.html)
- [Unity blog — Addressables planning and best practices](https://unity.com/blog/engine-platform/addressables-planning-and-best-practices)
- [Unity blog — Optimizing memory and build size with Addressables](https://unity.com/blog/engine-platform/extended-q-a-optimizing-memory-and-build-size-with-addressables)
- [Unity — How to profile and optimize a web build](https://unity.com/how-to/profile-optimize-web-build)
- [WASM out-of-memory / asset-compression fix](https://gamineai.com/help/unity-webgl-build-out-of-memory-at-runtime-wasm-memory-asset-compression-fix)
- [10 Critical Unity WebGL Performance Tips](https://friendzy.xyz/2025/09/17/unity-webgl-performance-tips/)
