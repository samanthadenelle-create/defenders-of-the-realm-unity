# WORK_ORDER_108 — Major Village Castle Town (Last Bastion) + World/Region Extension Overhaul

**Status: READY TO IMPLEMENT**

**Branch context:** feat/tower-core-loop (carry forward from prior pet/outpost/economy and build modal work).

**Core Vision (user query):**
- Epic defensible medieval **Castle Town** as player's "Last Bastion" (if it falls, game over).
- Extend world with multiple regions/scenes for progression, elemental themes, richer content/rewards.
- **Mobile-first**: performant (low draw calls, additive/streaming loads), reuse existing systems (BuildMode/Ghost/PreviewModal with rotation, EconomyService as *the* source of truth for all resources/income/upgrades, Hero/Enemy/ActorAnimator pipelines, etc.).
- Consistent scaling, no overlaps, ghost + modal rotation for all placement.
- All new income (nodes, outposts, automated defenses, region yields, building productivity) routes exclusively through EconomyService (Grant/AddResource/TrySpend/ResourceCost).

## High-Level World Structure Proposal (as required)
**1. Castle Town Layout (Last Bastion — improve/fix via VillageSceneBuilder only):**
- **Walls & Defenses**: Strong enclosing curtain walls (upgrade from current weak KayKit to layered Polyperfect _M Wall_Stone_*/Wall_Medieval_Stone + Quaternius/KayKit details for cohesion/perf). Corner **turrets** (place 4 taller Tower_* _M prefabs at corners with platforms). **Exactly 4 cardinal gates** (N/S/E/W openings with gatehouses using CastleGate assets + modular ends; clear 8-10m). Wide **walkable Ramparts** on top (4m+ platform using floor tiles + battlements/crenellations; full loop with stairs/ramps at gates + mids for access; placeable for defenses). Moat optional (keep/enhance existing for flavor).
- **Center**: Dominant **Tree of Life** (exact 0,0,0, glowing with emissive, large scale using tree_of_life.fbx + mound + stone ring). Central plaza.
- **4 Districts** (quadrants around Tree, divided by cross roads from gates; explicit offsets + padding in builder for no overlaps/consistent scale):
  - NE: **Commerce** (markets, shops, stalls — Quaternius/KayKit shop modules).
  - SE: **Housing** (homes, inns, cottages).
  - SW: **Pet** (stables, kennels, training yards/fences).
  - NW: **Artisan** (Forge/blacksmith, Armorer, Lumbermill, Mill, Resource Upgrade, Jeweler stall — workshop cluster).
- **NPCs & Upgrades**: Stationed NPCs (rigged chars from packs) at key buildings in districts. Interact opens code-built UI (reuse BuildPreviewModal style). Upgrade via Economy (TrySpend ResourceCost scaled by tier) → visual animation/transform to higher-tier version (use/extend StructureTierVisual or prefab swap + particles/scale) + productivity boost (e.g. periodic Economy.Grant of associated resource type, or global bonuses).
- **Automated Defenses**: Placeable on ramparts/walls (new AutoRampartDefense components in builder). Activate on enemy approach (scan for IDamageable enemies, fire using existing projectile/Enemy patterns). Heavy at gates/turrets for "Last Bastion" feel. Integrate with WaveManager threat.

**2. How Regions Connect to Castle:**
- Castle gates open to immediate "transition outposts" or paths in a near "Inner Wilds" zone (part of OuterWorld or additive "GateTransition" scene).
- Progression gates/outposts in regions must be claimed/defended (reuse/extend ClaimableCamp/OutpostHub/HarvestSite with local build grids + troop recruitment).
- Smooth transitions: Use additive scene loading (WorldSceneLoader or new RegionLoader) for performance — load region on crossing gate/outpost, unload far ones. Or large streaming world with culling/ZoneManager extension. No full reloads.
- Rewards flow back: Regional yields/outpost passive income → Castle Economy (boosts upgrades, automated defenses).

