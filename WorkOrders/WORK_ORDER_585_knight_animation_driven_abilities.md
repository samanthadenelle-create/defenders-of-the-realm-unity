> ⚠ **NUMBER COLLISION — this document does not own WO-585; `WORK_ORDER_585_inventory_equip_feedback.md` does.**
> Referred to hereafter as **WO-585-B (knight animation-driven abilities)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK ORDER 585 — Knight Animation-Driven Ability Set + Skill-Tree Actives

**Status:** DESIGN — owner review pending (drafted 2026-07-04)
**WO number:** provisional 585 — slot into the correct lane in `MASTER_PIPELINES_BACKLOG` / `CLI_LANES_WO_NUMBERS.md` before implement.
**Lane:** Combat/AI (code) + data (`hero-talents.json`) — no scene files.
**Owner directive it serves:** *"build the skills off what our animation can do best"* + *"passives are great but don't add a great animation sequence."* Memory: [[actives-are-animation-moments-convert-passives]], [[feel-first-combat-over-clip-depth]], [[talent-tree-v2-full-design]].

---

## The design rule (locked)

1. **The animation picks the ability, not the reverse.** The clip is the expensive, hard asset; the ability is numbers around it. Design each active to *showcase* a clip we already own and that looks great.
2. **Active = a moment; passive = a number.** Convert passives that have a **natural animation** into active cooldowns (big effect, brief window, on cooldown → fires a signature clip). Purely-numeric effects stay passive.
3. **More actives than slots** = a real loadout choice. Bar = **5 combat slots** for V1; tree offers ~8–10 reachable actives.
4. Bar slot = Obsidian frame + **our icon** + cooldown fill; the **same icon** is used on the skill-tree node and the battle-bar slot (shared source → they can't drift).

---

## The clips we own (grounded — `Assets/Action/Knight/Motion/`, ~137 FBX, AccuRig, same rig as KnightV3)

Money folder = `studio-mocap-sword-and-shield-moves\` (purpose-built for this character).
**Today only ~11 combat clips are wired** (`atk_slashright/left/spin`, `atk_stab`, `shield_blockup`, `m-ss-damage-01`, 4 turns) — 4 attack clips recycled across 9 slots. The best ability clips are UNUSED.

---

## V1 KNIGHT BAR — 5 slots, built from the best clips

| Slot | Ability | Clip (source) | Effect (start generous, tune) | Status |
|---|---|---|---|---|
| 1 | **Mending Oath** (heal) | `sword_heroic_f` **or** a magespellcast channel (owner picks — see decisions) | Self-heal; big, ~15–20s cd | ability EXISTS (`universal.mend`); needs clip + icon |
| 2 | **Arcane Bolt** (simple cast) | cast/point (owner picks martial-point vs battle-magic channel — see decisions) | Ranged magic bolt; short cd | ability EXISTS (`universal.arcane-bolt`); needs clip + icon |
| 3 | **Shield Bash** | `atk_shieldswipe01` (UNUSED — a *real* bash; today q wrongly plays `atk_stab`) | Stun/knockback interrupt; ~8s cd | NEW active |
| 4 | **Iron Resolve** (brace) | `shield_blockcrouch` (UNUSED brace pose) | **−40% incoming dmg for 8s, 25–30s cd** (converted from the −18% passive) | CONVERT passive→active |
| 5 | **Bulwark Charge** | `atk_shieldcharge` (UNUSED gap-closer) | Charge to target, engage + minor dmg; ~12s cd | NEW active |

## Extended tree actives (the loadout menu — more than 5, so slotting is a choice)

| Ability | Clip (UNUSED, on-rig) | Fantasy |
|---|---|---|
| **Overhead Cleave** | `atk_slashdown` | heavy single-target |
| **Sunder / Launcher** | `atk_slashup` | armor-break / pop-up |
| **Whirlwind** | `atk_spin` | AoE (currently only a combo-ender) |
| **Leap Slam** | `atk_jump` | leap to point + AoE |
| **Riposte / Parry** | `sword_parry*` set (9 clips unused) | timed block → counter |
| **Shield Wall** | directional `shield_block*` (only `blockup` wired) | active guaranteed-block (convert Guardian Stance passive) |
| **War Shout / Rally** | `sword_shout_m` (hero-motion) | party/self buff |
| **Taunt** | `sword_provocative_m` | aggro pull (V2 allies) |
| **Judgment** (ultimate) | `sword_judgment_m` | execute pose finisher |

## Passives (stay passive — numeric, no animation)
Guardian Stance block-chance (unless promoted to Shield Wall), Mending Oath heal +%, crit, always-on armor, cd-reduction. These *tune* the actives.

---

## Build tasks (implementation, when approved)

1. **Timed-buff handler** — the enabling piece. Current `HeroTalentModifiers` applies passives permanently; add an "apply effect for N seconds, then expire, on cooldown" ability type (reusable for every active-buff node). This unblocks Iron Resolve / Shield Wall conversions.
2. **Wire the unused clips** — extend `HeroAnimatorFactory.BuildKnightMocapController()` clip constants (`MocapAttackClips` etc.) with the new basenames; `LoadClip` matches by basename, folder must be in `searchRoots` (non-recursive). Add Cast-variant slots for the new actives. Menu: *Defenders/Animation/Build Knight Mocap Locomotion Controller*.
3. **Data** — in `hero-talents.json`, set the converted/new nodes `kind: "active"`/`"skill"` with `abilityId`, `effect.type` (new: `timedBuff`), duration + cooldown; fix slot q to point at the shield-bash clip, not `atk_stab`.
4. **Shared icon source** — one icon set (e.g. `Resources/Talents/knight/…` reused by the battle-bar slot); tree node and bar slot both read it. Obsidian frame + icon + cooldown-fill (per [[ui-blink-template-master-frame-formula]]).
5. Headless-verify (CompileGate + a talent-effect regression) before deploy; PO felt-verifies the animations on screen (the real bar).

## Open decisions for owner
- **Heal clip:** `sword_heroic_f` (martial heroic pose + green VFX) vs a magespellcast channel (more "spell"). Knight isn't a caster → heroic pose likely reads better.
- **Cast (Arcane Bolt):** martial "point the sword, bolt flies" (reuse a thrust; reads as a warrior ability) vs a true battle-magic channel (borrow a magespellcast clip; caster-flavored, slight prop mismatch since hands hold sword+shield). Owner's call on Knight identity.
- **Bar size:** 5 confirmed? (drives how many actives are "reachable" in V1.)
- **Iron Resolve tuning:** −40%/8s/25–30s (dramatic save) vs −25%/10s/15s (frequent, milder). Feel-first: start dramatic.

---
🤖 Design spec, grounded in the real clip inventory + KnightMocap.controller + HeroAnimatorFactory. No code/scene/bake yet.
