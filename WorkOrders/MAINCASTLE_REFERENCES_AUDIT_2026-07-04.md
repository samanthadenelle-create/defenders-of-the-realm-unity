# MainCastle_Hall + OuterWorld → `Main_Castle_Overworld` — Exhaustive Reference Audit & Remediation Plan

**Date:** 2026-07-04 · **Author:** Senior Unity Architect (read-only audit) · **Executes:** CLI
**Companion WO:** `WorkOrders/WORK_ORDER_608_world_merge_single_scene.md` (+ folded WO-609 spawning half)
**Status:** READ-ONLY audit. NO code was edited. This file is the CLI's stacked worklist.

---

## 0. TL;DR — the failure mode this audit prevents

The merge collapses BOTH old scenes (`MainCastle_Hall` **and** `OuterWorld`) into ONE scene
`Main_Castle_Overworld`. Every place in the solution that gates behavior on the **exact old scene name**
(`== "MainCastle_Hall"`, `== "OuterWorld"`, `IndexOf("OuterWorld")`, `StartsWith("MainCastle"/"Castle")`)
will **silently return false on the merged scene**. A missed gate = a **silent hub/overworld feature break**
on `Main_Castle_Overworld`: no vendors, no barracks NPC, no ambient VFX, spawn-capsule visible, no wave
spawn points, wrong music context, reps never engage, no harvest workers, no camps/outposts, missing world
boundary. None of these throw — they just do nothing. **The whole point of this list is that NOTHING is left
gating on an exact old name that the merged scene doesn't match.**

