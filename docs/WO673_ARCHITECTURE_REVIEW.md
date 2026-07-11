# WO-673 Architecture Review — Strategic Building Placement (player-placed functional buildings)

**Reviewer:** dev-lead architect pass, 2026-07-11. All claims below verified by direct code reads (file:line cites), not from the census.
**Verdict: GO-WITH-CHANGES.** The BuildMode spine genuinely carries functional buildings today — the
catalog rows, behavior cases, and damage surface already exist. The work is NOT placement engineering;
it is (a) injector/bake standdown + migration, (b) the vendor-NPC anchor gap, (c) collector
registry double-owner hygiene, (d) palette taxonomy + rotation. None require greenfield. The easy
path (bolt a new placement flow for buildings) is wrong; the right path (ride the existing spine,
decommission the auto-placers behind ff.strategicplacement) is also the cheaper one here — a rare
alignment. Where easy-vs-right genuinely diverge is migration (§3) and rotation grain (G-F); named there.

---

## 1. Is the spine ready? — Verified stage by stage

| Stage | Code | Ready for functional buildings? |
|---|---|---|
| Palette | `BuildPaletteUI.Configure(BuildType)` reads `BuildCategoryRegistry` (BuildPaletteUI.cs:92-101); data-driven via build-categories.json | YES — adding a category row + `_types` is data work. `CatalogType.Resource` entries exist but no BuildType maps them (build-categories.json lists Defense/Collector/Support only). |
| Ghost/validity | `BuildModeController.IsValidPlacement` (BuildModeController.cs:764-779); footprint from corrected bounds via `StructureFactory.MeasureUprightFootprintMetres` (StructureFactory.cs:441) | YES — entry-generic. Caveat: `FootprintCells` is square-only (BUILD_MODE_ARCHITECTURE.md G4); fine for the building set (roughly square meshes), wrong for future 1x3 shapes. |
| Charge | Multi-resource ledger IS wired — `ChargeLedger(cost)` post-commit (BuildModeController.cs:1064-1068, 1635-1651), `CostFor` falls back to crystals when `repo.cost` unauthored (BuildModeController.cs:1582-1590) | YES. Note: doc G2 ("crystals-only") is STALE — code has moved past the doc. Author `repo.cost` rows for buildings or they charge crystals-only. |
| Persist | `PlacedStructureData` (Core: itemId+cell+yawSteps+level+yawOffset+worldY+wallMounted, PlacedStructureData.cs:37-88); SaveSchema `CurrentVersion = 29` (SaveSchema.cs:32) | YES — building records are just itemIds. Additive-nullable + bump precedent stands. |
| Replay | `BaseLayoutLoader.Spawn` -> `StructureFactory.Create` (BaseLayoutLoader.cs:239-337); home-hub auto-replay via `BaseLayoutLoaderBootstrap` (BaseLayoutLoader.cs:411-467), gated to `SceneRouter.Castle` | YES. |
| Behavior attach | `StructureFactory.AttachBehavior` switch (StructureFactory.cs:534-664): `GameplayBuilding` case attaches `Building` + `BuildingInteractable` + registers with `VillageController` (:641-655); `ResourceCollector` case configures by `collectorBuildingId` (:598-606) | YES for the building/collector core. Gaps in §2. |
| Catalog rows | structures-catalog.json: pet-house, workshop, market, mill, lumbermill, forge, jeweler, arcane-tower all exist as `type: Resource`, `behaviorId: GameplayBuilding`, costed (rows at :489-773); collector_farm/lumbermill/forge as `Collector` | YES — content already authored. |
| Targetable | `Building : MonoBehaviour, IDamageableStructure` (Building.cs:61); `ResourceCollector : IDamageableStructure, ISiegeLootTarget, IHarvestSource` (ResourceCollector.cs:19); `StructureDamageVisuals` scans registries + FindObjectsByType generically (StructureDamageVisuals.cs:284-334) | YES — targetable-by-construction holds. One caveat: DefenseTargetableRegression.cs:62 currently EXCLUDES Buildings/GameplayBuilding from its oracle scope — extend it, do not trust it as-is. |

**Bottom line:** the "initial build that allows placing these structures" is closer than the WO
assumes — a Structures category row + palette entry gets a placeable, persisting, targetable,
repairable Forge TODAY. Everything else in this review is about what is wrong AROUND that.

## 2. Concrete gap table (the real work)

