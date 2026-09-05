# HANDOVER 2026-09-05 - overnight run (CLI, owner asleep from ~00:00)

Orders: `OVERNIGHT_ORDERS_2026-09-05.md`. Baseline at bedtime: commit `65d5a7eae` (pushed, owner-authorised,
23:5x), installed on the Seeker as build 2026.09.05.355952. **Nothing pushed since** (standing order).

Every line below names the log/PNG/commit it was read from. Anything without evidence is marked as such.

## Timeline (fill as it happens)
| time | what | evidence |
|---|---|---|
| 00:12 | first combined gate on lanes 1391+1393+1392+silo: RED, CS1657 `out _` shadowed by a `using var _` | `Builds/compile-gate.log` 00:13; fix at `ResourceCollectorService.cs:157` |
| 00:14 | `COMPILE_GATE_OK` | `Builds/compile-gate.log` mtime 00:14:42 |
| 00:17 | `REGRESSION_FAIL 377/378` - `echo-spec` pinned the OLD burn ("silo reset") | `Builds/regression.log` 00:17 |
| 00:19 | re-pinned `EchoSpecializationRegression.CheckDumpCredit` to conservation (pool == banked + retained); `COMPILE_GATE_OK` | `Builds/compile-gate.log` 00:19:19 |
| 00:21 | **`REGRESSION_OK 378/378 suites`** | `Builds/regression.log:116320`, mtime 00:21:27 |
| 00:22 | `UI_CAPTURE_OK` (91 PNGs regenerated) | `Builds/ui-capture.log` 00:22:33 |
| 00:23 | `RunRegisteredSecondaryCaptureHeadless`: **RED** `geometry=2 touch=2` - the WO-1391 card's LockBtn covers the Bonuses label (was green at baseline, `ui-reskin-registered-secondary-v9.log`) | `Builds/ui-capture-secondary.log`; `Builds/ui-capture/BuildingUpgrade_2670x1200.png` |
| 00:24 | WO-1391 lane resumed with the exact `[UICap-GEO]` lines; WO-1389 + WO-1388 lanes dispatched (file-disjoint) | this doc |
| 00:26 | `WELCOME_BACK_CAPTURE_OK 3/3` - per-resource rows, "9h 30m (STORAGE FULL)" | `Builds/ui-capture-welcomeback.png` / `Builds/ui-capture/WelcomeBack_2670x1200.png` |

## Builds installed on the Seeker
- 2026.09.05.355952 (bedtime baseline, 65d5a7eae) - still the installed build at 00:24 (adb devices: SM02G4061955851, battery 23%).

| 00:29 | 1391 layout fixed (bonus zone budgeted from the post-scale body; pills+tabs moved into the Close band): **`REGISTERED_SECONDARY_CAPTURE_OK 33/33 touch=clean`** | `Builds/ui-capture-secondary.log` 00:29:26; PNG regenerated |
| 00:31 | committed the gated set by explicit path | `da90ddc0f` |

## Commits (local only)
- `da90ddc0f` feat(ui,harvest): WO-1391 upgrade page, WO-1393 close-frame grace + queue drawer, WO-1392 harvest never burns (49 files). Gate evidence: COMPILE_GATE_OK 00:19, REGRESSION_OK 378/378 00:21 (before the 1391 layout fix), COMPILE OK + REGISTERED_SECONDARY_CAPTURE_OK 00:29 (after it). The full regression re-runs at the next combined gate before any APK.

## What the headless proofs already show (before any device run)
- WO-1391: preview = catalog icon, never noise (`[Flow:UpgradeUI] preview model NOT resolved for 'arcane-tower@1'` -> `model band built ... as ICON`); the sentence names the shortfall (`Short 1280 Wood, 800 Gold`); kit CLOSE. Open: the card bands overlap (above).
- WO-1392: welcome-back rows are per resource from `PendingByResource()`; storage-full warning in words.
- WO-1393 / 1392 / silo: pins green inside 378/378.

## Rulings needed in the morning (one word each)
1. `arcane-tower` tier-1 authors `costCrystal: 1280` but `BuildingUpgradeService.TierCost` charges WOOD by tier number; the page shows the CHARGED lane. Which is right - the data or the service? (trace: `[Flow:Upgrade] arcane-tower tier-1 authors costCrystal=1280 (costWood=0) ...`, `ui-capture-secondary.log`.)
2. `BankOverflowStatus` has no retained-vs-lost field, so the silo dump row still uses the modal's generic sentence even though nothing is burned any more. Add the field (schema-free, in-memory) - yes/no?
3. WO-1389 Q1-Q3, WO-1388 pack name / basket / badge (unchanged from the WOs).

## Not done (and why)
- (fill)
