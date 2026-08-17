<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 194 — Build Experience: Placement Feedback, Per-Type Timers, Construction State, Vulnerable Target

**Status:** READY TO IMPLEMENT (phased)
**Lane:** Build / Buildings — `Village/Buildings` + `Core/Catalog` + DB. Code + data; no village bake required.
**Source:** owner 2026-06-01 — build mode shows nothing; placement feedback confusing; want per-type timers,
a construction visual, and under-construction = priority target.
**Existing systems to build ON (do NOT greenfield):** `BuildMenu.cs`, `PlacementRules.cs`, `BuildTimerService.cs`
+ `BuildTimerConfig.cs` + `BuildJobData.cs` (WO-172 timers, DONE), `TowerData.cs` / `CatalogEntry.cs` /
`CatalogRegistry.cs`, `Building.cs` (HP/damage), `buildings.json`.

## Part 1 — Build menu shows NOTHING when selecting
**Bug:** opening build/select shows no placeable defensive structures. Investigate `BuildMenu.cs` — likely the
catalog→menu wiring isn't populating, or the defensive structures aren't registered in `CatalogRegistry`.
**Acceptance:** selecting Build shows the list of placeable defensive structures (icon + name + cost + build
time), populated from the catalog. Empty menu = FAIL.

## Part 2 — Clear "you can build here" feedback (best practice)
**Problem:** a "disk" currently signals not-OK; from the player's view it's confusing.
**Best practice (CoC / TD standard) — replace the disk with a placement GHOST:**
- Show a **translucent ghost of the actual building** that follows the cursor/finger, **snapped to the build grid**.
- **GREEN ghost = placeable, RED ghost = blocked** (tint the whole footprint, not a separate disk). Show the
  building's **footprint outline** so the player sees exactly the space it needs.
- On blocked, show a **short reason** ("Too close to a gate", "Overlaps a building", "Outside your plot") — sourced
  from `PlacementRules.cs` (which already computes validity; surface its reason).
- Confirm/cancel affordance. This makes "OK to build here" obvious without a cryptic disk.
**Acceptance:** the ghost is green where valid / red where not, with a footprint and a reason on invalid; no bare disk.

## Part 3 — Per-type build timers in the DB + defensive catalog
- Add a **`buildSeconds`** (build duration) field to each defensive structure's catalog entry (`CatalogEntry`/
  `TowerData`/`buildings.json`) — **per type** (bigger/stronger = longer). Bigger siege/wall tiers take longer.
- **Persist per build job in the DB** — `BuildJobData` + the WO-172 timer system already persist start/finish;
  drive the duration from the catalog `buildSeconds`, and ensure it's saved (schema already at v13 from WO-172).
- The ad-skip seam (WO-172) applies to these timers.
**Acceptance:** each defensive type has its own authored build time; a placed build runs that timer; it persists
across save/reload; the catalog/DB is the single source of the duration.

## Part 4 — Under-construction visual while the timer runs
- While a build job is active, show a **construction graphic** instead of the finished building — scaffolding /
  a fenced foundation / a partial mesh, with a **progress indicator** (radial timer or bar showing time left).
- On completion, swap to the finished building (a small "done" pop). CoC-style.
**Acceptance:** a building under construction visibly reads as under-construction (scaffolding + progress), not as
the finished structure; swaps to final on timer complete.

## Part 5 — Under-construction = FRAGILE PRIORITY TARGET — DECIDED (Warcraft model, owner 2026-06-01)
"Player's building a new attack tower → enemies hurry to kill it fast — wastes the resources AND destroys it
before it has HP." So:
- While under construction the structure is **FRAGILE + DESTRUCTIBLE**: it has only a **small construction HP pool**
  (the scaffold), NOT the finished tower's HP. It gains its **full defensive HP only on completion.**
- It is flagged a **HIGH-PRIORITY enemy target** — the WO-145 target-scorer weights under-construction structures
  **ABOVE finished buildings**, so attackers rush to destroy them before they come online.
- **Destroyed mid-build = FULL LOSS:** the build job is destroyed, the **resources spent are WASTED** (no refund,
  no timer-reset — they're gone). The tower never comes online. This is the whole point — the player must DEFEND
  the build, and a successful enemy rush punishes over-extension.
- Wire via `Building.cs` HP + an `UnderConstruction` flag the enemy AI target-scorer (WO-145) + the damage system read;
  on completion, set HP to the finished tower's max and drop the priority flag.

## Open decisions (owner)
1. ~~Part 5 model~~ — **DECIDED: fragile + destructible + priority target; destroyed mid-build = resources wasted (full loss).** (Warcraft.)
2. **Construction HP** — how fragile? (e.g. scaffold HP ≈ 20–30% of the finished tower, or a flat low value.) Tune.
3. Grid snap size + whether walls/ramparts use a different placement mode than towers.