| # | Gap | Evidence | Fix shape |
|---|---|---|---|
| G-A | **No vendor NPC on a placed building.** `CastleVendorNpcInjector` spawns vendors ONLY from baked `NPC_<Role>_Interactable` markers (CastleVendorNpcInjector.cs:189-201) or type-keyed deferred polls for exactly TWO station types (Apothecary :233, JewelersBench :280). A player-placed forge gets Building+BuildingInteractable but no smith, no sign. | CastleVendorNpcInjector.cs:55-56, 189-228 | Generalize the deferred-pass pattern: replace the name/marker loop with a poll over the live Building collection keyed by `Building.Id` -> `VendorFor(id)`. Readers query the collection (One Model §2b) — this ALSO fixes "what breaks when the Forge moves" for free, because the synthetic-marker path already derives position + Heart-facing from the Building transform (:349-362). |
| G-B | **Collector name-coupling + double-owner.** `ResourceCollectorBootstrap.EnsureCollectorOn` finds storefronts BY NAME ("Forge_Armor_Storefront", ResourceCollectorBootstrap.cs:40-42) and `EnsureFallbackCollector` creates origin-parked logical collectors when absent (:77-106). `ResourceCollectorRegistry` is last-write-wins per id (ResourceCollectorRegistry.cs:19). A placed collector_forge + the DDOL fallback = two live ResourceCollectors sharing one id; registration order decides which one the economy/damage systems see. | ResourceCollectorBootstrap.cs:40-47; ResourceCollectorRegistry.cs:16-27 | Flag-gate: when ff.strategicplacement is ON, the bootstrap name-wire AND fallback both stand down for ids present in BaseLayout (or entirely, with a grace fallback only while the player has not yet placed that collector — economy must not zero out pre-placement). ONE owner per concern (§2b.1). |
| G-C | **Runtime station injectors hardcode world positions and bypass the spine.** `CraftingStationInjector.StationPos = (11,0,2)` (CraftingStationInjector.cs:57), builds Building directly (:129-130). Same shape for `JewelerStationInjector` (Items/JewelerStationInjector.cs:34). These are the true auto-placers to decommission per-structure. | Items/CraftingStationInjector.cs:57,129 | Flag-gated standdown + a one-time migration record emit (§3). |
| G-D | **The 8 storefronts are SCENE-BAKED, not injector-placed.** The WO expectation ("most functional structures placed by runtime injectors") is only true for Apothecary / Jewelers-Bench / Colosseum. Blacksmith/Lumbermill/Windmill/EchoHollow/Forge/ArcaneTower/Jeweler/Marketplace are baked by CastleHubBuilder into the scene; `HubStructureVisualInjector` only RE-SKINS them by name (HubStructureVisualInjector.cs:60-79) and places one NEW visual (Colosseum, :121-132). | HubStructureVisualInjector.cs:8-22, 202-214 | Do NOT rebake the scene (owner-hand-dialed canon). The standdown precedent already exists IN this file: the Barracks SetActive(false) behind ff.barracks (HubStructureVisualInjector.cs:206-212). Deactivate baked functional structures the same way behind ff.strategicplacement. |
| G-E | **Palette taxonomy.** Owner ruled THREE categories: Build->Structures (functional + collectors), Build->Defense (towers/gates), Build->Walls (walls split out — claimed-outpost wall canon). Today: Defense=Tower/Wall/Gate, Collector, Support (build-categories.json:4-40); BuildType enum = {Defense, Collector, Support} (CatalogType.cs:18). | CatalogType.cs:8,18; build-categories.json | Add BuildType.Structures + Walls; re-map rows: Structures->[Resource, Collector], Defense->[Tower, Gate], Walls->[Wall]. Record the 06-27 "Defensive only" reversal in the BuildPaletteUI `_types`/`_lockedIds` comment (WO mandate). Data + enum + menu entries; no placement code. |
| G-F | **Rotation (owner addition).** 90-degree steps + free yawOffset already persist AND replay (PlacedStructureData.yawSteps/yawOffset, applied at BaseLayoutLoader.cs:258); the rotate-confirm menu exists (OnRotateConfirmed(int yawSteps), BuildModeController.cs:695-699; TowerPlacementRotateMenu at :2100-2124). NavMesh carve DOES rotate with the model — the NavMeshObstacle box is local to the yawed root (BaseLayoutLoader.cs:346-361). What does NOT rotate: grid occupancy — Occupy claims axis-aligned cells from an unrotated square footprint (BaseLayoutLoader.cs:279, 324). | BuildModeController.cs:661-699; BaseLayoutLoader.cs:258, 346-361 | **Recommend 90 for V1** (grid-honest: square footprints under 90-degree yaw occupy exactly the same cells — zero occupancy math). 45 needs no schema (rides yawOffset=45) but a 45-rotated square AABB overhangs its claimed cells by ~41% on the diagonal — ghost-vs-collision lies. Defer 45 until footprint claims can inflate for diagonal yaw. Easy-vs-right: shipping 45 "because the field exists" is the easy trap. |
| G-G | **Starting budget has no home yet.** DifficultyTuning is countdown-only (DifficultyTuning.cs:39-60) — NOT the right seam. | DifficultyTuning.cs | New-game defaults: seed the multi-resource ledger at new-game (GameState defaults / a starting-budget row in a canonical json, flag-gated). Size = core kit (forge 130 + collector_forge 120 + collector chain ~180 + 2 defenses ~200 ≈ 650 crystal-equivalent; tune vs repo.cost rows once authored). |
| G-H | **repo.cost rows unauthored for buildings** -> they charge buildCost crystals-only (BuildModeController.cs:1582-1590). | structures-catalog.json | Author multi-resource costs per row (data-only; StreamingAssets + Resources copies byte-equal — CanonicalJson dual-copy rule). |
| G-I | **Compass POIs / talk-route ids.** Yarn structureId routes key on ids, not transforms (DialogueService.PlayStructure, CastleVendorNpcInjector.cs:750). Position-independence LOOKS right but is census-verify: confirm the compass (HUD/Kit/HudKitController.cs) resolves POI positions from live objects, not authored coordinates. | HudKitController.cs (verify) | Census item; if any POI is coordinate-authored, re-point it at the Building collection. |

