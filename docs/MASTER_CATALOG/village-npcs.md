# Master Catalog — village-npcs

**Dated 2026-08-02 — supersedes the 2026-07-14 catalog** (that version was party-of-4 framed;
the party pivot is RETIRED — see "The pivot" below — and it missed five files that now exist
plus the WO-818/833/834 KayKit body program).

Area: `Assets/_Modules/Village/NPCs/`
Assembly: **DeNelle.Village** — namespace `DeNelle.Village`. Cross-assembly law: Village → Core only;
HUD reached via Core statics (`PostureSignals`/`HudCommands`) or reflection (`PartyHudBridge`).

Verified from the actual code (every `.cs` in scope read this pass; comments cross-checked —
mismatches are in the RISK LEDGER). External seams verified in: `FeatureFlags.cs`,
`Core/Dialogue/DialogueService.cs`, `Core/Dialogue/DialogueRunner.cs`,
`Village/Tutorial/DialogueService.cs`, `Village/Tutorial/DialogueCommandSink.cs`,
`Core/HubScenes.cs`, `Village/BuildMode/StructureSingleton.cs`,
`Assets/Editor/KayKitNpcAnimatorSetup.cs`, `structures-catalog.json`, staged
`Assets/Resources/NPCs/KayKit/*`, and git history of the folder.

---

## THE PIVOT — single Knight; companions are HISTORICAL

`FeatureFlags.SingleHero` (**default ON**, PlayerPrefs `ff.singlehero` —
`Assets/_Modules/Core/FeatureFlags.cs:40`) retires the whole recruited-party stack. Gates,
each verified:

| File | Gate | Effect when ON (default) |
|---|---|---|
| `StoryCompanionInjector.cs:149-161` | top of `Spawn()` | despawns any live companion bodies, spawns none |
| `CastleCompanionIntroducerInjector.cs:104-108` | `Bootstrap()` no-op | no walk-up introducer NPC, `Active` stays false |
| `SylasFirstMeeting.cs:137-140` | `ShouldRun` false | join beat stands down |
| `ElaraWaveThreeJoin.cs:122-125` | `ShouldStillTry` false | wave-3 join stands down, watcher self-destructs |
| `PartyHudBridge.cs:60-77` | `Update()` | hides HUD party slots 1..3 |

Flag OFF restores the party-of-4 wiring intact — the code is dormant, not deleted.
**The Grom join beat is GONE entirely**: no `GromOuterWorldReturnJoin.cs` anywhere in the
tree or in git history (`git log --all -- "*GromOuterWorldReturnJoin*"` = empty), and its
seen-key `grom_world_return_join` matches nothing in `Assets/`. It survives only as comments
(`Hero/CameraModeController.cs:44,107,405`; canon-order comments in `SylasFirstMeeting.cs:4`,
`ElaraWaveThreeJoin.cs:4-6`, `StoryCompanionInjector.cs:38,167`). The 2026-07-14 catalog
described it as a live file — that entry was never true of this tree.

Sylas still has a BODY in the FTUE via **SylasStewardInjector** (WO-702, below) — a
non-combat steward for the founding beats, spawned *because* `ff.singlehero` no-ops the
companion introducer (`SylasStewardInjector.cs:6-8`).

---

## DIALOGUE-BRIDGE TRUTH — where the ~40 verbs actually register (single-register law)

**Yarn is FULLY REMOVED (WO-557).** There is no YarnSpinner runtime, no DialogueRunner
prefab, no Yarn command registration anywhere (`AddCommandHandler` / `[YarnCommand]`:
zero hits in `Assets/`, save one comment in `Troops/TroopDialogueCommands.cs`).
**`NPCCommandBridge` is DEAD** — no class exists; the name survives only as a stale header
comment in `Village/Hero/ShopPanel.cs:9` and in `_Modules/HUD/README*.md`.

The live chain:

1. **`Assets/_Modules/Village/Tutorial/DialogueService.cs`** (ns `DeNelle.Village`, static) —
   the ONE launch seam every NPC interactable calls. A Yarn-free shim (header lines 2-15)
   forwarding to `DeNelle.Core.Dialogue.DialogueService` + `dialogues.json`
   (`DialogueCatalog.Find`). `Play(node)` :49, `PlayStructure(structureId, displayName)` :83 —
   conversational structures run as authored dialogue (:90-94); **transactions open panels
   directly** (shoppable → `PanelRouter.Open(PanelId.PartyShop, id)` :113, with the F8-14
   combat shop-block :106-112); neither → returns false so the caller's panel fallback runs.
   `CurrentStructureId`/`CurrentStructureName` :71-75; `Stop()` :124; New-Game reset hook :138-142.
