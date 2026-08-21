**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-29
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-29) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **UNRESOLVED NUMBER COLLISION — WO-586 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_586_fleet_save_probe_isolation.md`, `WORK_ORDER_586_battle_animation_posture_directional_death.md`
> The two tests **disagree**: `WORK_ORDER_586_fleet_save_probe_isolation.md` is first-on-disk (2026-06-29 00:20 vs 2026-07-05 15:25), but the *shipped* reference belongs to the other file — commit `38c7fd4b9` reads "WO-586: battle posture, directional death, orc cadence". First-on-disk-**and**-referenced is satisfied by neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WORK ORDER 586 — Fleet save-probe isolation (kill the concurrency false-positive)

**Status: READY TO IMPLEMENT** (small, test-harness only — no shipping code)
**Silo:** Combat/AI test-infra (gate-free, DevTools only)
**Priority:** Low (cosmetic to the fleet harvest; not a product bug)

## Problem
`AutoPilotDriver.AssertSaveRoundTrip` exercises the REAL save path via the
hardcoded `SaveSchema.PlayerPrefsKey` (`dotr-save`). On Windows, PlayerPrefs is a
per-company/product registry store, so **all N fleet instances share one save
store**. Concurrent instances stomp each other's `dotr-save` value, and a run
reading back another seed's probe (e.g. seed 7005 reads probe `…-7001`) flags a
false `ROSTER/QUEST drift`. This is pure test-isolation noise — single-instance
`RegressionSuite save-integrity` is green, and the atomic-write fix (`81015e80`)
already removed the related `[Flow:Save] HMAC mismatch`.

## Fix (pick one)
1. **Per-instance store (preferred):** give each fleet instance a distinct
   PlayerPrefs namespace (e.g. set `Application.productName` suffix by seed at
   boot in the AutoPilot bootstrap, or use a custom `ISaveProvider` that prefixes
   the slot with the run seed). Keeps every instance's save fully isolated.
2. **Serialize the phase:** only the seed-0 instance runs `AssertSaveRoundTrip`
   (cheap, but loses per-seed coverage of the save path).
3. **Probe a seed-namespaced key** via a test-only `ISaveProvider` swap that maps
   `dotr-save` → `dotr-save-<seed>` for the duration of the probe, then restores.

## Acceptance
- An 8-instance fleet run (`run-autopilot-fleet.ps1 -Count 8`) shows **0**
  `AssertSaveRoundTrip` drift/HMAC failures in `harvest.sh`.
- `RegressionSuite save-integrity` stays green.
- No change to shipping save code (`GameStateService`/`SaveSchema`/`LocalSaveProvider`).

## Do NOT touch
- The atomic save envelope (`81015e80`) — it's correct and verified.
- The real `SaveSchema.PlayerPrefsKey` for the actual game.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
