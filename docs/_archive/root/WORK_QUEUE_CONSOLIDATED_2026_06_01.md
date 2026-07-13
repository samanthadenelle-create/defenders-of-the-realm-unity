# Consolidated Work Queue — No Duplication, Dependency-Ordered
**Date:** 2026-06-01  
**Basis:** WO-196 through WO-229 + existing WO-1 through WO-210  
**Status:** Deduplicated, regrouped by execution logic (not FIFO)

---

## ⚠️ Key Consolidations

### Hero Cards Merged (WO-223/224/225/226 → SINGLE WO-230)
**Duplication detected:** 4 work orders for the same task (UI card creation pattern).
- **Consolidation:** Create ONE work order "Hero Select UI — Add 4 Character Cards"
- **Scope:** Sylas (Archer), Grom (Knight), Thrain (Mage), Elara (Cleric)
- **Time:** 1.5–2 hours total (all 4 cards in parallel/batch)
- **Avoids:** Repeating card layout/animation 4x

**New WO number:** WO-230 (replaces WO-223/224/225/226)

### Combat Feel Consolidation (WO-217/218/219 → stays sequential, but check WO-213)
**Potential overlap:** WO-213 (Troop downscale + battle UI pills) vs. WO-219 (visual feedback)
- **WO-213 scope:** Replace placeholder pill UI with actual character models
- **WO-219 scope:** Add particle effects, hit-stop, screen shake, damage numbers
- **Status:** NOT duplicated — 213 is UI swap, 219 is VFX/feedback
- **Keep both** but sequence: WO-213 first (character swap), then WO-219 (add effects)