2. **`Assets/_Modules/Village/Tutorial/DialogueCommandSink.cs` — THE single command host.**
   Registered once at `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` `Register()`
   (:46-55) via `DeNelle.Core.Dialogue.DialogueService.RegisterSink(sink)` +
   `RegisterConditions(sink)`. Core holds ONE static `_sink`
   (`Core/Dialogue/DialogueService.cs:19,23`) — last registration wins; only this sink ever
   registers, which IS the single-register law now. The runner dispatches each node command via
   `_sink?.Run(c.Verb, c.Args)` (`Core/Dialogue/DialogueRunner.cs:137`).
   - Gate: `FeatureFlags.CustomDialogue` — **default ON** (`FeatureFlags.cs:145`, WO-557:
     "no legacy path to fall back to, so the custom sink/View MUST register").
     ⚠ The sink's own header (:23) still says "default off" — STALE comment.
   - The verb switch (:79-203), ~40 verbs: panels `OpenRumorBoard/OpenUpgrade/OpenShop
     [vendor buy|sell]/OpenCraft/OpenAlchemy/OpenJeweler/OpenTalents/OpenCosmetics/
     OpenRealmStore/OpenEquip/OpenArena`; quests `StartQuest/AdvanceQuest/CompleteQuest/
     GiveKeystone/SetFlag/SetQuestFlag`; roster `RecruitCompanion` (:129 →
     `GameStateService.AddToParty` — companions-era, harmless under single-hero);
     upgrades `TryUpgradeBuilding/structure_upgrade` (:133-155, city-tier-wins dual-authority
     guard); `play_sfx/save_game/grant_resources_for_towers`; barracks
     `ShowTrainingUI/StartTraining` (→ `Troops/TroopDialogueCommands`); pets
     `spawn_starting_pet/spawn_named_pet/pet_task`; camera `camera_focus/camera_glance/
     camera_shake/camera_show_all_gates/camera_return_to_hero`; HUD `set_hud_objective/
     set_hud_hint/highlight_ui/unhighlight_ui`; movement `start_autowalk/stop_autowalk/
     enable_full_controls` (:286 finishes onboarding); `portrait`. Unknown verb ⇒
     `FlowTrace.Warn` (:198-202), never silent. Shop verbs blocked during combat via
     `AmbientNPC.IsCombatActive` (:61-68).
   - Conditions (`Check`, :380-443): `!`-negation, `quest_*_active/_done`, `keystone_*`,
     `keystone_count_min_*`, `pet_owned_*`, `pet_grantable_*`, `pet_select_closed`, `onboarded`.

---

## CODE — WO-818/833 KayKit structure-NPC bodies (the current NPC art program)

### KayKitNpcBody.cs (WO-818 resolver + WO-833 ArmIdle)
- `internal static class` (:27). The ONE data-driven resolver for a structure's KayKit NPC body.
- `Load(catalogId, system, out resolvedRes)` (:44-74): reads `CatalogRegistry.Get(id).repo.npcModel`
  (Guard-wrapped :50-54), loads `Resources/NPCs/KayKit/<slug>` (`ResourcesRoot` :30). Failure
  semantics (:9-17): un-authored row ⇒ **quiet null** (People chain is the designed fallback);
  authored-but-broken slug ⇒ **exactly ONE `FlowTrace.Warn`** (:67-70) then null. Never a blank NPC.
