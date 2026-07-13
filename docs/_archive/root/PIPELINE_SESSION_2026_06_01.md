# Pipeline Status — Session 2026-06-01

**Last Updated:** 2026-06-01 (this session)  
**Baseline:** PIPELINE_STATE.md (2026-05-29 CLI run)  
**Status:** 🟢 GREEN with clear action items

---

## Executive Summary

**Where we were:** WebGL build deployed to itch.io with Brotli incompatibility error blocking testers. Combat feel identified as "worst part" of playtesting. Build mode click-to-place not wired. Tutorial missing.

**Where we are:** Critical blockers identified and work orders created. Economy foundation (pet harvesting + resource nodes) coded and ready for CLI. Combat feel overhaul planned (4-phase sequence). Narrative onboarding architected (story companion system). 13 new work orders created, all READY TO IMPLEMENT.

**Next 48 hours (CLI execution):**
1. WO-196: Fix itch.io Brotli error (10 min)
2. WO-211: Optimize build size (30 min)
3. WO-217–219: Combat feel stack (5–8 hours, sequential)
4. WO-228–229: Pet harvesting + visual feedback (4–5 hours, tied together)

---

## Full Work Queue (17 NEW + 7 EXISTING)

### 🔴 CRITICAL BLOCKERS (Do These First)

| WO | Title | Status | Time | Blocker For |
|---|---|---|---|---|
| **196** | WebGL no-Brotli fix | READY | 10 min | Web tester access |
| **211** | WebGL optimize (remove unused assets) | READY | 30 min | First-load UX, itch.io file size |
| **215** | Build mode click-to-place + validation | READY | 45–60 min | All build/construction features |

**Recommendation:** Execute 196 → 211 → 215 sequentially. Total: ~2 hours. After this, web build works + build mode works.

---

### 🔴 COMBAT FEEL (Sequential, Highest-Impact Polish)

