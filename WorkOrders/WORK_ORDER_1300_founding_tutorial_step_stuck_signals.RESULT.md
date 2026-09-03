# WORK ORDER 1300 — RESULT

**Status:** FIXED (code + regression). **NOT CLOSED** — AC 5 requires the owner's felt-verify of the
founding FTUE end to end; a headless pass does not close an FTUE ticket (CLAUDE.md §13, PO closes).
**Silo:** Tutorial V2 only. No gate run, no commit, no `git add` — the lead owns both.

---

## What the data actually said, before any code was touched

The WO's two sub-cases were kept apart, as instructed. Neither was fixed on a theory.

### Geometry, measured — not assumed

`Main_Castle_Overworld.unity` carries **four BAKED** `WaveSpawnPoint-N/E/S/W` (which is also why
`CastleSpawnPointInjector` never injects here — it skips when any `WaveSpawnPoint` already exists,
`CastleSpawnPointInjector.cs:126-132`). Read out of the scene file:

| marker | world position |
|---|---|
| `WaveSpawnPoint-S` | `(-4.37, 0, -52.60)` |
| `WaveSpawnPoint-W` | `(-52.60, 0, 4.37)` |
| `WaveSpawnPoint-N` | `(4.37, 0, 52.60)` |
| `WaveSpawnPoint-E` | `(52.60, 0, -4.37)` |

Applying `TutorialWorldAnchors.GateAnchorPullbackMeters = 14f` to the S marker predicts a `guide_gate`
anchor at `(-3.21, ?, -38.65)`. The owner's own log records it at
`(-3.43, 0.08, -38.00)` — **the anchor maths is correct and the resolver is doing what it claims**:

```
Player-prev.log:49990  [Flow:Tutorial] WALK anchor 'guide_gate' resolved at (-3.43, 0.08, -38.00)
                       - nearest gate 'WaveSpawnPoint-S' pulled 14m toward the Heart
```

So the walk beat is not stuck on a wrong or unreachable target.

### Sub-case A (`founding_walk`) — the warp is already retired; the *diagnosis* was the gap

The seq 4376 harvest shows the hero moving at 12–14 m/s with `inputSuppressed=True`,
`scriptedMove=off`, `autoWalk=False` and
`ANIMATION-VELOCITY STALL … a mover other than this component wrote the transform` — i.e. the gate
warp the WO names. **`WORK_ORDER_1295` is live in the tree** (commit `62425d2d1`,
*"fix(gates): retire the gate warp; walk through all four castle openings"*), so the mechanism behind
that capture is gone, exactly as the WO predicted ("this half may already be fixed").

What is **not** fixed by WO-1295, and is fixed here, is that **the walk probe could not say which of
its preconditions had failed.** `TickProximityProbe` had two `return`s with no trace:

```csharp
if (_hero == null) { _hero = FindAnyObjectByType<HeroLocomotion>(); if (_hero == null) return; }
...
if (!TutorialWorldAnchors.TryResolveAnchor(anchorId, out Vector3 pos)) return;
```

and `TutorialWorldAnchors.LatchAnchor` returned `false` in complete silence while its only caller
(`TutorialFlow.EnterStep`) **discards the return value**. When either held, the beat emitted **zero**
`walk-probe` lines and then a bare `STEP-STUCK` naming the missing *signal* and never the missing
*precondition* — which is precisely why this ticket needed a second investigation. Confirmed against
the inbox: `walk-probe` appears in **no capture in `logs/f8-inbox/` at all**.

### Sub-case B (`founding_defend`) — a real, provable stuck path, no guessing required

The proving line is `TutorialFlow.RunScriptedTownWave`, which was:

```csharp
private async UniTaskVoid RunScriptedTownWave(WaveSpawnPoint gate)
{
    await UniTask.Yield();
    while (_townWaveArmed && CoreDialogue.DialogueService.IsRunning) await UniTask.Yield();
    if (!_townWaveArmed) return;
    await _tutorialWave.SpawnAt(gate, TownWaveCount);   // <-- unguarded
    _townWaveSpawnSettled = true;                       // <-- never reached on a throw
}
```