- `ArmIdle(bodyInstance, resolvedRes, system)` (:93-134, WO-833 — owner F8 2026-08-02 "NPC Stuck
  in T Pose"): staged FBXs import Humanoid with an Animator but NO controller ⇒ bind pose. Assigns
  the shared `Resources/NPCs/KayKit/KayKitNpcIdle` controller (`IdleControllerRes` :34);
  defensive Animator+Avatar self-heal via `Resources.LoadAll<Avatar>` on the FBX path (:110-127);
  `applyRootMotion = false` (:130 — ground-seated NPCs must not drift). Missing controller ⇒ ONE
  Warn, NPC stays visible in bind pose. Only KayKit bodies are armed — callers gate on
  `kayKitRes != null` (People-chain bodies ship their own animator).
- Callers: `BarracksNpcInjector.SpawnDrillmaster` (:253, ArmIdle :294) and
  `CastleVendorNpcInjector.SpawnVendor` (:722-726, ArmIdle :807). Both KayKit-FIRST, then the
  legacy People prefab, then capsule placeholder.
- Status: **WIRED/LIVE.**

### The 12-structure mapping (OWNER-ONLY retag — a swap is a one-word JSON edit, never code)
`repo.npcModel` in `Assets/StreamingAssets/Data/Canonical/structures-catalog.json` (mirrored at
`Assets/Resources/Data/Canonical/structures-catalog.json`).

> ## ⛔ CORRECTED 2026-08-20 — THE KAYKIT SLUGS BELOW ARE RETIRED. THE ROWS WEAR CRAFTPIX PEOPLE.
> PROD-002 Deliverable B retagged all 12 rows on 2026-08-18 (`233613615`) to **folder-qualified**
> CraftPix slugs. Read from the StreamingAssets copy today:
>
> | Structure id | `repo.npcModel` (LIVE) |
> |---|---|
> | `healing_caravan` | `CraftPixPeople/NPC_Peasant_5` |
> | `pet-house` | `CraftPixPeople/NPC_Peasant_6` |
> | `workshop` | `CraftPixPeople/NPC_RichCitizen_2` |
> | `market` | `CraftPixPeople/NPC_Peasant_1` |
> | `mill` | `CraftPixPeople/NPC_Peasant_2` |
> | `forge` | `CraftPixPeople/NPC_CityDweller_1` |
> | `armorer` | `CraftPixPeople/NPC_CityDweller_2` |
> | `jeweler` | `CraftPixPeople/NPC_RichCitizen_1` |
> | `arcane-tower` | `CraftPixPeople/NPC_RichCitizen_3` |
> | `collector_farm` | `CraftPixPeople/NPC_Peasant_3` |
> | `collector_lumbermill` | `CraftPixPeople/NPC_Peasant_4` |
> | `barracks` | `CraftPixPeople/NPC_RichCitizen_4` |
>
> Note `fountain_healing` is no longer the row that carries a slug — it is **`healing_caravan`**.
> The **QUEST CAST** is a separate injector (`QuestCastNpcInjector`) and was moved onto the same
> contract on 2026-08-20 (`79c1e61b`): **Village Elder → `CraftPixPeople/NPC_King`** (one of only two
> bodies no structure row claims, so the Elder's face appears nowhere else in the hub) and **Fenn
> Wildmane → `CraftPixPeople/NPC_Peasant_4`**.
>
> **The old table is kept below as the record of the KayKit era — do not read it as current.**

| Structure id | npcModel slug (⛔ RETIRED 2026-08-18) | line |
|---|---|---|
| fountain_healing | Cleric | 404 |
| pet-house | Druid | 491 |
| workshop | Engineer | 536 |
| market | Hoarder | 581 |
| mill | Farmer_B | 626 |
| forge | Barbarian | 708 |
| armorer | BlackKnight | 753 |
| jeweler | Tiefling | 795 |
| arcane-tower | Mage | 841 |
| collector_farm | Farmer_A | 949 |
| collector_lumbermill | Ranger | 997 |
| barracks | Paladin_with_Helmet | 1218 |

Staged bodies (TRACKED, not gitignored): `Assets/Resources/NPCs/KayKit/` — 12 FBXs + textures +
`KayKitNpcIdle.controller` all present on disk (verified this pass).

### ⛔ THE TWO PATHS THAT FED TOWN NPCs THE HERO'S ANIMATION (corrected 2026-08-20)

Both were live simultaneously, and fixing only the first would have looked like a fix and changed
nothing the owner could see:

1. **`KayKitNpcBody.ArmIdle` on a CraftPix body.** `KayKitNpcIdle` plays the Knight's combat standby
   (see the next section) — right for a knight, wrong for a shopkeeper. `79c1e61b` stopped the three
   NPC injectors arming CraftPix people with it. Pinned by `NpcIdleControllerRegression`
   `[npc-idle-controller]`.
2. **The DEFAULT path — `AC_CraftPixTownsfolk` itself.** Its Idle/Walk states resolved by GUID to
   `Assets/Action/Shared/Shared_Idle.fbx` and `Shared_Walk_Forward.fbx` — **the HERO's mixamo
   locomotion**, the same clips the Knight/Cleric/Mage/Ranger controllers play. **All 14** CraftPix
   bodies share this one controller, so every vendor, every wandering villager and both quest NPCs
   stood combat-ready and walked the hero's walk. Repointed in `9a2d1faae` to the civilian Supercyan
   `common_people@idle` / `common_people@walk` by the new editor entry
   **`DeNelle.Editor.CraftPixTownsfolkAnimatorSetup.Run`**, which drives Unity's own
   `AnimatorController` API (never a hand-edit of the `.controller` asset) and refuses to swap in a
   clip that is not imported Humanoid — a Generic clip cannot pose a humanoid rig.

⚠ **The lesson is about evidence, and it is on the record in `9a2d1faae`'s own body:** the earlier
commit asserted this controller "plays a Supercyan civilian clip" from a folder listing, without
opening the GUIDs. Comments and folder listings are not the animator.

### The shared idle (WO-833)
`Assets/Editor/KayKitNpcAnimatorSetup.cs` — `Build()` (:59) creates
`Assets/Resources/NPCs/KayKit/KayKitNpcIdle.controller`: ONE default "Idle" state, no params,
playing the **retargeted hero mocap standby clip `m-standby-idle`**
(`Assets/Action/Knight/Motion/studio-mocap-series-magical-moves/m-standby-idle.fbx`, :52-53 —
the same clip HeroAnimatorFactory uses as the KnightMocap Locomotion idle). Works because
`Assets/Editor/KayKitNpcImporter.cs` flips the staged copies to Humanoid (avatar verdict OK
12/12). KayKit's own animation pack is Generic-rigged + gitignored — optional flavor later, not
the fix. Menu `Defenders/Art/Build KayKit NPC Idle Controller`; batchmode
`DeNelle.Editor.KayKitNpcAnimatorSetup.Build`; markers `KAYKIT_IDLE_OK`/`KAYKIT_IDLE_FAIL`.

---

## CODE — Castle-hub injectors (the LIVE NPC population)

All are self-bootstrapping DDOL singletons (`RuntimeInitializeOnLoadMethod(AfterSceneLoad)`),
idempotent per load (nuke prior runtime holder), never touch a `.unity` file.

### CastleVendorNpcInjector.cs (the big one — 1272 lines, 4 classes)
- **Scene gate:** `IsCastleHubScene` = exact `MainCastle_Hall` OR `Main_Castle_Overworld`
  (:54-59, WO-608 merged scene) — NOT `HubScenes.IsHub`.
- **One spawn path** (WO-682 deleted the baked-marker loop + ff.strategicplacement):
  `AnchorVendorsToPlacedBuildings` coroutine (:274-419) — 2s poll over `AnchorRoles`
  (:248-265, role → BuildingId: Blacksmith→armorer, Lumbermill→collector_lumbermill,
  Windmill→collector_farm, EchoHollow→pet-house, Forge→forge AND collector_forge,
  ArcaneTower→arcane-tower, Jeweler→jeweler, Marketplace→market, Apothecary→apothecary,
  JewelersBench→jewelers-bench). Per-role settle; no timeout (player-paced).
  - **Collector vision** (:287-344): Lumbermill/Farm/Forge place as `ResourceCollector`
    (no `Building` component) — matched by bare id; **origin guard** (:326-337) skips LOGICAL
    economy collectors (no `PlacedStructure` parent — the "vendors stacked at the Heart" fix).
  - **Lever-1 fallback** (:346-373 + `ResolveBakedOrStationAnchor` :436-488): no live building ⇒
    anchor to the baked storefront / station census pos so a fresh hub pre-stands every trade.
    Deferred to **pass ≥ 1** (:368 — pass 0 ran before BaseLayoutLoader's replay and double-seated
    every role, the F8 2026-07-30 "duplicated NPCs"). **WO-834 blank-town gate** (:445-450):
    the whole fallback is withheld unless `StructureSingleton.MayBakedTwinSurface(buildingId)`
    (`BuildMode/StructureSingleton.cs:176/193`) — a Build-Your-Own save stays blank; vendors
    arrive as buildings are placed.
- **WO-707 placement hook `NotifyBuildingPlaced(id, transform)`** (:534-559, public static):
  called by `BuildModeController.Place` / `BaseLayoutLoader.Spawn` the instant a placement
  commits. Pre-Instance calls are **ENQUEUED** (`s_pendingPlacements` :51-52) and drained in
  `Awake` (:171, `DrainPendingPlacements` :177-193) — the timing-race fix. Routes through
  `SpawnVendorForPlaced` (:561-659): `RoleForBuildingId` reverse map (:667-686, adds palette ids
  workshop/mill/lumbermill/collector_forge/lumberyard/foundry/silo; unmapped storefront ⇒ Warn,
  known non-storefront prefixes tower_/wall_/gate_/mine_/deco_/fountain_/repair_ ⇒ quiet :691-698);
  **per-BUILDING idempotency** via `VendorSeatMarker` (:597-603, class :967-971 — fake-null
  self-clearing); **baked-stray eviction** (:638-654): a placed building evicts marker-less
  same-role bodies (placed wins — F8 seq524 "armorer twice").
- **`SpawnVendor`** (:709-835): KayKit-first body (:722-726) → `VendorFor(role)` People body
  (:105-150: armorer=WO-444 armor split, apothecary/jewelersbench = runtime-station speakers) →
  merchant → capsule. Front-of-building placement along the placed root's forward (:740-760,
  owner 2026-07-13 — supersedes Heart-facing for placed structures; Heart-facing is the
  fallback), ground-band navmesh accept [-0.35..0.75] (:767-775), render-verify (:796-802),
  ArmIdle for KayKit (:807), `NormalizeToHeroHeight` ~1.95m (:925-942, bubble counter-scale),
  `NpcGroundSeat.Seat` (:815), `AmbientNPC.Configure(arch, wander:false)` + **no hero handed**
  (:819-826 — proximity chatter stays quiet so it never competes with structure dialogue),
  agent disabled, `AttachInteraction` (:860-884): `CastleNpcInteractable` +
  `BuildingInteractable.MarkNpcCovered(id)` + `InteractableSign.ForStructureId`
  (`Buildings/InteractableSign.cs:50`) + `CastleVendorWaveHider`.
- **`VendorRoles()`** (:514-520, public) — the AutoPilot coverage oracle walks the injector's own map.

### CastleNpcInteractable (same file, :1073-1270)
- Slim proximity Talk for ONE structure. `Configure(structureId, label, hero)` :1091.
  ActivateRadius 6m; walk-away auto-close at +4m (:1113-1122); registers `TalkPromptRegistry`
  in range (:1139); WO-416: never raises the shared MobileInteractButton (HUD TALK button is
  canonical; desktop [F] removed :1153-1155); sets/clears `HudBuildingFocus` for upgradables (:1143-1144).
- **`ResolveRoute(structureId)`** (:1251-1254, public static, PURE — the single routing truth the
  headless `AssertVendorTalkRoute` oracle also reads): conversation-authored OR shoppable OR
  talk-function ⇒ `"talk-dialogue"`; upgrade-only ⇒ `"upgrade-panel"`. `TalkFunctionIds` =
  {"barracks"} (:1231-1234). Interact (:1157-1203): upgrade-panel route → `PanelRouter.Open(
  PanelId.BuildingUpgrade)`; else `DialogueService.PlayStructure`; WO-576 fallback to the upgrade
  panel; never a silent dead-end. Test seams `LastInteractRoute`/`LastInteractId` (:1086-1087).

### CastleVendorWaveHider (same file, :991-1062)
- Ticket F8-14: vendors are wander=false so AmbientNPC's flee skips them — this watcher hides
  renderers + disables the interactable on the SAME authority (`AmbientNPC.IsCombatActive`,
  :1021), restores after. One global transition Step with counts (:1025-1032).

### BarracksNpcInjector.cs (drillmaster)
- Scene gate: same castle pair (:39-43). Spawns the drillmaster in front of the barracks.
- **Unlock rule** (WO-724, :152-161): exists only when `BarracksUnlock.IsUnlocked`
  (`Troops/BarracksUnlock.cs:34` — ff.barracks AND founding-complete). 1 Hz poll (:112-131)
  surfaces it LIVE when the unlock flips in-hub — gated by the **WO-834 blank-town check**
  `StructureSingleton.MayBakedTwinSurface("barracks")` (:124) so a Build-Your-Own save waits for
  a placed barracks.
- **Anchor: placed wins** (WO-812, :176-187): a live `Building` with id "barracks" first, baked
  `CastleBarracks` fallback; reseat on `StructureSingleton.SingletonResolved` (:88-89, :137-144 —
  StructureSingleton v2 replaced the bespoke scan). Neither ⇒ Warn + "build one" log (:189-198).
- Body: **KayKit-first** (`KayKitNpcBody.Load("barracks")` :253 → Paladin_with_Helmet) →
  `NPCs/NPC_Blacksmith` → `NPCs/NPC_Merchant` → capsule. Render-verify (:283-289), ArmIdle
  (:294), normalize + `NpcGroundSeat.Seat` (:296-297), Blacksmith archetype wander:false (:301-302),
  agent off, `CastleNpcInteractable.Configure("barracks","Barracks")` + MarkNpcCovered + sign
  (:329-343). Center-facing placement toward the Heart, FrontOffset 4.5m (:236-247).
- WO-813 once-teach (:210-223): first surface ⇒ `barracks_intro` SeenTutorials + toast.
- Status: **WIRED/LIVE** (flag-gated by ff.barracks).

### CastleTownsfolkInjector.cs (wandering villagers — NEW since the old catalog)
- Owner feature 2026-07-06 "villagers hide during combat": the fleet census proved the castle hub
  had ZERO wander-eligible AmbientNPCs (every body wander:false) — this injector adds the subjects.
- Scene gate: castle pair (:48-50). Deferred start: waits ≤8s for ≥1 in-ring `Building` (:127-145).
- **BLANK-1 rule** (:174-182): ONE villager per DISTINCT in-ring building (cap 5,
  `VillagerCount` :55), never a recycled crowd; no building ⇒ no NPC. Town ring = 60u of origin (:59).
- Spawn (:253-338): Mevina/Tob peasant bodies round-robin, archetypes
  Villager/Child/Elder/Farmer/Villager (:71-86 — generic voices only, no warden archetypes);
  position nudged Heart-ward + tangent, navmesh-sampled with **ground band [-0.35..0.75]**
  (:67-68, :237-243 — rejects wall-walk mesh, the "NPC on the gatehouse" fix); capsule fallback;
  `Configure(arch, wander:TRUE, anchor)` + live agent with `baseOffset = seatDelta` (:317-323 —
  holds the ground-seat correction through roam/flee/return).
- Status: **WIRED/LIVE.** These are the only flee-eligible NPCs in the hub.

### SylasStewardInjector.cs (WO-702 founding steward — NEW since the old catalog)
- Sylas's BODY for the Tutorial V2 founding beats ("use the model for him, then unload it") —
  exists because ff.singlehero no-ops the companion introducer (:5-8).
- Gates: `FeatureFlags.TutorialV2` at Bootstrap (:82) + `HubScenes.IsHub` (:98,109) +
  `ArcIncomplete()` = TutorialV2 && !Onboarded (:139-145). 1 Hz poll destroys the BODY when
  Onboarded flips (:122-135) — **the injector stays resident** (FTUE-1 root fix :113-121: the
  old self-destruct on the Title screen killed New-Game-in-same-run spawns).
- Spawn (:147-244): Ranger-Scout body (Tob fallback, capsule last), near the Heart +
  `CourtyardOffset (2,0,-9)` (:66 — south of the trunk, owner F8 "is he an actual character"),
  navmesh-snapped, faces the tree, normalize + `NpcGroundSeat.Seat`, wander:false, render-verify.
  **Load-bearing name `"Sylas"`** (:189 — `TutorialWorldAnchors.ResolveSylas` finds it by
  `GameObject.Find`; the "world.sylas" spotlight target).
- `SylasStewardInteractable` (same file :293-344): courtesy Talk — replays
  `TutorialFlow.CurrentIntroDialogueId` (:319-320); registers TalkPromptRegistry ≤6m; NEVER
  auto-fires, never one-shots. Unconditional Debug.Log bootstrap instrumentation (:79-85, FTUE-1).
- Status: **WIRED/LIVE during the founding arc only.**

### VillageNpcInjector.cs (DEF-91 Phase 3 — legacy town)
- Scene gate: exact `"Village2"` (:31). Removes baked placeholder AmbientNPCs, instantiates the 4
  People-pack townsfolk at the builder's authored spots (`Defs[]` :47-56: Mevina/Villager/wander,
  Tob/Elder, Merchant/Quartermaster, Blacksmith at the forge — WO-116 warden voices). Normalize
  (:170-178), `NpcGroundSeat.Seat` (:184), bubble counter-scale (:189-190), WO-53 animator culling
  (:162-163), placeholder fallback per slot (:217-241), render-verify (:246-265).
