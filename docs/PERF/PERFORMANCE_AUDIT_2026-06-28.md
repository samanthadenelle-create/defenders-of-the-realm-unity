# Performance Audit — Live V1 Path (2026-06-28)

**Scope:** boot → MainCastle_Hall hub → OuterWorld → BattleArena fight → return.
Deprecated Village/ATB/tower V2 code excluded. **Method:** static analysis with
exact `file:line` citations (no profiler pixels — code-read against the §1 hot-loop /
§2 GC / §3 render / §4 UI / §5 startup / §6 physics checklist). READ-ONLY — no edits made.

---

## SINGLE BIGGEST WIN

**Enemy render cost — no mesh-combine + per-instance materials.** Every Tripo enemy
is instantiated as its full authored multi-SkinnedMeshRenderer / multi-material rig
and, on the orc/troll/demon path, gets a freshly-allocated **unshared** `new Material`
per renderer-slot per spawn. With the OuterWorld continuously maintaining ~6 reps and
the arena staging a 3–7-body family, draw calls + skinning + native-material memory all
scale linearly with (renderers × live bodies) and **SRP batching is broken** because no
two orcs share a material. Fixing this (offline Mesh-Baker combine + a shared-material
cache) is the highest-leverage change and is reuse-first — see **P0-1** and **P0-2**.

---

## P0 — Frame-rate killers

### P0-1. Enemies are never mesh-combined (the planned "Mesh Baker 6→1" is unimplemented)
- **Where:** `Assets/_Modules/Village/VisualFactory.cs:91-162` (`Skin` does a plain
  `Object.Instantiate(prefab, host)` and nothing else). No `CombineMeshes` / Mesh Baker
  call exists anywhere in `Assets/_Modules` (grep: only doc/editor references).
- **Moment:** every enemy spawn — OuterWorld reps (RepCount 6) + arena family.
- **Cost:** each body renders as its authored set of SkinnedMeshRenderers + materials;
  draw calls and GPU skinning scale with renderer count × live bodies. This is the
  dominant crowd cost and the memory `tripo-roster` "Mesh Baker 6→1 per char" intent is
  **planned, not applied**.
- **Fix (reuse-first):** run the offline Mesh-Baker combine on each Tripo enemy prefab in
  `Resources/Enemies` (one combined SkinnedMeshRenderer + atlas per character). No runtime
  code change — `VisualFactory.Skin` then instantiates a single-renderer body.

### P0-2. `TripoMaterialFixer` allocates `new Material(...)` per slot per spawn, unshared
- **Where:** `Assets/_Modules/Core/TripoMaterialFixer.cs:167` (`var newMat = new Material(lit)`
  inside the per-renderer / per-slot loop). Driven for the whole enemy roster via
  `Assets/_Modules/Village/Enemies/EnemyFactory.cs:116,134` (`FixTripoMaterials`).
- **Cost:** every orc/troll/demon instance gets unique materials → (a) SRP batching can
  never coalesce identical orcs, (b) native material memory churns as
  `OverworldEncounterSpawner` MaintainLoop re-tops the 6 reps and the arena re-stages
  families (materials are not `Destroy`-ed on death → accumulate until
  `Resources.UnloadUnusedAssets`).
- **Fix (reuse-first):** cache + reuse one shared `Material` per `(shader, texture, tint)`
  key in a small static dictionary inside `TripoMaterialFixer` so all `orc-warrior` bodies
  share one material — restores batching and kills the churn. No prefab change.

---

## P1 — Large hitches / sustained combat GC

### P1-1. OuterWorld additive load is SYNCHRONOUS + heavy terrain diagnostics on every load
- **Where:** `Assets/_Modules/Village/World/WorldSceneLoader.cs:193`
  (`SceneManager.LoadScene(OuterWorldSceneName, LoadSceneMode.Additive)` — blocking).
  Compounded by `DiagTerrain` at `WorldSceneLoader.cs:66-151` running on every OuterWorld
  `sceneLoaded`: `FindObjectsByType<Light>`, a `SampleHeight` loop, `td.GetAlphamaps(...)`
  and a possible full `td.SetAlphamaps(...)` repaint — left-in DEF-108 diagnostic code.
