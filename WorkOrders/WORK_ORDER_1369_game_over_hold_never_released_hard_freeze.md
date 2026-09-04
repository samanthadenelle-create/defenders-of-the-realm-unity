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
