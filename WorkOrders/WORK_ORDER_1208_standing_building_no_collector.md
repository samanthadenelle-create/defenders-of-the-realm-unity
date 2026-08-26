# WORK ORDER 1208 - A standing building with no collector silently withholds its income

**Status:** READY TO IMPLEMENT
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
3. **`farm` is mid-retirement** - it is the FOOD collector, and food is retired (WO-1163, PROD-016),
   so it may be a legacy structure whose replacement is the Quarry/Stoneyard line.

Candidate 3 changes the entire shape of the fix: if `farm` is being retired, the answer is a migration
for towns that already built one, NOT re-wiring a collector onto a structure that is about to be
removed. Read PROD-016 and WO-1163 sec.6 before deciding.

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
