# Defend-the-Tower — Spell Book Design (living doc)

Owner brainstorm 2026-05-29. Owner drives creative; agent implements. The hero is a
ranged turret on the stand; spells must read big and land on the distant enemies / the
tower. Foundation (DONE): offensive spells resolve at the **crosshair**, heal/ward spells
repair the **tower** (HeroAbilities.AimPointOverride + HealHandler).

## Owner's spell ideas (captured)
- **Rage** — +25% strength (damage) for 20s. **Team buff: extends to pets too.**
- **Slow all enemies** for 5s (global crowd-control).
- **Mage: DoT in an area** (lingering damage-over-time zone).
- **Fireball + freeze** (projectile/AoE that freezes on hit).
- **Tie into animations** (cast trigger already fires; add per-spell VFX + impact beats).

## New effect types to build (HeroAbilities.AbilityEffect + abilities.json "effect")
| Effect | Behaviour | Notes |
|---|---|---|
| `buff` | Temp damage × on hero **and pets** for N s | Rage. Needs a buff/timer layer + pet hook. |
| `globalslow` | Apply Slow to ALL live enemies for N s | Needs enemies to ACT on Slow (see below). |
| `dotzone` | Spawn a lingering zone; ticks damage in radius over N s | Mage Cinder Field. New runtime zone object. |
| `freeze` | AoE/strike that applies Freeze (stop) for N s | Fireball-freeze. Needs enemies to ACT on Freeze. |

## PREREQUISITE — enemies must act on status (currently stored, not applied)
`EnemyDamageable` stores `_slowUntil/_freezeUntil/_burnUntil` and exposes
`IsSlowed/IsFrozen/IsBurning`, but **Enemy.cs ignores them** ("does not yet model status
timers"). So slow/freeze do nothing visible today. Wire Enemy nav/glide speed to read
IsSlowed (×0.4) and IsFrozen (×0) — this unlocks snare, global-slow, and fireball-freeze.

## Proposed per-class book (draft — steer me)
- **Knight** (bruiser-support): Q Shield Bash · W **Battle Rage** (buff, team) · E Oath Ward (repair tower) · R Lantern Charge (big cleave at crosshair)
- **Ranger** (control): Q Quick Shot · W Snare Trap (single slow) · E **Tanglefield** (globalslow 5s) · R Storm of Arrows (aoe at crosshair)
- **Mage** (AoE/DoT): Q Arcane Bolt · W **Cinder Field** (dotzone) · E **Frost Nova** (freeze AoE) · R **Frostfire Meteor** (meteor + freeze)

## Pets
"Buffs extend to pets" — Rage should boost pet damage too (team buff). Verify pets reach
the DTT fight (they hunt nearest enemy; confirm they path/attack from the stand).

## Build order
A. ✅ Targeting fix (offense→crosshair, heal→tower).
B. Enemies act on Slow/Freeze (prerequisite for CC spells).
C. New effects: buff (Rage, team), globalslow, dotzone, freeze.
D. Per-spell VFX + animation polish.
