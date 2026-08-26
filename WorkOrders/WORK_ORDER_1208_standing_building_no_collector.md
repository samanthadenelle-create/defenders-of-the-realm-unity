# WORK ORDER 1208 - A standing building with no collector silently withholds its income

**Status:** IMPLEMENTED 2026-08-25 - awaiting owner device verification (a dungeon round trip: leave town, return, confirm the Quarry still pays). Root cause was NOT a missing component: the fallback wiring ran synchronously at `sceneLoaded`, either before `GameState` was ready (a null state correctly reads as "nothing built") or while the OUTGOING placed collector was still registered, with no state-ready or post-teardown retry. The DDOL host now owns a retry driver bound to `GameStateService.StateReplaced` plus a next-frame reconciliation per scene transition, and the formerly SILENT missing-host return warns. ⭐ Single owner per id: a real placed collector outranks the DDOL fallback, which is PARKED rather than deactivated so its stale snapshot cannot overwrite the placed collector's PlayerPrefs. ⚠ Two dev self-reviews rejected earlier versions - an unconditional OnDisable retry would have RESURRECTED a sold Quarry from the monotonic ledger, and reset could retain a live fallback that paid a new town from a dead save. Gates: `COMPILE_GATE_OK` + `REGRESSION_OK 284/284`. RESULT filed. *(Prior line:)* **Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1207 -> 1209 with WO-1207 in the same edit)
**Silo:** Economy / Village buildings

---

## Proving evidence - the owner's device, 2026-08-25 19:22:31

```
[Flow:Harvest] 'farm' is in the ever-built ledger but NO ResourceCollector is registered -
               13 Food WITHHELD this tick (a standing building with no collector component is a
               wiring bug; income is never granted straight to the wallet).
```

Source: `Assets/_Modules/Village/Buildings/Progression/ResourceBuildingHarvester.cs:186-188`.

The building stands in her town and pays **nothing**, every tick, silently from the player's side.

**The refusal itself is CORRECT and stays.** Granting income straight to the wallet with no collector
would bypass the collector/cap path entirely. The instrumentation named its own wiring bug - exactly
what CLAUDE.md sec.12 exists to buy.

## Find out WHY before writing code

The trace proves the SYMPTOM (no `ResourceCollector` registered for `farm`), not the cause. Three
candidates:

1. the prefab lost its `ResourceCollector` component;
2. it is present but never registers (a lifecycle/order problem);
3. ~~`farm` is mid-retirement~~ - **REFUTED the same evening, and the refutation makes this ticket
   MORE urgent, not less.** `structures-catalog.json` id `collector_farm` already reads
   **displayName "Quarry", description "Extracts Stone for your town over time.", role
   `stone_producer`**, and its `collectorBuildingId` is **`farm`** - the id in the trace. So the
   building withholding income is **not a legacy food building awaiting removal: it is the QUARRY,
   the NEW stone producer WO-1163 just shipped.** The word "Food" in the trace is the internal enum
   name (`def.Yields`) printing the frozen save slot, not a player-facing string.

⛔ **Re-rank accordingly.** This is not retirement housekeeping - a player who builds the town's
primary Stone faucet gets **nothing per tick** from it, on a build that takes real money. Candidates
1 and 2 (missing component / never registers) are the live ones; check whether the rename from Farm
to Quarry moved a prefab or a collector wiring reference with it.

## Acceptance criteria

- Root cause named with a captured line, not inferred (sec.12).
- Any standing structure whose catalog row promises a yield either COLLECTS or is HONESTLY RETIRED
  with a migration. There is no third state.
- A registered oracle that fails when a catalog row promising a yield has no reachable collector path,
  so the next one announces itself before the owner meets it.
- Gates judged by marker on fresh logs.

## What NOT to touch

- The withhold behaviour. Do not "fix" this by paying the wallet directly - that is the bypass the
  guard exists to prevent.
- PROD-016's Echo/node conversion. If the answer is retirement, that work belongs to the ticket that
  already owns the food surfaces.
