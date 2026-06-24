# WORK_ORDER_494 — ARENA COMBAT DESIGN: family synergy, counterplay, clarity (Grok-guided)

**Status:** DESIGN / TABLED · Combat lane · captured 2026-06-23 (Grok review of the open-kite arena)
**Relates:** WO-491 (animation/telegraphs), WO-493 (game feel), the themed arena scene, the immersion
research agent. This is the DESIGN layer — what makes the open-kite arena fight *tactical*, not flat.

## 1. Ability synergies & counterplay (biggest impact in open space)
The orc family = a COORDINATED threat that rewards smart play:
- **Tank** — draws aggro / body-blocks / protects the others.
- **Healer** — big healing beams (interrupt or focus-kill FIRST).
- **Wizard/Mage** — charges big AoE or single-target bursts (telegraphed, dodgeable).
- **DPS** — flanks / adds pressure.

**Knight tools that shine in the open:**
- **Dash / gap-closer** — reach the Healer fast.
- **Knockback / pull** — break formation (separate Tank from Healer).
- **Area denial / slow zone** — control the open field.
- **Burst ultimate** that rewards focusing the right target (e.g. "Healer Focus" bonus damage).

## 2. Visual & audio clarity (critical on mobile, open scenes)
- **Role readability:** floating role icons + colored outlines — green Healer, red DPS, blue Wizard,
  yellow Tank. (Reuse AttentionGlow/outline; ties to the node-glow approach-ring idea.)
- **Obvious cast wind-ups:** Wizard glowing staff, Healer channeling beam (WO-491 telegraph + WO-493 audio cue).
- **Satisfying feedback:** screen shake on big hits, juicy damage numbers (exist), **hit-stop on interrupts** (WO-493).
- Knight abilities = clear VFX trails + impact particles (reuse Spells Pack / Mirza Beig).
- Reference: Genshin / Honkai Star Rail — tactical-feeling mobile combat via strong visual language.

## 3. Positioning in open space (no tight chokes needed)
- Use the **natural scene terrain** (hills, trees, rocks — the themed arena) for line-of-sight + kiting.
- Knight abilities create **temporary mini-chokes** (ice wall, taunt circle, knockback).
- Enemy AI tries to **surround / focus the Knight** -> rewards circle-strafing + ability timing.

## 4. Pacing & reward loop
- **Short fights (45-90s)** with clear PHASES (e.g. a "Protect phase" while the Healer channels).
- **Performance-based rewards:** faster kill = better loot; perfect interrupts = bonus SKR / upgrade mats.
- **Post-fight summary** highlighting good plays ("Healer interrupted 3x", "Great kiting").

## Quick wins (do first, cheap)
- **Spawn the family SPREAD OUT** initially -> Knight must choose who to engage first. (BattleArena.SpawnFamily
  already spreads on X across the north side; widen with the bigger 60x48 arena.)
- Knight **"Engage" ability** that highlights the highest-threat target (Healer/Wizard).
- Use the existing **VFX system** for big juicy spell effects + hit feedback.

## Notes for the bots
- This is DESIGN intent — sequence behind WO-491 (animation/telegraph) + WO-493 (feel) which are the
  mechanics it rides on. Roles (mage/tank/warrior) exist; HEALER is implied here — confirm if the family
  includes a healer or add one. Mobile-first: big clear ability buttons, tap-to-priority-target, one-tap ult.
- Cross-check against the immersion-research agent's takeaways (Fallout/Clash/WC3/SC) — merge, don't duplicate.
