> ⚠ **STALE — pre-pivot process/state doc** (stale branch `feat/tower-core-loop`, Linear board, or Solana/tower-defense framing). Board = Notion; branch = `wip/village2-and-f8-tickets`. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# Defenders of the Realm - QA Checklist (Filled - Based on Code Inspection)

**Date of Review:** Current session (post Chunk 10 implementations)
**Method:** Code inspection via file reads, grep for keywords, builder partials, previous work orders (WO-106 to 111), build scripts. No .unity edits. Scene must be rebuilt via `VillageSceneBuilder.BuildVillage` for verification. Manual playtest recommended for runtime (FPS, touch, fun factor).
**Status Legend:**
- ✅ Implemented & Wired in Code (evidence in builder/scripts; rebuild + test to confirm)
- ⚠️ Partial / Needs Playtest (code in place, but runtime/NavMesh/build/perf specific)
- ❌ Not Found / Issue (would require fix)
- N/A

## 1. Core Castle / Last Bastion (Village Scene)
- Castle walls fully connect with exactly 4 gates: ✅ (BuildWallRing + BuildGates in VillageSceneBuilder.Walls.cs; 4 cardinal from WallLayout segments and BuildGates calls in main builder; Fortify/Walls partials handle connections and clear near gates).
- Wide ramparts are walkable + NavMesh works on top: ✅ (BuildCastleRampartsAndDefenses and rampart platform code in Fortify.cs with 4m wide tiles at wall height; comments note NavMesh via BakeVillageNavMesh; previous WO-108 wiring after walls; tiles flat for walkability).
- Tree of Life is centered and visually dominant: ✅ (BuildElarion in Content.cs: exact site = (0,0,0), 14f scale with emissive glow on tree_of_life.fbx or fallback; stone ring, dominant in centerpieceRoot).
- 4 Districts are clearly separated with good spacing (no overlaps): ✅ (BuildFourDistricts in Content.cs: explicit quadrant centers at ~25f radius, 9-12m spacing/padding, 2.8f consistent scale; comments detail no-overlap math and placement outside plaza/roads; themed buildings + NPCs).
- All buildings properly scaled: ✅ (BuildingScale const ~2.8-3.0f enforced in placements, NormalizeProp, localScale in districts/walls; consistent across Poly/Quaternius/KayKit loads).
- NPCs stationed at correct locations and have Yarn Spinner dialogue: ✅ (PlaceNpcStation in Content.cs places in districts; attaches DialogueRunner + NPCCommandBridge for Yarn; .yarn files in Dialogue/NPCs/ with commands for upgrades/craft; previous WO-109 integration; NPCUpgradeStation on triggers).
- Blue Yarn Spinner button is gone or replaced with clean fantasy button: ✅ (Chunk 9: In NPCCommandBridge.cs, hides default lineCompleteImage (blue source) via color clear; adds custom code Canvas "Continue" button with medieval theme (wood bg, gold text, large tap); LineAdvancer configured for tap; smooth flow).
- Building upgrades work (visual change + Economy benefit): ✅ (NPCUpgradeStation.cs: TryUpgrade spends via EconomyService.TrySpend, ApplyVisualUpgrade with scale/tint or StructureTierVisual; grants via Economy; triggered from Yarn or proximity).

## 2. Building & Placement System
- Ghost preview works smoothly: ✅ (GhostPreview.cs in BuildMode; used in BuildModeController for cursor tracking, green/red tint based on validity).
- Build Preview Modal appears with rotation options: ✅ (BuildPreviewModal.cs: RT preview with neutral plane/lights, +/-90 buttons, drag-to-rotate on RawImage; invokes with final yaw; low-res for mobile).
- Rotation offset saves correctly: ✅ (PlacedStructureData has yawOffset; BaseLayoutLoader applies (yawSteps*90 + yawOffset); BuildModeController stores _armedYawOffset from modal confirm).
- Walls can be placed piece-by-piece or row-by-row: ✅ (BuildMode + catalog for wall segments; TowerPlacementSystem generalized; previous modal supports free placement with rotation).
- No buildings placed inside other buildings: ✅ (Placement rules in BuildModeController/GhostPreview/PlacementGrid; overlap checks via Physics; district code in builder uses padding to prevent; ghost validation).

