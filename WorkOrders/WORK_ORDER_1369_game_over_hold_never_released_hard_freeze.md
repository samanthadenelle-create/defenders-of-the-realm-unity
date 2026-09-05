# WORK ORDER 1369 - P0 HARD FREEZE: the 'game-over' WorldHold is never released, and nothing can rescue it

**Status:** FIXED at HEAD (9779b9639: dead-owner probe; WorldHoldLivenessRegression [dead-owner] + GameOverScreenLifecycleRegression green, 377/377) - RED PROVEN from the owner's 09-04 09:37 dungeon death (freeze-20260904-095249.log L986991 ACQUIRE 'game-over', L987150 the view carrying OnRetry destroyed 11 ms later, zero RELEASE lines, 2m08s at timeScale 0, OS kill 09:40:33). NOT DONE: no post-fix device capture - owner felt-test = die in a dungeon on build 355872+ and the trace must show 'LAST hold gone, timeScale 1.00'. Two unproven items in the RCA section: the 'proven RED first' claim for the oracles has no surviving log.
**Silo / Lane:** Core/UI `WorldHold` + `Village/UI/EndState` - the world clock
**Type:** EXISTING system, **REGRESSION introduced by WO-1360 (2026-09-03, yesterday)**
**Minted:** 2026-09-04 (CLI), root-caused live from her device
**Severity:** ⛔ **P0. THE GAME FREEZES AND THE ONLY EXIT IS FORCE-KILLING THE APP.** On the
**2026.09.04.354315 PRODUCTION CANDIDATE** - the build on her Seeker and the one the store submission
is about.

## THE REPORT

Owner, mid-playtest: ***"everything completely froze. I had to kill app to exit"***.

## THE PROVING LINES - every link measured, none inferred

Source: `logs/device/freeze-20260904-095249.log` (1,230,143 lines, pulled before the buffer rolled).

```
09:38:25.063  [Flow:Pause] WorldHold ACQUIRE 'game-over' @ 0.00 -> effective timeScale 0.00
              (slowest wins), 2 holds outstanding
09:38:25.063  [Flow:Pause] WorldHold 'game-over' is PLAYER-OWNED: the player decides when it ends,
              so NO watchdog ceiling applies and it will never be force-released for being old.
09:38:26.284  RELEASE 'fx:death-slowmo' -> STILL HELD, 1 hold(s) remain [game-over],
              effective timeScale 0.00
09:40:23.383  RELEASE 'combat-item-picker' -> STILL HELD, 1 hold(s) remain [game-over],
              effective timeScale 0.00
09:40:32.679  PAUSE MENU -> WorldHold taken. Outstanding: [game-over, pause-menu]
09:40:33.252  ActivityManager: Killing 28972:com.denellestudios.echoesofelarion (adj 905): remove task
              (preceded by "Destroy timeout of remove-task" - the OS force-killing an app that did
               not respond to destroy)
```

⛔ **`grep -c "WorldHold RELEASE 'game-over'"` over the whole buffer returns `0`.**

**Acquired 09:38:25.063. Never released. `effective timeScale 0.00` for 2 minutes 7 seconds, until
the OS killed it.** The world clock was pinned at zero - that is the freeze. Not a hang, not OOM, not
a deadlock: the game was running and rendering, with time stopped.

## WHY THE OWNER THAT SHOULD RELEASE IT IS GONE

Same second, `EndStateView` logs its own failure:

```
09:38:25.071  HeroDeath shown: spoils=0 action=retry        (panel=451px)
09:38:25.074  'YOU HAVE FALLEN' destroyed WITHOUT firing its primary action - EndStateView.Show
              - REPLACED by a new end-state 'YOU HAVE FALLEN'. That action is now abandoned.
09:38:25.080  HeroDeath shown: spoils=0 action=respawn      (panel=370px)
09:38:25.081  'YOU HAVE FALLEN' destroyed WITHOUT firing its primary action - CloseFromArbiter
              (another modal opened over this end-state). That action is now abandoned.
09:38:26.511  HeroDeath primary fired: action=respawn
```

**TWO HeroDeath end-states are raised 9ms apart for ONE death** - `retry`, then `respawn` - and the
second closes the first through `PanelManager`'s single-modal arbiter. The hold was taken by a panel
that was then destroyed without firing.

⚠ Note `HeroDeath primary fired: action=respawn` DID appear at 09:38:26.511, so *a* primary fired -
**and the hold still was not released.** So this is not simply "the primary never ran"; the release
is not reliably coupled to it. **Establish that coupling from source before fixing.**

## ⛔ AND THE SAFETY NET WAS REMOVED YESTERDAY. THIS IS THE HARD PART.

