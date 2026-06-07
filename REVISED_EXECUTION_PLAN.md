# Revised Execution Plan — Fix First, Refactor Only If Needed

**Principle:** Make the existing system actually work before making big architectural changes.

**Date:** 2026-06-01  
**Total Time (revised):** ~100 hours (includes validation gates)

---

## Phase 0: IMMEDIATE HOT-FIX (1–2 hours) — RUN THIS FIRST

**Goal:** Get ATB battle from "broken/invisible" to "playable with HUD"

| WO | Task | Time | Do This |
|---|---|---|---|
| **WO-234** | Bug fixes (BattleVfx stub + debug logging) | 1–2 hr | ✅ **RUN IMMEDIATELY** |
| **TEST** | Play ATBBattle, verify HUD appears + ATB bars animate | 30 min | ✅ **VALIDATE** |

**Success criteria:**
- [ ] Console shows all ✓ logs (no errors)
- [ ] HUD visible (title, enemy/party cards, log, command bar)
- [ ] ATB bars filling over time
- [ ] Click "Attack" submits action
- [ ] Battle resolves without crashes

**If this works:** Proceed to Phase 1 (blockers)  
**If this fails:** Debug output tells us exactly where (no more silent failures)

---

## Phase 1: Critical Blockers (2 hours) — AFTER Hot-Fix Validation

**Goal:** Get game playable on itch.io + build mode working

| WO | Task | Time | Dependency |
|---|---|---|---|
| WO-196 | WebGL Brotli fix | 10 min | None |
| WO-211 | WebGL optimize | 30 min | After 196 |
| WO-215 | Build mode click-to-place | 45–60 min | After 211 |
| **TEST** | Re-upload to itch.io, confirm playable | 30 min | After 215 |

**Success criteria:**
- [ ] Game loads on itch.io (no 403 errors)
- [ ] Build mode works (click to place towers)
- [ ] Game is no longer "stuck on loading screen"

---

## Phase 2: Architecture Decision Gate (AFTER Phase 1)

**STOP. Evaluate.**

**Questions for Samantha:**
1. Now that the ATB battle is visible and playable, does it FEEL broken or do you want to keep the design?
2. Is the "enterprise-style" architecture working for you, or does it feel over-engineered?
3. Do we proceed with WO-232 (restructuring) or skip it and add features directly?

**Options:**
- **Option A (Recommended):** Keep current architecture. Proceed directly to Phase 3 (features). WO-232 (restructuring) becomes optional future work.
- **Option B:** Run WO-232 first (restructure), then Phase 3. Takes 14–15 more hours but results in cleaner codebase.

**My take:** If ATB battle works after hot-fix, the architecture is probably fine. No reason to spend 14–15 hours restructuring if the current system is already working. Add features first, refactor later if it becomes a problem.

---

## Phase 3A: Combat Feel (5–8 hours, if selected)

**Goal:** Fix "worst part of playtesting"

| WO | Task | Time | Sequential? |
|---|---|---|---|
| WO-213 | Troop downscale + UI swap | 35–50 min | First |
| WO-217 | Animation polish | 2–3 hr | After 213 |
| WO-218 | Animation layering | 1–1.5 hr | After 217 |
| WO-219 | Visual feedback | 2–3 hr | After 218 |

**Parallel to Phase 3A:** Economy (WO-228/229, 5–6.5 hr)

---

## Phase 3B: Economy System (4.5–6.5 hours)

**Goal:** Resource harvesting loop working

| WO | Task | Time | Sequential? |
|---|---|---|---|
| WO-228 | Pet harvesting | 2.5–3.5 hr | First |
| WO-229 | Harvest feedback | 2–3 hr | After 228 |

**Runs parallel to Phase 3A (no dependencies)**

---

## Phase 3C: Narrative (6–10 hours)

| WO | Task | Time | Dependency |
|---|---|---|---|
| WO-230 | Hero cards (4 chars) | 1.5–2 hr | None |
| WO-222 | Tutorial | 1.5–2 hr | Needs WO-215 from Phase 1 |
| WO-227 | Opening cutscene | 3–4 hr | Needs WO-230 + WO-222 |