- **Cost:** a guaranteed multi-hundred-ms frame stall at the moment of world entry (the
  whole OuterWorld terrain/regions/mine-nodes load on the main thread), plus the alphamap
  repaint hitch.
- **Fix:** switch to `LoadSceneAsync(..., LoadSceneMode.Additive)` (the loader is already
  event-driven, an async handle fits); gate `DiagTerrain` behind a debug flag or remove now
  that DEF-108 is closed.

### P1-2. `BattleHud9Zone` rebuilds strings / re-meshes TMP every frame DURING the fight
- **Where:** `Assets/_Modules/Village/Arena/BattleHud9Zone.cs`
  - `Update:317-328` — fans out to all Push* methods every frame with **no top-level
    dirty-check**.
  - `PushStarConditions:496-504` — `m:ss` clock + 5-segment string concat to `.text` every
    frame (value changes ~1×/sec).
  - `PushBottomCenter:1049-1073` — `new StringBuilder` + `m:ss` concat every frame.
  - `PushTarget:506-568` — `GetComponentInParent<Enemy>()` every frame + `Hp.ToString()` +
    `"Lv "+…` + a `new StringBuilder()` threat loop, re-assigned to `.text` unconditionally.
  - `PushTargetCycle:663-721` — per row `name.Replace("(Clone)","").Trim()` (~2-3 string
    allocs × ≤4 rows/frame); `RebindFamily:702` does `FindObjectsByType<Enemy>` + `Array.Sort`
    every 0.3s.
  - `PushAbilityCooldowns:1109-1180` — `CeilToInt(remaining).ToString()` + `Disc.color`
    write per cooling button every frame.
- **Cost:** ~30-50 string allocations/frame plus repeated TMP mesh regeneration during the
  most GPU-busy moment (the battle) — steady GC pressure → stutter.
- **Fix:** apply the proven `VillageHudController` `_lastTimerTotal` dirty-check pattern —
  cache the last displayed int/string per field and only touch `.text` when it changes;
  reuse one member `StringBuilder`; cache the cleaned row name until `row.Tracked` changes.

---

## P2 — Per-frame waste (cumulative)

### P2-1. `HeroTargetIndicator` — 2× `WorldToScreenPoint` + string build every frame for a THROTTLED log
- **Where:** `Assets/_Modules/Village/Hero/HeroTargetIndicator.cs:429-449` (in `LateUpdate`
  whenever a target exists). The `FlowTrace.Throttle` only emits ~1/sec but its **arguments
  are evaluated every frame**: two `_cam.WorldToScreenPoint(...)` projections + several
  `ToString` + a long concat.
- **Fix:** compute the message only when the throttle will actually fire (gate on the next-log
  time), or remove the slice-1 instrument the comment flags as temporary.

### P2-2. `RepEngageWatcher` calls `FindWithTag("Player")` every frame, per rep (×6)
- **Where:** `Assets/_Modules/Village/Enemies/OverworldEncounterSpawner.cs:501` (per-frame
  tick; also `:681/:689` in Engage). 6 reps → 6 managed scene scans/frame in the overworld.
- **Fix:** resolve the `Player`-tagged hero once (cache the `Transform`, refresh on
  `sceneLoaded`/when null) — it is a singleton (CLAUDE.md §7), one cached ref serves all reps.

### P2-3. `HeroLocomotion` — unbounded `FindObjectOfType` every frame while null + per-frame reflection
- **Where:** `Assets/_Modules/Village/Hero/HeroLocomotion.cs:564` (`TryResolveWaveManager`)
  and `:636` (`FindObjectOfType<SmartMobileCamera>()`) run every frame until resolved — in a
  hub with no WaveManager they run forever. `:1087-1102` (`ReadHudDpadMove`) does
  `Type.GetType` + reflection every frame via `ReadMoveInput:1058`.