`game-over` is one of the **seven holds WO-1360 converted to `PlayerOwned`** on 2026-09-03
(`3e6ae4274`), listed in its own table: *"`Village/Heart/GameOverScreen.cs:264` | `game-over` |
**PLAYER-OWNED** (ends on Retry) | **converted**"*.

`PlayerOwned` means **no ceiling** - the trace says so in the same breath as the acquire:
*"NO watchdog ceiling applies and it will never be force-released for being old."*

**Before WO-1360, the 180s `StuckHoldSeconds` watchdog would have force-released this hold and
thawed the world.** After WO-1360, an orphaned `game-over` hold is permanent **by design**.

⚠ **WO-1360's own commit body predicted this class and closed it for exactly one victim:**
> *"Removing the ceiling exposed the hole it had been papering over - PauseController disposed on
> OnDestroy but not OnDisable, and a disabled component never gets OnDestroy nor can process Resume -
> now closed."*

**The same audit was not done for the other six conversions.** `game-over` is the second victim, found
one day later, by the owner, on the production candidate.

⛔ **DO NOT "FIX" THIS BY REVERTING `game-over` TO A CEILING.** A ceiling would have masked it at 180s
- the player would still lose three minutes to a frozen world, and the orphaned hold would still be a
defect. The ceiling was removed for a good reason (WO-1353's regression force-released a legitimate
8-minute pause). **Fix the ownership, not the ceiling.**

## ⭐ RCA COMPLETE 2026-09-04 - ROOT CAUSE FOUND, PLUS A BIGGER ONE UNDERNEATH

### The scene is the whole story: `dg_folks_granary`, a composed DUNGEON

```
09:38:25.058 [Flow:Death] lethal hit ... scene='dg_folks_granary' ...
             OnDeath listeners=[GameOverScreen.ShowHeroFell, HeroDeathEndState.OnHeroDeath]
09:38:29.070 W [Flow:DeathTrace] TIMESCALE STILL 0 after 4.0s - GameOverScreen.Show froze time
             and it has NOT been restored within the death flow
```

`GameOverScreen` fired inside a dungeon it has no business owning.

### 1. WHY: it never UNSUBSCRIBES

`GameOverScreen.OnSceneLoaded` (`:131-132`) does `_heart = null; _hero = null;` - **it nulls the
references WITHOUT `-= ShowHeroFell`**. Compare `HeroDeathEndState.Unhook()` (`:88`), which does it
correctly. The hero is `DontDestroyOnLoad` and is CARRIED into composed dungeons, so the same
`HeroHealth` crosses the boundary still carrying the stale delegate. Its two stand-downs cover arena
and overworld - **neither covers a dungeon** - and there is no `IsDefeatScene` re-check inside the
handler. Hence TWO end-states 9ms apart.

### 2. WHY THE RELEASE NEVER RAN - all three paths failed for one reason

`ReleaseWorldHold` (`:110`) is correct and idempotent. Its three callers:

| Caller | Ran? | Why not |
|---|---|---|
| `OnRetry()` `:283` | NO | the view carrying that delegate was destroyed 10ms later without firing |
| `OnSceneLoaded()` `:126` | NO | no scene could load - **the world was frozen** |
| `OnDestroy()` `:89` | NO | `GameOverScreen` is a `DontDestroyOnLoad` singleton (`:79`) - never destroyed |

⛔ **Not a coincidence: all three are downstream of "the player pressed the button", and the button
was destroyed.**

### 3. ⛔ IT IS *NOT* THE WO-1360 "DISABLED NOT DESTROYED" SHAPE. IT IS WORSE.

`PauseController` owns its hold AND the UI that releases it, so `OnDisable` was a sufficient net.
**`GameOverScreen` does not own its UI** - it hands the release to `EndStateView` as a delegate
(`:276`), and `EndStateView` is a separate destructible object any modal can kill. `GameOverScreen`
cannot detect that death: no `OnDestroy` hook on the view, no `PanelHandle`, no probe.

**So adding `OnDisable` would fix NOTHING** - the component was enabled the entire 2m07s. The defect
is *a PlayerOwned hold whose release is delegated to an object the holder does not own and cannot
outlive-detect.* ⭐ `EndStateView.cs:2124-2140` already names this class in its own comment:
*"Whoever owns a route that matters must not delegate it to a UI object other systems can destroy."*

⚠ Secondary: `_shown` (`:246`) only resets in `OnRetry`/`OnSceneLoaded`, so it is **still true** - a
subsequent death would have been swallowed too.

### 4. Why `HeroDeath primary fired` at 26.511 did not help
Different view, different VM. `FromGameOver` -> `PrimaryRoute="retry"` (destroyed, never fired);
`FromHeroDeath` -> `PrimaryRoute="respawn"` (fired). **Nothing in the respawn chain references
`GameOverScreen`.** Release is coupled to one delegate on one destroyed object, not to the primary.

