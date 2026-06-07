# Final Execution Plan — Silo-Aligned Architecture + Complete Feature Pipeline

**Date:** 2026-06-01  
**Status:** READY TO IMPLEMENT  
**Total Time:** ~60 hours (Phase 0 + Phase 1 + WO-232 restructure + Phase 3 features)  
**Architecture:** Silo-based (8 domains, clear ownership, no cross-silo dependencies)

---

## Executive Summary

**Approach:** Fix bugs → web deploy → restructure to silos → add features in parallel

**Why this order:**
1. Phase 0 (WO-234): Get ATB working so you can see what's broken
2. Phase 1 (WO-196/211/215): Get game playable on itch.io
3. WO-232: Restructure codebase to silos (prevents future chaos)
4. Phase 3: Add all features in parallel by silo (combat, economy, UI, etc.)

**Total timeline:** ~2 weeks at full-time execution

---

## Phase 0: HOT-FIX (1–2 hours)

**Goal:** Get ATB battle from invisible to playable

| WO | Task | Time | Status |
|---|---|---|---|
| **WO-234** | ATB bug fixes (BattleVfx stub + debug logging) | 1–2 hr | ✅ Ready |
| **TEST** | Play ATBBattle, verify HUD + bars | 30 min | Ready |

**Success:** Console shows no errors, HUD visible, ATB bars animate, clicks work

**Blocker if fails:** Debug output tells you exactly what's null/missing

---

## Phase 1: CRITICAL BLOCKERS (2 hours)

**Goal:** Game playable on itch.io + build mode working

| WO | Task | Time | Dependency |
|---|---|---|---|
| WO-196 | WebGL Brotli fix | 10 min | None |
| WO-211 | WebGL optimize (remove 92MB + 19MB) | 30 min | After 196 |
| WO-215 | Build mode click-to-place | 45–60 min | After 211 |
| **TEST** | Re-upload to itch.io, confirm playable | 30 min | After 215 |

**Success:** Game loads on itch.io, build mode works, no 403 errors

---

## WO-232: SILO-ALIGNED RESTRUCTURE (14–15 hours)

**Goal:** Reorganize codebase into 8 clear silos with dedicated folders, namespaces, and ownership

**Why now:** Before Phase 3, while codebase is still manageable. Prevents merge conflicts + confusion as team scales.

| Phase | Task | Time |
|---|---|---|
| 1 | Create folder structure | 1 hr |
| 2 | Move Core files → Silo.Core/ + namespaces | 2 hr |
| 3 | Move Combat files → Silo.Combat/ | 2 hr |
| 4 | Move World files → Silo.World/ | 2.5 hr |
| 5 | Move Economy files → Silo.Economy/ | 1.5 hr |
| 6 | Move UI files → Silo.UI/ | 2 hr |
| 7 | Move Progression files → Silo.Progression/ | 1 hr |
| 8 | Move AudioVFX files → Silo.AudioVFX/ | 1 hr |
| 9 | Move Narrative files → Silo.Narrative/ | 1 hr |
| 10 | Integration testing (full playthrough) | 1.5 hr |
| 11 | Cleanup + documentation | 0.5 hr |

**Deliverable:** Silo-aligned folder structure + clear ownership boundaries + zero cross-silo imports

**Folder Structure:**
```
Assets/Scripts/
├── Silo.Core/           # GameManager, ServiceLocator, Interfaces
├── Silo.Combat/         # In-world 3D + ATB battles
├── Silo.World/          # Terrain, village, waves
├── Silo.Economy/        # Store, currency, IAP
├── Silo.UI/             # All UI screens
├── Silo.Progression/    # Talents, pets, leveling
├── Silo.AudioVFX/       # Sound, particles
└── Silo.Narrative/      # Story, dialogue
```

---

## Phase 3: FEATURES IN PARALLEL (Total ~40 hours)

After WO-232 completes, all new work happens **by silo** in parallel.

### Phase 3A: COMBAT FEEL (5–8 hours)

**Goal:** Fix "worst part of playtesting" — make village combat feel good

| WO | Task | Time | Silo | Sequential? |
|---|---|---|---|---|
| WO-213 | Troop downscale + UI swap | 35–50 min | Combat | First |
| WO-217 | Animation polish | 2–3 hr | Combat | After 213 |
| WO-218 | Animation layering | 1–1.5 hr | Combat | After 217 |
| WO-219 | Visual feedback | 2–3 hr | Combat | After 218 |

**Parallel:** Run simultaneously with Phase 3B/3C

---

### Phase 3B: ECONOMY SYSTEM (5–6.5 hours)

**Goal:** Resource harvesting loop working

| WO | Task | Time | Silo | Sequential? |
|---|---|---|---|---|
| WO-228 | Pet harvesting + resource nodes | 2.5–3.5 hr | Economy | First |
| WO-229 | Harvest visual feedback + HUD | 2–3 hr | Economy | After 228 |

**Parallel:** Run simultaneously with Phase 3A/3C (no dependencies)

