# WORK ORDER 1233 - The battle-lock SURVIVES the battle 8 times out of 9, and the world clock leaks at 4% speed

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated, APK 2026.08.27.343878).
**Silo:** Combat / lifecycle
**Severity:** P0. The player is left in a town they cannot interact with. This is the "the game is
frozen" class of defect and it has a KNOWN PRIOR (2026-08-20) that the instrumentation names itself.
**Origin:** Owner felt-test, Seeker build `2026.08.26.342290`, 2026-08-26. Owner verbatim, on the
retreat button: ***"doesnt do anything"***. Evidence pulled from the device
(`tmp/f8pull/break-log.jsonl`, 735 entries).

---

## PROOF - captured, not theorised

**NINE `BATTLE_QUIESCENCE_FAIL` events.** The gate is `BattleQuiescenceGate`
(`Assets/_Modules/Core/Combat/BattleQuiescenceGate.cs`); the lock is
`DeNelle.Core.Combat.BattleLock`.

| kind | count | battle-lock still HELD | timeScale leaked |
|---|---|---|---|
| **arena win** | **8** | 7 | 2 |
| retreat | 1 | 1 | 0 |

Verbatim:

```
[Flow:Quiescence] BATTLE_QUIESCENCE_FAIL (arena win) - 1 invariant(s) NOT restored after the battle:
  - battle-lock: still HELD after the battle ended. Combat input stays suppressed and the HUD
    cannot return to its town context.

[Flow:Quiescence] BATTLE_QUIESCENCE_FAIL (arena win) - 2 invariant(s) NOT restored:
  - timeScale: the world clock is 0.04 (4% speed), not 1.00. The player will read this as frozen or
    unresponsive controls even though input is fine - this is the exact 2026-08-20 defect
    (a leaked hit-stop).
  - battle-lock: still HELD after the battle ended.
```

## ⭐ THE REPORT UNDERSTATES THE DEFECT - READ THIS BEFORE SCOPING

The owner reported ONE symptom: retreat does nothing. **Retreat is 1 of 9 events. The dominant case
is WINNING - 8 of 9.** A fix scoped to the retreat path would close the rarest instance and leave the
common one live. Whatever releases the lock must be proven on the **arena-win** path first.

The two failure modes are ALSO distinct and must not be conflated:
- **battle-lock still held (8x)** - `PanelManager` refuses to open town panels, hotkeys are gated,
  so taps genuinely do nothing. This is the owner's "doesnt do anything".
- **timeScale 0.04 (2x)** - a leaked hit-stop. Input works perfectly and the world crawls, which
  reads as a freeze. The instrumentation explicitly identifies this as **the same defect as
  2026-08-20** - so it was fixed once and has RETURNED, which means the previous fix addressed a
  path, not the cause.

## Required

1. **Instrument the release path before editing it (section 12).** The gate already tells you the
   invariant is unrestored; it does not tell you WHY. Trace who acquires the lock, every path that
   should release it, and which one is missed on an arena win. **Do not add a release call until a
   captured line names the path that skipped it.** A "plausible fix" here is how 2026-08-20 came back.
2. Release must be **structural, not per-exit-path**. Eight paths failing the same way says the
   release is hung off individual outcomes rather than off the battle's lifecycle end. If a battle
   session can end in N ways, N release calls is the bug, not the fix - the owner's WO-1108 "one
   owner, one lifecycle" principle applies exactly here.
3. **The timeScale leak is a SEPARATE fix** with its own owner. Whatever sets a hit-stop must restore
   it on an unwind path that runs even when the battle ends mid-stop. Do not merge the two fixes.
4. `BattleQuiescenceGate` already detects both correctly - **do not weaken, narrow or disable it.**
   It is the reason we know about this at all. It is currently only a REPORTER; consider (and
   recommend) whether it should self-heal as a last-resort backstop after reporting, since the
   current player experience is a dead town.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. `Assets/Editor/Regression/BattleQuiescenceRegression.cs` extended with cases driving **arena win,
   arena loss, retreat, and a battle ended during an active hit-stop**, asserting `BattleLock` is
   released and `timeScale == 1.0` after each. Prove RED first (WO-1138) and state how.
3. ⭐ **A device capture over a session with several arena battles showing ZERO
   `BATTLE_QUIESCENCE_FAIL` lines.** The absence of the message is the acceptance.
4. The RESULT quotes the trace line naming the path that skipped the release - not a description of
   the fix, the DATA that proved it.
5. Owner felt-verifies (win an arena, then retreat from one, then interact with the town) and CLOSES.

## What NOT to touch

- ⛔ `BattleQuiescenceGate`'s detection logic or its message text. It is correct and it is the only
  reason this was findable.
- ⛔ `BattleLock`'s probe registration contract (`RegisterProbe` / `UnregisterProbe`,
  `ATBCombatManager.cs:115-120`) without saying so explicitly - other systems read it.
- ⛔ Do not "fix" this by making `PanelManager` ignore the lock. That hides the defect and unblocks
  panels during REAL battles.

## Note for the lead

These nine events sat unread on the device because of WO-1227 (device captures never reach the
inbox). This ticket is the strongest single argument for that bridge: a P0 softlock class was
recorded, correctly diagnosed by our own instrumentation, and reached nobody for weeks.
## LANDED-WORK AUDIT (2026-08-26)

The unified battle-end release landed in `b303c4fbf`. Fresh evidence:
`Builds/batch0-compile-2.log:1966` `COMPILE_GATE_OK`;
`Builds/batch0-regression-2.log:83416` `BATTLE_QUIESCENCE_SUITE_OK`; and `:83814`
`REGRESSION_OK 291/291`. Load-bearing RED was banked in
`Builds/wo1233-red-proof.log` / `Builds/wo1233-red-proof-retry.log`: removing the single
`ClearPursuits` call failed arena win, arena loss, and retreat; the call was restored exactly once.
**Post-FIXED APK checklist:** a multi-battle device capture with zero `BATTLE_QUIESCENCE_FAIL`, the skipped-release
trace quoted in a RESULT, and the owner's win/retreat/town-interaction felt-close.
