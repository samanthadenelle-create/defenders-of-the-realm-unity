> ## RECONCILED 2026-08-08 - true status is PARTIAL (authored complete through Phase 2; traversal broken)
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: slices 1, 2, 1b, 3-5, 6-8 and Phase 2
> all landed (6e2ceb1b, 05e112ba, 33354ea9, 1ea03b84, 335f6b81, a77384f0, 195ae8c8). BUT canon
> 2026-08-08 records PathPartial and floor delta start-to-stop = 0.00m, so the descents do not function.
> The previous Status line read "SPEC - READY (phased)" and was wrong.

# WORK ORDER 1001 — Deep Dungeon Program: large multi-level dungeons as data + the engine to run them

**Status:** PARTIAL - authored complete through Phase 2; traversal broken (reconciled 2026-08-08) · **Silo:** Dungeons / content / systems · **For:** CLAUDE CLI · **Date:** 2026-08-07
**PO:** Samantha (owner) · **Author:** UI seat · **UI-seat block:** 1000–1099
**Owner:** *"create other full complex dungeon instructions … for large real depth levels … we use a portal that takes you to a new scene."*
**Related:** WO-1000 (starter-dungeon visual overhaul), the VFX facade (WO-884/885 — candles/fog), the harvest-VFX subtlety ruling (WO-890).

> **⚠ SPEC CORRECTION 2026-08-07 (CLI, verified at source).** §1 below claims Pipeline A
> "supports **multi-level via `StairDown`/`StairUp`** sockets". **It did not.** Not partially — at
> all, and in a way no oracle noticed. Three things each independently forbade a vertical mate:
> both stair sockets pointed **down** at local Y=0 (so a pair scored `align = -1` against a +0.25
> threshold and carried no height to mate across); `TryMate`'s corrective nudge was planar-only (a
> Y gap could never close); and `RoomsOverlap` was XZ-only, so a *correct* stack — whose footprints
> coincide by design — was reported as an overlap, which is a hard bake abort. There had never been
> a multi-level bake in the tree, and no graph JSON referenced a stair socket in any edge.
> **Slice 1 has since fixed all three** (commits `6e2ceb1b` + the descent probe). Read §2.1 as
> "build it", not "verify it".

## 0. The vision
Each dungeon is a **portal-loaded standalone scene** that is a **large multi-level DESCENT**: you climb *down* level after level, each deeper floor darker + harder with richer loot, culminating in a **boss** at the bottom. The tension engine is the existing **oil/lantern darkness** pillar: your light burns down as you go deeper — **push for the deep-boss legendary loot, or extract and bank what you have.** Themed distinctly so dungeons feel different, not recolored.

## 1. Pick the pipeline — extend Pipeline A (data-authorable), don't hand-code each
Two pipelines exist (verified):
- **Pipeline A — JSON room-graph → composer → baker → scene** (`dungeon-graphs/<id>.json` → `GraphDungeonComposer.ComposeAndBake` → `DungeonBaker.BakeFromFile` → `Assets/Scenes/DungeonCompose/<id>.unity`). **Data-authorable** (add a room = one node + one edge), supports **multi-level via `StairDown`/`StairUp`** sockets, auto-dresses (torches+props via `DungeonDresser`), auto-adds the exit portal, bakes nav. **Model:** `dungeon-graphs/dg_starter_loop.json`. **Lacks:** boss encounters, loot/chests, oil/lantern, non-Hollow enemies, traps/locks.
- **Pipeline B — Healer's Cottage** (`DungeonSceneBuilder.cs`, hardcoded C# per dungeon) — has the full pillar set (oil, lore, checkpoints, chests, mini-boss, crafting) but a new dungeon = a new 300-line builder method. Not scalable to "many complex dungeons."

**Decision: make Pipeline A the complex-dungeon engine** (data in, full-featured scene out), so every future dungeon is a JSON graph, not code. §2 closes the gaps; §3 authors the dungeons on top.

## 2. PHASE 1 — extend Pipeline A into a full complex-dungeon engine (the enabler)
Wire the missing pillars into the composer/baker/dresser so a graph JSON can express them. Prioritized:

