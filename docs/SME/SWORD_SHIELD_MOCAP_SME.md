# SME Dossier — Studio Mocap: Sword & Shield Moves (+ sibling mocap sets)

**Date:** 2026-07-12 (overnight session) · **Owner ask:** "we are not making them anything like how the demo shows"
**Pack:** Reallusion/ActorCore *Studio Mocap Series — Sword and Shield Moves* (45 clips)
**Path:** `Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/`
**Siblings:** `studio-mocap-hero-motion/` (48 showcase idles by weapon archetype), `studio-mocap-series-magical-moves/` (44 caster clips + the damage/stagger reactions this pack lacks)

## TL;DR (read this first)

Of 45 clips we actively use **about 9**. The pack is authored as a *connected fighter kit* —
a 2-D strafe locomotion tree, a directional block + parry matrix, and numbered combo
chains — and we consume it as single clips per keyword. The three biggest unused systems:

1. **8 strafe/backward locomotion clips** (`walk/run left|right|backward`) — need a 2-D blend tree; our current tree is forward-only.
2. **14 of 15 defensive clips** — six directional shield blocks (hold-guards) + nine sword parries including the authored `sword_parrybackward01→04` fighting-retreat chain. We wire ONE clip (`shield_blockup`).
3. **The authored chains** — `atk_shieldswipe01→02` (2-beat bash) plays only beat 1; `atk_shieldcharge`, `atk_jump`, `atk_kick`, `atk_slashup` are entirely unused; skill1/skill2 currently point at unarmed kung-fu kicks from a different set.

## 1. Full clip inventory (by role)

Every FBX ships a `0_T-Pose` bind take first; `MotionClipPicker` skips it.

| Role | Clips |
|---|---|
| Idle / guard (3) | `idle_alert` (watchful), `idle_battle` (weight-shifting), `idle_ready` (braced) |
| Directional walks (8) | `walkforward01/02`, `walkbackward01/02`, `walkleft01/02`, `walkright01/02` (left/right = strafes; 01/02 = two gait cycles each) |
| Directional runs (4) | `runforward_218667`, `runbackward`, `runleft_218668`, `runright_218669` |
| Turn-in-place (4) | `turnleft90`, `turnright90`, `turnleft180`, `turnright180` |
| Sword attacks (8) | `atk_slashup/down/left/right`, `atk_stab` (thrust), `atk_spin` (AoE finisher), `atk_kick` (shove), `atk_jump` (leap + downward strike) |
| Shield offense (3) | `atk_shieldcharge` (rush), `atk_shieldswipe01→02` (2-beat bash chain) |
| Shield blocks (6) | `shield_blockup/down/left/right/backward/crouch` — hold-guards per incoming direction |
| Sword parries (9) | `sword_parryup/down/left/right/crouch` (one-shot deflects) + `sword_parrybackward01→02→03→04` (retreating multi-parry chain) |

**Not in this pack:** deaths, hit/stagger reactions, dodge/roll, sheathe/draw. Companions:
staggers = `m-ss-damage-01/02/03` (magical-moves set, same rig); draw = `sword_drawing_m` /
`swordshield_ready_m` (hero-motion set).

## 2. Intended moveset structure (what "the demo shows")

- **Three-tier guard hub:** `idle_ready` → `idle_alert` → `idle_battle`, the stance attacks fire from and return to.
- **2-D locomotion blend:** forward/back/strafe at walk+run speeds plus four pivot turns — designed for an 8-way strafe tree, not a 1-D speed blend.
- **Combo chains (matched enter/exit poses):** directional slash strings; `atk_shieldswipe01→02`; `sword_parrybackward01→04`; `atk_shieldcharge` as an opener into the swipe chain.
- **Defensive matrix:** held directional blocks (loop) + active directional parries (one-shot) as parallel systems keyed on incoming attack direction.
- Loops/holds: idles, walks, runs, shield blocks. One-shots: all `atk_*`, all parries, turns.

## 3. Current usage (verified from code, 2026-07-12)

**KnightMocap controller** (`Assets/Editor/HeroAnimatorFactory.cs`): forward-only 1-D
locomotion (and the mocap walk/run were overridden by owner registry picks — currently
`walk_normal_f` / `move_run_m`); combat idle `idle_ready`; attack chain `atk_slashright →
atk_slashleft → atk_spin`; per-cast variants; single `shield_blockup` on a Block bool; one
hit-react (`m-ss-damage-01`); four turns wired.

**motion-castings.json knight rows** (owner canon, Motion Caster): `heavy=atk_slashdown`,
`attack1=atk_spin`, `attack2=atk_slashright`, `attack3=atk_stab`,
`skill1=fist_flyingkick_m`, `skill2=fist_whirlwindkick_m` (kung-fu, off-theme),
`combatWalk=walkforward01`, `combatRun=runforward_218667`, `block=shieldswipe01`,
`cast/castHeal=magical-moves` (correct per the melee-caster rule F8-48),
`unsheathe=sword_drawing_m`.

### Gap list vs the demo
1. No 2-D locomotion blend — 8 strafe/backward clips unused.
2. Shield-swipe chain plays beat 1 only — no second-beat state wired.
3. No directional block selector, zero parries — 14/15 defensive clips unused; registry vocabulary already declares `parry`/`dodge` keywords with no knight rows.
4. One hit-react for all directions; `m-ss-damage-02/03` unused.
5. Idle tiers collapsed to one; `idle_alert`/`idle_battle` unused.
6. `atk_shieldcharge`, `atk_jump`, `atk_kick`, `atk_slashup` unused; skills point at off-theme kicks.

## 4. Recommended knight moveset mapping (Motion-Caster-ready)

Morning picks the owner can make TODAY in the tool (single-clip keywords):

| Keyword | Recommended clip | Why |
|---|---|---|
| `skill1` | `atk_jump` | The Heroic Leap — the jump-and-stab-down the owner originally ruled |
| `skill2` | `atk_shieldcharge` | Shield rush gap-closer (or `atk_kick` as an interrupt/shove) |
| `parry` (new row) | `sword_parryup` | First parry in the game; directional selector later |
| `dodge` (new row) | `sword_parrybackward01` | Reads as an evasive deflect-step; pack has no true dodge |
| `combatIdle` | `idle_ready` | Keep (already right) |
| `victory` | `swordshield_forgeahead_m` (hero-motion) | Themed flourish |
| `unsheathe` | keep `sword_drawing_m` | Already right |

**Needs controller work (not pickable via keywords alone — candidate WOs):**
- 2-D strafe blend tree consuming the 8 directional walk/run clips (params exist to model after: `TurnDir`/`HitDir`).
- Block2 second beat so `atk_shieldswipe01→02` chains.
- Directional Block/Parry selector over the 6+9 defensive clips.
- Directional hit-reacts from `m-ss-damage-01/02/03`.

---
*Research: read-only fleet agent, verified against HeroAnimatorFactory.cs + motion-castings.json at HEAD; clip inventory from the actual FBX files.*
