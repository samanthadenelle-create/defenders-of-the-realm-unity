# WORK ORDER 1360 - A USER PAUSE HAS NO CEILING

**Status:** FIXED 2026-09-03 - ON HER DEVICE. A regression WE shipped tonight and her F8 caught within the hour: WO-1353's per-hold ceiling force-released the PAUSE freeze after 180s, and the screenshot proved the PAUSED menu was on screen at that moment - the world running behind her own pause panel. The fix is categorical, not a bigger number: `HoldKind { BoundedBeat, PlayerOwned }`, with the CEILING STILL THE DEFAULT so unbounded must be asked for by name. Seven holds converted, five bounded beats unchanged, four left alone because they already dodge the ceiling with a per-frame Renew. Removing the ceiling exposed the hole it had been papering over - PauseController disposed on OnDestroy but not OnDisable, and a disabled component never gets OnDestroy nor can process Resume - now closed, releasing the hold AND hiding the panel together. âš  FLAGGED WITH MONEY ATTACHED, NOT CHANGED: the 180s TRANSACTION ceiling. Wallet signing is user-paced and can exceed three minutes; if it fires, the world thaws under a live payment - a manufactured route into 'paid but not granted'. That is an owner call. Gates COMPILE_GATE_OK + REGRESSION_OK 358/358. AWAITING HER FELT-VERIFY that a long pause stays paused; then Owner Validation closes it.
**Minted:** 2026-09-03 (CLI main line; banner bumped 1360 -> 1361 in the same edit)
**Silo:** Core / world clock (WorldHold) + the UI surfaces that hold it
**Supersedes nothing. Corrects:** WO-1353 (the per-hold ceiling failsafe)

---

## 1. THE DEFECT - A REGRESSION WE SHIPPED TONIGHT, CAUGHT ON HER DEVICE

WO-1353 gave `Time.timeScale` one owner with paired holds plus three failsafes, one of
which is a per-hold maximum on the UNSCALED clock (`StuckHoldSeconds = 180f`). That
ceiling is wrong for a user-driven pause, and it broke pause.

Owner F8 capture, **seq 4679, 19:08, 2026-09-03**, on the build installed an hour before:

```
[Flow:Pause] STUCK WORLD HOLD: 'pause-menu' (scale 0.00) has been outstanding for
  507.3s, past its 180.0s ceiling. It OVERRAN by 327.3s. Its owner never disposed it
  ... Force-releasing so the world is not left slow.
    DeNelle.Core.UI.WorldHold:WatchdogTick(Single)

[Flow:Pause] WorldHold RELEASE 'pause-menu' @ 0.00 after 513.15s unscaled
  -> LAST hold gone, timeScale 1.00
```

**THE SCREENSHOT PROVES THE HOLD WAS NOT STUCK.**
`logs/f8-inbox/device/SM02G4061955851/break_01_error.png` shows the **PAUSED** menu open
on screen - RESUME / SETTINGS / QUIT TO TITLE, over the town, hero visible below the
card. She had legitimately paused (or backgrounded with it open) for eight minutes. The
watchdog force-released the freeze **while the pause menu was still up**, so the world ran
underneath a screen that said the game was stopped - the exact WO-1016 shape that the
slowest-wins rule was chosen to prevent.

### Why the ceiling is right for one class of hold and wrong for the other

A **cosmetic dip** that outlives its host is a leak, and a ceiling is the right guard: a
hit stop is milliseconds, a celebration under a second, and a coroutine killed by a
deactivated host fires no `OnDestroy` and throws nothing, so nothing else can catch it.

A **user-driven pause has no natural ceiling at all.** A player can pause for hours;
backgrounding the app is the normal way to do it. Applying a leak-detection heuristic to
an intentional, user-owned state is a **category error** - and the consequence is worse
than the leak it guards.

> **RAISING 180s TO A BIGGER NUMBER IS NOT THE FIX.** It reproduces the same bug at a
> longer timeout - the "a human remembers" class of fix. The distinction is CATEGORICAL,
> not quantitative.

