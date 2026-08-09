# WORK_ORDER_494 — ARENA COMBAT DESIGN: family synergy, counterplay, clarity (Grok-guided)

**Status:** SPEC (reconciled 2026-08-09 - restates this file's own DESIGN / TABLED line in the canonical vocabulary: a design capture that feeds WO-491/493/496, with no ship of its own; no commit references WO-494)

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

---

## DEEP DIVE (Grok expanded, 2026-06-23) — full mobile-first battle design
**Core philosophy:** dynamic positioning in OPEN space replaces chokepoints; role counterplay makes each
family learnable; juicy mobile feedback keeps it satisfying on touch.

### Enemy family roles (visual + behavior + spawn)
- **Tank** — big shield icon, heavy armor glow; high HP, taunts Knight, charges with knock-up; stays
  BETWEEN Knight and backline.
- **Healer** — bright green cross + pulsing aura; channels big heals + occasional self-shield; stays back,
  RUNS if focused. (Interrupt/focus-kill first.)
- **Wizard** — glowing staff, purple energy buildup; slow but devastating AoE / targeted burst; positions
  for coverage, VULNERABLE while charging.
- **DPS** — dual daggers/bow, red trails; fast attacks, flanks, ranged poke; circles to backstab.
- **Spawn:** start SPREAD in the natural terrain (behind trees/rocks — initial surprise); CONVERGE on the
  Knight after 3-5s. (Ties to the WO-495 treeline + the bigger 60x48 arena.)

### Knight kit (small, high-impact, positioning/timing-rewarding)
- **Dash / gap-closer** — tap+direction-swipe to a target/location; BONUS dmg dashing to Healer/Wizard.
- **Knockback / repel** — swipe to push in a cone/circle; breaks Tank protection, knocks Wizard out of cast.
- **Taunt / zone** — short area forcing Tank/DPS onto you; buys time to focus Healer (protects future pets).
- **Ultimate "Heroic Strike"** — brief charge → massive AoE/nuke; EXTRA dmg on a low-HP Healer / interrupted Wizard.

### Mobile controls
Move = tap ground (path-preview line). Basic = auto / tap enemy. Abilities = 4 large bottom-screen buttons
with cooldown rings + icons. Target priority = tap enemy portrait OR a "Focus Healer" button. One-tap ult
(+ optional direction swipe).

### Pacing / phases / win
45-90s fights, phases: **Open** (family converges) → **Burst** (Wizard charges) → **Sustain** (Healer ramps).
Win = defeat all; BONUS for Healer-first; PERFECT run (no death + all interrupts) = extra SKR / cosmetic drop.

### Post-fight screen (important on mobile)
"Great positioning — Healer interrupted 3x!" + performance STARS + reward breakdown + Retry / Continue Adventure.

### Visual/audio polish (premium feel)
Enemy health bars with floating role icons; Wizard cast = screen-edge PURPLE tint + warning; hit feedback =
camera shake + SLOW-MO on big interrupts + satisfying "thud"; environment = destructible rocks/trees for
temporary cover / AoE clears.

### Build-first menu (Grok's suggested entry points — owner to pick)
1. Ability System skeleton (Knight kit C# with synergies). ← spec'd below
2. Enemy family spawner (roles + simple AI behaviors).
3. Battle UI layout (mobile buttons + targeting).
4. Visual feedback package (VFX/particle drop-ins).

---

## KNIGHT ABILITY KIT — cooldown spec (Grok, 2026-06-23) — BUILD-READY
Tight 4-ability touch kit for 45-90s fights; cooldowns tuned so each gets 2-4 uses/fight with meaningful
positioning windows. Each ability counters specific family roles.

| Ability | CD | Uses/fight | Best vs | Effect | Mobile control |
|---|---|---|---|---|---|
| **Heroic Leap** (dash) | 6s | 3-4 | Healer/Wizard | dash to loc/enemy; **bonus dmg + stun** if dashing into Healer/Wizard | tap ground/enemy → swipe for direction boost |
| **Shield Bash** (knockback) | 9s | 2-3 | Wizard/Tank | cone/circle knockback + brief slow; breaks Tank protection, **interrupts Wizard cast** | tap button + swipe for cone |
| **Defender's Call** (taunt/zone) | 12s | 2 | Tank/DPS swarm | short-area taunt forcing Tank/DPS onto you + temp shield | tap to place zone |
| **Radiant Strike** (ult) | 35-45s | 1-2 | Healer priority | charge 1.5s → massive AoE/nuke; **bonus dmg** on low Healer / interrupted Wizard | big button + charge bar → tap to confirm |

**Synergy/reward combos:**
- Dash → Knockback to ISOLATE the Healer.
- Taunt the Tank → Ult the Healer = massive reward.
- Interrupt Wizard cast with Knockback → bonus SKR / **"Perfect Counter"** popup.

**Mobile UI:** bottom row of 4 large buttons + cooldown overlay (dim + timer text); ability trails; screen
shake on Ult; slow-mo on big interrupts. **Balance:** start with these CDs, shave 1-2s off Dash/Knock if
fights feel slow. Ties to WO-496 #1 (fire feedback on the button TAP, not on resolution) + #5 (reserve juice).

---

## TIME-BOX + STAR RATING + BOSS FIGHTS (owner 2026-06-23)
- **TIME-BOX EVERY FIGHT — regular AND boss:** each fight has a TARGET time; beat it faster -> more STARS
  (1-3, Clash-style). The point: **never a drawn-out slog** — punchy, replayable, performance-rewarding.
  (`BattleArena.BattleTimeoutSeconds` exists as a hard safety cap; this adds a TARGET/par time for the star
  rating, not just a timeout.) Ties to WO-496 #14 (count-up reward beats) + #17 (live progress meter).
- **Stars** = beat-the-clock + perfect-play (no death, all interrupts) -> bonus SKR / cosmetic (WO-494 win conds).
- **BOSS FIGHTS:** one HUGE boss with its OWN unique movesets (telegraphed, WO-491) + its own timer for stars.
  Special boss scenes/backdrops (WO-499, LFS art "when we get there"). The boss is the climactic time-boxed test.
- Post-fight: stars slam in + count-up rewards + "Retry/Continue" (WO-494 post-fight screen).
