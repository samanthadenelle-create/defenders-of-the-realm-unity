# WO-804 — Raid structure-destruction % for CoC-style stars (later)

**Status:** READY TO IMPLEMENT — **LATER**; needs owner go (R3 in program doc)  
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2  
**Lane:** Raid scoring  
**Prefer after:** WO-802 + WO-774 copy settled on “Defenders %”  

## Why
True CoC stars weight **building destruction**, not only garrison kills. V1 scoring is garrison/boss/clock-based (`RaidScoring`) — correct for ship, but star *language* will stay “defenders” until this lands. This WO adds optional **structure HP destruction %** into star math and HUD.

## Depends on
- Owner ruling R3: invest structure-% now vs stay garrison-only for a release  
- `IDamageableStructure` on raid base buildings/towers/walls  
- `RaidScoring.ComputeStars` pure function + HUD  

## Scope
1. Track peak and remaining HP (or destroyed count) of **player-hostile structures** on the raid base.  
2. Expose `StructureDestruction01` on scorer.  
3. Star formula (owner-tunable): e.g. 1★ 50% structures OR 50% defenders; 2★ 50%+; 3★ 100% or TH/Heart + clock — document final table in RESULT.  
4. HUD + summary: show **Defenders %** and **Structures %** both (colorblind-safe labels).  
5. Oracle: pure ComputeStars cases with structure input.  

## Acceptance
- [ ] Destroying towers/walls moves structure % without killing all mobs  
- [ ] Stars can be earned via structure path per signed table  
- [ ] Copy never claims “base destroyed” unless 100% structures  
- [ ] Regression green  

## Do NOT
- Rebuild combat  
- Async PvP  
- Change deploy ring  
- Ship without owner R3 go  

## Note
If R3 = “stay garrison-only for V1,” mark this WO **DEFERRED** and leave READY file as backlog.
