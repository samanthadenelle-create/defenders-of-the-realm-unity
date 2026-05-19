# Memory-Leak & Resource-Disposal Audit — Defenders of the Realm (v2 Unity Port)

**Auditor:** Unity 6 performance-engineering pass — memory & resource-leak focus
**Date:** 2026-05-19
**Target gate:** Week-8 acceptance — total runtime memory ≤ 400 MB, frame-time
spikes ≤ 33 ms (a GC collection mid-wave is exactly a >33 ms hitch).
**Method:** Read-only static analysis of every runtime C# file under
`Assets/_Modules/` plus `Assets/Editor/`. Cross-referenced against
`docs/audit/mobile-performance.md` (RISK 2 memory, RISK 4 GC) and
`docs/audit/architecture-review.md` (CODE-004, CODE-005, CODE-011). No file was
modified except this document.

---

## Executive summary

**Verdict: ACCEPTABLE for the foundation milestone — no leak will sink the build,
but four real leak patterns must be fixed before the Week-8 profiling run, and
they are all in the gameplay hot path.**

The codebase is disciplined about the things that usually leak. Every UI Toolkit
button callback (`clicked +=`) and every cross-module C# `event` / `UnityEvent`
subscription that an auditor would expect to leak is, in fact, paired with an
unsubscribe in `OnDisable` / `OnDestroy` — `BattleController`, `WalletConnectDialog`,
`TitleController`, `VillageHudController`, `BuildMenu`, `DevPanelController` all
do this correctly, and several even guard against a double-`OnEnable`. The
async/UniTask flows return `UniTask` (never `async void`), the two onboarding
controllers properly `Dispose` their `CancellationTokenSource`, the
StreamingAssets loaders use `using var` on every `UnityWebRequest`, and the
editor screenshot scripts `DestroyImmediate` their temporary `RenderTexture` /
`Texture2D`. This is a codebase that knows the patterns.

The leaks that **do** exist are concentrated in one place: **runtime-spawned
GameObjects in the wave / pet / ability / dungeon-NPC paths are created without
pooling, and one of them creates a `Material` in code that is never destroyed.**
The headline risk is not a slow creep — it is **GC pressure**: per-cast VFX and
per-spawn enemy `Instantiate`/`Destroy` churn produce managed allocations and
native-object thrash that threaten the ≤ 33 ms spike gate during a wave. This is
the same finding the mobile-perf audit flagged as RISK 4 and the architecture
review flagged as CODE-005 — this audit quantifies it and adds one genuine
native-object leak (`WandererBubble`) the prior passes did not name.

Static analysis cannot prove the absence of a slow runtime leak across scene
transitions — the `DontDestroyOnLoad` accumulation and the Addressables
release-on-scene-exit behaviour can only be confirmed with the Unity Memory
Profiler. The Week-8 profiler plan in §3 is therefore not optional.

### Findings by severity

| Severity | Count | Finding ids |
|----------|-------|-------------|
| **P0** | **0** | — |
| **P1** | **4** | MEM-001, MEM-002, MEM-003, MEM-004 |
| **P2** | **5** | MEM-005, MEM-006, MEM-007, MEM-008, MEM-009 |
| **Total** | **9** | |

### Worst leak risk

**MEM-001 — un-pooled per-spawn / per-cast `Instantiate` + `Destroy` across the
wave loop (`HeroAbilities.SpawnVfx`, `WaveManager.SpawnOne`/`BuildPlaceholderEnemy`,
`PetDeployer.SpawnPet`).** Every ability cast builds a fresh `GameObject` +
`ParticleSystem` and `Destroy`s it ~1 s later; every enemy and pet is a fresh
`Instantiate` (or `CreatePrimitive`) destroyed on death. In a Wave-1 fight that
is dozens of object create/destroy cycles, each one a managed allocation plus
native transform/collider/mesh churn. The collection that frees it all is one of
the most reliable ways to blow the ≤ 33 ms frame-spike gate. It is not a
*growing* leak (the objects do get destroyed), but it is the dominant
memory-churn risk and the first thing the Week-8 profiler will surface.

---

## P1 findings

### MEM-001 — Un-pooled per-spawn / per-cast Instantiate + Destroy churn (GC spikes)
**Severity:** P1
**Location:**
- `Assets/_Modules/Village/Hero/HeroAbilities.cs` — `SpawnVfx` (lines 297-307),
  `BuildBuiltInBurst` (lines 314-341)
