# WO-1308 RESULT — Retreat leaves the battle-lock held; the enemy sits in fight

**Status:** FIXED (edit-only; NOT gated, NOT committed — the lead gates and commits)
**Date:** 2026-09-02
**Silo:** Combat / Quiescence

---

## The proving line worked from

`logs/f8-inbox/capture-20260902-034547-seq4664.md`, scene `Main_Castle_Overworld`, owner felt-test:

```
[Flow:Quiescence] battle-lock STILL HELD after the self-heal (retreat):
  [WaveManager.<OnEnable>b__106_0] (was [PursuitBattleProbe.Probe, WaveManager.<OnEnable>b__106_0]).
  A holder that survives a full session release is either a LIVE chase re-pulsing every aggro tick,
  or an owner whose probe is latched true with no battle behind it.
```

Read exactly: `BattleSessionEnd.Release` ran and **`PursuitBattleProbe` released**; the **wave probe
did not**. So this is *not* the WO-1233 pursuit-window leak and *not* a misreporting probe —
`_phase` really was `Active`, and it stayed that way for the rest of the session.

The `88e72ea8d` instrumentation was **not stripped and not weakened** — the `wave-phase`
`QuiescenceProbe`, `SetPhase`, `_lastSwitchFrame` and `DescribeLatchedWavePhase` are all intact and
now sit next to the repair they diagnosed. (Note for the record: seq 4663-4665 predate that commit by
~5h, so no capture yet carries the new dump; the fix is therefore built to *self-heal and keep
reporting*, which is what makes the next capture cheap either way.)

## Root cause (2 sentences)

WO-1233 established that every owner of a global unwinds its own state at the ONE battle-session end;
`WaveManager` owns a global — the battle-lock claim it raises through `_phase == WavePhase.Active` —
and was the one such owner that had **never registered an unwind**, so a retreat announced the
session end and nobody ever asked the wave loop whether its claim was still true. `Active` has
exactly one routine exit (`TickActiveWave -> CompleteWave`), reachable only from the `switch` at the
bottom of `Update` behind two early returns, so once the loop stopped reaching that tick the phase —
and the lock, and the "wolf sitting in fight" — was permanent.

This is the **same family** as the August arena instance: a release seam that existed, and an owner
that never reached for it. There it was `PostureSignals.ClearPursuits` with one scene-load caller;
here it is `BattleSessionEnd.RegisterUnwind` with no wave-loop caller.

## What changed

### `Assets/_Modules/Village/Waves/WaveManager.cs`

1. **The fix — a battle-end unwind that drives the loop's own tick.** `OnEnable` now also calls
   `RegisterWavePhaseSessionUnwind()`, registering `"WaveManager.phase"` with `BattleSessionEnd`
   (static delegate, name-keyed → no duplicates, never stale). At every session end
   `ReconcileLatchedWavePhaseOnSessionEnd` finds the manager satisfying the **exact lock-probe
   predicate**, reports its full state, and drives **ONE `TickActiveWave()`**.
   - It **re-decides nothing.** The clear rule stays in `TickActiveWave` alone — a second copy of
     "is this wave over" is a second answer waiting to disagree. One tick either completes the wave
     through `CompleteWave` exactly as the loop would have, or declines because enemies are alive
     exactly as the loop would have. **A genuine siege is preserved for free.**
   - It **declines while `TownSuspension.SuspendedFor(this)`** — that freeze is deliberate, owned by
     `TownSuspension`, and the RCA rated it self-clearing. Driving a tick there would cancel a wave
     the player is away from.
2. **`OnDisable` stands the phase down with the roster it clears** (`"OnDisable/roster-cleared"`).
   It already unsubscribed and dropped every live enemy, zeroed the held count and released the apex
   boss handle — but left `_phase` describing a wave whose bodies it had just deleted, so `OnEnable`
   re-registered the lock probe against that claim. RCA shape (b), closed at source.
3. **The dropped-async wedge self-heals** (RCA shape 1a). `_heldSmartReinforcements` is owned
   entirely by `DrainSmartReinforcements(...).Forget()`; an exception inside it **cannot** zero the
   counter, and from that frame `TickActiveWave`'s clear gate returns early forever. Added a
   heartbeat `_reinforcementDrainUnscaled`, stamped in the drain's loop body **and from inside its
   cap-wait predicate** (polled every frame while alive) — so an arbitrarily long legitimate wait
   keeps stamping and only a *dead task* can go stale. `held > 0` with a heartbeat older than
   `ReinforcementDrainStaleSeconds` (20 s) now logs `FlowTrace.Fail` naming the orphaned drain and
   releases the gate. **Reports honestly, then heals** — it does not silence the finding.