- Status: **LIVE but Village2 is the abandoned raid-target** — never fires in the castle hub.
- ⚠ Still name-based hero lookup with the stale "no Player tag" comment (:267-274).

---

## CODE — Ambient townsfolk core

### AmbientNPC.cs
- `sealed MonoBehaviour, [DisallowMultipleComponent]` (:44-45). One ambient villager: wanders the
  NavMesh or idles; proximity `TownsfolkBubble` with hysteresis (speak 5.5m, +1.5 hysteresis
  :71-75); drives Animator `Speed`/`IsTalking` (WO-163 param-presence cache :163-164, :264-271 —
  the 3,351-error-spam guard); WO-29 archetype tint safety-net for default-white placeholder
  bodies (:770-814).
- `Configure(archetype, wander, homeAnchor)` :174; `SetHero` :187; `SetBubble` :193;
  `SetReducedMotion` :199; `Speaking` :103. wander=false or no mesh ⇒ agent disabled, stands
  ground (:239-243).
- **Combat shelter** (owner 2026-07-06, :105-571): state machine None→FleeStagger(0-1.5s
  scatter)→Fleeing(2.1x speed to nearest `Building` "door", 10s-cached scan :450-483)→Hidden
  (renderers off, object stays active :487-500)→ReturnDelay(3-5s calm)→Returning→resume.
  **Only wander-eligible NPCs flee** (:355 — wander && live on-mesh agent); statics never leave
  their post. Presence census counters + per-transition FlowTrace (:143-154, :326-330).
