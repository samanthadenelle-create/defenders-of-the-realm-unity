<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_127: Zelda-Style Overworld Combat Architecture (Design)

**Status:** READY FOR ARCHITECT  
**Owner:** Architecture (design phase, no code)  
**Priority:** High (establishes combat design before WO-124 ships, informs VFX factory scope)  
**Related:** WO-124 (VFX Factory), EnemyBrain.cs, DESIGN-DECISIONS.md, BATTLE_2D_PARTY_DESIGN.md  
**Acceptance:** Architecture doc delivered, design vision locked, EnemyBrain scope clarified, options explored + owner decision.

---

## Executive Summary

**Concept:** Zelda-style overworld combat encounters (sleeping camps, roaming bosses with minions) instead of ATB turn-based for overworld. ATB reserved for dungeon battles.

**Current state:** Idea only. Needs architecture thinking before we commit.

**Scope:** Design exploration, not implementation. Answer:
- What is the combat *feel*? (real-time, pauseable, action-paced?)
- What does EnemyBrain need to do? (AI scope)
- How does gear/progression affect it?
- What are the constraints/risks?
- What are the design options?

**Deliverable:** `ZELDA_OVERWORLD_COMBAT_DESIGN.md` — complete design doc with vision locked, owner decision point, CLI ready to implement.

---

## Questions to Answer

### 1. Combat Feel & Pacing

**Option A: Real-time action** (like Zelda BOTW)
- Player controls hero directly (WASD movement, click to cast abilities)
- Enemies attack in real-time (no turn order)
- Positioning and reflexes matter
- Fast-paced, skill-based