**3. Elemental Region Ideas (4-6 core, extendable; match/extend current 4 anchors: Goldfields/Stoneback/Mirewood/Ashwood + new):**
- **Verdant Forest (East, low-med danger)**: Lush trees, plant/wood bonuses, wood-rich nodes, beast/plant enemy families. Rewards: Wood/Food, basic pets.
- **Frost Peaks / Frozen (North, med-high)**: Snow/ice, crystal nodes (high AetherCrystal), ice golems/frozen enemies. Rewards: Rare crystals, gear, new ice pets.
- **Stone Mountains / Highlands (West, med)**: Rocky, ore/iron rich, troll/stonebelly families (reuse prior). Rewards: Iron/Stone, defensive gear.
- **Ashen Wastes / Desert or Volcanic (South, high danger)**: Harsh, fire/volcanic themes, rare gems, stronger new families. Rewards: High yields, Jeweler materials.
- **Further/Optional**: Swamp (poison, unique resources), Deep Wilds. Each has outposts/harvest sites needing defense (pet + troop), stronger spawns, better Economy grants (scaled by distance/danger).

**4. Build & Defense Systems (mobile, reuse):**
- Resource nodes: Visual harvest structures (HarvestSite), pet assignment/harvesting with floating +X text, all via Economy.AddResource.
- Outposts in regions: Small build grids (extend BuildMode/OutpostHub), recruit AI troops (Tank/DPS/Healer via Economy costs + upkeep), automated defense.
- Castle hub: Ultimate with rampart auto-defenses + stationed troops.
- Placement: Ghost preview always + BuildPreviewModal (90° + drag rotation, yawOffset save) for everything.
- Economy as single source: Productivity from upgrades, regional passive, node/outpost yields, defense costs all through it. No dupes.

**Technical (Mobile First):**
- Additive scenes or smart streaming (load small region chunks on demand via gate/outpost triggers; reuse WorldSceneLoader).
- Builder-only for Castle (idempotent, reflection for components).
- Reuse: BuildModeController/Ghost/BuildPreviewModal (rotation), EconomyService (all resources), HarvestSite/OutpostHub/ClaimableCamp (extend), EnemyBrain/Factory (new families), ActorAnimator, etc.
- Perf: _M low-poly, batch, distance culling, consistent scale.
- Progression hook: Claiming/securing regions increases Castle "security level" (unlocks better auto-defenses or Economy multipliers).

**Scope for this WO (step-by-step start with Castle):**
- Prioritize Castle Town fixes/improvements (walls/ramparts/turrets/gates/districts/NPCs/upgrades/automated defenses) via builder edits.
- Hook Economy everywhere.
- Basic region extension (extend OuterWorldBuilder + connection logic from gates; elemental placeholders).
- Full regions/content in follow-ups.
- Update all indices/READMEs.
- Brace gate on every .cs. No .unity edits. One builder touch at a time.

**Files to Create/Modify (see list in final response):**
- New WORK_ORDER + RESULT.
- Editor builder partials (main, Walls, Fortify, Content, etc.).
- New/edited runtime: Auto defense components, region loader/manager, extensions to Harvest/Outpost.
- Economy if gaps.
- BuildMode if polish needed (reuse).
- Docs/indices.

**Acceptance:**
- Rebuild Village via builder → epic enclosed castle with all features, no overlaps, consistent scale, functional ramparts/gates/NPCs/upgrades/auto-defenses.
- Economy is sole handler for all new systems.
- Regions connect (basic streaming/additive or anchors), elemental themes started, progression feel.
- Mobile perf notes, reuse existing.
- All gates passed, indices updated.

Owner (Samantha) final creative calls on exact prefabs/distances/themes. Increment WO. Parallel lanes ok (builder vs runtime vs art).

This builds on prior (WO-106 pet/outpost/economy, build modal, animations) without greenfield.