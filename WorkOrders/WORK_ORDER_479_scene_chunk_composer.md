**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_479 — Scene/Dungeon Creator: composable anchor-relative CHUNKS

**Status: DESIGN — owner north-star (2026-06-21), ready to scope** · Supersedes the one-off Village2 builder approach.

## Owner vision (verbatim intent)
"Create a dungeon or scene creator so one [collection] is just an outer perimeter, the next is a small camp, and
[I] move those collections around to a starting point of each and build it in scripted chunks."

## The architecture
Generalize the existing capture→recipe→build pattern (CastleOffsetCapture, Village2Playable Capture/Replay,
Village2LayoutDump) ONE level up — from "one scene's pieces" to "a library of composable chunks":

- **Collection (chunk):** a named, self-contained unit captured **relative to its own anchor/start-point** —
  e.g. `OuterPerimeter`, `SmallCamp`, `Stronghold`, `DungeonRoom_*`. Stored as a recipe JSON of pieces
  (prefab/customFbx + LOCAL TRS relative to the anchor). Drop-anywhere, reusable across scenes.
- **SceneRecipe:** an ordered list of placements `{ collectionId, worldAnchor(x,y,z), yawDeg }`. The owner moves
  each collection's start-point to taste; capture records the anchor.
- **Composer (scripted-chunk build):** given a SceneRecipe, for each placement → instantiate the collection at
  its world anchor + yaw → builds the scene in chunks. Deterministic + reproducible (a rebuild never reverts the
  owner's layout — solves the castle/Village2 "regen wipes hand-dialing" problem permanently).
- **Markers travel with the chunk:** spawn points, hero-start, enemy-spawn groups are captured relative to the
  anchor too, so a placed `SmallCamp` brings its own `Spawn_*` + nav intent.

## Reuse (do NOT greenfield)
- Capture: extend `Village2LayoutDump`/`CastleOffsetCapture` to capture a sub-tree **relative to an anchor**.
- Replay/build: `Village2Playable.ReplayRecipeIntoScene` already instantiates a recipe — generalize to place a
  Collection at an arbitrary anchor+yaw, and to compose multiple.
- Colliders/floor: the `EnsureStructureCollider` fit + a floor-fill per chunk (kills "no colliders" + "huge hole").
- Nav: bake combined navmesh per composed scene (Village2Playable Phase C pattern).

## Village2 = the first proof
Its `StrongholdRoot` (130 objs, captured to `village2-layout-dump.json`) splits by the owner's own spawn zones into
3 starter collections: **OuterPerimeter** (walls/gate), **CourtyardCamp** (Spawn_Courtyard/Chokepoint), **KeepCore**
(Spawn_Keep/Rear). Capture each relative to an anchor → Village2 becomes a SceneRecipe composing the three.

## Two component classes (owner 2026-06-21)
1. **Structural chunks** (geometry): OuterPerimeter, SmallCamp, Stronghold, DungeonRoom, corridor.
2. **Gameplay components** (interactive, anchor-relative, parameterized): **Trap** (trigger + effect), **Squeeze/Choke
   point** (narrow pass — already an idea in Village2's `Spawn_Chokepoint`), **Fake Wall** (looks solid, passable /
   reveals on trigger), **Bridge** (a crossing — reuse the **RegionGate** primitive), **Maze components** (modular
   wall segments that tile into a maze). Each is a small scripted unit with params (damage, width, reveal-condition,
   span) and its own anchor — so a dungeon places "a trap here, a fake wall there, a bridge over this gap."

## Gate refinement — funnel navigation through the ARCH only (owner 2026-06-21)
The RegionGate crossing logic is sound, but movement should be physically constrained to the arch opening, not
left to logic alone. Add **two small vertical blocker panels INSIDE the gate structure**, flanking the arch gap,
so ONLY the area between the actual arch is navigable. Implementation (scripted onto the gate, parameterized from
the arch width):
- Two thin vertical panels (BoxCollider + NavMeshObstacle/carve, optionally invisible) positioned at the inner
  edges of the arch so the navmesh + physics route ONLY through the opening.
- Kills the "slip around/through the gate side" class of bug and makes the gate a real THRESHOLD.
- Bonus: the funnel IS a natural choke point — pairs with the encounter/role-mix design (a healer-backed ambush
  right at the arch). One gate component doubles as a Squeeze/Choke point.
Apply as a scripted change to specific gate parts (the recipe places the gate + auto-fits the two panels to the
arch bounds) — never hand-placed per scene.

## GUID backbone — the connective tissue (owner 2026-06-21: "using guids could really connect it all")
Every connectable thing carries a stable **GUID** (auto-generated, collision-proof) — crossings, regions, chunks.
This is the identity layer the whole system maps on:
- **HeroLinkCrossing** gets a `selfGuid` (its identity) + a `pairGuid` (its partner's selfGuid). A "Create Crossing Pair"
  tool generates the GUID + stamps BOTH markers — so the owner never types an id and two crossings can't collide. The
  hero warps self->pair by GUID. (Upgrades the current human-string `crossingId`.)
- **Regions / chunks** carry a GUID too; a crossing records the two region GUIDs it bridges (regionA, regionB).
- **The graph:** GUIDs + the crossing edges = a navigable WORLD GRAPH — "from region X, via crossing G, to region Y."
  The dungeon JSON / SceneRecipe references nodes by GUID (not name/position), so a layout survives renames/moves and
  a regression can assert "every region is reachable from spawn via the crossing graph" (SEAM-REACHABLE, generalized).
- **Why GUID over a typed id:** no typos, no accidental dupes, stable across renames/rebuilds, and machine-mappable —
  exactly what an AI-authored dungeon (the JSON generator) needs to wire connections reliably.

## Crossings = "Points of Entry" (Corgi-validated, GUID-keyed, cross-scene) — owner 2026-06-21
Our HeroLinkCrossing IS the proven Corgi Engine "Points of Entry" pattern (GoToLevelEntryPoint + LevelManager):
an entry on the source side, a destination marker on the target side, spawn the hero at the destination. Corgi
keys them by scene-name + ARRAY INDEX; we key by **GUID** — exactly the robustness extension Corgi's own docs
recommend over fragile indices. So the design is battle-tested, just with a better id layer.

Two modes, one component:
- **In-scene / additive** (both ends loaded — e.g. castle+OuterWorld): warp via the live Registry (have it).
- **Cross-scene** (e.g. OuterWorld -> Village2, separate scenes): the entry carries a `targetScene`; on trigger,
  LOAD that scene then warp to the crossing whose GUID matches — Corgi's GoToLevelEntryPoint, GUID-keyed.
  REUSE the existing machinery: SceneTransitionTrigger + SceneRouter already load a scene and reposition the hero
  (RepositionPlayerAfterLoad). The crossing's matched-GUID destination simply REPLACES the hardcoded targetPosition
  there — unifying the seam/raid transitions + the new gates onto ONE GUID points-of-entry system, and letting the
  legacy hardcoded slide retire. (Combine with the save system so position/state persists across the transition.)

## Two crossers, two mechanisms — NOT either/or (owner research 2026-06-21)
The "navlink vs paired-warp" question resolves cleanly — they serve DIFFERENT agent types:
- **AI (enemies, companions)** use SetDestination (pathfinding), so they AUTO-CROSS **NavMeshLinks** once both
  navmeshes are live. Place narrow NavMeshLinks (multiple small ones, ~1u, not one giant) at the gate so AI paths across.
- **The HERO is INPUT-driven** (Move, not SetDestination) → it NEVER auto-crosses a navlink. It crosses via the
  manual **HeroLinkCrossing** paired-warp (points-of-entry). This is WHY the manual crossing exists.
So a gate carries BOTH: a NavMeshLink (AI) + a HeroLinkCrossing pair (hero). They coexist.

**Additive loading is the unifying model** (both references agree): keep both regions' navmeshes live at once
(castle+OuterWorld already loads OuterWorld additively). Then — bake separate NavMeshSurface per region; place the
connecting NavMeshLinks in the additive/target scene pointing BACK to the other navmesh (narrow, multiple); the
merged navmesh lets AI path across, AND the hero's HeroLinkCrossing partner is already loaded so it's a simple
Registry warp (no scene-load). Load regions additively (LoadSceneMode.Additive) via a manager/streamer; unload on
leave. This is the gated-regions world working end to end: additive regions + NavMeshLinks (AI) + HeroLinkCrossing (hero).

## JSON-driven DYNAMIC dungeon builder (the end state)
A **dungeon recipe JSON** composes the above dynamically: rooms + corridors + the gameplay components, connected by
anchors/sockets. The builder reads the JSON and constructs the dungeon in scripted chunks at runtime/build —
**authored OR procedurally generated** (a generator emits the JSON; the same builder consumes it). This is the
"json dungeon builder": data in → playable dungeon out, every piece a reusable component, fully reproducible.

## Progression-scaled SEED (owner 2026-06-21)
The procedural generator is driven by a **seed whose BUDGET scales with player progression** — "the further they go
or the higher their level, the bigger the seed." A bigger seed budget → the generator emits a LARGER, HARDER dungeon:
more rooms/corridors, more gameplay components (traps/chokes/maze depth), higher enemy count + level. Same (seed,
budget) → the exact same dungeon (reproducible — for debugging, sharing, fair retries; mirrors the AutoPilot seeded-
chaos idea [[autopilot-chaos-not-one-scripted-path]]). Reuse the existing level-scaling (GarrisonStatBlocks.ApplyLevelScale,
baseEnemyLevel/levelOffset) for the enemy side + the OuterWorld danger-gradient idea for spatial difficulty.
Budget inputs: dungeon depth reached + player level → a single scalar the generator spends on size + difficulty.

The seed budget is a **universal encounter dial** — it doesn't only size the dungeon; the generator spends it across:
- **Dungeon size/complexity** — rooms, corridors, gameplay-component density (traps/chokes/maze depth).
- **Enemy count + level** — via the existing GarrisonStatBlocks scaling.
- **Troop counts** — number of troops fielded (player garrison and/or enemy defenders) scales with the budget.
- **AI strategy points** — the enemy AI gets a *strategy budget* it spends on tactics (formations, flanks, ability
  use, reinforcement waves) — deeper/higher-level = a smarter, better-resourced AI, not just more HP. (New AI
  layer; folds onto EnemyBrain roles — a budget the brain spends on plays.)
One scalar in → the generator allocates it across geometry, enemies, troops, and AI sophistication, deterministically.

## Encounter composition — ROLE MIX forces strategy (owner 2026-06-21)
A dungeon room is a composed **encounter** with a deliberate **role mix** (multiple Healers + DPS + Tanks) so the
player must be tactical — focus-fire the Healer first or the room never dies; bait the Tank; use chokes/traps to
split the pack. This REUSES the EXISTING EnemyBrain role AI (Tank charges / Healer pulses Enemy.Heal on the most-
wounded ally / DPS marches) — already proven by EnemyFamilyTestSpawner (3 DPS + 1 Tank + 1 Healer). The seed budget
allocates the role composition: higher budget → more healers + tighter coordination (AI strategy points) → harder,
more strategic rooms, not just bigger HP bars. Encounter = data: each room recipe carries its role roster + the
component layout (chokes/fake-walls) that makes the role mix matter.

### Phasing (proposed)
- **v1 — Composer + 3 Village2 chunks** (OuterPerimeter / CourtyardCamp / KeepCore), anchor-relative capture + compose.
- **v2 — Gameplay component library** (Trap, Choke, FakeWall, Bridge=RegionGate, Maze tile) as parameterized chunks.
- **v3 — JSON dungeon builder** (recipe → composed dungeon) + an optional procedural recipe generator.

## Encounter Spawner (the runtime arm — owner 2026-06-21 "we will need some type of enemy spawner")
A chunk/room carries **spawn anchors** (the owner's Village2 redo already has them: EnemySpawnPoints group +
Spawn_Gate/Chokepoint/Courtyard{E,W}/Inner{E,W}/Keep/Rear — a per-zone layout). The Encounter Spawner reads those
anchors and spawns a **role-mix per zone** (Tank/Healer/DPS via EnemyBrain) so each room demands tactics. REUSE the
existing path — do NOT write a new spawner from scratch:
- `GarrisonController.SpawnInitialGuards` already reads the EnemySpawnPoints group + builds via `EnemyFactory` + tethers.
- `EnemyFactory` + `EnemyBrain` roles + `GarrisonStatBlocks` level-scale = the build + role + difficulty.
- `Village2RaidController` activates it on entry.
The delta: generalize GarrisonController into a recipe-driven **EncounterSpawner** that takes a per-room roster +
role mix + seed budget and spawns it at the room's named anchors (zone-aware: a Healer behind the chokepoint, DPS at
the courtyard, a boss at Spawn_Keep). The seed budget sets roster size + healer/coordination density. Markers travel
with the chunk (captured anchor-relative) so any composed room brings its own spawn layout.

## Lighting / atmosphere component (owner 2026-06-21 "lighting tool to add torches, keep it darker")
A dungeon should be DARK + lit by torches for mood. Two parts, both composable:
- **Torch component:** a placeable chunk = point light (warm, short range, shadows) + flame VFX + **TorchFlicker**
  (already exists in Assets/_Village2/TorchFlicker.cs — reuse it). Place along walls/anchors via the same recipe
  system (a torch is just another anchor-relative component); the seed/recipe can scatter them at intervals.
- **Darkness profile per chunk/dungeon:** a lighting profile the builder applies for dungeon scenes — LOW ambient
  (override Village2Playable B0's bright Trilight), darker fog, dim/absent directional sun — so torches actually
  read as the light source. A "Dungeon" lighting profile vs the "Village/Outdoor" one; the SceneRecipe names which.
This makes dungeons atmospheric (dark, torch-lit) without hand-lighting each scene — author the profile + torch
density once, the builder applies it.

**Torches at set intervals along WALLS (owner 2026-06-21):** the torch placer scans a scene's WALL renderers and
drops a torch (point light + TorchFlicker) every N metres along them at a set height — automatic, even spacing, no
hand-placement. Combined with the Dark profile (skyline/ambient → darkness), this is a one-call "apply dungeon
ambience to <scene>" pass. APPLIES TO VILLAGE2 (owner request): once the HUD + colliders are confirmed, run the same
dark+wall-torch pass on Village2's StrongholdRoot walls → instant atmospheric, torch-lit stronghold. Reuse
DungeonComposer.ApplyDarkLighting + a generalized wall-interval torch placer; scripted, so it applies to ANY scene.

## CORE LOOP — EARN the flip (owner 2026-06-21 "empty hallways ok for now, want to make them earn flipping an outpost")
Content density is NOT the point — the EARN is. Sparse/empty hallways between encounters are fine for v1; pacing >
packing. The reward loop: **traverse → clear the outpost's KEY role-mix encounters → EARN the flip** (the enemy
outpost/stronghold converts to a PLAYER settlement = WO-475 stronghold->player-settlement, with its economy/buildings).
The flip is GATED behind clearing — never free on entry. So a few meaningful encounters (a healer-backed room, a
choke ambush, a boss at Spawn_Keep) carry the whole outpost; the empty halls are connective tissue. This makes the
seed budget meaningful (it sizes the gauntlet you must beat to earn the territory) and gives the dungeon-creator its
progression spine: clear-to-claim. Implement: GarrisonController.OnCleared / Village2RaidController.HandleCleared
(already the victory hook) -> StrongholdConversionService.Convert (WO-475). Don't auto-claim; require the clear.

## TWO dungeon purposes (owner 2026-06-21 — do NOT conflate)
The composer + component catalog + seed budget serve TWO distinct content types:
1. **Outposts = flippable TERRITORY.** Finite, meta-game. Earn-the-flip (clear -> convert to player settlement,
   WO-475). Claimable, becomes yours, feeds the economy. The "earn the flip" loop above applies HERE.
2. **Depth content = challenge DIVES.** NOT flippable / not owned. The roguelike "go deeper = harder" layer: depth
   drives a bigger seed budget -> larger/harder AI-sculpted dungeons, run for loot/XP/glory then exit. This is where
   the AI sculpts the really hard ones. No conversion — the reward is loot + progression, not territory.
Same engine, different intent: an outpost recipe is tagged claimable + has a conversion hook; a depth recipe is
tagged non-claimable + scales its seed by depth. The earn-the-flip hook fires ONLY for outposts.

**Depth-content REWARDS (owner 2026-06-21):** depth dives feed the meta-game progression as gated rewards —
- **4th companion unlock** — clearing a specific deep dungeon unlocks the fourth companion (a milestone gate).
- **Quest-gated legendary gear** — specific named dungeons are quest targets that drop the legendary gear.
- **Crafting supplies** — depth runs are the source of the rare materials the crafting system needs.
So depth content isn't just loot-spam: specific authored dungeons are progression gates (companion/legendary/quest),
while procedurally-seeded ones supply the grind (crafting mats, XP). The recipe carries its reward table + any
unlock/quest hook. Ties to: StoryCompanion roster (4th slot), GearCatalog legendary tier, GearCraftingRecipeCatalog mats.

## AI-SCULPTED dungeons (owner 2026-06-21 "dynamic enough AI can sculpt really hard ones?") — YES, for DEPTH CONTENT
Because a dungeon is DATA (a JSON recipe of components + seed budget), an AI can AUTHOR it — generating/mutating
JSON is exactly what an LLM does well; no code-gen. Four things make AI-sculpted "really hard BUT fair" dungeons
achievable on THIS architecture:
1. **Vocabulary = the component catalog.** Hard dungeons are clever COMPOSITION (healer behind a choke you must
   flank; a fake wall hiding the only path past a trap gauntlet; a bridge over a pit covered by archers), not just
   bigger numbers. The richer/more-parameterized the catalog (v2), the more expressive the AI's "level design."
2. **Validation keeps it solvable.** The composer enforces connectivity + navmesh reachability (the SEAM-REACHABLE
   oracle pattern), so an AI can't emit an unbeatable/broken dungeon — "hard, not impossible" is guaranteed by the engine.
3. **The eval loop = our existing headless autopilot.** The SAME instrument-first/AutoPilot fleet we use for bugs
   becomes the dungeon EVALUATOR: AI emits a recipe -> the fleet PLAYS it -> oracles measure (time-to-clear, deaths,
   choke/healer effectiveness, did-it-strand) -> the AI refines. A closed generate->test->tune loop = genuinely hard
   AND beatable, proven by data not vibes.
4. **Seed budget = a difficulty target the AI designs to.** "A dungeon for a lvl-30 player, ~8 min, that requires
   kiting the healers" -> AI emits a recipe -> fleet verifies it hits the target band.
HONEST caveats: hard != fun automatically — the OWNER seeds the style/aesthetic; the AI sculpts variations within that
design language (force-multiplier, not replacement). Needs the rich catalog (v2) + scoring oracles + autopilot-
traversable dungeons first. But theoretically AND practically: yes — this design is purpose-built for it.

## Open questions for the owner (scope this before building)
1. Chunk taxonomy: confirm the starter set (OuterPerimeter, SmallCamp, Stronghold, DungeonRoom?) + naming.
2. Authoring loop: drag collections in-editor + a "Capture Collection (with anchor)" menu, then a "Compose Scene
   from Recipe" menu? (Mirrors the castle flow you already know.)
3. Scope of v1: just the composer + the 3 Village2 chunks, or include a dungeon-room set?

## NOT touch (until scoped)
The shipping scenes; the existing Village2Playable phases (extend, don't break). This WO is the design spine; a
build WO follows once the taxonomy + authoring loop are confirmed.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
