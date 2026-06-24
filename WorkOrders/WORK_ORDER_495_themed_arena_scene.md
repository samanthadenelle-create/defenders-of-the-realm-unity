# WORK_ORDER_495 — THEMED BATTLEARENA SCENE ("the fight stays where you stood")

**Status:** SPEC / BUILD-READY (creative agent, 2026-06-23) · Arena/Presentation lane
**Goal:** the isolated BattleArena reads as an EXTENSION of the source region it was called from, not a
teleport to a void. Outside engage → grassy clearing under the same dawn sky; dungeon → cracked stone.
Owner: "wherever the scene that calls the arena should determine how it's styled." Now a 60x48 open kite space.

## Creative direction
Pop-into-battle should feel like the camera just framed a clearing in the grass you were already walking —
same warm dawn light + pink-violet horizon, grass underfoot, a soft treeline ringing the kite space like
the edge of a meadow. Dungeon = cracked stone, darker closer mood, rubble/rock ring. Achieved by: (a) the
SOURCE REGION's real ground texture on the floor (not a flat tint), (b) KEEP the persisted skybox/ambient/fog
(already match where you were), (c) a natural SEE-THROUGH edge (treeline + boulders OUTSIDE the invisible
walls — silhouette only, never collide). Invisible walls still do the real confinement.

## Key reuse insight
**Do NOT use `Assets/Generated/Terrain/Exterior_*.terrainlayer`** — those are procedurally solid-color noise
(`ExteriorTerrainBuilder.MakeSolidTexture`), no richer than today's tint. Use the **texture-backed Blink
stylized GROUND materials** (`Assets/Blink/Art/Textures/Stylized*` — these are ground/env textures, NOT the
junked Blink hero armor; safe to reuse).

## File-by-file (follow-up build; theme keys already emitted by `OverworldEncounterSpawner.ThemeForScene` = outerworld/castle/cavern)
1. **Ground — `BattleArena.ApplyGroundTheme` (~224-241):** theme→material table, `Resources.Load<Material>`:
   - `outerworld` → `StylizedForestTextures/Grass_1/Grass_1.mat` (alt `Grass_Rocks`).
   - `castle` → `StylizedDungeonTextures/Dwarven_Ground/Dwarven_Ground.mat`.
   - `cavern` → `StylizedDungeonTextures/Floor_Sharp_Stones/Floor_Sharp_Stones.mat` (alt `Dirt_Stones`).
   Set `mainTextureScale ~ (12,10)` for the 60x48 plane. **Skip-safe:** null load → keep today's per-theme
   `Color` tint + LogWarning once. Never break the fight.
2. **Sky/ambient/fog:** default = DO NOTHING (persisted dawn sky/Trilight ambient/pink fog already match).
   `cavern` ONLY → optionally save current `RenderSettings` fog/ambient in `BuildArena`, set dim stone mood,
   **restore in `Resolve` (~412-445)** so the open world is untouched on return. Null-safe save/restore.
3. **Natural edge — new `DressArenaEdge(theme)` after the walls in `BuildArena` (~190-211):** ~12-20 low-poly
   props on a jittered ring at radius `ArenaHalf*+3..6` (OUTSIDE the walls, never on the floor), parented to
   `_arenaRoot` (auto torn down), **colliders STRIPPED** (silhouette only — never catch the kiting hero),
   deterministic `System.Random` seed (autopilot-chaos memory). Per theme: outerworld = KayKit Forest trees +
   boulders (`KayKit Forest Nature Pack 1.0/.../Color1/Tree_*_A`, `Rock_*`); cavern = rocks/rubble only; castle
   = sparse/bare. PolyPerfect `_M` nature = lighter alt. Skip-safe per prop (LogWarning, skip).
4. **Mobile-light:** cap ~20 props, instanced mat, all loads null-guarded via the existing `Guard.Try` pattern
   (BattleArena ~173-179) so one missing asset can't abort `StageRoutine`.

## Files
- Edit `Assets/_Modules/Village/Arena/BattleArena.cs` (`ApplyGroundTheme`, `BuildArena`, new `DressArenaEdge`, `Resolve`).
- Asset move: copy the 3 ground `.mat`s + the KayKit edge prefabs into `Resources/Arena/` (or Addressables) so
  the runtime arena loads them without the editor `AssetDatabase`.
- NO edits to ExteriorTerrainBuilder (cite for paths only), OverworldEncounterSpawner (theme map already correct), or any `.unity`.