**Option B: Pauseable turn-based** (like Divinity Original Sin, Baldur's Gate 3)
- Real-time movement/positioning on map
- Combat pauses when you need to decide (cast spell, move, defend)
- Strategic positioning, less twitch skill
- Deliberate but dynamic

**Option C: Hybrid** (like Zelda Tears of the Kingdom)
- Real-time positioning and movement
- Abilities execute on a cooldown (not instant cast)
- Player can prepare, position, then commit to action
- Medium pacing

**Questions to explore:**
- Which fits the game's vibe (tower defense + spell book)?
- What's the skill floor vs. ceiling?
- How does it differ from ATB dungeons (should feel different)?
- Mobile-friendly? (real-time action is harder on touch)

---

### 2. Hero Control & Input

**If real-time:**
- WASD movement + mouse aim (PC)?
- Click-to-move (mobile)?
- Ability hotkeys or click-to-cast?
- Does hero auto-attack or only cast spells?

**If pauseable/hybrid:**
- Click-to-move during planning phase?
- How long is the "pause window"?
- Can enemies interrupt your planning?

**Key question:** How much control does the player have vs. auto-behavior?

---

### 3. Enemy Encounter Design

**Sleeping camps:**
- How many enemies (3-5? 10+)?
- Do they wake up when you approach, or only when you attack?
- Can you backstab/sneak kill before combat starts?
- Positioning: do they patrol, or static?

**Roaming bosses with minions:**
- Boss + 2-3 minions?
- Do they patrol a patrol route, or roam freely?
- Can you separate them or face them together?
- What triggers the encounter (proximity, aggression)?

**Stealth mechanics?**
- Can you approach undetected?
- Is there a "detection range"?
- Backstab bonus damage?
- Does one enemy alerting others cause a chain reaction?

**Environmental factors:**
- Does terrain affect combat (high ground, cover)?
- Can enemies use terrain defensively?
- Do you have access to environmental damage (traps, hazards)?

---

### 4. EnemyBrain Scope

**Current EnemyBrain (from codebase):**
- Pathfinding
- Attack patterns
- Damage/health logic
- Behavior states (patrol, chase, attack, retreat)

**New scope needed:**
- Formation/positioning (how many stand together, how they arrange?)
- Stealth awareness (detection range, alarm systems)?
- Tactical decision-making (when to retreat, when to special attack)?
- Minion leadership (boss coordinates minions)?
- Environmental use (pathfind to cover, high ground)?

**Complexity assessment:**
- Is current EnemyBrain sufficient, or does it need major expansion?
- What's the minimum viable AI vs. "nice to have"?

---

### 5. Gear & Progression Impact

**How does player power scale?**
- Gear increases stats (damage, health, speed)?
- Abilities unlock with level/progression?
- Enemy difficulty scales (higher-level enemies have more health, faster attacks)?

**How is difficulty managed?**
- Enemy roster by region (weak, medium, hard tiers)?
- Scaling by player level?
- Optional hard encounters (boss rushes, elite camps)?

**Example progression:**
- Early game: solo 1-2 enemies
- Mid game: small camps (3-5 enemies)
- Late game: roaming bosses (boss + 3 minions)

---

### 6. Constraints & Risks

**Development time:**
- Real-time action combat = higher dev cost (animation, AI, balance)
- Pauseable = medium cost (less animation polish needed)
- Hybrid = medium cost (moderate scope)

**Mobile compatibility:**
- Real-time action is harder on touch (requires precise input)
- Pauseable is easier (player controls pacing)

**Asset requirements:**
- Do enemies need combat-specific animations? (attack, defend, cast, react)
- Can you reuse map models + animations?
- What about VFX during combat? (use 3D factory from WO-124?)

**AI complexity:**
- Real-time AI needs fast decision-making (avoidance, kiting, targeting)
- Pauseable AI can be simpler (just respond to player moves)

---

### 7. Design Options to Explore

**Option 1: Full Real-Time Action**
- Hero directly controlled (WASD + click-to-cast)
- Enemies attack in real-time
- Stealth approach possible
- High skill ceiling
- *Risk:* Complex AI, animation-heavy, mobile unfriendly

**Option 2: Pauseable Tactical**
- Real-time positioning, pause to plan
- Turn-order within pauses (or free-form timing)
- Stealth approach possible
- Medium skill ceiling
- *Risk:* UI complexity (pause menu during combat), feels different from dungeon ATB

**Option 3: Cooldown-Based Hybrid**
- Real-time positioning and movement
- Abilities on cooldowns (not instant, not turn-based)
- Stealth approach possible
- Medium skill ceiling
- *Risk:* Balancing cooldown timings, feels unfamiliar

**Option 4: Minimal Encounters (No Full Combat)**
- Overworld = tower defense only (keep current Defend-the-Tower)
- Encounters = ambush/flee (not full engagement)
- Full combat reserved for dungeons (ATB)
- Low complexity
- *Risk:* Overworld feels less dynamic

---

## Design Decision Tree

```
Does overworld combat need full real-time control?
  ├─ YES → Real-time action (Option 1)
  │        - Animation-heavy
  │        - Complex AI
  │        - Mobile challenge
  │
  └─ NO → Pauseable/Tactical (Options 2–3)
           - Medium complexity
           - Mobile-friendly
           - Clear separation from ATB
```

---

## Deliverables

**ZELDA_OVERWORLD_COMBAT_DESIGN.md** must include:

1. **Vision Statement** (1–2 paragraphs, what it feels like to play)
2. **Design Pillars** (3–4 core principles, e.g., "stealth-first", "tactical positioning", "real-time action")
3. **Combat Feel** (chosen option + rationale)
4. **Hero Control** (input model, ability system)
5. **Encounter Types** (sleeping camps, roaming bosses, specs)
6. **EnemyBrain Scope** (what AI needs to do, expansion needed?)
7. **Progression** (how gear affects combat, difficulty scaling)
8. **Constraints** (dev time, mobile compatibility, asset needs)
9. **Integration with ATB** (how overworld differs from dungeon battles)
10. **VFX/Audio Strategy** (use 3D factory from WO-124? spatial audio?)
11. **Open Questions** (what's TBD for owner decision)
12. **Owner Decision Point** (which design option, any constraints?)

---

## Success Criteria

- [ ] Vision is clear and specific (not vague)
- [ ] EnemyBrain scope is well-defined (minimal expansion vs. major rewrite?)
- [ ] Design options are explored fairly (not biased toward one)
- [ ] Constraints and risks are identified
- [ ] Effort estimate provided (dev time for CLI)
- [ ] Ready for owner (Samantha) sign-off
- [ ] Ready for CLI implementation (design doc is the spec)

---

## Notes for Architect

**This is design work, not implementation.** Your job is to:
1. Explore the design space (what are all the viable options?)
2. Assess each option (pros, cons, dev cost, risk)
3. Recommend a direction (but don't force it)
4. Leave decision point for owner (Samantha picks)
5. Deliver a spec that CLI can implement from

**Key constraint:** The design should leverage EnemyBrain (which already exists), not create new systems. If EnemyBrain needs expansion, identify exactly what and why.

**Timing:** This should be done BEFORE WO-124 ships or ASAP after, so CLI knows what the VFX factory is being built for.

---

## Timeline

- **Exploration:** 4–6 hours (research options, sketch designs, assess trade-offs)
- **Documentation:** 2–3 hours (write ZELDA_OVERWORLD_COMBAT_DESIGN.md)
- **Owner decision:** 1 hour (Samantha picks option, adds constraints)
- **Total:** ~1 day architect work, then owner decision, then CLI ready to implement

---

## Sign-Off

**Architect (Claude):** [to be filled after design work]  
**Owner (Samantha):** [awaiting design doc]  
**CLI:** [awaiting owner decision]