- `Assets/_Modules/Village/Waves/WaveManager.cs` — `SpawnOne` (line 414),
  `BuildPlaceholderEnemy` (lines 432-443), apex boss `Instantiate` (line 351)
- `Assets/_Modules/Pets/PetDeployer.cs` — `SpawnPet` (lines 129-151)
- `Assets/_Modules/Village/Enemies/Enemy.cs` — `Die` → `Destroy(gameObject, …)`
  (lines 389-393)

**Description:** No spawned gameplay object is pooled. `HeroAbilities.SpawnVfx`
runs `new GameObject("AbilityVFX_Placeholder")` + `AddComponent<ParticleSystem>()`
on **every Q/W/E/R press** and `Destroy`s it after `life + 0.5 s`. `WaveManager`
`Instantiate`s (or `CreatePrimitive`-builds) one `Enemy` GameObject per spawned
enemy; `Enemy.Die` `Destroy`s it. `PetDeployer` does the same per pet. Each
create is a managed allocation (the `GameObject`, the component, the
`ParticleSystem.Burst[]` array in `BuildBuiltInBurst`) plus native
transform/collider allocation; each `Destroy` feeds the GC and the native
deallocator. During a wave — 8 enemies in Wave 1, more later, plus an ability
cast every few seconds — this is steady create/destroy thrash. A garbage
collection landing mid-wave is precisely a >33 ms frame hitch, which directly
fails the Week-8 frame-spike gate. The team already does the *non-alloc* pattern
correctly elsewhere (`HeroAbilities._overlap` / `Pet._overlap` +
`OverlapSphereNonAlloc`), so the pattern is understood — it is just not applied
to spawned objects. This is RISK 4 in `mobile-performance.md` and CODE-005 in
`architecture-review.md`.

**Fix:** Introduce a small pooling layer for the three hot spawn paths:
1. **Ability VFX** — a ring buffer of N reusable `ParticleSystem` objects in
   `HeroAbilities`; `SpawnVfx` re-positions, re-tints, `Clear()`s and `Play()`s
   the next one instead of `Instantiate`/`Destroy`. Set the particle system to
   `stopAction = ParticleSystemStopAction.Disable` rather than destroying.
2. **Enemies** — pool `Enemy` GameObjects keyed by prefab; `Die` deactivates and
   returns to the pool instead of `Destroy`. Unity's `ObjectPool<T>` (UnityEngine.Pool)
   is the standard tool. Reset HP/state on re-acquire.
3. **Pets** — three pets, deployed once per scene; lower churn, but pool them on
   the same path for consistency.

Verify with the Profiler's **GC Alloc** column during a wave — target near-zero
per-frame managed allocation in steady state.

---

### MEM-002 — `WandererBubble` creates a `Material` in code and never destroys it
**Severity:** P1 (native-object leak — unbounded if the bubble is ever rebuilt)
**Location:** `Assets/_Modules/Dungeons/Wanderer/WandererBubble.cs` —
`ApplyPanelMaterial` (lines 156-167), `Build` (lines 117-153).

**Description:** `ApplyPanelMaterial` does `var mat = new Material(shader)` and
assigns it to `_panelRenderer.sharedMaterial`. A `Material` created with `new`
in code is a **native object that Unity does not garbage-collect** — it must be
explicitly `Destroy`d (or `Resources.UnloadUnusedAssets` must reclaim it). This
`WandererBubble` has **no `OnDestroy`** and never destroys the material. On a
single dungeon load it is one leaked material per Bryn — small. But the leak is
*per build*: `Build()` is called from `Awake()` and again defensively from
`Show()` if `!_built` — and any future code path that rebuilds the bubble, or
re-enters the dungeon scene without the old `WandererBubble` being collected,
leaks another material. Worse, assigning a unique `new Material` to
`sharedMaterial` (rather than using `.material`, which Unity auto-instances and
tracks) is the classic "leaked material instance" pattern the Profiler Memory
module's "objects not destroyed" diff exists to catch. (Note: the editor scene
builders also `new Material(...)`, but those are editor-time asset construction
and are correctly exempt — this is the one *runtime* occurrence.)