### 5. THE SEVEN-HOLD AUDIT - 2 guilty, 2 partial, 3 clean

| Hold | Verdict |
|---|---|
| `pause-menu` | CLEAN (WO-1360 fixed it) |
| **`game-over`** | ⛔ **GUILTY - this ticket** |
| `wave-results` | CLEAN (holder IS the UI; `OnDestroy` is a real catch-all) |
| `combat-item-picker` | ⚠ PARTIAL - `HudKitController` has **no `OnDisable`**; a disabled HUD strands it |
| `bug-report-form` | ⚠ PARTIAL - no `OnDisable`; a component disabled without `Close()` strands it |
| **`f8-note-capture`** | ⛔ **GUILTY** - `OnDestroy` does NOT dispose the hold; the note box is `OnGUI` so a disabled harness can never commit; **no cancel/escape path**. Guarded by `UNITY_EDITOR \|\| DEVELOPMENT_BUILD` - **reachable in the dev APK she plays** |
| `vfx-parade-curation` | CLEAN (paired `OnEnable`/`OnDisable`/`OnDestroy`) |

### 6. ⛔⛔ THE FINDING THAT OUTRANKS THIS TICKET: THE PROMISED SAFETY NET IS NOT WIRED

`WorldHold.AcquirePlayerOwned`'s own XML doc (`Core/UI/WorldHold.cs:369-372`) reassures every future
author that removing the ceiling is safe:

> *"What still catches this hold: `ReleaseAllForSceneLoad` (a scene change drops every hold - quit to
> title cannot land frozen), `ForceReleaseAll` on teardown paths..."*

**`ReleaseAllForSceneLoad` HAS ZERO RUNTIME CALLERS.** `grep -rn` finds it only in
`Editor/Regression/BattleQuiescenceRegression.cs:1386,:1619` - the harness that proves it works.
`ForceReleaseAll` has two runtime callers (`PauseController.cs:357`, `DevTools/GateTraversalProof.cs:108`).

⛔ **The "scene change drops every hold" net exists only in prose and in the test that proves it
works in isolation. It is not installed.** Rows 3-5 of the audit lean on it for at least one exit
path, and that lean is unsupported. **This is the §16 duplicated-state class: a guarantee asserted in
a comment, tested in a harness, and never wired.**

### 7. THE FIX SHAPE - four changes, none restores a ceiling

- **(a) THE ORACLE, and the only one that prevents the NEXT occurrence:** make a liveness probe a
  **REQUIRED argument** of `AcquirePlayerOwned` that THROWS on null - the exact
  `OverTimeEffects.cs:302-307` shape (WO-1330), so a PlayerOwned hold *cannot be constructed without
  saying how to test whether its owner still exists*. The watchdog then polls the probe and
  force-releases with a `FlowTrace.Fail` the moment it answers false. ⭐ **This asks "does its owner
  exist", never "is this too old"** - so a legitimate 8-minute pause with a live `PauseController` is
  untouched, which is precisely the WO-1353 regression this ticket forbids re-creating.
  ⭐ **The probe type already exists and is already authored at these call sites:** `PanelHandle`'s
  `Func<bool> IsOpen` (`PanelManager.cs:42`), e.g. `() => view != null` (`EndStateView.cs:201`),
  `() => _itemPicker != null` (`HudKitController.cs:1993`). **Reuse it. Do not invent a second
  liveness concept.** This alone would have thawed the world at 09:38:25.081 - the frame the view died.
- **(b)** `GameOverScreen` stops delegating the release: bind the hold's lifetime to the object that
  can die, or register a `PanelHandle` so the arbiter can reach it. Reset `_shown` on the same path.
- **(c)** Add the missing `Unhook()` to `OnSceneLoaded` (`-= ShowHeroFell` / `-= ShowHeartFell`),
  matching `HeroDeathEndState.cs:88`, **and** re-check `IsDefeatScene` at the top of the handler - the
  delegate rides a DDOL hero across every scene boundary in the game.
- **(d)** Wire `ReleaseAllForSceneLoad` to a real `SceneManager.sceneLoaded` hook, **or delete the
  sentence from the doc.** A written guarantee with no implementation is worse than none.

⚠ **(b) and (c) each independently prevent THIS capture. Only (a) prevents the next one, and only (a)
can be asserted by an oracle** - which this ticket requires, because `REGRESSION_OK 358/358` was green
on the build that froze her twice.

## THE WORK

1. **Find why the `game-over` release does not run.** Read `Village/Heart/GameOverScreen.cs:264` and
   its release path. ⚠ The WO-1360 precedent is the shape to check first: **a component disabled
   rather than destroyed never gets `OnDestroy` and cannot process a resume.** Same question here.
