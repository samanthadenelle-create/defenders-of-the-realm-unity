> ⚠ **NUMBER COLLISION — this document does not own WO-107; `WORK_ORDER_107_climate_regions_terrain.md` does.**
> Referred to hereafter as **WO-107-B (village/castle overhaul)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK_ORDER_107 — Major Village Castle Overhaul: Dream Defensible Medieval Castle Town (Elarion)

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: dream fixed-layout castle vs player-built town)

**Owner:** (this session)  
**Related:** Previous BuildMode work (BuildPreviewModal, yawOffset, Ghost), EconomyService (for upgrades), VillageSceneBuilder (serialization bottleneck — touch carefully), DESIGN-DECISIONS (no Keep, Elarion Tree of Life center, canon naming).

## Core Vision (from user query)
A beautiful, cohesive, defensible medieval fantasy **Castle Town** as the player's last bastion around the glowing central **Tree of Life** (Heart of Elarion). If the castle falls, game over.

Fix current issues: overlapping buildings, inconsistent scaling, random placement, lack of proper walls/ramparts.

**Non-negotiable per Claude.md:**
- NEVER hand-edit `Village.unity`. All layout via `DeNelle.Editor.VillageSceneBuilder.BuildVillage` (batchmode or menu).
- Builder is the serialization bottleneck — one agent/branch at a time.
- Use `_M` tier prefabs from Polyperfect (gitignored — reimport + fix materials), Quaternius Medieval, KayKit (current base).
- Update indices/READMEs when adding files.
- After any .cs edit: run exact python brace check before reporting done.
- Village → Core only. Use `EconomyService` (the Economy class) for all resource/upgrade costs/income (Grant/TrySpend/AddResource from prior WO-106).
- Code-built UIs (no UXML in builds).
- Null-conditional `?.` on cross services.
- Reconcile, don't duplicate (build on existing BuildModeController, GhostPreview, BuildPreviewModal, PlacedStructureData with yaw, StructureTierVisual, EconomyService, builder partials).

## High-Level Castle Layout Plan (proposed)
**Center**: Tree of Life (exact (0,0,0), dominant 12-15m scale using `Assets/Resources/Structures/tree_of_life.fbx` with emissive violet glow + mound + stone ring). Central paved plaza.

**Outer Defenses (fully enclosing)**:
- Strong exterior **Castle Walls**: Large rectangle ~90m E-W x 70m N-S (or slight bow per old spec), ~7-8m tall stone base + battlements. Modular from Polyperfect `Wall_Stone_3x3_*` + corners + Quaternius/KayKit supplements for cohesion. Full connected loop.
- **Corner Turrets**: 4 taller (12m) round/square towers at corners with roofs, platforms, arrow slits. Place using catalog towers (e.g. Tower_Castle_* _M).
- **Wide Ramparts**: On top of walls — continuous 4m+ wide walkable platform (stone floor tiles + low crenellations on outer edge). Full loop. Hero + defensive units/towers placeable on top. NavMesh surface.
- **Access**: Stairs or ramps (modular steps) at ~8 points (near gates + mid-wall segments) for rampart access.
- **Exactly four gates/openings** (cardinal only): N/S/E/W. Each a gatehouse (piers + arched opening or double doors using CastleGate assets + KayKit gate pieces). ~8-10m clear opening. Small gatehouse buildings flanking. WaveSpawnPoint 12-15m outside each, aligned.

**Interior Division (4 Clear Districts around Tree, separated by cross roads from gates to plaza)**:
- **Roads/Plaza**: Wide paved cross (N-S + E-W spines) using KayKit hex_road or Quaternius stone floors. Central plaza ~15-20m radius around Tree.
- **NE Quadrant — Commerce District**: Market square, 4-6 shop/market stalls, tavern/inn (Quaternius shop modules, KayKit neutral buildings). NPC: Commerce/Resource NPC near main market.
- **SE Quadrant — Housing District**: Clustered 6-8 homes/cottages/inns (residential KayKit/Quaternius houses). Paths between. NPC: Innkeeper/Housing rep.
- **SW Quadrant — Pet District**: Stables (barn), 3-4 pet homes/kennels, fenced training yard (use barn props, fences from packs + Poly pet-related if any). NPC: Stablemaster/Pet Trainer.
- **NW Quadrant — Artisan/Upgrade District**: Workshops cluster — Forge (blacksmith/anvil), Armorer, Lumbermill (saw), Mill (grain/food), Resource Upgrade building, Jeweler stall (placeholder for crystal→gem). NPC stations at each key building.

**NPC Stations & Upgrades**:
- 6-7 stationed NPCs (rigged low-poly characters from Quaternius/KayKit/People packs, placed at building fronts, small idle area).
- Key NPCs: Mill/Food, Armorer, Forge/Weapons, Lumbermill, Resource Upgrade (central), Jeweler.
- Interact (proximity trigger or "Talk" action): Opens code-built upgrade modal/panel for that building (tiers 1-3, shows current benefits, cost as ResourceCost from Economy).
- On upgrade: Economy.TrySpend(cost) → visual transform (higher-tier prefab swap or StructureTierVisual extension with "build up" animation/scale/particles) → register productivity bonus (e.g. periodic small Economy.Grant of associated resource, or global cost reduction). Ties directly to existing Economy class.

