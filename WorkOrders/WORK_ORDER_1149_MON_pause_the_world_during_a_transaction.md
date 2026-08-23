**Status:** DONE 2026-08-22 - shipped and gated. WorldHold is the SINGLE writer of Time.timeScale; PauseController became a client. Acquired as a `using` declaration and the FIRST statement of PackStore.Purchase, so every exit releases by construction - including the exception path. Pinned by TransactionWorldHoldRegression + the [world-clock] assertions. OWNER-PROVEN: survived the live Devnet purchase.

# WO-1149 - MON - The world keeps running during a purchase

**Minted:** 2026-08-22 (CLI, banner bumped 1149 -> 1150 in the SAME edit)
**Lane:** **MON** - monetization. **Class:** SAFETY, and a money-path hazard.
**Provenance:** owner, 2026-08-22, testing on device: *"we need to stop game during transactions
got killed while making purchase test"*.

## THE DEFECT

Opening the store switches the HUD to Modal posture. **It does not stop the simulation.** Wave
timers keep ticking, the ATB keeps running, enemies keep moving and attacking. The owner was killed
while mid purchase-test.

Verified at source: `PackStore.cs` and `PurchaseGate.cs` reference **no** pause, **no** `timeScale`,
**no** `BattleLock`. Nothing in the purchase path stops the world.

## WHY THIS IS A MONEY-PATH ISSUE, NOT JUST AN ANNOYANCE

A real purchase is **not instant**. The MON contract is: wallet signs -> chain confirms -> server
verifies -> entitlement recorded -> grant -> save verifies -> receipt. That is **many seconds**, some
of it outside our control.

For all of it the player currently:
- **cannot defend themselves**, and
- **cannot cancel without abandoning a transaction that may already have been signed.**

That is the WORST possible moment to force a choice between dying and losing money. And "paid but
not granted" is already ruled a recoverable pending state (MON section 1.1) - do not manufacture new
ways to enter it.

## THE MECHANISM ALREADY EXISTS - WIRE IT, DO NOT BUILD ONE

`Assets/_Modules/Settings/PauseController.cs`, its own header: *"On pause: `Time.timeScale = 0`
(freezes wave timers, the ATB tick, enemy movement) + show. On resume: restore the CAPTURED
pre-pause timeScale."* Six systems already call it, and `Assets/_Modules/Core/UI/PauseGate.cs` is the
Core-level seam built precisely because `DeNelle.HUD` and `DeNelle.Settings` cannot reference each
other. **Route through the existing seam.** ⛔ Do not add a second pause owner.

## SCOPE - AND THE THREE THINGS THAT WILL BITE

1. **Pause on ENTERING the purchase flow, resume on leaving it** - every exit: success, failure,
   cancel, wallet rejection, timeout, and the app being backgrounded mid-flight.
2. ⛔ **RESTORE THE CAPTURED timeScale, NEVER HARDCODE 1.0.** `PauseController` already captures the
   pre-pause value for exactly this reason. A purchase opened from a already-paused state, or during
   a dev time-skip, must not silently resume at full speed.
3. ⛔ **A PAUSE THAT FAILS TO RESUME IS WORSE THAN NO PAUSE** - a frozen game after a completed
   purchase is a support ticket AND a refund. Every early return, every `catch`, every guard clause
   on the purchase path must resume. Prefer a structure where resume cannot be skipped (`finally`,
   or a disposable scope) over remembering to call it on N branches.
4. **Decide the scope of the pause deliberately and say which you chose:** the whole simulation
   (`timeScale = 0`) or combat only. `timeScale = 0` is the proven, already-wired answer and is what
   this WO recommends - but note it also freezes any timer the player might be watching, so state
   the choice rather than inheriting it.

## \u26a0 THE ADJACENT QUESTION, NAMED NOT ANSWERED
Should the store be openable AT ALL mid-combat? `BattleLock` already gates other actions during a
fight. Refusing to open the store while enemies are engaged is a DIFFERENT and possibly better
answer than pausing. **That is an owner call** - do not implement it silently in place of the pause.

## ACCEPTANCE

- [ ] Opening the purchase flow stops wave timers, the ATB tick and enemy movement
- [ ] EVERY exit resumes - success, failure, cancel, rejection, timeout, backgrounded - proven by
      driving each path, not by reading the code
- [ ] The resumed timeScale is the CAPTURED pre-pause value, not a hardcoded 1.0
- [ ] Regression pins that no purchase-path branch can return without resuming.
      \u26a0 MEASURE, do not RESTATE: assert the resume actually happens on each branch; do not merely
      grep that a `Resume()` call exists somewhere in the file
- [ ] Owner felt-verify on device: open the store mid-wave and survive it