**Fix:** Cache the created `Material` in a field and destroy it in an `OnDestroy`:
```
private Material _panelMaterial;
...
_panelMaterial = new Material(shader) { color = _panelColor };
_panelRenderer.sharedMaterial = _panelMaterial;
...
private void OnDestroy() { if (_panelMaterial != null) Destroy(_panelMaterial); }
```
Alternatively, since the panel needs only a flat tint, set the colour via a
`MaterialPropertyBlock` on a shared material — the same pattern `Gate` and
`PetDeployer.TintPlaceholder` already use correctly, which leaks nothing.

---

### MEM-003 — Long-lived fire-and-forget UniTasks have no `CancellationToken` — continuations resume on destroyed objects
**Severity:** P1 (state-corruption / `MissingReferenceException` on scene unload,
not a classic heap leak — but a leak of *work* and a crash risk)
**Location:**
- `Assets/_Modules/Village/Waves/WaveManager.cs` — `SpawnBatch` (lines 372-404,
  `.Forget()` at 304/314)
- `Assets/_Modules/Wallet/SolanaWalletProvider.cs` — `ConfirmTransaction`
  (lines 410-440, a 30 × 1 s poll loop)
- `Assets/_Modules/Dungeons/DungeonController.cs` — `EnterDungeon` (line 152
  `.Forget()`; awaits `DungeonLayoutLoader.LoadAsync` / `LoreFragmentsLoader.LoadAsync`
  with no liveness re-check)
- `Assets/_Modules/BattleATB/BattleController.cs` — `ReturnAfterResult`
  (lines 417-425)

**Description:** Several `async UniTask` flows are launched fire-and-forget with
`.Forget()` and contain `await UniTask.Delay(...)` loops, but none accepts a
`CancellationToken`. If the owning scene unloads mid-await — a breach during
`SpawnBatch`, a dungeon exit while `EnterDungeon` is still loading, a wallet
confirm still polling when the player backs out — the continuation resumes on a
destroyed `MonoBehaviour` and either throws `MissingReferenceException` or
mutates dead state. `SpawnBatch` *partly* mitigates this (it checks
`_phase != WavePhase.Active` after each delay — a good instinct), but
`ConfirmTransaction`'s 30-second poll has no guard at all and `EnterDungeon` does
not re-check liveness after the layout `await`. This is CODE-011 in
`architecture-review.md`. It is included in a *memory* audit because a
fire-and-forget task that outlives its scene keeps its captured closure state
(the `WaveBatch`, the `EnemyDef`, the whole `BattleParams`) alive and reachable
until the loop finally exits — a transient retention leak — and the
`ConfirmTransaction` poll holds an `IRpcClient` and a `signature` string alive
for up to 30 s past the scene.

**Fix:** Adopt UniTask's `this.GetCancellationTokenOnDestroy()` and thread the
token through every `UniTask.Delay` / `UniTask.Yield` in these flows
(`UniTask.Delay(span, cancellationToken: token)`). The two onboarding
controllers (`SplashLoading`, `StoryIntroController`) already model this
correctly with their own `CancellationTokenSource` — extend the same discipline
to `WaveManager`, `DungeonController`, `BattleController` and the wallet confirm
loop.

---

### MEM-004 — `WaveManager` has no `OnDisable`/`OnDestroy` — apex-boss subscription + roster lists leak on scene teardown
**Severity:** P1
**Location:** `Assets/_Modules/Village/Waves/WaveManager.cs` — `SpawnApexBoss`
(line 358 `dragon.Died += HandleApexBossDied`), `SpawnOne` (lines 421-422
`enemy.Died += …`, `enemy.ReachedHeart += …`); no `OnDisable`/`OnDestroy` in the
file.

**Description:** `WaveManager` subscribes to per-enemy `Action<Enemy>` events
(`Died`, `ReachedHeart`) and to the apex boss's `Died`. These are unsubscribed
**only** on the normal death/clear paths (`HandleEnemyDied`, `HandleApexBossDied`,
`TriggerBreach`). There is **no `OnDisable` or `OnDestroy`** on `WaveManager`
itself. If the village scene is torn down while a wave is live — the player
quits to Title mid-wave, or a dev scene-jump — every live `Enemy` and the
`DragonBoss` are destroyed, but `WaveManager` never runs a teardown pass:
- The live enemies / apex boss are scene objects destroyed with the scene, so
  the *subscriptions* die with both ends — not a true cross-scene leak in the
  common case.
