# WORK ORDER 945 — Tutorial: the SECOND tower runs the full 90s curve while the teaching wave lands

**Status:** DONE — implemented + `[grace]` regression green 2026-08-10; owner felt-verify pending
(rerun the tutorial: both towers 5s). See the RESULT file.
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 945 → 947 in the same edit, together with WO-946)
**Silo:** Village/BuildMode + Buildings (BuildTimerService callers) — disjoint from Wallet (WO-931 lane) and Hero/Arena (death-pin lane)
**Type:** EXISTING defect — an owner ruling's intent betrayed by its own scoping
**Origin:** owner felt-report 2026-08-10 (seen MULTIPLE times, Seeker AND desktop exe): *"It didn't do the
five second build. Its build timer was set for a minute and a half. So by the time I exited and found
them and got over there, it had almost destroyed my tower. So we need to work on the timing."*

---

## 1. The defect in one line

The tutorial asks for TWO towers of the SAME structure id; the first-build 5s grace is scoped
per-structure-id, so tower #2 runs the real ~90s tier curve — and the tutorial's scripted wave then
attacks the still-under-construction (damageable) tower.

## 2. Proving lines (captured, Player.log session 2026-08-10 — §12 satisfied)

| Line | Evidence |
|---|---|
| 31181 | `[Flow:BuildTimer] FIRST-BUILD grace on 'pet-house@13_19': 30s -> 5s (tier 0 curve bypassed).` |
| 51090 | `[Flow:BuildTimer] FIRST-BUILD grace on 'tower_ground_archer@16_23': 90s -> 5s (tier 1 curve bypassed).` — tower #1 |
| 55450 | `[Flow:BuildTimer] build 'tower_ground_archer@14_7' tier=1 (basket 150) -> 90s` — tower #2, NO grace line |
| 55676 | `[BuildMode] Placed 'tower_ground_archer' at cell (14,7) ... charged nothing (first-build FREE).` — the COST freebie DID fire for #2; cost-freebie and timer-grace use different predicates |
| 49011 | `[Flow:Tutorial] step 'founding_defend' scripted town wave: SpawnAt(spawn-0, 3) via TutorialWaveSpawner` — the teaching wave that catches the unfinished tower |

## 3. Where the code stands (verified at source 2026-08-10)

- `Assets/_Modules/Village/Buildings/BuildTimerService.cs:409-430` — `StartBuilderJob`'s grace block.
  The OWNER RULING 2026-08-06 comment states the intent verbatim: *"so onboarding never stalls on a
  timer"* — but the gate is `firstEverBuild` (first-ever build of THIS id), which the CALLER computes
  (`structureId` here is the job key `UnderConstructionVisual.KeyFor(data)`, not the catalog id).
- The pallets carve-out (lumberyard / foundry / silo are EXCLUDED from grace) is the caller's decision
  and MUST survive this WO untouched.
- The grace only ever SHORTENS (`graceMs < durationMs` check) — keep that invariant.

## 4. The fix (recommended; owner's standing autonomy ruling 2026-08-10 applies)

**(a) Timer side — make the ruling's intent literal:** while the player is NOT Onboarded (the tutorial
gate — same `Onboarded` flag that closes the wave loop), EVERY build qualifies for the
`firstBuildSeconds` grace, not just the first-per-id. Implement at the CALLER that computes
`firstEverBuild` (it owns the catalog id and the pallets carve-out); keep the carve-out. Trace the
new branch: `FlowTrace.Step("BuildTimer", "ONBOARDING grace on '<id>' ...")` so tutorial-time grace is
distinguishable from first-build grace in a capture.

**(b) Wave side (flow ordering):** the `founding_defend` scripted wave must not be armed by tower
PLACEMENT while construction is still running — gate the step's completion signal on both towers
COMPLETED (or grace-completed, which with (a) is ≤5s, making this nearly moot but honest). If the
step-signal change is disproportionate, (a) alone resolves the felt defect (5s ≪ walk time); say so
in the RESULT rather than forcing (b). Coordinate with WO-1012 P3 (the 8-beat arc re-authors these
steps) — do NOT fork the step schema.

## 5. Acceptance criteria

1. Headless/EditMode: with Onboarded=false, a SECOND build of an already-built structure id starts
   with duration == firstBuildSeconds (5s); with Onboarded=true, the same second build runs the tier
   curve unchanged. Pallets carve-out asserted unchanged in both states.
2. The grace-only-shortens invariant holds (a tier curve under 5s is not lengthened).
3. Proving trace present: the new `ONBOARDING grace` FlowTrace line fires in a headless tutorial run.
4. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` green; new/extended regression case registered
   in the BuildTimer/BuildEconomy suite following its existing pattern.
5. Owner felt-verify (PO closes): tutorial run — both towers stand before the wave reaches them.

## 6. What NOT to touch

- The 2026-08-06 owner ruling's carve-out (pallets excluded) and the first-build COST freebie
  (`freeBuildsUsed`, v32) — cost and timer stay separate concerns.
- `BuildTimerConfig.queueDepthPerLine` / slots (WO-911 rulings).
- `tutorial-steps.json` beyond the founding_defend completion signal if (b) is taken.
- The `!Onboarded` wave-loop gate itself (memory: enemies-never-spawn-tutorial-onboarded-gate).
