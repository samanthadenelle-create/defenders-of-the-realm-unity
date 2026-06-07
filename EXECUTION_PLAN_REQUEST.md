# Execution Plan Request for Architect

**Date:** 2026-06-01  
**Context:** Complete game restructuring + new ATB system + combat polish  
**Stakeholder:** Samantha (samanthadenelle@gmail.com)  
**Current State:** All WOs documented, ATB blocker identified, ready for sequencing

---

## Problem Statement

We have 20+ work orders spanning:
- Critical blockers (WebGL, build mode, ATB battle)
- Foundation restructuring (project org, zone architecture)
- Core gameplay (economy, combat feel, narrative)
- Visual polish (cameras, gates, UI)

**Need:** Optimal execution sequence that:
1. Unblocks web deployment ASAP
2. Fixes ATB battle system (currently non-functional)
3. Restructures project foundation before adding major features
4. Maximizes parallel work where possible
5. Prioritizes highest UX impact (combat feel)

---

## All Work Orders (Ready State)

### Phase 0: Critical Blockers (Web Deployment)
- **WO-196**: WebGL Brotli fix (DONE locally, needs re-upload)
- **WO-211**: WebGL optimize (remove 92MB cosmetics + 19MB source FBX)
- **WO-215**: Build mode click-to-place wiring

### Phase 1: ATB Battle System
- **WO-233**: FF-style ATB system (bars + turn queue + hero actions + AI)
- **ATB-DEBUG**: Diagnostic guide for identifying failures (complete)

### Phase 2: Foundation Restructuring
- **WO-232**: Project restructuring (folder org, GameManager hub, 14-15 hr)
- **WO-231**: Zone architecture redesign (exterior world, enemy march, 8-10 hr)

### Phase 3A: Economy System
- **WO-228**: Pet harvesting + resource nodes (code ready, 2.5-3.5 hr)
- **WO-229**: Harvest visual feedback + HUD (code ready, 2-3 hr)

### Phase 3B: Combat Feel (Highest UX Impact)
- **WO-213**: Troop downscale + UI pill swap (35-50 min)
- **WO-217**: Animation polish (anticipation/impact/recovery, 2-3 hr)
- **WO-218**: Animation layering (attack while moving, 1-1.5 hr)
- **WO-219**: Visual feedback (hit-stop, particles, trails, damage numbers, 2-3 hr)
- **WO-220**: Audio feedback (future, 1.5-2 hr, post-launch)

### Phase 3C: Narrative + Hero Select
- **WO-230**: Hero Select UI — 4 character cards (consolidated, 1.5-2 hr)
- **WO-222**: Tutorial redesign (1.5-2 hr, depends on WO-215)
- **WO-227**: Opening cutscene + story companion (3-4 hr, depends on WO-222 + WO-230)

### Phase 3D: Visual Polish
- **WO-212**: Gate z-fighting fix (15 min)
- **WO-214**: Dual-camera (village overhead / overworld over-shoulder, 30-40 min)
- **WO-221**: Defend Tower camera closer (10-15 min)

### Phase 4: Content Loop
- **WO-216**: Enemy camps (3 types, 4-6 hr, depends on WO-215)

---

## Known Dependencies

```
WO-196 (Brotli) 
  ↓
WO-211 (WebGL optimize)
  ↓
WO-215 (Build mode) ← BLOCKS WO-222, WO-216

WO-232 (Restructuring) ← Foundation for all future work

WO-231 (Zone architecture) ← Uses restructured folder layout from WO-232

WO-228 (Economy) → WO-229 (Visual feedback)

WO-217 (Animation polish) → WO-218 (Layering) → WO-219 (VFX)

WO-222 (Tutorial) ← Needs WO-215 (build mode working)
WO-222 → WO-227 (Opening cutscene)

WO-230 (Hero cards) ← Independent, can run anytime

WO-216 (Camps) ← Needs WO-215 (build mode working)
```

---

## Time Estimates (Total ~80 hours)

