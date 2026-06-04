# Village2 — gameplay-wiring checklist (before it can REPLACE the old village)

Village2 (experiment/village2 branch) currently = a pretty, generated town. To make it
*playable* and swap it in for the hand-built village, the gameplay layer must be wired.
Do NOT merge to feat/tower-core-loop until these pass.

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