1. **Deep multi-level descent (verify + scale).** `StairDown`/`StairUp` room prefabs + `stair_down_01`/`stair_up_01` sockets exist (`DefaultDungeonRoomsBuilder.cs`). Confirm the composer solves vertical socket mates across Y-levels and bake a **5+ level** graph end to end (the starter loop is flat). Extend the `DungeonGraph` schema (`GraphDungeonComposer.cs:42-77`) with a per-node `level`/depth if the solver needs it. **Deliverable: a dungeon can descend N floors via stairs.**
2. **Per-encounter ENEMY FAMILY (beyond Hollow-only).** `OutpostEnemyGroupSpawner` spawns only a weighted Hollow-skeleton mix (`WeightedSkeletonId:211`). Extend the `EncounterSpec` (`DungeonComposeLayout.cs:57-81`) + spawner so `kind` can be `hollow-group | orc-group | undead-group | mixed` and select from the real roster (`enemies.json`: hollow-walker/warrior/rogue/acolyte/mage/reaper/brute, necromancer, orc-berserker/shaman/necromancer/raider, troll, ogre). **Depth scales the family/tier** (walkers up top → warriors/mages/brutes deep).
3. **Boss encounter wiring.** `BossKeep` room archetype exists as geometry only. Wire it: a graph node with `archetype:boss` + a `boss` block (`{ enemyType, displayName, threat }`) places a boss encounter (mirror `EncounterTrigger.ConfigureBoss` / the Apothecary mini-boss `DungeonSceneBuilder.BuildMiniBoss:1334`); victory **unlocks the exit + grants deep-boss loot**.
4. **Loot / chests from data.** Let a graph node (esp. `RewardVault`/`SecretAlcove`) carry `chests[]`/`containers[]` with a `lootTableId`/`rewardKey`; place `BreakableContainer` (`lootTableId`, default `crate-common`) + narrative chests (`DungeonLootGrant`, `dungeon-chest`/`dungeon-deepboss`). Tables already exist in `loot-tables.json` (`dungeon-hollow`, `dungeon-miniboss`, **`dungeon-deepboss`** = the only legendary-component source, "5+ deep payoff"). Loot banks to the `VillageInventory` larder.
5. **Oil / lantern in composed scenes (the tension engine).** The `Lantern.cs` oil pillar (fuel drain, range/intensity fall with oil, oil-stone refills, HUD `DungeonHudController`) is fully built but only wired into Cottage. Wire it into composed dungeons: `DungeonController` hands the `Lantern` to the composed scene; place **oil stones** from graph data (a node flag / a `oilStones[]` block). **Deliverable: your light burns down as you descend a composed dungeon, refillable at oil stones.**
6. **The darkness risk-reward CONSEQUENCE (close the parked ~10%).** Today darkness only shrinks visibility; `RandomEncounterTable.Roll(...inDarkness:false...)` (`EncounterTrigger.cs:286`) accepts the param but it's hardcoded false. Implement the consequence: **low oil / deep dark = higher ambush odds + the deep-boss/legendary loot only reachable past the dark floors** — the literal "push for loot vs extract before your light dies" choice. Extract points (the auto exit portal) on each level let the player bank and leave.
7. **Simple hazards + locks (optional depth — art exists, logic thin).** Trap floor tiles exist as art only (`floor_tile_big_spikes/grate`) — add a lightweight trap-trigger (step → damage/telegraph) usable from graph data; and a **locked door + key** (no support today) so a dungeon can gate the deep floor behind a key from a side branch. Illusory/secret walls already work (`Doorway.Illusory`, `SecretAlcove`) — use them for optional treasure. Keep this minimal; it's the "complex" spice.

Each of #1–#7 is its own reviewable slice; ship in order. #1–#5 are the must-haves for "full complex deep dungeon"; #6 is the soul; #7 is polish.

## 3. PHASE 2 — three large themed deep dungeons (authored as graphs)
Each = a `dungeon-graphs/<id>.json` (nodes from the §1 room library, edges by socket mate, per-node `encounter`/`boss`/`chests`/`oilStone`), its own portal-scene, a multi-level descent, distinct theme via KayKit dressing + the candle/fog VFX facade. Design spine (fill exact room-by-room graph at build):