2. **AUDIT ALL SEVEN WO-1360 CONVERSIONS for the same hole** - `pause-menu`, `game-over`,
   `wave-results`, `combat-item-picker`, `bug-report-form`, `f8-note-capture`, `vfx-parade-curation`.
   ⛔ Two of seven have now failed in two days. **Assume the rest are guilty until each is proven to
   release on every exit path** - destroyed, disabled, arbiter-closed, scene-changed, and replaced.
3. **Fix the duplicate HeroDeath raise.** Two end-states 9ms apart for one death is its own defect and
   it is what orphans the hold. Why are `retry` and `respawn` both raised?
4. ⭐ **A PlayerOwned hold needs a LIVENESS test, not a ceiling.** The ceiling answered "is this too
   old"; the right question is "does its owner still exist". ⚠ **The pattern already exists in this
   repo** - `Core/Combat/OverTimeEffects.cs` (WO-1330) makes liveness a **REQUIRED constructor
   argument that THROWS on null**, *"so the engine cannot be built without saying how to test it."*
   **Reuse that shape; do not invent a second one.**

## ACCEPTANCE

- [ ] ⛔ **Proven from a capture, not from source reading**: a hero death followed by any exit path
      leaves **zero** outstanding holds. Quote the `LAST hold gone, timeScale 1.00` line.
- [ ] ⛔ **Proven RED first** - reproduce the orphan, show `timeScale 0.00` persisting, then show it
      released. A fix for a freeze that was never seen red is not evidence of anything (WO-1138).
- [ ] All seven WO-1360 conversions audited, each with its release proven on **every** exit path.
      List them and say which were guilty.
- [ ] Only ONE HeroDeath end-state is raised per death.
- [ ] An oracle fails the build if any `PlayerOwned` hold can outlive its owner. ⚠ Without it this
      recurs silently - `REGRESSION_OK 358/358` was green on the build that froze her twice.
- [ ] ⛔ The fix does NOT restore a ceiling to `game-over`.

## WHAT NOT TO TOUCH

- ⛔ Do not revert WO-1360. The PlayerOwned model is correct; its ownership audit was incomplete.
- ⛔ Do not add a ceiling back to any converted hold.
- ⛔ Do not touch the wallet/purchase hold - the owner ruled 2026-09-04 that the 180s ceiling STAYS
      on wallet signing (WO-1360 §4). That ruling is unaffected by this ticket.

## RELATED

- **WO-1360** (`3e6ae4274`) - introduced the regression; its §4 carries the owner's separate wallet ruling.
- **WO-1353** (`ecb1a1a5e`) - gave `Time.timeScale` one owner; the reason WO-1360 existed.
- **WO-952** - the same duplicate-end-state cascade appears there as a symptom; the two tickets share
      the `destroyed WITHOUT firing its primary action` evidence but are different defects.

---
## RCA re-verified 2026-09-04 (QA read-only pass)
**Verdict:** SUPERSEDED
**Evidence:**
- Commit `f6540db88 2026-09-04` (ancestor of HEAD) body: "WO-1369 - THE P0 HARD FREEZE... unsubscribe + re-check the defeat scene, the hold no longer delegated..., the two GUILTY holds... plus the two PARTIAL ones closed"; "ReleaseAllForSceneLoad had ZERO runtime callers" wired.
- Fix (a): `Assets/_Modules/Core/UI/WorldHold.cs:446 AcquirePlayerOwned(string reason, Func<bool> isOwnerAlive)`; `:463-471 RequireProbe` throws `ArgumentNullException`. Every caller passes a probe: `GameOverScreen.cs:415 () => _deathView != null`, `PauseController.cs:284`, `HudKitController.cs:2045`, `BugReportView.cs:98`, `BreakCaptureHarness.cs:594`, `VfxParadeRuntime.cs:192`, `EndStateView.cs:211`.
- Fix (b): `GameOverScreen.cs:138 _deathView`, `:166-179 PollDeathViewLiveness()` releases the hold and resets `_shown` when the view dies; `:234 Update()`.
- Fix (c): `GameOverScreen.cs:181 OnSceneLoaded` -> `:191 Unhook()`; `:210-213 Unhook()` does `-= ShowHeartFell` / `-= ShowHeroFell`; `:270-281 GateHandlerToDefeatScene` re-checks `IsDefeatScene`; `:89 Unhook()` in OnDestroy.
- Fix (d): `WorldHold.cs:419-420` doc "WIRED to SceneManager.sceneLoaded by WireSceneLoadRelease"; `:940-945 OnSceneLoadedReleaseAll` -> `ReleaseAllForSceneLoad(scene.name)` (`:944`).
- PARTIAL holds closed: `HudKitController.cs:4069-4079 OnDisable` ("WO-1369 PARTIAL #1"); `BugReportView.cs:105-111 OnDisable` ("PARTIAL #2"); `BreakCaptureHarness.cs:266-268` + `:285-292`.
- Oracles: `Assets/Editor/Regression/WorldHoldLivenessRegression.cs` (`:145` null-probe throws, `:214` game-over probe, `:319` call-site regex) and `GameOverScreenLifecycleRegression.cs:126` (requires `AcquirePlayerOwned(HoldReason, () => _deathView != null)`), registered `DataRegression.cs:675, :677`.
- WO-1360 is CLOSED 2026-09-04 (owner felt PASS); `WorldHold.cs:211 StuckHoldSeconds = 180f` unchanged - the wallet ruling is intact. `EndStateView.cs` comment cited at `:2124-2140` now sits at `:2466`.
- Post-fix device capture: none. `logs/device/` newest logs are the 09-04 morning pre-fix pulls; `grep -c "WorldHold RELEASE 'game-over'"` = 0 in all three (the defect capture, as expected).
**What changed since the RCA:** all four fix shapes (a)-(d) landed in `f6540db88`; this WO's `**Status:**` line was never flipped (`git log -1 -- <WO>` = `58abaf093`, pre-fix).
**Ready for a lane?** no - implemented and oracle-pinned; the open acceptance is the capture-based proof ("LAST hold gone, timeScale 1.00" after a death on device). Files a lane would touch: this WO (Status line).
**Pins/rulings needed:** owner felt-verify of a hero death in a dungeon on a build newer than `354315`; no design ruling.

