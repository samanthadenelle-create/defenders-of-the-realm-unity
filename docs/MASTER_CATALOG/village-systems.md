# Master Catalog — Village Systems

**Dated 2026-08-02. Verified from source (file:line cites), NOT from comments.**
Supersedes the 2026-06-12 edition. Scope: `Assets/_Modules/Village/` subfolders
**BuildMode, Buildings, Walls, Catalog, Arena, Harvest**, plus root
**HubStructureVisualInjector.cs**. (Tutorial/FTUE, Combat-folder tells, EconomyService and
the root economy are catalogued in their own areas now; the 2026-06-12 sections for them are
retired from this file — see git history for the frozen text.)

- **Assembly:** everything below is `DeNelle.Village` (`DeNelle.Village.asmdef`).
- **Namespaces:** `DeNelle.Village` (BuildMode/Buildings/Harvest/Catalog + root),
  `DeNelle.Village.Arena`, `DeNelle.Village.Buildings.Progression`, `DeNelle.Village.Walls`.
- Line counts are `wc -l` on 2026-08-02.

---

## 0. Owner rulings this area is built on (RECORD — binding)

| Ruling | Date | Where enforced |
|---|---|---|
| **Strategic placement is ALWAYS ON** — `ff.strategicplacement` REMOVED (WO-682). New games set the migration marker in `ResetToNewGame`; legacy saves migrate once. | 2026-07-12 | `StrategicPlacementMigration.cs:3-4,49-52` |
| **Destroyed = build fresh at FULL COST.** No repair-of-destroyed, no in-place respawn, no free rebuild (the first-build-free ledger is BURNED on destruction). Supersedes the WO-672 "inoperable shell". | 2026-07-19 + F8 2026-07-30 | `Vfx/Destructible.cs:131-189,210-231` (WO-753) |
| **Singleton-ness is CATALOG-DRIVEN, one authority.** "THERE SHOULD ONLY EVER BE ONE" → a new catalog row with `repo.singleton=true` (+ `repo.bakedTwins`) is fully enforced with ZERO code; the v1 per-building code map is DELETED. | 2026-08-01 | `BuildMode/StructureSingleton.cs:1-17` (WO-819 v2) |
| **Blank founding = truly blank.** On a migrated save a baked twin surfaces only for ids in `GameState.EverBuiltStructureIds` (monotonic; selling never removes). | 2026-08-02 | `StructureSingleton.cs:156-198`, `GameState.cs:518-545` (WO-834, save v36) |
| **Fresh hub: baked stores PRE-STAND, visible + staffed** (CoC/WWCD, "Lever 1") — baked standdown keys on `HasRecord` alone (+ the WO-834 gate), never on has-catalog-row. | 2026-07-24 | `StrategicPlacementMigration.cs:182-208` |
| **ONE true gold button per panel** (WO-832) — the upgrade panel's right-pane CTA is the only gold-filled commit; tabs get a gold underline, cards only select. | 2026-08-02 | `Buildings/Progression/BuildingUpgradePanelMvvm.cs:20-38,91-94` |
| **WO-830 Echo affinity/synergy IMPLEMENTED (pending gates)** — all 6 Echoes prefer Harvest with distinct affinities; the PLAYER picks each Echo's resource (5-chip picker; affinity = match bonus, never a lock); **Maren harvests Crystals** (Bran+Maren = the one doubled affinity, combined trickle slowest); 3 disclosed pair synergies + a hidden tri-synergy (applied-only, NEVER displayed). WO-831 emergence sprite beat wired (art pending, Guard fallback). | 2026-08-02 | `EchoRosterCatalog.cs` / `EchoBonusCalculator.cs` / `EchoAssignments.cs` / `echoes-balance.json`; specs `WorkOrders/WORK_ORDER_830_*.md` + `_831_*.md` |

---

## 1. BuildMode/ — the CREATE verb (place / move / sell / persist)

CoC-style grid building, wired end-to-end. Enter → angled-overview cam + frozen waves →
palette card → info panel → arm → ghost → **two-step commit** (a world tap only DROPS the
pending ghost; the PLACE button is the only commit — `BuildModeController.cs:9-14`) → charge
after commit → append `GameState.BaseLayout` + `MarkEverBuilt` → replay on load via
`BaseLayoutLoader`.