- **But** `_liveEnemies`, `_breachRoster`, `_schedule` and `_enemyCatalog` are
  never cleared, and if `WaveManager` were ever made to survive a scene (or is
  re-entered via additive load), the stale enemy references and the cached
  `WaveSchedule`/`EnemyCatalog` would be retained. Combined with MEM-003 — a
  `SpawnBatch` UniTask still draining when the scene unloads holds the
  `WaveManager` alive through its captured `this`, and *then* the subscriptions
  genuinely outlive the teardown.
- The architecture review (CODE-004) already flags the fragile mid-iteration
  ordering in `TriggerBreach` (`e.Kill()` raises `Died` → `HandleEnemyDied`
  removes from `_liveEnemies` while `_liveEnemies.Clear()` also runs); a
  defensive teardown removes that whole class of bug.

**Fix:** Add an `OnDisable` (or `OnDestroy`) to `WaveManager` that snapshots and
unsubscribes every live `Enemy` and the apex boss, then `Clear()`s `_liveEnemies`
and `_breachRoster`:
```
private void OnDestroy()
{
    foreach (var e in _liveEnemies)
        if (e != null) { e.Died -= HandleEnemyDied; e.ReachedHeart -= HandleEnemyReachedHeart; }
    if (_liveApexBoss != null) _liveApexBoss.Died -= HandleApexBossDied;
    _liveEnemies.Clear();
    _breachRoster.Clear();
}
```
This is cheap, removes a latent leak, and hardens the CODE-004 ordering concern.

---

## P2 findings

### MEM-005 — `BuildMenu` ghost preview leaks if the menu GameObject is destroyed while armed
**Severity:** P2
**Location:** `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs` — `SpawnGhost`
(lines 342-349), `DestroyGhost` (lines 351-358), `Disarm` (lines 334-340).

**Description:** `BuildMenu.SpawnGhost` does `_ghost = Instantiate(_ghostPrefab)`.
`DestroyGhost` correctly destroys it, and `Disarm`/`SpawnGhost` call it — so on
the normal flow the ghost is freed. But `DestroyGhost` is only reached through
`Disarm` / `SpawnGhost` / `TryPlace`. If the `BuildMenu` component (or its scene)
is destroyed while a building is *armed* and the ghost is live, nothing destroys
`_ghost` — there is **no `OnDisable`/`OnDestroy`** calling `DestroyGhost`. The
ghost is a scene object so it dies with the scene in the common case, but if the
menu is destroyed independently (closed/re-opened, additive scene) the ghost
leaks. Low likelihood, cheap fix.

**Fix:** Add `private void OnDisable() => DestroyGhost();` to `BuildMenu`.

---

### MEM-006 — `DevPanelController` static `event Action` subscribers can outlive the panel
**Severity:** P2 (DEV-only — compiled out of release builds)
**Location:** `Assets/_Modules/DevTools/DevPanelController.cs` —
`GodModeChanged` / `InstantWinWaveChanged` `static event Action<bool>`
(lines 153-156).

**Description:** `DevPanelController` exposes two **static** events that gameplay
subscribes to (per the integrator notes at the foot of the file). Static events
are a textbook managed-memory leak vector: a gameplay `MonoBehaviour` that does
`DevPanelController.GodModeChanged += …` and never unsubscribes is held alive by
the static delegate for the whole process lifetime, surviving every scene load.
The panel itself is fine; the *subscribers* are the risk, and the integrator
notes do not call out the unsubscribe obligation. This is DEV-only — the entire
file is `#if DEVELOPMENT_BUILD || UNITY_EDITOR` so nothing ships — hence P2, but
a dev build profiled for the Week-8 run will show it.

**Fix:** Document in the integrator notes that any subscriber to
`GodModeChanged`/`InstantWinWaveChanged` must unsubscribe in `OnDisable`/`OnDestroy`.
Better: have the panel raise the change through a normal (non-static) event on a
located instance, or clear the static delegates when the panel is destroyed.

---

### MEM-007 — `GameStateService` `DontDestroyOnLoad` singleton — confirm exactly one survives
**Severity:** P2
**Location:** `Assets/_Modules/Core/State/GameStateService.cs` — `Awake`
(lines 80-96), `OnDestroy` (lines 98-101).

