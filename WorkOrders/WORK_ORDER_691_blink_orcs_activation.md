> **SOURCE: Grok execution package 2026-07-12** (owner-relayed, built from the docs/SME dossier fleet). Slotted into the WO numbering by CLI; reconcile against docs/SME/WO677_PHASE0_APPLICABILITY.md (the code-verified assessment).

# 🛠️ Work Order: Activate Blink Stylized Orcs (Backlog Item #5)

**Status:** DONE (reconciled 2026-08-09 from the tree - `Assets/Editor/BlinkOrcImporter.cs` stages the pack into `Assets/Resources/Enemies/Blink/` (folder present) and `EnemyFactory.cs:559-562` maps all four archetypes: blink-orc-warrior, blink-orc-hunter, blink-orc-warlock, blink-orc-boss. NOT felt-verified; no `.RESULT.md`)

**Priority:** P1  
**Effort:** Low–Medium  
**Impact:** High — instant high-quality enemy roster

---

## Goal
The Stylized Orcs Bundle is already in the project, fully Humanoid-rigged, with 4 archetypes × 3 skins and two complete 22-clip animation sets + ready animator controllers. It currently has **zero code references**. Turn it on.

## What’s already there
- Unity-Humanoid rigged (verified)
- 4 archetypes × 3 skins
- Two complete 22-clip animation sets
- Ready-made animator controllers
- Zero existing code references (completely unused)

---

## Tasks for Claude

1. **Create Enemy Prefabs**
   - Create 4 clean enemy prefabs (one per archetype) using the orc models + skins.
   - Use the provided animator controllers (or override them into our shared ActorAnimator system if preferred).

2. **Register in Enemy System**
   - Add them to the enemy spawn tables / wave definitions.
   - Give them sensible stats, health, damage, and targeting behavior (they should use the existing Pillager or standard enemy AI).

3. **Animation Integration**
   - Make sure the 22-clip sets play correctly through our Action / Motion Caster system (or keep the vendor controllers if they are already solid).
   - Map common keywords (Idle, Run, Attack, Hit, Death, etc.).

4. **Visual Polish**
   - Ensure materials are URP-correct (no magenta).
   - Add any missing hit/death VFX if needed.

5. **Validation**
   - Spawn each of the 4 archetypes in a test scene.
   - Confirm they move, attack, take damage, and die correctly.

---

## Deliverables
- 4 ready-to-use Orc enemy prefabs
- Registered in the enemy system
- Working animations
- Short notes on how to add more skins later

This should be one of the fastest high-impact content additions we can make.