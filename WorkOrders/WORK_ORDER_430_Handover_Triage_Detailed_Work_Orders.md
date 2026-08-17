<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_430 — Handover Triage: Detailed Work Orders Consolidation (Open Tickets 363–437 + P0 Gates)

**Status:** READY TO IMPLEMENT  
**Authority:** CLI_LANES_WO_NUMBERS.md (reconciled 2026-06-12) + docs/MASTER_CATALOG/docs-wo-state.md. Next free WO = 430. This file supplies the missing detailed specs for the enumerated handover list (most previously only in Notion rows). Slot new numbers from 430+ into the lanes documented in CLI_LANES (do not mint from filesystem max; collisions 328–339 etc. already noted — do not reuse).  
**Branch context:** feat/tower-core-loop (git status at session start: 1 commit ahead, clean).  
**Date synthesized:** 2026-06-13 from user-provided handover enumeration + verified MASTER_CATALOG / ARCHITECTURE_PRINCIPLES / PIPELINE_STATE / area catalogs (hud, battle-atb, village-hero, editor-tools, scenes, dialogue, core, etc.).  
**Decision lens (ARCHITECTURE_PRINCIPLES):** player-felt + holistic leverage, bounded context, presentation NEVER touches objects, pool by default, One Model (entries+capabilities), tests gate structural, owner playtest (not green gate) is the only verdict. Never patch-and-claim-fixed.