**Description:** `GameStateService` is the only `DontDestroyOnLoad` object in
the reviewed runtime code. Its `Awake` has a correct duplicate-guard
(`if (_instance != null && _instance != this) Destroy(gameObject)`) and `OnDestroy`
nulls `_instance` — so it does **not** accumulate, and this is *not* a leak as
written. It is listed P2 only as a **verification item**: `SceneRouter` is a
pure static class with no `DontDestroyOnLoad` canvas, the `ISceneFader` is wired
externally, and `architecture-review.md` ARC-005 already notes there is no
defined bootstrap owner for `GameStateService`. If a future scene each carries
its own `GameStateService` GameObject (a likely integration pattern), the guard
destroys the duplicate's *GameObject* — confirm that GameObject carries nothing
else (a `UIDocument`, a fader) that would leak when only the `GameStateService`
component's logic intended to be discarded. Also confirm the `GameState`
`ScriptableObject` created via `CreateInstance` in `Awake` (line 92) is not
re-created on every scene load — currently it is only created when `_state`
is null, which is correct.

**Fix:** No code change required today. During the Week-8 memory pass, take a
snapshot and confirm exactly **one** `GameStateService` and **one** `GameState`
SO instance are resident. Establish the single Core bootstrap ARC-005 recommends.

---

### MEM-008 — Unbounded battle-log growth in `BattleState.Log` / `BattleController` rendered labels
**Severity:** P2
**Location:** `Assets/_Modules/BattleATB/BattleController.cs` — `RenderLog`
(lines 326-347); `BattleState.Log` (append-only, `Types.cs` line 375).

**Description:** `BattleState.Log` is an explicitly "append-only event log" and
`BattleController.RenderLog` creates one new `Label` `VisualElement` per entry,
appended to `_battleLogContent` and never removed. For a single bounded battle
(the v2 foundation fights are short — one hero, a few enemies) this is small and
fully reclaimed when the ATB scene unloads, so it is **not** a process leak. It
becomes a concern only if a battle runs very long or if the ATB scene is ever
made persistent. Flagging it so it is on the radar: the log and its rendered
labels grow without bound *within a battle*.

**Fix:** None required for the foundation. If long battles appear later, cap the
rendered log to the most recent ~100 entries (recycle the oldest `Label`s) and
consider a ring buffer for `BattleState.Log`.

### MEM-009 — Per-frame `InteractableRoots` array allocation in `DungeonController`
**Severity:** P2 (micro-allocation — GC pressure, not a retained leak)
**Location:** `Assets/_Modules/Dungeons/DungeonController.cs` —
`InteractableRoots` property (lines 606-607).

**Description:** `public IReadOnlyList<Transform> InteractableRoots => new[] { … };`
allocates a fresh three-element array on **every property access**. The doc
comment says it is read by the scene builder (editor-time, infrequent), so the
real-world allocation rate is near zero today. It is listed only because a
property that allocates on every `get` is a latent per-frame-allocation trap if
a future caller reads it in `Update`. Not a leak; a GC-hygiene note.

**Fix:** Cache the array in a field built once, or return the three fields via an
explicit method named to signal it allocates. Trivial; do it opportunistically.

---

## What was verified clean

Recorded so the contractor knows these were audited, not skipped.

- **C# `event` / `UnityEvent` subscription symmetry — clean across the board.**
  `BattleController` subscribes `ATBRuntimeState`'s three `UnityEvent`s in
  `OnEnable` and removes all three in `OnDisable`; `WalletConnectDialog`
  subscribes `WalletService.StatusChanged` in `Awake`/`SetWalletService` and
  removes it in `OnDestroy` *and* re-checks on re-injection; `TitleController`,
  `VillageHudController`, `BuildMenu` and `DevPanelController` all add their UI
  Toolkit `clicked` handlers in `OnEnable`/bind and remove them in `OnDisable`,
  several with an explicit `-=` guard against a double-`OnEnable`. `SplashLoading`
  adds `VideoPlayer.errorReceived` and removes it in a `finally`. `Enemy` and
  `WaveManager` unsubscribe per-enemy events on the death path. The one gap is
  MEM-004 (`WaveManager` has no teardown for the abnormal path).
