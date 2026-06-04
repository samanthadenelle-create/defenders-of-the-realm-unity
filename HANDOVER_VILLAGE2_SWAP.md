# HANDOVER — Village2 → live-village swap (DEF-243)

**Date:** 2026-06-04
**Owner directive:** make Village2 (generated town) the real village, **component-by-component, methodical, confirm each step.** Mechanism is locked: build a fresh `Village2.unity`, repoint scene refs at it, then **unhook (NOT delete) `Village.unity` LAST**, after Village2 is verified in a real build. `Village.unity` stays the shipping scene the entire time.

---

## Hard rules (do not violate)
- **Never touch / re-save `Village.unity`** — corruption-on-resave history. Village2 is a *separate* scene.
- Run batchmode only with **Unity closed**. Use `run-unity-method.ps1 -Method <fqmethod> -LogName <x.log>`. Judge success by the `Exiting batchmode successfully` marker + `[Village2Playable]`/`[Village2Build]` log lines, NOT the wrapper exit code (Unity forks). A `505` license line is transient/non-fatal.
- Editor tooling can't reference Assembly-CSharp → add runtime gameplay types by **reflection** (`FindType` + `AddComponent(System.Type)`), same pattern as VillageSceneBuilder.

## Tooling
- `Assets/Editor/Village2Playable.cs` (committed `8f5e948`, namespace `DeNelle.Editor`). One MenuItem per component, idempotent, verbose. Menu `Defenders/Village2/`:
  - `0. Inspect Layout` — transform vs renderer-bounds diagnostic (read-only).
  - `A. Promote Shell To Village2.unity` — opens `Village2Test.unity`, saves as `Village2.unity`, adds to Build Settings.
  - `B1. Wire Heart` — HeartController on a clean scale-1 `HeartOfElarion` anchor at village centre.
- `Assets/Editor/Village2Build.cs` (committed) — `1. Harvest Quaternius Buildings`, `2. Setup + Generate Village2` (regenerates the shell into `Village2Test.unity` + screenshot `Builds/village2_overhead.png`).
- `Assets/_Village2/Village2Generator.cs` — the GOOD committed generator (restored). Has `ScaleToHeight`. **Do not** reinstate the crash version (`Village2Generator.CRASH-VERSION.txt` at repo root, for reference only).

## DONE + verified
- **Recovery:** the Jun-4 05:09 `Village2Test.unity` was crash-corrupted (geometry scattered ±50k units). Regenerated clean from the Jun-3 20:35 prefabs. Inspected: Village2 root @ origin, tree ~14m @ (0,0,0), quadrants within ±24, town span 96×86.
- **Phase A:** `Village2.unity` exists, 336 objects, in Build Settings (right after `Village.unity`, both enabled).
- **Phase B1:** Heart on `HeartOfElarion` @ (0,0,0) scale 1. NOT on the tree — tree localScale is **18.06**, so a Heart there → 36m blocker capsule (heart-collider-scale-trap). Verified 1 instance.

## NEXT — in order (reordered for fail-fast de-risk)
1. **Phase C — NavMesh (the owner-flagged "walled-in" risk; DO THIS NEXT).**
   - Village2 has NO ground of its own + none of the `Ground/Roads/Walls/Buildings` named roots the village bake (`VillageSceneBuilder.NavMesh.BakeVillageNavMesh`) keys on. The hero walks on the **OuterWorld terrain** (additively loaded).
   - Must bake **ONE combined navmesh across Village2 + OuterWorld** (see `OuterWorldBuilder.BakeWorldNavMesh`, chain BuildVillage→BuildOuterWorld→BuildExterior→BakeWorldNavMesh, "marked ≥1 terrain") + a **gate-seam NavMeshLink** so the hero gets over the wall/moat lip.
   - Mark Village2 roads/floor walkable, walls/houses obstacle; keep the 4 gate openings WALKABLE (don't let wall arches voxelize solid across them).
2. **Phase D — repoint.** Flip `SceneRouter.Village` ("Village") and `WorldSceneLoader.VillageSceneName` ("Village") → "Village2". Grep + fix EVERY literal `"Village"` scene-name ref (audio director that owns village BGM, save, any `scene.name == "Village"`). Then a **player build** (delete `Builds/Windows` first — exe-stub quirk) + **Tricia walkability playtest**: hero spawns, walks, exits a gate, OuterWorld loads. CANNOT be confirmed from a headless log.
3. **Phase B2 — gates.** `Gate.Configure(GateGap)` needs a `GateGap` from `WallLayout` (`WallLayout.GateHalfWidth`, `WallThickness`); see `VillageSceneBuilder.Fortify.cs` ~770. Add `GateProximityOpener` (RequireComponent Gate; opens on `HeroLocomotion` proximity, radius 8). Do AFTER walkability proven (open gaps are safer for the first walk test).
4. **Phase B3 — spawns + waves.** Waves find the **`WaveSpawnPoint` COMPONENT** via `FindObjectsByType<WaveSpawnPoint>()`, NOT a "SpawnPoint" tag. Place ~12m outside each gate. `WaveManager` (`VillageSceneBuilder.Systems.cs` ~188) needs serialized `_heart` + `List<WaveSpawnPoint>` + enemy prefab.
5. **Phase B4 — buildings.** `Building` + `BuildingInteractable` on forge/armorer/lumbermill/tavern/church (see `VillageSceneBuilder.Scene.WireBuildingInteractables`) so upgrade/craft panels hook (PanelRouter). Many village systems self-install at runtime via `RuntimeInitializeOnLoad` — they do NOT need to be baked in.
6. **Phase E — deprecate Village1.** Unhook `Village.unity` from Build Settings + mark retired. KEEP the file (instant rollback). ONLY after Village2 verified in a build.

## State pointers
- Branch `feat/tower-core-loop`, remote at `8f5e948`.
- Live resume memory: `village2-swap-progress.md`.
- Scenes `Village2.unity` / `Village2Test.unity` are untracked local (regenerable; reference the gitignored `Assets/Quaternius` 128MB pack → won't resolve on a fresh clone until the pack is LFS-tracked or trimmed — VILLAGE2_WIRING_NOTES.md item 6).
