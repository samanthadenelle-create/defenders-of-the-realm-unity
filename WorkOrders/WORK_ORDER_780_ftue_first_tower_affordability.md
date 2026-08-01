# WO-780 — FTUE first-tower affordability (tutorial stall risk)

**Status:** SHIPPED 2026-07-27 (5dbe9574 — founding_defense grant.prepaidTower).
**Minted:** 2026-07-26 (CLI, from gameplay-gap ledger — P1 onboarding)
**Lane:** FTUE/tutorial (single lane). Dispatch on the clean committed base.

## Why (evidence)
The first-tower tutorial step grants no crystals and the `prepaidTower` path exists but **no step sets it** (`docs/qa/GAMEPLAY_GAPS_2026-07-26.md`). A new player is taught to place a tower they may not be able to afford → likely stall; today it only clears via a 120s watchdog (a bad first impression). The claim-loop should be taught first and the taught build must be affordable when taught.

## Scope
1. RCA the first-tower FTUE step (§12): find the tutorial step definition (`TutorialFlow` / the step config) and the `prepaidTower` flag + where placement affordability is checked. Confirm the actual failure (no grant vs grant-too-late vs prepaid-never-set).
2. Make the first taught build affordable AT the moment it's taught: either the step grants the required crystals, or it sets `prepaidTower` so the first placement is free/prepaid. Match whichever the existing FTUE affordance was designed for (prefer `prepaidTower` if that's the intended mechanism, so the economy stays honest after tutorial).
3. Ensure the step advances on the real placement (not only the 120s watchdog).

## Acceptance (data-verified)
- Oracle/EditMode: entering the first-tower step, the player can afford (or is prepaid for) the taught tower WITHOUT relying on the watchdog; the step completes on placement. Assert `prepaidTower` (or the crystal grant) is set by the step.
- Felt (owner): fresh founding → tutorial → first tower places smoothly, no stall, no 120s wait.

## Do NOT touch
- The enemy-spawn gate (already correct — no pre-defense spawn, memory `enemies-never-spawn-tutorial-onboarded-gate`).
- The broader tutorial back-half (scrapped/parked per canon).
- Post-tutorial economy balance (only the first taught build's affordance).