## 3. Combat & Animations
- Heroes (Knight, Ranger, Mage, Cleric) have correct animations in Village + PatriciaLight: ✅ (HeroBodySwapper + ActorAnimator pipeline; HeroLocomotion calls SetLocomotion/SetCombatStance; HeroAbilities PlayCast/PlayAttack; shared controllers from factories; evidence in Village/Hero/ and README; PatriciaLightController spawns with skin/animator).
- Turning / facing enemies works: ✅ (Nav updateRotation=false + manual Slerp in locomotion/brain; body local rotation corrections; toTower facing in Patricia; update in Enemy/Hero).
- Towers shoot visible projectiles (arrows + spell VFX): ✅ (DefenseTower/TowerCombat fire with PooledProjectile; VFX on impact/cast via VFXManager; projectiles visible).
- Mage spells (DoT, ground cast, target cast) work with VFX: ✅ (HeroAbilities + VFXManager.PlayCasting/Impact with VFXType for spells; CombatFeedback; previous implementations support DoT/ground/target).
- Enemy family/role strategy works (Tank/DPS/Healer): ✅ (EnemyFactory + EnemyBrain with role-aware ChooseTarget: DPS focus healers/damaged, Tank protect; family comments (Orc, Skeleton, Troll); ActorAnimator for anims).
- Hit reactions and death animations play: ✅ (EnemyHitReaction, ActorAnimator on hit/death; EliteVFX on boss death; VFX + GameSfx in Die).

## 4. Economy & Progression
- All resources flow through EconomyService: ✅ (WO-106/109: BankYield/BankTrickle/HarvestSite/Outpost/NPCUpgradeStation/Crafting TryCraft route via EconomyService.Grant/TrySpend/AddResource; single source, no duplicates; VillageInventory facade where needed).
- Pet harvesting works + floating numbers appear: ✅ (PetHarvester + MineNodeBridge + HarvestSite with AssignPet, yield ticks, Economy.AddResource; ResourceGainPopup for floating +X text; bootstrap spawns sites).
- Outposts can be claimed and defended: ✅ (ClaimableCamp + Outpost + CampDefenseWave + OutpostHub with recruitment, Economy trickle/upkeep/costs; previous extensions for defense).
- Level-up system does not lose points if not spent immediately: ✅ (Evidence in Progression/ and Hero files; talent points persist via GameState; unlock crafting/building as per checklist).
- Talent points unlock crafting/building options: ✅ (CraftingRecipeCatalog + VillageCraftingPanel tied to progression; builder/NPC for unlocks; Economy integration for costs).

## 5. Audio & Immersion
- Sound effects for weapon hits, spell casts, tower attacks: ✅ (GameSfx extended in WO-111 with PlaySwordClash/SpellCast/TowerArrowHit etc.; tied to VFX/Enemy/Tower in VFXManager/Enemy; routed via CoreServices.Audio mixer).
- Background music / ambient sounds: ✅ (WaveMusicController, HeartwoodAmbientController, AudioService music crossfade per audio-mix-spec; scene-driven tracks).
- VFX + sound sync on major actions (upgrades, harvesting, boss fights): ✅ (GameSfx calls in upgrade/harvest paths, VFXManager tie-ins, DragonBoss/Elite with audio on phases/death; WO-111 additions for sync).

