# Building Upgrade Trees — Design Canon (owner-authored, 2026-07-16)

**Status:** AUTHORITATIVE design (owner-provided). WC3-style building tech-tree.
**Supersedes** any free-form perk invention. All perk/tier authoring conforms to THIS.
**Related:** memory `building-upgrades-warcraft3-style`; existing backend WO-432/460
(BuildingPerkService, building-tiers.json, ModifierService); tabbed Upgrade+Skills panel.

---

## Core philosophy (WC3 style)

- Buildings start at **Tier 0 (Basic)**; upgrade sequentially **Tier 0 -> 1 -> 2** using resources + time.
- Each upgrade **unlocks new capabilities**, improves efficiency, and feeds **tower defense + gathering**.
- Upgrades are **permanent per campaign/season**, cost **increasing** resources.
- Ties directly into procedural gathering, tower placement, and wave survival.
- **Visual progression:** buildings physically upgrade (model swaps) using KayKit/Tripo assets.
- **Mutual dependencies** (WC3 tech gating): e.g. Granary T1 required before Forge T2.
- Upgrades take **time (build queue, WC3-style)** and/or gathered resources.

## The four buildings & upgrade trees

### 1. Lumbermill — Wood / Construction
Role: primary wood gatherer + outpost expansion.
- **T0 (Basic Hut):** slow wood income from nearby nodes; basic walls/terrain clearing.
- **T1 (Sawmill):** +50% wood gathering speed; unlocks stronger basic towers (wooden barricades); faster outpost repairs.
- **T2 (Ancient Sawmill):** auto-gathers from distant nodes; unlocks advanced construction (better companion paths, reinforced outposts that reduce wave damage); wood -> temporary defense buffs.

### 2. Granary — Food / Population & Sustain
Role: food production + hero/companion support.
- **T0 (Root Cellar):** basic food to sustain a few companions; small health-regen aura for nearby towers.
- **T1 (Harvest Granary):** increases max companion count; food-based buffs (e.g. "Bounty": boosts gathering yield for one run).
- **T2 (Eternal Granary):** passive food income even offline; unlocks high-tier support towers (life-giving totems) and hero ability "Feast" (heal all towers + companions mid-wave).

### 3. Smithy — Metal / Military
Role: weapon/armor crafting for towers & heroes.
- **T0 (Forge Shed):** basic metal for simple tower upgrades (damage/armor).
- **T1 (Armory Smithy):** unlocks tower weapon tiers (Iron -> Steel); heroes get better gear for gathering runs.
- **T2 (Rune-Forged Smithy):** legendary upgrades (magic damage, chain lightning); epic towers + permanent hero equipment slots; salvage enemy drops into metal.

### 4. Forge — Magic / Essence (Aether)
Role: magic-resource (Aether/Essence) processing + spell tech.
- **T0 (Basic Anvil):** converts essence into basic spell runes for towers.
- **T1 (Arcane Forge):** elemental tower upgrades + companion spells (e.g. firestorm during defense).
- **T2 (Elarion Forge):** master magic (global cooldown reductions, hero ultimates); "Realm Echo" mechanics (temporary super-towers / wave-clear abilities); ties into lore.

## Resource economy tie-in
- **Wood** (Lumbermill) -> construction & basic towers.
- **Food** (Granary) -> sustain & population.
- **Metal** (Smithy) -> military power.
- **Essence/Aether** (Forge) -> magic & special abilities.
- Gathering runs feed all four -> meaningful choices ("rush Smithy for defense or Granary for more heroes?").

## Loop integration
- **Gathering phase:** upgraded buildings let companions harvest faster/safer/deeper (Smithy gear reduces risk).
- **Defense phase:** higher tiers = stronger towers, synergies (Lumbermill wood + Smithy metal = hybrid towers), emergency powers.
- **Progression feel:** early = survive on basics; mid = tech-rush one building for a strategy; late = fully-upgraded, personalized outpost.

## Implementation note (CLI — phasing)
Some effects map to EXISTING modifiers (gather speed, tower damage/armor, army/companion cap, cost/yield);
these author into `building-tiers.json` now. Others are NEW SYSTEMS to scope as their own work:
tower weapon-tier unlocks, hero equipment slots, salvage, offline/passive income, auto-gather distant nodes,
"Feast"/hero ultimates, "Realm Echo" super-towers, and per-tier building MODEL SWAPS. Flag each as
maps-today vs needs-new-system; ship the panel + the today-effects first, phase the rest.
