# WORK ORDER 936 — Catalog gating + progression truth pass

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-09 (CLI seat) — number from the `CLI_LANES_WO_NUMBERS.md` banner (bumped 936 → 937 in the same edit)
**Lane:** Catalog / progression data. **Presentation untouched** — this is about what the data PROMISES vs what the code DELIVERS.
**Provenance:** Owner questions during the WO-1010 art pass, 2026-08-09 — *"where do players upgrade wood?"* and
*"Jeweler is gated just till it's unlocked correct?"*. Both turned out to expose gaps, not answer questions.

---

## 0. Why this exists

Three separate findings, one shared shape: **the data advertises a progression the code cannot deliver.** None
is visible to any gate — every one passes compile, passes 132 suites, and reads correct to anyone skimming the
JSON. They only surface when someone asks "so how does a player actually do this?"

---

## 1. FINDING A — `LockedIds` has no unlock path (jeweler is permanently hidden)

`BuildCategoryRegistry.LockedIds` is built once from `build-categories.json` and thereafter only READ.
`BuildPaletteVM.Rebuild` does `if (_lockedIds.Contains(e.id)) continue;` — **unconditional**. Nothing anywhere
mutates the set, and no unlock/progression system feeds it.

The code comment distinguishes intent clearly:
- *"Jeweler stays **unlock-gated**"* → meant to be TEMPORARY
- *"the palette **retires** mine_crystal / mill / lumbermill / armorer / collector_forge"* → meant to be PERMANENT

Both are implemented identically, so the temporary one is permanent in practice. The Jeweler can never appear
regardless of what the player does.

`StrategicPlacementRegression:194` asserts `town.LockedIds.Contains("jeweler")`, and :617 already carries an
`unlockGateExempt` notion — so the current state is deliberately pinned, and un-gating means updating that suite
too. That is a feature: it cannot flip silently.

**Deliverable:** either (a) implement the unlock path — a gated id becomes available when its condition is met
(perk owned / tier reached / quest complete) — or (b) rename the concept so the data stops implying a door that
does not exist (e.g. `RetiredIds` vs `UnlockGatedIds`, with only the latter consulted against player state).
**Do not silently un-gate the jeweler**: it is a real building with a live vendor surface
(`PanelId.JewelerCrafting`, `jewelers-bench`), so making it placeable is a design change, not a bug fix.

---

## 2. FINDING B — the three stockpiles cap the economy with no way up

`lumberyard` (wood), `foundry` (iron) and `silo` (food) each declare `storageCapacity` + `maxLevel: 3` in
`structures-catalog.json`. But `building-tiers.json` lists tiers for only: `arcane-tower, armorer, barracks,
forge, lumbermill, farm`. **None of the three stockpiles has a single tier row.**

So each advertises three levels while nothing defines what level 2 or 3 grants, or what it costs. Per WO-837 the
stockpiles ARE the capacity-cap progression and are the only enemy raid targets — so a wood cap frozen at 500
with no upgrade path is a hard ceiling on the whole wood economy, and the same for iron and food.

**Deliverable:** author tier rows for `lumberyard` / `foundry` / `silo` (capacity per tier + cost + any HP bonus),
OR drop `maxLevel` from the catalog rows so the data stops promising levels that do not exist. **Numbers are an
owner call — bring options, do not invent balance.**

---

## 3. FINDING C — the live Lumber Mill upgrades through a retired row

The placeable building is `collector_lumbermill`; its `collectorBuildingId` points at `lumbermill`, which is the
row that actually owns the tier tree (*"Restore the Mill"*: wood production +10%, structure HP +20%, 900 wood +
300 food). `lumbermill` is in Town's `LockedIds` — retired from the palette.

This WORKS today. The risk is that the live building's entire progression is defined on a row the palette hides,
so a future "clean up the retired rows" pass silently deletes the Lumber Mill's upgrades. It is a live dependency
that looks like dead data.

**Deliverable:** make the indirection explicit and guarded — a regression asserting that every
`collectorBuildingId` target still exists and still owns tier rows, so deleting a retired row that is load-bearing
fails the gate instead of the game.

---

## 4. Cross-cutting: display names are not identities

Every one of the above cost real time today because labels were read as identities. In this catalog:

| display name | ids |
|---|---|
| "Lumber Mill" | `lumbermill` (retired) **and** `collector_lumbermill` (live) |
| "Armorer" | id `forge` |
| "Forge" | id `workshop` (now displays "Weaponsmith") |
| "Blacksmith" | id `armorer` (retired) |

The author of this WO mis-stated the live/retired status of Lumber Mill and Jeweler on this basis, twice, in one
session. **Any claim about what is live must come from the id + a `LockedIds` check, never from the name.**

**Deliverable (cheap, high leverage):** a doc block or oracle that publishes the id ⇄ displayName map, so the next
reader does not re-derive it wrongly.

---

## 5. Acceptance criteria

- [ ] `LockedIds` either gains a real unlock path, or the concept is split so permanently-retired and
      temporarily-gated ids are no longer indistinguishable in the data.
- [ ] `lumberyard` / `foundry` / `silo` either have authored tier rows, or no longer claim `maxLevel: 3`.
- [ ] A regression asserts every `collectorBuildingId` target exists AND owns tier rows.
- [ ] The id ⇄ displayName collisions are documented where a reader will hit them.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`.
- [ ] Any balance numbers are OWNER-RATIFIED, not authored by the implementing seat.

## 6. What NOT to touch

- Build-screen presentation (WO-1010 owns it).
- The retired ids' catalog rows — saves replay them (WO-707); retiring from the palette is not deleting.
- `StrategicPlacementRegression`'s existing jeweler assertion, except as a deliberate part of an un-gating change.