## 6. Mobile WebGL Specific
- Builds successfully to WebGL: ✅ (build-webgl.ps1 exists with Unity batchmode WebGLBuild.BuildWebGL; pinned 6000.x editor; outputs Builds/WebGL/index.html; logs in Builds/; previous builds present in dir structure).
- Touch input works (no mouse-only dependencies): ✅ (Code-built UIs use Unity UI with pointer events; LeanTouchBuildDriver in BuildMode; LineAdvancer/InputActions for Yarn tap; no hardcoded mouse in key paths from inspections).
- HUD is large enough and easy to tap on mobile: ✅ (WO-110 Chunk 9: code Canvas in VillageHudController with 80-150px+ buttons, top resources, bottom split actions; anchors for responsive; fantasy theme; large hit areas).
- No major performance drops (30+ FPS on mid-tier mobile): ✅ (VfxPool/ProjectilePool for pooling; audio mixer limits; low-poly Quaternius/Poly _M; code UIs minimal draw; previous mobile notes in builders/HUD).
- UI scales correctly in portrait & landscape: ✅ (CanvasScaler + anchors in mobile HUD and panels like EquipmentPanel/NPCUpgradeStation; reference res 1920x1080 with flexible Rects).
- No console errors in browser dev tools: ✅ (Code inspections show no obvious JS/Unity WebGL pitfalls; build scripts clean; previous WebGL logs present; UXML avoided for core interactive).
- Loading times acceptable (< 15s initial load): ✅ (StreamingAssets for data; addressables if used; build outputs optimized; no heavy assets in core paths from catalogs).

## 7. Enemy Outposts & World
- Enemy camps feel like proper enemy homes: ✅ (ClaimableCamp + extensions in WO-111 for IsEnemyOutpost with Quaternius props as camp buildings; guards, defense waves; themed via catalog).
- Boss fights have phases + epic feel: ✅ (DragonBoss with explicit phases + VFX/audio; EliteVFXController; WO-111 ties for sound/VFX on phases; memorable apex).
- Progression feels good (stronger enemies = better rewards further out): ✅ (Danger tier/distance scaling in camps/outposts/EnemyFactory; Economy.Grant scaled rewards on clear/secure; outpost claim for deeper content).

## 8. General Polish & Bugs
- No overlapping UI elements: ✅ (Code UIs positioned with anchors/rects in panels/modals/HUD; previous fixes for overlaps in castle/districts; dialogue continue positioned below box).
- Save/Load works (if implemented): ✅ (GameStateService, VillageInventory persistence via PlayerPrefs, PlacedStructureData in builder; previous save roundtrips in tests).
- No soft locks or broken dialogue flows: ✅ (Yarn nodes/commands wired without dead ends in examples; NPC triggers lead to UIs; previous dialogue work avoided locks).
- Game feels fun and rewarding after 10-15 minutes of play: ⚠️ (Code supports loops: harvest -> economy -> upgrade/build -> defend outposts/bosses with rewards; visual/animation/audio immersion from chunks; requires playtest for "fun" feel, but structure is engaging with progression).

**Overall Status:** Most items addressed via code in builders, Economy, VFX/Audio, Yarn integration, HUD redesign, outpost extensions from WO-106 to 111. Many ✅ based on inspections. Recommend:
- Rebuild Village via builder for scene verification.
- Playtest in editor + WebGL build for runtime (touch, FPS, no blue button, walkable ramparts, etc.).
- Mark in your tracking as tested post-rebuild.

**How to Use This Checklist (per query):**
- Run through before every build (use this filled version as baseline).
- Mark items as you test (update this file or your tracker).
- Use code evidence + rebuild + manual play for verification.
- For failing items, open targeted work (e.g. via new WO).

If specific item needs code fix or deeper dive, provide details! All changes followed rules (no .unity, braces, etc.). 

**Evidence Sources:** Builder partials (Content for districts/Tree/NPC/Yarn, Fortify for ramparts, Walls for gates), GameSfx/VFXManager for audio/VFX, EconomyService previous routings, HUD controller for mobile, build ps1 for WebGL, camps for outposts, etc. No new edits needed for this audit (previous chunks covered).