The **good news** (verified from code): the central predicate `DeNelle.Core.HubScenes.IsHub` already lists
`"Main_Castle_Overworld"` (HubScenes.cs:25) and matches by exact-or-`Contains`, so **every gate already routed
through `HubScenes.IsHub` is AUTOMATICALLY covered** (CraftingStationInjector, JewelerStationInjector,
SafeZoneRecovery, RuntimeRegionGate, WorldSceneLoader, OutpostVictoryController, GameOverScreen, RaidEntryBridge,
TutorialDirector, companion introducers, WaveManager's hub test, etc.). The residual work is the handful of
gates that still hardcode a raw string instead of the predicate.

---

## 1. Reference counts by category

Raw string occurrences: **~2,170 across 284 files** (dominated by comments + `QA_F8_ARCHIVE/*.jsonl` logs +
dated `.md` ledgers — all NON-load-bearing). Load-bearing runtime/config references that actually matter:

| Category | Count (files) | Action |
|---|---|---|
| **BOOT/ROUTER** | 4 | Mostly IN-FLIGHT (routing agent). Verify only. |
| **HUB-BEHAVIOR GATE — castle (BROKEN on merged)** | 4 critical + 5 secondary | **CLI must fix** |
| **OVERWORLD-BEHAVIOR GATE (BROKEN on merged)** | 6 | **CLI must fix** |
| **EDITOR-BUILDER (legacy two-scene authoring)** | ~30 | **LEAVE** |
| **TEST** | 6 | LEAVE (still pass); optional add-assert |
| **DOC** | ~60 | LEAVE / banner later (cosmetic) |
| **SCENE/ASSET/BUILD-SETTINGS** | 5 | Verify (routing agent owns Build Settings) |
| **RETURN-TARGET** | 5 | **CLI must repoint** (flag-gated) |
| **DATA (json)** | 4 | Fix/verify |
| **ALREADY-HANDLED (in-flight, DO NOT double-fix)** | 9 | Skip |

---

## 2. ALREADY HANDLED — DO NOT TOUCH (in-flight routing / Lane-B / already merged-aware)

Verified merged-aware in the current tree — the CLI must **NOT** re-edit these:

| File:line | What's already done |
|---|---|
| `Assets/_Modules/Core/HubScenes.cs:25` | `Names[]` **includes** `"Main_Castle_Overworld"`; `IsHub` matches exact-or-Contains → all IsHub callers covered. |
| `Assets/_Modules/Core/SceneRouter.cs:125-132` | `Castle` is a **flag-aware property** (returns merged name when `ff.mergedworld`, else `MainCastle_Hall`). Routing agent owns. |
| `Assets/_Modules/DevTools/AutoPilotDriver.cs:96-100` | `TargetScene` flag-aware (merged vs legacy). Routing agent owns. |
| `Assets/_Modules/Village/World/WorldSceneLoader.cs:162` | Skips additive OuterWorld load when active scene == `Main_Castle_Overworld` (`MergedWorldSceneName`). Lane-B. |
| `Assets/_Modules/Village/World/RuntimeRegionGate.cs:98,115` | Special-cases `Main_Castle_Overworld` + retires the castle→OuterWorld crossing under `ff.mergedworld`. Lane-B. |
| `Assets/_Modules/Village/World/CastleMoatBuilder.cs:145-213` | `MergedScene` const + `MergedBridgesOnly()`; builds 4 bridges only on merged scene. §9 bottleneck agent owns. |
| `Assets/_Modules/Village/World/CastleBeamHider.cs:37-42` | `MergedTargetScene` + `IsCastleHubScene()` helper. Done. |
| `Assets/_Modules/Village/Waves/CastleSpawnPointInjector.cs:50-56` | `MergedTargetScene` + `IsCastleHubScene()` helper. Done. |
| `Assets/_Modules/Village/World/WorldFeelInjector.cs:87` | `OutdoorScenes[]` includes `Main_Castle_Overworld` (IsOutdoor covered — but see §4.15 `openWorld` residual). |
| `Assets/Resources/Data/region-gates.json:2-50` | Rows carry `retiredOnMergedWorld` intent; runtime skip lives in RuntimeRegionGate (above). Data left intentionally. |
| `Assets/_Modules/Core/FeatureFlags.cs` | `MergedWorld` flag is CLI-owned per WO-608. |
| `Assets/Editor/WorldMergeBuilder.cs` | The NEW merge orchestrator itself. |

---

## 3. CASTLE (`MainCastle_Hall`) references

### 3A. BOOT/ROUTER — verify only (in-flight)
- `SceneRouter.cs:132`, `AutoPilotDriver.cs:100`, `HeroSelectController.cs:586` (`GoCastle()` → routes to `SceneRouter.Castle`, auto-follows the property), `GameStateService.cs:126` (bypass paths reach the hub) — all follow `SceneRouter.Castle`. **No edit** once the property flips.

### 3B. HUB-BEHAVIOR GATE — **CRITICAL, BROKEN on merged** (gate on `== "MainCastle_Hall"` only)
These self-boot injectors gate on a single `TargetScene = "MainCastle_Hall"` and will **NOT fire** on
`Main_Castle_Overworld` → the castle loads with the feature dead:

| # | File:line | Gate | Merged-scene failure |
|---|---|---|---|
| 1 | `Assets/_Modules/Village/NPCs/CastleVendorNpcInjector.cs:156,167,234,281` | `GetActiveScene().name == TargetScene` | **No storefront vendor NPCs** in the castle |
| 2 | `Assets/_Modules/Village/NPCs/BarracksNpcInjector.cs:74,85` | `== TargetScene` | **No barracks NPC** |
| 3 | `Assets/_Modules/Village/HubAmbientVfxInjector.cs:132,143` | `== TargetScene` | **No ambient hub VFX** (torches/dust/tree glow) |
| 4 | `Assets/_Modules/Village/Hero/CastleSpawnMarkerHider.cs:78,89` | `== TargetScene` | **Raw white spawn CAPSULE visible** (the pill bug returns) |

### 3C. HUB-BEHAVIOR GATE — secondary (miss merged, degraded not dead)

| # | File:line | Gate | Merged failure |
|---|---|---|---|
| 5 | `Assets/_Modules/Audio/AudioService.cs:923` | `sceneName == "MainCastle_Hall" \|\| "CastleHub" \|\| "CastleHub_MainKeep"` | Wrong/again-triggering **music context** on merged scene |
| 6 | `Assets/_Modules/Village/Hero/HeroEquipHud.cs:54` | `n == "MainCastle_Hall" \|\| "Village2" \|\| "CastleHub"` | **Equip HUD** enable check misses merged |
| 7 | `Assets/_Modules/Core/GroundZFightFixer.cs:412,421` | `StartsWith("MainCastle")` / `StartsWith("Castle")` | **Z-fight fixer skips** merged (`"Main_Castle_Overworld"` starts with neither) |
| 8 | `Assets/_Modules/Village/BuildMode/BaseLayoutLoader.cs:55-56` | pure-hub list `{"MainCastle_Hall","CastleHub"}` | BaseLayout may **wrongly load** in the merged hub |
| 9 | `Assets/_Modules/Village/World/WorldFeelInjector.cs:340` | `openWorld = active.StartsWith("OuterWorld")` | Open-world **feel/fog toggle** false on merged (IsOutdoor is fine; this specific toggle isn't) |

### 3D. EDITOR-BUILDER — **LEAVE** (legacy two-scene authoring; never runs at play)
`CastleHubBuilder.cs` (~85 hits, incl. the batch entries that open/save `MainCastle_Hall.unity`),
`CastleBlueprint.cs`, `CastleGateNavVerify.cs`, `CastleOffsetCapture.cs`, `CastleNavPlaneScrub.cs`,
`CastlePlaceCrossing.cs`, `CastleTroopWallNav.cs`, `CastleWallsFromRecipe.cs`, `CastleWallKitSpawner.cs`,
`CastleWallStairsSeatFix.cs`, `MainCastleFloorFix.cs`, `MainCastlePropSeatFix.cs`,
`MissingPrefabInstanceCleaner.cs`, `WallPreview.cs`, `WallTools/*` (GridWallBuilder, CastleBarracksPlacer,
PerimeterWallGenerator, RaidBaseGenerator), `SceneScreenshot.cs`, `RegressionSuite.cs`,
`MagentaMaterialScanner.cs`, `StairwayBuilder.cs`.
**Why leave:** they author/repair the *saved* `MainCastle_Hall.unity`, which remains the SOURCE the merge reads
(WO-608 §STRATEGY: never regen the hand-dialed hub). Editing them adds risk with zero runtime benefit. The
merged scene is produced by `WorldMergeBuilder`, not these.

### 3E. TEST — **LEAVE** (assertions still hold)
`HubScenesTest.cs`, `CompanionEntryHubGateTest.cs`, `TutorialDirectorHubGateTest.cs`,
`CastleCompanionIntroducerTest.cs`, `ModalPanelDisciplineTests.cs`. They assert `IsHub("MainCastle_Hall")==true`
(still true) and rely on `HubScenes`. Optional hardening: add `Assert.IsTrue(HubScenes.IsHub("Main_Castle_Overworld"))`.

### 3F. DIAGNOSTIC (gate on `== "MainCastle_Hall"`) — low priority
`CastleNavTopologyDiag.cs:36`, `FloorDeepDiag.cs:32`, `PlayerBot.cs:43` (`scene != "MainCastle_Hall" && != "Village2"`).
These only affect whether a *diagnostic* runs on the merged scene. Broaden if you want merged-scene diag coverage;
harmless if skipped. Not a player-facing break.

---

## 4. OUTERWORLD (`OuterWorld`) references

### 4A. OVERWORLD-BEHAVIOR GATE — **BROKEN on merged** (the real breakage surface, WO-608 §Lane-C)
⚠ **Naming trap:** the merged scene is `Main_Castle_Overworld` — note **"Over**world**"**, but these gates look
for **"Outer**World**"**. `IndexOf("OuterWorld")` and `== "OuterWorld"` **DO NOT MATCH** `Main_Castle_Overworld`.
So every one of these silently returns false → the overworld half of the merged scene is inert:

| # | File:line | Gate | Merged failure |
|---|---|---|---|
| 10 | `Assets/_Modules/Village/Enemies/OverworldEncounterSpawner.cs:133` | `s.name.IndexOf("OuterWorld", …) >= 0` | **Reps never engage** — no overworld encounters |
| 11 | `Assets/_Modules/Village/Harvest/WorkerManagerBootstrap.cs:85,90` | `scene.name == "OuterWorld"` | **No harvest workers / MineNode dispatch** |
| 12 | `Assets/_Modules/Village/World/Camps/CampSystem.cs:201` | `IndexOf("OuterWorld", …) >= 0` | **No camps** spawn |
| 13 | `Assets/_Modules/Village/World/Camps/RaidOutpostSystem.cs:267` | `IndexOf("OuterWorld", …) >= 0` | **No raid outposts** spawn |
| 14 | `Assets/_Modules/Village/World/OuterWorldBoundaryInjector.cs:39` | `TargetScene="OuterWorld"`, gated on `isLoaded` | **No world-boundary wall** (OuterWorld never separately loads on merged) |
| 15 | `Assets/_Modules/Village/World/Camps/OutpostVictoryController.cs:114` | `IndexOf("OuterWorld")` **inside** `IsOuterWorldLoaded()` | Partially saved by the `\|\| HubScenes.IsHub` on line 95 (merged is IsHub) — **lower risk**, still repoint for correctness |

### 4B. BOOT/ROUTER + additive load — IN-FLIGHT (verify only)
- `WorldSceneLoader.cs:27,190-207` — additive-loads `OuterWorld`; already skipped on merged (§2). **No edit.**
- `RuntimeRegionGate.cs:53,227,248` — OuterWorld AI-link + gate build; merged retire handled (§2). **No edit.**

### 4C. EDITOR-BUILDER — **LEAVE** (produce the saved `OuterWorld.unity` the merge consumes)
`OuterWorldBuilder.cs`, `OuterWorldNavBake.cs` (⚠ the `EnsureCastleNavHole` ±62 blanket carve is explicitly
replaced by WorldMergeBuilder's moat-basin-only volume — WO-608; do NOT run the solo bake on the merged scene),
`OuterWorldCavePortalBuilder.cs`, `OuterWorldCleanWoundCrack.cs`, `ExteriorTerrainBuilder.cs`,
`WorldBakeOrchestrator.cs`, `Village2Build.cs`, `Village2Playable.cs`, `Village3Builder.cs`,
`Village2OutpostFinalize.cs`, `CastleGateNavVerify.cs`, `CastleBlueprint.cs`, `CastlePlaceCrossing.cs`,
`ProceduralSiegeArenaBuilder.cs`. Editor-time only; source of the merge inputs.

### 4D. RETURN-TARGET — **CLI must repoint** (flag-gated to merged)
The destination a dungeon/outpost/arena/seam sends the player to must land on `Main_Castle_Overworld` when
`ff.mergedworld` is ON, not the retired standalone scenes:

| # | File:line | Current target | Fix |
|---|---|---|---|
| 16 | `Assets/Editor/DungeonChainBuilder.cs:220` | `targetScene: "MainCastle_Hall"` (dungeon "Return Home") | Repoint to merged (or route via `SceneRouter.Castle`). Editor-baked into the return trigger → needs re-bake OR runtime remap (§5). |
| 17 | `Assets/_Modules/Core/Arena/ArenaContracts.cs:80` | `Scene = "OuterWorld"` (default battle-return) | Return scene is normally set to where-engaged; default should be the merged name. |
| 18 | `Assets/_Modules/Village/World/SceneTransitionTrigger.cs:28` | `targetSceneName = "OuterWorld"` (default) | Seam trigger to OuterWorld is **obsolete** on merged (content in-scene). See §5 runtime remap. |
| 19 | `Assets/Editor/CastleHubBuilder.cs:2164`, `GarrisonSceneBuilder.Scenes.cs:633`, `EnemyStrongholdBuilder.cs:873` | editor-author `targetSceneName="OuterWorld"` into triggers | Editor-baked; legacy path. Leave the builders; handle via runtime remap for the merged scene. |
| 20 | `Assets/_Modules/Core/World/SceneLink.cs:7` + `Assets/Resources/Data/scene-links.json:3` | `castle_to_outerworld` seam (`fromScene:"MainCastle_Hall" toScene:"OuterWorld"`) | Obsolete on merged; retire the row for `ff.mergedworld` (mirror region-gates treatment). |

Display-only (no functional break): `SceneTransitionTrigger.cs:406,410` (`case "OuterWorld"`/`"MainCastle_Hall"` →
prompt text). Cosmetic; update alongside §5 if desired.

### 4E. TEST — **LEAVE**
`CompanionEntryHubGateTest.cs:41`, `HubScenesTest.cs:27` assert `IsHub("OuterWorld")==false` (still true — OuterWorld
is not, and the merged scene is a distinct name). `EncounterArchitectureTests.cs:58,65` assert battle return-scene
plumbing with `"OuterWorld"` as a sample value — still valid.

### 4F. MISC runtime (OuterWorld builder artifacts) — no gate, LEAVE
`MineNodeVisual.cs`, `CrystalMineNode.cs`, `OutpostMaterialFixInjector.cs`, `WorldMusicDirector.cs`
(uses a boundary crossing via `_lastInWorld`, computed from position, not scene name — OK) — reference
OuterWorld-baked object names/comments; they act on objects present in the merged scene too. No change.

---

## 5. SCENE / ASSET / BUILD-SETTINGS / DATA

| File:line | Note | Action |
|---|---|---|
| `ProjectSettings/EditorBuildSettings.asset:33` (`MainCastle_Hall.unity`) + `:72` (`Main_Castle_Overworld.unity`) | Both registered. | Ensure `Main_Castle_Overworld` **enabled + index 0 (primary start)**; keep `MainCastle_Hall`/`OuterWorld` during transition. Routing agent / boot owns. |
| `Assets/Scenes/MainCastle_Hall.unity`, `Assets/Scenes/OuterWorld.unity` | The legacy source scenes. | **KEEP** until merged scene is fully verified (they are the merge inputs; §3 STRATEGY). |
| `Assets/Scenes/Main_Castle_Overworld.unity` | The new merged scene (present). | Target. |
| `Assets/Resources/Data/scene-links.json:3` | `castle_to_outerworld` seam row. | Retire for `ff.mergedworld` (see #20). |
| `Assets/Resources/Data/region-gates.json` + `Assets/StreamingAssets/…` mirror | castle→outerworld rows. | Runtime skip handled (§2). Leave data. |
| `Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json` + StreamingAssets mirror (`scene:"MainCastle_Hall"` ×7) | TutorialDirector activates via `HubScenes.IsHub` (covered), but each step carries `scene:"MainCastle_Hall"`. | **VERIFY** whether the step runner gates per-step on exact `scene` name; if so, broaden to accept the merged name (or drop the field). |
| `Assets/Resources/Data/Canonical/scene-configs.json` (+ mirror) | No `MainCastle_Hall`/`Main_Castle_Overworld` entry → defaults to NOT-enemy-owned = correct HUD. | Optional: add explicit `Main_Castle_Overworld` (ownership `Home`) for clarity. Not required. |

---

## 6. ARCHITECT REMEDIATION — the exact fix per category

### Fix pattern A — castle hub injectors (§3B, items 1–4)
Mirror the already-shipped `CastleBeamHider`/`CastleSpawnPointInjector` pattern **exactly** (consistency, and it
keeps these castle-only so they never fire in Village2):
```
private const string MergedTargetScene = "Main_Castle_Overworld";
private static bool IsCastleHubScene(string n) => n == TargetScene || n == MergedTargetScene;
```
Then replace every `SceneManager.GetActiveScene().name == TargetScene` / `scene.name == TargetScene` /
`!= TargetScene` with `IsCastleHubScene(...)` (negated where appropriate). Do NOT delete `TargetScene`.
Rationale: castle-specific chrome must appear on the merged scene AND still be excluded from Village2/raids.

### Fix pattern B — overworld systems (§4A, items 10–15): ONE shared predicate
Add a single Core-clean predicate and route all overworld gates through it (kills the "OuterWorld" vs
"Overworld" trap permanently, and is the seam WO-609's `WorldPopulationDirector.InWorld` will later own):
```
// DeNelle.Core.HubScenes
public static bool IsOverworld(string n) =>
    !string.IsNullOrEmpty(n) &&
    (n.IndexOf("OuterWorld", StringComparison.OrdinalIgnoreCase) >= 0 ||
     n == "Main_Castle_Overworld");
```
Repoint `OverworldEncounterSpawner`, `WorkerManagerBootstrap`, `CampSystem`, `RaidOutpostSystem`,
`OutpostVictoryController.IsOuterWorldLoaded`, and `OuterWorldBoundaryInjector` to it. For the boundary injector,
also allow the merged scene to be the ACTIVE scene (it won't be "loaded additively" — it IS the scene).
Rationale: single source of truth; `Contains`-based so future renames don't re-break it.

### Fix pattern C — secondary hub gates (§3C, items 5–9)
- **AudioService.cs:923 / HeroEquipHud.cs:54 / BaseLayoutLoader.cs:55:** simplest correct fix = route through
  `HubScenes.IsHub(sceneName)` (already merged-aware) instead of the inline `==` chain — but confirm each
  intends *hub* semantics (AudioService/HeroEquip do; BaseLayoutLoader wants pure-hubs, so just add
  `"Main_Castle_Overworld"` to its list to keep BaseLayout OUT of the merged hub).
- **GroundZFightFixer.cs:412,421:** add `n.StartsWith("Main_Castle")` (note underscore) or an explicit
  `n == "Main_Castle_Overworld"` to both predicates.
- **WorldFeelInjector.cs:340:** change `openWorld = active.StartsWith(OuterWorldPrefix)` to also accept the
  merged name (or reuse `HubScenes.IsOverworld`).

### Fix pattern D — return-targets (§4D, items 16–20)
The player-facing risk: a dungeon/outpost/arena that returns to `"MainCastle_Hall"` or a seam that warps to
`"OuterWorld"` will, under `ff.mergedworld`, load a **retired** scene. Two options:
1. **Runtime remap (preferred, no re-bake):** in `SceneTransitionTrigger` add a merged-world guard — when
   `ff.mergedworld` and `targetSceneName ∈ {"OuterWorld","MainCastle_Hall"}`, redirect to `SceneRouter.Castle`
   (the merged name) **or** no-op if the target is now the same in-scene region (the castle↔outerworld seam is
   a walk now). This covers items 18/19/20 without touching baked triggers.
2. For `DungeonChainBuilder.cs:220` (#16) and `ArenaContracts.cs:80` (#17): change the literal to route via
   `SceneRouter.Castle` so it follows the flag. DungeonChainBuilder is editor-authored into the return trigger —
   the runtime remap (option 1) also catches it, so a re-bake is optional.

### LEAVE (with rationale)
- **Editor-builders (§3D, §4C):** they author the SAVED source scenes the merge consumes; the merged scene is
  produced by `WorldMergeBuilder`, so editing them is pure risk. (§3 STRATEGY: never regen the hand-dialed hub.)
- **Tests (§3E, §4E):** assertions remain true; only optional hardening.
- **Docs (~60 `.md`, all `QA_F8_ARCHIVE/*.jsonl`):** frozen/historical per CANON §15 — banner, don't rewrite;
  cosmetic, last.

---

## 7. ORDER, DEPENDENCIES & RISK

**Order:**
1. **Boot/router first** (in-flight): `SceneRouter.Castle` property + Build Settings primary start must point at
   `Main_Castle_Overworld` BEFORE anything else, or nothing loads the merged scene to test the gates.
2. **Add the two predicates** (`HubScenes.IsOverworld`; confirm `IsHub` merged entry — already present) — a
   dependency for Fix B/C. Core edit, compile once.
3. **Castle hub injectors (A)** + **overworld systems (B)** — file-disjoint, parallelizable, then batch-gate.
4. **Secondary gates (C)** + **return-targets (D)**.
5. **Data verify** (tutorial-steps, scene-configs) + **docs banner** last.

**Dependency:** Fix B/C reference new `HubScenes` members → land the `HubScenes` edit first (step 2) so the lane
agents compile against it.

**Risk (the exact failure the merge must avoid):** a **missed hub/overworld gate is a SILENT no-op**, not a
crash — the merged castle simply ships with no vendors / no barracks NPC / a visible spawn capsule / wrong music,
or the overworld ships with no reps / no harvest / no camps / no boundary. Headless AutoPilot must assert each of
these ran on `Main_Castle_Overworld` (vendor count > 0, barracks NPC present, spawn markers hidden, encounter
spawner armed, worker manager installed, boundary present), because none of them will throw. This is the
`never-inference-fix` discipline applied to a merge: prove each system booted on the merged scene from captured
`[Flow:*]` lines, don't assume.

---

## 8. STACKED TASK LIST (CLI works top-down; most-critical first)

> Land Lane-A hero-movement build first on its own gate (WO-608 EXECUTION ORDER), THEN this.

**STACK 1 — predicates + boot (do first, unblocks the rest)**
- [ ] T1. Confirm `SceneRouter.Castle` + `EditorBuildSettings` primary start = `Main_Castle_Overworld` under `ff.mergedworld` (in-flight — verify, don't duplicate).
- [ ] T2. Add `HubScenes.IsOverworld(string)` (Fix B). Compile-gate.

**STACK 2 — CRITICAL hub gates (dead features on merged)**
- [ ] T3. `CastleVendorNpcInjector.cs` — add `MergedTargetScene`+`IsCastleHubScene`; repoint lines 156,167,234,281.
- [ ] T4. `BarracksNpcInjector.cs` — same pattern; lines 74,85.
- [ ] T5. `HubAmbientVfxInjector.cs` — same pattern; lines 132,143.
- [ ] T6. `CastleSpawnMarkerHider.cs` — same pattern; lines 78,89.

**STACK 3 — CRITICAL overworld gates (dead half of merged scene)**
- [ ] T7. `OverworldEncounterSpawner.cs:133` → `HubScenes.IsOverworld`.
- [ ] T8. `WorkerManagerBootstrap.cs:85,90` → `IsOverworld`.
- [ ] T9. `CampSystem.cs:201` → `IsOverworld`.
- [ ] T10. `RaidOutpostSystem.cs:267` → `IsOverworld`.
- [ ] T11. `OuterWorldBoundaryInjector.cs:39` → accept merged scene (active or loaded) via `IsOverworld`.
- [ ] T12. `OutpostVictoryController.cs:114` → `IsOverworld` (lower risk; correctness).

**STACK 4 — secondary hub gates**
- [ ] T13. `AudioService.cs:923` → `HubScenes.IsHub` (music context).
- [ ] T14. `HeroEquipHud.cs:54` → `HubScenes.IsHub`.
- [ ] T15. `GroundZFightFixer.cs:412,421` → add `Main_Castle_Overworld`.
- [ ] T16. `BaseLayoutLoader.cs:55` → add `Main_Castle_Overworld` to pure-hub list.
- [ ] T17. `WorldFeelInjector.cs:340` → merged-aware `openWorld`.

**STACK 5 — return-targets (avoid loading retired scenes)**
- [ ] T18. `SceneTransitionTrigger` runtime remap: `ff.mergedworld` + target ∈ {OuterWorld, MainCastle_Hall} → `SceneRouter.Castle` / no-op (covers items 18/19/20 without re-bake).
- [ ] T19. `DungeonChainBuilder.cs:220` + `ArenaContracts.cs:80` → route via `SceneRouter.Castle`.
- [ ] T20. `scene-links.json` `castle_to_outerworld` — retire under `ff.mergedworld`.

**STACK 6 — data + cosmetic (last)**
- [ ] T21. VERIFY tutorial-steps `scene` per-step gating; broaden if exact-matched.
- [ ] T22. Optional: add `Main_Castle_Overworld` to `scene-configs.json` (ownership Home).
- [ ] T23. Optional test hardening: `Assert.IsTrue(HubScenes.IsHub("Main_Castle_Overworld"))`.
- [ ] T24. Docs: banner stale `MainCastle_Hall`/`OuterWorld` canon refs per CANON §15 (don't rewrite frozen ledgers).

**VERIFY (headless, per §7):** AutoPilot on `Main_Castle_Overworld` asserts — vendors>0, barracks NPC present,
spawn capsule hidden, wave spawn points injected, encounter spawner armed, worker manager installed, boundary
present, music context correct, dungeon/outpost/arena return lands on the merged scene. Then owner felt-verify.
