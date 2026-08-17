<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 170 — 2D Retro Battle Animations & Spell VFX (the throwback FF vibe)

**Status: READY TO IMPLEMENT**
**Priority:** Medium — the *feel* layer of the FF battle screen; makes ATB read as a real retro RPG battle.
**Date:** 2026-05-30
**Lane:** Combat / VFX — `DeNelle.BattleATB` UI/VFX + sprite assets. Code + 2D assets. No VillageSceneBuilder; no bake.
**Source:** owner — *"add simple 2D animations for battle scenes, simple spell casts, retro throwback vibe."*
**Pairs with:** WO-169 (party HUD + FF layout: enemies left / heroes right) and BATTLE_2D_PARTY_DESIGN.

---

## The vibe (locked)
**SNES-era retro RPG (FF4–6 / Chrono Trigger).** Flat, punchy, readable 2D — NOT modern 3D particle
storms. Simple sprite-sheet frame animations + bold, short spell flashes. Charm over fidelity: a
2-3-frame cast pose, a bright elemental burst, a screen-flash on a big hit. Deliberately low-fi and
snappy — the throwback IS the aesthetic. Keep it **performant on mobile** (sprites/flipbooks, not heavy VFX).

### Nostalgia is a DESIGN PILLAR, not a limitation (owner 2026-05-30)
The retro look is a deliberate **strength**, and the team should lean into it on purpose:
- **Instant legibility** — anyone who's seen an FF/Chrono battle knows exactly how this works on sight =
  near-zero combat-tutorial friction.
- **Emotionally sticky** — the FF4–6 generation are now adults with disposable income (and overlaps the
  crypto-flush audience the NORTH_STAR targets). Nostalgia is a real retention + spend hook, not just art.
- **Cheap + fast to produce** — flat 2D flipbooks cost a fraction of modern VFX, and *look intentional*.
- **Differentiates** — a polished retro ATB battle nested inside a low-poly base-builder is a combo
  nobody else ships; the contrast is a signature, not a clash.
- **⚠ The one craft rule:** retro must read as **"by choice," not "by budget."** Clean, confident,
  consistent low-fi = stylish. Sloppy/inconsistent low-fi = looks cheap. Polish the *restraint* — tight
  timing, bold readable colors, satisfying snap — so the nostalgia lands as a love letter, not a shortcut.

## What to add (all on the WO-169 battle screen)

### 1. Per-unit battle animations (simple sprite-sheet / flipbook)
- **Idle** — a gentle 2-frame breathing/bob loop per combatant (party + enemies).
- **Attack/cast pose** — a brief 2-3 frame "step forward + swing / raise staff" on a unit's action (the
  classic FF lunge-and-return). Return to idle after.
- **Hit react** — a quick flash/recoil (sprite tint white + small shake) when a unit takes damage.
- **Defeat** — a simple fade/topple (sprite fade-out or a 2-frame collapse) when a unit falls.
- Drive these from the existing battle events (`ATBRuntimeState` `OnActionSubmitted`/`OnTurnResolved` —
  WO-169): the action fires the pose, the damage fires the hit-react, death fires the defeat anim.

### 2. Simple spell-cast VFX (retro elemental flashes)
- A small library of **flat 2D effect flipbooks**, one per element/ability family — e.g.:
  - **Fire** — a burst of flame sprites on the target.
  - **Ice** — shards / a frost crystal flash.
  - **Aether/lightning** — a bolt / sparkle pop.
  - **Physical** — a slash/impact spark.
  - **Heal/buff** — rising green/gold motes on an ally.
- Each ability maps to an effect id (data-driven — fits the dynamic direction; the ability def carries
  its VFX key, played on resolve). AoE = the effect plays on **all** targets (ties WO-169 targeting).
- **Throwback flourishes:** a brief **screen flash** on a big/crit hit, a subtle **screen shake** on heavy
  damage, floating **damage numbers** in a retro bitmap font (cyan/white normal, yellow crit). Optional
  classic **battle-start swirl/wipe** transition into the battle screen.

### 3. Keep it data-driven + reusable
- Effects are a **catalog of flipbook prefabs/sprite-sheets** keyed by id; the ability/combatant def
  references its anim + VFX keys (no hard-coded "if fire then..."). Matches the project's dynamic-
  collection direction (WO-169 §P2). Adding an element = adding an asset + a key.
- Reuse existing VFX/SFX seams where they fit (AudioService for the cast SFX; the damage-pop HUD seam).

## Constraints
- **2D / sprite-based, mobile-light** — flipbooks + tints + simple transforms, not 3D particle systems.
- Code-built UI/VFX driving (no UXML); play off `ATBRuntimeState` events (WO-169) — don't poll.
- `DeNelle.BattleATB` assembly; brace-gate; no bake. Engine math untouched (this is presentation only).
- Asset note: simple sprite-sheets can be sourced/authored cheaply; if assets aren't ready, ship with
  **placeholder colored flipbooks** so the system works, swap art later (don't block the system on art).

## Acceptance criteria
1. Each combatant plays **idle / attack-cast / hit-react / defeat** sprite animations driven by battle events.
2. Abilities play a **simple 2D elemental VFX flipbook** on their target(s); AoE plays on all targets.
3. Retro flourishes present: **damage numbers** (retro font, crit color), **screen flash/shake** on big hits; optional battle-start transition.
4. Effects + anims are **data-driven** (ability/combatant def → anim/VFX key; a catalog of flipbooks) — no hard-coded element branches.
5. **SNES-retro feel** — flat, punchy, readable; mobile-light (sprites not particle storms).
6. Driven off `ATBRuntimeState` events (WO-169); engine untouched; code-built; brace balance; no bake.
7. Works with placeholder art if final sprites aren't ready (art swappable later).

## Open questions for owner
- **Art source** — author custom sprites, use an asset-store retro VFX/sprite pack, or AI-generate the flipbooks? (Recommend a retro 2D VFX pack + placeholder colors to start.)
- **How retro** — full pixel-art sprites, or stylized-flat-vector "retro-inspired" (cleaner, matches the low-poly world better)? (Recommend stylized-flat to sit beside the low-poly 3D world; pure pixel-art may clash.)
- **Damage-number font** — pixel bitmap font, or a clean retro-styled font?

## Done checklist (CLAUDE.md §10)
- [ ] Per-unit idle/attack/hit/defeat sprite anims off battle events
- [ ] Per-element 2D VFX flipbooks on targets; AoE plays on all
- [ ] Retro damage numbers + screen flash/shake; optional battle-start transition
- [ ] Data-driven anim/VFX keys (catalog); no hard-coded element branches
- [ ] Mobile-light 2D; code-built; engine untouched; works with placeholder art
- [ ] Brace balance; no bake
- [ ] `WORK_ORDER_170_battle_2d_retro_vfx.RESULT.md` when complete