Fire-and-forget (`RunScriptedTownWave(gate).Forget()`, `TutorialFlow.cs:1920`) over an **entirely
unguarded await chain**, and `TickScriptedWave` refuses to poll until `_townWaveSpawnSettled`:

```csharp
private void TickScriptedWave()
{
    if (!_townWaveArmed || !_townWaveSpawnSettled) return;
```

`SpawnAt` awaits `WaveManager.GetEnemyCatalogAsync()` → `WaveDataLoader.LoadEnemiesAsync()`
(`WaveManager.cs:595-600`), an await that can fault. **If it faults, `_townWaveSpawnSettled` stays
false forever, `TickScriptedWave` never arms, and `wave.tutorial_band_repelled` — whose *only*
publisher in the entire tree is `TutorialFlow.cs:1960` inside that very method — is never raised.**
The step then burns the full 120 s and is rescued-as-SKIPPED, with the exception surfacing (if at all)
as an unobserved-task log carrying no tutorial context. That is a 1:1 match for seq 4370:
`builderOpenedThisStep=False`, 120 s charged, 1 s excluded — pure uninterrupted play against a fight
that never started.

The same shape applies to the dialogue-hold loop above it: the pre-fight line is an authored
**buffer**, but nothing bounded it, so a dialogue that never ends turned the buffer into a permanent
gate — silently.

Note this is **NOT** the `!Onboarded` / dead-`pausePressure` family (memory
`enemies-never-spawn-tutorial-onboarded-gate`). The scripted band deliberately **bypasses** those
peace gates (`TutorialWaveSpawner` spawns via `SpawnEnemyForExternalMode`), so the `!Onboarded` gate
is working as designed here and is not implicated. Checked before assuming novelty, as instructed.

---

## Publisher census (AC 4 input)

Every `TutorialSignals.Raise` site in the tree was enumerated. For the two stuck signals there is
**exactly one live publisher each**, so nothing was orphaned by a rename:

| signal | sole publisher |
|---|---|
| `hero.reached:*` | `TutorialFlow.TickProximityProbe` — `TutorialSignals.Raise(_awaitSignal)` |
| `wave.tutorial_band_repelled` | `TutorialFlow.TickScriptedWave` |

The defect was never a missing publisher; it was a publisher **that could never be reached**, and a
stall that could not describe itself.

---

## The fix

### 1. `Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs`

* **`RunScriptedTownWave`** — the whole await chain is now inside `try/catch`.
  * On a throw: `FlowTrace.Fail` names the exception type and message *in tutorial terms*, then
    `SettleScriptedWaveWithoutBand("the arm threw")` settles the clear poll. The beat completes down
    its normal path instead of stranding. **The signal is still raised only by `TickScriptedWave`** —
    nothing is raised from the catch, so the one-publisher invariant is preserved.
  * The dialogue hold now traces every 5 played seconds and **proceeds anyway** after
    `TownWaveDialogueHoldBoundSeconds = 30f` of played time, with a `FlowTrace.Warn` saying so. A
    buffer that becomes an infinite gate is the defect; 30 s sits far under the watchdog so the band
    still has time to be fought and repelled. **This is not a watchdog change.**
  * A missing `TutorialWaveSpawner` is now a `FlowTrace.Fail` + settle rather than a silent hang.
* **`SettleScriptedWaveWithoutBand` / `_townWaveForcedClear`** (new) — the forced settle reads as
  cleared in `TickScriptedWave`, so a band that could never spawn completes the beat. Reset on every
  fresh arm in `StartScriptedTownWave`, and consumed on use.
* **`TickProximityProbe`** — both early-return preconditions now emit a throttled `FlowTrace.Warn`
  on the *same played-time cadence* as the existing `walk-probe` trace, naming which one failed
  (`no HeroLocomotion` vs `anchor does not resolve`) with the hero position, played seconds and the
  bound. A future stuck walk beat names itself in one capture.

### 2. `Assets/_Modules/Village/Tutorial/TutorialWaveSpawner.cs`

* **`MarkClearedWithoutBand(reason)`** (new, public) — the explicit, **logged** spelling of the
  proceed-don't-wedge contract the class header already promises. Before this, that contract only held
  when `SpawnAt` *returned*; an arm that threw mid-await left `_spawnRequested == false` and
  `IsCleared == false` forever. It also runs `ClearCombatMusicIfDone()` so a half-armed band cannot
  strand the battle music / `BattleLock` probe.