| Phase | Work Orders | Estimated Time | Parallel? |
|---|---|---|---|
| Phase 0 | WO-196/211/215 | 2 hr | Sequential |
| Phase 1 | WO-233 + debug | 3-4 hr | Sequential with Phase 0 |
| Phase 2 | WO-232 + WO-231 | 22-25 hr | Sequential (WO-232 first) |
| Phase 3A | WO-228 + WO-229 | 5-6.5 hr | Sequential, parallel to Phase 3B |
| Phase 3B | WO-213/217/218/219 | 5-8 hr | Sequential, parallel to Phase 3A |
| Phase 3C | WO-230/222/227 | 6-8 hr | Mixed parallel, depends on WO-215 |
| Phase 3D | WO-212/214/221 | 1-2 hr | Full parallel |
| Phase 4 | WO-216 | 4-6 hr | After WO-215 |

---

## Questions for Architect

1. **Execution Order:** What's the optimal sequence given dependencies and time constraints?

2. **Parallelization:** Which work can run in parallel without resource contention?
   - (e.g., can WO-228/229 run while WO-232 is happening?)

3. **Critical Path:** Which work is on the critical path (blocks everything else)?
   - Is it WO-232 (restructuring) or WO-215 (build mode)?

4. **ATB Blocker:** Where does WO-233 (ATB fix) fit in execution order?
   - Should it be done immediately after WO-196/211/215 (Phase 0)?
   - Or can it wait until WO-232 is complete?

5. **Risk Mitigation:** Are there any WOs that should be bumped up due to risk?
   - (e.g., does the project restructuring need more time before other work starts?)

6. **Testing Windows:** When should we pause for local testing?
   - After Phase 0 (web deployment)?
   - After Phase 2 (restructuring)?
   - After Phase 3B (combat feel)?

7. **Delivery Milestones:** What's the minimum viable set of work to:
   - Get game playable on itch.io?
   - Get combat feeling good?
   - Get a full game loop (spawn → fight → win)?

---

## Current Blockers

**Hard blockers (can't start other work):**
- ATB battle is non-functional (WO-233 + ATB_DEBUGGING_GUIDE.md)
- Web build won't load (WO-196 Brotli)
- Build mode wiring missing (WO-215)

**Soft blockers (can work around):**
- Project structure chaotic (WO-232) — future work will go to wrong folders
- Zone architecture stubby (WO-231) — battles feel cramped

---

## What's Ready

✅ **Complete specification:**
- WO-233 (FF ATB) — 2 complete scripts + integration steps
- WO-232 (Restructuring) — 10-phase plan + target folder structure
- WO-231 (Zones) — ExteriorTerrainBuilder complete + spawn logic
- ARCHITECTURE_REFERENCE.md — GameManager + scene flow + ScriptableObjects
- WO-QUEUE_CONSOLIDATED — dependency-ordered list (already done)
- ATB_DEBUGGING_GUIDE — step-by-step diagnostic for failures

✅ **Code already written:**
- WO-228/229 (Economy) — 7 .cs files in Assets/_Modules/Economy/
- ATBUnit.cs + Improved BattleController.cs (WO-233)

✅ **Art & design approved:**
- 4 hero cards (Sylas/Grom/Thrain/Elara) — ready for WO-230 UI
- Zone constants (spawn distance, road length) — WO-231 ready

---

## Request for Architect

Please provide:

1. **Execution sequence** (which WO → which WO → which WO)
2. **Parallel tracks** (which can run simultaneously)
3. **Time estimate** (if optimal sequence is different than 80 hr estimate)
4. **Go/no-go decision** on:
   - Should WO-232 (restructuring) be FIRST before other feature work?
   - Should WO-233 (ATB fix) be FIRST or wait for restructuring?
   - Should we ship Phase 0 (web) before starting Phase 2 (restructure)?

---

## Context for Architect

**User priorities (from 6/1 session):**
- "group consolidate, make sure we are not recreating the wheel" → WO deduplication ✓
- "try to get everything organzied in the pipeline it is not first in first out" → dependency order ✓
- "im playing and testing locally till build is ready" → local iteration → web re-upload → Phase 0 closure
- "yes when i play test thats the worst part" → combat feel (WO-217–219) is highest priority after blockers
- "get ready here comes alot of orders" → ready for large execution batch

**Stakeholder is in active local testing** — can validate work as it ships.

---

**Awaiting architectural direction for optimal execution sequence.**

