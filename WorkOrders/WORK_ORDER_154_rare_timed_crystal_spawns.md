**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 154 — Rare One-Time Crystal Spawns (timed, region-gated treasure events)

**Status: READY TO IMPLEMENT**
**Date:** 2026-05-30
**Priority:** Medium-High — the "chase" layer of the crystal economy: rare, high-value, ephemeral. Owner ask: *"rare one-time crystals that random spawn for a duration in specific regions."*
**Lane:** gameplay / economy code (CLI). **NOT the frozen `VillageSceneBuilder`; no `Village.unity` hand-edit; no bake fired by UI.**
**Distinct from WO-153 (the Crystal Mine):** the mine is the *reliable, renewable, placed* faucet; **this is the opposite — random location, one-time, time-limited, rare grade.** A treasure-hunt beat layered on the same world.

---

## RECONCILE — build on, don't duplicate

| Need | State | Where |
|---|---|---|
| Crystal node model + `Extract()` → bank | **WO-141** | the rare spawn is a **one-shot, despawning** variant of the harvest node |
| Crystal **grades** (rarer in dangerous regions) | **WO-144** | rare spawns yield a **high/special grade** (or a dedicated "rare" grade) gated to specific regions |
| Region identity + `GetZone(Vector3)` | **WO-107 `ZoneManager`** | the "specific regions" these spawn in are classified here — reuse, don't redefine |
| Roaming-raid / event-tick layer | **WO-143** | if a world event ticker exists, the spawn scheduler can ride it; else self-contained timer |
| Crystal wallet | **`GameState.AetherCrystals`** | banked grade-aware (WO-144) on pickup |

---

## The feature

A **scheduler** periodically spawns a **rare crystal** at a random valid location **within specific regions**, that **persists only for a limited duration** then despawns if not collected. Catch it in time → big grade-aware crystal payout. Miss it → it's gone. The "chase."

### Behavior

1. **Spawn cadence:** on a randomized interval (e.g. every N–M minutes, tunable in an SO/constants), the scheduler rolls whether/where a rare crystal appears.
2. **Region-gated location:** spawns **only in the eligible regions** (designer-set — e.g. the higher-danger zones per WO-144's danger⇄reward spine). Uses `ZoneManager.GetZone` to validate the random point is in an allowed region + on valid ground (reuse WO-141's surface check).
3. **Time-limited:** the spawned crystal has a **lifetime** (e.g. 60–120s, tunable). A world cue (glow/VFX/beacon, code-built) marks it; optional HUD/compass ping so the player knows it's out there. On timeout → despawn, no payout.
4. **One-time pickup:** walk up / hold-to-extract (WO-141 prompt) → bank a **rare-grade** crystal amount (WO-144) → the crystal is consumed and despawns. Not renewable (that's the Mine, WO-153).
5. **Rarity controls:** spawn chance, eligible regions, grade, payout, lifetime all live in a `RareCrystalSpawnData` SO / `ProgressionConstants` — never hard-coded — so the designer tunes the chase difficulty.

### Distinct-from-Mine summary

| | Crystal Mine (WO-153) | Rare Spawn (WO-154) |
|---|---|---|
| Location | fixed/placed | random within regions |
| Lifetime | persistent | time-limited, despawns |
| Repeatable | renewable | one-time |
| Grade | region grade | rare/special grade |
| Feel | reliable faucet | treasure chase |

## Assembly / constraints (CLAUDE.md §5/§6)

- `RareCrystalSpawnData` SO → `DeNelle.Core.Data`; scheduler + spawned-node runtime → `DeNelle.Village` (or world module). Reuse WO-141's node `Extract`/bank path + WO-144 grades + WO-107 `ZoneManager`. Bank writes `GameState` directly. World cue + any HUD ping **code-built (no UXML)**. No new currency, no `System.Reflection`.

## Acceptance criteria

1. A scheduler spawns rare crystals on a **randomized interval**, **only in designer-eligible regions** (validated via `ZoneManager.GetZone` + valid-ground check).
2. Each spawn is **time-limited** — a visible world cue marks it; it **despawns on timeout** with no payout if uncollected.
3. Pickup is **one-time**: extract → bank a **rare-grade** crystal (WO-144) → crystal consumed. Not renewable.
4. All tuning (interval, chance, regions, lifetime, grade, payout) lives in an SO/constants — **no hard-coded values in logic**.
5. Built on WO-141 node model + WO-144 grades + WO-107 regions — no parallel systems.
6. No `VillageSceneBuilder` edit, no bake, no UXML, no new currency.
7. Brace balance; Village→Core only; `?.` on cross-module calls.

## Open questions for owner/designer
- **Eligible regions:** which specific regions can spawn rares? (Default: the higher-danger zones, matching WO-144's danger⇄reward — confirm.)
- **Grade:** reuse WO-144's top grade, or a dedicated "rare event" grade above it?
- **Discoverability:** silent (player must be roaming there), or a HUD/compass ping when one spawns? (Recommend a subtle ping so the chase is playable, not pure luck.)

## What NOT to touch
- Don't duplicate WO-141 node model, WO-144 grades, or WO-107 regions — reuse.
- Don't edit `VillageSceneBuilder.cs` / hand-edit `Village.unity` / fire a bake.
- Don't add a new currency; bank into `GameState.AetherCrystals` grade-aware.
- No UXML; world cue + HUD ping code-built.

## Done checklist (CLAUDE.md §10)
- [ ] Scheduler spawns region-gated, time-limited, one-time rare crystals; despawn-on-timeout verified
- [ ] Rare-grade payout banks grade-aware on pickup; tuning all in SO/constants
- [ ] Built on WO-141/144/107; no parallel systems; no bake/UXML/new currency
- [ ] Brace balance; Village→Core only
- [ ] `WORK_ORDER_154_rare_timed_crystal_spawns.RESULT.md` when complete

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