- **`IsCombatActive`** (:341, public static) — THE shared combat authority: wave `Active` OR
  `Countdown ≤ 5s` (`CombatImminentThreshold` :299 — a long between-wave countdown reads as
  TOWN, the "where are the NPCs" F8 fix) OR `BattleLock.IsInBattle()`; 0.25s shared poll
  (:308-333). Reused by `CastleVendorWaveHider`, `DialogueService.PlayStructure`,
  `DialogueCommandSink.ShopsClosedForCombat` — one poll, never a second signal.
- **Dragon foreshadow** (owner 2026-07-08, `PickSpokenLine` :620-639): near the apex dragon wave
  the spoken line escalates through `TownsfolkDialogue.DragonRumor` tiers (live apex boss forces
  Imminent); urgent tiers always warn, distant tiers alternate with normal chatter.
- ⚠ `ResolveHeroFallback` (:833-841) is name-based with the STALE "project defines no Player tag"
  comment (:829-831) — the hero IS tagged `Player` (CLAUDE.md §7). See RISK LEDGER.
- Status: **WIRED/LIVE** — the body driver every injector uses.

### TownsfolkDialogue.cs
- `static class`, plain data. **enum `Archetype` — numeric values STABLE, do not renumber**
  (:54-76): Trader=0, Villager=1, Guard=2, Child=3, Elder=4, Blacksmith=5, Quartermaster=6,
  Archmage=7, Farmer=8 (serialized by value in AmbientNPC).
