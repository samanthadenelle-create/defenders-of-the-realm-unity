# Regression Coverage Audit - 2026-09-04 (evening, before the production regression)

Read-only audit, verified from files opened this session (never from a doc). Branch
`feat/synty-art-retheme`. Numbers below are as counted at the time of the audit; the suite count
is read off the `REGRESSION_OK` marker on a fresh log, never off this page (CLAUDE.md s8).

Trigger: owner directive 2026-09-04 - "I want a full regression run, I want to ensure we have proper
regression coverage." This page records what "proper" is missing.

## 1. How suites register (the mechanism, not a count)

- `Assets/Editor/Regression/DataRegression.cs` `RunAll()` - one call-site per line of the shape
  `if (!X.Run(out var r)) failures.Add(r); else log.AppendLine("[tag] " + r);` between the
  `START FENCE` (~:289) and `END FENCE` (~:1495) comment fences. No attribute, no reflection.
- The denominator is DERIVED from source: `RegressionMarkerRegression.TryGetExpectedSuiteCount`
  (`RegressionMarkerRegression.cs:982-1035`) counts `.Run(out` between the fences; `DataRegression.cs:1523-1560`
  fails on a shortfall only.
- Inline groups inside `RunAll` (not in the marker): the gear block (:55-92) + 25 `Check*` calls +
  `AssetMoveManifestRegression.Verify` before the fence, `CheckItemIconCoverage` (:1563) and
  `CheckTutorialSteps` (:1571, pins the 8-beat mandatory arc at :1663-1664) after it.
- CLAUDE.md s8's "101 registered suites + 26 inline groups" is ~260 stale - the duplicated-state class
  that file itself warns about. Read the marker.

### 1.1 FINDING - ten suites on disk were registered NOWHERE (fixed the same evening)

Commit `1ef5f6ad4` (2026-09-04 17:34) added fourteen suites and registered FOUR. These ten existed,
compiled or not, and never ran once: `AwaySummaryReportRegression`, `HireReinforcementsRegression`,
`RaidDiscoverabilityCopyRegression`, `RaidEscalationRegression`, `RaidFunnelRegression`,
`RaidGoldArrowRegression`, `RaidLootCurrencyRegression`, `RaidPayoutVisibilityRegression`,
`RaidSeasonXpRegression`, `StarterArmyGrantRegression`. Two of them (`AwaySummaryReport`,
`HireReinforcements`) referenced runtime members that had never landed - the compile gate caught
that at 19:00 (`Builds/compile-gate.log`), which is the only reason anyone looked.
`RaidEscalationRegression.cs:49` carries its intended registration line inside a comment.

Resolution 2026-09-04: all ten registered in `DataRegression.cs` after the heartfire line, with a
dated comment; the two missing runtime halves (Lane G away summary, Lane D gold hires mercenaries)
implemented in the same wave. Whether they PASS is read off the next `REGRESSION_OK` marker.

## 2. Area map (summary; the fence is the authority)

| Area | Approx suites | Thin spots |
|---|---|---|
| Core / Save / State | 13 | BaseLayout round-trip only ever N=1 (`CoreSaveRegression.cs:479-520`, `:582-612`) |
| BuildMode / Structures | ~40 | `BuildEconomyRegression.CheckBaseLayoutReplay` (:592-640) is 3 synthetic records, no save path |
| Waves / Enemies / Siege | ~30 | `SiegeLossStakesRegression.cs:589-635` asserts persisted stays - the opposite direction of the P0 |
| Hero / Abilities / Gear / Talents | ~55 | `Village/Hero` 36 of 105 files unnamed by any suite/test |
| Combat / ATB / Arena | ~16 | `ATBRuntimeState` has no test |
| HUD / UI | ~34 | strong (MVVM/Obsidian ratchets, capture coverage, `MaxVisibleFaces` pinned) |
| Dungeons | 26 | strong |
| Raids / Troops / Army | 18 (+7 that were unregistered) | see 1.1 |
| Economy / Harvest / Echoes | ~42 | `CrystalProductionRegression.cs:166` = the only BaseLayout save/load assertion (one record, level only) |
| Wallet / Store / Monetization / Ads | 31 | `Core/Payments` 10 of 14 files unnamed - receipt settlement + grant applier |
| Data catalogs / Content / Addressables | ~18 | ok |
| VFX | 15 | ok |
| Audio | 4 | 23 files, `BattleMusicManager` / `TowerAudioController` unnamed |
| Onboarding / FTUE / Dialogue | ~21 | ok |
| Scenes / Routing / World | 14 | `CinemachineCameraController`, `DragonCinematicFlyby`, `NightTorchLightSystem` zero oracles |
| Ops / Tunables / Tooling | 6 | ok |