- **Async discipline — no `async void`.** Every async flow returns `UniTask`;
  fire-and-forget sites use `.Forget()`. The gap is the missing
  `CancellationToken` (MEM-003), not the task shape.
- **`CancellationTokenSource` disposal — correct.** `StoryIntroController` and
  `SplashLoading` each create a `CancellationTokenSource`, cancel and `Dispose`
  it in `OnDisable`, and null the field. Textbook.
- **`UnityWebRequest` / file handles — correct.** `WaveDataLoader`,
  `DungeonLayoutLoader`, `LoreFragmentsLoader` (and the other StreamingAssets
  loaders) all use `using var req = UnityWebRequest.Get(...)`, so the request
  and its native download handler are disposed. `Theme` reads themes.json with
  `File.ReadAllText` (no open handle left dangling) inside a try/catch.
- **Native-object creation in editor scripts — correctly scoped + freed.**
  `SceneScreenshot` and `DragonPreview` create a `RenderTexture` + `Texture2D`,
  render, read pixels, then `DestroyImmediate` all temporaries —
  no leak. `AssetImportPostprocessor`, `KayKitMaterials`, the scene builders and
  `ExteriorTerrainBuilder` create `Material`s, but as **persisted assets**
  (`AssetDatabase.CreateAsset`) or scene-object materials authored at build
  time — editor-only, exempt per the audit brief. `OnboardingSceneBuilder`'s
  bumper `RenderTexture` is saved as an asset, not leaked.
- **`MaterialPropertyBlock` usage — correct and leak-free.** `Gate`
  (`_mpb = new MaterialPropertyBlock()`) and `PetDeployer.TintPlaceholder` tint
  via `MaterialPropertyBlock` on the *shared* material — `MaterialPropertyBlock`
  is a managed struct-like object, not a native asset, so it is GC-collected
  normally and leaks nothing. This is the correct pattern; `WandererBubble`
  (MEM-002) is the one place it was *not* used.
- **Static collections — bounded.** `Theme._catalog` (loaded once from
  themes.json, fixed 7-theme size), the editor builders' `_placeholders` /
  `_notes` / `_missingClips` static `List<string>` diagnostic buffers (editor-
  only, cleared per build run), and the various catalog classes are all
  fixed-size data loaded once — no unbounded-cache growth was found.
- **`PackStore` / `DevPanelController` `OwnedItemIds` writes — bounded.** Both
  `RecordOwned` helpers guard with `Contains` before `Add`, so the owned-items
  list cannot grow past the pack/cosmetic SKU set.

---

# Unity Memory Profiler plan — the Week-8 runtime leak check

Static analysis cannot prove the *absence* of a slow leak — it cannot see
`DontDestroyOnLoad` accumulation across many scene transitions, Addressables
groups failing to release on scene exit, or native objects orphaned by a code
path that never ran in review. The take-two-snapshots-and-diff workflow below is
the authoritative check and must run once a playable build exists (it is gated
on the same integration pass as the rest of the Week-8 work — see
`mobile-performance.md` §3.0 / P0-5).

## Precondition & tooling

- Install the **Unity Memory Profiler** package (`com.unity.memoryprofiler`) —
  separate from the built-in CPU/Rendering Profiler.
- Profile a **Development Build, IL2CPP, ARM64, Seeker_High** quality, installed
  on a real Seeker (or the emulator, accepting it hides GPU/thermal truth).
  Editor snapshots are useful for *diffing* leaks but the editor's own managed
  heap and asset cache distort absolute numbers — use the device for the
  400 MB gate verdict.
- Connect the Memory Profiler to the device session over adb/USB; confirm it is
  the **device** target, not the editor.

## What to capture

| Snapshot | When | Why |
|----------|------|-----|
| **A — idle village** | Village scene loaded, before Wave 1 starts | Baseline. Texture + mesh + managed heap floor. |
| **B — mid-wave peak** | During Wave 1 at peak enemy + VFX count (cast every ability) | Catches MEM-001 churn — compare GC alloc / object counts vs A. |
| **C — post-wave village** | Wave 1 cleared, back to idle village | Diff vs A — *should* match A. Any delta = enemies/VFX/materials not freed. |
| **D — in dungeon** | Healer's Cottage loaded, run live | Dungeon asset set resident; catches MEM-002 (`WandererBubble` material). |
| **E — back in village after dungeon** | Returned from dungeon to village | Diff vs A — dungeon assets must have *released* (Addressables). |
| **F — after a breach → ATB → return round-trip** | Village → breach → ATBBattle → return to village | Diff vs A — catches battle-scene + `WaveManager` round-trip retention (MEM-004). |