### `Assets/Editor/Regression/BattleQuiescenceRegression.cs` — the oracle

- **`RetreatReleasesEveryLockHolder`** (behavioural, both directions): a latched lock holder that
  registers **no** unwind must **survive** `BattleSessionEnd.Release` — that is the captured defect,
  and asserting it is what stops the test passing for the wrong reason; the **same** holder, once it
  registers an unwind, is released by the same call. Nothing force-clears `BattleLock`.
- **`WaveLoopUnwindsItsOwnPhase`** (source-lint on the real holder — a live wave loop cannot be
  driven inside a synchronous editor batch; same discipline as the existing session-wiring group).
  Four rules: the unwind is registered; it drives `TickActiveWave` rather than re-deciding; the
  **probe predicate is unchanged** (`Instance == this && _phase == WavePhase.Active` — narrowing it
  is the forbidden "fix"); `OnDisable` stands the phase down.

## Oracle mutation — proven RED

Unity was **not** run (out of scope for this seat). Instead the lint's `ReadCode` + all four rules
were replicated byte-identically in Python and run against real mutants:

| Subject | registers-unwind | drives-own-tick | probe-intact | phase-down-on-disable | Verdict |
|---|---|---|---|---|---|
| **`HEAD:WaveManager.cs`** (pre-fix — the shipped defect) | FAIL | FAIL | PASS | FAIL | **RED (3)** |
| **Probe weakened** to `Instance == this && false` (the forbidden fix) | PASS | PASS | **FAIL** | PASS | **RED (1)** |
| Working tree (fixed) | PASS | PASS | PASS | PASS | **GREEN** |

Every rule is discriminating: the pre-fix file fails three, and the one rule it passes is proven by
the second mutant. The behavioural half is self-mutating by construction — its branch (a) *asserts*
the un-unwound holder survives, so a `Release` that force-cleared the lock would fail it.
⚠ For the lead: the behavioural half has not been **executed**; it needs one
`DeNelle.Editor.BattleQuiescenceRegression.RunAll` on the gate run.

## Brace / NUL check

| File | Result |
|---|---|
| `Assets/_Modules/Village/Waves/WaveManager.cs` | BALANCED, clean |
| `Assets/Editor/Regression/BattleQuiescenceRegression.cs` | BALANCED, clean |

## Acceptance criteria

1. **Retreat from an overworld encounter releases the lock** — the unwind drives the tick that
   completes an empty wave. ✔ (needs the owner's felt-test to close)
2. **A retreat during a GENUINE wave still holds the lock** — the forced tick declines with live
   enemies, and says so in the trace. ✔ Both directions asserted by the oracle.
3. `BATTLE_QUIESCENCE_FAIL (retreat)` no longer fires — **needs a captured run**; PO closes.
4. **The self-heal path still runs and still reports honestly** — nothing was silenced; three new
   `FlowTrace` lines were added, and every existing WO-1308 / WO-1233b trace is untouched. ✔

## Deliberately NOT touched

- ⛔ `WorldHold.cs`, `WaveCelebrationManager.cs`, `CombatFeedbackManager.cs`, `HeroHitReaction.cs` —
  the concurrent `timeScale` lane (owner flag 4656). No clock was written.
- ⛔ `BattleQuiescenceGate` reporting — not weakened, not re-thresholded, **not edited at all**.
- ⛔ No second lock, probe registry or recovery ladder. No new asmdef reference.
- ⛔ The wave battle-lock probe is **unchanged**, and it is never unregistered as a workaround.
- ⛔ `OverworldEncounterSpawner` — it has zero `WaveManager` references and was not the writer.
- ⛔ Hero locomotion, tutorial, audio, inventory, talents, enemy-asset files — other lanes.
- The RCA's Q4 two-manager shape and the heart-gated stuck-enemy cull (shape 1d) are **left as-is**:
  the unwind now covers both symptomatically (the forced tick runs the cull when a heart exists, and
  the lock is released when the field is genuinely empty), but neither was proven live by a capture
  and §12 forbids editing on a theory. The `wave-phase` probe prints both discriminators.

## Also updated

- `docs/MASTER_CATALOG/village-enemies-world.md` — the `WaveManager.cs` entry now records the
  three-registration `OnEnable`, the unwind contract, and the two closed latch sources (CLAUDE.md
  §15: canon moves with the change).
