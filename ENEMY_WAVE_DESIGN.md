# Defend-the-Tower — Enemy Roster & Wave Composition (living doc)

Owner brainstorm 2026-05-29: real assets, flying ≠ land, families grouped (not a
single-file row), roles (melee / ranged / tank / healer), **smaller waves but actual
composition logic**. Owner drives creative; agent implements.

## Roster (KayKit Skeletons + dragon) → role → behaviour
| Role | Model | Behaviour | HP | Speed |
|---|---|---|---|---|
| Grunt (melee) | Skeleton_Minion | march → melee the tower | low | normal |
| Warrior (melee) | Skeleton_Warrior | march → melee | med | normal |
| Rogue (fast) | Skeleton_Rogue | rush → melee | low | **fast** |
| **Ranged** | Skeleton_Mage | **stop at range, pelt the tower from afar** | low | normal |
| **Tank** | Skeleton_Golem | slow, soaks hits, body-blocks | **high** | slow |
| **Healer** | Necromancer (elite) | **heals nearby living enemies on a cadence** | med | slow |
| **Flyer** | Dragon | glides in over the field (air lane) | med | — |
| Boss | Big Dragon / Necromancer | air, high HP, telegraphed | huge | — |

## Behaviours to build (the "actual logic")
- **Ranged**: halt at standoff distance from the tower, attack it from there (don't run into melee). New: ranged-attack state on Enemy.
- **Healer**: periodic AoE heal to nearby living enemies — makes the player prioritise it. New: healer component.
- **Tank**: just stat-driven (high HP, slow) — soaks, screens the squishies.
- **Fast**: stat-driven speed.
- **Flyer**: already glides (air lane). Needs a flying-looking model (dragon), NOT a floating skeleton.

## Formation — grouped families, not a conga line
Spawn each wave as **squads**: a cluster spawns together across a lateral spread (not one X
lane), with depth stagger, and each enemy targets a slightly different point on the tower's
front arc so they advance as a loose mob. Squads can be single-family ("4 warriors") or mixed.

## Wave composition (smaller waves, real logic)
Escalating mixes rather than count-spam:
- W1: 4× Grunt
- W2: 3× Grunt + 1× Ranged
- W3: 2× Warrior + 2× Ranged + 1× Tank
- W4: 3× Warrior + 2× Ranged + 1× Tank + 1× Healer
- W5: + Flyers
- Boss wave: Boss + a small honor guard
Tuning: fewer enemies, each meaningful; the player reads the squad and picks targets.

## Build phases
- **P1 (visual/feel, low risk):** flying = dragon (delineated), grouped/spread formation, family→model mapping. No new behaviour.
- **P2 (logic):** roles as data (HP/speed/range per family), ranged stand-off attack, healer heal-allies, tank stats.
- **P3:** wave-composition table driving squad spawns.
- **P4:** walk/attack animation (KayKit clips) + per-family VFX.