- Named wardens (WO-116, `_names` :111-122): Brunhild the Smith, Aldric Quartermaster,
  Archmage Sela, Goodman Harrow.
- `NameFor` :309, `PoolFor` :316, `LineFor` :339 (modulo, never throws), `ArchetypeCount` :349.
- **Dragon foreshadow** (owner 2026-07-08): `enum DragonHintTier` Far/Mid/Near/Imminent (:87-97);
  `DragonWaveId = 4` (:106 — ⚠ a mirrored design constant of waves.json's apex wave; if the
  schedule moves, update it); `TierForWave` :272, `DragonRumorPool` :282, `DragonRumor` :299;
  pools :236-265 (owner-revisable first draft). All strings `// LOCALIZE:` constants.
- Status: **WIRED/LIVE.**

### TownsfolkBubble.cs
- Self-building billboarded world-space speech bubble (TextMesh on quads, no UGUI/UXML).
  `Show(speakerName, line)` :142, `Hide`, `IsVisible`. Single global active bubble
  (`s_activeBubble` :140 — steals the slot :159-161); auto-hide 4.5s (:66); rounded SDF shader
  `DeNelle/UI/RoundedChatBubble` with flat fallback (:314-321); Ignore-Raycast layer (DEF-151).
- Status: **WIRED/LIVE** (unchanged since the prior catalog's verification; no commits since).

### TownsfolkController.cs
- Scene-level coordinator: pushes hero + reduced-motion to child AmbientNPCs (`SetHero` :64,
  `SetReducedMotion` :71, `TownsfolkCount` :78). NOT required — NPCs self-resolve.
- Status: **LIVE but tied to the hand-authored Village/Village2 bake** (added by
  VillageSceneBuilder). The castle injectors configure NPCs directly and never use it.
  Stale "no Player tag" comment :45-47.

### NpcGroundSeat.cs (T-033 — NEW since the old catalog)
- `internal static class` (:35). Shared ground-snap for every runtime-spawned NPC: raycasts DOWN
  through the body, seats the combined renderer-bounds bottom on the real floor collider —
  pivot- and scale-agnostic (`Seat(go, fallbackGroundY)` :58-81, returns the applied deltaY so
  wanderers can hold it via `NavMeshAgent.baseOffset`).
- **Accept band** around the navmesh Y: hits above `+0.4` (`AcceptedFloorBandAboveGround` :42 —
  raised building platforms) or below `-0.35` (`AcceptedFloorBandBelowGround` :49 — the old
  basement plane) are **REJECTED to the navmesh ground, not clamped** (:136-147 — the old clamp
  floated NPCs exactly +0.40m, data-proven 2026-07-10 `[Flow:NpcSeat]`).
- Callers: all five body injectors. Status: **WIRED/LIVE.**

---

## CODE — Talk plumbing (Village → HUD, reflection-free since P23)

### TalkPromptRegistry.cs
- `static class` — O(1) self-registering list of talkable NPCs currently in range (NPCs register
  from their existing in-range check; no new proximity scan — the OuterWorld-leak lesson).
- `Count` :29, `Register(node, talk)` :32 (idempotent, Warn on null args :37-41),
  `Deregister` :50, `NearestTalk(from)` :57 (prunes stale entries, traces empty/hit :64-74).
- Writers: `CastleNpcInteractable`, `CompanionIntroducerInteractable`, `SylasStewardInteractable`.
  Reader: `TalkHudBridge`. Status: **WIRED/LIVE.**

### TalkHudBridge.cs
- DDOL bootstrap (:41-50). **No reflection, no cached HUD instance** (P23 root-cause fix,
  header :7-21 — the old one-shot reflection hook died on every scene swap). Availability =
  edge-triggered `PostureSignals.SetTalkAvailable(TalkPromptRegistry.Count > 0)` on a 0.25s
  poll (:61-79); Talk press = `HudCommands.RegisterTalk(OnTalkPressed)` re-registered every
  scene load (:47-49); press routes to `NearestTalk(heroPos)` (:81-97).
- Status: **WIRED/LIVE.** (The old catalog's "reflection + MaxResolveAttempts 240" description
  is obsolete.)

### PartyHudBridge.cs
- DDOL, 0.5s refresh. **Under `ff.singlehero` (default): hides HUD party slots 1..3 and returns**
  (:60-77, Guard-wrapped reflection invokes). Flag OFF: fills slots from `StoryCompanion.Active`
  (O(1) registry, WO-403 :92-103), roster-gated by `GameState.PartySize` (WO-301 :87-88),
  real Hp/MaxHp (:117-121), fake-null revalidation (:111-115), bare-name portrait key (:123-128).
  HUD resolved via `CoreServices.Hud`, methods by reflection (:156-186).
- ⚠ Header :15-18 still claims companions "have no health... PLACEHOLDER full bar" — FALSE
  (mortal since WO-438-era; the body pushes real HP). Carried-over stale comment.
- Status: **WIRED/LIVE** (as the hide-slots enforcer under single-hero).

---

## CODE — Historical companion stack (dormant under ff.singlehero; intact for flag-off)

Everything below compiles and is reachable ONLY with `ff.singlehero=0` (plus its own gates).
No commits have touched these files since 2026-07-14 except none — verified via
`git log --since=2026-07-14 -- Assets/_Modules/Village/NPCs/` (only Barracks/Vendor/KayKit changed).

### StoryCompanion.cs
- `sealed MonoBehaviour, IDamageableStructure` (:51). Follows, fights (shared `TargetManager`,
  leash 22m), speaks via TownsfolkBubble, mortal (`Hp` :79, `MaxHp` :82, `IsAlive` :85;
  `IDamageableStructure.ApplyContactDamage` :94-96). Static join-ordered registry
  `Active` (:76). Per-class kits (Cleric Mend / Knight Taunt+Bulwark / Ranger Multishot /
  Mage Arcane Burst). `SetSpeechSuppressed` :301 (WO-277). Also reused by the Arena via the
  injector's `SpawnDefender`/`SpawnAttacker`.
- Status: **DORMANT** (no bodies spawn under single-hero) — the class itself has no flag gate;
  the injector is the gate.

### StoryCompanionInjector.cs
- DDOL singleton; hub-gated (`HubScenes.IsHub` :89,119,136,142). **`Spawn()` opens with the
  SingleHero despawn-and-bail** (:149-161). Flag OFF: one body per persisted roster member
  (`_companions` dict :40-41), class-override for story beats (`SetHeroClassOverride` :85-90),
  player-class body suppression (:178-188, "second one ends up being me" fix), real hero meshes
  from `Resources/Heroes/<slug>` via VisualFactory.
- `SpawnDefender` (:375) / `SpawnAttacker` (:445) — WO-389 Arena reuse, `internal static`,
  NOT SingleHero-gated (arena callers own that decision).
- Status: **LIVE as a guard** (its flag branch actively despawns), spawn paths dormant.

### CompanionDialogue.cs
- Static line tables: Grom "Veteran of the Wall" (Knight), Sylas "Scout of the Reach" (Ranger),
  Thrain "Keeper of the Light" (Mage), Elara "Acolyte of the Heart" (Cleric).
  `NameFor/IntroPoolFor/PoolFor/IntroFor/LineFor`. Status: **DORMANT data** (read by the
  dormant beats + injector logs).

### SylasFirstMeeting.cs (WO-238/DEF-180)
- The standalone first-meeting → join beat. `ShouldRun` (:131-164): session guard →
  **SingleHero stand-down (:140)** → `CastleCompanionIntroducerInjector.Active` stand-down
  (:148, single-trigger law) → seen-key `sylas_first_meeting` → FTUE defer (!Onboarded :160).
  Hub-gated (:111). Delegates to `DialogueService.Play("SylasFirstMeeting")` when the node is
  authored + companion is Ranger (:195-200); scripted-bubble fallback otherwise.
- Status: **DORMANT** (double-gated: SingleHero + introducer precedence).

### ElaraWaveThreeJoin.cs
- Second join (Cleric), `JoinWave = 3` via `WaveManager.OnWaveCleared` (:105). SingleHero
  stand-down :125. Carries the Echo-intro (`companion_echo_intro`, WO-360) and gear-offer
  (`companion_gear_offer`, WO-364) sub-beats. Status: **DORMANT.**

### (Grom join beat) — ABSENT
- See "The pivot" above: no file, no seen-key, comments only. If the party pivot ever un-retires,
  the third join must be REBUILT, not re-enabled.

### CastleCompanionIntroducerInjector.cs + CompanionIntroducerInteractable
- The walk-up introducer (owner 2026-06-12) that collapsed the three fragile auto-FTUE intro
  paths. **`Bootstrap()` no-ops under SingleHero (:104-108) — `Active` stays false**, so the
  single-trigger flag is moot in the current game. Flag OFF: hub-gated spawn 5m in front of the
  hero (:163-176), Ranger-Scout body, one-shot key `castle_companion_introducer`, Talk (or
  4m auto-fire :443-448) → `DialogueService.Play("SylasFirstMeeting")` → `<<RecruitCompanion
  Ranger>>` via the command sink. WO-438 stale-registry fix (:411-418).
- Status: **DORMANT.** ⚠ Header comments still say "Yarn node"/"command bridge" — the node now
  lives in dialogues.json and the verb runs through DialogueCommandSink (stale wording, correct flow).

### CompanionGearSetup.cs / GearGrantToast.cs / GearOfferChoiceUI.cs (WO-364 riders)
- Unchanged since the prior verification: `CompanionGearSetup.GrantFor/Apply(HeroClass)` equips
  weapon+armor on the Player-tagged hero's GearLoadout (level-req ignored, story grant);
  `GearGrantToast.Show` — code-built uGUI top toast, 4s, `s_active` singleton (:35);
  `GearOfferChoiceUI.Show(Action<bool>)` — two-button choice, both paths auto-equip in place,
  MinHold 0.4s, 12s auto-choose failsafe, `s_active` singleton (:42).
- Status: **DORMANT** (only caller is ElaraWaveThreeJoin's gear sub-beat).

---

## ASSETS

- `NPCs/Animators/` — `AC_AmbientNPC_Mevina/_Tob`, `AC_Blacksmith`, `AC_Merchant`: People-pack
  locomotion controllers (params `Speed` float + `IsTalking` bool; Idle/Walk/Talk — matches
  AmbientNPC's hashes exactly).
- `NPCs/Materials/` — `MAT_Blacksmith[_Anvil/_Hammer]`, `MAT_Merchant`, `MAT_Peasant_Mevina/_Tob`.
- People-pack body **prefabs** live under `Resources/NPCs/NPC_*` (gitignored Models on a fresh
  clone ⇒ capsule fallbacks fire, by design). KayKit bodies + `KayKitNpcIdle.controller` under
  `Resources/NPCs/KayKit/` are **TRACKED** (WO-818 phase 1).

---

## SCENE GATING MAP (the inconsistency ledger)

| Component | Gate | Notes |
|---|---|---|
| CastleVendorNpcInjector | exact MainCastle_Hall ∨ Main_Castle_Overworld | castle-only by design (never Village2/raids) |
| BarracksNpcInjector | same pair | + BarracksUnlock + blank-town gate |
| CastleTownsfolkInjector | same pair | wanderers castle-only |
| VillageNpcInjector | exact Village2 | the abandoned raid-target town |
| SylasStewardInjector | `HubScenes.IsHub` | + TutorialV2 + !Onboarded |
| StoryCompanionInjector / join beats / introducer | `HubScenes.IsHub` | all SingleHero-dormant |
| TalkHudBridge / PartyHudBridge | none (DDOL, every scene) | registry/roster-empty elsewhere |

⚠ `HubScenes.Names` (`Core/HubScenes.cs:25`) = {Village2, MainCastle_Hall, CastleHub,
CastleHub_MainKeep, Main_Castle_Overworld} and `IsHub` matches by **contains** (:33). So
hub-gated NPC systems (SylasSteward + the dormant companion stack) also fire in **Village2**
(a raid target), while the castle-pair injectors would NOT fire in a hypothetical new hub scene
until `IsCastleHubScene` learns its name (the vendor code itself warns about this drift,
`CastleVendorNpcInjector.cs:569-574`).

---

## RISK LEDGER

### Stale comments vs code (comments lie — do not trust these headers)
1. **`DialogueCommandSink.cs:23`** — "Flag-gated on CustomDialogue (default off)". FALSE:
   `FeatureFlags.cs:145` = `defaultOn: true` (WO-557 made it mandatory). A reader could conclude
   dialogue verbs are dead; they are the ONLY live path.
2. **`PartyHudBridge.cs:15-18`** — "companions have no health... PLACEHOLDER full bar". FALSE:
   the body pushes real `Hp/MaxHp` (:117-121). (Carried over from the prior catalog; still unfixed.)
3. **"no Player tag" comment family** — `AmbientNPC.cs:829-831`, `TownsfolkController.cs:45-47`,
   `VillageNpcInjector.cs:267-268` (+ StoryCompanion/StoryCompanionInjector/SylasFirstMeeting).
   The hero IS tagged `Player` (CLAUDE.md §7; every newer file does `FindWithTag("Player")` first).
   The name-based-only fallbacks still WORK but the rationale is wrong, and hero-resolution
   strategy remains inconsistent (tag-first vs name-only) across the area.
4. **`CastleCompanionIntroducerInjector.cs` header (:25-33)** and `ShopPanel.cs:9` — still speak
   of "Yarn node" / "command bridge" / "NPCCommandBridge". Yarn is removed; the flow is
   dialogues.json + DialogueCommandSink.
5. **`CastleVendorNpcInjector.cs:1-30` header** — "wired to the existing YarnSpinner structure
   dialogue... parameterized Yarn structure dialogue". The mechanism is now
   `DialogueService.PlayStructure` → custom runner/panels. Flow correct, wording stale.

### Dead / superseded / absent
6. **NPCCommandBridge**: dead (no class in tree). **Grom join beat**: absent entirely (see pivot).
   **SylasFirstMeeting / ElaraWaveThreeJoin / CastleCompanionIntroducerInjector /
   StoryCompanion(+Injector) / gear trio**: dormant behind ff.singlehero — do NOT RCA-fix
   companion reports against this code; nothing here runs in the shipped configuration.
7. **TownsfolkController**: alive but only reachable via the hand-authored Village bake; unused
   in the canonical castle hub.
8. The prior catalog documented **GromOuterWorldReturnJoin.cs as a verified live file** — it never
   existed in git history. Treat pre-2026-08 catalog claims about the join arc with suspicion.

### Design/data gaps (real, confirmed in source)
9. **`TownsfolkDialogue.DragonWaveId = 4`** (:106) is a hand-mirrored constant of waves.json's
   apex wave — a schedule change silently skews every foreshadow tier (documented in-source, no test).
10. **arcane-tower** vendor role still has no ResourceBuildingProgression def/portrait
    (`CastleVendorNpcInjector.cs:98-104` — degrades gracefully; known data gap). Under WO-818 it
    DOES get a KayKit body (Mage) via the catalog row.
11. **Jeweler Lever-1 fallback position is a hardcoded owner-tunable**
    (`CastleVendorNpcInjector.cs:477-485` — beside Marketplace_Monetization or (12,0,32)).
12. **StoryCompanion.TryClericMend** still `FindObjectsByType` scans per cast (:464/:479 region) —
    counter to the WO-403 registry direction; moot while dormant, a perf tripwire on flag-off.

### Behavior seams worth knowing before touching anything
13. **Vendor doubles class of bug is closed by THREE cooperating rules** — pass-0 fallback defer
    (:368), per-building `VendorSeatMarker` idempotency (:597-603), baked-stray eviction on
    placement (:638-654). Removing any one re-opens F8 seq524/2026-07-30. The 2s poll + the
    placement hook + the pending-queue drain are ONE spawn path by design (all via `SpawnVendor`).
14. **`AmbientNPC.IsCombatActive` is the single combat authority** for NPC hide/flee AND the
    shop-close gates (sink + PlayStructure + wave hider). Countdown counts as combat only within
    5s of the wave (:299). Inventing a second combat poll here is the anti-pattern the code
    repeatedly warns against.
15. **KayKit failure semantics are load-bearing** (WO-818 acceptance): un-authored ⇒ silent People
    fallback; authored-but-broken ⇒ exactly one Warn. Adding warn-spam or a hard fail to
    `KayKitNpcBody.Load` breaks the contract the F8 harness expects.
16. **`ArmIdle` must only run on KayKit bodies** (`kayKitRes != null` at both call sites) —
    arming a People body would stomp its real Speed/IsTalking controller with the single-state idle.
17. **Blank-town gate (WO-834)** — both the vendor Lever-1 fallback (:445-450) and the barracks
    poll (:124) consult `StructureSingleton.MayBakedTwinSurface`; bypassing it refurnishes a
    Build-Your-Own save's deliberately blank town every 2s.
