# REGRESSION COVERAGE MATRIX - known dictionary (2026-07-19)

> Produced by the silo-audit + regression-coverage fleet (this-week arc 07-13..07-19). Each row is a
> source-cited fact. Refresh every Sunday (SUNDAY_HOUSEKEEPING.md). VERDICT: NO — regressions do not cover the findings. 0 of 73 have hard defect-specific coverage; 12 SOFT (partial/report-only), 61 fully uncovered. All 5 P1s (3 monetization integrity failures, dungeon roach-motel, waves.json schema) are unguarded. 68 findings need a new/extended regression, ~68 NEW vs GAP_AUDIT. Seven root oracles (exhaustive save round-trip, all-7-sites enemy divergence, one PlayMode NavMesh reachability suite, a global feature-flag-defaults walk, authored-portal↔scene join, modal-arbiter source-lint, pack economy integrity) close ~half; the rest are per-finding suites.

**Stats:** {"total": 73, "new": 68, "p1": 5, "p2": 30, "p3": 38, "covered": 0, "uncovered": 61, "soft": 12, "actionableRegressions": 68, "silos": 16, "severityMapping": "blind-spot high->P2, medium/low->P3", "knownInGapAudit": 5, "knownGapAuditItems": ["#6 (CS-2)", "#12 (BLIND-1-F3)", "#14 (HUD-2)", "#15 (DGN-P2-5)", "orc-raider (EW-1)"]}

> **⚠ PARTIALLY SUPERSEDED 2026-07-20** (overnight loop — see `OVERNIGHT_RESULT_2026-07-20.md`). The
> "0 of 73 covered" headline below is now stale: **13 hard defect-specific oracles landed + went green**,
> closing several listed P1s. Newly COVERED: **ECON-2** split-brain pack ownership (`PackGrantRegression`
> -> PACK_GRANT_OK, fixed via `GlimmerCurrencyService.MarkCosmeticOwned`); **ECON-3** Glimmer-never-granted
> (pack `ApplyPackContents` now routes Glimmer); **DGN-P1-1** dungeon roach-motel (`DungeonExitRegression`
> -> DUNGEON_EXIT_OK + runtime exit bootstrap). Plus new oracles: WAVE_SCALING, ENEMY_REWARDS,
> WALL_MITIGATION, UPGRADE_AUTHORITY, SFX_RESOLVE, FOUNDING_REACH, FTUE_HONESTY, ECHO_CARD_COPY,
> SHADER_PIN, MODAL_REGISTRATION, CRYSTAL_PRODUCTION. **ECON-1 + EW-3 CLOSED 2026-07-20** (owner
> request): **ECON-1** -> `PackCosmeticIntegrityRegression` (PACK_COSMETIC_INTEGRITY_OK) drives the real
> `ApplyPackContents` across all 13 packs and asserts every advertised cosmetic ends up owned; **EW-3** ->
> `WavesSchemaRegression` (WAVES_SCHEMA_OK) asserts the `"waves"` key + >=1 wave on both dual copies AND
> proves a renamed-key variant collapses to 0-wave and is caught, plus a loud runtime `FlowTrace.Fail` on
> empty schedule. **All 5 audit P1s are now cleared + guarded.** **`dungeon-dressing` CLOSED 2026-07-20** (owner
> greenlight): `DungeonDresser.DressRoom` seats ~8 real props/room, wired into `DungeonBaker`; oracle
> upgraded name-scan -> behavioral (DUNGEON_DRESSING_OK). **DataRegression = REGRESSION_OK, all 16 suites
> green, ZERO reds.** The body below is the
> frozen 07-19 snapshot — do not rewrite; the next Sunday refresh reconciles the counts.

# Regression-Coverage Proof — Full Silo Audit Synthesis
**Branch** wip/village2-and-f8-tickets · **HEAD** 567f166d · **Date** 2026-07-19

## Headline

- **Total findings: 73** across 12 gap-audit silos + 4 blind-spot fills.
- **NEW (not a numbered GAP_AUDIT item): 68.** Only **5** map to an existing GAP_AUDIT row — #6 (CS-2, claimed-closed-but-open), #12 (BLIND-1-F3), #14 (HUD-2), #15 (DGN-P2-5), and the orc-raider divergence (EW-1). Everything else this coverage audit surfaced fresh.
- **Severity: 5 P1 · 30 P2 · 38 P3.** (Blind-spot high→P2, medium/low→P3.)
- **Coverage: 0 COVERED (hard) · 12 SOFT (report-only/partial) · 61 UNCOVERED.**
- **Actionable "needs a regression": 68** (61 uncovered + 7 partial-SOFT that must be hardened; the other 5 SOFT are §15 doc / code-quality fixes, not oracle targets).

> **The single most important number: ZERO of 73 findings have a hard, defect-specific oracle that goes RED on the actual bug.** Every green marker in the tree (`REGRESSION_OK`, `COMPILE_GATE_OK`, `ROOMFORGE_REGRESSION_OK`, `DATAWEB_OK`, `GEAR_CURATION_OK`, `CORESAVE_OK`) is currently passing over all 73.

### The 5 P1 gaps (all completely unguarded)
| ID | Silo | What ships green today |
|---|---|---|
| ECON-1 | Economy | 15 paid-pack cosmetic SKUs (incl. founders-vow) dangle — unredeemable, no pack→cosmetic integrity check |
| ECON-2 | Economy | Split-brain ownership: pack cosmetics land in `OwnedItemIds`, shop reads `GlimmerCurrencyService` — never crossed |
| ECON-3 | Economy | Every pack card advertises Glimmer; `ApplyPackContents` never grants it |
| DGN-P1-1 | Dungeons | `dg_starter_loop` "playable" roach-motel — no exit/return affordance baked |
| EW-3 | Enemies/World | `waves.json` has only a PARSE check — a renamed top-level key maps to a 0-wave loop, stays green |

---

## Coverage Matrix (by silo, P1 first)

**Legend** — Covered?: `NO` = no oracle touches the defect · `SOFT` = a suite touches the area but is report-only or partial (does not hard-fail on this defect) · `YES` = hard oracle (none exist). `†` = NEW (missed by GAP_AUDIT).

### Economy / Monetization
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| ECON-1† Flagship packs grant cosmetics absent from cosmetics.json | P1 | NO | none ([econ-meta] validates pack pricing only) | [econ-meta] `CheckPackCosmeticIntegrity`: every `contents.cosmetics[]` id ∈ cosmetics.json |
| ECON-2† Split-brain pack ownership vs shop store | P1 | NO | none | New `PackEntitlementRegression`: `RecordOwned`→shop `OwnsId` true |
| ECON-3† Packs advertise Glimmer, never grant | P1 | NO | none | Extend [glimmer]: `ApplyPackContents` → balance delta == advertised |
| ECON-4† BattlePass debits 2400 before content/null check; no BattlePassData asset | P2 | NO | none | New `BattlePassRegression`: asset exists; null-data purchase → zero debit |
| ECON-5† Dead third BattlePass persistence store, header lies | P3 | NO | none | `MonetizationStoreReconciliationRegression`: no orphaned/uncalled store |

