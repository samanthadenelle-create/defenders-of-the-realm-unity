# WORK ORDER 1300 — Founding tutorial STEP-STUCK: `founding_walk` and `founding_defend` completion signals never fire

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED - the scripted-band arm's unguarded fire-and-forget await chain now catches, reports and settles (founding_defend can no longer await a signal whose only publisher never ran), the walk probe's two silent early-returns now name themselves, and a new regression pins one live publisher per founding completion signal. Awaiting owner felt-verify to CLOSE (AC 5).
**Source:** F8 captures seq **4376** (`founding_walk`) and seq **4370** (`founding_defend`).
Ledger: `docs/qa/F8_TRIAGE_2026-09-02.md` §4.
**Silo:** Tutorial V2 (`Assets/_Modules/Village/Tutorial/V2/`)
**Severity:** P1 — the owner is felt-testing the founding flow right now, and two of its steps are
silently self-skipping.

## Owner-facing symptom

Two steps of the founding FTUE time out at 120 s of charged play, get rescued by the watchdog, and are
**recorded as SKIPPED with their narration suppressed**. The player is walked past two beats of the
opening story without being told, and without failing — she simply never sees the outro fiction for
either step. This happened in **two different sessions** on 2026-09-01.

## Captured proving lines (§12 evidence — quoted verbatim)

**Seq 4376** — `logs/f8-inbox/capture-20260902-013516-seq4376.md`, `scene=Main_Castle_Overworld`, `t=246.55215454101563`:
```
[Flow:Tutorial] STEP-STUCK :: founding_walk — no 'hero.reached:guide_gate' after 120s in-step
  (bound 120s, builder time excluded; ff.tutorialv2 on; builderOpenedThisStep=True, coachBeats=3);
  [WO-1036 clock: played-and-charged 120s, wall 232s, excluded (builder/frozen) 57s, discarded suspend gap 55s];
  RESCUED via watchdog and recorded as SKIPPED - the step was NOT completed, its outro is suppressed
  (no fiction narrated), grants still applied so the player is never half-granted.
```

**Seq 4370** — `logs/f8-inbox/capture-20260902-013512-seq4370.md`, `scene=Main_Castle_Overworld`, `t=385.2325439453125`:
```
[Flow:Tutorial] STEP-STUCK :: founding_defend — no 'wave.tutorial_band_repelled' after 120s in-step
  (bound 120s, builder time excluded; ff.tutorialv2 on; builderOpenedThisStep=False, coachBeats=3);
  [WO-1036 clock: played-and-charged 120s, wall 176s, excluded (builder/frozen) 1s, discarded suspend gap 55s];
  RESCUED via watchdog and recorded as SKIPPED - the step was NOT completed, its outro is suppressed
  (no fiction narrated), grants still applied so the player is never half-granted.
```

Both are emitted from:
```
DeNelle.Village.TutorialFlow:TickWatchdog () (at D:/EoA/Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs:2253)
```

## The two sub-cases are DIFFERENT and must be diagnosed separately

### A. `founding_walk` — awaits `hero.reached:guide_gate`. **Independent of the 2026-09-01 content outage.**

The awaited signal is a world-trigger / locomotion event with **no Addressables dependency**, so the
R2 catalog 404 (PROD-021) cannot explain it.

It shares a seam with the owner's own flag (**WO-1298**). In the flagged session the tutorial FocusMask
was pointing at the west gate while the hero warped past it:
```
[Flow:Tutorial] FocusMask resolved highlightId=world.gate_direction target=WaveSpawnPoint-W style=Glow rect=(962,254,120,120)
[Flow:Seam] WarpTo sample HIT @ (-34.00, 0.08, 0.00) …
[Flow:HeroOwner] … velRoot=14.49 … inputSuppressed=True … pos=(-39.50, 0.08, 0.00)
```
A hero **warped** past the gate anchor at 14 m/s can trivially skip a trigger volume. `WORK_ORDER_1295`
has since retired that warp and the owner felt-verified the gate on 2026-09-02, **so this half may
already be fixed.** Prove it before changing anything.

### B. `founding_defend` — awaits `wave.tutorial_band_repelled`. **CAUSE NOT DETERMINED — DO NOT GUESS.**

The R2 outage cost the enemies their *art*, not their existence: `EnemyContentWarmer.cs:346` states in
its own failure message that this is *"a visual defect only; the game did NOT stall, which is the
point"* — enemies should still have spawned as tinted capsules and been repellable. But **there is no
per-capture spawn evidence for this session.** The daemon drained a three-session backlog in one burst
at 01:35, so every capture in the band carries the *same* end-of-session `Player.log` tail; its
`enemies=0 / wave=False` reading is from after the step was already skipped and proves nothing about
`t ≈ 265–385`.

**Instrument first (CLAUDE.md §12).** Do not open a fix on the wave path on a theory.

## Acceptance criteria

1. **Instrumentation before edit, for both sub-cases.** Produce a captured run of the founding FTUE
   on current HEAD (with the R2 content now pushed, so the outage is off the table) and read:
   - **A:** does `hero.reached:guide_gate` fire when the hero walks to the gate? Trace the publish
     site and the trigger volume, not just the consumer. If it fires, close sub-case A citing WO-1295.
   - **B:** does the tutorial band **spawn** at all? Trace `wave.tutorial_band_repelled`'s publisher
     back through `WaveManager` to the spawn, and record whether the band spawned, whether it was
     repelled, and whether the signal was published but not received. Name which of those three it is,
     with the log line.
2. Any signal that is published but not received, or received on the wrong step, is fixed at the
   plumbing — not by lengthening the 120 s bound. **Raising the watchdog timeout is not a fix and is
   explicitly out of scope.**
3. Every new trace stays in the code permanently (CLAUDE.md §12 — no stripping).
4. A regression under `Assets/Editor/Regression/` asserts that each founding step's completion signal
   has exactly one live publisher, so a rename cannot silently orphan it again.
5. Owner felt-verifies the founding FTUE end to end and closes (§13). A headless pass does not close
   an FTUE ticket.

## What NOT to touch

- ⛔ **Do not raise, remove, or make configurable the 120 s watchdog bound**, and do not touch the
  WO-1036 played-and-charged clock accounting. The watchdog did its job — it caught this. Blunting the
  detector is the failure mode CLAUDE.md §12 exists to prevent.
- ⛔ **Do not remove the SKIPPED-rescue path.** "Grants still applied so the player is never
  half-granted" is deliberate and stays.
- ⛔ `Assets/_Modules/Village/World/GateTraversalInjector.cs` — WO-1295 is live in the working tree and
  another seat owns that file.
- ⛔ Do not re-author tutorial copy, step ordering, or `ff.tutorialv2`. This is signal plumbing only.
- ⛔ Do not merge the two sub-cases into one fix until step 1 proves they share a cause.