- **Fix:** back off with a retry timer (the `HeroHealthBootstrap` 0.5s pattern); cache the
  resolved `Type`/`PropertyInfo` (or a "not present" flag) once.

### P2-4. `SmartMobileCamera` — per-frame uncached `GetComponent` + `Camera.allCameras` alloc
- **Where:** `Assets/_Modules/Village/Hero/SmartMobileCamera.cs:958` (`GetHeroVelocity` does
  `_target.GetComponent<HeroLocomotion>()` every `LateUpdate`); `:548` (`EnforceSoleCamera`
  runs unconditionally every `LateUpdate`, and `Camera.allCameras` allocates a `Camera[]`
  each access — there is already a 1-sec timer pass at `:677`).
- **Fix:** cache `HeroLocomotion` when `_target` is set (already resolved at `:516`); drop the
  per-frame `EnforceSoleCamera()` at `:548` and rely on the existing timer + scene-load hook.

### P2-5. `HeroAbilities` authored cast VFX is `Instantiate`+`Destroy` per cast (not pooled)
- **Where:** `Assets/_Modules/Village/Hero/HeroAbilities.cs:890-899` (`SpawnVfx` when
  `_castVfxPrefab` is assigned). Per-cast (not per-frame), but churns GC + a destroy spike on
  a fast-casting hero. The default (no prefab) path correctly routes through the pooled
  `VFXManager`/`AbilityVfxKit`.
- **Fix:** route the authored prefab through the same `VFXManager` pool instead of raw
  Instantiate/Destroy.

### P2-6. `HeroHealth.TakeDamage` builds an un-throttled FlowTrace string every hit
- **Where:** `Assets/_Modules/Village/Hero/HeroHealth.cs:253-255` (interpolated string built
  on every `TakeDamage`, regardless of trace enabled). The comment marks it a temporary
  HP-desync probe.
- **Fix:** guard with `if (FlowTrace.Enabled)` / throttle, or remove the probe.

### P2-7. `BattleArena.WatchToResolution` calls `FindWithTag` every frame until disband
- **Where:** `Assets/_Modules/Village/Arena/BattleArena.cs:1068-1135` (per-frame `yield return
  null` loop) → `MaybeDisbandOnArrival:869` does `GameObject.FindWithTag("Player")` each frame
  until `_familyEngaged` latches. (The `RemoveAll` lambda at `:1084` is capture-free → compiler
  caches it, so no alloc there.)
- **Fix:** cache the hero transform once when the battle stages; reuse it in the disband check
  and the resolve warps (`:965/:1024/:1047/:1102/:1459`).

### P2-8. `VFXManager` procedural fallback is unpooled
- **Where:** `Assets/_Modules/Village/Vfx/VFXManager.cs:862-866` (`ProceduralLoopFallback`
  does `new GameObject` + `AddComponent<ParticleSystem>` per call) — taken only for `VFXType`s
  with no wired prefab. Also 3 separate `GetComponentsInChildren<ParticleSystem>` walks per
  pooled play (`:546,:608,:628`).
- **Fix:** ensure combat-relevant `VFXType`s are wired in `Resources/VFX/VFXCatalog` so the
  pool path is taken (or route combat impacts through the already-correct `VfxPool`); cache the
  particle-system array on the pooled instance.

---

## P3 — Nice-to-haves

- **`EnemyFactory.Build` redundant hierarchy walks** — `VisualFactory.cs:176,235` +
  `EnemyFactory.cs:386,408` do ~4-5 `GetComponentsInChildren<Renderer>` walks per spawn.
  Spawn-time only; compute the renderer set/bounds once in `Skin` and pass them out.