| Class | File (lines) | Responsibility / key API (verified) |
|---|---|---|
| **BuildModeController** | BuildModeController.cs (3477) | MB singleton. Entry/exit + the whole placement loop. `Instance`; static `event BuildModeChanged` (:53) + **`event StructurePlaced(string id)`** (:59 — the LIVE placement signal; tutorial + StructureSingletonBootstrap ride it); `IsActive`; `HasArmedEntry` (:65); camera pan/zoom/orbit w/ 45° yaw detents (:106-111); single-pointer drag-pan for WebGL (:93-105). Commit appends BaseLayout **and calls `state.MarkEverBuilt(_armed.id)`** at the same seam (:1816-1822, WO-834), raises StructurePlaced guarded (:1827), spawns the vendor NPC for the placed storefront (:1829+), then returns to the carousel via `CancelArmed(afterPlacement:true)` (:1854-1864). `IsSingletonBuilt(entry)` (:1909-1918) **delegates to StructureSingleton.IsBuilt** — the bespoke body is deleted; `SingletonAlreadyBuilt` traces the arm/place refusal (:1920-1925). Sell: `SellSelected` frees grid, `RemoveLayoutEntry(itemId, cell)` (:2054, :2755-…), `BaseLayoutLoader.Forget`, cancels its BuildTimer job (:2059-2061). |
| **StructureSingleton** *(+ StructureSingletonBootstrap)* | StructureSingleton.cs (550) | **THE one authority for only-ever-one (WO-819 v2 + WO-834).** Static. `IsSingleton(id)` = catalog `repo.singleton` (:97-102). `IsBuilt(id)` union cheapest-first: BaseLayout record → ACTIVE baked twin (`GameObject.Find`, :121-124) → live `PlacedStructure` → live `Building.IsAlive` (:110-138); per-frame memoized `IsBuilt(CatalogEntry)` for palette polls (:144-153). **`MayBakedTwinSurface(id, everBuilt, migrated)`** — the pure WO-834 blank-town rule (:176-186): pre-migration → true (bake owns the town); migrated → true iff id ∈ EverBuiltStructureIds. `Enforce(id)` (:215-264): placed exists → stand twins down ("placed wins"), UNLESS the WO-673 migration latch holds (`IsManagedId && !StanddownActive`, :231-237); nothing remains → resurface twins (post-sell) unless the blank-town gate is closed → then twins are actively STOOD DOWN (:251-259). `EnforceAll()` sweeps every singleton catalog row per hub load w/ surfaced/suppressed tally (:271-284). Event seam: `NotifyPlaced`/`NotifyRemoved` → `SingletonResolved`/`SingletonReleased` (:74-80, :291-316). Baked twins come **ONLY from catalog `repo.bakedTwins`** (:420-425). Barracks twin resurface routes through `HubStructureVisualInjector.EnsureBarracksSurfaced` so the WO-724 unlock gate holds (:354-364). Bootstrap (:445-549): DDOL, castle-hub scenes only (`MainCastle_Hall`/`Main_Castle_Overworld`, :452-453), waits ≤300 frames for GameStateService, subscribes StructurePlaced once. |
| **StrategicPlacementMigration** *(+ Bootstrap)* | StrategicPlacementMigration.cs (474) | WO-673 L3, **always-on since WO-682**. (a) **One-shot migration writer** `RunIfNeeded()` (:276-361): converts the 8 baked storefronts (`BakedRows`, :85-95 — bakedName→itemId census incl. workshop=weapons-Forge / forge=Armorer trade convention) + 2 runtime stations (`StationRows`, :110-114, hardcoded fallback (±11,0,2)) into grid-quantized BaseLayout records (≤~1.5 m accepted snap drift, yaw→nearest 90°, :387-399), then the **WO-834 template grant**: marks the whole template + `barracks` ever-built (:339-346), sets `StrategicPlacementMigrated`, latches the scene handle, saves. (b) **Standdown oracle**: `StanddownActive` (:157-173 — marker set AND home hub AND not-the-migration-load); **`StanddownActiveForBaked(bakedName, out itemId)`** (:195-208) = `StanddownActive && (HasRecord || !MayBakedTwinSurface)` — Lever-1 HasRecord-only + the WO-834 second clause so a blank founding loads blank at scene load; `StanddownActiveForStation` = unconditional when active (WO-703/BLANK-1, :222-225); `ShouldReplayRecord` — managed ids replay only under standdown (:234-238). Lever-1 read-only census accessors `BakedStorefronts()`/`StationAnchors()` feed CastleVendorNpcInjector (:132-149). |
| **BaseLayoutLoader** *(+ BaseLayoutLoaderBootstrap)* | BaseLayoutLoader.cs (595) | Runtime twin of VillageSceneBuilder. `LoadFromState` once per instance (`_loadedOnce` latch, :128-168; hub-skip set now only legacy `CastleHub`, :54-57). `Rebuild` (:202-250) applies the WO-673 replay filter (withheld-count telemetry) + partial-base Warn. `Spawn` (:258-407): CatalogRegistry → `StructureFactory.Create` (Guarded), yaw = `yawSteps*90 + yawOffset` (:280), persisted `worldY` seats wall-walk defenses (:276), TWO footprints (blocker=unrotated vs grid claim=rotation-honest AABB, :299-313), **walls/gates get the "Structure" physics layer** so tower LoS linecasts hit them (:325-334), `wallMounted` grants +25% elevation range mult (:353-360), DEF-208 tier reskin + `ApplyTierStats` for level>1 (:363-376), re-arms `UnderConstructionVisual` for mid-build saves (:386-388), reload-notifies the vendor injector (:401-404). `AddFootprintBlocker` (:416-512): blocker sized to rendered bounds ×0.85 eave-inset, clamped [1 cell..claim]; NavMeshObstacle carve (no rebake); strips all child colliders — root footprint box is the ONE collider-of-record. Bootstrap (:538-594): DDOL, ensures a loader whenever `SceneRouter.Castle` loads (F8-39 towers-vanish fix). |
| **GhostPreview** | GhostPreview.cs (402) | Translucent green/red ghost via VisualFactory; MPB tinting; tracks created materials and destroys them in `Clear()` (no per-re-arm leak, :41-44). API: `SetEntry` (:63) / `SetPlaceholder` (:142) / `MoveTo(pos,yawSteps|yawDegrees)` (:171/:176) / `SetValid` (:185) / **`SetReason(msg)`** (:206 — the floating "why it's red" world-space label, owner 2026-07-24) / `CurrentPosition` (:231) / `Hide`/`Clear`. Applies the entry's OrientationFix under the yaw (WYSIWYG, :36-39). |
| **PlacedStructure** | PlacedStructure.cs (177) | Runtime marker per placed structure: `itemId, gridCell, footprint, yawSteps, yawOffset, level, worldY, wallMounted, sellValue`, `TierVisual`. |
| **PlacementGrid** | PlacementGrid.cs (317) | Shared cell-occupancy grid singleton; `WorldToCell/CellToWorld/FootprintCells(metres[,yaw])/Occupy/Free/ClearAll`. |
| **ObsidianQueueHud** | ObsidianQueueHud.cs (534) | WO-773/778 — the tucked-away **work-queue modal** (player-facing "Queues"): per-channel active slots + FIFO pending with live 1 s timers for **Builders / Training / Research** (:63-68), never a mixed list. Code-built uGUI on ElarionUiKit; self-installing DDOL (:73-80). Opened via `ObsidianQueueGate.RequestToggle()` (Core seam, `Core/UI/ObsidianQueueGate.cs:26` — HUD never references Village); repaints on `BuildTimerService.QueueChanged`. Colorblind-safe ASCII state markers (`>` running / `...` queued / `-` free, :26-28). Sell-time Instant / Ad-skip / Buy-slot buttons call existing BuildTimerService APIs. |
| **UnderConstructionVisual** | UnderConstructionVisual.cs (258) | WO-612 scaffold visual keyed by `KeyFor(data)`; attached at place + reload while `BuildTimerService.IsBuilding`. |
| **StructureTierVisual** | StructureTierVisual.cs (112) | DEF-208 per-tier visual (bronze/silver/gold or per-tier model). |
| Palette/UI set | BuildPaletteUI.cs (880), BuildPaletteVM.cs (182), StructureCardVM.cs (246), BuildStructureInfoPanel.cs (369), BuildPreviewModal.cs (531), BuildSelectionUI.cs (227), BuildHudController.cs (349), BuildTabRow.cs (128), BuildWalletRow.cs (168), LiveWalletSource.cs (108), BuildPlaceButton.cs (98), BuildFeedbackToast.cs (194), SiblingPanelSettings.cs (48) | Code-built palette/carousel + card VMs (singleton cards render "Built" via `IsSingletonBuilt`), structure info preview, move/sell/upgrade action panel, browse/placing intent HUD, wallet row, reject-reason toast. |
| Input seam | IBuildInput.cs (93), DesktopBuildInput.cs (98), LeanTouchBuildDriver.cs (364) | S6 seam; LeanTouch driver is still the ONLY Lean.Touch file in BuildMode; WebGL falls back to the controller's single-pointer drag-pan. |
| Bridges | BuildButtonBridge.cs (136), BuildModeHudBridge.cs (96) | HUD→Toggle wiring by reflection (Village can't ref HUD); combat-HUD hide while building. |
| **RotationCorrectionRegistry** | RotationCorrectionRegistry.cs (149) | WO-282 per-prefab persistent yaw correction (PlayerPrefs). |

**Placement-ownership seam in one breath:** bake/injector own a structure until the one-shot
migration writes records; the NEXT hub load flips `StanddownActive` → records replay + bakes
hide. `StructureSingleton.EnforceAll` then keeps the invariant every hub load: placed wins →
twins down; sold-to-nothing → twins resurface (if ever-built); never-built on a migrated save →
twins suppressed (blank town). Three cooperating gates, one owner each — do NOT add a fourth.

---

## 2. Buildings/ — timers, towers, progression, the upgrade panel

### 2.1 BuildTimerService — the multi-channel "Obsidian" queue (WO-172 → WO-773)
`Buildings/BuildTimerService.cs` (786). ONE DDOL service, **channels Builder / Train /
Research** (`Core/Jobs/JobKind.cs:63 ChannelId`) that never share slots (CoC parallel
workers). Queue math is the pure `DeNelle.Core.Jobs.ObsidianQueueEngine`
(`ObsidianQueueEngine.cs:33`, headless-testable); this MB owns `GameState.ObsidianQueue`
persistence + wall-clock `TimeSource.NowUnixMs()` → **offline-fair**: on load
`SweepAllChannels` completes overdue jobs and cascades pending pulls (:94-105). Legacy
`GameState.BuildJobs` folded into Builder by the v34→v35 migration (:28-33). API (verified
lines): `SlotCount`/`BuySlot` (:159/:168), `ActiveJobsOf`/`PendingJobsOf` (:188/:195),
`StartBuild`/`StartUpgrade` (:217/:226), generic `Enqueue(kind[,channel],targetId,duration)`
(:305/:309), `IsBuilding`/`RemainingSeconds`/`Progress` (:342-356),
`InstantFinishPrice`/`CanWatchAdToSkip`/`WatchAdToSkip`/`TryInstantFinish` (:368-413),
`CompleteJob`/`CompleteChannelJob` (:457/:460), `RepointJob` (:498),
`CancelJob`/`CancelChannelJob` (caller-owns-refund, :530/:533), `ReorderPending` (:562).
Events `JobCompleted/JobStarted/JobSkipped/QueueChanged` (:59-68). Also seeds
`RaidEntryGate.ArmyStatus` once GameState exists (first-frame flash fix, :111-120).

### 2.2 Towers (two generations + combat/LoS)
- **DefenseTower.cs** (973) — the LIVE auto-firing defensive structure. Role-priority
  targeting; `TowerAllegiance {PlayerOwned, EnemyOwned}` (:28-30 — garrison turrets target the
  player party through the same IDamageableStructure seam). **LoS gate:** acquisition + fire
  are blocked when a wall/gate on the **"Structure" layer** sits between muzzle and target —
  `Physics.Linecast(fPos, target, LayerMask.GetMask("Structure"))` (:500-530); fliers exempt
  (:518). Layer applied by `BaseLayoutLoader.Spawn` (:325-334) for placed walls/gates and by
  `WallSegment.RebuildCollider` for configured ones. `ElevationRangeMult` (wall-walk +25%) set
  from the persisted `wallMounted` flag. Breaks at 0 HP → `Destructible.NotifyBroken` (removal,
  §2.5); repair path via `Repair()`.
- **ArcaneTower.cs** (565) — the magic tower variant (element default from catalog;
  aura VFX owned via Destructible).
- **TowerCombat.cs** (617) — shared firing math incl. `BlockedByWall` (the LoS mirror).
- **Tower.cs** (1404) — the **legacy DEF-74/75** placement-tower component
  (TowerPlacementSystem lane): level 1-3 visual swap, upgrade VFX, reflection-based camera
  shake (:31-33 — grandfathered; §10 forbids NEW reflection). Layer fallback
  `"Tower"→"Building"` (:514-515). Kept for the legacy TowerPlacementSystem/BuildMenu path —
  not the BuildMode path.
- Support: TowerPlacementSystem.cs (427, legacy placement), TowerSwapMenu/Service,
  TowerConstruction(Queue), TowerPersistenceService (161), TowerRangeRing, TowerDamageVisuals,
  ProjectilePool/PooledProjectile, ProjectileVFXCatalog (408) / ProjectileArtCatalog (207),
  StructureBurn (413), StructureAttackAlert (266), TowerLoopDevHarness (200).

### 2.3 Progression/ (ns `DeNelle.Village.Buildings.Progression`)
- **ResourceBuildingProgression.cs** (597) — data-driven leveling for Farm/Lumbermill/Forge;
  balance table still **hardcoded in `Build()`** (:233), NOT JSON. Contains the
  Progression-namespace `ResourceCost(HarvestResource, int)` — the OTHER ResourceCost (see
  Risk ledger). `ResourceBuildingState.cs` (234) runtime levels + `TryUpgrade`;
  `TechTree.cs` (84) Magic-gated nodes (`arcane_forge`).
- **BuildingUpgradeVM.cs** (694) — the PURE ViewModel (IPanelViewModel, no UnityEngine.UI;
  `BuildingUpgradeVM.cs:10-16`). `CreateDefault(buildingId, onClose)` resolves economy +
  default building (:93-104); collector-id normalization (`collector_farm`→`farm`,
  :108-110); serves BOTH families — city tier ladder (`BuildingTierCatalog` →
  `BuildingUpgradeService.TryUpgrade`) and legacy resource buildings
  (`ResourceBuildingState.TryUpgrade`) (:18-23); per-tile `CostFor/EffectFor/KeyLine/Gate`
  maps (:74-86, gates `village`/`building-tier`/`cost`); synthetic `villagetier` row routes
  to `VillageTierService.TryUpgrade` (:47-51). Disambiguates the two ResourceCosts via
  `using EcoCost = DeNelle.Village.ResourceCost` (:35).
- **BuildingUpgradePanelMvvm.cs** (1508) — the VIEW, **reworked 2026-08-02 (WO-832 "one true
  button")**: master-detail per the owner-approved 060241 mockup — LEFT horizontal tier-card
  path (cards ONLY select; no in-card gold commit, :22-28), RIGHT detail pane whose big gold
  Upgrade CTA is **the one bright-gold commit on the panel** (:37-39); tabs
  Upgrade|Skills wear a gold UNDERLINE when selected, never the CTA's gold fill (:20-21,
  :91-94). Clean obsidian palette built in-code (:58-68), no UXML, no BuildObsidianPanel.
  Render-dedup via content-signature hash (economy ticks no longer rebuild the tree,
  :99-108). Flag `FeatureFlags.BuildingUpgradePanel` (default ON since WO-476) = kill-switch.
  Spawned by `BuildingUpgradePanelMvvmBootstrap.cs` (85).
- Collectors: ResourceCollector (341) + Service/Registry/Bootstrap, CollectorStackView (437)
  + CollectorStackPropCatalog, ResourceBuildingHarvester (149), CompletedUpgradeApplier (113),
  AutoHarvestService (54), BuildingPerkService (77), VillageTierService (75).

### 2.4 Building interaction / misc
Building.cs (366, `IsAlive`/`BuildingId` — one of StructureSingleton's truth probes),
BuildingInteractable (635), BuildingCatalog (235), BuildingSign(+Injector), InteractableSign,
MarketplaceInteractor, NPCUpgradeStation(+VM), HealingFountain (633), CrystalMine (638) +
CrystalVisual/CrystalVfx, DungeonPortal (299), Door/Drawbridge/CastleDoor controllers,
MobileInteractButton (371), BuildMenu (714) + BuildMenuVM/BuildMenuHudBridge (legacy),
UI/ (TowerManagerPanel, LevelUpSkillPopup(+Bootstrap), TowerUpgrade/Empower buttons,
PlacedTowerListVM, LevelUpVM, ProgressBar, Billboard), DailyQuestTowerBridge,
EmpowermentDebugTrigger.

### 2.5 Destructible (in Vfx/, catalogued here — WO-753)
`Vfx/Destructible.cs` (285). **The ONE owner of a destructible's death lifecycle.**
`Ensure(go)` composed by Building/DefenseTower/ArcaneTower Awake (:58-66). `TeardownVfx`
(:94-129): pool-return held VFXHandles, stop+disable ArcaneAuras, destroy registered roots —
also from `OnDestroy` (catch-all, :277-282). **`NotifyBroken`** (:150-189) executes the full
ruling: VFX teardown → despawn the bound vendor (VendorSeatMarker, real-null checked,
:163-170) → free grid + `BaseLayoutLoader.Forget` + drop the persisted record (:176-183) →
**`BurnFreeBuild(itemId)`** (:210-231 — destruction burns `GameState.FreeBuildsUsed` so a
baked structure's rebuild is never free, owner F8 2026-07-30) → toast "rebuild at full cost"
(`OfferRebuild`, :258-268; interactive confirm deferred) → `Destroy(gameObject)`.
NOT a damage model — never reads HP (:27-29).

---

## 3. Walls/ (ns `DeNelle.Village.Walls` for tier data; segments in `DeNelle.Village`)

| Class | File (lines) | Responsibility |
|---|---|---|
| **WallSegment** | WallSegment.cs (231) | One perimeter section; `IDamageableStructure`. Tier 1-3 with toughness `{1, 1.6, 2.56}` — incoming contact damage DIVIDED by it (:57-65). `Configure()` by VillageController; BuildMode-placed walls attach a bare WallSegment (no Configure) — the Structure layer comes from `BaseLayoutLoader.Spawn:325-334`. |
| **WallTierData** | WallTierData.cs (215) | The CoC wall ladder DATA only: naming Wood→Iron→Reinforced Steel, per-tier mesh prefab path (**placeholders, owner meshes pending**), upgrade cost (Iron, then Iron+Crystals). Durability deliberately NOT duplicated — `ToughnessFor()` reads WallSegment's table (header :6-20). |
| **WallRepairController** | WallRepairController.cs (1057) | The player repair LOOP (scan→select→modal prompt→confirm). **Repair cost ruling 2026-07-11:** damage-fraction × the structure's own BUILD cost in its own materials; Crystals never spent on repair (header :13-22). Spends via EconomyService.TrySpend + the GameState Wood/Iron mirror (GrantSpendable both-sides pattern). HUD cross-wired by reflection (module isolation). NOTE: "destroyed = rebuild at full build cost" here predates WO-753 — a destroyed structure is now REMOVED by Destructible, so the repair loop only ever sees damaged-but-standing structures. |
| Support | RepairTarget (290), RepairHighlight (285), HubRepairAffordance (341), WallRepairHudBridge (242), WallRepairStrings (75), WallLayout (402) | Selection wrapper, amber/violet highlights, hub affordance, HUD bridge, strings, square-ring layout math. |

---

## 4. Catalog/ — the data-driven structure buckets

| Class | File (lines) | Responsibility |
|---|---|---|
| **StructureFactory** | StructureFactory.cs (864) | **The ONE creation path** (WO-148): editor bake, runtime placement, and save replay all call `Create(entry, pose, parent)` (:78). Runtime-safe (no UnityEditor). WO-764 Y-height normalization: `YHeightVariable = 4f` × `repo.heightMul` (towers 1.25, siege 0.75) — one number rescales the whole town (:42-68). **behaviorId → component is an explicit switch, add-cases-not-reflection** (`AttachBehaviorImpl`, :680; cases `DefenseTower/ArcaneTower/WallSegment/Gate/ResourceCollector/CrystalMine/HealingFountain/GameplayBuilding`, :684-787; building-type map :836-859). Also `MeasureUprightFootprintMetres`, `ReskinForLevel` (DEF-208), Composite→`CreateGroup` (:90-94). |
| **CatalogBootstrap** | CatalogBootstrap.cs (228) | Fills CatalogRegistry from **`Data/Canonical/structures-catalog.json`** (StreamingAssets source, Resources copy WINS on WebGL) via CanonicalJson; tiny 2-tower hardcoded fallback only if JSON fails. **Live JSON `version: 6`** with 12 `repo.singleton:true` rows, 9 carrying `repo.bakedTwins` (verified 2026-08-02): fountain_healing, pet-house→EchoHollow, workshop→Blacksmith_Weapons, market→Marketplace, mill, forge→Forge_Armor, armorer, jeweler→Jeweler_Gems (twin tolerated-absent — removed from the bake), arcane-tower→ArcaneTower_MagicUpgrades, collector_farm→Windmill, collector_lumbermill→Lumbermill, barracks→CastleBarracks. Overlays `StructureOrientationLocalStore` (local wins). |
| **BuildCategoryRegistry** | BuildCategoryRegistry.cs (244) | BuildType→CatalogType palette mapping from `build-categories.json` (Town/Defenses/Walls/Collector), data not switch. |
| **StructureOrientationLocalStore** | StructureOrientationLocalStore.cs (136) | Orient-tool dials upserted to `persistentDataPath/structure-orientations.json`; overlaid at startup so owner dials survive sessions/rebuilds; `[OrientRecipe]` console line = the bake-into-repo source. |

---

## 5. Arena/ (ns `DeNelle.Village.Arena`)

Two generations coexist deliberately:

- **BattleArena.cs (2394)** — the **generic real-time battle spine (WO-482)**, LIVE. PvE
  entry `BeginEncounter`: builds an open kite arena (floor + runtime NavMesh, NO structures)
  staged at `ArenaCentre = (5000, 0, 5000)` (:81), `ArenaWorldRadius = 200` (:85) with
  `IsArenaPosition` so shared systems can tell arena hits from home-scene bleed-through
  (:87-90); warps hero in (south) vs enemy family (north) via the shared
  EnemyFactory/EnemyBrain; real-time fight through the EXISTING combat stack (zero new combat
  code); win/lose/flee → `BattleRewardSummary {Xp, Wisdom, Wood, Iron, GearName}` (:58-70) →
  return warp under ScreenFader. Logic-only (presentation reused, header :22-25). Flag
  `FeatureFlags.OverworldEncounter`. Companions: BattleArenaHud (uGUI result/HUD),
  BattleHud9Zone, BattleStarRating, ArenaDeathCam, ArenaBiomeDressing, EncounterParams,
  ArenaVM/ArenaPaletteVM.
- **ArenaMode.cs (476)** — the verified **async-PvP raid loop** (ARENA MVP WO-386/388/389/390),
  kept working untouched; generalization onto BattleArena is extraction-later by design
  (BattleArena.cs:8-11). Herald entry (ArenaHeraldSpawner 445), defense setup + attack recruit
  controllers, hardcoded seed catalogs (ArenaCatalog 3 opponents / ArenaDefenseCatalog 6
  defenders, pool 50 / DefensePatternLibrary — all still `TODO data-driven`), ArenaPanel,
  ArenaProgressStore, ArenaNavMeshBaker (player-castle path only), ArenaHudBridge (staged
  battle hides home HUD; restore under return fade).
- **ArenaWalletService.cs (127)** — **STILL the client-side SKR wager STUB**: PlayerPrefs key
  `dotr-arena-skr-balance`, seed 500 (:38-41); swap point isolated to
  `Debit/Credit/Balance` (header :10-17). Never trust for real funds; real wallet =
  WalletService(CurrencyKind.Skr) + backend escrow (not built).

---

## 6. Harvest/ — Echo workforce + offline accrual

### 6.1 The Echo faucet (LIVE — WO-830 affinity/synergy implemented 2026-08-02, pending gates)
- **EchoService.cs** — owns EchoCount (1..`MaxEchoes=6`, wave-earned every
  `WavesPerEcho=5`), the pooled SILO (hours-capped buffer), Dump-to-wallet, unlock events.
  Persisted in GameState (EchoCount/SiloResources/WavesCompleted, schema v25+). Income:
  `RatePerSecond = EchoCount × (BaseRatePerHour/3600) × EchoBonusCalculator.AggregateHarvestMultiplier() × (1 + HarvestRateBonus())`
  (the count-quadratic spine folded ONCE, never `GlobalHarvestMultiplier` too);
  `SiloCapacity = SiloCapHours × BaseRatePerHour × EchoCount × AggregateHarvestMultiplier()`
  (WO-830 reconcile — capacity now shares the rate's multiplier basis so fill-time ≈
  SiloCapHours; the STEWARD talent factor stays excluded). Offline accrual shares ONLY the
  clock (`GameState.LastHarvestClaimMs`) with OfflineHarvestService — separate faucets, no
  double-grant. **Dump is a 5-WAY split** (WO-830): Wood/Iron/Food/Gold/Crystals by
  `HarvestTargetWeights` (largest-remainder, exact pool); Wood/Iron/Food+Crystals bank via
  `EconomyService.GrantSpendable(..., crystals:)`, Gold via `EconomyService.AddCoins`.
  Founding-echo teaching one-shot `FoundingTaughtKey`.
- **EchoBonusCalculator.cs** — the ONE place the curve lives (WO-738/830);
  `AggregateHarvestMultiplier()` = count × (1 + per-echo [base + affinity-match + level]
  + running PAIR bonuses + 6-set + **hidden tri-synergy** when ALL pairs run — the tri term
  is APPLIED-ONLY, excluded from `ReadoutFor().BonusPct` and every displayed string
  (WO-830 §3d; FlowTrace edge-logs activation)). Match law = ASSIGNED resource == affinity.
  `HarvestTargetWeights()` (5-way, by actual assignment) replaced the old 3-way
  `HarvestResourceWeights`. `SynergyFor()` = the DISCLOSED pair readout for the card.
- **EchoRosterCatalog.cs** — the fixed 6-spirit code table; order == EchoCount; each Echo =
  the awakened essence of a soul the Heart guards (WO-752). **WO-830 identity rows: ALL SIX
  `PreferredLane = Harvest`** with affinities Aldwin/Frost→**Food**, Elowen/Nature→**Wood**,
  Corvin/Shadow→**Gold**, Bran/Storm→**Crystals**, Doran/Earth→**Iron**, Maren/Fire→
  **Crystals** (the one deliberately doubled affinity; Repairs removed 2026-08-02).
  New `HarvestTarget` enum + token/label helpers; WO-831 `EmergeLine` per entry +
  `LoadEmergence` (Guard-wrapped, portrait/text fallback).
- **EchoAssignments.cs** — per-echo token storage in `GameState.EchoLanes` CSV. **WO-830
  grammar:** `<resource>:<level>` (wood/iron/food/gold/crystals — the primary form, resource
  preserved) alongside `<lane>:<level>` + `idle`; a v33 generic `harvest:N` defaults on read
  to the echo's AFFINITY resource; pre-v33 bare wood/iron/food read at that resource L1. NO
  schema bump (grammar documented on `SaveSchema.PersistedState.EchoLanes`).
  **`PickableLanes = { Harvest }` only** (dead Crafting chip removed);
  **`PickableResources` = the 5 targets** the card's resource picker offers.
  New APIs: `ResourceTokenOf` / `TryTargetOf` / `AssignHarvest` / `ResourceLabelFor`.
- UI/feedback: EchoWorkforceHud + VM + Bootstrap, EchoRosterView +
  EchoRosterVM/EchoCardView/EchoCardVM (**WO-830: the card is a 5-chip RESOURCE PICKER** —
  affinity disclosed as "Favors: X" + the "(best -- this Echo's calling)" text flag; the
  disclosed pair-synergy line via `SynergyText`; hidden tri never worded), EchoUnlockFeedback,
  **EchoUnlockDialogue (WO-831: two-state — 2D EMERGENCE beat first (`Resources/Echoes/
  Emergence/<PortraitName>_emerge.png`, LFS; CanvasGroup fade-in; missing art degrades to
  portrait→text, never blocks) then Continue → the awakening card)**, EchoWispInjector,
  EchoSpiritPresentation, EchoWaveUnlockBridge, EchoBalanceCatalog (owner-tunable knobs,
  echoes-balance.json: WO-830 `preferredLaneMatchBonus 0.40`, 3 `crossBonuses` pairs
  Provisions/Forge/Fortune @ +0.10, `hiddenTriSynergyBonus 0.25`, Bran+Maren rates 0.45 each
  so the combined crystal trickle stays the slowest faucet).
- Oracles: `EchoSpecializationRegression` (affinity table, balance dual-copy, token grammar,
  pair/tri math incl. applied≠displayed, save round-trip, dump credit across all 5 wallets)
  + `EchoResourcePickerRegression` (chip projection, picker verb, card strings, synergy line,
  WO-831 emergence data/fallback).

### 6.2 WO-830 — status
Implemented 2026-08-02 (this section previously tracked it as pending). Spec + banners:
`WorkOrders/WORK_ORDER_830_echo_harvest_affinity_synergy.md`; sprite beat:
`WORK_ORDER_831_echo_emergence_sprite_beat.md`. Emergence ART is not yet supplied — the
loader degrades to the portrait until the 6 LFS files land under
`Assets/Resources/Echoes/Emergence/`.

### 6.3 Offline accrual + legacy workers
OfflineHarvestService (335, WO-115 claims-on-resume; `OfflineHarvestBootstrap` DDOL) +
OfflineHarvestResult (55) + TimeSource (42, the ONE clock seam) + WelcomeBackPopup (205).
WorkerManager (363) + Worker/NodeFillIndicator/WorkerManagerBootstrap — the WO-117 dispatch
system, **retired for V1**: `UseOfflineCatchUp` off and `ClickToDispatch` disabled by
EchoWorkforceBootstrap so it never banks the same nodes (EchoService header :32-34);
OuterWorld-gated anyway. HarvestSourceRegistry (31).

---

## 7. HubStructureVisualInjector.cs (root, 507) — the baked-hub visual seam

Runtime re-skin of baked castle-hub structures to lightweight Resources models (no scene
edit). The **Swaps table** (:63-87) is the hand-dialed row set (Tripo yaw-90 convention,
WO-764 fit-to-height; CastleBarracks row at :84). Also `Place`s new props (colosseum) with
the ONE blanket `GroundY = 0` rule (:100-109) and exposes `RuntimePlacedProps` for the
AutoPilot prop-seating oracle (:91-97).

**Blank-town gate seam (verified):** each swap first asks
`StrategicPlacementMigration.StanddownActiveForBaked(bakedName, out itemId)` (:277) — which
now folds the WO-834 `MayBakedTwinSurface` clause — and hides the bake when true;
`CastleBarracks` additionally gates on `BarracksUnlock.IsUnlocked` (:291).
**`EnsureBarracksSurfaced()`** (:165-183) reactivates + re-skins the baked barracks only when
unlocked — the route `StructureSingleton.ResurfaceBakedTwins` uses for the barracks twin.
**`ResurfaceStorefront(bakedName)`** (:307) re-activates + re-skins any other stood-down
storefront (idempotent, `LightSkin_` marker child) — the post-sell resurface body.

---

## 8. Risk ledger (prioritized)

1. **WO-830 implemented 2026-08-02 (pending CompileGate + DataRegression):** the roster/
   assignments/calculator now match the ruling (see §6.1). Residual risks: the WO-738 doc's
   dialogue copy is banner-flagged STALE; WO-831 emergence ART is not yet supplied (loader
   degrades to portrait); the two new oracles must be registered in `DataRegression.RunAll`
   by the committer.
2. **Comment drift — catalog version:** `StructureSingleton.cs:17` and `:418` say
   "structures-catalog.json **v5**"; the live JSON is **`version: 6`**. Harmless today
   (fields unchanged) but the §15 canon rule says fix the comment on next touch.
3. **Two `ResourceCost` structs still coexist** (`DeNelle.Village.ResourceCost` 4-field wallet
   cost vs `Buildings.Progression.ResourceCost` resource+amount). Live mitigation:
   `BuildingUpgradeVM.cs:35` aliases `EcoCost`. Any new Progression-namespace file must
   disambiguate or it will silently bind the wrong one.
4. **StructureSingleton world scans:** `IsBuilt`/`Enforce` use `GameObject.Find` +
   `FindObjectsByType` (incl. inactive in `FindByNameInclInactive`, :429-435). The per-frame
   memo (:85-86) covers palette polls, but `EnforceAll` on hub load is O(scene) per singleton
   row — fine at 12 rows; watch it if the singleton set grows large.
5. **Three singleton rows have NO baked twin** (fountain_healing, mill, armorer): for them
   `IsBuilt` rests on records/live probes only, and the blank-town gate is a no-op (nothing
   to suppress). Expected, but a new bake for one of them MUST add `repo.bakedTwins` or the
   sweep can't see it.
6. **ArenaWalletService is still a PlayerPrefs stub** (seed 500 SKR) and the Arena seed
   catalogs are still hardcoded with TODO-JSON notes — unchanged since the MVP. Do not
   demo it as real custody.
7. **Legacy tower generation lives on:** `Tower.cs`/`TowerPlacementSystem`/`BuildMenu` are the
   pre-BuildMode lane (Tower.cs still uses reflection for camera shake, :31-33). New defense
   work goes through DefenseTower + BuildMode; don't extend the legacy trio.
8. **WallTierData mesh paths are placeholders** (owner meshes pending) and `WallRepairController`'s
   destroyed-structure prose predates WO-753 (destroyed structures are removed before the
   repair loop can see them) — behavior is correct, header text is stale.
9. **Wood/Iron dual-wallet hazard persists** (EconomyService in-session pool vs GameState
   ledger; `GrantSpendable` writes both). WallRepairController and EchoService.Dump both
   deliberately use the both-sides path — keep doing that.
10. **Silo capacity vs rate: RECONCILED (WO-830, 2026-08-02)** — `SiloCapacity` now scales by
    the same `AggregateHarvestMultiplier()` basis as rate (talent factor still excluded by
    design), so fill-time ≈ `SiloCapHours` with specialization active.
