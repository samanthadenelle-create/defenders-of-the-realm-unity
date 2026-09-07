# WO-1540: the blank-start census sees a baked CastleBarracks because ff.barracks is ON in batchmode

**Status:** READY TO IMPLEMENT - P2
**Silo:** Editor/regression environment - `BlankStartCensusRegression` + `FeatureFlags` + possibly
`HubStructureVisualInjector`.
**Source:** wave-two regression `Builds/reg-wave2.log` (422/435), 2026-09-06. Surfaced by
`BlankStartCensusRegression`, **registered tonight by WO-1496**. Minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1540 -> 1541 in the same edit).

## 1. EVIDENCE

```
BLANK START: 1 failure(s):
  EXTRA structure: baked 'CastleBarracks' visible - ff.barracks is ON in this environment
  (default OFF; spawner: scene bake, hidden by HubStructureVisualInjector.TrySwap)
```

The flag's default is OFF. It is ON inside the batchmode run, which means a PlayerPrefs value is bleeding
between suites - one suite sets `ff.barracks` and never restores it, and every later suite runs in an
environment nobody authored.

That is worse than the one failure it produced: any suite after the setter is testing a configuration that
does not match a player's, and the ORDER suites run in silently changes results. This one surfaced because a
census counts everything; the others would just quietly pass or fail wrong.

## 2. FIX SHAPE

Decide from the flag's authority in `FeatureFlags.cs`, then take ONE of:

- **The suite pins flags to their defaults before the census** (and restores after), which fixes this suite;
  or
- **`HubStructureVisualInjector` honours the flag at bake**, which fixes the bake path.

Then, regardless of which: **make flag bleed impossible, not just handled here.** A shared
set-up/tear-down that snapshots and restores PlayerPrefs flags around every suite is the durable fix; one
suite pinning its own flags leaves the next one exposed.

## 3. WHAT NOT TO DO
- Do not set `ff.barracks` OFF at the top of this one suite and call it done. The bleed is the defect; this
  census is only the detector.
- Do not change the flag's default to match the batchmode environment.

## 4. ACCEPTANCE
- [ ] The RESULT names WHICH suite leaves `ff.barracks` ON, with the file:line that sets it.
- [ ] Flags snapshot/restore around suites, so results do not depend on run order.
- [ ] `BlankStartCensusRegression` reports zero failures, run both alone AND after the setter suite.
- [ ] `REGRESSION_OK n/n` on a fresh log.
