# WORK ORDER 159 — Node Settlements: claim → auto-harvest → defend → deplete (territory control)

**Status: READY TO IMPLEMENT (phased)**
**Priority:** High — the real HARVEST pillar in the open world; ties nodes + build-mode + tribes into one loop
**Date:** 2026-05-30
**Lane:** gameplay/economy code (CLI) + world placement. NOT `VillageSceneBuilder`; OuterWorld nodes via `OuterWorldBuilder`/runtime; no bake by UI.
**Supersedes:** the earlier "add 8 more nodes" draft — that assumed hand-tap nodes; **this is the correct model.**
**Source:** owner — *"people can set up small settlements to protect nodes and harvest, so nodes persistent till empty."*

---

## The loop (owner-confirmed model)

**Claim → Build → Harvest → Defend → Deplete.** Not a walk-up-press-F clicker — an economic
territory-control loop in the open world:

1. **Nodes are persistent FINITE reserves.** A node holds a total reserve (e.g. 500 iron). It sits in
   the world and **persists until mined empty** — it does not cooldown-respawn per tap.
2. **The PLAYER claims a node by building a small SETTLEMENT** on/adjacent to it (build-mode placement
   out in the zone). The settlement is the player's harvesting outpost.
3. **The settlement AUTO-HARVESTS + DEFENDS.** It passively drains the node's reserve into the player's
   wallet over time (no manual tapping) AND defends the node from tribe raids (has defenders/defense).
4. **Wandering tribes (WO-160) raid it** — they're the threat the settlement defends against. This is
   why defense matters: an undefended/overrun settlement stops harvesting (or loses the node).
5. **When the node is mined empty:** the **node vanishes**, but the **settlement REMAINS** as a regular
   outpost (repurposable — a held position in that region, a forward base, future use). The player then
   expands to a new node.

This reframes `MineNode` from a cooldown tap into a **depleting reserve worked by a structure**, and it
makes the open-world economy about **claiming and holding ground**, with tribes as the pressure.

---

## RECONCILE — what exists vs the reframe

| Piece | State | This WO |
|---|---|---|
| `MineNode.cs` (cooldown + per-extract [F] tap, region-aware) | BUILT | **Reframe:** replace the cooldown/per-tap model with a **finite reserve** (`ReserveTotal`, `ReserveRemaining`) drained by a settlement, not the player. Keep the region/danger-aware yield scaling. |
| `OuterWorldBuilder` placing nodes | BUILT (8) | nodes stay as the claimable reserves; the [F]-tap interaction is removed/replaced by "build settlement here" |
| Build mode / `StructureFactory` / catalog (WO-108/148) | catalog + factory exist | the settlement is a **buildable** placed via build-mode at a node — reuse, don't reinvent |
| Wandering tribes (WO-160) | spec'd | the raiders that attack settlements — **WO-159 + WO-160 are the two halves of one loop** (harvest vs threat); build them aware of each other |
| Auto-harvest / worker / offline seams (WO-117/115) | spec'd | the settlement's passive drain reuses these faucet seams → GameState wallet |

---

## Phases

- **Phase 1 — Node as finite reserve (data + MineNode reframe).** `ReserveTotal` / `ReserveRemaining`
  on the node; region-scaled richness (danger ⇄ reward). No tap. Node persists in world until
  `ReserveRemaining == 0`, then despawns. *Ships:* persistent depleting nodes.
- **Phase 2 — Settlement buildable (claim + auto-harvest).** A `Settlement` structure placeable at a
  node via build-mode (`StructureFactory`/catalog). While standing + the node has reserve, it drains
  `ReserveRemaining` → player wallet at a harvest rate (reuse WO-117/115 faucet → GameState). On node
  empty: node despawns, **settlement remains** as an outpost. *Ships:* claim-and-harvest.
- **Phase 3 — Defense + tribe raids (the "protect").** Settlement has HP/defenders; wandering tribes
  (WO-160) target settlements within their roam. Overrun = harvest halts / settlement damaged (owner
  tunes: pause harvest vs lose the node). Hero/towers defend. *Ships:* the full defend loop.

## Terrain pass — uneven ground in deep regions (architect/world lane)

Author **rougher terrain around deep-region nodes** so defending them is harder. Lane note: this is
terrain/world authoring (`OuterWorldBuilder` / the exterior terrain + `Terrain_Plane_Slope`/hill tiles
per `world-construction-plan.md`), the **architect lane** — coordinate single-writer; it can be its own
sub-task from the gameplay code.

- **Goldfields (E) / Stoneback (W):** mostly flat / gently rolling — settlements here are **easy to
  defend** (wall a square, clear sightlines). The starter territory.
- **Mirewood (S):** broken, boggy, uneven — water cuts, raised hummocks; flanking approaches.
- **Ashwood (N):** the hardest ground — ridges, slopes, chokes, rubble; sightlines break, enemies come
  from elevation. Defending an Ashwood node is a real problem.
- Use `Terrain_Plane_Slope1–4` / hill tiles (catalog) as transitions; keep nav **valid** (agents must
  still path — uneven, not impassable) and re-verify pathing after the terrain pass. Don't punch holes
  in the NavMesh; rough ≠ broken.
- Tie the difficulty to `Depth` (zone doc): the **node deeper in** a region sits in **rougher** ground
  than one near its edge — depth raises both threat and terrain difficulty together.

