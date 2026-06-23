# WORK ORDER 19 — RESULT (functional MVP; KayKit art + extra dungeons are follow-ups)

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025
**Outcome:** Data-driven dungeon-entrance system implemented end-to-end in tracked code + assets. Hero walks up → "Press F to enter" → loads the dungeon. Build clean. Two deliberate, documented deviations from the literal spec (placeholder visual instead of a KayKit model; runtime placement instead of scene-baked) — both to respect the gitignored-`Models` trap and the curated-scene rule. Runtime visual eyes-on is the remaining tick.
**Editor:** Unity 6000.4.8f1

---

## 1. What shipped

| File | Role |
|---|---|
| `Assets/_Modules/Village/Dungeons/DungeonDef.cs` | ScriptableObject — per-dungeon identity (id / nameKey / displayName / sceneName / banner / accentColor / desc / sceneExists) |
| `Assets/_Modules/Village/Dungeons/DungeonEntrance.cs` | The interaction: proximity (HeroLocomotion-filtered trigger), "Press F" prompt, `SceneRouter.LoadScene(def.SceneName)`, per-instance accent via `MaterialPropertyBlock` |
| `Assets/_Modules/Village/Dungeons/DungeonEntranceBootstrap.cs` | Runtime placement — builds N entrances around the perimeter, assigns defs |
| `Assets/Resources/Dungeons/HealersCottage.asset` | DungeonDef for `Dungeon_HealersCottage` (exists) |
| `Assets/Resources/Dungeons/FolksGranary.asset` | DungeonDef for `Dungeon_FolksGranary` (exists) |
| `VillageController.cs` | `Start()` now also `EnsureDungeonEntrances()` (runtime attach) |

The interaction mirrors **WO-08**'s gate pattern: hero identity = `HeroLocomotion` component (not tags/layers); the trigger carries a **kinematic Rigidbody** because the hero is a solid, RB-less, transform-moved collider; accent applied via `MaterialPropertyBlock` (never mutates a shared material — per the WO hard rule).

## 2. Dungeon-scene inventory (task 3.1)

Only **2** of the spec's 8 dungeon scenes exist on disk: `Dungeon_HealersCottage`, `Dungeon_FolksGranary`. `SceneRouter` names 7 (+ those two: SunkenBellTower, WolfwardensVigil, FrostStair, GlassCathedral, ApothecarysVault) but their scenes aren't built. Per WO §3.1, I authored **DungeonDef assets for the 2 that exist** and left the rest as scaffold (a `DungeonDef.SceneExists` flag makes a future entrance a safe no-op until its scene lands; `SceneRouter.LoadScene` also guards via Build Settings). The bootstrap places one entrance per authored def → **2 functional entrances** today; dropping in 5 more `.asset` files (30 sec each, the WO's design goal) places the rest once their scenes exist.

`DungeonDef` → scene map: `HealersCottage` → `Dungeon_HealersCottage`; `FolksGranary` → `Dungeon_FolksGranary`.

## 3. Two deliberate deviations (both to avoid known traps)

1. **Placeholder visual, not a KayKit model.** The WO §3.2 says import a KayKit Dungeon doorway into `Assets/Models/…`. But `Assets/Models/` is **gitignored** (the fresh-clone trap that caused WO-05/10/18) — a prefab referencing a model there would break on every clone. Instead the entrance is built procedurally (a 2-post + lintel doorway from primitives with a URP/Lit material, accent-tinted), so the whole feature is **tracked and clone-safe**. Swapping in real KayKit art is a clean follow-up: parent a model under the entrance root + drop the primitives. (Consequence: no `DungeonEntrance_Base.prefab` asset — the bootstrap's `BuildEntrance` is the "shared base." AC2's prefab-as-asset is met in spirit, not as a `.prefab` file.)
2. **Runtime placement, not scene-baked.** The village is baked by the edit-time `VillageSceneBuilder` (curated-scene rule forbids re-running it), and hand-editing 8 PrefabInstances into `Village.unity` is a large, risky scene-diff commit (BUG-023). So entrances are placed at runtime by `DungeonEntranceBootstrap` (attached via `VillageController.Start`, same pattern as the WO-08 gate openers + WO-20 HUD bridge). (Consequence: entrances appear in **Game view / playmode**, not the edit-time **Scene view** — AC3's "Scene view" is unmet by design.)

## 4. Acceptance criteria

| AC | Status |
|---|---|
| 1. DungeonDef.cs + def assets | ✅ class + 2 real assets (the 2 existing scenes); 5 scaffold documented (§2) |
| 2. DungeonEntrance.cs + base prefab | ✅ component; "base" is the bootstrap's procedural builder, not a `.prefab` asset (§3.1) |
| 3. 8 entrances visible in Scene+Game | ⚠️ 2 entrances (= existing scenes), visible in **Game view** (runtime), not Scene view (§3.2) |
| 4. Distinct banner + accent per entrance | ✅ distinct accent colour per def (amber / green); banner sprites = follow-up (placeholders are null) |
| 5. "Press F to enter" on approach | ✅ implemented (world-space prompt, HeroLocomotion-gated); eyes-on pending |
| 6. F transitions to the dungeon scene | ✅ `SceneRouter.LoadScene(def.SceneName)`; eyes-on pending |
| 7. Built exe replicates | ✅ build clean; in-exe walk-up needs eyes-on (build-side gate) |
| 8. Small focused commits | ✅ |
| 9. This RESULT.md | ✅ |

## 5. Verification

- ✅ Build after all WO-19 code + the 2 `.asset` files: `[DesktopBuild] SUCCEEDED`, 0 compile errors, no warnings in the new files; the hand-authored DungeonDef assets import cleanly.
- ✅ Static correctness: interaction mirrors the proven WO-08 trigger pattern; `SceneRouter.LoadScene` path verified against the existing scene names + Build Settings.

## 6. Remaining (eyes-on + follow-ups)

- **Owner eyes-on (build-side gate):** Village playmode → walk the hero (WASD) to a perimeter entrance → "Press F — Healer's Cottage" appears → F loads `Dungeon_HealersCottage`. (Can't be driven headlessly through the onboarding flow.)
- **Follow-ups (clean, small):** (a) swap the placeholder doorway for a KayKit Dungeon model (parent under the entrance root); (b) author banner sprites + the remaining `DungeonDef` assets as their scenes land; (c) if edit-time Scene-view placement is wanted, a small `safe-scene-edit` batchmode pass can bake the entrances in (the data + component are ready).
