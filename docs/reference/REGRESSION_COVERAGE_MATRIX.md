# REGRESSION COVERAGE MATRIX — known dictionary

> **CURRENT = this file, measured 2026-09-06** (Sunday sweep step 4). Every row was opened at source
> tonight. **Prior content is NOT rewritten, it is preserved**: the 2026-08-16 §A census (21 days old)
> and the frozen 2026-07-19 ledger (49 days old) live at
> `git show bd798ad73:docs/reference/REGRESSION_COVERAGE_MATRIX.md`. Nothing from them is carried
> forward unverified — their counts are stale (fence was `:276–:861`, now `:289–:1643`; 193 suite
> files, now 436).

## Census (measured tonight)

| Fact | Value | Source |
|---|---|---|
| Registrations inside the fence | **417** | `Assets/Editor/Regression/DataRegression.cs`, fences `:289`→`:1643`, gate regex `RegressionMarkerRegression.cs:502-503` |
| `.cs` in `Assets/Editor/Regression/` | 436 | `(Get-ChildItem …).Count` |
| Node suites | **34** in `test/*.test.js` (`game.load.test.js` added under WO-1502) | `Get-ChildItem test` |
| Last gate run | `registered oracle suites: 354 (354 green)` | `Builds/data-regression.log`, mtime **2026-09-03** — 3 days old, **63 behind the 417 now registered** |
| Gates that run `node --test` | **ZERO** | grep of `*.ps1` — still no gate. WO-1502 added `package.json` `scripts.test` = `node --test test/*.test.js`, so `npm test` now RUNS (it did not exist before); wiring it into a gate is item 1 below and is still open |

## Matrix — WO-1444…1497 (all minted 2026-09-06; sources: the WO files + `docs/GET_WELL_PLAN_2026-09-06.md` §4/§6, dated today)

Severity: **no ticket carries a `Severity:` line and no ticket body carries a P0/P1/P2 token** (both greps run). A label appears below only where `docs/GET_WELL_PLAN_2026-09-06.md` §1/§4/§6 names that ticket, or the ticket's own Status qualifies it. `—` = genuinely unstated, not omitted.

