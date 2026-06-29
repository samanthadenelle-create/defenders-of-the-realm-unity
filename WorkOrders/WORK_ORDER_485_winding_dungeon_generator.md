# WORK_ORDER_485 — Winding Dungeon Generator (procedural recipe emitter for DungeonComposer)

**Status: DESIGN-COMPLETE / READY TO IMPLEMENT — BUT QUEUED BEHIND THE KNIGHT.**
Do NOT start until the V1 single-Knight + overworld real-time battle (WO-481 / WO-482) is felt-verified
and closed by the owner. This is depth-content tooling, not the V1 critical path.

**Spine:** the **v3 "procedural recipe generator"** slice of **WO-479** (Scene/Dungeon Creator). It does
NOT replace the composer — it **emits a recipe JSON** that the EXISTING `DungeonComposer` consumes.

**Lane:** World/Environment + Combat/AI (editor tooling + data; no shipping-scene edits). Single-file new code.

**WO-number reconciliation:** 480/481/482/483 used (484, 485 free per filesystem). `CLI_LANES_WO_NUMBERS.md`
authority line says "Next free WO = 430" but 430–483 are since minted on-board; **485 is the next clean slot
under the WO-479 spine.** Slot into the World/Dungeon lane in `MASTER_PIPELINES_BACKLOG_2026-06-06.md` +
`CLI_LANES_WO_NUMBERS.md` when this lands (sibling of WO-479/480).

---

## Owner vision (verbatim intent, 2026-06-23)
> "auto generate one, and it doesn't need to be mine — the idea was seriously just random hallways and
> making it winding so they had to work to get to the boss."

Plus the authoring loop she stated:
> "you [CLI] create, I'll go in and modify a few pieces by sight, and you take the offset."

So: **CLI generates** a winding dungeon → **owner nudges a few pieces visually in-editor** → **CLI
re-captures** the adjusted layout via the EXISTING `Village2LayoutDump` (offset capture) so her tweaks
**persist into the recipe and are never regenerated away.** Her hand-tweaks are sacred + reproducible.

**Design intent (decisive):** NOT a pure random maze (tedious/unfair). ONE guaranteed **winding critical
path** entrance → boss that snakes (forces them to work), PLUS optional dead-end / loop branches for loot.
Boss room at the terminus. "Hard, not impossible" — guaranteed solvable by the validator.

---

## What we ALREADY have (REUSE — do NOT greenfield)

| Concern | Existing asset (REUSE) | The small NEW delta |
|---|---|---|
| **Build dungeon geometry** | `Assets/Editor/DungeonComposer.cs` — `Room`/`Corridor` model → floors + corridors + dark lighting + torches + nav bake → `Dungeon_Demo.unity`. v1 = built-in recipe; header says "v2 reads JSON". | Make the generator **emit a recipe JSON** the composer reads. NO new per-tile grid builder. |
| **Recipe consume / replay** | `Village2Playable.ReplayRecipeIntoScene` (instantiates a placement recipe); composer's `DemoRooms()/DemoCorridors()` model is the target shape. | Generator outputs `rooms[]` + `corridors[]` (+ branches + roster + boss) JSON; composer deserializes it instead of `DemoRooms()`. |
| **Owner offset capture** | `Village2LayoutDump.Dump` (read-only sub-tree → JSON: path, prefab, local+world TRS); `Village2Playable.CaptureSelectedToRecipe`; `Village2PlacementRecipe.json`. | A "**re-capture adjusted dungeon → recipe**" menu that diffs owner-moved pieces back into the room/corridor recipe (offset capture). |
| **Encounter spawning** | `GarrisonController.SpawnInitialGuards` (reads `EnemySpawnPoints` group + `EnemyFactory` + tethers); composer's `BuildEncounter` already wires a `GarrisonController` per room **by reflection**. | Generator fills each room's **roster + role mix**; composer's existing `BuildEncounter` consumes it. **Do NOT write a new spawner.** |
| **Enemy roles** | `EnemyBrain` Tank/Healer/DPS (proven by `EnemyFamilyTestSpawner`). | Generator allocates role mix per room from the seed budget (healer behind the choke, boss at the end). |
| **Difficulty scaling** | `GarrisonStatBlocks.ApplyLevelScale` + `minLevel/maxLevel/threatLevel` (composer already sets these). | Generator computes level/threat per room from the seed **budget**. |
| **Clear → flip (outposts)** | `Village2RaidController` (`OnCleared` victory hook) → `StrongholdConversion` (WO-475). | Generator tags a recipe `claimable` (outpost) vs `depth` (non-claimable dive). Wire only the existing hook. |
| **Gates / nav** | RegionGate (WO-467) + in-scene `NavMeshLink` (AI) + `HeroLinkCrossing` (input-driven hero) + the WO-479 "gate funnel blocker panels"; `ArenaNavMeshBaker` / composer's `NavMeshBuilder.BuildNavMesh`. | Generator may place a RegionGate at a corridor mouth (choke); composer auto-fits the two funnel panels. Reuse the bake the composer already runs. |
| **Dark / torch atmosphere** | `DungeonComposer.ApplyDarkLighting` + `PlaceRoomTorches` + `TorchFlicker`. | None — the composer already does this; the generator just emits more rooms for it to light. |
| **Reachability oracle** | SEAM-REACHABLE pattern (WO-479 §validation); composer's `NavMesh.SamplePosition` entry check; AutoPilot fleet (run-defenders skill). | A **validator** the generator runs on its own graph BEFORE emit, + a headless traverse oracle. |

