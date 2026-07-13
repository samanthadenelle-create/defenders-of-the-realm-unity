# WORK ORDER 707 — Town catalog grooming: ONE building per trade (owner taxonomy ruling)

**Status: READY TO IMPLEMENT — two small owner pins open (below), implementable around them.**
**Lane:** Catalog/BuildMode/Economy data. **Type:** EXISTING (grooming two overlapping building
generations into the ruled set — no new systems).

## THE RULING (owner 2026-07-13, verbatim intent — CANON)

> "You have a Forge (weapon) — Upgrade, Stores Iron; an Armorer (armor) — Upgrade; Arcane Tower
> (magic) — Upgrade; Jeweler (rings); Food (food) — Upgrade, Stores Food; Lumbermill — Upgrade,
> Stores Wood."

One building per trade. Each is that trade's UPGRADE vendor; the resource trades also STORE
their resource (the store is the attackable stake — WO-702's "some roofs hold your stores"
lesson and WO-672's damage lifecycle hang off this).

**REFINED same session (owner, final form):** storage lives in THREE DEDICATED CONTAINER
buildings — the original storehouse-containers design restored. Trade buildings are pure
vendor/upgrade shops; the containers hold the stock with CoC-style visible fill. The earlier
"Forge stores iron / food stores food / Lumbermill stores wood" annotations are superseded.

| Building | Role | Function |
|---|---|---|
| **Forge** | Trade | weapon upgrades |
| **Armorer** | Trade | armor upgrades |
| **Arcane Tower** | Trade | magic upgrades |
| **Jeweler** | Trade | ring gear |
| **Farm** (RESOLVED — "farm is cleaner"; `mill` retires) | Trade/production | food income + upgrades |
| **Lumbermill** | Trade/production | wood income + upgrades |
| **Lumberyard** | **STORAGE** | **stores Wood** (visible pallet stacks fill) |
| **Foundry** | **STORAGE** | **stores Iron** (visible fill) |
| **Silo** | **STORAGE** | **stores Grain/food** (visible fill) |
| **Echo Hollow** | Pets | pet acquisition | — (stays — WO-702 first placement) |
| **Store** | Buy Packs | PackStore front (monetization — ~70% built, do NOT greenfield) | — |

*(Echo Hollow + Store confirmed by the owner in the same ruling: "Echo Hollow (Pets) and Store
(Buy Packs)". The `market` row's tile word becomes **Store**; it fronts the existing PackStore.)*

## What this retires / fixes (the current mess, from the 2026-07-13 palette audit)
- **`mine_crystal` OUT of the palette** (owner: "that's a node") — mining happens at world
  MineNodes; keep the catalog row only if the node system references it, else retire the row.
- **The `collector_*` band retires from the palette:** `collector_farm` / `collector_lumbermill`
  / `collector_forge` fold their ResourceCollector income + echo-worker behavior INTO the ruled
  buildings above (one building = storefront + vendor + income + storage). No duplicate tiles.
- **The id/name crossings straighten to match the ruling:** today id `workshop`="Forge",
  id `forge`="Armorer", id `armorer`="Blacksmith" — after grooming, the WORD on the tile is the
  trade per the table, "Blacksmith" retires (weapons=Forge, armor=Armorer). Prefer remapping
  displayNames + palette membership over renaming ids (ids are load-bearing: BaseLayout records,
  vendor AnchorRoles, talk-routes, WO-695 migration rows — a save with old ids must still replay).
- **THREE NEW catalog rows — the storage containers** (`lumberyard` wood · `foundry` iron ·
  `silo` grain): type Resource (Town tab), storage capacity as a catalog field (data-driven per
  the owner's lookup-table doctrine) wired to the resource caps if caps exist, else stubbed for
  the WO-672 damage-to-stores loop. **This design was discussed long ago and NEVER shipped
  (owner: "it never came out") — this WO is where it finally lands.** Portraits for the three
  new rows join the WO-706 art list. Trade buildings carry NO storage field.
  **Why separate (owner rationale, same session):** (a) it **isolates the shop from storage** —
  bounded contexts: raiding a container never breaks a vendor/talk-route, and shop logic never
  entangles stock logic; (b) **storage upgrades independently to hold more** — the containers
  get their own capacity tiers on the existing building-upgrade tech tree (WO-432 perk model /
  WO-675 panel), the classic CoC storage-upgrade loop, without touching shop upgrade paths.
  **(c) Enemy targeting: CONTAINERS ONLY (owner, same session):** the trade/shop buildings are
  NOT enemy targets — only Lumberyard/Foundry/Silo are attackable stores (wire into the
  `ff.enemystructureaware` targeting sweep as the container set, shops excluded). The threat
  lives exactly where the stock lives; a raid can never soft-lock a vendor/talk-route.
  **Design lineage (owner, 2026-07-13 — do not re-consolidate):** the owner ORIGINALLY designed
  per-resource storehouse containers; a prior implementation collapsed storage into one. This
  ruling restores the original intent in distributed form — each trade building IS the container
  for its resource. Distributed stores are load-bearing for gameplay (per-building raid stakes,
  the WO-702 placement lesson, WO-672 damage-to-stores); merging them back into one storehouse
  is a design regression, not a simplification.
  **Visual stores = PALLETS (owner design, recaptured 2026-07-13 — a prior conversation that
  was never written to canon; owner: "I loved the idea of visually seeing your store"):** each
  storing building shows its stock IN THE WORLD as pallet stacks beside it — wood stacked on
  pallets at the Lumbermill, iron at the Forge, food at the food building — growing/shrinking
  with the actual stored amount. The pallet stack IS the player-readable store: what you see is
  what a raid can burn. Colorblind-safe by construction (quantity reads by stack SIZE/count,
  never color). Implementation can start coarse (3–4 fill-level steps per resource) — split to
  its own visual WO if it doesn't fit this lane.
  **Reference = Clash of Clans storages (owner, 2026-07-13):** the gold/elixir storages whose
  visible fill level IS the stock readout — glanceable from town view, no UI needed to know
  what you hold and what a raider would want. That readability bar is the acceptance.

## Owner pins — ALL RESOLVED
1. **RESOLVED (owner, 2026-07-13): the food producer is the FARM** ("farm is cleaner") — the
   `mill` row retires from the palette; farm.jpg already exists; mill.jpg dropped from WO-706.
2. ~~Market~~ **RESOLVED (owner, same session): Market becomes "Store" (Buy Packs)** — fronts
   the existing PackStore (~70% built; PIPELINE_STATE: scene-wiring disabled pending its own
   PanelSettings — the tile/talk-route lands now, the store UI wiring stays its own lane).

## Destruction & persistence rulings (owner, 2026-07-13 — pass 2 scope)
- **Placement offsets save to the player** — each placement (cell + rotation) persists in the
  save's BaseLayout records (already the WO-673 system; this ruling confirms it as canon for
  the founding flow: what you lay out is YOUR town, forever).
- **V1 destruction model = CoC-STYLE (owner refinement, same day, supersedes the pay-to-rebuild
  line below for V1):** "the way we get there faster is exactly CoC style — one person deploying
  their troops and watching it play out." A raid NEVER deletes buildings: they take damage,
  may break for the battle, and **restore after the wave**; the REAL loss is **loot ripped from
  the containers** (ResourceCollector.RaidLootFraction 0.5 already implements the steal).
  Placement stays sacred; no rebuild flow needed for V1. The raid combat model is CoC's:
  **the attacker deploys troops and WATCHES it play out** — no direct control; layout vs
  deployment is the whole contest (feeds flip-a-base, WO-673; ArmyStorage/RaidDeploy exist).
  **Deployment freedom (owner, same breath): "they can deploy all different troops in
  whatever method they want"** — any troop MIX, any drop order/timing/location (within the
  legal deploy zone), no scripted sequence; the troop AI does the fighting, the player's
  choices are composition + where/when to drop.
- *(Parked post-V1 escalation:)* **Destroyed building = pay to rebuild.** The earlier same-day
  ruling — destruction deletes the row, full-cost fresh placement anywhere ("losing a
  badly-placed building is the invitation to place it better") — stays canon as a HARDER mode/
  later escalation, not V1 scope. The singleton gate needs no change either way (it reads
  BaseLayout rows; a deleted row frees the slot automatically). Repair (damaged-but-standing)
  stays the REP-1 paid-repair path in both models. Wire with WO-672 in pass 2.

## Acceptance
- [ ] Town tab shows exactly: Echo Hollow, Store, Forge, Armorer, Arcane Tower, Jeweler, the
      food producer, Lumbermill, **Lumberyard, Foundry, Silo**. No Crystal Mine, no collector
      duplicates, no two tiles sharing a word.
- [ ] The three containers show CoC-style visible fill (pallet stacks / fill steps) that tracks
      actual stored amounts; coarse 3–4 steps acceptable for the first pass.
- [ ] Every surviving building: vendor anchors + talk-routes still resolve (fleet vendor probe
      green); echo-worker gather assignment targets the merged buildings (ECHO-1/WO-681 flow).
- [ ] Migrated + fresh saves both replay/place correctly (BaseLayout ids unchanged;
      StrategicPlacementRegression + BlankStartCensusRegression green).
- [ ] Storage fields present on Forge (iron) / food building (food) / Lumbermill (wood).
- [ ] Portraits resolve for every surviving tile (coordinates with WO-706's art set).
- [ ] COMPILE_GATE_OK + DataRegression baseline + owner felt-pass of the palette (PO closes).

## What NOT to touch
Catalog IDs on disk-persisted rows (display/membership changes only) · the WO-695 migration
marker + BakedRows semantics · Defenses/Walls tabs (separate audit) · vendor dialogue content.

*Cross-refs:* owner palette audit 2026-07-13 · WO-706 (portraits) · WO-702 (stores = stakes
lesson) · WO-672 (damage lifecycle) · WO-587/WO-681 (echo workforce) · WO-695 (migration ids).