## The diff workflow (the core of the leak hunt)

1. **Capture snapshot A** (idle village baseline).
2. Play through the loop that you suspect leaks — a full wave (B), clear it (C).
3. **Capture the second snapshot** at the matching quiescent state.
4. In the Memory Profiler, open the **two snapshots side by side** and use the
   **Compare / Diff** mode. The "All Of Memory" and "Unity Objects" tables show
   per-type **deltas**: a positive delta in `Texture2D`, `Mesh`, `Material`,
   `GameObject`, `MonoBehaviour` or `ParticleSystem` count between two
   *equivalent* states is a leak.
5. **Pin the leak to a type, then to an object.** Select the leaked type, sort
   by count delta, pick an instance, and read its **References** panel — the
   inbound-reference chain shows *what is holding it alive*. A leaked `Material`
   held by a `MeshRenderer` on a destroyed-bubble object is exactly MEM-002; a
   leaked `Enemy`/`ParticleSystem` after a cleared wave is MEM-001 incompletely
   freed; a second `GameStateService` is MEM-007.
6. **Specifically diff for:**
   - **A vs C** — equal village states. Any `Enemy`, `ParticleSystem`,
     `GameObject` or `Material` delta means the wave did not fully clean up.
   - **A vs E** — confirms Addressables *released* the dungeon mesh/texture set
     on village re-entry (the dungeon pack must not stay resident).
   - **A vs F** — confirms the breach→ATB→return round-trip leaves no battle
     objects, no stale `WaveManager` subscriptions, no orphaned `BattleParams`
     retained by a still-running `.Forget()` UniTask (MEM-003).
   - **Managed heap growth** — watch the managed-heap total across A→C→E→F. A
     monotonic climb across equivalent states is a managed-reference leak (a
     static event subscriber — MEM-006 — or an undisposed token source).

## Targeted checks tied to this audit's findings

- **MEM-001:** In snapshot B vs A, watch `ParticleSystem` and `Enemy` object
  counts and the CPU Profiler's **GC Alloc** column during the wave. After the
  pooling fix, B should show a small *stable* pool count, not a count that
  tracks casts/spawns, and per-frame GC alloc should be near zero.
- **MEM-002:** In snapshot D, search the Unity Objects table for `Material` and
  look for one named like the URP/Unlit instance with no asset path (a code-
  created material) held by `WandererBubble`'s panel renderer. After the fix it
  should be gone (MaterialPropertyBlock) or destroyed with the bubble.
- **MEM-003 / MEM-004:** In snapshot F (post round-trip), confirm zero `Enemy`
  instances and zero `WaveManager`-rooted reference chains survive; confirm no
  `BattleState` / `BattleParams` is still reachable.
- **MEM-007:** In every snapshot, filter to `GameStateService` and `GameState` —
  the count must be exactly 1 each, always.

## On-device sustained run

Beyond the discrete diffs, record one **full 5-minute acceptance playthrough**
with the CPU Profiler attached and watch the **Total Reserved** memory line and
the GC spike markers. The pass criteria: Total Reserved ≤ 400 MB at every point
(idle village, mid-wave, in dungeon), and **no frame > 33 ms** — a GC collection
showing as a spike in the frame chart is a direct fail of the spike gate and
points straight back at MEM-001.

---

## Closing summary

No P0 leak — the project will not fall over from a memory fault. The four P1
items are real and all sit in the runtime gameplay path: fix the un-pooled
spawn/cast churn (MEM-001), the one genuine native `Material` leak in
`WandererBubble` (MEM-002), thread cancellation tokens through the
fire-and-forget UniTasks (MEM-003), and give `WaveManager` a teardown pass
(MEM-004) — and do all four *before* the Week-8 profiling run so the baseline is
honest. The five P2 items are hardening and verification notes. The
take-two-snapshots-and-diff workflow above is the only thing that can confirm the
≤ 400 MB gate and catch what static analysis cannot — it is on the critical path
for Week-8, not a nice-to-have.

_Tend the Heart. Hold the dark. Free what you allocate._
