# WORK ORDER 1369 - P0 HARD FREEZE: the 'game-over' WorldHold is never released, and nothing can rescue it

**Status:** READY TO IMPLEMENT
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