- **`FloatingHealthBar.ApplyScaleCompensation` every frame per visible bar** —
  `Assets/_Modules/Village/Combat/FloatingHealthBar.cs:484` (and `:293`) recompute per-axis
  inverse scale + 2 transform writes each frame while a bar is visible (gated off when faded,
  `:448`). Cheap per bar but ×N enemies in combat. Could run only on a scale-change check.
- **`DamageNumberSpawner` mutates the TextMesh shared font material's `renderQueue`** —
  `Assets/_Modules/Village/Enemies/DamageNumberSpawner.cs:254,289` (`sharedMaterial.renderQueue
  = 4000`). Effectively one-time but writes the SHARED font material on each Build; set once.
- **`VillageHudController` reads `SceneManager.GetActiveScene().name` per frame** —
  `Assets/_Modules/HUD/VillageHudController.cs:1056,1088` (fresh string alloc + compare each
  frame). Cache the scene name on `sceneLoaded`.
- **`CompassHud` writes `_compassLabel.text` every frame even when the cardinal is unchanged**
  — `Assets/_Modules/HUD/CompassHud.cs:225`. Cache `_lastHeading`, assign on change only.

---

## GOOD NEWS — already optimized (do NOT re-touch)

- **VFX hit/death are pooled and live-used.** `Assets/_Modules/Village/Vfx/VfxPool.cs:345-390,
  461-488` — code-built pool, pre-warmed, color via cached `MaterialPropertyBlock` + cached
  renderers, **zero per-frame alloc, no per-instance material**. Consumed by `Enemy.cs`/`WaveManager.cs`.
- **Projectiles are pooled.** `Assets/_Modules/Village/Hero/MoverProjectilePool.cs:95-115,
  183-221` (per-kind queues, visual built once, replayed on lease). Used by `RangedAttackVFX`/`ProjectileMover`.
- **Damage numbers are pooled.** `Assets/_Modules/Village/Enemies/DamageNumberSpawner.cs:159-209`
  (SetActive cycle under a DontDestroyOnLoad root; `LateUpdate` is pure local math, no alloc).
- **JSON catalogs are cached, not re-parsed.** `Core/Data/CanonicalJson.cs` →
  `LocalJsonCatalogSource.cs:31-36` loads `Resources.Load<TextAsset>` (Unity-cached) and every
  consumer caches the PARSED object in a static guarded field (e.g. `AbilityCatalog.cs:170,264`).
- **NavMesh pathing is throttled.** Enemy re-path is throttled (DEF-56,
  `Enemy.cs:950-961`) and the Rush path-validity check reuses one pooled `NavMeshPath` on a
  ~2s cadence (WO-410, `EnemyBrain.cs:249-258,689-699`) — the former #1 GC source is fixed.
- **Physics sweeps use `OverlapSphereNonAlloc` into shared buffers** across `EnemyBrain`
  (`_scanBuffer[32]`), `Enemy` (`_structureScanBuffer[16]`), `HeroAbilities` (`_overlap[64]`),
  `HeroHealth` (`_buf[24]`), `SmartMobileCamera` — no per-scan array alloc.
- **`HeroAbilities.Update`** (`:212-220`) is mana regen + a 4-element cooldown loop only —
  no finds, no alloc. **`XPBarController`/`FloatingXpText`/`BattleArenaHud`** are
  event-driven / dirty-checked / pooled — clean.

---

## Suggested order of attack
1. **P0-2** shared-material cache in `TripoMaterialFixer` (code-only, restores SRP batching + kills churn).
2. **P0-1** offline Mesh-Baker combine on enemy prefabs (asset bake, biggest draw-call win).
3. **P1-1** async OuterWorld load + remove `DiagTerrain` (kills the hub-entry stall).
4. **P1-2** dirty-check the `BattleHud9Zone` Push* methods (kills combat GC stutter).
5. **P2** batch: cache hero `Transform`/components (P2-2/3/4/7), strip leftover per-frame
   instrument strings (P2-1/6).