### Dungeons / RoomForge
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| DGN-P1-1† Composed dg_starter_loop has no exit/return | P1 | NO | none (room-forge is geometry-only) | New `DungeonPlayabilityOracle`: `PopulateForPlay` → ≥1 return/exit affordance |
| DGN-P2-2† dg_starter_loop.unity shipped BINARY yet enabled:1 | P2 | NO | none (COMPILE_GATE scans .cs only) | Extend `SceneRoutingRegression`: enabled DungeonCompose scenes must start `%YAML` |
| DGN-P2-3† DungeonPortals comment "OFF" but defaultOn:true | P2 | NO | none | New `FeatureFlagDefaultsOracle`: unset pref → flag == milestone intent |
| DGN-P2-4† dg_starter_loop zero loot wiring | P2 | NO | none | Extend room-forge Case9 w/ dg_starter_loop: chest/pickup count > 0 |
| DGN-P2-5 (#15) Healer's Cottage chests grant nothing | P2 | NO | none | New `DungeonRewardOracle`: `OpenChest` → real grant (gate behind WO-749) |
| DGN-P3-6† Composed dungeon on Village systems (no DungeonHero/Lantern) | P3 | NO | none | Extend DungeonPlayabilityOracle: `FindAnyObjectByType<DungeonHero>`!=null (coord PO) |
| DGN-P3-7† "loop" never closes — turn3 sealed dead-end | P3 | NO | none (Case9 covers spine/demo only) | Extend Case9: dg_starter_loop closed-cycle / sealedN==1 topology pin |

### Core / Save
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| CS-1† Dead equippedRingId/AmuletId fields; real GearLoadout path untested | P2 | NO | none (oracle asserts DEAD fields) | New `[accessory-persist]`: GearLoadout ring+amulet round-trip; flag outside-envelope gap |
| CS-2 (#6) Round-trip blind to 5 persisted fields | P2 | NO | none (Part H checks a hand-picked subset) | Author non-default ArenaDefense/BuildJobs/AdSkips*/LastHarvestClaimMs; assert survive Save/Load |
| CS-3† Gap-oracle structurally blind to unwired schema field | P2 | NO | none (this IS the hole) | Make `CheckPersistenceGaps` body-aware: seed every PersistedState prop, assert round-trips (subsumes CS-1/CS-2) |
| CS-4† FreeBuildsUsed doc drift (one-free-EACH→TOTAL) | P3 | SOFT | none — §15 canon lint | Doc fix; not an oracle target |
| CS-5† Header "41 persisted fields" (schema ~60) | P3 | SOFT | none — §15 canon lint | Doc fix; CS-3 makes count self-enforcing |

### Enemies / World
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| EW-3† waves.json PARSE-only, no typed regression | P1 | SOFT | [core-catalog] parse-only (does not catch schema map) | New `[waves-schema]`: deserialize both copies through WaveSchedule; Count>0 + wave[0] fields |
| EW-1 orc-raider divergence 7 sites/4 HP values + AI split | P2 | SOFT | [combat-atb] PARTIAL (2 of 7 sites) | Extend `CheckSynthesizedStatDivergence` to all 7 builders + enemies.json; one canonical {hp,archetype} |
| EW-2† Divergence detector compares only 2 of 7 sites | P2 | NO | none (detector's own blind spot) | Same all-sites oracle + source-count guard: distinct builders collapse to 1 |
| EW-4† OutpostEnemyGroupSpawner hollow stats (live in dg_starter_loop) | P3 | NO | none | Generalize divergence oracle to hollow ids (DefFor vs Garrison vs enemies.json) |

### HUD / Panels
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| HUD-1† Pets box (EchoRoster) modal never registers with PanelManager | P2 | NO | none | New source-lint `[modal-arbiter-conformance]`: modal builders must call `NotifyOpened` |
| HUD-2 (#14) EndStateView.Show bypasses single-modal arbiter | P2 | NO | none (self-documented bypass; allow-listed in ui-mvvm) | Same lint + play-mode: `Show` → `AnyOpen` true + prior panel closed |
| HUD-3 Non-ASCII separators in panel strings | P3 | SOFT | [hud-ui-sme] partial (skips VM files; wrong font truth) | Extend `CheckTofu` candidacy to *VM.cs; pin FontOracle to shipped Blink atlas |
| HUD-4† EchoUnlock dialogue + SFX not scene-gated | P3 | NO | none (hud-posture is pursuit-pulse only) | New `[echo-unlock-scene-gate]`: non-gameplay scene → no DDOL card + SFX suppressed |

### Combat / BattleArena / ATB
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| ARENA-1† Fled-pack leash writes transform.position on live NavMeshAgent (reverted) | P2 | NO | none (ArenaCombatOracle drives ResolveForTest only) | New `[arena-leash]`: positive-control revert + leash keeps agent within radius post-frame |
| ARENA-2† Self-heal gate `_familyEngaged` never latches if leader dies at range → 240s pin | P2 | NO | none | New `[arena-watchdog]`: leader killed pre-latch → resolves in disengage window not 240s |
| ARENA-3† WO-556 star-bonus gear scaling clamped away (dead computation) | P3 | NO | none | New `[arena-gear-odds]`: per-roll chance ==0.04 flat across stars; EnemyOutpost parity |
| ABIL-1† Explicit castAnim path never exercised (E-hotkey anim half) | P3 | NO | none ([abilities] checks Slot/Name only) | Extend [abilities]: ResolveAnimVariant priority-1 + fallback; ResolveSlotDef→ResolvedDef icon |

### Gear / DataCatalogs
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| GEAR-1† weapons/armor byte-exempt; curation checks id-presence only | P2 | NO | none | Extend `CheckGearCuration`/[gear-fields]: per-row field validation + cross-copy DeepEquals of shared stats |
| GEAR-2† IronScrap dead-end loot material (0 recipes/code) | P2 | NO | none (crafting-chain orphan check is SOFT + ing_*-scoped) | New `[material-sink]` HARD: every droppable material id ∈ recipe-consumed union |
| GEAR-3† Merged blink-armor rows lack makersMark/flavor schema | P3 | NO | none | `[gear-schema]`: consistent required-field set incl makersMark on themed rows |
| GEAR-4† DefaultArmorIdFor ↔ ReferencedDefaultArmorIds hand-dup | P3 | NO | none | `[default-armor-sync]`: reflect HeroClass, DefaultArmorIdFor ⊆ ReferencedDefaultArmorIds |
| GEAR-5† Curation gate green-skips if picks file absent | P3 | NO | none | `[curation-input]`: assert GearCurationPicks.json exists + included>0 |
| GEAR-6† Additive-never-drops → orphaned Resources rows | P3 | NO | none | `[curation-orphan]`: every Resources id ∈ (native ∪ picks ∪ default) |
| GEAR-7† GearCatalog header comment lies about load path | P3 | SOFT | none — doc-hygiene (not regressible) | Fix comment (Resources-first); optional grep-lint |

### Echo / Harvest
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| ECHO-1† Non-Harvest lanes are write-only stubs; picker advertises +% | P2 | NO | none (echo-spec asserts write side only) | New `[echo-lane-consumers]`: a production system READS each lane mult (delta==mult) |
| ECHO-2† EchoLaneBonuses comment lies "Read by EchoService" | P3 | NO | none | Extend [echo-spec]: RatePerSecond reflects HarvestBonusMult holder |
| ECHO-3† Founding Frosthowl identity contradicts auto-Harvest | P3 | NO | none (Group4 fixture ENSHRINES the bug) | Group1: founding echo (Order==1) PreferredLane==Harvest + fix Group4 fixture |
| ECHO-4† "Echo Leveled Up to 1!" copy on new/founding card | P3 | SOFT | [ui-mvvm] (allowlist only, report-only) | New assert [echo-spec]/[dialogue]: Build(count 1/2) must NOT contain "Leveled Up" |
| ECHO-5† SiloCapacity fill-time not constant under specialization | P3 | NO | none | Fill-time invariant: SiloCapacity/RatePerSecond ~ SiloCapHours at low & high specSum |

### Tutorial / FTUE / Onboarding
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| FTUE-01† Founding-Echo card fires prematurely + retroactively on old saves | P2 | NO | none (card decoupled from FTUE step chain) | New `[founding-teach]`: 4-part EvaluateFoundingTeach gate (premature/veteran-migration/dead-gate/PanelManager) |
| FTUE-02† Card entity (Frosthowl) ≠ granted pet (aether-sprite) | P2 | NO | none (echo-spec & econ-meta never cross-ref) | New `[founding-identity]`: ByCount(1) vs StarterPetSpecies == one reconciled entity |
| FTUE-03† hub_anchor RCA comment wrong (Heart at origin, not 5000,5000) | P3 | NO | none | Extend [core-world]/[tut-anchors]: hub_anchor near Heart(origin), far from Arena(5000) |
| FTUE-04† Guided-build auto-complete treats Gate as tower; disagrees w/ live criteria | P3 | NO | none | New `[tut-defense-complete]`: auto-complete & live-signal predicates agree; Gate≠tower step |
| FTUE-05† Oracle accepts structure_placed:<id> without resolving id | P3 | SOFT | [tutorial-steps] (present, asserts nothing) | Extend CheckTutorialSteps: <id> ∈ CatalogRegistry AND ∈ FoundingKit |

### BuildMode
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| BM-A1† one-free-total palette advertises FREE on whole catalog | P2 | NO | none | Extend StrategicPlacementRegression: post one non-founding burn, all non-founding `Freebie`==false |
| BM-A2† one-free-total + FoundingKit branch: zero behavioral coverage | P2 | NO | none (StrategicPlacement has 7 gates, none touch it) | New `GateEight_FreeBuildLedger`: full contract (a-d) incl null-state no-exploit |
| BM-A3† Founding freebie check case-sensitive vs OrdinalIgnoreCase | P3 | NO | none | Add to GateEight: seed 'PET-HOUSE', query 'pet-house' → not re-granted |
| BM-A4† Founding freebie not FTUE-gated (veterans get 3 extra) | P3 | NO | none (contract ambiguous) | Add to GateEight after PO ruling: veteran state → ruled intent |

### UI-MVVM
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| UIMVVM-1† BuildMenu affordability hardcoded stub → 2 towers unbuildable + free-build exploit | P2 | NO | none ([build-econ] never touches BuildMenu/ChargeLedger) | New `[tower-affordability]`: 4 TowerVariantDef buildable at real cost; ChargeLedger honors short wallet |
| UIMVVM-2† NPCUpgradeVM caches EconomyService for station lifetime | P3 | NO | none (build-upgrade covers a different VM) | New `[npc-upgrade-vm]`: create w/ null economy, then affordable → purchase succeeds |
| UIMVVM-3† WO-744 over-claim: BattleHudUgui VM inert behind default-OFF flag | P3 | NO | none | Add to [combat-atb]: default prefs → `BattleHudVm`==true & HUD binds via VM |

### VFX / Audio
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| VFX-1† Loop-bucket leak half-fixed; "neither bucket pins" false for loops | P2 | NO | none (vfx checks are data-only resolvers) | New `[vfx-loop-lifecycle]`: N>_maxActiveLoops unstopped persistent loops → 26th returns live handle |
| VFX-2† No join of motion-castings vfxKey/sfxId back to catalog/clips | P3 | NO | none | New `[motion-castings-vfx-join]`: vfxKey∈HovlVfxCatalog, sfxId→Resources/Sfx clip |
| VFX-3† _hovlKeyOf write-only dead state, unbounded growth | P3 | SOFT | none — code-quality (remove dead dict) | Prefer code fix; optional Count-growth assert in loop-lifecycle oracle |

### BLIND-1 — NavMesh runtime wall-carve (all NEW except F3)
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| BLIND-1-F1† Arch stays PathComplete after runtime flank-carve | P2 | NO | none (GateNav/SpawnPath edit-mode, carve-blind, not in RunAll) | New PlayMode oracle: load hall+OuterWorld, settle carve, CalculatePath through each arch = Complete + beside = unreachable; emit marker |
| BLIND-1-F2† Flank-carve doorway net-passable (not sealed by doorwayHalf/padding) | P2 | NO | none | Same oracle: residual gap between ArchL/ArchR proxies > agent radius; PathComplete thru centre |
| BLIND-1-F3 (#12) Tower wall doesn't sever enemy/hero→Heart | P2 | NO | none (AutoPilot CheckStranded hero-only, passive, not gated) | New oracle: contiguous tower row spawn→Heart via BaseLayoutLoader; CalculatePath(spawn/hero→Heart)=Complete or rejected |
| BLIND-1-F4† Runtime PathComplete+ring machinery exists but not a gate | P3 | NO | CastleNavTopologyDiag (opt-in diagnostic, not in RunAll) | Promote it: always-on Heart-targeted headless w/ REGRESSION marker; register in RunAll |

### BLIND-2 — Collector upgrade-id contract (all NEW)
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| BLIND2-F1† ResolveUpgradeId(collector_*→bare) has ZERO binding (the shipped device fix) | P2 | NO | none (core-catalog: 0 refs) | Add to core-catalog: ResolveUpgradeId('collector_lumbermill')=='lumbermill' (farm/forge) + non-collector passthrough |
| BLIND2-F2† building-tiers ladder under bare collector ids unvalidated | P2 | NO | partial: troop-roster (barracks only) | Extend build-upgrade/core-catalog: lumbermill/forge→IsUpgradable+TierOf!=null; farm→ResourceProgression |
| BLIND2-F3† [build-upgrade] tests WRONG system for lumbermill+forge (false confidence) | P3 | SOFT | [build-upgrade] (exercises ResourceProgression; VM routes city-tiers) | VM oracle: BuildingUpgradeVM.CreateDefault(collectorId) → grid Perks.Count>0/MaxTier>=2, Title!=empty |
| BLIND2-F4† 3 near-miss lumber ids, no cross-file join test | P3 | NO | none | In CheckStructures: non-empty collectorBuildingId resolves to BuildingTierCatalog OR ResourceProgression |
| BLIND2-F5† Two divergent resolution idioms, no test binding them | P3 | NO | none (repair paths standalone, 0 collector refs) | core-catalog one-liner: (entry.collectorBuildingId ?? id)==ResolveUpgradeId(entry.id) |

### BLIND-3 — Portal-id → enabled-scene join (all NEW)
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| BLIND-3-F1† AuthoredPortal id → enabled/loadable-scene join unasserted | P3 | NO | none (scene-route enumerates SceneRouter consts only) | New `AuthoredPortalSceneJoinRegression`: each id resolves to an ENABLED scene via verbatim‖'Dungeon_'+id predicate |
| BLIND-3-F2† dg_starter_loop east route: 0 oracle either path; disable → silent no-op | P3 | NO | none (not a SceneRouter const) | Same oracle: composed ids as load-bearing enabled-scene asserts; LoadDefs-injected ids enabled |
| BLIND-3-F3† EnterDungeon verbatim/prefix dual-resolution untested | P3 | NO | none | Resolution table-test: {dg_starter_loop verbatim, FolksGranary→Dungeon_FolksGranary, HealersCottage→prefix} |
| BLIND-3-F4† FolksGranary classified deferred-NOTE yet is a live AuthoredPortal | P3 | SOFT | scene-route (report-only NOTE, cannot fail) | Reconcile: any AuthoredPortal/LoadDefs id promoted from NOTE→load-bearing, must be enabled; flag drift |

### BLIND-4 — New camera flags, no default pin (all NEW)
| Finding | Sev | Covered? | Covering suite | Proposed regression |
|---|---|---|---|---|
| BLIND-4-F1† ff.dungeonfpv/dungeoniso defaults pinned by NO suite (silent flip ships green) | P2 | NO | none (existing flag tests restore prefs, never assert unset default) | New `[feature-flag-defaults]`: DeleteKey → DungeonFpv==false AND DungeonCameraIso==false |
| BLIND-4-F2† DungeonCameraRig.ResolveMode default (OverShoulder) asserted by nothing | P2 | NO | none (no DungeonCameraRig test anywhere) | Assert ResolveMode default==OverShoulder under cleared prefs (instantiate or proxy flags) |
| BLIND-4-F3† Proposed FeatureFlagDefaultsOracle unbuilt + DungeonPortals-only scope; defaults-pin gap is systemic | P3 | NO | none (oracle does not exist) | Broaden [feature-flag-defaults] to walk EVERY FeatureFlags getter, pin defaultOn under cleared prefs |
| BLIND-4-F4† MASTER_CATALOG describes pre-07-17 iso rig — canon stale | P3 | SOFT | none — §15 canon | Doc fix (OTS default, 3 CamModes, 2 new flags) |

---

## UNCOVERED — NEEDS A REGRESSION (master actionable list)

68 findings need a new or extended hard oracle (61 zero-coverage + 7 partial-SOFT to harden). The 5 §15 doc / code-quality items (GEAR-7, CS-4, CS-5, VFX-3, BLIND-4-F4) are excluded — fix by editing the source/doc, not by an oracle.

**Highest-leverage roots that close multiple findings at once:**
- **CS-3 (exhaustive body-aware persistence round-trip)** subsumes CS-1 + CS-2 and self-enforces the field count (moots CS-5).
- **EW-1 all-7-builders divergence oracle** closes EW-1, EW-2, and generalizes to EW-4 (hollow ids).
- **One BLIND-1 PlayMode NavMesh oracle** closes F1 + F2 + F3 (arch openings + tower-wall→Heart) and F4 promotes the existing dormant machinery into it.
- **[feature-flag-defaults] walking every getter (BLIND-4-F3)** closes BLIND-4-F1, F2, and DGN-P2-3 (DungeonPortals) in one suite.
- **AuthoredPortalSceneJoinRegression (BLIND-3-F1)** closes all four BLIND-3 rows.
- **New `[modal-arbiter-conformance]` source-lint** closes HUD-1 + HUD-2.
- **[econ-meta] pack-integrity + PackEntitlement + ApplyPackContents** closes the 3 economy P1s.

Full per-finding proposed tests are in the `uncovered` array (silo, finding, severity, concrete assertion).

---

## Verdict

**No — our regressions do not cover everything found. Not close.** Of 73 findings across all silos, **zero have a hard, defect-specific oracle** that would go RED on the actual bug; the entire tree of green markers passes over all of them. **12 findings have only SOFT coverage** (partial-site, parse-only, wrong-system, or report-only lints — e.g. [combat-atb] sees 2 of 7 orc-raider sites, [core-catalog] parse-checks waves.json without schema-mapping it, [build-upgrade] exercises the wrong progression system for 2 of 3 collectors), and **61 have nothing at all.**

Most urgent: **all 5 P1s are completely unguarded** — three monetization referential-integrity failures (dangling pack cosmetics, split-brain ownership, advertised-but-ungranted Glimmer), the dungeon roach-motel, and the waves.json schema gap that can silently zero the main defense loop. **68 findings (of which ~68 are NEW, missed by GAP_AUDIT — only 5 map to numbered audit rows) need a new or extended regression.** Seven high-leverage root oracles (exhaustive save round-trip, all-sites enemy divergence, one PlayMode NavMesh reachability suite, a global feature-flag-defaults walk, the authored-portal↔scene join, a modal-arbiter source-lint, and pack economy integrity) would collapse roughly half the list; the remainder are per-finding suites already specced above.

---

## Uncovered findings needing a regression (actionable list)

- [P1] Economy/Monetization - ECON-1: Flagship paid packs grant cosmetic SKUs absent from cosmetics.json (15 dangling incl founders-vow) -> Extend [econ-meta] EconomyMetaCatalogRegression with CheckPackCosmeticIntegrity: load cosmetics.json ids into a set; for every pack assert each id in contents.cosmetics[] exists in that set. RED on the 15 absent SKUs.
- [P1] Economy/Monetization - ECON-2: Split-brain ownership — pack cosmetics land in GameState.OwnedItemIds but shop reads GlimmerCurrencyService -> New PackEntitlementRegression: PackStoreVM.RecordOwned a pack cosmetic SKU, then assert CosmeticShopPanel.OwnsId / GlimmerCurrencyService.Owns returns true for it.
- [P1] Economy/Monetization - ECON-3: Paid packs advertise Glimmer on every card but never grant it -> Extend [glimmer] (or PackPurchaseRegression): run PackStoreVM.ApplyPackContents against fresh wallet, assert glimmer balance delta == advertised contents.glimmer (and convenience tokens).
- [P2] Economy/Monetization - ECON-4: BattlePass premium debits 2400 Glimmer before content/null check; no BattlePassData asset -> New BattlePassRegression: assert a BattlePassData ScriptableObject exists; reflect PurchasePremiumPass with battlePassData==null and assert glimmer UNCHANGED and _hasPremium==false.
- [P3] Economy/Monetization - ECON-5: BattlePass is a dead unreconciled third persistence store, header lies -> New MonetizationStoreReconciliationRegression: assert BattlePassManager has a live caller AND reconciles with the same ownership store pack cosmetics use, or is removed.
- [P1] Dungeons/RoomForge - DGN-P1-1: Composed dg_starter_loop has no exit/return (roach motel) -> New DungeonPlayabilityOracle: headless PopulateForPlay(dg_starter_loop), assert composed root set has >=1 return-to-hub affordance (DungeonStubReturn or exit-pad calling SceneRouter.GoCastle/GoVillage). RED today.
- [P2] Dungeons/RoomForge - DGN-P2-2: dg_starter_loop.unity shipped BINARY yet enabled:1 -> Extend SceneRoutingRegression: for every enabled build scene under Scenes/DungeonCompose/, read first bytes and FAIL unless it begins with '%YAML'. dg_starter_loop 00-header -> RED.
- [P2] Dungeons/RoomForge - DGN-P2-3: FeatureFlags.DungeonPortals comment says default OFF but defaultOn:true -> New FeatureFlagDefaultsOracle (or extend scene-route): with no pref set, assert FeatureFlags.DungeonPortals == milestone intent (false). Folded into the global [feature-flag-defaults] walk.
- [P2] Dungeons/RoomForge - DGN-P2-4: dg_starter_loop has zero loot wiring -> Extend RoomForgeRegression Case9 to include dg_starter_loop.json: parse layout and assert chest/pickup count > 0 (or PopulateForPlay bakes >=1 reward object).
- [P2] Dungeons/RoomForge - DGN-P2-5: Healer's Cottage chests grant nothing (OpenChest records id only) -> New DungeonRewardOracle: OpenChest(chestId) with rewardKey and assert a real inventory/currency grant is recorded. Gate behind WO-749.
- [P3] Dungeons/RoomForge - DGN-P3-6: Composed dungeon runs on Village systems (no DungeonHero/Lantern/DungeonCameraRig) -> Extend DungeonPlayabilityOracle: after PopulateForPlay assert FindAnyObjectByType<DungeonHero>!=null and >=1 dungeon felt-mechanic object. Coordinate PO before hard ratchet (may be intentional).
- [P3] Dungeons/RoomForge - DGN-P3-7: dg_starter_loop 'loop' never closes — turn3 sealed dead-end -> Add dg_starter_loop.json to RoomForgeRegression Case9 with topology pin: connections form a closed cycle (turn3->junction edge / every non-mouth socket mated), expected sealedN==1.
- [P2] Core/Save - CS-1: equippedRingId/AmuletId are DEAD fields; real GearLoadout accessory path untested -> New [accessory-persist]: equip ring+amulet via real GearLoadout, re-create loadout to re-read PlayerPrefs, assert both restore. Add CheckPersistenceGaps flag for accessory-outside-signed-envelope, OR delete the dead seed-assertions.
- [P2] Core/Save - CS-2: Round-trip blind to 5 persisted fields (ArenaDefense, BuildJobs, AdSkipsUsedToday, AdSkipDayKey, LastHarvestClaimMs) -> Extend ApplyNewerFieldsOntoSO + AssertNewerFieldsMatch (and core-save Part H) to author non-default values for all 5 and assert each survives a real Save()/Load() round-trip.
- [P2] Core/Save - CS-3: Gap-oracle structurally blind to a schema field with no Snapshot/Apply wiring (root cause) -> Make CheckPersistenceGaps body-aware: drive Snapshot()->ApplyPersisted() seeding a distinct non-default onto EVERY reflectively-enumerated PersistedState prop, assert each returns changed. Subsumes CS-1 and CS-2.
- [P1] Enemies/World - EW-3: waves.json has no typed regression — main defense-loop schedule only parse-checked -> New [waves-schema]: deserialize both Res+Sa waves.json through the same WaveSchedule/JsonConvert path WaveDataLoader uses; assert schedule!=null AND Waves.Count>0 AND wave[0] has spawn count>0, non-empty enemy id set, cadence>0. Renamed 'waves' key -> RED.
- [P2] Enemies/World - EW-1: orc-raider divergence is 7 sites / 4 HP values + AI split (combat-atb covers only 2) -> Extend combat-atb CheckSynthesizedStatDivergence to resolve orc-raider from ALL 7 builders (RegionMobSpawner, EnemyOutpost, CampGuards, CampDefenseWave, GarrisonStatBlocks, TribeManager, WardTetherService) + enemies.json; assert one canonical {hp, archetype}.
- [P2] Enemies/World - EW-2: Divergence detector compares only 2 of 7 sites — unify-2 turns red green -> Same all-sites oracle as EW-1 + a source-count guard: enumerate every EnemyDef-building site for a Wildlands id not in enemies.json and assert distinct builders for orc-raider collapse to 1 shared source.
- [P3] Enemies/World - EW-4: OutpostEnemyGroupSpawner (live in dg_starter_loop) hollow stats diverge from enemies.json -> Generalize the divergence oracle to hollow ids: for {hollow-walker, hollow-rogue, hollow-acolyte, hollow-warrior} resolve Hp from OutpostEnemyGroupSpawner.DefFor, GarrisonStatBlocks, enemies.json and assert agreement. hollow-rogue 34 vs 70 -> RED.
- [P2] HUD/Panels - HUD-1: Pets box (EchoRoster) modal never registers with PanelManager -> New source-lint [modal-arbiter-conformance]: any first-party .cs calling ElarionUiKit.BuildObsidianModal/BuildModalCanvas that makes NO PanelManager.NotifyOpened reference (and holds no PanelHandle) fails. HardFailOnNew=true. EchoRosterView -> RED.
- [P2] HUD/Panels - HUD-2: EndStateView.Show bypasses the single-modal arbiter -> Same [modal-arbiter-conformance] lint flags EndStateView; plus play-mode: register a dummy panel, invoke EndStateView.Show, assert PanelManager.AnyOpen true AND the prior panel was closed.
- [P3] HUD/Panels - HUD-3: Non-ASCII separators survive in player-facing panel strings (VM files unscanned; wrong font-truth) -> Extend HudUiRegression.CheckTofu candidacy to *VM.cs/*ViewModel.cs display-string files (catches BuildingUpgradeVM ' · '); pin FontOracle.Resolve to the actual shipped Blink atlas, not LiberationSans fallback.
- [P3] HUD/Panels - HUD-4: EchoUnlock center dialogue + reward SFX are not scene-gated -> New [echo-unlock-scene-gate]: load a non-gameplay scene (Title/HeroSelect), raise EchoService.EchoUnlocked, assert no active EchoUnlockDialogue DDOL card AND GameSfx.PlayLevelUp suppressed (OnEchoUnlocked short-circuits via IsGameplayScene).
- [P2] Combat/Arena/ATB - ARENA-1: Fled-pack leash writes transform.position on a live NavMeshAgent (silently reverted) -> New [arena-leash]: (1) positive-control — write transform.position on a live agent, tick a frame, assert it reverted to nextPosition; (2) drive LeashStagedEnemies against a staged agent beyond leash radius, tick, assert actual post-frame position within radius. RED until code uses Warp/nextPosition.
- [P2] Combat/Arena/ATB - ARENA-2: Self-heal gated on _familyEngaged which never latches if leader dies at range (240s pin) -> New [arena-watchdog]: BeginEncounter with a FamilyLeader killed at range before it disbands (never latching _familyEngaged); assert the disengage-resolve path resolves within ~7s+margin, NOT BattleTimeoutSeconds(240).
- [P3] Combat/Arena/ATB - ARENA-3: WO-556 star-bonus gear scaling is permanent dead computation (clamped away) -> New [arena-gear-odds]: assert BattleArena per-roll gear chance ==0.04 flat across stars 1/2/3 (chance(3star)==chance(1star)); assert EnemyOutpost base==cap==0.04 parity; fail if GearDropPerStar*(stars-1) is computed but provably clamped.
- [P3] Combat/Arena/ATB - ABIL-1: Explicit castAnim path never exercised (E-hotkey cast-animation half) -> Extend [abilities]/[ability-anim]: (1) feed a synthetic AbilityDef with explicit castAnim into ResolveAnimVariant, assert it returns that variant (priority-1); (2) per real ability assert documented variant or -1 sentinel; (3) assert HudModelProducers.ResolveSlotDef routes to HeroAbilities.ResolvedDef (icon unification).
- [P2] Gear/DataCatalogs - GEAR-1: weapons/armor byte-exempt from drift+version; curation validates only id-presence -> Extend CheckGearCuration / new [gear-fields]: load weapons.json+armor.json through GearCatalog and assert per-row id/name non-empty, damage>0 (weapons), defense>=0, valid slot, valid rarity, buyCost>0 where not free; for ids in both StreamingAssets+Resources, DeepEquals shared stat fields across copies.
- [P2] Gear/DataCatalogs - GEAR-2: IronScrap is a sink-less loot material (6 loot lines, 0 recipes/code) -> New [material-sink] HARD check: union every materialId consumed across all recipe catalogs; assert every droppable kind:material id (minus documented allowlist) is in the consumed set. RED: 'IronScrap dropped by 6 loot lines, consumed by 0 recipes'.
- [P3] Gear/DataCatalogs - GEAR-3: Merged blink-armor rows lack makersMark/flavor — two schemas in one catalog -> Add [gear-schema] to CheckGearCuration: for every armor.json Resources row require a consistent field set (id, name, slot/category, makersMark) or explicit addressable-blink exemption; assert makersMark non-empty on any row ArmorVfxMap/GearAppraisal themes.
- [P3] Gear/DataCatalogs - GEAR-4: DefaultArmorIdFor <-> ReferencedDefaultArmorIds hand-maintained duplicate -> [default-armor-sync] in CheckGearCuration: reflect every HeroClass, call HeroBodySwapper.DefaultArmorIdFor (expose internal hook), assert the id set is a subset of ReferencedDefaultArmorIds and every non-seed entry is produced by some class.
- [P3] Gear/DataCatalogs - GEAR-5: GEAR_CURATION gate green-skips if GearCurationPicks.json absent -> [curation-input]: if curation is the committed shipping model (marker/flag), assert File.Exists(Editor/GearCurationPicks.json) and included-count>0, so removing the picks file goes RED instead of green-by-skip. Or promote the skip to a hard failure.
- [P3] Gear/DataCatalogs - GEAR-6: Additive-never-drops leaves orphaned Resources gear rows, no cleanup/gate -> [curation-orphan] reverse-direction check: allowed set = native Resources-only rows UNION current included picks UNION ReferencedDefaultArmorIds; assert every id in the Resources weapons/armor catalog is in that set. Requires exporter to persist a native-id manifest.
- [P2] Echo/Harvest - ECHO-1: Non-Harvest Echo lanes (Crafting/Defense/Exploration) are write-only stubs; picker advertises +% -> New [echo-lane-consumers]: per lane assert a production system READS the mult — Crafting via Forge cost/time delta==CraftingMult; Defense via city-defense delta==DefenseMult; Exploration via dungeon-reward delta==ExplorationMult. Or assert the picker doesn't advertise +% for consumerless lanes.
- [P3] Echo/Harvest - ECHO-2: EchoLaneBonuses comment lies 'Read by EchoService'; holder is write-only -> Extend [echo-spec]: assert EchoService.RatePerSecond reflects EchoLaneBonuses.HarvestBonusMult (force the holder to a sentinel via Recompute, assert rate tracks it). RED today, surfacing the write-only holder.
- [P3] Echo/Harvest - ECHO-3: Founding Frosthowl identity contradicts its auto-Harvest assignment (Group4 fixture enshrines the bug) -> Extend [echo-spec] Group1: assert the founding echo (Order==1) has PreferredLane==Harvest AND non-null HarvestResource. Update the Group4 fixture so expected AggregateHarvestMultiplier no longer bakes in the Exploration mismatch.
- [P3] Echo/Harvest - ECHO-4: Unlock banner reads 'Echo Leveled Up to N!' for a NEW echo; founding shows 'to 1!' -> New assertion in [echo-spec]/[dialogue]: EchoUnlockDialogue.Build for count 2 and founding count 1 must NOT contain 'Leveled Up'; specifically assert the count==1 banner is not 'Echo Leveled Up to 1!'.
- [P3] Echo/Harvest - ECHO-5: SiloCapacity fill-time not constant under specialization (contradicts header) -> Add fill-time invariant to [echo-spec]: compute timeToFill=SiloCapacity/RatePerSecond at specSum~0 and at high harvest specialization; assert both within tolerance of SiloCapHours*3600. Specialized case fills in ~1/(1+specSum) -> RED.
- [P2] Tutorial/FTUE - FTUE-01: Founding-Echo card fires prematurely on fresh saves + retroactively on pre-existing saves -> New [founding-teach] FoundingTeachGateRegression: (1) flag unset + FTUE not at founding_echo step -> EvaluateFoundingTeach not-eligible; (2) EchoCount=6 save without the flag -> treated as already-taught (migration guard); (3) dead 'EchoCount<1' gate actually blocks; (4) announce routes through PanelManager single-modal.
- [P2] Tutorial/FTUE - FTUE-02: Founding card entity (Frosthowl/Ice) differs from FTUE-granted pet (aether-sprite) -> New [founding-identity]: read EchoRosterCatalog.ByCount(1).Id and TutorialFlow.StarterPetSpecies (+ PetCatalog display); assert they name one reconciled founding entity or an explicit sanctioned mapping table that a rename must update.
- [P3] Tutorial/FTUE - FTUE-03: hub_anchor RCA comment wrong — Heart at origin, 5000,5000 is the BattleArena -> Extend [core-world] / new [tut-anchors]: resolve hub_anchor and assert distance(anchor, Heart@~origin) < ~25m AND distance(anchor, BattleArena.ArenaCentre 5000,0,5000) > large threshold.
- [P3] Tutorial/FTUE - FTUE-04: Guided-build auto-complete treats a Gate as a tower; disagrees with live criteria -> New [tut-defense-complete]: assert TutorialFlow auto-complete predicate and TutorialSignalAdapters live-signal predicate agree on the accepted CatalogType set for founding_defense; assert a Gate placement does NOT auto-complete the 'raise a tower' step.
- [P3] Tutorial/FTUE - FTUE-05: Oracle accepts build.structure_placed:<id> without resolving <id> to catalog/FoundingKit -> Extend [tutorial-steps] CheckTutorialSteps: for every step whose signal StartsWith StructurePlacedPrefix, strip prefix and assert <id> resolves in CatalogRegistry AND is in BuildModeController.FoundingKit. Catalog rename orphaning a step -> RED.
- [P2] BuildMode - BM-A1: one-free-total palette advertises FREE on whole non-founding catalog at once -> Extend StrategicPlacementRegression: on a state with one non-founding burn in FreeBuildsUsed, iterate non-founding CatalogRegistry entries and assert StructureCardVM.CreateForEntry(e).Freebie==false for ALL (zero advertise FREE post-burn).
- [P2] BuildMode - BM-A2: one-free-total + FoundingKit branch shipped with zero behavioral coverage -> New GateEight_FreeBuildLedger: (a) empty ledger -> both founding+non-founding FreeBuildAvailable, EffectiveCostFor==0; (b) burn one non-founding -> all non-founding charged, founding still free; (c) burn a founding id -> only it charges, general freebie live; (d) null state -> FreeBuildAvailable==false.
- [P3] BuildMode - BM-A3: Founding-kit freebie check case-sensitive (List.Contains) vs OrdinalIgnoreCase elsewhere -> Add to GateEight: seed FreeBuildsUsed with a founding id in a different case ('PET-HOUSE' while querying 'pet-house') and assert FreeBuildAvailable returns FALSE (not re-granted) — founding branch must match ledger case-insensitively.
- [P3] BuildMode - BM-A4: FoundingKit per-id freebie not gated to FTUE/first-run (veterans get 3 free) -> After PO rules the contract, add to GateEight: construct a veteran GameState (non-empty FreeBuildsUsed without founding ids, first-run flag off) and assert FreeBuildAvailable(foundingId) equals the ruled intent. Pins the behavior either way.
- [P2] UI-MVVM - UIMVVM-1: BuildMenu affordability hardcoded stub -> 2 towers unbuildable + free-build exploit -> Extend [build-econ] or new [tower-affordability]: for each of 4 BuildMenu TowerVariantDef feed a wallet at exact cost -> CanAfford true, one-short -> false; assert GetMaterialCount tracks real economy. Drive ChargeLedger with a short wallet and assert TrySpend fail is propagated (no prepaid placement, wallet unchanged).
- [P3] UI-MVVM - UIMVVM-2: NPCUpgradeVM caches EconomyService.Instance for the station lifetime -> New [npc-upgrade-vm]: construct NPCUpgradeVM.CreateDefault while EconomyService.Instance is null, then bring the economy up affordable, then TryPurchaseUpgrade -> assert SUCCEEDS (re-resolves live economy). Second: destroy+recreate economy, assert cached _vm not holding a dead ref.
- [P3] UI-MVVM - UIMVVM-3: WO-744 over-claim — BattleHudUgui VM binding inert behind default-OFF flag -> Add to [combat-atb]/RunAll: with PlayerPrefs unset assert FeatureFlags.BattleHudVm==true, OR behaviorally build BattleHudUgui under default flags and assert _useVm==true and submenus source hero-class/ability/item lists from BattleHudVM not the in-View resolvers.
- [P2] vfx-audio - VFX-1: Loop-bucket leak half-fixed; 'neither bucket can pin' false for loops -> New PlayMode [vfx-loop-lifecycle]: PlayLoop N>_maxActiveLoops against persistent (non-destroyed, never-Stop'd) hosts, then assert the manager still admits a new loop (26th returns a live handle, not a throttled null-drop) OR a backstop reclaims stale-unstopped loops.
- [P3] vfx-audio - VFX-2: No headless oracle joins motion-castings vfxKey/sfxId back to catalog/clips -> New [motion-castings-vfx-join]: load both motion-castings.json copies, assert every non-empty vfxKey/vfxImpact/vfxProjectile is a key in HovlVfxCatalog.asset and every non-empty sfxId/sfxImpact resolves to an AudioClip under Resources/Sfx. Typo'd key -> RED naming the row.
- [P2] BLINDSPOT:navmesh-wall-carve - BLIND-1-F1: No oracle proves the arch stays PathComplete after the runtime flank-carve -> New PlayMode headless oracle: load MainCastle_Hall + additive OuterWorld, let CastleWallNavObstacleInstaller carve (~1.5s), NavMesh.CalculatePath from hero-spawn through each arch opening -> PathComplete AND a point beside each opening now unreachable. Emit REGRESSION marker; add to RunAll.
- [P2] BLINDSPOT:navmesh-wall-carve - BLIND-1-F2: Flank-carve doorway width hand-editable (doorwayHalf/padding) — no net-passable assert -> In the same oracle, after carve read the ArchL/ArchR proxy AABBs and assert the residual gap (accounting for thicknessPadding) exceeds the HeroLocomotion NavMeshAgent radius, and confirm PathComplete through the doorway centre. Optional data-guard on a doorwayHalf floor.
- [P2] BLINDSPOT:navmesh-wall-carve - BLIND-1-F3: A mid-field wall of placed towers can sever enemy/hero -> Heart (GAP_AUDIT #12, unverified) -> New headless play-mode oracle: place a contiguous tower row spanning spawn->Heart via the real BaseLayoutLoader/BuildMode path, settle carves, assert NavMesh.CalculatePath(spawn->Heart@origin) and (hero->Heart) both PathComplete, or that placement was rejected. Marker-gated.
- [P3] BLINDSPOT:navmesh-wall-carve - BLIND-1-F4: Runtime PathComplete+reachability-ring machinery exists (CastleNavTopologyDiag) but is not a gate -> Promote CastleNavTopologyDiag: add an always-on, Heart-targeted headless entry (Heart@origin extra target + ring stays fully reachable after a representative wall layout) that emits REGRESSION_OK/FAIL; register in DataRegression.RunAll. Reuses ~80% of the existing body.
- [P2] BLINDSPOT:collector-upgrade-id - BLIND2-F1: ResolveUpgradeId(collector_*->bare) has ZERO regression binding (the shipped device fix) -> Add to core-catalog (CoreCatalogRegression): after CatalogBootstrap, assert ResolveUpgradeId('collector_lumbermill')=='lumbermill', 'collector_farm'=='farm', 'collector_forge'=='forge', AND a non-collector id passes through unchanged. Fail loud if the map is identity for a collector id.
- [P2] BLINDSPOT:collector-upgrade-id - BLIND2-F2: building-tiers.json ladder under the bare collector ids (lumbermill/forge) not validated -> Extend build-upgrade/core-catalog: for each collector resolve the id then assert its family is reachable — lumbermill/forge: BuildingTierCatalog.IsUpgradable(resolved) && TierOf(resolved,1)!=null; farm: ResourceBuildingProgression.IsResourceBuilding('farm').
- [P3] BLINDSPOT:collector-upgrade-id - BLIND2-F3: [build-upgrade] tests the WRONG progression system for lumbermill+forge (false confidence) -> Add a VM-level oracle: for each of collector_farm/lumbermill/forge construct BuildingUpgradeVM.CreateDefault(collectorId) and assert the grid has >=1 tier/perk (Perks.Count>0 or MaxTier>=2) and Title!=empty — proving the classified family actually renders options.
- [P3] BLINDSPOT:collector-upgrade-id - BLIND2-F4: Three near-miss lumber ids; no structures-catalog<->building-tiers join test -> In CheckStructures: for every entry with non-empty repo.collectorBuildingId assert it resolves to EITHER BuildingTierCatalog.IsUpgradable(cbid) OR ResourceBuildingProgression.IsResourceBuilding(cbid). An orphaned collectorBuildingId -> fail.
- [P3] BLINDSPOT:collector-upgrade-id - BLIND2-F5: Two divergent resolution idioms (ResolveUpgradeId vs inline 'collectorBuildingId ?? id') with no binding test -> Add a core-catalog one-liner: for every Collector entry assert (entry.repo.collectorBuildingId ?? entry.id) == CatalogRegistry.ResolveUpgradeId(entry.id). Binds placement and upgrade idioms so changing one without the other trips red.
- [P3] BLINDSPOT:portal-scene-join - BLIND-3-F1: No oracle asserts the AuthoredPortal id -> enabled/loadable-scene join -> New AuthoredPortalSceneJoinRegression (RunAll, covenant-style): reflect DungeonWorldPortalSpawner.AuthoredPortals, build the enabled-scene set as SceneRoutingRegression does, and for each authored id assert enabled.Contains(id)||enabled.Contains('Dungeon_'+id). Fail naming any id resolving to no enabled scene; also assert LoadDefs-injected ids enabled.
- [P3] BLINDSPOT:portal-scene-join - BLIND-3-F2: dg_starter_loop composed east route has zero oracle; a build-settings disable is a silent no-op -> In the same oracle add composed-dungeon ids as an explicit load-bearing list (not deferred NOTEs): assert dg_starter_loop is an enabled build scene AND its .unity guid resolves, so a future disable (as with d4_sunken_crypt) goes RED instead of silently dropping the east portal.
- [P3] BLINDSPOT:portal-scene-join - BLIND-3-F3: DungeonPortal.EnterDungeon verbatim-then-prefix dual-resolution untested -> Pure resolution helper table-test: for {(dg_starter_loop->dg_starter_loop verbatim), (FolksGranary->Dungeon_FolksGranary prefix), (HealersCottage->Dungeon_HealersCottage prefix)} assert the same predicate EnterDungeon uses picks the expected enabled scene.
- [P3] BLINDSPOT:portal-scene-join - BLIND-3-F4: FolksGranary classified deferred-NOTE (never-fail) yet is a live west AuthoredPortal -> Reconcile the two classifications in the new oracle: any dungeon id in AuthoredPortals (or LoadDefs-injected) is promoted from deferred-NOTE to load-bearing and MUST be enabled; emit a cross-check flagging the drift ('FolksGranary is a live AuthoredPortal but scene-route treats its scene as deferred').
- [P2] BLINDSPOT:camera-flag-defaults - BLIND-4-F1: ff.dungeonfpv / ff.dungeoniso defaults pinned by NO suite (a silent flip ships green) -> New [feature-flag-defaults] in RunAll: DeleteKey('ff.dungeonfpv') and DeleteKey('ff.dungeoniso'), assert FeatureFlags.DungeonFpv==false AND DungeonCameraIso==false (unset-pref default), restore in finally. Cite owner 2026-07-17: OTS is default, FPV/iso opt-in.
- [P2] BLINDSPOT:camera-flag-defaults - BLIND-4-F2: DungeonCameraRig.ResolveMode default branch (OverShoulder) asserted by no test -> Assert DungeonCameraRig ResolveMode default == OverShoulder under cleared prefs — instantiate the rig, call Bind, inspect private _mode via reflection, or use the cheap proxy (DungeonFpv==false && DungeonCameraIso==false under DeleteKey).
- [P3] BLINDSPOT:camera-flag-defaults - BLIND-4-F3: The defaults-pin gap is systemic — proposed FeatureFlagDefaultsOracle is unbuilt and DungeonPortals-only scoped -> Broaden [feature-flag-defaults] to walk EVERY FeatureFlags getter (petcombat/barracks/colosseum/wallstab/poicallouts/dungeonfpv/dungeoniso/dungeonportals...) and pin each defaultOn value under cleared prefs — closes this whole class (and DGN-P2-3) in one suite.