---

### Phase 3C: NARRATIVE + HERO (6–10 hours)

**Goal:** Story progression, hero selection, tutorial

| WO | Task | Time | Silo | Sequential? |
|---|---|---|---|---|
| WO-230 | Hero cards (4 characters) | 1.5–2 hr | Progression | First |
| WO-222 | Tutorial redesign | 1.5–2 hr | Narrative | After WO-215 |
| WO-227 | Opening cutscene + companion | 3–4 hr | Narrative | After 222 + 230 |

**Parallel:** Hero cards can start immediately. Tutorial/cutscene after WO-215 completes.

---

### Phase 3D: VISUAL POLISH (1–2 hours)

**Goal:** Final visual touches (can run anytime)

| WO | Task | Time | Silo | Notes |
|---|---|---|---|---|
| WO-212 | Gate z-fighting fix | 15 min | World | ✅ Anytime |
| WO-214 | Dual camera (village/overworld) | 30–40 min | World | ✅ Anytime |
| WO-221 | Defend Tower camera closer | 10–15 min | Combat | ✅ Anytime |

**No dependencies** — run in parallel with everything

---

### Phase 4: CONTENT LOOP (4–6 hours)

**Goal:** Enemy camps (wandering troops, multiple waves)

| WO | Task | Time | Silo | Dependency |
|---|---|---|---|---|
| WO-216 | Enemy camps (3 types) | 4–6 hr | World | After WO-215 |

**Note:** WO-215 (build mode) must be done first (defines world placement rules)

---

### Phase 5: ADDITIONAL FEATURES (Future, not in Phase 3)

These are Phase 4+ work (post-MVP):
- **WO-220:** Audio feedback (2 hr) — after core combat works
- **WO-237:** Hero Movement Refactor (1–2 hr) — polish WO-235
- Special abilities, dungeons, seasonal events, cosmetics progression, etc.

---

## Complete Work Order Summary

| WO | Title | Time | Phase | Status |
|---|---|---|---|---|
| 234 | ATB bug fixes | 1–2 hr | 0 | ✅ Ready |
| 196 | WebGL Brotli fix | 10 min | 1 | ✅ Ready |
| 211 | WebGL optimize | 30 min | 1 | ✅ Ready |
| 215 | Build mode click-to-place | 45–60 min | 1 | ✅ Ready |
| **232** | **Silo restructure** | **14–15 hr** | **Foundation** | ✅ Ready |
| 235 | In-world combat core | 2–3 hr | 3A | ✅ Ready |
| 236 | Cosmetic Store UI | 1–2 hr | 3B | ✅ Ready |
| 213 | Troop downscale | 35–50 min | 3A | Ready |
| 217 | Animation polish | 2–3 hr | 3A | Ready |
| 218 | Animation layering | 1–1.5 hr | 3A | Ready |
| 219 | Visual feedback | 2–3 hr | 3A | Ready |
| 228 | Pet harvesting | 2.5–3.5 hr | 3B | Ready |
| 229 | Harvest feedback | 2–3 hr | 3B | Ready |
| 230 | Hero cards | 1.5–2 hr | 3C | Ready |
| 222 | Tutorial | 1.5–2 hr | 3C | Ready |
| 227 | Cutscene | 3–4 hr | 3C | Ready |
| 212 | Gate z-fighting | 15 min | 3D | Ready |
| 214 | Dual camera | 30–40 min | 3D | Ready |
| 221 | Tower camera | 10–15 min | 3D | Ready |
| 216 | Enemy camps | 4–6 hr | 4 | Ready |

---

## Execution Timeline

```
Phase 0: WO-234 (1–2 hr) 
    ↓ TEST: Does ATB work?
    ↓ YES → Phase 1

Phase 1: WO-196/211/215 (2 hr)
    ↓ TEST: Game playable on itch.io?
    ↓ YES → WO-232

WO-232: Restructure (14–15 hr)
    ↓ COMPLETE: All files in silos, namespaces updated, tests pass
    ↓ → Phase 3

Phase 3 (PARALLEL TRACKS): ~40 hours total
  
  Track A: Combat Feel (5–8 hr)
    WO-235 → WO-213 → WO-217 → WO-218 → WO-219
  
  Track B: Economy (5–6.5 hr)
    WO-236 → WO-228 → WO-229
  
  Track C: Narrative (6–10 hr)
    WO-230 (immediate)
    WO-222 (after WO-215 done)
    WO-227 (after WO-222 + WO-230)
  
  Track D: Polish (1–2 hr)
    WO-212, WO-214, WO-221 (anytime)

Phase 4: Content Loop (4–6 hr)
  WO-216 (after WO-215 done)
```

**Critical Path:** Phase 0 → Phase 1 → WO-232 → Phase 3A Track A (combat feel)
**Total time on critical path:** ~26 hours
**Parallel acceleration:** ~14 additional hours (Tracks B, C, D run simultaneously)

---

## Silo Responsibilities