---
## RCA re-verified from capture 2026-09-04

Read-only pass. Every line below is quoted from `logs/device/freeze-20260904-095249.log` (line numbers are
the file's own; the freeze session is pid `28972`, `Version '2026.09.04.354315'` at L833451) or opened at
source at HEAD `3f49e93d5` / the pre-fix tree `9779b9639` (= `f6540db88^`, the last commit before the fix
landed at 12:47; the APK on her device launched at 09:30 and is pre-fix). Nothing is inferred.

### A. The capture, reconstructed (RED is PROVEN)

| Log line | Time | Event |
|---|---|---|
| L967018 | 09:37:59.219 | `[Flow:DeathTrace] TIMESCALE -> 1 (RESTORED, step-out) by GameOverScreen.OnSceneLoaded('dg_folks_granary')` - the DDOL singleton saw the dungeon load; pre-fix `:131-132` nulled `_hero` WITHOUT `-= ShowHeroFell` |
| L986929 | 09:38:25.058 | `[Flow:Death] lethal hit ... OnDeath listeners=[GameOverScreen.ShowHeroFell, HeroDeathEndState.OnHeroDeath]` - BOTH handlers still subscribed on the carried `HeroHealth` inside a dungeon |
| L986991 | 09:38:25.063 | `[Flow:Pause] WorldHold ACQUIRE 'game-over' @ 0.00 -> effective timeScale 0.00 (slowest wins), 2 holds outstanding [fx:hit-stop, game-over]` - stack L986994-995 `GameOverScreen:Show` <- `GameOverScreen:ShowHeroFell` |
| L986999 | 09:38:25.063 | `WorldHold 'game-over' is PLAYER-OWNED: ... NO watchdog ceiling applies and it will never be force-released for being old. ... Its release is owned by the UI that took it (WO-1360).` - **kind = PlayerOwned, ceiling = none** |
| L987085 | 09:38:25.071 | `[Flow:EndState] HeroDeath shown: spoils=0 action=retry` (the GameOverScreen view, carrying `OnRetry`) |
| L987136 | 09:38:25.073 | `SCREEN OPENED: EndState 'YOU HAVE FALLEN' by ...HeroDeathEndState.OnHeroDeath` - the SECOND raise, 10 ms later |
| L987150 | 09:38:25.074 | `W 'YOU HAVE FALLEN' destroyed WITHOUT firing its primary action - EndStateView.Show - REPLACED by a new end-state 'YOU HAVE FALLEN'. That action is now abandoned.` - **the `retry` view (the only object holding the release delegate) is destroyed** |
| L987199 / L987223 | 09:38:25.080-.081 | `HeroDeath shown: spoils=0 action=respawn` then `destroyed WITHOUT firing ... CloseFromArbiter` (the arbiter swaps it again) |
| L987327 | 09:38:25.167 | `RELEASE 'fx:hit-stop' ... STILL HELD, 2 hold(s) remain [game-over, fx:death-slowmo], effective timeScale 0.00` |
| L987824 | 09:38:26.284 | `RELEASE 'fx:death-slowmo' @ 1.00 after 1.23s unscaled -> STILL HELD, 1 hold(s) remain [game-over], effective timeScale 0.00` |
| L987939 | 09:38:26.511 | `HeroDeath primary fired: action=respawn` - a primary DID fire, on the `HeroDeathEndState` view; no `game-over` release follows |
| L988856 | 09:38:29.070 | `W [Flow:DeathTrace] TIMESCALE STILL 0 after 4.0s - GameOverScreen.Show froze time and it has NOT been restored within the death flow` |
| L1024365 / L1025586 | 09:40:20.748 / 09:40:23.383 | `ACQUIRE 'combat-item-picker' ... [game-over, combat-item-picker]` then `RELEASE 'combat-item-picker' @ 0.00 after 2.64s ... STILL HELD, 1 hold(s) remain [game-over], effective timeScale 0.00` - she was still tapping (L1024399 picker opened; L1027076/L1027437/L1027843 three `EchoRoster` opens `REJECTED (battle-lock)`) |
| L1030821 / L1030831 | 09:40:32.677-.679 | `ACQUIRE 'pause-menu' ... [game-over, pause-menu]` / `PAUSE MENU -> WorldHold taken. Outstanding: [game-over, pause-menu]` |
| L1031652-653 | 09:40:33.245-.252 | `ActivityTaskManager: Destroy timeout of remove-task, attempt to kill Task{...echoesofelarion}` / `ActivityManager: Killing 28972:com.denellestudios.echoesofelarion/u0a348 (adj 905): remove task` |

- `grep -c "WorldHold RELEASE 'game-over'"` over the whole 1,230,143-line buffer = **0** (re-run this pass).
- `grep -n -E "OVERRAN|STUCK WORLD HOLD|OPEN PLAYER-OWNED HOLD"` for pid 28972 = **0 hits**: no ceiling fired (none applies), and the
  180 s "names itself once" line never came because the hold was only 128 s old when the OS killed the process.
- **What the player saw** (`[Flow:Perf]`, one line per ~4 s): L987361 `fps=34 ms=29.4 ... scene=dg_folks_granary enemies=13` at 25.236,
  then L990238-L1028254 a flat run of `W [Flow:Perf] LOW fps=23-26 ms=41-47 ... enemies=13` from 09:38:33 to 09:40:30 - the app was
  rendering ~23 fps for the full 2 min 08 s with `timeScale 0.00` and `enemies=13` unchanged. **A rendering, input-accepting world with
  the clock stopped** - not a hang, not OOM (`mem=484MB` flat), exactly what the WO states.
- Acquire -> death of the release-carrying view: 09:38:25.063 -> 25.074 = **11 ms** (the WO's "18 ms" counts to the arbiter close at .081).

### B. Read at source

**(1) Exact acquire site and the unreachable release site - pre-fix `9779b9639`, the build she froze on:**
- Acquire: `Assets/_Modules/Village/Heart/GameOverScreen.cs:267` `_worldHold = WorldHold.AcquirePlayerOwned(HoldReason);` (the WO's
  ":264" is the comment block above it). Pre-fix `WorldHold.cs:374` `AcquirePlayerOwned(string reason)` took NO probe.
- Release delegate handed away: `:276` `EndStateView.Show(EndStateVM.FromGameOver(isHeartDestroyed, title, body, OnRetry));` - the ONLY
  reference to `OnRetry` (`:281-283` `ReleaseWorldHold("the player chose Retry")`) lives on that view, which L987150 proves was destroyed
  11 ms later without firing.
- The other two callers of `ReleaseWorldHold` (`:110`): `OnSceneLoaded` `:126` (no scene loaded - the clock was 0) and `OnDestroy` `:89`
  (`:79 DontDestroyOnLoad(gameObject)` - never destroyed). There was no `OnDisable`, and it would not have mattered: the component was
  enabled throughout (its own `Update`-less singleton has nothing to disable).
- Why the handler fired in a dungeon at all: `:52 IsDefeatScene => HubScenes.IsHub(sceneName)`; `HubScenes.cs:24` `Names = { "Village2",
  "MainCastle_Hall", "CastleHub", "CastleHub_MainKeep", "Main_Castle_Overworld" }` - `dg_folks_granary` is not a hub, so `OnSceneLoaded`
  `:134` did not re-Hook, but `:131-132` `_heart = null; _hero = null;` never unsubscribed either. `ShowHeroFell` `:205-232` stands down
  only for `BattleArena.AnyBattleInProgress` and `HubScenes.IsOverworld` - neither is a dungeon - so it ran. `HeroDeathEndState.cs:107`
  gates on `IsHub(scene) && !IsOverworld(scene)` - also not a dungeon - so it ran too. That is the double raise at L986929/L987085/L987136.
- The `respawn` primary at L987939 is `EndStateVM.FromHeroDeath` on the second view; nothing in that chain references `GameOverScreen`.

**HEAD `3f49e93d5` (fix `f6540db88` is an ancestor):** acquire moved to `GameOverScreen.cs:415`
`AcquirePlayerOwned(HoldReason, () => _deathView != null)` with `_deathView` captured at `:398` from `EndStateView.Show(...)` BEFORE the
acquire; `:166-179 PollDeathViewLiveness()` (called from `:234 Update`) releases + re-arms `_shown` when the view is gone; `:97-100 OnDisable`
added; `:210-213 Unhook()` does the `-=`, called from `:89 OnDestroy` and `:191 OnSceneLoaded`; `:278-281 GateHandlerToDefeatScene` re-checks
`IsDefeatScene` at the top of BOTH handlers (`:257`, `:327`) - so in a dungeon `ShowHeroFell` now stands down and only `HeroDeathEndState`
raises. `WorldHold.cs:446-448 AcquirePlayerOwned(string, Func<bool>)` -> `:467-471 RequireProbe` throws `ArgumentNullException`; the
watchdog `:838-861` polls `AskOwnerAlive()` per tick and force-releases with `FlowTrace.Fail("ORPHANED PLAYER-OWNED HOLD ...")`, age is never
consulted for PlayerOwned (`:884` ceiling branch is only reached for bounded kinds). `:986-989 WireSceneLoadRelease`
(`RuntimeInitializeOnLoadMethod`) subscribes `:940-944 OnSceneLoadedReleaseAll` -> `ReleaseAllForSceneLoad` (Single-mode loads only).

**(2) The seven WO-1360 conversions - acquire/release pairs audited at pre-fix AND at HEAD:**

| Hold | Pre-fix acquire | Pre-fix release paths | Same shape as `game-over`? | HEAD |
|---|---|---|---|---|
| `pause-menu` | `PauseController.cs:279` | `:116 OnDisable`, `:152 OnDestroy`, `:310 Resume`, `:357 ForceReleaseAll` | NO - holder owns the UI; already had OnDisable (WO-1360) | `:284` probe `() => this != null && isActiveAndEnabled` |
| **`game-over`** | `GameOverScreen.cs:267` | `:283 OnRetry` (delegate on a foreign view), `:126 OnSceneLoaded`, `:89 OnDestroy` (DDOL) | **THIS TICKET** | `:415` probe `() => _deathView != null` + `:166` poll + `:97` OnDisable |
| `wave-results` | `EndStateView.cs:207` | `:2194 OnDestroy` `_worldHold?.Dispose()` | NO - holder IS the view; any destroy (arbiter `:2171`, replace, scene) runs OnDestroy | `:211` probe `() => view != null` (same lambda as the `PanelHandle` at `:201`) |
| `combat-item-picker` | `HudKitController.cs:2007` | `:2134 CloseItemPicker` (idempotent), `:3991 OnDestroy` -> `CloseItemPicker` | PARTIAL - no `OnDisable`; a deactivated HUD strands it. Not the delegated-release shape | `:2045` probe `() => this != null && _itemPicker != null`; `:4079 OnDisable` -> `CloseItemPicker` |
| `bug-report-form` | `BugReportView.cs:91` | `:96-102 OnDestroy`, `:323 Close()` | PARTIAL - no `OnDisable`, same as above | `:98` probe `() => this != null && isActiveAndEnabled`; `:111 OnDisable` |
| **`f8-note-capture`** | `BreakCaptureHarness.cs:537` | `:564-566 CommitFlag` ONLY. `:244-254 OnDestroy` does NOT touch `_worldHold`; no OnDisable; commit is an `OnGUI` (`:608`) key path with no cancel | **GUILTY, different mechanism** - a hold with exactly one release path that requires the owner to keep running, and no teardown net at all | `:594` probe `() => this != null && isActiveAndEnabled && _noteMode`; `:268 OnDisable` + `:292-294` cancel release; `:680` Escape commits |
| `vfx-parade-curation` | `VfxParadeRuntime.cs:189` (OnEnable) | `:193 OnDisable`, `:199-209 OnDestroy` | NO - paired lifecycle | `:192` probe `() => this != null && isActiveAndEnabled` |

Verdict on the WO's 2/2/3 table: **agrees.** Only `game-over` has the "release delegated to an object the holder does not own and cannot
outlive-detect" shape; `f8-note-capture` is guilty by a different route (single release path, no net); the two PARTIALs are the WO-1360
disabled-not-destroyed shape. All seven at HEAD now carry a lambda probe on the object that can actually die, and
`WorldHoldLivenessRegression.cs:52-68 Owners` lists all seven with their required lifecycle methods.

**(3) Fix shape - the WO's four changes vs what the data supports:**
- (a) required liveness probe: **agree, and it is the only change the capture would have felt in-frame.** L987150 is the view dying
  at 25.074; with the HEAD watchdog (`WorldHold.cs:849`) the next tick sees `_deathView == null` -> `ORPHANED PLAYER-OWNED HOLD` ->
  release. A ceiling would have thawed at 25.063+180 s = 09:41:25, i.e. AFTER the OS kill at 09:40:33 - so the WO's "would have masked
  it at 180 s" is optimistic: **on this capture even the old ceiling would not have saved her.** The probe is not a nicer ceiling, it is the
  only net that fires before the kill.
- (b) bind the hold to the view: **agree** - `:398/:415` order (view first, then probe) plus the `:166` poll makes the singleton itself
  release without waiting for the watchdog; `_shown` reset at `:176` closes the secondary "next death swallowed" defect.
- (c) `Unhook` + scene re-check: **agree, and it is what prevents the double raise** - the capture proves the stale subscription
  (L986929 lists both listeners after L967018 showed the dungeon load pass through `OnSceneLoaded`). Note (c) alone would also have
  prevented this capture (no `ShowHeroFell` in a dungeon = no `game-over` hold at all), which the WO says.
- (d) `ReleaseAllForSceneLoad` wiring: **agree it was prose-only** (pre-fix grep matched only the regression harness); wired at HEAD.
  One caveat the WO does not state: `:939` returns early for `LoadSceneMode.Additive`, so it is a net for Single loads only - fine for
  quit-to-title, not a net for anything composed additively. Not a defect, but do not cite it as covering every scene change.
- Nothing here restores a ceiling: `WorldHold.cs:211 StuckHoldSeconds = 180f` is unchanged and only reached for non-PlayerOwned kinds.

**(4) The oracle:**
- `WorldHoldLivenessRegression.ADeadOwnerIsForceReleasedImmediately` (`:207-240`) is the case that models THIS shape verbatim: acquire
  `"game-over"` with `() => view != null`, tick once (must stay held - the live-owner half), set `view = null`, tick again, and FAIL with
  `[dead-owner] THE WO-1369 P0, UNFIXED` if `WorldHold.Count != 0` or the clock is not 1.00. Against a watchdog that ignores the probe
  it goes RED; against the pre-fix API (`AcquirePlayerOwned(string)` only) the file does not COMPILE, which is how "red first" for the
  `[api]` case (`:108-160`, null probe must throw) would have to be shown.
- `GameOverScreenLifecycleRegression` `[unhook]` (`:96` OnSceneLoaded must call Unhook), `[scene-gate]` (`:106`), `[hold-owner]`
  (`:127` probe must name `_deathView`), `[shown-reset]` (`:159-173` poll exists and Update calls it), `[no-ceiling]` (`:193`) pin the
  source shape of (b)/(c).
- `TransactionWorldHoldRegression` (`:137-508`): **no existing case goes RED on this shape** - `CaseScopeReleasesOnEveryExit` covers a
  using-scope/exception/double-dispose on a bounded hold, `CaseForceReleaseAlwaysUnfreezes` covers `ForceReleaseAll`,
  `CaseBackgroundTimeDoesNotAgeHolds` covers age under OS suspension, `CaseRefCountedAcrossOverlappingHolds` covers pause-over-purchase.
  The missing case would be "a PlayerOwned hold whose probe flips false is dropped on the next tick; one whose probe stays true is
  never dropped at any age" - which is exactly `ADeadOwnerIsForceReleasedImmediately` + `ALiveOwnerIsNeverForceReleased`
  (`:168-200`) in the new suite. Do not duplicate it into the transaction suite; that suite is about the wallet ceiling, which the
  owner ruled stays.

### C. Two things the record still does not prove (§11B - said, not ticked)

1. **"Proven RED first" for the liveness/lifecycle oracles has NO surviving log on disk.** The commit body of `f6540db88` claims it
   (`every one proven RED against the real failing input before it went green`). What exists in `Builds/`: `wave-regression.log`
   (12:26, `REGRESSION_FAIL: 2 ... 358/360` - both failures are `REGRESSION MARKER FAIL ... unregistered oracle`, i.e. the new suites had
   not RUN), `wave-regression2.log` (12:28, same), `wave-regression3.log` (12:33, `362/363` - `[worldhold-liveness]` and
   `[gameover-lifecycle]` already GREEN at L110245/L110252; the one red is `EndState body fit`), `wave-regression4.log` (12:46, `363/363`).
   `grep -rl "P0, UNFIXED\|does not name the END-STATE VIEW" Builds logs WorkOrders` = no file. Compile-level red (the pre-fix API has no
   probe parameter) is plausible but is not on disk either. **Acceptance box 2 is not closed by evidence available here.**
2. **No post-fix device capture.** `logs/device/` holds only the 09-04 morning pre-fix pulls; acceptance box 1 (`LAST hold gone,
   timeScale 1.00` after a dungeon death on a build newer than `354315`) is open, as the QA pass above already said.

Everything else in the WO's RCA is confirmed line-for-line from the capture and from source at both trees. Status line remains
unflipped in this file; not changed by this read-only pass.
