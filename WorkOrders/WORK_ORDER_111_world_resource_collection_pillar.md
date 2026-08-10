# WORK ORDER 111 — World Resource Collection Pillar (collection points → build mines → auto-harvest)

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: pillar implemented as Echo harvest + nodes + stockpiles, canon §7/§8)
**Priority:** Pillar (big) — the idle/economy backbone. Build incrementally; WO-110 is Phase 1.
**Lanes:** design (owner + UI) · gameplay code (CLI) · world placement (`VillageSceneBuilder` / world scenes)
**Roadmap:** the build-out of [[resource-idle-economy-roadmap]] (resource gathering + pet auto-harvest + offline → upgrades).

---

## Vision (owner)

> "Collection points throughout the world — you'd **build mines** at them and they **auto-harvest**."

**Inspiration:** Warcraft — **gold mine + crystal** as the resource nodes you claim and work.

End state: the world is seeded with **collection points** (resource nodes). The player **builds a
mine** on a node; the mine **auto-harvests** its resource over time; harvest **accrues while
offline** (up to a cap); the player **spends** the haul on tower/structure upgrades (later forge/enchant).
A **pet** can be assigned to auto-harvest / boost a node.

**The tension (owner) — defend or lose it:** a mine is **destructible**. Build **defenses**
(towers/walls) around it and you mine safely; leave it exposed and **roaming world enemies will
attack and destroy it** if you're not vigilant. A destroyed mine stops harvesting and must be
rebuilt. This is the synergy that fuses the idle-harvest with the existing tower-defense core —
the harvest is *not* pure idle; it's "claim a node, fortify it, keep it alive."

---

## Reconciliation — what already exists (build-up, not rebuild)

| Need | Exists? | Where |
|---|---|---|
| Per-node mine (yield + L1→L3 upgrade) | **BUILT** (passive, per-wave) | `CrystalMine.cs` — generalize it |
| Spin/pulse node visual | **BUILT** | `CrystalVisual.cs` |
| Resource wallet | **BUILT** | `GameState` — `AetherCrystals`, `Stone`, `Iron`, `Wood`, `Resources.{Food,Coins,Crystals}` |
| Award path (Core can't ref Village) | **BUILT** | write `GameState.AetherCrystals` directly ([[core-cannot-reference-village-award-crystals-via-gamestate]]) |
| Build-a-structure flow | **BUILT** | `BuildMenu` / `Building` plot system |
| Pets | **BUILT** | `Pet.cs` — add an auto-harvest behaviour |
| Offline progression | spec'd | offline-economy roadmap / save-sync |

**So the new work is the SYSTEM around these, not the pieces.**

---

## Phases

**Phase 1 — single node (WO-110, in progress).** Crystal mine: node mesh + `CrystalVisual` +
`CrystalMine` passive yield. Ships now.

**Phase 2 — collection-point data + build flow.**
- A `CollectionPoint` descriptor: world position, **resource type** (crystal/stone/iron/wood/…),
  **richness** (base yield/min), built-or-empty state.
- Seed N points across the world (a data array in the world builder, like `Buildings[]`).
- An empty point shows a "build mine here" affordance; building one (cost via `BuildMenu`/economy)
  places the mine + starts harvest. Generalize `CrystalMine` → `ResourceMine` (resource-type param).

**Phase 3 — auto-harvest over time (the idle core).**
- Mine accrues `richness × level × time` into the matching `GameState` resource on a timer
  (not per-wave). Cap per mine; visible "X banked / Y cap" readout (ties to the dev portal + HUD).

**Phase 4 — pet auto-harvest + boost.**
- Assign a `Pet` to a node → faster harvest / higher cap / collects while you fight elsewhere.

**Phase 5 — offline accrual.**
- On load, compute elapsed time since last save, grant `min(rate × elapsed, cap)` per mine →
  "Welcome back — your mines gathered N." Reuses the save-sync timestamp.

**Phase 6 — sinks.**
- Harvest spends into tower/structure upgrades (then forge/enchant) — closes the loop.

---

## Open design questions (owner)
- Resource types beyond crystal? (Stone/Iron/Wood already in `GameState` — map nodes to them.)
- Is the "world" the village arena, a separate overworld map, or both? (Affects where points seed —
  and note **WO-104 castle will reshape the village**, so don't hard-place village nodes until it lands.)
- Harvest cadence + offline cap values (feel/tuning).
- Build cost curve + which currency.

## Lane / discipline
- Gameplay code (CLI) through the brace/compile gate; world placement via the builder (never hand-edit scenes).
- Reconcile, don't duplicate — generalize `CrystalMine`, reuse `BuildMenu`/`Pet`/`GameState`.

🤖 Vision captured by the build-connected CLI; reconciled against existing economy/build/pet systems.
