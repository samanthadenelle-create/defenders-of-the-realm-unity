# WORK ORDER 1353 - The world clock gets ONE owner, and every step into slow pairs with a step out

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T17:22:20, build 2026.09.04.354315). PRIOR STATUS: FIXED 2026-09-03 - ON HER DEVICE (`2026.09.03` build, installed, R2_PUSH_OK + R2_PARITY_OK 266 objects). `Time.timeScale` now has ONE owner - `WorldHold`, EXTENDED not replaced, because a rival `WorldClock` would have been a SECOND owner - with paired acquire/release and a SLOWEST-WINS rule, so zero live holds provably means 1.00 (minimum is monotone; releasing any hold can only move the world toward full speed). Today's 0.28 named: `WaveCelebrationManager._slowMoScale`, the only 0.28 in the tree. But the VALUE was never the bug - five per-class owners each CORRECTLY declined to stamp a clock that did not read their value, so an overlap left the residue with everyone having honourably walked away. Retreat DID leak; its unwind is now unconditional. Failsafes: per-hold ceilings on the UNSCALED clock, a scene-load release (LoadScene does not reset an engine global), and a drift watchdog armed AT BOOT - the measured state had zero holds, so a lazily-installed watchdog would not have been running. `BattleQuiescenceGate` stayed an OBSERVER. Game feel diffed field-by-field against HEAD and IDENTICAL. Gates COMPILE_GATE_OK + REGRESSION_OK 358/358. AWAITING HER FELT-VERIFY that town runs at full speed - then Owner Validation closes it.
**Silo / Lane:** Core / world clock ownership + instrumentation
**Type:** EXISTING engine global, currently written by many and owned by nobody
**Minted:** 2026-09-03 (CLI) on a direct owner ruling, from a live felt-test.
**Severity:** P0 - the game was measured running at 28% speed in open town, on the build she is
testing. Every timer, every animation, every cooldown and the wave clock are all wrong together, and
nothing on screen says so.

## THE CAPTURED DATA (this is the ticket's foundation)

```
[Flow:HeroOwner] scene='Main_Castle_Overworld' owner=HeroLocomotion ownerCC=none
  ownerAgent=on-mesh scriptedMove=off velSelf=0.00 velRoot=0.00 animFeed=velSelf
  animSpeed=0.00 rootYaw=328.5 timeScale=0.28 dt=0.0046 inputSuppressed=False
  autoWalk=False pos=(17.84, 0.08, 27.08)
```

**`timeScale=0.28`** - 28% speed. No battle, no modal, `inputSuppressed=False`. The hero reads
`velSelf=0.00` because the clock is nearly stopped, which is why it also presented as "frozen in
place". `dt=0.0046` confirms it: a 4.6 ms delta where ~16 ms is expected.

⚠ **`fps=40 ms=24.9` in the same window is a RED HERRING.** The CLI read that number first, built a
frame-budget explanation around it, and asserted it twice before checking the clock. The owner caught
it: *"Why did you guess"*. **Read the captured trace before forming any theory** (CLAUDE.md s12;
memory `never-inference-fix`). `[Flow:HeroOwner]` prints `timeScale=` on every line.

## THE OWNER'S RULING, verbatim

> *"I want a guard on all time changes"*
> *"every battle death victory"*
> *"anything that steps into time slow needs to step to time return"*

That is a **paired step-in / step-out contract**, and it is the same shape as `FlowTrace.Enter/Step`
which this project already runs on. A slow that is entered and never left is the defect class; the
guard must make leaving *structural*, not a thing a future author remembers.

## WHAT TO BUILD

### 1. ONE owner for `Time.timeScale`

A single service is the ONLY thing in the project permitted to write `Time.timeScale`. Everything else
**acquires a scoped hold** and releases it. Suggested shape - argue for a better one if you have it:

- `WorldClock.Hold(reason, scale)` returns a token; disposing/releasing it restores the clock.
- The effective scale is derived from the live holds (slowest wins, or last-wins - **choose and state
  why**), so two overlapping holds cannot fight and leave a residue.
- **Zero live holds ⇒ the clock is 1.0, always.** That invariant is the whole ticket.

⛔ **Do NOT add a second writer.** One owner, one lifecycle - the rule that governs every presence in
this codebase (CLAUDE.md s7).

### 2. A failsafe, because a paired contract still breaks

A hold must not be able to outlive its owner:
- **Every hold carries a maximum duration** measured on the UNSCALED clock (`Time.unscaledTime`), after
  which it self-releases and emits `FlowTrace.Fail` naming the reason and how long it overran. A hit
  stop is milliseconds; nothing legitimate holds for a minute.
- **A scene load releases every hold.** ⚠ `Time.timeScale` is an ENGINE GLOBAL and
  `SceneManager.LoadScene` does NOT reset it - `BattleQuiescenceGate.cs:21` already says so in prose,
  learned the hard way.
- **A watchdog** ticks on the unscaled clock: if the scale is not 1.0 and there are no live holds,
  restore it, and `FlowTrace.Fail` with the last hold that released and when. That is exactly the
  state measured today.

### 3. Cover the moments she named, and prove each one

> *"every battle death victory"*

Trace and test the FULL round trip for each:
- **battle** - entering a battle, and leaving by every exit: win, loss, **retreat**, and a scene change
  mid-battle. ⚠ Retreat is the case that has already bitten: it destroys survivors via `Destroy()`
  rather than `Die()`, which is how WO-1337's pursuit pulse leaked yesterday. Assume it leaks here too
  until proven otherwise.
- **death** - the death freeze and its release. `DeathTrace.TimeScaleFroze` / `TimeScaleRestored`
  already exist as a step-in/step-out pair - **fold them into the one owner rather than leaving a
  parallel mechanism**, and keep their reporting.
- **victory** - the reward/celebration beat and its release, including the player dismissing it early,
  backgrounding the app mid-beat, and a reward screen that never appears.

### 4. Make every existing writer route through the owner

Audit and convert. Known writers to start from (**re-grep, do not trust this list**):
- `Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs:521` - F8 freezes to 0 and restores at `:542`.
  ⚠ It already saves/restores, so it is a *correct* pair; converting it must not break F8.
