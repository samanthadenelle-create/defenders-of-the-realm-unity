# WORK ORDER 591 — Base-Defense V2 Pillar (damage/repair · threat AI · anti-air · resource cost)

**Status:** SPEC (reconciled 2026-08-09 - restates this file's own DESIGN CAPTURED line: a V2 pillar to be built behind `ff.basebuilding`, explicitly not V1 polish; no commit references WO-591)

**Status:** DESIGN CAPTURED — build with the V2 base-building layer (`ff.basebuilding`), NOT V1 polish.
**Owner design stream:** 2026-06-29 felt-test. One coherent base-defense loop emerged from testing the
flying dragon + a Heart/Tree death. Single-hero north star puts base-defense in V2 — so this is
designed + logged now, built behind `ff.basebuilding`. Do not bleed into V1 polish.

## The loop (how the pieces interlock)
Flyers/bosses **fight what hurts them** → **damage your towers** → towers go **inoperable until
repaired** with **harvested resources** → you need the **right tower for the threat** (anti-air) →
a defeat **damages, doesn't wipe** your base. The dragon, shrunk + lowered, becomes a real fight.

## Component specs

### A. Threat / retaliate targeting — universal (task #65)
- Already half-built: `EnemyBrain.FindHighestThreatTarget()` + RETALIATE (`Enemy.cs:330` "struck enemy
  turns on its attacker") + per-role tower-targeting. GAP: MiniBoss/boss roles return null → straight
  Heart-march (the dragon only hit the tree).
- Make threat/retaliate targeting **universal incl. bosses**: a tower shooting an enemy pulls its aggro;
  enemies prefer the nearest threat that is damaging them, falling back to Heart-march. Tune the
  march-vs-threat priority so they don't ignore the Heart forever.

### B. Tower damage → inoperable → repair (tasks #64, #63)
- Towers already `IDamageableStructure` + HP. Add a persisted **damaged/inoperable** state per slot in
  `GameState` (alongside `Towers`/`TowerAbilities`); **gate the tower's firing** on operable.
- **Repair action** at the tower: costs **wood/iron** (echo-harvest economy) to restore operability.
- **Defeat = DAMAGE, not wipe:** a Heart/Tree death damages the base (towers inoperable) instead of
  resetting to level-1/no-towers. Meta progress (gear/talents/echoes/gold/BestWave) already persists.
- Cracked/rubble **damage VFX** so the inoperable state is readable at a glance.
- V1-side check (do regardless): placed towers must **save on-place**, not only periodically.

### C. Resource-based upgrade + repair cost (task #60)
- Live `Tower.TryUpgrade` charges a single crystal ("diamond") cost; the `WatchtowerUpgrades.json`
  wood+iron+crystal table is NOT wired in. Wire upgrades **and repairs** to the multi-resource cost so
  the harvest→build→damage→repair loop consumes wood/iron; crystals become premium/prestige
  (empowerment, respec).

### D. Anti-air tower capability (task #67)
- Flyers (dragon) are uncounterable if towers target ground only. Add an **air-targeting capability** —
  a tower type or upgrade branch that can hit flying enemies — so ground/air counter-play exists.

### E. Dragon tuning (task #66 — partly V1-visible)
- The dragon (`Boss_Dragon.prefab`, EnemyFactory family `Dragon`/`boss-dragon`) is too big to see AND
  too big/high to engage. Size = `def.Height` in `enemies.json` (EnemyFactory normalizes; special large
  boss path ~`EnemyFactory.cs:203`). **Shrink + lower** to combat-reachable range so the hero can fight
  it. Keep the beauty, just smaller. No prefab hand-edit (data/code).

## Acceptance (V2)
- A tower under attack can be destroyed→inoperable→repaired (resource cost), state persists across save.
- Bosses + flyers retaliate against towers/hero that damage them.
- An anti-air tower can hit the dragon; the dragon is engageable at its new size/height.
- Heart death damages the base (repairable) rather than wiping it.

## Flags / scope
- All behind `ff.basebuilding` (V2). Dragon size (E) + save-on-place (B last bullet) are the only
  V1-safe slices and may ship in a V1 combat pass if the owner wants the dragon fixed sooner.