## Constraints (CLAUDE.md §5/§6/§9)
- Node reserve data + `SettlementDef`/`SettlementState` → `DeNelle.Core` (pure data). Node + settlement
  runtime → `DeNelle.Village`/World. Village→Core only; wallet writes GameState directly.
- Reuse build-mode/catalog/`StructureFactory` for placement, WO-117/115 for the harvest faucet, WO-160
  tribes for the threat, `ZoneManager` for region scaling. **No parallel systems.**
- Settlement/node state persists via GameState/SaveSchema round-trip (bump schema; coordinate w/ SaveMigrator owner).
- No new currency, no UXML, no `System.Reflection`.

## Acceptance criteria
1. Nodes are **finite reserves** that persist in-world until empty, then despawn (no cooldown-tap model).
2. Player **builds a settlement** at a node (build-mode) to claim it; settlement **auto-harvests** the reserve → wallet over time (no manual tapping).
3. Node empties → **node gone, settlement stays** as an outpost.
3b. A **destroyed** settlement razes the site; the site is **locked for 3 game days** (build-mode shows "Razed — clears in N days"), then re-claimable if reserve remains.
3c. **Deep-region nodes sit in uneven/rough terrain** (Mirewood/Ashwood) that's harder to defend than flat safe-region nodes; NavMesh stays valid (rough, not impassable).
4. Settlement **defends** the node; **strong wandering enemies (WO-160) raid** settlements; an
   **insufficiently-supported settlement is DESTROYED** (claim lost, node reverts to unclaimed) — with
   a fair "under attack" warning + response window. Properly defended settlements survive and keep harvesting.
5. Region/danger scales node richness (deadly region = richer reserve), shared dial with WO-144/155/160.
6. Built on build-mode/catalog + WO-117/115 + WO-160 + ZoneManager — no parallel systems; brace balance; Village→Core only; persists save/load.

## Stakes — unsupported settlements get DESTROYED (owner 2026-05-30, locked)

A claim is a **commitment you must back up**, not build-and-forget. **Strong enemies wander near
settlements, and if the player does not properly support a settlement (defenders/garrison/hero
presence/defensive structures), it gets DESTROYED — the claim is lost** (node reverts to unclaimed, the
settlement is razed, in-progress harvest stops). This is the harsh-overrun answer to the old open
question — it's what gives the territory loop teeth.

- **Strong roaming threats near nodes.** Ensure the wandering tribes (WO-160) / roaming mobs near a
  node include **genuinely dangerous enemies** scaled to the node's region (`ThreatLevel`, danger tier ×
  depth) — strong enough that an undefended settlement is overrun. Richer node (deadlier region) ⇒
  stronger raiders ⇒ more support required (danger ⇄ reward, raised to the settlement level).
- **Uneven terrain makes deep-region defense harder (owner, locked).** Deeper/deadlier regions
  (Mirewood S, Ashwood N) get **rougher terrain around their nodes** — slopes, ridges, chokes, broken
  ground — so a settlement there is **geometrically harder to defend** than one on the flat safe-region
  farmland: enemies flank over rises, sightlines break, you can't just wall a tidy square. This is a
  terrain-authoring task (see the dedicated section below) and it reinforces danger⇄reward — the richest
  nodes sit in ground that fights you.
- **"Support" the player must provide:** garrison defenders, defensive structures (walls/towers from
  build-mode), pets, or the hero physically defending. A settlement with insufficient defense vs an
  incoming raid loses HP and, if not relieved, is **destroyed**.
- **Loss + 3-game-day site lockout (owner, locked).** A fully-destroyed settlement is **not instantly
  rebuildable** — the razed site is **blocked for 3 game days** before the player can build there again.
  Mechanics: on destruction, write a `razedUntilDay = currentGameDay + 3` timestamp on the node/site
  state (persisted in GameState alongside the zone records); the site rejects placement (build-mode shows
  "Razed — clears in N days") until the game-day clock passes it. After 3 days the site clears and the
  node (if it still has reserve) is re-claimable. This makes losing a node genuinely costly — 3 days of
  zero harvest from that site + the rebuild — so defense is mandatory, not optional.
  - Reuse the existing **game-day clock** (the offline/day system, WO-115 / day tracking) — do NOT add a
    parallel time system; read whatever `GameState` already uses for the day count.
  - A fair "under attack" alert + response window still precedes destruction (loss isn't silent).

## Open questions for owner
- **Build cost / limit:** does a settlement cost resources to place, and is there a cap on how many nodes you can hold at once? (Drives pacing — default: costs resources, soft cap by player/village level.)
- **Harvest rate vs reserve size:** tune so a node lasts a meaningful but finite time (minutes of presence / offline accrual via WO-115).

## Done checklist (CLAUDE.md §10)
- [ ] Node = finite reserve, persists till empty then despawns (MineNode reframed off cooldown-tap)
- [ ] Settlement buildable at node (build-mode/catalog); auto-harvests reserve → wallet
- [ ] Node empty → node gone, settlement remains
- [ ] Settlement defense + WO-160 tribe raids wired; overrun outcome per owner
- [ ] Region-scaled richness; persists save/load; built on existing systems
- [ ] Brace balance; Village→Core only; no new currency/UXML/Reflection
- [ ] `WORK_ORDER_159_node_settlements_harvest_defend.RESULT.md` when complete
