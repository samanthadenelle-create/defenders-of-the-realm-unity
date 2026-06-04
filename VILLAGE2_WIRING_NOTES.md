# Village2 — gameplay-wiring checklist (before it can REPLACE the old village)

Village2 (experiment/village2 branch) currently = a pretty, generated town. To make it
*playable* and swap it in for the hand-built village, the gameplay layer must be wired.
Do NOT merge to feat/tower-core-loop until these pass.

## 0. Scene defaults (empty-scene gap) — DONE via `Village2Playable.B0_AddSceneDefaults`
Village2 was generated into a **blank `EmptyScene`**, which (unlike a normal new scene) ships
with **no Main Camera and no Directional Light** → black Game view + unlit geometry. `B0` adds them
in C#, mirroring the live `VillageSceneBuilder` exactly:
- **Main Camera** — SolidColor dawn tint, FOV 60, far 600, `AudioListener`, **`VillageCamera` +
  `SmartMobileCamera`** (the live adaptive follow camera; hero target wired later).
- **Directional Light** — warm mid-morning sun (DEF-109 values), soft shadows.
- **Ambient + fog** — `Trilight` gradient ambient + linear fog (matches live).
Menu: `Defenders/Village2/B0. Add Scene Defaults (Camera + Light)`. Idempotent.

## ⚑ PLAYABLE-SETTLEMENT COMPONENT MANIFEST (the swap does NOT auto-carry these)
**Phase D (`D_SwapIntoLiveVillage`) overwrites the ENTIRE `Village.unity` with Village2's
contents** — anything not present in Village2 is LOST in the swap. The live village bakes the
following as scene objects (via `VillageSceneBuilder`); a generated town needs the SAME set to be
playable. **This is the recipe the generator/factory should inject automatically — including when
players generate their own camps/settlements** (owner 2026-06-04: note these so player-built camps
get them for free). Source = live builder:
| Component | Live builder site | In Village2? |
|---|---|---|
| Main Camera + `VillageCamera` + `SmartMobileCamera` + `AudioListener` | `Scene.cs CreateCamera` | ✅ B0 |
| Directional Light + ambient/fog | `Wiring.cs CreateDirectionalLight` / `ConfigureSceneLighting` | ✅ B0 |
| `HeartController` (lose condition) | `Content.cs` | ✅ B1 |
| `EventSystem` (UI input — HUD clicks dead without it) | `Scene.cs EnsureEventSystem` | ✅ B2 (`ImportEventSystem`) |
| `VillageController` (orchestrator) | `VillageSceneBuilder.cs:330` | ✅ B2 (`ImportVillageController`, `_heart` wired) |
| `VillageHudController` (the HUD) | `Scene.cs` / `Wiring.cs` (`DeNelle.HUD`) | ✅ B3 (`ImportVillageHud`) |
| Hero rig (`BuildHero`) + camera target wiring | `Characters.cs` / `Fortify.cs:692` | ✅ B4 (`ImportHero` + `WireCameraTargetToHero`) |
| `WaveManager` | `Systems.cs:194` | ❌ TODO (B5) |
| `WaveSpawnPoint`s (4 cardinals) | `Dressing.cs:414` | ❌ TODO — needs gate→position mapping (§2) |
| Sounds (music/ambient bed) | AudioService / `CoreServices.Audio` | ❌ TODO (static part) |
| Mesh→role mapping (house→`Building`+Interactable, wall→`WallSegment`, gates) | catalog | ❌ TODO (§4) |
> Mesh→role mapping must ALSO ensure each placed wall/building mesh has a **collider** + sits on a
> camera-occlusion layer (**Default/Building/Tower**). `SmartMobileCamera` already pulls the camera in
> front of walls (DEF-151 spherecast on that mask) AND the hero capsule needs wall colliders to not
> walk through — both fail silently if generated Quaternius walls lack colliders / use the wrong layer.
> (This is the "tight-walls camera caveat" — the camera logic is fine; the dependency is collider+layer.)

