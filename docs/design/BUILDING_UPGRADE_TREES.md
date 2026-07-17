# Building Upgrade Trees — Design Canon (owner-authored, rev 2 · 2026-07-16)

**Status:** AUTHORITATIVE. WC3-style tech tree. **Rev 2 supersedes the 4-building draft** —
it is now **6 buildings, Tier 0 -> 3**, mapped 1:1 onto the SIX existing building ids (no
rename). Constraints from owner: **NO YarnSpinner** (upgrade UI is code-built uGUI, already
compliant); **models are owner-sourced** — CLI produces the per-tier model list, owner creates/buys.
**Related:** memory `building-upgrades-warcraft3-style`; backend WO-432/460; tabbed Upgrade/Skills panel (built);
`docs/design/BUILDING_PERKS_DESIGN.md` (effect mapping).

## Overview
- **Tier 0** = basic starting version; **Tiers 1-3** = sequential upgrades. Each tier improves
  efficiency, unlocks new abilities/units/towers, and gives passive bonuses.
- **Synergies:** buildings boost each other (upgraded Lumbermill makes Barracks/Armorer units cheaper, etc.).
- **Visuals:** buildings grow grander per tier (Tripo3D/KayKit assets — owner sources models).
- **Strategic paths:** can't rush everything; players pick military vs magic vs economy focus.

## Building id map (1:1, no rename)
Lumbermill=`lumbermill` · Windmill=`windmill` · Forge=`forge` · Armorer=`armorer` · Barracks=`barracks` · Arcane-Tower=`arcane-tower`.

## 1. Lumbermill (Wood / Construction) — id `lumbermill`
- **T0 Basic Lumber Camp:** slow wood income; basic walls.
- **T1 Sawmill:** +40% wood gather rate; unlocks reinforced wooden towers (higher HP).
- **T2 Timber Hall:** auto-gathers from medium range; -25% construction time for ALL buildings; unlocks mobile barricades (temp defenses).
- **T3 Ancient Grove Mill:** global wood income +25%; wood spendable mid-wave for emergency repairs. **Synergy:** Armorer + Barracks units 15% cheaper.

## 2. Windmill (Food / Sustain) — id `windmill`
- **T0 Simple Windmill:** basic food; small tower health-regen aura.
- **T1 Harvest Windmill:** +50% food rate; +1 max active companions; unlocks "Bounty" (short gather boost).
- **T2 Grand Mill:** passive offline food; unlocks sustain towers (Life Totem heals defenders); companions minor regen while gathering.
- **T3 Eternal Winds:** massive food surplus; global tower+hero health regen. **Synergy:** boosts Barracks training speed + Forge essence conversion.

## 3. Forge (Essence / Magic Tech) — id `forge`
- **T0 Basic Forge:** slow essence processing; basic rune upgrades for towers.
- **T1 Arcane Forge:** +60% essence rate; unlocks elemental tower enchantments (fire/ice).
- **T2 Rune Crucible:** unlocks hero spells + area-effect runes; -20% spell cooldowns.
- **T3 Elarion Eternal Forge:** high-tier magic (global abilities like "Realm Shield"); essence powers super-towers mid-wave. **Synergy:** improves Arcane-Tower damage + Armorer gear quality.

## 4. Armorer (Metal / Defense & Gear) — id `armorer`
- **T0 Makeshift Armory:** basic metal for tower armor upgrades.
- **T1 Field Armorer:** +45% metal rate; unlocks armored towers (better resistance) + basic hero gear.
- **T2 Master Smithy:** unlocks advanced weapons (piercing, splash); heroes get combat bonuses during gathering runs.
- **T3 Legendary Armory:** epic gear sets + salvage (enemy drops -> metal); permanent tower damage boost. **Synergy:** Barracks units tankier + Forge runes stronger.

## 5. Barracks (Companions / Military Units) — id `barracks`
- **T0 Training Grounds:** basic companion (scout) for gathering.
- **T1 Warrior Barracks:** unlocks melee/ranged companions; +1 max companion slot.
- **T2 Veteran Hall:** improved companion stats + abilities (taunt, heal); call reinforcements mid-wave.
- **T3 Elite Legion Hall:** heroic companions with ultimates; auto-defend outpost when idle. **Synergy:** Lumbermill/Windmill reduce training costs.

## 6. Arcane-Tower (Specialized Magic Defense) — id `arcane-tower`
- **T0 Basic Arcane Spire:** basic magic damage tower.
- **T1 Enchanted Spire:** chain lightning / slow effects; higher damage vs magic-immune foes.
- **T2 Mystic Obelisk:** area-denial runes + mana abilities; can empower nearby towers.
- **T3 Elarion Arcane Nexus:** ultimate power (orbital strikes / global slow / wave-clear bursts); campaign centerpiece. **Synergy:** Forge amplifies all effects; Armorer adds durability.

## Progression arc
Early = Lumbermill + Windmill (economy). Mid = Armorer + Barracks (defense/teams). Late = Forge + Arcane-Tower (epic waves/bosses).

## Implementation phasing (CLI)
- **DONE (this build):** tabbed Upgrade/Skills panel; the existing numeric perks (tower/troop/production mults); army-cap (Barracks more troops) + auto-harvest (Lumbermill capstone); Village-Tier raise so perks are buyable.
- **Phase-2 (needs new systems, per-effect mapping in BUILDING_PERKS_DESIGN.md):** synergies (cross-building cost/speed), new tower types (reinforced/armored/sustain/Life Totem/mobile barricades), construction-time reduction, offline income, emergency mid-wave repairs, elemental typing, hero spells/ultimates/cooldowns, salvage, new companion units + reinforcements + auto-defend, Arcane-Tower ability tiers, and **per-tier building MODEL SWAPS** (owner sources models). Add `costIron` so Armorer tiers can cost Metal.