## 3. The injector question — the RIGHT decommission shape

**Recommendation: flag-gated standdown + a one-time "default layout writer" migration — a hybrid,
with a precise split:**

- **Standdown (renderer/behavior side):** baked storefronts get the Barracks pattern —
  SetActive(false) behind ff.strategicplacement in HubStructureVisualInjector (precedent
  HubStructureVisualInjector.cs:206-212). The NPC injector then finds no markers and no-ops by
  construction (its loop is marker-driven); the collector bootstrap name-lookup finds nothing and
  must NOT fall back for placed ids (G-B). Runtime stations (Apothecary / Jewelers Bench) gate
  their Inject() on the same flag. **No scene edit, no rebake — MainCastle_Hall untouched.**
- **Migration (data side):** a first-load-under-flag migrator converts each auto-placed functional
  structure into a PlacedStructureData record at its current position, THEN the standdown applies.
  This is the "injectors become default layout writers, once" shape — but implemented as a
  SaveMigrator-adjacent one-shot (keyed by a persisted migration marker on the v29->v30 bump),
  NOT by leaving the injectors running in a writer mode forever. A permanently dual-mode injector
  is the double-spawn factory: one system that places AND one that replays the same id is exactly
  the two-VFX-stacks scar (§2b.1 — ONE owner per concern).
- **Double-spawn prevention is structural, not defensive:** after migration, the ONLY spawner of
  functional structures is BaseLayoutLoader (already once-per-session latched, `_loadedOnce`,
  BaseLayoutLoader.cs:66,134). Flag OFF = injectors/bake own everything, loader replays only
  towers (today, byte-identical). Flag ON = loader owns everything functional, injectors dark.
  There is never a frame where both own the same id.
- **Grid-quantization caveat (name it to the owner):** PlacedStructureData is cell-based (3m
  cells). Migrating a baked storefront at an arbitrary world pos snaps it to the nearest cell —
  up to ~1.5m drift; and owner-hand-dialed yaw/roll/scale (HubStructureVisualInjector Swaps rows,
  e.g. jeweler rollDeg=110.4 at :71) does NOT survive a yawSteps+yawOffset model. Options:
  (a) accept drift on migrated saves (owner felt-pass judges), or (b) add an optional worldX/worldZ
  override to the record (additive-nullable, v30). Recommend (a) first — (b) weakens the
  grid-relative Arena/replay contract for one cohort of legacy records. If the felt-pass rejects
  the drift, (b) is the fallback, explicitly scoped to migration-emitted records.
- **SaveSchema:** bump to v30 with the migration marker; additive-nullable + default-on-read
  (v29 precedent, SaveSchema.cs:32).

## 4. Dependency web — what reads position, what breaks when the Forge moves