### Two tiers — global singletons vs scene-local (don't wire the globals)
The mobile control layer is **already global + automatic** — do NOT add it per-scene/per-recipe:
- `VirtualJoystick` (move) and `CameraPanInput` (slide-to-pan / swipe-look → `SmartMobileCamera.AddYaw`)
  both self-bootstrap via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + `DontDestroyOnLoad` and
  **self-activate whenever a `HeroLocomotion` exists** (so B4's hero turns them on in Village2 for free).
  They ship in the build by being in `DeNelle.Village` — nothing to bake. `CameraPanInput` aborts in
  the DTT/PatriciaLight scene (LeanTouchAimDriver owns Lean there). Swipe-look works in builds because
  `SmartMobileCamera` force-sets `_orbitBehind=true` at runtime (`_forceCameraFix`, DEF-202/204).
- **The LeanTouch "no EventSystem" warning** = `CameraPanInput`'s LeanTouch polling one frame before the
  scene EventSystem inits; benign, resolved by B2 adding the EventSystem.

So the factory recipe lists ONLY scene-local parts (geometry, Heart, spawns, VillageController, camera
object, light). Global singletons (input/control, and any other `RuntimeInitializeOnLoadMethod`+DDOL
service) come along for free in any build — never put them in the recipe.

**Status 2026-06-04:** B0–B4 written as scene-agnostic `Import*` methods in `Village2Playable.cs`,
plus orchestrator **`Defenders/Village2/B. Build Playable City (all phases)`** (runs B0→B4 = the
factory proof). Each `Import*` takes a parent `Transform` so a future `CityFactory` calls them
unchanged (see [[city-factory-recipe-decision]]). Remaining: WaveManager+spawns (needs gate mapping),
sounds, mesh→role mapping, then bridges (build/ability/wave HUD bridges once partners exist).
> WO-280's 6 checks below still don't list these systems — that gate predates this manifest. The
> manifest above is the authoritative "what a playable city needs" list.

## ✦ Tree of Life upright (DEF-96) — STILL OPEN
Owner observed 2026-06-04: the centre tree is **lying flat on its right side**. The generator places
it at `Euler(0,0,0)` (upright in code), so the lean is the `Tree_Of_Life.fbx` import orientation —
needs a corrective rotation on the clone (fix in `Village2Generator` tree placement + an idempotent
correction phase for the already-generated scene). Confirm fall axis before committing the angle.

## 1. Heart of Elarion
- Put `HeartController` on the Tree of Life at (0,0,0) (lose condition + HP + HUD bridge).

## 2. Gates + spawns (the 4 cardinal openings)
- `Gate` + `GateProximityOpener` at each of the 4 wall gaps (N 12 m, S/E/W 6 m).
- `SpawnPoint`-tagged markers ~12 m OUTSIDE each gate (enemy spawn + wave→Heart pathing).

## 3. *** NavMesh — the two-scene seam bridge (owner flagged this specifically) ***
- Village2 must bake into the **COMBINED Village + OuterWorld navmesh**, NOT an isolated one.
- This is `OuterWorldBuilder.BakeWorldNavMesh` (marks ≥1 terrain NavigationStatic, bakes ONE
  navmesh across both scenes) + the gate/seam **NavMeshLink** bridging — the "reference / navmesh
  edge applied to the two scenes to get over the bump" the owner added. Without it the hero is
  walled inside Village2 and can't walk out to the OuterWorld terrain.
- Replicate: ensure Village2 ground + buildings (obstacles) bake walkable, gate lanes clear,
  and the Village2↔OuterWorld seam is bridged the same way the old village's was
  (`StairNavLink` / `RampartNavLinkInstaller` patterns; BakeWorldNavMesh chain).

## 4. Interactable buildings
- `Building` + `BuildingInteractable` on the gameplay buildings (Forge/Market/Workshop/PetHouse/
  Farm/Lumbermill/Armorer) so the upgrade/craft/talent panels hook (PanelRouter).
- v1 reuses houses for specialty buildings — re-skin to real Forge/Tavern/Church later.

## 5. Scene-loader swap
- Point `WorldSceneLoader` / scene wiring at Village2 instead of the old Village scene
  (or have Village2 become the village content root). Two-scene additive load preserved.

## 6. Quaternius pack in git
- `Assets/Quaternius` (675 MB CC0) is untracked on disk; harvested prefabs reference it by GUID.
- For production: LFS-track the pack (or just the meshes/materials/textures used), or bake the
  used pieces into a trimmed folder. Don't ship 675 MB to WebGL untrimmed — strip unused pieces.

## Backlog this MOOTS once merged (tag "superseded by Village2"):
DEF-106 (double wall ring), DEF-114 (gate z-fight), DEF-101 (building overlaps gate),
DEF-96 (upside-down tree regen), DEF-195 (moat), DEF-220 (forge mesh), DEF-240 (town art
consistency), DEF-191 (castle structure), DEF-193 (production buildings), DEF-198 (cathedral→tree),
the rampart-stair saga.