### 3A. `dg_sunken_vault` — The Sunken Vault of Elarion (early/mid · 4–5 levels)
- **Fantasy:** the drowned treasury of the forgotten civilization; masonry half-swallowed by the deep.
- **Theme/dress:** KayKit stone + water-stained banners (blue), candles on brackets (candle-VFX light), **low ground-fog/haze** pooling on the floors (subtle). Palette cool grey-blue.
- **Descent:** Entrance → combat corridors (ChokePoint/Straight) → 4-way Intersection hub → RewardVault (side) + SecretAlcove (illusory wall) → StairDown ×4 to deeper flooded levels → BossKeep.
- **Enemies:** Hollow (walkers up top → warriors + acolyte-healers deeper). **Boss:** a **Hollow Warden** (hollow-warrior boss-scaled). 
- **Hazard:** grate/spike trap tiles on the flooded floor (#7).
- **Loot:** `dungeon-hollow` per encounter; `dungeon-chest` vaults; `dungeon-miniboss` on the Warden. Oil stones every 1–2 levels.

### 3B. `dg_bonecrypt` — The Bonecrypt (mid · 5–6 levels)
- **Fantasy:** an undead crypt where a Hollow necromancer raises the old dead.
- **Theme/dress:** Halloween bits **coffins/sarcophagi**, dark-green necrotic candlelight (candle-VFX tinted), **poison-green haze** (subtle), cobwebbed KayKit. Palette dark green/black.
- **Descent:** Entry → TJunctions branching to LoreShrines (the crypt's history) → CombatChambers → **locked door (#7)** gating the deep floor, key from a SecretAlcove side branch → StairDown ×5 → BossKeep.
- **Enemies:** hollow-reaper, hollow-mage, cellar-hollow (undead-group family, #2). **Boss:** a **Necromancer** (raises adds). 
- **Hazard:** illusory-wall secrets + a spike-trap corridor.
- **Loot:** `dungeon-deepboss` on the Necromancer = the **legendary reforge components** (the deep-delve payoff). Oil scarce (deliberately tense).

### 3C. `dg_ember_deep` — The Ember Deep (hard · 6+ levels)
- **Fantasy:** a magma-lit deep hold seized by orcs; the hottest, deepest crawl.
- **Theme/dress:** fire-pillar braziers (the gold-hall look — candle/fire VFX, brighter), **drifting smoke/steam** through the light, warm ember palette, KayKit + red banners. 
- **Descent:** Entry → CombatChambers → Intersection → **steam-jet trap corridor (#7 — PressurisedSteam VFX as a real hazard)** → RewardVaults → StairDown ×6 → BossKeep.
- **Enemies:** orc-berserker, orc-shaman, orc-necromancer, **troll/ogre** deep (orc-group family, #2). **Boss:** an **Ogre warlord** (or tie to the `orc-warlord` arena boss / a Dragon cameo). 
- **Hazard:** steam-jet traps (telegraphed) + spike tiles.
- **Loot:** richest `dungeon-deepboss`; multiple chests. Oil burns fastest (heat) — the tightest push-vs-extract.

## 4. Portal + scene wiring (per dungeon)
Each graph bakes to `Assets/Scenes/DungeonCompose/<id>.unity`. Add its **portal entry** in the overworld (mirror `CavePortalRepointInjector` / `DungeonWorldPortalSpawner` — a world portal/arch that loads the scene) and the **auto exit portal** (`DungeonExitInteractable`) back to the hub. Entry pose at the graph `entry` node.

## 5. Guardrails
- Every dungeon must hit the **WO-1000 visual bar** (enclosed/ceiling, textured KayKit stone, candle-VFX lighting, subtle fog/haze — never a plume, WO-890 rule, never daylight/open-sky).
- Data-first: dungeons are `dungeon-graphs/*.json`, not new builder methods (that's the whole point of Phase 1).
- Reuse the roster/loot tables/oil pillar that exist — extend, don't fork.
- Colourblind-safe telegraphs on traps (shape/motion, not colour).

## 6. Acceptance
**Phase 1 (engine):** a composed dungeon can descend 5+ levels; pick enemy families by depth; place a boss that gates the exit + grants deep-boss loot; place chests/containers with loot tables; the lantern-oil drains + refills at oil stones in a composed scene; low-oil/deep-dark raises ambush odds and gates the legendary loot (push-vs-extract works). `COMPILE_GATE_OK` + `REGRESSION_OK` + bake markers.
**Phase 2 (content):** the three graphs bake to portal-scenes, each multi-level, distinctly themed, hitting the WO-1000 visual bar; boss + deep-boss loot at the bottom; oil tension felt. `UI_CAPTURE_OK` — headless-capture each dungeon's entry + a deep level + the boss room; open the PNGs.
**Owner felt-close:** each dungeon feels like a real, large, atmospheric descent with a meaningful push-or-extract decision — not a flat greybox loop.

## 7. RESULT
`WorkOrders/WORK_ORDER_1001_deep_dungeon_program.RESULT.md` — Phase 1 slices landed, the three graph files, and screenshots per dungeon (entry / deep level / boss).

---
*Split option for CLI: Phase 1 (engine) and each Phase-2 dungeon can be minted as their own 1000-block WOs if you prefer smaller units — this umbrella holds the shared framework + sequencing.*