**Building Placement Rules (core + player additions)**:
- All core baked structures: consistent scale (enforce ~2.8f BuildingScale), min 6-8m spacing/padding, quadrant grid or offset math to eliminate overlaps/randomness.
- Player additions (inside walls or designated zones): Always ghost preview (existing GhostPreview). On arm: show BuildPreviewModal (RT + neutral plane + lights) with 90° buttons + free drag-to-rotate on image. Confirm passes final yaw (yawSteps*90 + offset) to place. Save offset in PlacedStructureData (already supported). Use existing BaseLayoutLoader / StructureFactory.

**Assets**:
- Polyperfect Low Poly Ultimate _M tier heavily for walls/towers/floors (modular stone).
- Quaternius Medieval Village MegaKit for cohesive buildings, props, URP-native.
- KayKit Medieval Hexagon (current base for roads/tiles) + any medieval for gates/details.
- Existing: tree_of_life.fbx (center), CastleGate assets.
- Fallbacks with LogWarning (never error) per rules. Check catalogs before naming prefabs.

**Technical/Polish**:
- Update VillageSceneBuilder (partial split: Walls.cs, Fortify.cs for defenses; Content.cs for districts/Tree/NPCs; possibly new or inline Castle.cs logic).
- Builder remains idempotent (clear VillageRoot, rebuild).
- Integrate upgrades with EconomyService (source of truth — no duplicate logic).
- Ramparts/walls gameplay: WallSegment/Gate components via reflection (existing).
- NavMesh update for ramparts/paths.
- Lighting: Dominant Tree glow + wall torches + global.
- Mobile-perf: Low poly, batching, combine where sensible, no high-res Tripo bloat.
- Overworld seam: Walls define the edge for exterior builder.
- Build on prior: BuildPreviewModal (rotation), Economy (WO-106), StructureTierVisual, Ghost.

**Acceptance Criteria**:
- Rebuild Village via builder → beautiful enclosed castle town, no overlaps, consistent scale, proper connected walls + wide walkable ramparts + 4 turrets + 4 cardinal gates + stairs.
- Tree dominant at exact center.
- 4 distinct districts with correct themed buildings + stationed NPCs.
- Talk to NPC → upgrade UI → spend via Economy → visual upgrade anim + benefit.
- Inside: Build mode uses ghost + modal preview + rotation (90+drag) + saved offset.
- Braces balanced on all edited .cs.
- No .unity hand-edits. Builder only.
- References only approved catalog prefabs (with fallbacks).
- "Wow factor" defensible last-bastion feel.

## Files to Create or Modify (list + summary)
**New**:
- `WORK_ORDER_107_village_castle_overhaul.md` (this) + `.RESULT.md` (on complete).
- `Assets/_Modules/Village/Buildings/NPCUpgradeStation.cs` (or similar name; stationed NPC + upgrade UI + Economy integration + visual trigger).
- Possibly `Assets/_Modules/Village/Buildings/StructureUpgradeVisual.cs` (extends StructureTierVisual for animation on upgrade).

**Modify (key)**:
- `Assets/Editor/VillageSceneBuilder.cs` (orchestration, constants, scale enforcement).
- `Assets/Editor/VillageSceneBuilder.Walls.cs` — major: full connected walls, 4 cardinal gates with gatehouses, corner turrets placement, rampart base.
- `Assets/Editor/VillageSceneBuilder.Fortify.cs` — ramparts (wide walkable top layer + battlements + stairs), moat polish if kept, defenses.
- `Assets/Editor/VillageSceneBuilder.Content.cs` — Tree of Life dominant placement + mound, 4 quadrant districts with specific building clusters (Commerce/ Housing/ Pet/ Artisan), NPC character placement at stations, road/plaza refinement.
- `Assets/Editor/VillageSceneBuilder.Characters.cs` — if needed for NPC rigging/positioning.
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` + `BuildPreviewModal.cs` + `GhostPreview.cs` (polish integration, ensure modal always shown on arm for castle pieces, consistent scale in preview, rotation feedback).
- `Assets/_Modules/Village/BuildMode/BaseLayoutLoader.cs` + `PlacedStructureData.cs` (if any yaw tweaks needed; already has support).
- `Assets/_Modules/Village/EconomyService.cs` (minor if new production hooks needed; primarily consume existing Grant/TrySpend).
- `Assets/_Modules/Village/README.md` + module indices (update for new station/upgrade files).
- `docs/polyperfect-asset-catalog.md` or `kaykit...` if new specific refs added (add entries).
- Possibly `Assets/Editor/VillageSceneBuilder.Helpers.cs` for new load/place helpers.

**What NOT to touch**:
- `Village.unity` (or any .unity) — builder only.
- No direct HUD refs.
- No new parallel economy (use existing EconomyService).
- Avoid touching Village2 generator unless explicitly for seam.
- Keep builder reflection for runtime components.

**Dependencies / Order**:
- Builder changes first (layout, scale fix, walls/districts/Tree/NPCs baked).
- Then runtime upgrade station (Economy calls, visual swap).
- Polish BuildMode last (leverages existing modal from prior).
- Test: Run builder (batch or menu), play Village, interact NPCs, enter build mode inside.

**Tuning knobs**: Scales, district offsets, wall height, rampart width, upgrade costs (in WO or data), NPC interact radius.

Increment WO number. Owner makes final creative/asset calls.

This delivers the "dream defensible medieval castle town" with verticality, districts, upgrades, and solid build UX while obeying all project rules.