| WO | Title (short) | Sev | Covering suite / case | Class | A new suite must MEASURE |
|---|---|---|---|---|---|
|1444|Manage QUEUE count never painted|SPEC|`ManageQueuePanel8Regression.cs:86` `[tab-counts-real]`|PARTIAL — pins overlay tab counts, not the bar face|painted glyph on the face vs the composed value|
|1445|Offline `Grant` drops remainder|low|`TownBankCapRegression.cs:855` `[one-reader]`|PARTIAL — only that `ClampGrant` is *called*|drive `Grant` at cap; remainder lands in the pending store|
|1446|`auth_sessions.signed_at` absent live|P0|`test/auth.session.renewal-cap.test.js:113`|PARTIAL — greps `schema.sql`; untracked, ungated; no migration exists|`information_schema.columns` vs columns the handlers write|
|1447|Cloud load restores 7 fields|P0|NONE (`BackendSaveAuthRegression.cs:34` = auth only)|UNCOVERED|round-trip structures+army+queue through `LoadFromBackend`|
|1448|Scene enter overwrites local|—|NONE (no suite names `PersistenceBridge`)|UNCOVERED|stale vs fresh server row; the stale one is refused|
|1449|builders-hour unbuyable|P0|`test/purchases.quote.test.js:120`, `google-play-payment-provider-surface.test.js:9`|COVERED but UNGATED — **ticket stale, fixed at HEAD; 32/32 and 9/9 pass**|wire both into the packs gate|
|1450|Aggro probe log 320/s|P0|NONE|UNCOVERED|lines/second per system, headless|
|1451|TowerPreviewCamera RenderPass|P0|NONE (absent from all suites)|UNCOVERED|error count on a headless preview open|
|1452|Session cap beaten by junk nonce|—|`test/auth.session.renewal-cap.test.js:81`|UNCOVERED — **the case PINS the bug** (asserts `sessionHeader && !nonceHeader`)|which credential verified; `signed_at` carried into the INSERT|
|1453|Signature rail 500s on redeem|—|`test/auth.rawbody.session.test.js:55`|UNCOVERED — **that case asserts the 500 is correct**|a real signed payload → 200; tampered → 401|
|1454|Transient 5xx clears session|—|NONE (`ClearSession` in zero suites)|UNCOVERED — **fix already at HEAD**, `BackendRequestSigner.cs:606` gates on `IsCredentialRefusal`|keep-vs-clear over 401/403/500/503/timeout|
|1455|Sync queue uncapped|—|NONE|UNCOVERED|cap holds at 200; one warning per depth crossing|
|1456|No rate limit on auth/nonce|—|`test/auth.nonce.budget.test.js` (new tonight, ungated)|PARTIAL — nonce half landed (`nonce.js:34`); **`session.js` still has no budget**|renewal: #1 200 **and** #N+1 refused|
|1457|`schema_version` goes backwards|—|`test/game.save.schema-version.test.js` (new tonight, ungated)|**FIXED at HEAD** — `save.js:412` `GREATEST(...)`, refusal codes `:106-107`|wire the test to a gate|
|1458|Raid walls admitted hostile|—|`BreakableContainerChestRegression.cs:166` `[hostile-admit-instrumentation-intact]`|PARTIAL — lints that the literal survives|admit set membership for `WallSegment` in a raid base|
|1459|Raid 26 fps median, 11 worst|—|`VfxPerformanceGateRegression` (synthetic `Decide()` ms)|UNCOVERED — no suite measures real frames|frame-time percentiles from a headless raid|
|1460|F8 daemon stopped capturing|—|`SoftlockClassifierRegression.cs:40`|PARTIAL — file-exists + literal|daemon heartbeat freshness vs the play window|
|1461|3★ clear banks 25 of 1800|P1|`RaidPayoutVisibilityRegression.cs:245`; `RaidSelectionSpoilsRegression.cs:24`|PARTIAL — end screen only; repeat×0.25 and cap excluded|quoted spoils == banked+pending against a full bank|
|1462|RaidDeployScreen no backdrop|—|`RaidSelectionLayoutRegression.cs:697` `S7:deploy-opaque-backdrop`|**COVERED; already fixed** — `RaidDeployScreen.cs:144` takes the kit backdrop|—|
|1463|Rally flag magenta|—|`ShaderPinRegression.cs:116`|PARTIAL — pins URP unlit, not `CreatePrimitive`'s default material|runtime-built renderers carry a URP material|
|1464|In-raid tray / bands overlap|—|`RaidHudThumbBandRegression.cs:92` `[buried-abilities]`|PARTIAL — MEASURES one band pair only|every raid band vs nameplate/compass/joystick; tray glyph fit|
|1465|Gear menu behind Night Market|—|NONE (`UiTouchClampRegression` runs on synthetic canvases only)|UNCOVERED|gear-vs-card sort key; PAUSE/joystick rect disjointness|
|1466|Night Market caption ellipsis|—|`HudLabelFitRegression.cs:1654` `Case11_NightMarketStandout`|PARTIAL — **ticket premise false**: it measures, but at the 20px hard floor in reference px, not the rendered rect|rendered text width vs painted plate per aspect|
|1467|Suites pin an unbound 4-face model|P1|`HudActionBarRegression.cs:239` (greps `const int count = 5;`); stale pins `SessionShapeRegression.cs:232`, `HudLabelFitRegression.cs:266`|PARTIAL — shipped dock is source lint; **the pins bind nothing**|face count/ids/order read off the built visual tree|
|1468|ITEM charge badge outside frame|—|NONE|UNCOVERED|badge rect containment in the bar frame|
|1469|`distribute-android` no R2 gate|—|NONE (no suite reads that file)|UNCOVERED|every device path calls `tools/r2-ship.ps1`|
|1470|Ship chains accept stale parity log|—|`RegressionMarkerRegression.cs:483` RULE 3|PARTIAL — marker literal only|proof mtime postdates `ServerData/`|
|1471|World clock frozen on device|—|`WorldHoldLivenessRegression.cs:181`|PARTIAL — game-over hold owner only|every hold owner releases; overworld included|
|1472|25 structures no cosmetic category|—|`CosmeticApplyRegression.cs:432` rule 6|PARTIAL — one category reaches one renderer|every catalog structure binds a category|
|1473|Arcane aura loops never release|—|`VfxLoopFlagRegression.cs:147`|PARTIAL — loop-vs-burst classification, no release policy|loop slot age/occupancy ceiling|
|1474|Echo split ignores authored rate|—|`EchoSpecializationRegression.cs:611`|PARTIAL — asserts self-consistency only; **fix landed**, `EchoBonusCalculator.cs:182` wires `BaseRateFor`|output changes when `echoes-balance.json` changes|
|1475|`GrantSpendable` remainder discarded|SPEC|`CollectorIncomeRegression.cs:1032`|PARTIAL — collector path only|no call site discards the returned basket|
|1476|VFX rising over town|—|NONE (emitter unidentified)|UNCOVERED|identify from a capture|
|1477|Rumor board PREVIOUS button|—|`RumorBoardLayoutRegression.cs:125` `[previous]`|**COVERED; already satisfied** — `RumorBoardPanel.cs:425` has `PrevPage`|—|
|1478|Fabricated cost basket in capture|—|NONE (`UICaptureLaunch` named only by `CaptureProvenanceRegression.cs:29`)|UNCOVERED|captured cost strings derive from the catalog|
|1479|CANCEL never quotes its refund|—|NONE (no suite asserts refund copy; `BuildEconomyRegression.cs:521` is the *sell* path)|UNCOVERED|face text equals the composed `refunded`, fitted to the row|
|1480|`WallSegment.SetTier` clamps 1..3|—|NONE (`SetTier` in zero suites)|UNCOVERED|no literal level ceiling under `Village/Buildings` (lint is honest here)|
|1481|CLAUDE.md §8 rotted|—|NONE|UNCOVERED|doc claims vs the constants they name|
|1482|Canon says branch pushed|—|NONE|UNCOVERED|ahead-count and single-anchor invariant|
|1483|Empty overworld 22 fps|—|NONE|UNCOVERED|frame-time in an empty scene|
|1484|Heap +66 MB in 4 min|—|NONE|UNCOVERED|heap delta over a fixed dwell|
|1485|APK texture pass duplicated|—|NONE|UNCOVERED|duplicate/unshipped texture census|
|1486|`ServerData/Android` never pruned|—|`AndroidContentTargetRegression.cs:147` (file-exists backstop)|PARTIAL|orphan bundles vs the current catalog|
|1487|20 of 24 build tiles no portrait|—|`ManagePortraitCoverageRegression.cs:338` `[building-tier-portrait]` + `:80` `[exemption-still-accurate]`|**COVERED** — resolves real sprite keys; the suite IS the art checklist|—|
|1488|Queue drawer row overlap|—|`ManageQueueDrawerRegression.cs:255` `[drawer-clear-of-card]`|PARTIAL — band arithmetic for the Troops drawer only, header-plus-one-row|row rects ⊂ plate at N rows; timer fit; X clear of the tab strip|
|1489|Capture plan blind to 4 of 9|—|NONE (`UiCaptureCoverageRegression` pins filenames, not the nine mockup screens)|UNCOVERED|declared plan == nine panels; a planned frame missing is a FAIL, not a log line|
|1490|Research grid 5 cards, dead band|—|NONE (`ManageResearchCardRegression` cases are model/copy only)|UNCOVERED|cards-per-row from the plate rect; dead-band fraction|
|1491|Manage copy artifacts, dead field|—|NONE (`CopyHygieneRegression.cs:13-21` = a fixed six-file retired-phrase list)|UNCOVERED|copy artifacts + every contract field has a reader|
|1492|13 ManageRedesign WOs no Status|—|NONE (no suite reads `WorkOrders/`)|UNCOVERED|every WO parses to a board row|
|1493|SessionRegression never run, 6/6 label|P0|`RegressionMarkerRegression.cs:971` (names it, does not assert it)|**LANDED, UNCOMMITTED** — `SessionRegression.cs:117` now derives; `checkin_gate.ps1:312` runs stage 5|gate the derived count like RULE 6|
|1494|Six suites claim MEASURE, are lint|P0|`RegressionMarkerRegression.cs:85` RULE 4|UNCOVERED — RULE 4 finds can't-go-red, not lint-claiming-measure|each suite's claim vs whether it executes the subject|
|1495|13 allowlists undated|P1|`RegressionMarkerRegression.cs:168-174` opt-out tokens — **themselves undated**|UNCOVERED|every exemption carries a WO + date + remove-by|
|1496|Nine suite files unregistered|P2|`RegressionMarkerRegression.cs:73` RULE 2 `[registration]`|PARTIAL — the self-declared opt-out is the escape hatch|registration coverage with no self-granted exemption|
|1497|MOVE door under a collided number|—|NONE|UNCOVERED|WO number uniqueness on disk|