| WO | Title | Status | Time | Dependency |
|---|---|---|---|---|
| **217** | Animation polish (anticipation/impact/recovery) | READY | 2–3 hr | None — do first |
| **218** | Animation layering (attack while moving) | READY | 1–1.5 hr | After 217 |
| **219** | Visual feedback (hit-stop, particles, trails, damage #s) | READY | 2–3 hr | After 218 |
| **220** | Audio feedback (whoosh, impacts, casts) | SPEC ONLY | 1.5–2 hr | After 219 |

**Recommendation:** Execute 217 → 218 → 219 → (future 220). These MUST run sequentially. Total: 5–8 hours. **Biggest UX win per hour invested.**

---

### 🟡 NARRATIVE ONBOARDING (Parallel after Core Systems)

| WO | Title | Status | Time | Dependency |
|---|---|---|---|---|
| **222** | Tutorial redesign (hero first, free tower, supplies quest) | READY | 1.5–2 hr | 215 (build mode) |
| **226** | Cleric hero (4th class, healer role) | READY | 3–4 hr | None |
| **227** | Opening cutscene + story companion system | READY | 3–4 hr | 222, 226 |

**Recommendation:** Do in parallel after WO-215 works. 226 can run independently. 227 ties 222+226 together. Total: 7–10 hours (overlappable).

---

### 🟡 ECONOMY SYSTEM (New Core Pillar)

| WO | Title | Status | Time | Dependency |
|---|---|---|---|---|
| **228** | Resource nodes + pet harvesting (code ready) | CODE READY | 2.5–3.5 hr | None |
| **229** | Harvest visual feedback + HUD display | READY | 2–3 hr | After 228 |

**Recommendation:** Execute 228 → 229. Code for 228 already written (7 .cs files ready to integrate). Total: 4.5–6.5 hours. Enables entire idle/economy loop.

---

### 🟠 VISUAL POLISH (Parallel)

| WO | Title | Status | Time | Dependency |
|---|---|---|---|---|
| **212** | Gate z-fighting fix | READY | 15 min | None |
| **213** | Troop downscale + battle UI pills → character models | READY | 35–50 min | None |
| **214** | Dual-camera (village overhead / overworld over-shoulder) | READY | 30–40 min | None |
| **221** | Defend Tower camera closer (better sightlines) | READY | 10–15 min | None |
| **223** | Archer hero card (Sylas) | READY | 30–45 min | None |
| **224** | Knight hero card (Grom) | READY | 30–45 min | None |
| **225** | Mage hero card (Thrain) | READY | 30–45 min | None |

**Recommendation:** All can run in parallel after WO-196/211/215. These are visual polish, not blockers. Total (if done sequentially): ~3 hours. (If parallel: 30–50 min).

---

### 🟡 CONTENT (After Build Mode Works)

| WO | Title | Status | Time | Dependency |
|---|---|---|---|---|
| **216** | Enemy camps system (Frostbite/Emberfang/Wraithveil) | READY | 4–6 hr | 215 (build mode) |

**Recommendation:** After WO-215 ships. Provides repeatable content + resource gathering context.

---

## Execution Roadmap (Recommended Sequence)

### Phase 1: Critical Fixes (2 hours, unblocks everything)
```
WO-196 (Brotli fix) → 10 min
WO-211 (Build optimize) → 30 min
WO-215 (Build mode input) → 45–60 min
[ Re-upload to itch.io. Game playable. ]
```

### Phase 2: Combat Feel (5–8 hours, highest UX impact)
```
WO-217 (Animation polish) → 2–3 hr
WO-218 (Animation layering) → 1–1.5 hr
WO-219 (Visual feedback) → 2–3 hr
[ Combat feels good. No more "sluggish" feedback. ]
```

### Phase 3: Economy + Narrative (Parallel, 7–15 hours)
```
PARALLEL TRACK A:
WO-228 (Pet harvesting code) → 2.5–3.5 hr
WO-229 (Harvest visuals + HUD) → 2–3 hr

PARALLEL TRACK B:
WO-226 (Cleric hero) → 3–4 hr
WO-227 (Story companion system) → 3–4 hr
WO-222 (Tutorial) → 1.5–2 hr

PARALLEL TRACK C (Polish):
WO-212/213/214/221 (visual fixes) → 2–3 hr total
WO-223/224/225 (hero cards) → 1.5–2 hr total
```

### Phase 4: Content (After Build Mode)
```
WO-216 (Enemy camps) → 4–6 hr
```

---

## Current State Summary

| Pillar | Status | Notes |
|---|---|---|
| **Defend the Tower** | ✅ BUILT | Fully playable, polished |
| **Village defense** | 🟡 WIRED, gaps remain | Wave loop built, visual polish pending |
| **WebGL deployment** | 🔴 BROKEN | Brotli error on itch.io (WO-196 fixes) |
| **Build mode** | 🔴 BROKEN | Code exists, input not wired (WO-215 fixes) |
| **Combat feel** | 🔴 WORST PART | Animation sluggish, no impact (WO-217–219 fixes) |
| **Tutorial** | ❌ MISSING | WO-222 adds story-driven onboarding |
| **Pet economy** | ❌ MISSING | Code ready (WO-228), needs integration + visuals |
| **Story/narrative** | ❌ MISSING | WO-227 adds opening cutscene + companion guide |
| **4th hero (Cleric)** | ❌ MISSING | WO-226 adds healer class |
| **Hero select UI** | 🟡 EXISTS | 3 cards ready, layout needs 4th slot update |
| **Resource refinement buildings** | ❌ DESIGN PENDING | Forge/Mill/Lumbermill/Arcane Tower/Jeweler (future WO) |

---

## Code Ready for CLI Integration

**Economy Foundation (WO-228):**
- ✅ ResourceNode.cs (base class)
- ✅ IronOreNode.cs, LumberNode.cs, MagicNode.cs, GemNode.cs
- ✅ PetHarvester.cs (auto-harvesting logic)
- ✅ ResourceInventory.cs (resource tracking + events)

**All in:** `Assets/_Modules/Economy/`  
**Remaining:** Prefabs, scene placement, HUD integration, save/load.

---

## Ship Criteria (For v1.0)

- [ ] WebGL loads on itch.io (WO-196)
- [ ] Build size optimized (WO-211)
- [ ] Build mode playable (WO-215)
- [ ] Combat feels good (WO-217–219)
- [ ] Tutorial teaches mechanics (WO-222/227)
- [ ] Pet economy functional (WO-228/229)
- [ ] 4 playable heroes (WO-226, existing 3)
- [ ] Opening cutscene (WO-227)

---

## Blockers & Dependencies

**Hard Blockers (can't ship without):**
1. WO-196 (web deployment error)
2. WO-215 (build mode broken)

**Critical Path (blocks major features):**
1. WO-196 → Web tester access
2. WO-215 → All construction features
3. WO-217–219 → Combat playable

**Nice-to-haves (don't block ship):**
- WO-220 (audio — can add post-launch)
- WO-216 (camps — content, not core loop)
- Hero card art (WO-223–225 — cosmetic)

---

## Confidence Level

🟢 **HIGH**

- All work orders are SPEC'd (not guesses)
- Code foundation written (WO-228)
- Time estimates realistic (based on scope)
- Execution path clear (Phase 1 → 2 → 3 → 4)
- No unknown unknowns

**What could break it:** Asset import issues (polyperfect models not found), animation system incompatibility, save/load complexity. All manageable with CLI's Windows codebase access.

---

## Recommended Next Step

**CLI starts Phase 1 immediately:**
1. Execute WO-196 (10 min)
2. Execute WO-211 (30 min)
3. Execute WO-215 (45–60 min)
4. Re-upload to itch.io + test

**Report back:** Is the web build loading? Does build mode work? Then phase 2 (combat feel) unlocks.

---

**Summary:** We went from "pipeline stuck on blockers" to "clear roadmap with code ready." 13 new WOs, 7 existing WOs, all actionable. You're 48 hours from a playable game with pet economy + tutorial + 4 heroes.