### 3. `Assets/_Modules/Village/Tutorial/V2/TutorialWorldAnchors.cs`

* **`LatchAnchor`** — the unresolvable-anchor path is no longer silent. `FlowTrace.Once`, keyed per
  anchor id (so a later successful latch is unaffected and a hot loop cannot spam), states plainly
  that a `hero.reached` step on this anchor has nothing to walk to.

### 4. `Assets/Editor/Regression/TutorialCompletionPublisherRegression.cs` (new — AC 4)

Markers `TUTORIAL_COMPLETION_PUBLISHER_OK` / `_FAIL`; `Run(out reason)` is DataRegression-shaped.

1. `[publisher-exists]` — every **mandatory** (`ftue_v2`) step's completion signal, read from
   `tutorial-steps.json`, has a live runtime publisher under `Assets/_Modules` (editor/test raises do
   not count). A renamed signal or a moved publisher fails at the gate, not on the owner's phone.
2. `[publisher-unique]` — the two WO-1300 signals have **exactly one** raise site each, in the
   expected method. A second publisher is how an ambient clear could satisfy the scripted-band beat,
   which is the hole WO-1012 P3 split these ids to close.
3. `[signal-family]` — a completion-signal family authored with **no publisher rule in the suite**
   fails loudly. This is the anti-orphan clause: new beats cannot be silently unchecked.
4. `[stuck-reports]` — pins the WO-1300 instrumentation at source: two `FlowTrace.Warn` in the probe,
   `try/catch` + `SettleScriptedWaveWithoutBand` in the arm, a trace in `LatchAnchor`. Stripping any
   of them fails the gate (CLAUDE.md §12 — instrumentation is permanent).
5. `[forbidden-fixes]` — `WatchdogSeconds = 120f` is still 120f, `STEP-STUCK` still exists, and the
   `CompleteCurrentStep(skipped: true)` rescue path still exists. **The WO's own "do not touch" list,
   made mechanical.**

---

## Quality gate (per CLAUDE.md §1)

```
Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs                   BALANCED clean
Assets/_Modules/Village/Tutorial/V2/TutorialWorldAnchors.cs           BALANCED clean
Assets/_Modules/Village/Tutorial/TutorialWaveSpawner.cs               BALANCED clean
Assets/Editor/Regression/TutorialCompletionPublisherRegression.cs     BALANCED clean
```

(`clean` = no NUL bytes.) All added strings are ASCII. Line endings preserved per file (the two LF
files were LF at HEAD; no mixed endings introduced). All edits made with the Write/Edit tools on the
Windows path — no bash redirect touched a `.cs`.

---

## What was deliberately NOT touched

* The **120 s watchdog bound** and the WO-1036 played-and-charged clock — untouched, and now pinned
  by `[forbidden-fixes]`.
* The **SKIPPED-rescue path** and its grants — untouched, and pinned.
* `Assets/_Modules/Village/World/GateTraversalInjector.cs` — WO-1295, another seat owns it.
* Tutorial copy, step ordering, `ff.tutorialv2`, `tutorial-steps.json` — signal plumbing only.
* `WaveManager.cs` / `WaveDataLoader` — read to trace the await that can fault, **not edited**
  (shared wave lane).
* `DataRegression.cs` — lane-fenced; the one-line wiring is in the new suite's header for the
  committer.
* No `.meta` written for the new file — Unity generates it on import.

## Open, for the lead / PO

* **AC 1 is only half-dischargeable from this seat.** The static half is done and cited above; the
  *captured run* half needs a play session this edit-only seat may not start. Sub-case B's cause is
  now proven from source and closed; **sub-case A's runtime behaviour is still unproven** — but it can
  no longer go stuck without naming its own cause, so the next capture will settle it in one read
  rather than another investigation.
* `docs/MASTER_CATALOG/village-systems.md` has no TutorialFlow row to refresh; if the lead wants the
  new `MarkClearedWithoutBand` seam catalogued, that is a one-line add outside this silo.