---

## 2. THE FIX - THE SHAPE, WHICH MATTERS MORE THAN ANY NUMBER

A new categorical kind on the hold, with the **safe case as the default** and the
unbounded case something an author must **ask for by name**.

```csharp
public enum HoldKind
{
    BoundedBeat = 0,   // the CODE owns the duration -> the ceiling applies. THE DEFAULT.
    PlayerOwned = 1,   // the PLAYER owns the duration -> no ceiling, ever.
}
```

API surface (`Assets/_Modules/Core/UI/WorldHold.cs`):

| entry point | kind | ceiling |
|---|---|---|
| `Acquire(reason)` | BoundedBeat | `StuckHoldSeconds` (180s) - unchanged |
| `AcquireScale(reason, scale, maxUnscaledSeconds)` | BoundedBeat | caller's - unchanged |
| **`AcquirePlayerOwned(reason)`** *(new)* | PlayerOwned | **none** |
| **`AcquirePlayerOwnedScale(reason, scale)`** *(new)* | PlayerOwned | **none** |

All four funnel through one private `AcquireKind(...)`; `Acquire` still delegates to
`AcquireScale(reason, 0f, ...)` so `WaveModalSafetyRegression`'s structural regex still
holds. `Handle.IsPlayerOwned` is public so an oracle and a capture can read the kind.

**Why an opt-in enum rather than a bigger number or a flag on the ceiling:** a future
author who does not think about the distinction gets the leak-detecting default. The only
way to get an unbounded hold is to name it, and the name says why.

### The watchdog

