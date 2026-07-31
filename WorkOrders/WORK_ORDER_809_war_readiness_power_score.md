# WO-809 — War Readiness / army power score (Raids screen)

**Status:** READY TO IMPLEMENT  
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
