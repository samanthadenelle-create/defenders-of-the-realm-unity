# Fleet Triage -- 2026-07-12 (AutoPilot 4-bot run)

Source: `Builds/autopilot-tickets.md` (generated 2026-07-12T21:46:16Z; 4/4 runs, seeds 1-4).
Anchor: `CANON_GROUND_TRUTH_2026-07-12.md`. Regression cross-ref: `WORK_ORDER_684` sec.R.

Headline: **10 distinct tickets -- 8 KNOWN pre-existers, 2 NEW (both flaky 1/4).**
Zero regressions in the always-run core lane (BootToGameplay -> save round-trip -> vendor ->
build-move -> HUD panels -> combat invariants all PASS). All fails are on the world-egress /
encounter-return strand and Title-scene panel duplication -- the standing open lanes.

## Confirmed (reproduced in >=2 runs)

| # | Captured line (RCA) | Verdict | Maps to |
|---|---|---|---|
| 1 | `HOME_RETURN_FAIL :: gate=<none>` -- NO outer return entrance exists (no SceneTransitionTrigger targets the hub). Way back home is unwired. (x4) | KNOWN | **WO-602** way_back_home |
| 2 | `AssertEncounterRealPath: hero NOT returned (dist 7088.1m from engagement spot)` -- battle resolves, but the return-teleport lands the hero ~7km off. (x4) | KNOWN | **WO-453** outerworld_gated_regions (encounter strand ~7km) |
| 3 | `AttemptExitCastle: hero could not path to 'CavePortal_Trigger' -- closest 403.0m of radius 16.0m (navmesh edge / blocked)` (x3) | KNOWN | **CavePortal seam unreachable** -- no-seams-ever, port-around (WO-608); needs egress-portal WO if not already open |

## Below threshold (1/4 -- flaky, verify; a deterministic 1/N is still real)

| # | Captured line (RCA) | Verdict | Maps to |
|---|---|---|---|
| 4 | `duplicate UIDocument: 2 ENABLED docs share PanelSettings 'OnboardingPanelSettings' in 'Title' -- docs=[JupiterSwapHost,SplashLoading]` (raycast-fight / eat input) | KNOWN | **duplicate OnboardingPanelSettings UIDocument** (Title) -- existing ticket |
| 5 | `AssertScatterRecords: FAIL at link 4 -- hero warped 154m (> cull radius 115m) but no scatter CULL fired within 2 maintain ticks (cull pass in MaintainScatter dead or tick gated)` | **NEW** | needs WO -- MaintainScatter cull miss on warp; ties to WO-684 sec.R coverage gap (no PlayMode scatter-maintain oracle). Confirm vs "MaintainLoop gated". |
| 6 | `potential z-fight: DistantGround Y=-0.200 vs DistantGround Y=-0.200 (coplanar, dY 0.000 < 0.1m)` | KNOWN | **arena ground / backdrop z-fight** cluster (anchor: F8-37 arena audit, Backdrop_Cap soft suspect) |
| 7 | `potential z-fight: Ground Y=0.000 vs Ground Y=0.000` | KNOWN | same arena-ground z-fight cluster as #6 |
| 8 | `potential z-fight: Backdrop_Cap Y=90.200 vs Backdrop_Cap Y=90.200` | KNOWN | same cluster (Backdrop_Cap named in anchor perf note) |
| 9 | `AssertEncounterRealPath: battle did NOT resolve within 15s after the family died -- loop stuck` | **NEW** | needs WO -- encounter resolve-loop stall (distinct from #2's return-distance fail); same encounter system as WO-453, different failure mode. Watch for repro. |
| 10 | `duplicate UIDocument: 2 ENABLED docs share PanelSettings 'DevRuntimePanelSettings' in 'Title' -- docs=[[DEV] QA Dev Console,JupiterSwapHost]` | KNOWN (variant) | same root class as #4 (Title-scene PanelSettings sharing); dev-console panel, lower priority |

## WO-683 build-screen D-pad -- probe status

`WORK_ORDER_683_build_screen_dpad.RESULT.md` = **IMPLEMENTED + GATED**; probe-read fix
(`BuildModeController` reads `HudMoveInput.Move`, `ElarionUiKit.BuildVirtualDPad`) is IN THE TREE.
`AssertBuildMoveChain` passed here (line 29: tower moved 13,16 -> 15,16) but that exercises the
baseline click-move, not necessarily the new DPAD-vector link. The DPAD-specific fleet probe
(arm -> publish HudMoveInput up/down -> assert ghost cell changed) was **in flight at RESULT time
(4 bots, seeds 8200, exe 2026-07-12 evening) -- DPAD probe verdict + owner felt-pass on a phone
PENDING.** Re-verify against the next fleet run before closing WO-683.

## Regression cross-ref (WO-684 sec.R)

The DataRegression suite (SFX_WEBGL / CORESAVE / BUILDECON / DATAWEB / HUDUI) is baselining
7 truthful failures and is orthogonal to this fleet run. Coverage gaps that would have CAUGHT
the NEW items above: sec.R lists **no PlayMode scatter-maintain oracle** (ticket #5) and **no
Combat/ATB SME suite** (ticket #9's encounter resolve-loop) -- both are named WO candidates.
Route #5 and #9 into that PlayMode/Combat coverage lane rather than one-off fixes.