### Monetization Check (WO-72–80)
**Status:** WO-72 (strategy) through WO-80 (Vercel backend) already exist  
- **Current state:** ~70% built (per user's notes)
- **Our new work:** None—avoid touching (no new monetization WOs created)
- **Confirmed:** Economy system (WO-228/229) is SEPARATE from shop/payment system

---

## Executive Revised Queue

### 🔴 PHASE 1: Critical Blockers (2 hours, unblocks everything)

| WO | Title | Status | Time | Blocker For |
|---|---|---|---|---|
| **196** | WebGL no-Brotli rebuild | READY | 10 min | Web deployment |
| **211** | WebGL optimize (remove unused assets) | READY | 30 min | Build size, load speed |
| **215** | Build mode click-to-place + validation | READY | 45–60 min | Construction features |

**Execute:** 196 → 211 → 215 (sequential)  
**Then:** Re-upload to itch.io, confirm playable

---

### 🔴 PHASE 2: Combat Feel Polish (5–8 hours, sequential — HIGHEST UX impact)

| WO | Title | Status | Time | Dependency |
|---|---|---|---|---|
| **213** | Troop downscale + battle UI pills → character models | READY | 35–50 min | None—do first |
| **217** | Animation polish (anticipation/impact/recovery) | READY | 2–3 hr | After 213 (optional) |
| **218** | Animation layering (attack while moving) | READY | 1–1.5 hr | After 217 |
| **219** | Visual feedback (hit-stop, particles, trails, damage #) | READY | 2–3 hr | After 218 |
| **220** | Audio feedback (whoosh, impacts, casts) | SPEC ONLY | 1.5–2 hr | After 219 (future) |

**Execute:** 213 → 217 → 218 → 219 → (future 220)  
**Note:** 213 can run parallel to 217 if needed (independent swaps)  
**Output:** Combat feels responsive, impactful, satisfying

---

### 🟡 PHASE 3A: Economy Pillar (4.5–6.5 hours, tied pair)

| WO | Title | Status | Time | Dependency |
|---|---|---|---|---|
| **228** | Resource nodes + pet harvesting (code ready) | CODE READY | 2.5–3.5 hr | None |
| **229** | Harvest visual feedback + HUD display | READY | 2–3 hr | After 228 |

**Execute:** 228 → 229  
**Code ready:** 7 .cs files in `Assets/_Modules/Economy/` (just needs prefabs + scene placement)  
**Output:** Pet auto-harvests; resources accumulate in HUD

---

### 🟡 PHASE 3B: Narrative Onboarding (6–10 hours, overlappable)

| WO | Title | Status | Time | Dependency |
|---|---|---|---|---|
| **222** | Tutorial redesign (hero → free tower → supplies quest) | READY | 1.5–2 hr | After 215 (build mode) |
| **230** | Hero Select UI — 4 Character Cards (Sylas/Grom/Thrain/Elara) | CONSOLIDATED | 1.5–2 hr | None |
| **227** | Opening cutscene + story companion system | READY | 3–4 hr | After 222 + 230 |

**Execute (parallel):** 230 independent, then 222, then 227  
**Consolidation:** Merged WO-223/224/225/226 into single batch WO-230  
**Output:** 4-hero select + guided tutorial + opening cinematic

---

### 🟠 PHASE 3C: Visual Polish (2–3 hours, parallel after Phase 1)

| WO | Title | Status | Time | Blocker |
|---|---|---|---|---|
| **212** | Gate z-fighting alignment fix | READY | 15 min | None |
| **214** | Dual-camera (village overhead / overworld over-shoulder) | READY | 30–40 min | None |
| **221** | Defend Tower camera closer (better sightlines) | READY | 10–15 min | None |

**Execute:** All parallel (independent fixes)  
**Output:** No visual glitches, camera feels right for context

---

### 🟡 PHASE 4: Content Loop (4–6 hours, after Phase 1)

| WO | Title | Status | Time | Blocker |
|---|---|---|---|---|
| **216** | Enemy camps system (Frostbite/Emberfang/Wraithveil) | READY | 4–6 hr | After 215 (build mode) |

**Execute:** After 215 works  
**Output:** 3 camp types, repeatable enemy spawning

---

## Execution Timeline (Recommended Order)

```
PHASE 1 (2 hr):
  WO-196 (Brotli) → WO-211 (optimize) → WO-215 (build mode)
  [ Game playable on itch.io + build mode works ]

PARALLEL after Phase 1:

PHASE 2 (5–8 hr, sequential):
  WO-213 (swap UI) → WO-217 (animation polish) → WO-218 (layering) → WO-219 (VFX)
  [ Combat feels good ]

PHASE 3A (4.5–6.5 hr, sequential):
  WO-228 (pet harvest) → WO-229 (visual feedback)
  [ Economy loop playable ]

PHASE 3B (6–10 hr, parallel):
  WO-230 (hero cards) [2 hr]
  WO-222 (tutorial) [1.5–2 hr, after 215]
  WO-227 (cutscene + companion) [3–4 hr, after 222 + 230]

PHASE 3C (2–3 hr, parallel):
  WO-212, WO-214, WO-221 (all polish fixes)

PHASE 4 (4–6 hr, after 215):
  WO-216 (camps)

FUTURE (post-launch):
  WO-220 (audio)
```

---

## Actual Critical Path (Not FIFO)

**Hard dependencies** (can't skip):
1. WO-196 → web deployment works
2. WO-211 → load speed acceptable
3. WO-215 → build mode works (blocks WO-222, WO-216)

**High impact per hour:**
1. WO-217–219 (combat feel: +5 hours = biggest UX gain)
2. WO-228–229 (economy: +6.5 hours = core gameplay loop)
3. WO-227 (narrative: +4 hours = story context)

**Can run anytime (no blockers):**
- WO-212/213/214/221 (visual polish)
- WO-230 (hero cards)

---

## Deduplication Checklist

- [x] Hero cards merged into WO-230 (was WO-223/224/225/226 — 4 separate → 1 batch)
- [x] Combat overlap checked (WO-213 ≠ WO-219 — UI swap vs. VFX, keep both)
- [x] Monetization confirmed independent (WO-72–80 existing, no new shop work)
- [x] Economy confirmed unique (WO-228/229 ≠ monetization)
- [x] Tutorial confirmed unique (WO-222 teaches mechanics, not monetization)
- [x] Narrative confirmed unique (WO-227 opening cinematic, not gameplay loop)

---

## Revised Work Order List (20 total, deduplicated)

**Critical Blockers (Phase 1):**
- WO-196, WO-211, WO-215

**Combat Feel (Phase 2):**
- WO-213, WO-217, WO-218, WO-219, WO-220 (future)

**Economy (Phase 3A):**
- WO-228, WO-229

**Narrative (Phase 3B):**
- WO-222, WO-227, WO-230 (consolidated hero cards)

**Polish (Phase 3C):**
- WO-212, WO-214, WO-221

**Content (Phase 4):**
- WO-216

**Existing (maintain):**
- WO-1 through WO-210 (no changes)

---

## What We're NOT Doing (Avoided Duplication)

- ❌ WO-220 audio — deferred (can ship without, post-launch feature)
- ❌ New monetization work — WO-72–80 already exist at 70%
- ❌ New hero mechanics — use existing 4 classes (Sylas/Grom/Thrain/Elara)
- ❌ New dungeon/campaign — Defend Tower is the loop; camps are enemy content

---

## Next Step for CLI

**Execute Phase 1 immediately** (2 hours):
1. WO-196: Fix Brotli on itch.io
2. WO-211: Optimize build (remove unused 92MB + 19MB source)
3. WO-215: Wire build mode click-to-place

**Report back:** Is web playable? Does build mode work?

Then Phase 2 (combat feel) unlocks for maximum UX gain.

---

**Summary:** 20 work orders, no duplication, organized by dependency + impact. Ready for CLI execution.