Directories with ZERO suite references (name-reference proxy; over-credits, never under-credits):
`_Modules/GooglePlay/`, `Core/Payments/` (10 of 14), `Core/Referral/`, `Core/Monetization/`
(`AdConsentService` - consent gating, legal exposure), `Core/Events/`, `Village/Camera/`,
`Village/Campaign/`, `Village/Cinematics/`, `Environment/`, `Data/`, `BattleATB/State/`,
`Village/Audio/` (5 of 7), `Core/Web3/`.

## 3. Hollow passes (runbook s4)

- Ledgered, in code, still present: `KnownHollowSites` at `RegressionMarkerRegression.cs:248-307` -
  27 rows, every one "OWED A RESOLUTION" (:218). 27 greens that are partly unearned.
- Candidates NOT in the ledger (narrow-window find; for the scanner to judge):
  `QuestCompletabilityRegression.cs:1521-1525` (missing tracked source -> note, not fail),
  `DataWebRegression.cs:626,629` (`arr == null` returns true with no excuse),
  `BuildEconomyRegression.cs:1577-1581` (depends on `LogFallbackVerdict` on zero rows).
- Shape B "OK, 0 checked" is ADVISORY only (RULE 5, `:1107-1136`, counted never failed) - the honest
  admission that an empty discovered collection cannot red a suite. Fix is a ~150-suite contract change
  = a work order.
- 55 suite files use `RegressionOutcome.Skip/PartialSkip`; 9 carry `hollow-pass-ok` opt-outs.

## 4. THE P0 HAS NO ORACLE - save data loss

`[Flow:BaseLayout] Enter build mode CENSUS: live=9 loader.Loaded=9 persisted=17` (emitter
`BuildModeController.cs:513-523`, a `FlowTrace.Step` that can never fail). Ticket: WO-1361 (READY, P0).
No registered suite, inline group, EditMode or PlayMode test asserts live == persisted after a load,
or that N > 1 records survive save -> reload -> migrate.

Seams where an oracle attaches (read at source):
1. `BaseLayoutLoader.LoadFromState -> Rebuild(layout)` (`BaseLayoutLoader.cs:140-170`, ~:203) - assert
   `Loaded.Count == N` and `FindObjectsByType<PlacedStructure>().Length == N`; also assert the home hub
   `Main_Castle_Overworld` is NOT in `_hubScenesNoBaseLayout` (:145-156) - the 08-19 captures read 0 loaded.
2. `BaseLayoutLoader.Spawn` (:258) - two `return null` drop points (:265 unresolvable id, ~:301 factory
   null, "THE WORST SEAM"): every persisted itemId must resolve AND `StructureFactory.Create` must return
   non-null, over the REAL ids the game persists.
3. `SaveMigrator` (:308, :643-645) + `GameStateService` (:441, :582, :706, :1250) - the places the list is
   REPLACED. Round-trip N records through the real save path; `CoreSaveRegression` does N=1.

Resolution in flight 2026-09-04: `BaseLayoutRoundTripRegression` (seam 3, headless data oracle)
authored and registered this evening. Seams 1-2 need a scene (PlayMode) and remain OPEN under WO-1361.

## 5. Tests on disk

- `Assets/Tests/EditMode/` 89 files; `Assets/Tests/PlayMode/` 2 files (`EnemyResolverSpawnTests`,
  `VillageSmokeTests`); module-local: `BattleATB/Tests` 10, `Core/Tests` 10, `HUD/Tests` 1, `Wallet/Tests` 5.
- `Builds/test-results-EditMode.xml` EXISTS (931,578 bytes, mtime 2026-08-31 07:41,
  `total="1033" passed="1033" failed="0"`). Four days and many commits stale - not evidence for today's
  tree. (An earlier receipt this session said "not found"; that was a mangled shell path, corrected here.)

## 6. Top 10 gaps, ranked by player cost

1. No live-vs-persisted structure oracle (P0 progress loss) - partially closed by seam 3 tonight; seams 1-2 open.
2. Ten new suites unregistered - closed tonight.
3. `Core/Payments` + `GooglePlay/` unexercised on the live money path.
4. 27 ledgered hollow-pass sites.
5. Shape-B "OK, 0 checked" advisory only.
6. `AdConsentService` untested.
7. PlayMode is two files - the loader/NavMesh/scene-replay defect class has no automated home.
8. `Village/Hero` one-third dark by name-reference.
9. Camera / cinematic / night-light controllers: zero oracles.
10. Audio: 4 suites for 23 files.

## Verdict

Large and unusually self-policing (derived denominator, unique markers, control-flow hollow-pass
detection) - but NOT yet "proper coverage": the single worst open defect had no oracle at any seam,
ten suites for today's feature drop ran nowhere, and 27 ledgered hollow sites plus an advisory-only
Shape-B rule mean a slice of the greens is not evidence. Strong on data-shape and source-lint
invariants; thin on scene-runtime invariants, which is exactly the axis the P0 lives on.