## 0. Global Rules (NON-NEGOTIABLE — every implementer obeys)
- Read (and obey) before touch: docs/MASTER_CATALOG.md (full), relevant docs/MASTER_CATALOG/<area>.md, docs/ARCHITECTURE_PRINCIPLES.md, PROJECT_INDEX.md, Assets/_Modules/README.md, Assets/README.md, docs/README.md, PIPELINE_STATE.md, CLI_LANES_WO_NUMBERS.md.
- Canon (from PIPELINE_STATE + MASTER_CATALOG + DESIGN-DECISIONS): Home hub = `MainCastle_Hall` (CastleHubBuilder sole regen authority; owner hand-dialed offsets committed — regen reverts). Village2 = canonical raid/TD target. `Village.unity` = ABANDONED, corruption-cursed — NEVER hand-edit or re-save. DTT/PatriciaLight pillar = REMOVED 2026-06-09 (module + scene gone; only `Resources/PatriciaLight/tower2` kept). OuterWorld additive over hubs via WorldSceneLoader + SceneTransitionTrigger + HubScenes. Elarion (not Avalon). Heart of Elarion at (0,0,0).
- Assembly (CLAUDE.md §5 + MASTER_CATALOG): Village → Core only. HUD → Core only. Never Village ↔ HUD. All cross via `CoreServices.Hud` (IVillageHud) + `CoreServices.Audio` (IAudioService) with `?.` null-conditional. Editor uses reflection + FindType only (no Village ref in DeNelle.Editor.asmdef). BattleATB Engine = pure C# (no UnityEngine except optional unused SOs).
- HUD law: 100% code-built uGUI (Canvas + CanvasScaler ScaleWithScreenSize ref 1080x1920/1920x1080 + 0.5 match + GraphicRaycaster + RectTransform manual anchors + Image + TextMeshProUGUI + Button). NO UXML/UIDocument/VisualElement at runtime in player builds (UXML trap learned the hard way). Lean Touch exclusive for all mobile input (no raw `Input.Get*`).
- Builders: CastleHubBuilder / VillageSceneBuilder = serialization bottlenecks. ONE agent at a time. Idempotent root-clear + rebuild. Use polyperfect `_M` + Quaternius only (catalogs in docs/*-catalog.md). After any edit that touches scene-affecting, run CompileGate + RegressionSuite.
- C# Quality Gate (CLAUDE.md §1, binding): After editing **any** `.cs`, immediately run the exact brace python on that file and fix before reporting/claiming. Never ship mismatch.
- Work order completion: CLI writes `WORK_ORDER_430_....RESULT.md` with commit hash + acceptance checkboxes + playtest notes. UI/owner marks Notion. Push only after owner retest (felt) or regression green.
- Mobile/WebGL first: CanvasScaler, Lean, low-poly _M, LODs, low draw calls, no per-frame FindObjectsByType (use registries), Resources-first for WebGL (CanonicalJson dual copy rule).
- No greenfield: Reuse EconomyService (single source for Wood/Iron/Food/Crystals + Grant/TrySpend), Yarn (single DialogueService + DialogueCommandBridge ~40 verbs; SignalContentComplete pattern for blue-dot), ActorAnimator/IActorAnimator (PlayAttack/PlayCast/PlayHit/Die via hashes), HeroBodySwapper/VisualFactory/GearLoadout, BuildModeController, WaveManager, HeartController (IDamageableStructure), existing ATB Engine (Types/Defs/BattleState with ActiveUnitId/Units/Side.Party/Resource, BattleAction.Make*, BattleLogEvent.Attack/Ability), VfxPool/ProjectilePool.
- Presentation separation (ARCHITECTURE_PRINCIPLES §2): Objects expose state only. HUD/Combat feedback/VFX never live on the gameplay object.
- One Model + pooling: Express new world content as entry + composable capabilities read by single-owner systems. Pool spawns (VfxPool precedent; audit Instantiate sites).
- Tests + Regression: Structural/holistic changes require EditMode/PlayMode tests that lock prior behavior (permission gate). All WOs in this file must pass CompileGate + relevant RegressionSuite cases + owner playtest.
- DO NOT: Hand-edit .unity (use builders), smuggle holistic refactors into player-facing tickets, use System.Reflection new in bridge scripts, bypass PanelManager for modals, leave NRE spam or GC alloc/frame hot paths.

## 1. P0 Hard Deployment Gates (Lane 0 — verify/build now; highest priority)
These block shipping / WebGL / regression gates per list + MASTER_CATALOG risk ledger (orientation validation, trees/magenta, battle HUD empty, floor/collision, WebGL Yarn crash, title hitboxes, missing gate/compass, hero select breakage, NRE spam, mine dead, hero orientation/facing, castle perf/seam/camera).

### WO-363: Character Orientation Validation — Hard Deployment Gate
**Lane:** 0 (explicit in CLI_LANES)  
**Priority:** P0 — hard gate before any build shipping.  
**Context (MASTER_CATALOG village-hero + editor-tools + scenes + risk §1/5a/21):** HeroLocomotion comment LIES ("pure transform, no NavMeshAgent"); real code = NavMeshAgent + `_agent.Move(step)` + manual LookRotation + camera-relative basis from SmartMobileCamera.CameraYaw (WO-387). Hero walks north but model/anim 90° right (WO-326). Enemies walk backwards (WO-315). Hero attacks without facing target (WO-423 — rotate-to-target + turn blend). DTT aim-north + head-pivot (318/317 — DTT removed but similar camera/anim issues recur). Idle pose on scene load + dialogue missing (WO-376). Castle seam warp + camera fights geometry (383/385). RegressionSuite source-greps the stale HeroLocomotion comment for camera-yaw gate — dangerous.  
**Files to edit (minimal, targeted):**
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs` (fix facing math, document the NavMeshAgent truth in header, expose yaw cleanly, WarpTo preserves facing).
- `Assets/_Modules/Village/Hero/PlayerAttackController.cs` (or HeroAbilities) — rotate root to target before swing/cast, blend turn anim via ActorAnimator.
- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` + `HeroPoseController.cs` (DriveIdlePose on load + during Yarn via DialogueService hooks; resilient single-flight).
- `Assets/_Modules/Core/Combat/ActorAnimator.cs` + `IActorAnimator.cs` + `AnimParams.cs` (ensure PlayAttack/PlayCast accept optional face target; turn layer or root yaw).
- `Assets/_Modules/Village/Camera/SmartMobileCamera.cs` (confirm CameraYaw is sole authority for 3rd-person follow; no conflicting yaw sources).
- `Assets/Editor/RegressionSuite.cs` (add/strengthen "Character Orientation Validation" case + camera-yaw-is-authority; source-grep guard must match actual code post-fix).
- `Assets/Editor/HeroAnimatorFactory.cs` (if controller params/layers need turn blend).
- `Assets/Editor/CastleHubBuilder.cs` (if seam/gate facing needs marker tweak).
- Related: Any EnemyBrain facing (for 315), Pet.cs (also self-adds NavMeshAgent).
**Acceptance criteria (line-by-line review required):**
- [ ] Brace balance python passes on every `.cs` touched (`python3 -c "..."` exact from CLAUDE.md).
- [ ] CompileGate clean (`COMPILE_GATE_OK`).
- [ ] RegressionSuite Critical Gates + new orientation case = REGRESSION_OK (includes "hero faces move dir in follow + top-down", "attack rotates root before anim", "idle on load/dialogue", "seam warp no spin", "no source-grep fooled by comment").
- [ ] In 3rd-person follow: forward = camera yaw + input; model/anim matches (no 90° right on north walk).
- [ ] Attack (melee or cast): hero turns to CurrentTarget (or aim point) before PlayAttack/PlayCast; turn anim blends, no snap or moonwalk.
- [ ] On any scene load (MainCastle_Hall, Village2, OuterWorld, ATBBattle return) + during any Yarn dialogue: hero in canonical idle (ActorAnimator or controller "Idle" state, weapon sheathed per pose rules, no T-pose/walk cycle).
- [ ] Castle ↔ OuterWorld seam: hero exits facing outward, no 180° flip or stuck.
- [ ] Enemies (open world + garrison + breach): face locomotion direction (no backwards moonwalk).
- [ ] Mobile (Lean + VirtualJoystick) + desktop (WASD) + gamepad parity.
- [ ] Zero new NREs or alloc/frame in hot path. Owner playtest (mobile portrait + landscape + desktop) confirms "character orientation feels correct at every exit/attack/load/dialogue" — this is the verdict, not the gate.
- [ ] Update MASTER_CATALOG/village-hero.md + editor-tools.md + any stale headers.
- [ ] No direct Village refs from HUD/BattleATB; all cross use `?.` + CoreServices or reflection-by-name.
**Do NOT touch:**
- `Village.unity` (abandoned).
- Any UXML/UIDocument path for HUD or battle (code-built only).
- DTT/PatriciaLight code paths (pillar removed; only keep the one tower2 resource).
- Raw Input.GetKey/Mouse (Lean + existing VirtualJoystick only).
- Green-field new locomotion or animation rig (reconcile to existing NavMeshAgent + ActorAnimator + HeroBodySwapper).
- CastleHubBuilder offsets that would revert owner hand-dialed MainCastle_Hall (use additive only if needed).

### WO-373: CRITICAL Regression Gates — Hard Blockers Before Build Shipping
**Lane:** 0 (explicit).  
**Context:** Extends 363. Covers tree-of-life-origin (Heart at 0,0,0), WASD/camera-relative, scene-loads-clean, camera-yaw authority, compile, catalog parity (Resources vs StreamingAssets byte-equal), no-duplicate-landmines, perf-lint. Many open tickets (orientation, trees white, battle HUD, floor/collision, WebGL crash, hero select, NRE, mine, missing gate, compass, castle seam/perf) must pass these before any ship.  
**Files:** `Assets/Editor/RegressionSuite.cs` (add cases or strengthen for every P0 in this list: orientation, trees/magenta render, BattleHudUgui non-empty on load, floor collision at key points, Yarn load no crash, hero select portraits/stats fit, no NRE spam on title/hero/castle/village load, mine harvest adds to Economy + HUD, gate/compass visible, MainCastle 0.1fps GC not regressing, seam warp succeeds). Update gates that source-grep (HeroLocomotion comment trap).  
**Acceptance:** All gates PASS (REGRESSION_OK); every ticket in user list has a regression case or is covered by existing; owner signs off post-fix playthrough of title→hero→pet→castle→outer→village2 breach. Brace + compile on touched files.  
**Do NOT:** Alter non-regression code in same change; touch abandoned Village.unity; bypass the gate for "fast".

### WO-332 / WO-323 / WO-409: Trees Render All White (Missing Material/Shader) + Magenta Towers + UI Sprite Glyphs
**Lane:** 0 + 1 (world) + 10 (perf).  
**Context (MASTER_CATALOG risk §4 + editor-tools + resources-art):** Quaternius + polyperfect _M prefabs render white/magenta after URP (missing _BaseMap/_BaseColor or Standard shader). UI sprite refs render as text glyphs ("*" or "#"). WebGL 223 MB (WO-408 scripts exist but NOT run). 409 also covers tower structures. Polyperfect requires `Defenders/Art/Fix Polyperfect URP Materials` on fresh clone.  
**Files:** `Assets/Editor/MagentaMaterialFixer.cs` + `MagentaMaterialScanner.cs` (broaden or add tree/tower specific passes), `Assets/Editor/CastleHubBuilder.cs` (ensure URP mats on load for Quaternius walls/towers), `Assets/Editor/VillageSceneBuilder.*.cs` (partials — one agent), polyperfect/Quaternius material folders if needed, `Assets/_Modules/HUD/VillageHudController.cs` + BattleHudUgui (fix any sprite refs that fell back to glyphs; use RpgUiCatalog + HudIcons), `Assets/Editor/DesktopBuild.cs` / WebGLBuild (ensure WO-408 texture opt path exercised or document). Update `docs/polyperfect-asset-catalog.md` + `docs/QUATERNIUS_NOTES.md` if gaps.  
**Acceptance:** No white trees or magenta structures in MainCastle_Hall / Village2 / OuterWorld / CastleHub rebuild. UI icons (ability, resource, portrait) render as images not glyphs. Build size reduction path verified. Owner visual confirm on mobile + WebGL. Brace on editor .cs.  
**Do NOT:** Re-import full packs (gitignored); hand-place in .unity; change _M tier requirement.

### WO-334 / WO-421 / WO-437: Battle Ready HUD Not Displaying + Battle HUD Broken (Skill Bar Empty) + Combat HUD Full Restyle from Tech Hud Elements Pack
**Lane:** 4 (UI/HUD) — **BLOCKED on WO-405**. 437 = owner directive full restyle.  
**Context (MASTER_CATALOG battle-atb + hud + risk §12/14 + ARCHITECTURE):** Live HUD = code-built `BattleHudUgui` (Canvas bottom panels, Command/Party, dynamic from Defs.HERO_ABILITIES[HeroClass] → AbilityDef Name/Cost/Slot, ATBRuntimeState.ActiveUnitId + Units, Side.Party, Resource/MaxResource, BattleLogEvent.Attack/Ability, BattleAction.Make*). Old UIDocument/BattleHUD.uxml dead (BattleController Start creates BattleHudUgui). Skill bar empty = missing GetAbilitiesForActiveHero alignment or no abilities registered for class. 405 ElarionUiKit (UGUI design system from Tech hud elements pack) is DO FIRST gate for all HUD restyle/unified work (403/404/411/417/421/437 blocked). Tech pack = `Assets/Tech hud elements/`.  
**Files (after 405 kit lands):** `Assets/_Modules/BattleATB/BattleHudUgui.cs` (full restyle: use Tech pack sliced sprites for frames/rings/bars/buttons/portraits; large thumb-friendly per mobile; cooldown rings + symbols per 308; wire real Defs + state; fix wave text to use BattleState.Wave), `Assets/_Modules/BattleATB/BattleController.cs` (ensure _hudUgui.Render on every state change + TickVisualAtb; remove any dead _hudDocument remnants), `Assets/Editor/BattleSceneBuilder.cs` (update if scene wiring stale), `Assets/_Modules/HUD/` for any shared kit (ElarionUiKit tokens in Core/UI or new HUD kit module). Add to VillageHudController if combat/town overlap.  
**Acceptance (after 405):** Battle HUD shows on ATBBattle load with 4 party slots (portraits, HP/MP/ATB rings, names), Command panel Attack + Skills (populated Q/W/E/R per active hero class from real HERO_ABILITIES, costs, icons), Item/Defend. Skill bar non-empty. Cooldown rings + symbols visible. Restyle matches Tech pack + owner mockups (no deviations like 411). No empty bars or "WAVE 1" hardcode. Owner playtest full breach flow (village + dungeon). Brace + compile.  
**Do NOT:** Use UXML at runtime; touch old BattleHUD.uxml for live path; invent API (use Types.cs BattleState/AbilityDef/Defs exactly); bypass 405 gate.

### WO-405: Complete UGUI Design System for ALL Game HUDs — DO FIRST, Blocks All HUD Work
**Lane:** 4 (explicit "DO FIRST" + blocks 400/403/404/411/417/421/437).  
**Context:** Every HUD (town/combat/hero select/settings/dev/vendor) must use the single ElarionUiKit (code-built tokens for panels, large buttons, resource bars, ability rings, compass, portraits, modals). Tech hud elements pack is the source art. Current VillageHudController + BattleHudUgui + Help/Admin use partial L* parchment + RpgUiCatalog; inconsistent and blocked for unified/mobile mockup parity (411: 10 deviations from hud_mobile_town.png).  
**Files:** New or central `Assets/_Modules/HUD/ElarionUiKit.cs` (or Core/UI) with static factory methods: StylePanel(RectTransform, style), CreateButton, CreateAbilityRing, CreateResourceBar, CreatePortraitSlot, large thumb padding (min 44–56px touch), dark misty parchment/gilt palette, 9-slice from Tech pack. Update `VillageHudController.Build()`, `BattleHudUgui.Build()`, HelpMenu, AdminOverlay, CompassHud, PlayerProgressPanel, vendor panels (412/415), hero select (257/328), settings (417), any Yarn dialogue option styling. Wire into PanelManager.  
**Acceptance:** 405 kit approved by owner. All subsequent HUD WOs (403 unified town, 404 combat, 411 town mockup 0 deviations, 417 rows visible with correct font, 421/437 battle restyle, 415 vendor storefront) use ONLY the kit. No more ad-hoc colors/anchors. Mobile thumb friendly verified. Code-built (no UXML). Brace on kit + consumers. Owner signs visual parity on all surfaces.  
**Do NOT:** Ship any HUD restyle before this; mix UXML; hard-code sizes/colors outside the kit; touch abandoned DTT HUDs.

### WO-333: Floor Shading/Collision Overlap Issues
**Lane:** 1 or 5 (world).  
**Files:** CastleHubBuilder (floor + invisible walkable NavMeshFloor), VillageSceneBuilder partials (one agent), OuterWorldBuilder/ExteriorTerrainBuilder, any NavMeshSurface baking paths (reflection). Add collision tests or visual floor layers.  
**Acceptance:** No z-fight shading, no walk-on-air or blocked floor at key points (castle courtyard, village paths, outer regions, seam exits). NavMesh walkable where visual floor is. Owner traversal confirm.

### WO-331: WebGL Crash on Village Load (Suspected Yarn Spinner — Triage) + WO-375: Yarn Spinner Threading Safety & Debug Element Removal
**Lane:** 10 + 4.  
**Context (dialogue catalog + risk):** Yarn single runner + DialogueCommandBridge (SignalContentComplete for blue-dot per 330). Threading safety issues on WebGL (coroutine or re-entrancy in OptionItem static guard or wait_for_event). Debug elements (blue Next indicator) must be hidden. 330 specifically: review Yarn docs for SignalContentComplete in hero select blue dot origin.  
**Files:** `Assets/_Modules/Village/Tutorial/DialogueService.cs` + `DialogueCommandBridge.cs` (add guards, correct SignalContentComplete usage for option/content complete blue-dot flow), `Assets/_Modules/DialogueUI/CompanionDialoguePresenter.cs` + OptionItem (static guard already present; harden for WebGL), `Assets/Editor/DialogueSystemBuilder.cs`, hero select / title Yarn paths, any `Debug` element removal in dialogue UI. WebGL build settings.  
**Acceptance:** WebGL load of Village2 + castle + dialogue no crash (itch + local host). Blue dot / option complete behaves per Yarn docs. No leftover debug Next indicators. Brace + owner WebGL playtest of onboarding + vendor talk.

### WO-335: Title Screen Button Hitboxes Misaligned
**Lane:** 4.  
**Files:** TitleController (code-built UI on Title.unity), any button RectTransforms + CanvasScaler.  
**Acceptance:** All title buttons (Continue, Play Intro, etc.) have large, accurate hitboxes on mobile portrait/landscape. No mis-taps.

### WO-328 (x2 in list): Hero Select — Character Portraits Not Fitting Window (Clipping/Overflow) + Recurring NullReferenceException Spam (Root-Cause)
**Lane:** 4.  
**Files:** HeroSelectController (code-built), portrait loading (Resources/HeroPortraits or cache), layout anchors. For NRE: any lazy reflection in HUD bridges / HeroControlEnsurer / title flow; add null guards + logs.  
**Acceptance:** Portraits fully visible inside window, no overflow/clip. Zero NRE spam on title→hero→pet flow (mobile + editor). Root cause documented in RESULT.

### WO-329: Hero Select — Player Stat Cards Missing (Specs Not Displayed Per Hero)
**Lane:** 4 + 6.  
**Files:** HeroSelectController + any stat display from HERO_STATS or GameState.  
**Acceptance:** Each hero (Knight/Ranger/Mage/Cleric) shows its stat card (HP/MP/speed/abilities preview) per design. Fits layout.

### WO-330: Hero Select — Blue Dot Origin — Review Yarn Spinner Docs for SignalContentComplete
**Lane:** 4 + 12.  
**Files:** HeroSelect + Yarn nodes for onboarding, DialogueCommandBridge (SignalContentComplete), CompanionDialoguePresenter.  
**Acceptance:** Blue dot (attention/option cue) originates and animates correctly per Yarn signal docs; no stuck or wrong placement.

### WO-327: Remove "Jump into the Action" Button (DTT Crash)
**Lane:** 0/4.  
**Context:** DTT removed; button dead and crashes.  
**Files:** HeroSelectController (and any Title remnants).  
**Acceptance:** Button gone; no crash path; onboarding flows clean to castle.

### WO-326: Hero Walks North but Model/Anim 90° to the Right (also covered under 363)
**Lane:** 0/2.  
**Files:** Same as 363 (HeroLocomotion, BodySwapper, anim factory, VisualFactory yaw -90 correction).  
**Acceptance:** Covered by 363 gate.

### WO-325: Nothing Happens at Resource Node (Mine Upgrade/Harvest Dead) + WO-424: Harvested Resources Not Added to HUD Resource Count
**Lane:** 6.  
**Files:** MineNode / CrystalMine (EconomyService grant), Harvest logic (offline + worker), HUD bridges (HeartHudBridge or TownHudBridge for resource counts), DialogueCommandBridge OpenUpgrade, VillageInventory if any.  
**Acceptance:** Tap/harvest at node adds to correct Economy pool (Wood/Iron) + GameState + HUD resource strip updates live. Upgrade flow works end-to-end.

### WO-321: Missing Gate on Side Exit (Near Pet House) + WO-322: Compass Not Visible (Can't Orient at Exits)
**Lane:** 1/5 + 4.  
**Files:** CastleHubBuilder (add gate marker + visual near pet quarters), SceneTransitionTrigger instances, CompassHud + bootstrap (ensure visible outside town ring, arrows for offscreen).  
**Acceptance:** All cardinal exits have visible gates/markers. Compass strip + red pips visible and accurate at every exit (castle + village). Mobile + desktop.

### WO-310: Companion Renders Wrong Color (Green Tint) Fix
**Lane:** 2.  
**Files:** StoryCompanion / companion injector (CastleCompanionIntroducerInjector), material application in HeroBodySwapper or VisualFactory (TripoMaterialFixer / atlas).  
**Acceptance:** Companions (Sylas etc.) render correct per-class tint/no green. Matches player hero.

### WO-308: Ability Bar w/ Cooldown Rings + Symbols
**Lane:** 4 (after 405).  
**Files:** BattleHudUgui (or shared kit) + VillageHudController ability slots. Use Tech pack rings + per-slot symbols. Wire to HeroAbilities cooldowns.  
**Acceptance:** 4 ability buttons show cooldown ring + icon/symbol. Functional (click casts via existing).

### WO-196: WebGL No-Brotli Rebuild
**Lane:** 10.  
**Files:** WebGLBuild.cs (the -noBrotli path), build scripts.  
**Acceptance:** Clean no-Brotli WebGL artifact produced and size-noted.

### WO-290: QuestService + Tracker UI
**Lane:** 12 (foundational).  
**Files:** Core Quests + QuestService, QuestTrackerHud, DialogueCommandBridge quest verbs (Start/Advance/Complete), GameState quest fields + SaveSchema.  
**Acceptance:** Quests track + display in HUD top-left; vendor/Yarn quest flow works; persistence survives reload.

### WO-110 / WO-257: Yarn Blue-Button Fix + Mobile-First HUD + Hero Select Screen Layout Fix
**Lane:** 4.  
**Files:** Dialogue UI (blue button → parchment dark ink), HeroSelectController layout (code-built, large buttons, portraits/stats per 328/329).  
**Acceptance:** No blue buttons; hero select fits and functional on mobile.

### WO-302: Floating Health-Bar Oversize Fix (Green Pill)
**Lane:** 0/4.  
**Files:** FloatingHealthBar.cs (scale compensation per-axis, chip Simple not Sliced). Already partly addressed per catalog.  
**Acceptance:** Bars correct size on large-scale enemies (orc/troll); no giant pill.

### WO-255 / WO-286: Hero Backwards + Walk Anim Not Playing + Hero FBX Rig Fix (AccuRIG)
**Lane:** 0/2/3.  
**Files:** Hero FBX import settings + .meta (root motion, avatar, rotation), HeroBodySwapper (post-swap rebind + controller), HeroLocomotion (Speed param to ActorAnimator + legacy), HeroAnimatorFactory (Mixamo clips).  
**Acceptance:** Hero faces forward, walk anim plays on move (Speed > 0), no backwards rig.

### WO-253: Split VillageSceneBuilder into Partials
**Lane:** 1 (serial, already done in practice per catalog).  
**Files:** VillageSceneBuilder.*.cs partials.  
**Acceptance:** Clean partial split; one-agent rule observed; no behavior change.

### WO-164: Zone Foundation (ThreatLevel/Depth/ZoneState)
**Lane:** 5 (foundational, do early).  
**Files:** Core World/ZoneManager + ZoneState, OuterWorld regions, RaidOutpost/Garrison threat, enemy scaling.  
**Acceptance:** Zones classify correctly (Goldfields T1 etc.); threat/depth drive spawn/encounter.

### WO-303: Combat Party HUD Wire-to-Live-Data
**Lane:** 4.  
**Files:** BattleHudUgui party slots + PartyHudBridge (or ATB equivalent) for real StoryCompanion HP.  
**Acceptance:** Party frames show live HP (not placeholder immortal).

### WO-254: Hero Hover Exploit Fix
**Lane:** 2.  
**Files:** HeroLocomotion GroundSnapEnabled or PlayerAttackController reach.  
**Acceptance:** No hover/ground exploit.

### WO-173: Exterior Terrain Missing (Black Void)
**Lane:** 1/5.  
**Files:** ExteriorTerrainBuilder + OuterWorldBuilder.  
**Acceptance:** Terrain visible and walkable in OuterWorld.

### WO-368: Camera Distance Fix — Movement Regression & Orientation Validation
**Lane:** 0/4.  
**Files:** SmartMobileCamera (distance + follow), HeroLocomotion.  
**Acceptance:** Camera distance stable on move; orientation gate (363) passes.

### WO-383: Castle ↔ OuterWorld Seam Connection (+ Hero-Strand Bug)
**Lane:** 5 (explicit).  
**Context:** WorldSceneLoader + SceneTransitionTrigger + NavMeshLink (reflection in CastleHubBuilder) + WarpTo. Hero strands at (0,0.5,-80) past off-mesh clamp.  
**Files:** CastleHubBuilder (Wire* methods + reflection NavMeshLink), WorldSceneLoader (HubScenes include Castle*), SceneTransitionTrigger, HeroLocomotion.WarpTo (clamp + facing), HeroControlEnsurer (Castle* recovery).  
**Acceptance:** South gate seamless walk from castle to OuterWorld + back; hero lands on walkable NavMesh facing out; no strand or camera snap. Mobile tested.

### WO-385: Castle Camera Fights Enclosed Geometry — World-Locked Seat + Wall-Collision Jam
**Lane:** 4.  
**Files:** SmartMobileCamera (castle-specific follow or seat), CastleHubBuilder (camera volumes or fade), collision on walls.  
**Acceptance:** Camera stays in valid volumes inside keep/battlements; no wall jam or pop.

### WO-387: Camera-Relative Movement in 3rd-Person Follow Mode
**Lane:** 0/2 (explicit ✓DONE in lanes but verify).  
**Files:** SmartMobileCamera (CameraYaw authority), HeroLocomotion (input rotated by yaw).  
**Acceptance:** Input in follow mode is camera-relative; orientation gate passes.

### WO-376: Hero Pose Initialization — Idle State on Scene Load & Dialogue
**Lane:** 4/2 (explicit).  
**Files:** HeroBodySwapper, HeroPoseController, DialogueService hooks.  
**Acceptance:** Idle on load + Yarn (covered by 363/376).

### WO-377: Dialogue Input Blocking During Yarn Spinner
**Lane:** 4.  
**Files:** HeroLocomotion (InputSuppressed static), DialogueService, HeroAbilityInput.  
**Acceptance:** No movement or ability input while dialogue runs; resumes clean on complete.

### WO-391: P1 Dialogue Box Text Overlap (Echo Warden / Echo Hollow Pet Choice)
**Lane:** 4.  
**Files:** CompanionDialoguePresenter (RepairOptionsLayoutOnce), ClassicRPG OptionItem, Yarn nodes (PetHouse / StableBonds).  
**Acceptance:** Line text + options stack vertically, no overlap on pet choice nodes.

### WO-397: P1 Enemy Doesn't Engage Player (Idle Aggro)
**Lane:** 2.  
**Files:** EnemyBrain, RegionMobSpawner, EnemyOutpost, ATB Ai.  
**Acceptance:** Open-world + garrison enemies acquire and path to player.

### WO-394: P1 Build Click Gives No Feedback — Surface Why Build Is Blocked
**Lane:** 11.  
**Files:** BuildModeController / BuildPreviewModal + HUD bridge.  
**Acceptance:** Build button click surfaces exact block reason (resources, wave active, placement invalid) in UI.

### WO-398: P1 Knight Still Dealing Ranged Damage (Should Be Melee-Only)
**Lane:** 2.  
**Files:** HeroAbilities (class loadout), AbilityCatalog or Defs, PlayerAttackController or projectile launch gate.  
**Acceptance:** Knight Q/W/E/R are melee only (no ranged projectile or ranged damage calc).

### WO-406 / WO-412: P1 Shops Empty — Vendor Inventories Not Populated + Vendor Wares Catalog Empty — BUY Tab Lists No Items
**Lane:** 6.  
**Files:** Vendor data (packs or Economy vendor catalog), DialogueCommandBridge OpenShop, vendor UI panels (code-built from Tech pack per 415), PackStore or StoreService (429).  
**Acceptance:** All 8 castle vendors (Blacksmith etc.) show populated BUY/SELL tabs with real items/prices; buy succeeds and grants.

### WO-408: P1 WebGL Texture Optimization — 223 MB → <60 MB (Scripted)
**Lane:** 10.  
**Files:** Existing WO-408 scripts (run them), build pipeline.  
**Acceptance:** WebGL artifact <60 MB (or documented Gzip path); itch-ready.

### WO-409 (dup with trees): P1 Missing Materials (Magenta) on Tower Structures + Missing UI Sprite Refs Render as Text Glyphs
**Lane:** 0.  
**Covered under 332/323/409 cluster.**

### WO-410: P0 0.1 fps in MainCastle_Hall — Main-Thread GC Storm (13–22 MB Allocated/Frame) + Combat-Object Leak
**Lane:** 10 (explicit ★P0).  
**Context (MASTER_CATALOG risk §2 + perf):** Per-frame FindObjectsByType in towers/bridges/companion heal; combat objects leak across additive loads. Castle is home hub — must be smooth.  
**Files:** Castle-specific towers (DefenseTower/ArcaneTower etc. in Village or shared), StoryCompanion, any bridge that scans, WorldSceneLoader / additive load paths, AtbCombatantSwapper hide + cleanup, Profiler/PerfDiagnostic. Replace scans with O(1) registries.  
**Acceptance:** Castle loads at stable fps (30+ mobile target); GC alloc/frame <1 MB in steady state; no combat leak on repeated castle→outer→battle returns. Owner profile + playtest.

### WO-411: P1 Town HUD Does Not Match hud_mobile_town.png Mockup (10 Deviations)
**Lane:** 4 (blocked on 405).  
**Files:** VillageHudController (layout + widgets), after 405 kit.  
**Acceptance:** 0 deviations from mockup on town chrome (resources top, abilities bottom-right, build bottom-left, minimap, party, wave, etc.). Large thumb buttons. Mobile verified.

### WO-413: P1 Upgradable Buildings Wrongly Offer Shop Menu — Split isUpgradable vs isShoppable Dialogue Options
**Lane:** 6.  
**Files:** Buildings data + DialogueCommandBridge (structure_status caps), StructureMenu.yarn, Building classes (isUpgradable / isShoppable flags), NPC vendor injectors.  
**Acceptance:** Windmill/Farm etc. offer Upgrade only (no Shop); Store/Blacksmith offer Shop + appropriate. Yarn options respect the split.

### WO-415: P1 Vendor Storefront UI (Armor First) Skinned from Tech Hud Elements Pack — Replace Placeholder Vendor Wares Modal
**Lane:** 4 (after 412).  
**Files:** Vendor panel code-built from 405 kit + Tech pack assets; OpenShop path.  
**Acceptance:** Vendor UI (armor/weapons first) skinned correctly; functional buy/sell with Economy.

### WO-419: P1 Enemies Do Not Attack After Castle → OuterWorld Transition
**Lane:** 2 (explicit).  
**Files:** RaidOutpostSystem, EnemyOutpost, RegionMobSpawner, EnemyBrain, any transition cleanup in WorldSceneLoader or HeroControlEnsurer.  
**Acceptance:** Enemies aggro and attack immediately after seam cross.

### WO-417 (dup): P1 — DO FIRST: Settings + Dev Tools Panels Render Labels Under the Row Layer — All Rows Blank
**Lane:** 4 (explicit ★DO FIRST).  
**Files:** HelpMenu.cs + AdminOverlay.cs (explicit LegacyRuntime.ttf font, code-built rows, no UXML dependency for layout).  
**Acceptance:** Settings gear and Ctrl+Shift+A dev panel show visible labeled rows on all platforms. No blank rows. Matches 405 kit if applicable.

### WO-423: P1 Hero Attacks Without Facing Target — Rotate-to-Target on Attack + Turn Animation Blending
**Lane:** 2 (explicit).  
**Covered under 363 orientation cluster.**  
**Acceptance:** Covered.

### WO-424 (dup): P1 Harvested Resources Not Added to HUD Resource Count
**Covered under 325 cluster.**

### WO-428: P1 Hero Damage Not Shown on HUD — Health Bar Never Moves, but Death Still Occurs
**Lane:** 4.  
**Files:** VillageHudController (SetHeroHp calls), HeroHealth (OnHealthChanged event + bridge), HeroAbilitiesHudBridge or HeartHudBridge.  
**Acceptance:** Hero HP bar on town/combat HUD updates live on damage/heal; death still triggers correctly.

### WO-429: Store Stock Served from the Neon DB (StoreService + Offline-First Fallback)
**Lane:** 7 (explicit; repo spec exists).  
**Files:** StoreService / PackStore wiring, vendor catalog load (Neon endpoint + local fallback JSON), 412/415/406 panels.  
**Acceptance:** BUY tab populates from DB (or offline), purchase works, persists.

### WO-432: P1 NPC Companion Breaks (Owner Playtest 2026-06-12)
**Lane:** 12/2.  
**Files:** StoryCompanion, CastleCompanionIntroducerInjector, leash/follow, gear, Yarn RecruitCompanion.  
**Acceptance:** Companions spawn, follow, fight, talk, persist without NRE or break.

### WO-430: P1 Tree of Life Fail (Owner Playtest 2026-06-12)
**Lane:** 1/0.  
**Files:** HeartController / TreeOfLife (IDamageable, HP, crystals, HUD bridge), castle placement (at 0,0,0 per regression).  
**Acceptance:** Tree visible, damageable, updates HUD, no fail states on load/raid.

### WO-431: P1 Stores Fail to Load (Owner Playtest 2026-06-12)
**Lane:** 6/7.  
**Covered under 406/412/429 cluster.**

### WO-166: Playtest Regressions (Gates / Walk-Anim / Pet / Stairs)
**Lane:** 1/2/12.  
**Files:** Gates (SceneTransitionTrigger), walk anim (HeroBodySwapper + anim), pet (Pets module), castle stairs (CastleHubBuilder + NavMeshLink).  
**Acceptance:** All listed regressions cleared; owner retest green.

### WO-434: P1 NUL-Padded .cs Files on Linux Mount — Commit-Poisoning Guard
**Lane:** 0.  
**Files:** Any write path (but per rules: UI never edits .cs via bash/mount; only CLI on Windows). Add guard in CompileGate or pre-commit if possible.  
**Acceptance:** No NUL-padded or garbled .cs in tree; mount-sync rule enforced in process.

### WO-437: P1 Combat HUD Full Restyle from Tech Hud Elements Pack (Owner Directive)
**Lane:** 4 (after 405).  
**Covered under 334/421/437 + 405 cluster.**  
**Acceptance:** Covered.

## 2. Remaining Lower-Priority / Overlapping from Handover List
- All DTT-specific (317/318/327/330/331/332/333/335 etc.): Treat as frozen history per PIPELINE_STATE 2026-06-09 removal. Only keep regression guards if they overlap live paths (ATB, orientation, Yarn).
- 314 BuildPreviewModal, 316 Mobs families, 308 ability bar (merge into 437/405), 196/408 web (merge), 290/291 quest (foundational), 164 zone (early), 303/428/421 HUD data (post-405), 254/255/286 hero (merge 363/423), 253/384 builder/castle stairs (Lane 1 serial), 173 terrain, 310 companion, 322 compass (merge 321), 375/391 Yarn (merge 331/377), 394/398/413/415/406/412/424/429 economy/vendor (Lane 6), 410/419/383/385/368/387 castle perf/seam/camera (Lane 5 + 10 + 0), 376/377 pose/block (merge 363), 409 magenta (merge trees), 411/417 (post-405), 423 (merge 363), 432/430/431 playtest (specific follow-ups after core gates).

## 3. Implementation Order Recommendation (Leverage per ARCHITECTURE_PRINCIPLES §3)
1. Lane 0 gates first (363+373+409+trees+WebGL Yarn+title+hero select NRE) — unblock everything.
2. 405 UGUI kit (DO FIRST) — unblocks all Lane 4.
3. 417 Settings/Dev rows (DO FIRST per list).
4. 410 Castle perf GC (P0 home hub).
5. 383/385/368/387 castle seam + camera (player-felt exploration).
6. Parallel: Lane 2 combat (419/423/398/397/315/316), Lane 6 economy (406/412/413/415/424/429), Lane 1 world serial (if builder free).
7. HUD restyles (421/437/411) after kit.
8. Foundational 290/164/339 as capacity allows (many lanes depend).

## 4. Deliverables for This WO
- This spec file committed.
- Individual follow-up `WORK_ORDER_43x_*.md` (or .RESULT) per major ticket as claimed (backfill Notion specs).
- Updated `CLI_LANES_WO_NUMBERS.md` (if new mints or status changes) + `docs/MASTER_CATALOG/docs-wo-state.md`.
- For any .cs: brace python in the RESULT.
- Owner playtest sign-off per ticket (felt verdict).

**Ready for assignment per lanes. Owner creative calls on visuals/mockup parity (Tech pack, hud_mobile_*.png). Quality over speed — what is right, not what is easy.**

(End of consolidated detailed work orders. All items from user handover list + cross-referenced open state in MASTER_CATALOG / lanes addressed with files, acceptance, NOT-touch, and architecture guardrails.)