**The ONLY substantial new code = (1) the generator (recipe emitter) + (2) its validator.** Everything else
is wiring existing classes. If you find yourself writing a spawner, a tile builder, or a lighting pass — STOP,
you're reinventing; route back to the composer.

---

## 1. SEEDED procedural generator (recipe emitter) — NEW

New file: `Assets/Editor/WindingDungeonGenerator.cs` (`DeNelle.Editor`, editor-only, batchmode-runnable).

**Output:** a recipe JSON (`Assets/_Dungeons/dungeon-recipe-<seed>.json`) shaped to what the composer reads:
```json
{
  "seed": 12345, "budget": 40, "claimable": false,
  "rooms": [
    { "id":"Entry", "cx":0,  "cz":0,  "w":14, "d":14, "isEncounter":false },
    { "id":"R1",    "cx":18, "cz":12, "w":10, "d":12, "isEncounter":true,  "roster":["hollow-warrior","hollow-walker","hollow-walker"] },
    ... ,
    { "id":"Boss",  "cx":..,"cz":..,  "w":20, "d":20, "isEncounter":true, "isBoss":true, "roster":[...] }
  ],
  "corridors": [ { "fromId":"Entry","toId":"R1","width":4 }, ... ],
  "branches":  [ { "fromId":"R2","toId":"Loot1","width":3,"deadEnd":true } ]
}
```

**Algorithm (decisive):**
1. **Winding critical path FIRST.** Lay the entrance → boss spine as a **self-avoiding walk on a coarse grid**
   (seeded RNG): step N/E/S/W, forbid revisiting a cell, bias toward turns (reject ~60% of "straight again"
   moves) so the path **snakes** rather than runs straight — this is "make it winding so they work to get to
   the boss." Path length scales with budget (see §3). Place a `Room` at each spine node, a `Corridor` between
   consecutive nodes. The LAST spine node = the **Boss** room (`isBoss=true`, terminus).
2. **Branches SECOND (optional).** Spend remaining budget spawning dead-end / short-loop branches off random
   spine rooms (`deadEnd=true`) carrying loot/optional encounters — reward exploration, never block the spine.
3. **Encounters.** Mark interior spine rooms `isEncounter=true`; the boss room is always an encounter. Roster
   + role mix per room from §3. Entry room stays empty (safe seat).
4. **Determinism:** seed RNG with `new System.Random(seed)`; **same (seed, budget) → byte-identical recipe.**
   No `UnityEngine.Random`, no time/Guid in the geometry math.

Geometry uses the composer's existing room/corridor box model — the generator only chooses **positions, sizes,
connectivity, and rosters**. It does NOT build GameObjects (the composer does).

---

## 2. Reachability VALIDATION (SEAM-REACHABLE oracle) — NEW (part of the generator)

Two layers, both required:

**A. Graph validation (pre-emit, in the generator):** before writing JSON, build the room/corridor adjacency
graph and **BFS/flood-fill from Entry**; assert **every room (boss + every branch) is reachable** and the boss
is on the spine. If a self-avoiding walk paints itself into a corner (can't reach budget length), **re-roll
that segment** (bounded retries), never emit a stranded dungeon. Log `[Flow:DungeonGen] reachable=N/N`.

**B. Navmesh validation (post-build, in the composer pass):** after the composer bakes the navmesh, sample-walk
**Entry → Boss** via `NavMesh.CalculatePath` (or AutoPilot traverse) and assert a complete corridor path exists
(`PathComplete`). Fail the build (non-zero, logged) if the boss isn't navmesh-reachable from the entry seat.
This generalizes the composer's existing single-point `NavMesh.SamplePosition` entry check to a full traverse.

"Hard, not impossible" is an **engine guarantee**, not a hope — an AI- or seed-authored dungeon physically
cannot emit an unbeatable layout.

---

## 3. Seed BUDGET scaling (WO-479) — deterministic

One scalar `budget` (from dungeon depth reached + player level) the generator spends across:
- **Spine length** — `criticalRooms ≈ clamp(3 + budget/8, 3, 12)`. Bigger budget → longer, twistier path.
- **Branch count** — leftover budget → more dead-end loot branches.
- **Enemy count / level** — per room: `roster size` and `minLevel/maxLevel/threatLevel` scale with budget;
  composer already forwards these to `GarrisonController` → `GarrisonStatBlocks.ApplyLevelScale`. **Reuse it.**
- **Role mix density** — higher budget → more **Healers** + tighter packs (healer-behind-a-choke forces the
  focus-fire tactic, per WO-479 encounter design). Boss room gets the toughest roster.

Same `(seed, budget)` → the same dungeon (debug / fair-retry / shareable), mirroring the AutoPilot seeded-chaos
principle. Budget→allocation is a pure deterministic function (document the formula in the file header).

---

## 4. Encounters via the EXISTING path (do NOT write a new spawner)

The composer's `BuildEncounter(room)` already: creates `Encounter_<id>` → an `EnemySpawnPoints` group with a
spawn per roster entry → adds `GarrisonController` (by reflection) → sets `activateOnStart`, `threatLevel`,
`minLevel`, `maxLevel`, `enemyTypeIds`. **The generator's only job is to fill `roster` + the level/threat scalars
per room.** Zone-aware placement (healer behind a choke, DPS forward, boss at the terminus) comes from which
room the generator tags and the role mix it writes. `GarrisonController.SpawnInitialGuards` + `EnemyFactory` +
`EnemyBrain` roles do the runtime spawn. No new spawner code.

---

## 5. Gates / nav

- **Choke gates (optional):** the generator may tag a corridor as a `gate` choke; the composer places a
  RegionGate (WO-467) there and **auto-fits the two funnel blocker panels** to the arch (WO-479 §"Gate
  refinement") so movement is physically funneled through the opening — doubles as the natural choke for a
  healer-backed ambush.
- **AI nav:** in-scene `NavMeshLink`(s) at any gate so `EnemyBrain` AI (SetDestination) auto-crosses.
- **Hero nav:** `HeroLinkCrossing` pair only if a corridor is a true scene seam (depth dungeons are single-scene,
  so usually just the merged navmesh — no hero warp needed inside one dungeon).
- **Runtime navmesh:** the composer already runs `NavMeshBuilder.BuildNavMesh()`; reuse it (or `ArenaNavMeshBaker`
  if the dungeon loads additively). Do NOT add a new bake path.

---

## 6. The owner authoring loop (her tweaks are sacred)

1. **Generate:** `WindingDungeonGenerator.Generate(seed, budget)` → recipe JSON → `DungeonComposer.BuildFromRecipe(json)`
   builds `Dungeon_<seed>.unity`.
2. **Owner modifies by sight:** opens the scene, nudges a few rooms/corridors visually (no drag-drop authoring of
   logic — pure spatial taste, the allowed kind per `never-dragdrop-or-manual-playtest`).
3. **CLI offset-capture:** run the **re-capture** menu (built on `Village2LayoutDump.Dump`'s read-only sub-tree
   dump) → reads the adjusted `DungeonRoot` transforms → **writes the moved positions/sizes back into the recipe
   JSON** (offset capture). The recipe now encodes her layout.
4. **Reproducible:** rebuilding from the updated recipe reproduces HER dungeon — a regenerate from the raw seed
   never overwrites her tweaks, because the captured recipe (not the seed) is the source of truth once captured.
   (Same "regen never reverts hand-dialing" guarantee WO-479 established for Village2/castle.)

Deliver this as composer menu items: `Generate Winding Dungeon (seed+budget)` and `Re-Capture Adjusted Dungeon → Recipe`.

---

## 7. Reuse map (summary)

| Piece | Reuses | New delta |
|---|---|---|
| Geometry build | `DungeonComposer` rooms/corridors/lighting/torches/bake | composer reads JSON not `DemoRooms()` |
| Recipe emit | recipe model shape (`Room`/`Corridor`) | **NEW generator** |
| Validation | SEAM-REACHABLE pattern, `NavMesh.SamplePosition/CalculatePath`, AutoPilot | **NEW validator** (graph BFS + traverse oracle) |
| Spawning | `GarrisonController.SpawnInitialGuards`, `EnemyFactory`, `EnemyBrain` | generator fills roster/role mix only |
| Difficulty | `GarrisonStatBlocks.ApplyLevelScale`, threat/level fields | generator computes from budget |
| Capture loop | `Village2LayoutDump.Dump`, `CaptureSelectedToRecipe`, placement recipe JSON | recapture-into-recipe menu |
| Gates/nav | RegionGate (467), NavMeshLink, HeroLinkCrossing, funnel panels (479) | optional gate tag per corridor |
| Flip (outposts) | `Village2RaidController.OnCleared` → `StrongholdConversion` (475) | `claimable` recipe tag |

**Substantial new code is ONLY: `WindingDungeonGenerator.cs` (emitter) + its validator + a small
`DungeonComposer.BuildFromRecipe(json)` + recapture menu.** Anything more = reinvention.

---

## 8. Acceptance criteria + HEADLESS verification

**Functional:**
- [ ] `Generate(seed, budget)` emits a recipe JSON with a **winding** spine (≥ N turns; not a straight line),
      a boss room at the terminus, and `corridors` connecting every spine room.
- [ ] `DungeonComposer.BuildFromRecipe(json)` builds a dark, torch-lit, navmesh-baked, walkable `Dungeon_<seed>.unity`
      with `GarrisonController` encounters per encounter room (boss roster toughest).
- [ ] **Determinism:** generating twice with the same `(seed, budget)` yields **byte-identical** recipe JSON.
- [ ] **Budget scaling:** budget 20 → fewer/shorter rooms + lower levels; budget 60 → longer/twistier + more
      rooms + higher levels/more healers. Deterministic mapping, documented in the header.
- [ ] Owner authoring loop round-trips: generate → move a room → re-capture → rebuild reproduces the moved layout.

**HEADLESS verification (run-defenders skill; instrument-first, §12):**
- [ ] **Reachability oracle:** graph BFS reports `reachable=N/N` (every room incl. boss + branches) pre-emit;
      re-roll on a stranded segment is exercised by a deliberately tight budget seed.
- [ ] **Navmesh traverse oracle:** post-build `Entry → Boss` `CalculatePath` returns `PathComplete`; the
      **AutoPilot fleet** traverses entrance → boss and reports arrival (no strand, no softlock).
- [ ] **Same-seed determinism:** headless generates seed 12345 twice, asserts identical JSON hash.
- [ ] `[Flow:DungeonGen]` traces (Enter/Step/Warn/Fail) cover spine-walk → branch → validate → emit; captured
      in `break-log.jsonl`.

**Compile gate:** `DeNelle.Editor.CompileGate.Run` → `COMPILE_GATE_OK`, NUL-byte clean, braces balanced on every
`.cs` touched (§1). New code is editor-assembly only (`DeNelle.Editor`) — no runtime assembly change.

---

## 9. NOT touch / scope

- **AFTER the Knight.** Queued behind WO-481/482 V1 (single Knight + overworld real-time battle). Do not start,
  do not imply it starts now. This is the **depth-content layer**, not the V1 arena critical path.
- **Extend the existing toolchain, do NOT fork it.** Generator emits into `DungeonComposer`; capture reuses
  `Village2LayoutDump`; spawning reuses `GarrisonController`. No new composer, no new spawner, no new tile/grid
  builder, no new bake path, no new lighting pass.
- **No shipping-scene edits.** Only generated `Dungeon_<seed>.unity` scenes + JSON under `Assets/_Dungeons/`.
  Never hand-edit `Village.unity`/`Village2.unity` (§3); the recapture menu is read-only on the scene
  (`Village2LayoutDump` contract — opens, reads, writes JSON, never resaves the scene).
- **Depth vs outpost:** this generator defaults to **depth** (non-claimable, seed-scaled dives). The outpost
  earn-the-flip path (`claimable` + `StrongholdConversion`) is a one-tag reuse, not new logic — keep it a flag,
  don't build a conversion system here (that's WO-475).
- **AI-sculpt loop is OUT of scope** for this WO — this delivers the deterministic generator + validator that an
  AI sculptor would later drive (WO-479 §AI-SCULPTED). Generate→test→tune lives in a follow-up.

---

## Suggested files
- NEW `Assets/Editor/WindingDungeonGenerator.cs` — generator + validator + menus.
- EDIT `Assets/Editor/DungeonComposer.cs` — add `BuildFromRecipe(string json)` (deserialize → existing build path).
- NEW `Assets/_Dungeons/` — output recipe JSONs.
- REUSE `Assets/Editor/Village2LayoutDump.cs` — recapture (or a thin `DungeonLayoutDump` modeled on it).
