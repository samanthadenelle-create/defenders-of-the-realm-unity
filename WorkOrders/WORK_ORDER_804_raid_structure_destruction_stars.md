<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-30
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-30) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-804 — Raid structure-destruction % for CoC-style stars (later)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
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

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `RaidScoring.cs:206,244,344` — destruction in stars. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