| Reader | Mechanism | Dynamic? | Break risk when placed/moved |
|---|---|---|---|
| Vendor NPCs | Baked name markers NPC_<Role>_Interactable (CastleVendorNpcInjector.cs:189-201) | NO — name/bake coupled | HIGH — G-A. Fix = poll Building collection (the apothecary deferred-pass shape :244-259 already computes position from the live Building). |
| Townsfolk flee-shelters | s_shelterBuildings = lazily refreshed live Building[] cache (AmbientNPC.cs:139-141) | YES | LOW — already the One Model answer. Verify the refresh cadence picks up a mid-session placement. |
| Collector economy | ResourceCollectorRegistry by id (ResourceCollectorRegistry.cs:29) | YES (id, not pos) | MEDIUM — G-B double-owner, not position. |
| Enemy targeting / WaveDamageReport / StructureDamageVisuals | Registry + FindObjectsByType scans of IDamageableStructure bearers (StructureDamageVisuals.cs:284-334) | YES | LOW — capability surface, position-free. |
| Talk-routes / dialogue | structureId string -> DialogueService.PlayStructure (CastleNpcInteractable.Interact, CastleVendorNpcInjector.cs:721-767) | YES (id) | LOW once the NPC exists (G-A). |
| Tutorial | Signal-driven (TutorialSignals.TowerPlaced/BuildModeEntered, TutorialSignalAdapters.cs:108-175); anchors runtime-resolved (TutorialWorldAnchors.cs:61-146) | YES | MEDIUM — signals fire fine, but beats that ASSUME a storefront exists (vendor talk beats) hit a world with zero functional buildings on a flag-on new game. Needs the "place your Forge" step or a grace default (census enumerates the exact beats). |
| Compass POIs | HudKitController (unverified) | verify | Census item (G-I). |

**The abstraction (binding):** every reader asks the collection — the Building roster /
ResourceCollectorRegistry / IDamageableStructure scans — keyed by id or capability, never a baked
name or an authored coordinate. The flee-shelter cache is the exemplar; the vendor marker loop is
the violation to retire. No new registry needed — the collections already exist.

## 5. Risk ratings + gate tests (§2c — the permission gate)

- **Save/migration risk: MEDIUM.** Mechanics proven (v13->v29 precedents; additive default-on-read
  fields in PlacedStructureData already demonstrate the pattern). The genuine risk is semantic:
  quantization drift (§3) and the fallback-collector interaction (G-B).
- **FTUE risk: HIGH (flag-on new game only).** A new player in an EMPTY functional town with an
  unfamiliar build palette is a cold-start cliff; every vendor beat dangles until placement. This
  is why ff.strategicplacement defaults OFF until felt-passed — the flag IS the mitigation.

**Gate tests that must exist before the flag can default ON (extend the existing harness —
Assets/Tests/EditMode|PlayMode, DataRegression, TowerRespawnRegression pattern):**
1. **Existing-save round-trip:** load a v29 fixture save (injector-era), run migration, save,
   reload — assert structure COUNT and id SET identical pre/post; assert each migrated record
   replays (Rebuild built==count; the built < layout.Count Warn at BaseLayoutLoader.cs:224 is the
   oracle hook). No building lost, ever.
2. **Flag-off = today:** new game with ff.strategicplacement OFF — assert baked storefronts
   active, vendor count == today, BaseLayout untouched, zero migration writes (byte-identical
   save on a no-op session).
3. **Placement->attack->repair->reload chain (PlayMode/headless):** place forge + collector via
   the real Place path -> assert IDamageableStructure registration + enemy targeting acquires it
   (extend DefenseTargetableRegression to INCLUDE GameplayBuilding — currently excluded,
   DefenseTargetableRegression.cs:62) -> damage it -> assert StructureDamageVisuals tell + repair
   charges in-kind -> reload -> assert level/damage-appropriate state and NO double-spawn
   (exactly one Building per id).
4. **Vendor-anchor test:** placed forge -> assert a vendor NPC spawns at it (collection-driven,
   G-A fix) and CastleNpcInteractable.ResolveRoute returns the same route as the baked-era oracle
   (AssertVendorTalkRoute seam, CastleVendorNpcInjector.cs:649-653, 809-812).
5. **Data gates:** DataRegression on the new category rows + costs; catalog integrity (every
   palette id resolves via CatalogRegistry; both canonical copies in sync).

## 6. Per-site layouts (owner extension — assessed, parked out of V1)

