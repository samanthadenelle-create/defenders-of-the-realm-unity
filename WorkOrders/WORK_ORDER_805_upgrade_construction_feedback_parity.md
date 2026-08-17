<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-30
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-30) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-805 — Upgrade / construction feedback parity (world + HUD + complete)

**Status:** READY TO IMPLEMENT  
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2  
**Lane:** Build presentation (disjoint from raid; can parallel 774)  
**Synergy:** WO-800 building card Queue tab; WO-801 glance row for same job  

## Why
WC3 and CoC both sell trust with **three tells** for a timed job: (1) world scaffolding, (2) HUD/queue progress, (3) completion pop. Missing any one makes timers feel broken. Code has `UnderConstructionVisual`, queue jobs, and toasts — parity is uneven across structure kinds.

## Scope
1. **Audit** all paths that start timed build/upgrade/repair (BuildMode, BuildingUpgradeService, towers, walls, resource buildings).  
2. For each path assert:
   - World visual while in flight (`UnderConstructionVisual` or equivalent)  
   - Job appears on Builder channel (and glance when 801 lands)  
   - Completion: visual clear + SFX + optional toast  
3. Fix any path that applies level instantly while `ff.buildtimers` ON, or finishes timer with no world clear.  
4. FlowTrace.Once per structure family on start/complete.  
5. Lightweight regression or checklist oracle: “timer job ⇒ structureId in active/pending.”  

## Acceptance
- [ ] Starting an upgrade always shows world construction tell when timers ON  
- [ ] Completing always removes tell and leaves correct tier  
- [ ] No silent finish  
- [ ] Felt: “I trust the timer”  

## Do NOT
- Change duration formulas / economy costs  
- Raid  
- Greenfield new construction system — fix parity on existing  

## Files
- `UnderConstructionVisual`, `BuildTimerService.CompleteJob`, `CompletedUpgradeApplier`, BuildMode upgrade commit path  
