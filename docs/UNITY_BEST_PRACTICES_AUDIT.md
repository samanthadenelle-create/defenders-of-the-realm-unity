# Unity Best-Practices Audit — Defenders of the Realm / Echoes of Elarion

**Scope:** READ-ONLY architect review of runtime C# under `Assets/_Modules/**` (Editor + Plugins excluded except where noted).
**Target:** Unity 6 (6000.4.8f1), URP, mobile-first WebGL (IL2CPP).
**Date:** 2026-06-03 · **Branch:** feat/tower-core-loop · **Method:** ripgrep sweeps for the usual Unity hot-path / lifecycle / WebGL anti-patterns, then sample-read of the worst candidates to confirm they're real (in a hot path / an actual leak) rather than pattern-match noise.

This is **guidance only**. No code/scene/asset files were modified.

---

## Executive summary

The runtime codebase is **in good shape and unusually disciplined** for a project of this size. The two highest-multiplied hot loops — `Enemy.Update` and `EnemyBrain.Update` (one per live enemy, every frame) — are allocation-free, throttle their perception/scoring, and use `OverlapSphereNonAlloc` into reused buffers. Async is a hard `UniTask`-only mandate that is genuinely adhered to (exactly **one** `async void`, and it's a legitimate UI button handler). UI panels that subscribe to service events correctly unsubscribe in `OnDisable`. WebGL-fatal `File.IO` is consistently routed through the `CanonicalJson` "Resources-first, File-fallback" loader.

The top three themes for improvement are: **(1)** one real per-frame target-search regression in `EnemyBrain` that only bites when the tactics ScriptableObject is unassigned (the default case); **(2)** a scattering of dev/debug `FindObjectOfType`/`GameObject.Find` calls that live inside `Update()` but are gated behind input or one-shot flags (cheap today, easy footguns to leave for later); **(3)** minor structural drift — a few 1000-1900-line "god" controllers — that is a maintainability cost, not a correctness one.

**Finding counts:** P0 = **2** · P1 = **3** · P2 = **3**

---

## P0 — correctness / leaks / WebGL-fatal

### P0-1 — `EnemyBrain.FindClosestTarget` uses `GameObject.FindWithTag` per-frame in the default (no-tactics) path — *latent scene-wide scan in the core hot loop*
`Assets/_Modules/Village/Enemies/EnemyBrain.cs:905-911` (reached from `:710-711` and `:762`)

When the optional `_tactics` ScriptableObject is **not** assigned — which the code comment at `:708-710` explicitly calls "today's EXACT behaviour... the common case" — every DPS/Ranged/MiniBoss enemy falls through to `FindNearbyHero() ?? FindNearestTower() ?? FindClosestTarget()` **every frame, unthrottled**. `FindClosestTarget` does two `GameObject.FindWithTag` calls. The throttle (`_targetEvalTimer`, 2 s) that protects the scored path at `:715-719` is gated behind `_tactics != null`, so the default case never benefits from it.

This is filed P0 not because it crashes but because it's a *silent* correctness/perf trap: the well-optimized scoring path gives a false sense that targeting is throttled, while the shipping default path is not. On a full wave on WebGL this is N-enemies × 2 tag scans × 60 fps.

**Fix guidance:** Hoist the `_targetEvalTimer` throttle so it also caches the result of the legacy `FindNearbyHero ?? FindNearestTower ?? FindClosestTarget` chain (return `_currentTarget` between eval ticks regardless of `_tactics`). Alternatively, assign a default tactics SO to every enemy prefab so the already-throttled path is always taken. Either removes the per-frame `FindWithTag`.

### P0-2 — WebGL `File.Exists`/`File.ReadAllTextAsync` direct-read branch in the async loaders
`Assets/_Modules/Village/Waves/WaveData.cs:328-332` (same pattern in `Dungeons/DungeonLayout.cs:322`, `Dungeons/Crafting/CraftingData.cs:254`, `Dungeons/LoreFragments.cs:161`)

`ReadTextAsync` only routes through `UnityWebRequest` when the path contains `://`; otherwise it calls `File.Exists` + `File.ReadAllTextAsync`. On WebGL there is no synchronous filesystem — if `Application.streamingAssetsPath` resolves without a `://` scheme (or a catalog is loaded by absolute path), `File.Exists` returns false / `ReadAllTextAsync` throws, and the wave/dungeon catalog silently fails to load. The synchronous catalog loaders (`BuildingCatalog`, `PetCatalog`, `WaveRegistry`, etc.) already solve this correctly via `CanonicalJson` (Resources-first). These four async loaders do **not** have a `Resources.Load<TextAsset>` fallback.

**Fix guidance:** Mirror the `CanonicalJson` pattern in `ReadTextAsync` — try `Resources.Load<TextAsset>` first and return its `.text`, then fall back to `UnityWebRequest` for `://` paths, then `File.*` only as the editor/desktop last resort. This matches the documented WebGL-safe loader memory and removes the only runtime `File.*` reads not already guarded.

---

## P1 — mobile / WebGL performance (GC / per-frame)

### P1-1 — `FindObjectOfType` / `GameObject.Find` calls living inside `Update()` (gated, but fragile)
Multiple files. Confirmed real `Update`-body occurrences:
- `Assets/_Modules/Village/Waves/WaveFeedbackDirector.cs:53` — `FindObjectOfType<HeartController>()` (runs only until `_heartT` resolved; throttled to 0.25 s — **low impact, fine**).
- `Assets/_Modules/Village/World/MineNode.cs:265` — `GameObject.FindWithTag("Player")` (cached after first hit — **fine**).
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelBootstrap.cs:111,119` — `FindObjectOfType<HeroLocomotion>()` + `FindObjectsByType<Building>()` (gated behind `GetKeyDown(F)` — **dev path, fine**).
- `Assets/_Modules/Village/Crafting/VillageCraftingPanelBootstrap.cs:115` — same `GetKeyDown(F)` gate.
- `Assets/_Modules/Village/Buildings/TowerLoopDevHarness.cs:69` — `FindAnyObjectByType<WaveManager>()` (dev harness).
- `Assets/_Modules/Dungeons/DungeonStubReturn.cs:59` — `GameObject.Find("DungeonHeroPlaceholder")` on a `_proxCheck` timer.

**Impact:** Individually low — each is either cached-on-first-hit or input-gated. Flagged as a cluster because the pattern (`Find*` literally inside `Update`) is exactly the footgun that becomes a real cost the moment someone removes the gate or copies the bootstrap. **Fix guidance:** resolve these in `Start`/`OnEnable` or via the existing injector bootstraps, not in `Update`; for the genuinely-lazy ones, the cached-null-check pattern they already use is acceptable — leave them but add a comment so the gate isn't accidentally removed.

### P1-2 — Per-frame `Physics.OverlapSphere` (allocating, non-NonAlloc) in a couple of Update paths
- `Assets/_Modules/Environment/TorchFireController.cs:110` — `Physics.OverlapSphere(...)` in `Update` (author left a note: "acceptable for ≤ 8 torches"). Each call allocates a `Collider[]`. Acceptable at low count but it's GC-per-frame-per-torch on WebGL.
- `Assets/_Modules/Village/Camera/CinemachineCameraController.cs:107` — `Physics.OverlapSphere` (camera-collision probe).
- `Assets/_Modules/Village/Enemies/PlayerAttackController.cs:174` — `Physics.OverlapSphere(transform.position, _attackRange, _enemyLayer)` (this one is correctly LayerMask-filtered and fires only on attack, not per-frame — **fine**).

**Fix guidance:** convert the two `Update`-resident ones (`TorchFireController`, `CinemachineCameraController`) to `OverlapSphereNonAlloc` into a cached buffer — the codebase already uses this idiom in `HeroHealth` and `EnemyBrain`, so it's a drop-in. Rough impact: removes a small steady GC trickle that WebGL's GC handles poorly.

### P1-3 — Per-frame `material.SetColor("_EmissionColor", …)` with string property names in pulse animations
`Assets/_Modules/Dungeons/Checkpoint.cs:200`, `Assets/_Modules/Dungeons/Crafting/CraftingPedestal.cs:303` (pulse loops in `Update`), plus `BossHealthBar.cs:97` building a `$"…"` HP string every frame while visible.

String-keyed `SetColor`/`SetFloat` re-resolve the property name each call, and `.material` (vs `.sharedMaterial`) instantiates a material instance. **Impact:** minor — these are single-instance objects (one boss bar, a few dungeon props), not multiplied across a wave. **Fix guidance:** cache `static readonly int EmissionId = Shader.PropertyToID("_EmissionColor")` once and pass the int; for `BossHealthBar`, only rebuild the string when the integer HP value actually changes. Low priority — list for cleanup, not urgent.

---

## P2 — maintainability / structure

### P2-1 — A handful of god-class controllers
`PatriciaLightController.cs` (1982 lines), `VillageHudController.cs` (1523), `WaveManager.cs` (1194), `DevPanelController.cs` (1177), `GameStateService.cs` (1112), `Enemy.cs` (1098), `AudioService.cs` (1053). These compile and work, but they're hard to reason about and are change-risk magnets (the `Village.unity` / `VillageSceneBuilder` serialization-bottleneck lesson in CLAUDE.md §9 applies in spirit to large controllers too). **Guidance:** no rewrite — opportunistically extract cohesive responsibilities (e.g. `VillageHudController`'s sub-panels) into partials/components as they're next touched. `Enemy.cs` and `WaveManager.cs` are the highest-value candidates because they're in the gameplay-critical path. Note: the team already split `VillageSceneBuilder` into partials — apply that same pattern.

### P2-2 — `async void` UI handler swallows exceptions
`Assets/_Modules/Web3/JupiterSwapPanelController.cs:271` — `async void OnConfirmTapped()` awaits `ExecuteSwapAsync` with no `try/catch`. This is the one sanctioned `async void` (a UI event handler, which is the correct place for it), but an exception inside the swap will be swallowed by the synchronization context and the button is left disabled with a stale status. **Guidance:** wrap the body in `try/catch` that re-enables the confirm button and shows an error toast on throw. Tiny, isolated.

### P2-3 — `System.Reflection` surface is broad but currently all "bridge" pattern
The reflection sweep returned ~37 runtime files, but every one inspected is the sanctioned cross-asmdef **bridge** pattern (`*HudBridge`, `*VfxBridge`, `BuildMenuHudBridge`, etc.) explicitly permitted by CLAUDE.md §5. **No new/unsanctioned reflection creep was found.** Flagging only so the team keeps enforcing the §10 checklist item — the breadth means a stray reflective call would be easy to hide. A cheap CI grep (below) would catch it.

---

## What's already done RIGHT (keep these)

- **Allocation-free core hot loops.** `Enemy.Update` (`Enemy.cs:482`) and `EnemyBrain.Update` (`EnemyBrain.cs:323`) — the most-multiplied components — contain **zero** `Find*`, `GetComponent`, LINQ, or `new` allocations. Target scanning uses `Physics.OverlapSphereNonAlloc` into a reused `_scanBuffer` (`EnemyBrain.cs:738`, `HeroHealth.cs:141`).
- **Perception/targeting is throttled, not per-frame.** `TickPerception` (`EnemyBrain.cs:661`) runs the sensor scan on an LOD cadence (distant Unaware enemies scan 2.5× slower), and pushes Animator bools only on state change — textbook mobile AI throttling. (The one gap is the no-tactics legacy path, P0-1.)
- **`Camera.main` is consistently cached.** Nearly every one of ~60 hits uses the lazy `if (_cam == null) _cam = Camera.main;` idiom and resolves once. No per-frame `Camera.main` in any confirmed hot path.
- **UniTask-only async discipline is real.** A grep for `async void` returns mostly *comments reaffirming the ban*; exactly one actual `async void`, and it's a legitimate UI handler. Fire-and-forget uses `.Forget()`.
- **Event subscriptions are balanced.** `PromoCodeUI`, `InviteFriendsUI`, `ClanChatPanel` all pair `+=` in `OnEnable` with `-=` in `OnDisable` — no leaks found in the UI panels sampled.
- **WebGL-safe catalog loading.** The synchronous catalog family (`BuildingCatalog`, `PetCatalog`, `WalletRegistry`, `CosmeticCatalog`, `HeroTalentCatalog`, …) all route through `CanonicalJson` Resources-first with `File.*` as desktop fallback — exactly the documented WebGL pattern.
- **Animator param-guarding.** `EnemyBrain` caches `_hasIsAlertParam` and guards `SetBool` (`:681`) — avoids the documented 3,351-errors/frame param-spam trap.
- **Physics queries are LayerMask-filtered.** The combat/build raycasts (`TowerPlacementSystem`, `WallRepairController`, `PlayerAttackController`, `SmartMobileCamera`) pass explicit masks rather than hitting everything.

---

## Suggested guardrails

1. **Cheap CI/editor lint (highest ROI).** A pre-commit ripgrep that fails on the two patterns that actually bit this project, scoped to `Assets/_Modules/**`:
   - `Find(Object|FirstObject|AnyObject)OfType|GameObject\.Find` **inside** an `Update`/`FixedUpdate`/`LateUpdate` body (catches P1-1 regressions).
   - `Physics\.(Raycast|OverlapSphere|SphereCast)\b` not suffixed `NonAlloc` inside those same bodies (catches P1-2 regressions).
   - `System\.Reflection|BindingFlags` in any file **not** named `*Bridge.cs` (catches reflection creep — the §10 checklist item, automated).
2. **CLAUDE.md addition (§1 quality gate).** Add a one-liner: *"Targeting/perception in any per-enemy `Update` MUST be throttled (timer or cadence) — never call `FindWithTag`/`OverlapSphere` unthrottled per frame. See `EnemyBrain.TickPerception` for the pattern."* This pins the P0-1 lesson so the next agent doesn't reintroduce the no-tactics fast path.
3. **WebGL loader rule.** Add to §8: *"All catalog/data reads go through `CanonicalJson` (Resources-first). New `File.*`/`ReadAllTextAsync` reads are forbidden in `Assets/_Modules` runtime code — they throw on WebGL."* (closes P0-2's door for future code.)