The shape already tolerates the direction. PlacedStructureData is grid-relative and carries no
site identity (PlacedStructureData.cs:37-88) — records are site-portable by construction, which is
exactly what the headless-replay/Arena seam wanted. What is home-coupled is the SCOPING, not the
data: GameState.BaseLayout is one global list, and replay is gated to SceneRouter.Castle
(BaseLayoutLoader.cs:448) with a per-scene skip set (:54-57). The growth path is a keyed
collection — additive-nullable `SiteLayouts: List<SiteLayout> { string siteId; List<PlacedStructureData> records }`
(schema bump, default-on-read = empty; BaseLayout remains the home site, or migrates to
SiteLayouts["home"] in a later bump), plus a per-site PlacementGrid origin and a loader keyed by
"which site is this scene/claim" instead of the hardcoded castle check. **V1 rules so we do not
paint ourselves in:** keep every new record grid-relative (resist the §3(b) world-coordinate
override unless migration forces it, and scope it to migrated records if so); keep
IsValidPlacement/cost pure (no scene dependency — already true); route any new "which layout do I
replay" decision through ONE resolver function rather than adding more scene-name string checks.
Nothing in the V1 slice below violates these. Parked: NOT in V1.

## 7. The V1 slice (implementable lanes) — and what is deliberately NOT in it

**V1 = home base only, flag-gated (ff.strategicplacement, default OFF), 90-degree rotation.**

| Lane | Work | Files |
|---|---|---|
| L1 Data | BuildType.Structures + Walls; build-categories rows (Structures->[Resource,Collector], Defense->[Tower,Gate], Walls->[Wall]); repo.cost rows for the 8 buildings + collectors; palette-reversal comment; starting-budget data row + new-game seed | CatalogType.cs, build-categories.json (both copies), structures-catalog.json (both), BuildPaletteUI.cs comment |
| L2 Menu | Build menu entries for the two new verbs -> EnterBuildMode(BuildType.Structures/Walls) (generic entry exists, BuildModeController.cs:189-198) | BuildMenu + BuildModeController (menu wiring only) |
| L3 Standdown + migration | ff.strategicplacement flag; baked-storefront deactivation (Barracks pattern); station-injector gating; collector-bootstrap standdown for placed ids; one-shot migration writer -> BaseLayout records; SaveSchema v30 + marker | FeatureFlags.cs, HubStructureVisualInjector.cs, CraftingStationInjector.cs, JewelerStationInjector.cs, ResourceCollectorBootstrap.cs, SaveMigrator/SaveSchema |
| L4 Vendor anchors | Vendor injector: marker loop -> Building-collection poll (generalize the apothecary deferred pass); flag-off keeps the marker loop | CastleVendorNpcInjector.cs |
| L5 Rotation | Stepped 90-degree rotate-left/right buttons (+[Q]/[E]) on the existing rotate menu; persists via existing yawSteps — no schema change | TowerPlacementRotateMenu, BuildModeController.cs:661-699 |
| L6 Gates | The five §5 tests; extend DefenseTargetableRegression to include GameplayBuilding; fleet palette/placement probes | Assets/Editor/Regression, Tests |
| L7 FTUE guard | Minimal: flag stays OFF for the tutorial cohort in V1 (no tutorial authoring); a "place your Forge" beat is V2 | — |

**Deliberately NOT in V1:** 45-degree rotation (occupancy lies — G-F); per-site / claimed-outpost
layouts (§6); non-square footprints (G4); tutorial "place your Forge" beat (flag stays off for
FTUE); walls-on-claimed-outposts (rides per-site); bounded plot (G6); mobile touch driver (G5,
parallel WO); any scene rebake (forbidden); Arena snapshot.

## 8. Top-3 risks

1. **Double-spawn / double-owner during the transition** (baked+placed forge, fallback+placed
   collector). *Mitigation:* structural single-owner split by flag (§3), registry last-write
   audited (G-B), gate test #3 "exactly one Building per id" assertion.
2. **Migration fidelity — the owner hand-dialed town shifts or loses a building.** *Mitigation:*
   gate test #1 (count+id round-trip); quantization drift named to the owner up front with the
   worldX/Z fallback scoped (§3); flag-off path byte-identical (test #2) so rollback is a pref flip.
3. **FTUE cliff on flag-on new games** (empty functional town + dangling vendor beats).
   *Mitigation:* flag default OFF until felt-passed; starting budget sized to the core kit (G-G);
   census enumerates the exact tutorial beats before any default-ON decision.

---
*Cross-refs: docs/ARCHITECTURE_PRINCIPLES.md (§2b One Model, §2b.1 one-owner, §2c gates),
docs/BUILD_MODE_ARCHITECTURE.md (stale on G2 — code has multi-resource charging), WO-672 (damage
lifecycle), WO-584 (ownership flip — per-site future).*