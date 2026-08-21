<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-30
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-30) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-809 — War Readiness / army power score (Raids screen)

> ⚠ 2026-08-01: build ATOP `Assets/_Modules/Village/Troops/ArmyReadiness.cs` (WO-823 phase A) — the single readiness truth. This WO is a presentation layer over ArmyReadiness.Compute, NEVER a second readiness engine.

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2A  
**Lane:** Raid selection / Army presentation  
**Prefer after:** WO-806/807 for troop L inputs; WO-808 optional for hero gear component  

## Why
CoC always answers “can I take this base?” with army strength feel. We have housing counts and troop types but **no single readiness number** on Raids / Army screens — players guess.

## Scope
1. Pure `WarReadiness.Compute(...)` (no scene):
   - Inputs: deployable troops (by type × count), each type’s `TroopLevel`, housing used/cap, optional hero gear contribution if 808 exists  
   - Output: integer score + band label (e.g. Muster / War Band / Host) — ASCII  
2. Show score on **RaidSelection** and **Army (pre-raid)** screens.  
3. Optional: grey advice “Raise Footman L or train more housing” from lowest lever (heuristic).  
4. EditMode tests: empty army = 0; more troops / higher L increases score monotonically.  
5. Do **not** gate raid entry on score for V1 (display only) unless owner later rules hard gate.  

## Acceptance
- [ ] Score visible before March  
- [ ] Training or troop L up changes score after refresh  
- [ ] Pure function tested  
- [ ] Felt: “I know if I’m underpowered”  

## Do NOT
- Hidden MMR / matchmaking  
- Async PvP  
- Block raids by score in V1  

## Files
- New pure helper under Village or Core, RaidSelectionVM, RaidDeployVM  

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `RaidDeployVM.cs:139,338; RaidDeployVMTests.cs:75` — power rating shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