`WatchdogTick` skips the ceiling for a `PlayerOwned` hold and **never force-releases it**.
It still **reports**, once, at `PlayerOwnedReportSeconds` (180s - the old ceiling value, so
a trace read against tonight's logs lines up):

```
[Flow:Pause] OPEN PLAYER-OWNED HOLD: 'pause-menu' (scale 0.00) has been outstanding for
  507.3s. This is NOT a leak and it will NOT be force-released ... Logged once so a
  capture taken during a long pause can still say what holds the clock.
```

A hold that IS genuinely stuck - i.e. a bounded beat - still names itself loudly with the
unchanged `STOP: STUCK WORLD HOLD` `FlowTrace.Fail`. Nothing was stripped (CLAUDE.md
sec.12).

### What still protects a player-owned hold

Removing the ceiling removes ONE net, not all of them:

1. **`ReleaseAllForSceneLoad`** - a single-mode scene load drops every hold and stamps
   1.00. `Time.timeScale` is an engine global that a load does NOT reset, so this is what
   makes quit-to-title unable to land in a frozen scene. Pinned by the oracle.
2. **`ForceReleaseAll`** - teardown / quit-to-title (`PauseController.OnQuitClicked`
   already calls it). Pinned by the oracle.
3. **The zero-holds drift watchdog** - once released, `timeScale != baseline` with zero
   live holds is corrected and named within `DriftGraceSeconds`.
4. **The owning UI's own lifecycle** - and this is the one that actually replaces the
   ceiling.

### THE REAL HOLE, NAMED AND CLOSED

`PauseController.OnDestroy` disposed the hold. **`OnDisable` did not.** A component that
is merely deactivated does not get `OnDestroy`, and a deactivated PauseController cannot
process Resume - so with the ceiling gone, a pause menu deactivated while paused would
have stranded the world frozen **forever**. The ceiling had been papering over that.

Closed: `OnDisable` now releases the hold **and hides the panel together**, with a
`FlowTrace.Warn`. Together matters - releasing the hold alone would leave an orphaned
PAUSED panel over a running world, which is the same WO-1016 shape from the other
direction. Pinned by the oracle (Direction 5 asserts PauseController has both step-outs).

---

## 3. EVERY `AcquireScale` / `Acquire` CALL SITE, CLASSIFIED

| # | call site | reason token | scale | ceiling before | class | action |
|---|---|---|---|---|---|---|
| 1 | `Village/Vfx/HitStopManager.cs:294` | `hit-stop` | 0.02-0.05 | **2s** | bounded beat | unchanged |
| 2 | `Village/Vfx/CombatFeedbackManager.cs:316` | hit stop / kill slow-mo | 0.05 / 0.30 | **3s** | bounded beat | unchanged |
| 3 | `Village/Waves/WaveCelebrationManager.cs:232` | wave-clear dip | 0.28 | **4s** | bounded beat | unchanged |
| 4 | `Village/Hero/HeroHitReaction.cs:193` | death ramp | 0.30 | **4s** | bounded beat | unchanged |
| 5 | `Village/Arena/ArenaDeathCam.cs:172` | `arena-death-cam` | saved / 0.30 | **15s** | bounded beat | unchanged |
| 6 | `Wallet/PackStore.cs:3075` | `purchase` | 0 | **180s** | **see sec.4** | **unchanged - reported, not redesigned** |
| 7 | `Settings/PauseController.cs:260` | `pause-menu` | 0 | 180s | **PLAYER-OWNED** | **converted** (the P0) |
| 8 | `Village/Heart/GameOverScreen.cs:264` | `game-over` | 0 | 180s | **PLAYER-OWNED** (ends on Retry) | **converted** |
| 9 | `Village/UI/EndState/EndStateView.cs:205` | `wave-results` | 0 | 180s | **PLAYER-OWNED** (decision node) | **converted** |
| 10 | `HUD/Kit/HudKitController.cs:2000` | `combat-item-picker` | 0 | 180s | **PLAYER-OWNED** | **converted** |
| 11 | `HUD/BugReportView.cs:90` | `bug-report-form` | 0 | 180s | **PLAYER-OWNED** (typing) | **converted** |
| 12 | `Core/Diagnostics/BreakCaptureHarness.cs:534` | `f8-note-capture` | 0 | 180s | **PLAYER-OWNED** (typing) | **converted** |
| 13 | `VfxParade/Runtime/VfxParadeRuntime.cs:188` | `vfx-parade-curation` | 0 | 180s | **PLAYER-OWNED** (dev tool, human-paced) | **converted** |
| 14 | `Core/UI/FocusedModalHost.cs:21,33` | `focused-card-modal` | 0 | 180s | player-owned | **left as-is - see note** |
| 15 | `Core/UI/ObsidianNavigationWorkspace.cs:53` | `obsidian-navigation-workspace:<name>` | 0 | 180s | player-owned | **left as-is - see note** |
| 16 | `Core/UI/HarvestOverflowModal.cs:29` | `harvest-overflow-result` | 0 | 180s | player-owned | **left as-is - see note** |
| 17 | `Village/Crafting/JewelerDiscoveryFtue.cs:61` | `jeweler-discovery` | 0 | 180s | player-owned | **left as-is - see note** |

**Note on 14-17 (deliberate, not an oversight).** All four are player-owned states that
already dodge the ceiling by calling `WorldHold.Renew(_hold)` **every `Update()`** - and
`Update` runs at `timeScale == 0`, so their deadline never arrives while the panel is
visible. They are **not broken today**; converting them is cleanup, not a fix, and this
ticket stayed narrow because the lead is holding a ship build. The `Renew`-every-frame
pattern IS the workaround the enum replaces, so the follow-up is: convert 14-17 to
`AcquirePlayerOwned` and delete their per-frame `Renew` calls. `Renew` itself stays - it
is still the right seam for a bounded beat that legitimately extends.

---

## 4. THE 180s TRANSACTION CASE - REPORTED, NOT REDESIGNED

`PackStore.cs:3075` takes `WorldHold.Acquire(ReasonPurchase)` at the 180s ceiling.
**It has the same shape as the pause bug and it has money attached**, so it is flagged
here and deliberately left alone (the lead's constraint: report, do not silently redesign
the purchase flow).

The concern, precisely:

* A Solana purchase sends the player **out to a wallet app to sign**. That leg is
  **user-paced**, not code-paced - a first-time wallet install, a seed-phrase prompt, a
  2FA detour or a phone call can all exceed three minutes.
* On that path the app is **backgrounded**, so `NotifyApplicationPause` already excludes
  OS-suspended time from the hold's age (WO-1260). That materially reduces the exposure -
  but it does not eliminate it: any foreground-but-slow leg still accrues, and the
  suspend-exclusion depends on `OnApplicationPause` firing reliably on the device.
* If the ceiling does fire mid-transaction, the world thaws under a live payment: the
  player is dropped back into a running battle while a signed transaction is in flight,
  which is a manufactured route into "paid but not granted" (WO-1121 sec.1.1).
* Force-releasing does NOT cancel the purchase - it only unfreezes. So the failure is
  silent and the money outcome is unchanged; what changes is that the player can be
  killed while paying.

**The purchase hold is arguably TWO holds in sequence** - a code-owned settlement leg
(chain confirm -> server verify -> grant) which a ceiling suits, and a user-owned signing
leg which it does not. That is a design call with money implications and it belongs to the
owner, not to this ticket. **Recommended follow-up:** split the purchase hold at the
hand-off to the wallet app, or convert the whole thing to player-owned and give the store
UI a hard step-out on its own lifecycle the way PauseController now has.

**No behaviour on the money path changed in this ticket.**

### ⛔ OWNER RULING 2026-09-04 - THE 180s CEILING STAYS ON WALLET SIGNING. CLOSED.

Owner, verbatim: ***"180 stays on wallet"***.

**The recommended follow-up above is DECLINED and this item is no longer an open owner call.**
`PackStore.cs:3075` keeps `WorldHold.Acquire(ReasonPurchase)` at `StuckHoldSeconds`. The purchase
hold is NOT split, and it is NOT converted to `AcquirePlayerOwned`.

**This ruling is a decision to accept a known, documented exposure - not a claim that the exposure
is absent.** Everything in section 4 above stays true and stays readable: a foreground-but-slow
signing leg can still reach the ceiling, and if it does the world thaws under a live payment. The
suspend-exclusion (`NotifyApplicationPause`, WO-1260) remains the mitigation that makes the
backgrounded case - the common one - safe.

⛔ **Do not re-open this as a defect, and do not "helpfully" convert the purchase hold in a later
sweep.** A future seat reading section 4 cold will find a persuasive argument for conversion; the
argument was read, and the owner ruled the other way. If the exposure is ever OBSERVED in a capture
rather than reasoned about, that is new evidence and a new ticket - cite the captured line.

---

## 5. THE ORACLE - `BattleQuiescenceRegression.APlayerOwnedHoldOutlivesEveryCeiling`

Extends the suite that already owns this concern (`DataRegression.cs` is fenced - the lead
registers suites; this case is called from the existing `RunAll` body alongside
`AnOverrunHoldSelfReleasesAndReports`). Five directions, because a one-directional test
would pass a fix that simply switched the watchdog off:

1. **A player-owned pause survives arbitrarily long.** `AcquirePlayerOwned("pause-menu")`,
   then `WatchdogTick` at **+507.3s** (the captured number), **+3600s** and **+60000s**.
   Count must stay 1 and the clock 0.00 at every one. Then a normal `Dispose()` must
   return the world to 1.00.
2. **A bounded beat still expires and reports.** A 0.28 dip with a 0.5s ceiling, ticked at
   +2s, must be force-released and the clock returned to 1.00.
3. **The ceiling is still the DEFAULT.** `AcquireScale(...)` must NOT report
   `IsPlayerOwned`, and must still expire.
4. **The remaining nets still fire.** `ReleaseAllForSceneLoad` and `ForceReleaseAll` each
   drop a player-owned hold and restore 1.00.
5. **The owning UI is the net.** Source assertions that `PauseController` takes
   `AcquirePlayerOwned` and has **both** `OnDisable` and `OnDestroy` step-outs.

### RED PROVEN FIRST, AND THE MUTATIONS

The watchdog's per-hold branch was ported verbatim and run against three semantics
(edit-only ticket: no Unity gate was run - that is the lead's, per sec.7):

```
tonight   D1(pause survives)=False  D2(beat expires)=True   -> RED
fix       D1(pause survives)=True   D2(beat expires)=True   -> GREEN
disabled  D1(pause survives)=True   D2(beat expires)=False  -> RED
```

* **Mutation A - "tonight's code":** make `AcquirePlayerOwned` delegate to
  `AcquireScale(reason, 0f, StuckHoldSeconds)`, i.e. exactly what shipped. **Direction 1
  fails at the first tick (+507.3s)** - the hold is force-released, count 0, clock 1.00,
  which is the captured defect reproduced.
* **Mutation B - "just disable the watchdog":** `continue` for every hold regardless of
  kind. Direction 1 passes; **Direction 2 fails** - the abandoned 0.28 dip survives its
  0.5s ceiling. This is the mutation that makes the test real: the lazy fix is caught.
* **Mutation C - "make PlayerOwned the default":** Direction 3 fails - `AcquireScale`
  reports `IsPlayerOwned` and outlives its ceiling.
* **Mutation D - "drop the OnDisable step-out":** Direction 5 fails.

Against tonight's tree, Directions 1, 3, 4 and 5 additionally fail to **compile** -
`AcquirePlayerOwned` and `Handle.IsPlayerOwned` do not exist there.

---

## 6. FILES TOUCHED

**Core (the shape):**
* `Assets/_Modules/Core/UI/WorldHold.cs` - `HoldKind`, `Handle.Kind` / `.OpenReported` /
  `.IsPlayerOwned`, `PlayerOwnedReportSeconds`, `AcquirePlayerOwned`,
  `AcquirePlayerOwnedScale`, private `AcquireKind`, watchdog exemption + one-shot report.

**Converted to player-owned (7):**
* `Assets/_Modules/Settings/PauseController.cs` - **the P0**, plus the `OnDisable` hole.
* `Assets/_Modules/Village/Heart/GameOverScreen.cs`
* `Assets/_Modules/Village/UI/EndState/EndStateView.cs`
* `Assets/_Modules/HUD/Kit/HudKitController.cs`
* `Assets/_Modules/HUD/BugReportView.cs`
* `Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs`
* `Assets/VfxParade/Runtime/VfxParadeRuntime.cs`

**Oracle + lint co-updates (the seam moved, so the assertions move with it):**
* `Assets/Editor/Regression/BattleQuiescenceRegression.cs` - the new case; plus the
  GameOverScreen source lint now accepts either acquire form.
* `Assets/Editor/Regression/TownMovementFloorRegression.cs` - BreakCaptureHarness lint,
  same.
* `Assets/Editor/Regression/PauseMedievalSkinRegression.cs`,
  `CombatItemPickerRegression.cs`, `PostWaveVictoryModalRegression.cs` - one literal each
  (`WorldHold.Acquire(X)` -> `WorldHold.AcquirePlayerOwned(X)`).

**Constraints honoured:** no game-feel change (hit stop / death freeze / celebration
durations and scales are byte-identical); `BattleQuiescenceGate` remains an OBSERVER and
was not touched; no FlowTrace stripped; `DataRegression.cs` untouched; no `.unity` file
touched; ASCII-only in every added line; brace + NUL check clean on all 13 files.

**Heads-up for the lead:** `HudKitController.cs` already carried uncommitted WO-1359
action-bar-icon work from another lane when this edit landed. This ticket's change there
is one line (`_itemPickerHold`); stage by explicit path.

---

## 7. NOT DONE HERE (edit-only ticket)

Compile gate, `DataRegression` / `BattleQuiescenceRegression` run, screenshot capture,
build and commit are the lead's. Nothing in this ticket was gated or committed by the
implementing agent.
