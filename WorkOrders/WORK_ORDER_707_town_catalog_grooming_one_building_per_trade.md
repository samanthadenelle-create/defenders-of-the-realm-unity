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

| Building | Trade | Vendor/Upgrade | Stores |
|---|---|---|---|
| **Forge** | Weapons | weapon upgrades | **Iron** |
| **Armorer** | Armor | armor upgrades | — |
| **Arcane Tower** | Magic | magic upgrades | — |
| **Jeweler** | Rings | ring gear | — |
| **Food building** (pin #1: Farm or Mill) | Food | food/econ upgrades | **Food** |
| **Lumbermill** | Wood | wood/econ upgrades | **Wood** |
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
- **Storage capacity** becomes a catalog field on Forge/Food/Lumbermill rows (data-driven per the
  owner's lookup-table doctrine) wired to the existing resource caps if caps exist, else stubbed
  for the WO-672 damage-to-stores loop.
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

## Owner pins (answer before/while implementing)
1. The food building's NAME: **Farm** or **Mill**? (farm.jpg art exists; "Farm" is
   ten-year-old-clear — recommended. mill.jpg is in WO-706 either way until ruled.)
2. ~~Market~~ **RESOLVED (owner, same session): Market becomes "Store" (Buy Packs)** — fronts
   the existing PackStore (~70% built; PIPELINE_STATE: scene-wiring disabled pending its own
   PanelSettings — the tile/talk-route lands now, the store UI wiring stays its own lane).

## Acceptance
- [ ] Town tab shows exactly: Echo Hollow, Forge, Armorer, Arcane Tower, Jeweler, the food
      building, Lumbermill (+ Market per pin #2). No Crystal Mine, no collector duplicates,
      no two tiles sharing a word.
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
