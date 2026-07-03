# WORK ORDER 595 — KayKit modular dungeon kit (24 snappable grid pieces)

**Status:** READY TO IMPLEMENT (design complete; pack present + measured)
**Date:** 2026-07-01 (overnight)
**Priority:** P2 — the coherent-dungeon foundation
**Owner:** Samantha (design) · Author: CLI (from measured catalog) · Implements: CLI
**Lane:** World/Environment · feeds the chunk-composer north-star + dungeon-outpost-arena primitive.

## Why (owner, 2026-07-01)
Free-form AI dungeon generation produced incoherent geometry ("Picasso stairs — 3 directions in one
room," the Tree of Life dropped in a dungeon). **Fix: a standardized, snappable, pre-authored kit** —
randomize the *arrangement* of valid pieces, never generate geometry from scratch, so **every maze is
coherent by construction.** 20–30 pieces is plenty for endless randomized mazes.

## The pack (PRESENT + measured — not assumed)
- **KayKit Dungeon Remastered 1.1** — imported: `Assets/Models/KayKit/dungeon/fbx(unity)/*.fbx` (**211 FBX**),
  shared material `.../Materials/dungeon_texture.mat`. Gitignored (§4 → `Debug.LogWarning` on missing model).
- **Native grid = 4 m cell, 4 m wall height, 2 m sub-grid** (measured from OBJ vertices). 1 unit = 1 m, Y-up.
- **No prefab wrappers** — raw FBX + one shared atlas material. Builder instantiates FBX (or CLI authors thin prefab wrappers).

## The grid standard (locks "so the math works")
- **Cell = 4 m × 4 m.** Wall/level height = 4 m. Sub-grid = 2 m.
- **Door = ~2 m wide × ~3 m tall, centered on a 4 m edge** (confirm the first `wall_doorway` against a 2 m agent before mass-instantiating).
- **Walls straddle the cell edge line** (centerline on the edge) so neighbors share a wall — composer dedups shared edges (wall if neighbor edge closed, doorway if it's a connection). No double-walls / z-fight.
- **Anchor** each chunk at cell (0,0) center; sockets at edge midpoints `(±2,0,0)`/`(0,0,±2)`. Snap = match open socket→open socket, translate 4 m, yaw to align. Vertical pieces advance the level index ±1 (±4 m Y).

## The 24-piece kit (each an integer cell multiple; any open edge mates any open edge)
Rooms: `room_small`(1×1), `room_medium`(2×2), `room_large`(3×3, interior pillars), `boss_room`(3×3, grand door + banners + pillars).
Halls: `hall_straight`(1×3), `hall_straight_short`(1×2), `hall_corner_L`, `hall_T`, `hall_cross`, `pillar_hall`(1×3).
Access: `door_gate`(portcullis `wall_gated`), `door_arch`, `entrance`(1×2, to world, torches), `exit_portal`(RegionGate warp marker), `dead_end`, `rubble_cap`(collapsed blocker).
Vertical: `stairs_up`(1×1,+4 m), `stairs_down`(1×1,−4 m), `stairs_grand`(1×2 gentle), `elevator`(1×1, **custom moving-platform script** — the one mechanical piece with no native prefab).
Traps: `trap_spikes`(`floor_tile_big_spikes`), `trap_pit`(`floor_tile_big_grate_open`, 1 m pit), `trap_grate_hall`(1×2).
*(Full per-piece FBX part lists + open-edge tables in the overnight catalog agent report — reproduced into `dungeon-kit.json`.)*

## Theming = material swap (not new geometry)
One authored theme (grey stone) + **6 recolor atlases** in `.../KayKit Dungeon Remastered 1.1/Assets/textures/`
(`alt_texture_1_Golden … 6_NightB`). All 211 meshes share one UV atlas → **one material swap re-skins the whole
kit.** "Pick a theme" = pick which of the 7 atlases the builder assigns per dungeon/region.

## Build plan (data-driven — owner thinks in data structures)
1. **`dungeon-kit.json`** (started this session) — each chunk = `{ id, type, cells:[w,h], sockets:{N,S,E,W:open|closed},
   parts:[{fbx, pos:[x,y,z], yaw}], theme? }`. The single source; the composer reads it.
2. **`DungeonKitBuilder`** (editor) — instantiate a chunk's `parts` FBX at anchor-relative positions + assign the
   theme material; author thin prefab wrappers per chunk for hand select-and-snap in the editor.
3. **Composer** — grid placer that snaps chunks socket→socket at 4 m, randomizes a maze from a seed budget
   (ties to the chunk-composer north-star + the seed-budget progression), dedups shared walls.
4. **NavMesh** — bake/carve after composing; keep openings ≥ agent width (2 m door satisfies it; avoid the
   Village2 thin-gap tunneling — memory `rebake-navmesh-after-terrain-change`).

## Acceptance
- `dungeon-kit.json` holds all 24 chunks with sockets + parts. Changing it changes the kit with no code edit.
- `DungeonKitBuilder` instantiates any chunk correctly themed; pieces snap on the 4 m grid with no double-walls.
- A seeded composer builds a walkable, coherent maze (no floating/mis-facing geometry); navmesh bakes; hero paths it.
- Theme swap = one material assignment re-skins a whole dungeon.

## What NOT to do
- Do NOT free-generate geometry — only place authored chunks (that's the whole point).
- Do NOT hardcode piece dimensions — read cell/socket data from `dungeon-kit.json` (WO-594 measure-driven principle).
- `Debug.LogWarning` (not error) on a missing KayKit FBX (gitignored pack; §4).

## Open (owner call in the morning)
1. **Theme** — which atlas is the default (grey / golden / sepia / night)?
2. **Elevator** — build the custom moving-platform now, or ship v1 with stairs only + elevator later?
3. Confirm the ~2 m door width against a 2 m agent before mass-instantiation.

## OWNER FELT-TEST REQUIREMENTS (2026-07-02, from Outpost1 F8 sweep — BINDING on the build-out)
The live Outpost1 confirms why this WO exists — the AI-generated stand-in fails on every axis. The kit build-out MUST deliver:
- **Collision-correct traversal:** every kit piece has real colliders; stairs are climbable by construction (owner: "stairs not usable, AI generated has no logic, collisions not in them").
- **Castle-style camera:** outposts/dungeons use the same camera rig/behavior as MainCastle_Hall (owner: "movement broken, partially the wall height and camera — implement camera like in castle"); wall heights authored with the camera in mind (4m walls vs camera distance).
- **No render voids:** the space is enclosed/dressed — no skybox-void sightlines (owner flag: Outpost1 renders as empty void with a floating hero).
- Movement/navmesh verified by bot walk before owner felt-test.
