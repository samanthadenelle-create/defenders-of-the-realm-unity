# WORK ORDER 797 — Dungeon rooms own their enemies (per-area seating + confinement)

**Status: IMPLEMENTED pending gates** (2026-08-02 — compile gate + AutoPilot/regression run + re-bake still owed)

> **2026-08-02 implementation note (F8 seq 622 evidence):** the owner's felt-test that day
> reproduced the drift at scale — 13 `outpost-hollow-*` enemies with no room ownership
> CAMPED THE ENTRANCE GATE, burying the runtime-injected RETURN exit
> (DungeonExitInteractable/DungeonExitSpawner injects at entry + (0,0,-2.6)) so the run read
> as "no way to exit". Implemented: per-room `encounter` blocks in the graph + layout JSON
> (all 4 dual-copies), `EnemyBrain.SetRoomArea` wake-from-footprint + destination confinement
> (hoisted above the retaliation override), spawner room binding (bake-time via
> `DungeonBaker.WriteEncounterFields` SerializedObject writes AND runtime via the new
> `DungeonRoomBinder` for the already-baked binary scene — no immediate re-bake needed),
> the exit discoverability beacon (pulsing light + glow beam + EXIT label, prompt radius
> 3.0 -> 4.5), `DungeonRoomOwnershipRegression` + new EnemyLeashLogicTests cases.
> Deviation from the spec text: the encounter-block oracle requires every room WITH an
> encounter to be valid/confined, not that every combat-archetype room HAS one — corridors
> (corr1/turn1-3/loop2) are archetype "combat" but were never authored with spawners, and
> re-authoring composition was out of scope. EW-4 stat divergence: DEFERRED (untouched).

**Original status: READY TO IMPLEMENT** (owner routing: F8 seq 461 "all enemies are at the enterance.
should be pinned to areas")
**Classification (QA triage 2026-07-30):** per-area pinning = NEW FEATURE (the dungeon graph
schema has ZERO enemy/spawn/area fields; spawner rooms are a hardcoded literal). One EXISTING
defect inside it — leash never re-pinned (mobs froze at the entrance) — was fixed same day in
EnemyBrain (return-home on leash-out). This WO builds the real system.

## Data-proven causes (leave in the record)

1. Nearest group spawns INSIDE its own 10m leash of the hero seat (junction ring slots land
   ~7.45m from entry; margin 2.5m) → beeline on frame one. OutpostEnemyGroupSpawner.cs:87-114.
2. Retaliation override (EnemyBrain.cs:618-641) runs BEFORE the leash gate with no range cap —
   one hero swing tows a mob across the dungeon.
3. (FIXED 2026-07-30) leash-out froze mobs in place instead of walking home.
4. Scene bake (2026-07-20) predates the leash field entirely — `leashRadius` absent from the
   serialized spawner (type-tree verified); only the C# default arms it.

## Build

Graph schema gains a per-room `encounter` block (both Canonical + StreamingAssets copies):
`{ kind, min, max, seatMode, formationRadius, confine: { mode:"room", slack, returnHome, wakeRadius } }`
- wakeRadius measured from the ROOM FOOTPRINT, not a ring slot (kills cause 1)
- confine clamps nav destinations into room AABB ∪ slack, hoisted ABOVE the retaliation
  override (kills cause 2 — provoked mobs fight but never leave the room)

Files: DungeonBaker.cs :400-457 (delete the `{junction,loop1,loop3}` literal; read encounter
blocks; write fields via SerializedObject so they land in the SCENE), GraphDungeonComposer.cs
(carry encounter + room AABB), OutpostEnemyGroupSpawner.cs (areaBounds + wakeRadius),
EnemyBrain.cs (SetArea + clamp), DungeonBakerChecks.cs (oracle: every combat room has an
encounter block + spawner seated in its own AABB), EnemyLeashLogicTests.cs (ShouldConfine
cases), re-bake dg_starter_loop.unity (**isolated worktree — NUL-corruption memory**).

## Acceptance

- [ ] Steady-state: every enemy stays inside its room AABB for a whole headless run.
- [ ] Retreat test: survivors return to their room (`leash: returning home` trace), never
      follow to the entrance, never freeze mid-corridor.
- [ ] loop1/loop3 groups dormant until the hero enters their room.
- [ ] leashRadius/wakeRadius present in the re-baked scene's serialized data.
- [ ] Do NOT touch: WO-790/791/792 outpost paths; the `_leashRadius==0` village short-circuit.
- [ ] Fold in or defer explicitly: EW-4 stat divergence (DefFor hollow stats vs enemies.json).