---

## Phase 3D: Visual Polish (1–2 hours, parallel)

| WO | Task | Time | Can run anytime |
|---|---|---|---|
| WO-212 | Gate z-fighting | 15 min | ✅ Yes |
| WO-214 | Dual camera | 30–40 min | ✅ Yes |
| WO-221 | Defend Tower camera | 10–15 min | ✅ Yes |

---

## Phase 4: Content Loop (4–6 hours)

| WO | Task | Time | Dependency |
|---|---|---|---|
| WO-216 | Enemy camps | 4–6 hr | WO-215 (build mode) |

---

## Revised Timeline

```
Phase 0 (HOT-FIX):
  WO-234 (1–2 hr) → TEST → GATE (Is ATB working?)
      ↓ YES
  
Phase 1 (BLOCKERS):
  WO-196/211/215 (2 hr) → Re-upload to itch.io → TEST → GATE (Is game playable?)
      ↓ YES
  
ARCHITECTURE GATE:
  Question for Samantha: Keep current arch or restructure (WO-232)?
      ↓ KEEP CURRENT (Recommended)
  
Phase 3 (PARALLEL TRACKS, 5–10 hr each):
  Track A: WO-213/217/218/219 (Combat feel) — 5–8 hr
  Track B: WO-228/229 (Economy) — 5–6.5 hr
  Track C: WO-230/222/227 (Narrative) — 6–10 hr
  Track D: WO-212/214/221 (Polish) — 1–2 hr (anytime)
  
Phase 4 (AFTER Phase 1 complete):
  WO-216 (Camps) — 4–6 hr
```

**Total time (without restructuring):** ~40 hours of work  
**Total time (with restructuring):** ~55 hours of work

---

## Key Decision Points (Gates)

### Gate 1: After WO-234 (Hot-fix)
**Question:** Does the ATB battle now display and play?
- **Yes** → Proceed to Phase 1
- **No** → Debug with console logs; WO-234 gave us visibility to fix it

### Gate 2: After WO-215 (Build mode)
**Question:** Is the game playable on itch.io?
- **Yes** → Proceed to architecture decision
- **No** → Likely build or upload issue, not architectural

### Gate 3: Architecture Decision
**Question:** Restructure now (WO-232) or add features first?
- **Restructure first** → 14–15 hrs, then features
- **Features first** → Features now (40 hrs), restructure later if needed (RECOMMENDED)

---

## My Recommendation

1. **Run Phase 0 immediately** (WO-234) — 1–2 hours
2. **Run Phase 1** (WO-196/211/215) — 2 hours
3. **At Gate 3, choose Option A** — Skip WO-232, proceed to features
4. **Run Phases 3A–3D in parallel** — 5–10 hours each track
5. **Run Phase 4** (camps) — 4–6 hours

**Total: ~40 hours instead of 55–80 hours**

The current architecture is working. Don't restructure it until it's proven to be a problem. Add features, validate, then refactor if needed.

---

## What's Different From Original Plan

| Original Plan | Revised Plan | Why |
|---|---|---|
| WO-232 (restructuring) as Phase 2 | Made optional after Gate 3 | Architecture not proven broken |
| 80+ hours total | 40 hours without restructuring | Remove unnecessary refactoring |
| Blocks features on restructuring | Features run in parallel | Don't let restructuring block gameplay |
| Uncertain value of restructuring | Value only if system breaks | Pragmatic: refactor when needed, not before |

---

## Next Steps

1. ✅ **Immediate:** CLI runs WO-234 (hot-fix, 1–2 hr)
2. ✅ **Then:** Test (30 min) — verify HUD works
3. ✅ **Then:** CLI runs Phase 1 (WO-196/211/215, 2 hr)
4. ✅ **Then:** Re-upload to itch.io, test
5. **Then:** Samantha decides at Gate 3 (restructure or features first?)

---

**This is the pragmatic path: fix what's broken, validate it works, then decide on bigger changes.**