| Silo | Owner* | Owns | Key Files |
|---|---|---|---|
| **Silo.Core** | — | GameManager, ServiceLocator, interfaces | GameManager.cs |
| **Silo.Combat** | Combat Lead | In-world 3D + ATB | EnemyController, BattleController, ATBUnit |
| **Silo.World** | World Lead | Terrain, village, waves | ExteriorTerrainBuilder, WaveManager |
| **Silo.Economy** | Monetization Lead | Store, currency, IAP | CurrencyService, StoreManager |
| **Silo.UI** | UI Lead | All screens | CosmeticShopUI, VillageHud, BattleHud |
| **Silo.Progression** | Systems Lead | Heroes, talents, pets | HeroData, TalentTree, PetSystem |
| **Silo.AudioVFX** | Audio Lead | Sound, particles | AudioService, VFXManager |
| **Silo.Narrative** | Story Lead | Dialogue, story | DialogueSystem, StoryManager |

*Owner = person/agent responsible for that silo's code quality + decisions

---

## Success Metrics

After each phase:

**Phase 0 Success:**
- [ ] ATB battle visible and playable
- [ ] No silent failures (all errors logged)
- [ ] HUD shows title, cards, ATB bars, commands

**Phase 1 Success:**
- [ ] Game loads on itch.io (no 403)
- [ ] Build mode works (click to place)
- [ ] Game is no longer stuck on loading screen

**WO-232 Success:**
- [ ] All code in silo folders
- [ ] All namespaces updated
- [ ] No cross-silo circular imports
- [ ] Full playthrough works (spawn → fight → win)
- [ ] Console clean (no warnings)

**Phase 3A Success:**
- [ ] Combat feels responsive
- [ ] Animations smooth, not choppy
- [ ] Multiple enemies attack without lag
- [ ] Audio feedback for attacks

**Phase 3B Success:**
- [ ] Pet harvests resources
- [ ] Resources visible on HUD
- [ ] Visual feedback (floating text, particles)

**Phase 3C Success:**
- [ ] Hero select works (can pick character)
- [ ] Cutscene plays
- [ ] Tutorial explains controls

**Phase 3D Success:**
- [ ] No z-fighting on gates
- [ ] Cameras feel appropriate (village vs. overworld)

**Phase 4 Success:**
- [ ] Camps spawn in world
- [ ] Multiple enemy types
- [ ] Difficulty increases with waves

---

## Risk Mitigation

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| WO-232 migration breaks build | Medium | High | Test after each phase, backup before |
| Phase 3 work conflicts (merge) | Medium | Medium | Clear silo boundaries, no cross-silo edits |
| Game performance degrades | Low | High | Profile after WO-235/236, optimize if needed |
| Feature creep delays ship | Medium | High | Stick to MVP scope, defer Phase 4+ work |

---

## What's NOT in Scope (Phase 4+)

These are future work:
- Dungeons (procedural or handcrafted)
- Seasonal events
- Guild system
- PvP
- Cosmetics progression (battle pass)
- Loot tables, crafting
- Full audio (currently stub with WO-220)
- Advanced AI (tactical, learning)

Do Phase 0–3 first. Then decide on scope for Phase 4+.

---

## Go/No-Go Gates

**Gate 1 (After Phase 0):** Is ATB battle working?
- **Yes** → Proceed to Phase 1
- **No** → Debug with console logs, fix, retry

**Gate 2 (After Phase 1):** Is game playable on itch.io?
- **Yes** → Proceed to WO-232
- **No** → Likely build or upload issue, not architectural

**Gate 3 (After WO-232):** Is codebase stable in new silos?
- **Yes** → Proceed to Phase 3 (all tracks in parallel)
- **No** → Revert and re-migrate more carefully

**Gate 4 (After Phase 3):** Is game loop complete + fun?
- **Yes** → Consider Phase 4 (camps, events, etc.)
- **No** → Polish more, balance, tune, repeat Phase 3 quality passes

---

## Next Steps (Immediate)

1. ✅ **Review this plan** — Does it feel right? Any changes?
2. ✅ **Hand off to CLI** — CLI executes Phase 0 (WO-234)
3. ✅ **Local test** — You play and report results
4. ✅ **Gate 1** — If ATB works, CLI proceeds to Phase 1

---

## Documents Reference

See:
- **WORK_ORDER_232_silo_architecture.md** — Detailed restructure spec + folder layout
- **WORK_ORDER_234_atb_bug_fixes.md** — Phase 0 bug fixes
- **WORK_ORDER_235_inworld_combat_core.md** — Phase 3A combat foundation
- **WORK_ORDER_236_cosmetic_store_ui.md** — Phase 3B store UI
- **CLAUDE.md** — Project rules (brace checks, no hand-edits to scenes, etc.)

---

**This is the complete roadmap. Phase 0 starts now. Proceed to Phase 1 only after ATB validates.**

**Estimated total calendar time: 2 weeks at full execution (CLI working on this + related work 40 hr/week).**