- `Assets/_Modules/Core/Combat/BattleQuiescenceGate.cs:432` - restores to 1 and reports the leak.
  ⭐ **That gate is an OBSERVER by explicit design** (its header says it "OBSERVES and REPORTS ... a
  gate that quietly fixes things trains" the wrong habit). **Keep that character.** It should now
  observe the new owner and fail loudly when the invariant breaks - do not turn it into the fixer.
- `Assets/VfxParade/Runtime/VfxParadeRuntime.cs:174` - a dev curation tool that freezes to 0.
- `Assets/Mirza Beig/...DemoManager.cs:206`, `Assets/UnityTechnologies/...AdjustTimeScale.cs:50` -
  **VENDOR pack demo scripts. Do NOT convert vendor code**; exclude their directories from the lint and
  say so, or the lint becomes noise everyone learns to ignore.
- ⚠ Regression suites set `Time.timeScale` deliberately (`BattleQuiescenceRegression`,
  `TransactionWorldHoldRegression`). **Editor/test code is exempt** - but the exemption must be
  explicit in the lint, not accidental.

⭐ **`PauseController` DOES NOT write `Time.timeScale`** - WO-1149 moved the freeze elsewhere and the
only "timeScale" left in that file is prose. Do not "fix" it back.

### 5. Find TODAY's 0.28 and name it

The measured 0.28 is not 0 (a pause) and not 0.04 (the hit stop this repo uses). **A value nobody
authored suggests something is scaling the clock rather than setting it, or a lerp/ramp that never
finished.** Find the writer with data - add the instrumentation first if the current traces cannot name
it. ⛔ Do not fix a suspected cause you have not proven; that is how this ticket's own diagnosis went
wrong twice today.

## Instrumentation (mandatory, and it is half the deliverable)

Every acquire and release emits a `FlowTrace` line: the reason, the requested scale, the resulting
effective scale, the number of live holds, and - on release - how long it was held on the unscaled
clock. **A future occurrence must name its own culprit in one line**, because today's cost was entirely
that nothing said who slowed the clock. ⛔ Never strip FlowTrace (CLAUDE.md s12).

## Constraints

- ⛔ **Do not change game feel.** The hit stop, the death freeze and any celebration beat keep their
  current durations and scales; this ticket changes OWNERSHIP and GUARANTEES, not tuning. If a value
  looks wrong, report it - do not retune it.
- ⚠ **Anything driven by `Time.deltaTime` inherits the slow**, so a UI countdown or a network timeout
  that must run in real time should use the unscaled clock. Report any you find relying on the scaled
  clock where they should not; fix only the ones inside this ticket's seam.
- ⛔ Never hand-edit a `.unity` scene. UXML does not work in builds. ASCII-only in player-facing strings.
- Do not run a Unity gate, do not commit, do not build. The lead does all three.

## ⛔ LIVE LANES - stay out

`PackStore.cs` + `ImpulsePackRegression.cs` (gap-pack rail, landed uncommitted); `PlayerDeckWorkspace.cs`,
`HudLabelFitRegression.cs`, `ElarionUiKit.cs`, `HeroSkillTreePanelMvvm.cs`, `HeroInventoryController.cs`;
`GroundZFightFixer.cs`, `HubSceneLiteralRegression.cs`; `RepairAvailabilityProbe.cs`,
`WallRepairController.cs`, `RepairHudContractRegression.cs`; `StructureDamageVisuals.cs`,
`StructureBurnRegression.cs`; `Assets/HeroContent`; `tutorial-steps.json`; board tooling;
`tools/*ship*.ps1`; `VfxManualPicks.json` (read only); `DataRegression.cs` (**the lead registers suites**).

⚠ `BattleQuiescenceGate.cs`, `BattleArena.cs`, `Enemy.cs` and `PanelManager.cs` were WO-1337's files and
are now committed - you MAY edit them, but read the WO-1337 changes first so you do not undo them.

## Oracle

Pin the invariant, not an instance:
1. **No `Time.timeScale =` outside the one owner** (excluding editor/test and vendor dirs, explicitly).
2. **Zero live holds ⇒ scale is 1.0.**
3. **Every hold path releases** on all its exits - and specifically for battle **win, loss, retreat and
   scene change**, for death, and for victory.
4. **A hold that overruns its maximum self-releases and reports.**

**Prove it RED first and report the mutation** - the mutation is easy and honest here: today's HEAD,
where a hold leaks and leaves 0.28. Extend an existing suite (`BattleQuiescenceRegression` is the
natural home and already owns this concern); ⛔ do not create a new suite and do not touch
`DataRegression.cs`.

## Acceptance

- [ ] Exactly ONE writer of `Time.timeScale` in shipping code, enforced by the lint.
- [ ] Zero live holds ⇒ 1.0, proven; a scene load clears all holds, proven.
- [ ] Battle (win / loss / **retreat** / scene change), death and victory each acquire and release,
      each proven separately.
- [ ] A hold that overruns self-releases and emits a `FlowTrace.Fail` naming it.
- [ ] TODAY's 0.28 traced to its writer and NAMED, with the log line that proves it.
- [ ] `BattleQuiescenceGate` still OBSERVES and REPORTS - it did not become the fixer.
- [ ] Game feel unchanged: hit-stop and death-freeze durations and scales are identical. Say so.
- [ ] Oracle proven RED against HEAD; mutation reported.
- [ ] Brace + NUL check per `.cs` file.
- [ ] ⛔ Owner felt-verifies that town runs at full speed and CLOSES.