## Counts by class

| Class | n |
|---|---|
| COVERED | **4** (1449 ungated · 1477 and 1462 apparently already satisfied · 1487) |
| LANDED-UNCOMMITTED | **1** (1493) |
| PARTIAL | **17** |
| UNCOVERED | **32** |
| **Total rows** | **54** |

Structural finding: only `RaidHudThumbBandRegression`, `HudDockLayoutRegression`,
`NightMarketRuntimeLayoutRegression` and `RaidSelectionLayoutRegression` read or compute real rects —
every other Manage/HUD oracle is source lint, which is why five UI tickets are uncovered for one reason.

Two node cases **pin the defect their ticket asks to fix** and must be rewritten, not supplemented:
`auth.session.renewal-cap.test.js:81` (WO-1452) and `auth.rawbody.session.test.js:55` (WO-1453).

## New suites to write, most severe first

1. **Wire `node --test` into the packs/check-in gate** — no gate runs the 30 node suites (1449, 1446, 1452, 1453, 1456, 1457). Nothing below matters until this exists.
2. `test/auth.session.absolute-cap.test.js` + rewrite case :81 — MEASURED (1452).
3. `test/promo.signature-rail.test.js` — signed payload → 200; rewrite case :55 — MEASURED (1453).
4. `test/api.column-migration-parity.test.js` — live column shape vs handler writes — MEASURED (1446).
5. `test/save.schema-version-monotonic.test.js` — captured upsert params — MEASURED (1457).
6. `test/auth.rate-limit.test.js` — success **and** refusal paths — MEASURED (1456).
7. `CloudLoadRestoresWholeStateRegression` + `BackendLoadRecencyRegression` — MEASURED (1447, 1448).
8. `SessionRenewalStatusClassRegression` — keep-vs-clear per status class — MEASURED (1454).
9. `ShipPathR2GateRegression` — every device path calls `r2-ship.ps1`; proof postdates bytes (1469, 1470).
10. `SuiteHonestyRatchet` — a suite claiming MEASURE must execute its subject; allowlists dated with a remove-by; registration with no self-granted opt-out (1494, 1495, 1496).
11. `DeviceFrameFloorRegression` + heap-delta case — MEASURED (1459, 1483, 1484).
12. `HudDrawOrderAndRectRegression` — sort keys + pairwise rects across raid and HUD bands (1464, 1465, 1468, 1488, 1490).
12b. `ManageCapturePlanRegression` — planned screens vs the ledger; a missing frame FAILs (1489).

13. `OfflineSyncQueueBoundRegression` — MEASURED (1455).
14. `SpoilsAreBankableRegression` — quoted == banked+pending (1461, 1445, 1475: one law, three producers).
15. Lint is honest for 1480 and 1474 — write them as lint and say so.
