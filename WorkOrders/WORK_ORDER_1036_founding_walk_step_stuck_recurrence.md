# WORK ORDER 1036 — `founding_walk` STEP-STUCK recurs AFTER WO-962 shipped: a 245s dead wait, then a silently skipped beat

> ⚠ **Title corrected 2026-08-17.** It read *"the FTUE hard-blocks"* — that was written from a
> TRUNCATED capture and is **FALSE**: a watchdog rescues the step (§1b). Left visible rather than
> silently reworded, because the wrong title is what a skimming seat would have scheduled off.

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1036 → 1037 in the same edit
**Lane:** Tutorial V2 / guide gate. ⚠ Interacts with WO-1031's guide despawn — see §4.
**Priority:** **MEDIUM** (was HIGH — corrected 2026-08-17, see §1b: a watchdog RESCUES the step, so
this is a ~245s dead wait + a silently skipped story beat, **not** a stranding block).
**Provenance:** F8 **seq=2433** (2026-08-16, 125s) and **seq=2343** (2026-08-15, 241s).

---

## 1. The captured signal, twice

```
[Flow:Tutorial] STEP-STUCK :: founding_walk — no 'hero.reached:guide_gate' after 125s in-step
[Flow:Tutorial] STEP-STUCK :: founding_walk — no 'hero.reached:guide_gate' after 241s in-step
```

Same step, same missing event, two separate sessions a day apart. **Not a one-off.**

## 1b. ★ FULLER CAPTURE 2026-08-17 (seq 2513) — corrects this WO's severity, and adds a finding

The earlier captures were **truncated**. The full line:

```
no 'hero.reached:guide_gate' after 245s in-step (bound 120s, builder time excluded; ff.tutorialv2 on;
builderOpenedThisStep=False, coachBeats=2); RESCUED via watchdog and recorded as SKIPPED - the step was
NOT completed, its outro is suppressed (no fiction narrated), grants still applied so the player is
never half-granted.
```

### ⚠ CORRECTION — this is NOT a hard block. My HIGH severity was overstated.

**A watchdog rescues the step.** `TutorialFlow.TickWatchdog:1797` marks it **SKIPPED**, suppresses the
outro so no fiction is narrated over a step that did not happen, and **still applies the grants so the
player is never half-granted.** That is careful, deliberate design and it is working.

**Re-rate:** not a hard block — a **245-second dead wait followed by a silent skip**. Still bad, and
still worth fixing: the player stands around for four minutes, then quietly loses a story beat of the
FTUE. But it does not strand them, and this WO's header claim that it *"hard-blocks the FTUE"* was
based on the truncated text. ⚠ **Do not schedule this as a P0 stranding bug.**

⚠ **It also changes the WO-1031 interaction (§4):** a guide despawn firing mid-step would still be
wrong, but the watchdog would rescue it too. Bad, not catastrophic.

### ⚠ THE "2× BOUND" FINDING BELOW IS **DISPROVEN** — corrected 2026-08-17 from the capture itself

**The bound is NOT doubled, and it IS honoured — on every step.** Left visible rather than reworded,
because "the bound is systematically 2×" is exactly the theory a seat would have refactored against.

The proof was already sitting in the harvested context of `capture-20260817-092752-seq2513.md`, four
lines below the STEP-STUCK it explains:

```
[Flow:Tutorial] coach :: step 'founding_walk' idle 245s awaiting 'hero.reached:guide_gate'
                with the builder never opened - re-stated the objective (beat 2/4).
[Flow:Tutorial] STEP-STUCK :: founding_walk — ... after 245s in-step (bound 120s ... coachBeats=2)
[Flow:Offline]  Claim #6 (resume): resume window -- counting from the background edge
[Flow:Offline]  Claim #6 (resume): ONE delta = 196s (0.05h) ...
```

Coach beat 2 is due at **90s** and fired at **245s**; only 2 of 4 beats had been spent in what the wall
clock called four minutes. **Two independent wall-clock timers were late by the same ~196s** — that is a
stopped frame loop plus a resume jump, never a doubled bound. `45s (beat 1) + 196s (background) = 241s`,
which is the 2026-08-15 capture **to the second**.

**Real cause:** `Time.unscaledTime` is not clamped by `Time.maximumDeltaTime`, so the first frame after
the OS restores a backgrounded app carries the **whole suspend window as one `unscaledDeltaTime`**. The
watchdog held a wall-clock stamp (`_watchdogAt`) and charged all of it to the step. The player had **~49s
of played time** on the beat and it was rescued-and-SKIPPED on the resume frame, before they could move.
Compounding it: `PauseController.OnApplicationPause(true)` auto-pauses to `timeScale 0` and **never
auto-resumes**, so the rescue fired while the hero was frozen (`[Flow:HeroOwner] WORLD CLOCK FROZEN`,
seq 2343 — §3's cheap check was right to flag it).

**Counter-example proving the bound is fine:** seq 2433 fired at **125s** with `builderOpenedThisStep=True,
coachBeats=1` — a normal foreground session, bound honoured, builder time correctly excluded.

**Fix:** `TutorialFlow.StepClock` — the bound is spent in **PLAYED FRAMES** (per-frame
`unscaledDeltaTime` clamped to 1s), excluding builder-open and `timeScale<=0` frames; a suspend jump
contributes one clamped frame and is TRACED. The coach cadence rides the same budget. Bound unchanged at
120f (WO-962 §3). Oracle: `TutorialWatchdogBoundRegression` [`tutorial-watchdog-bound`].

### ~~★ NEW FINDING — the watchdog fired at 245s against a stated bound of 120s~~ (DISPROVEN, see above)

`bound 120s` and `builderOpenedThisStep=False`, so **there was no builder time to exclude** — yet the
rescue landed at **245s, roughly 2x the bound**. The two earlier captures (241s, 125s) fit the same
picture: one near the bound, two near double it.

**Investigate as part of this ticket:**

- Is the watchdog's clock counting something other than what `bound` describes?
- Is `TickWatchdog` gated behind a condition that delays its first evaluation?
- Or does `in-step` time start earlier than the watchdog's own timer?

⚠ **This matters beyond one step.** If the bound is systematically ~2x its stated value, **every**
watchdog-protected tutorial step strands the player for twice as long as designed — a global FTUE
patience cost hiding behind a single symptom. Check whether the bound is honoured on other steps.

**Keep the diagnostic fields.** `bound` / `builderOpenedThisStep` / `coachBeats` in that line are what
made this analysis possible; the truncated captures were unactionable by comparison. §12 — this is
instrumentation earning its keep.

## 2. ⚠ WHY THIS IS NOT WO-962 — check before re-treading it

`WORK_ORDER_962_guide_gate_anchor_latch.md` is **DONE**, landed in `e2759f1e9`
(*"guide_gate LATCHES on step enter instead of chasing the nearest gate"*), and it was filed against
this exact symptom: *"it hard-blocks the second beat of the FTUE."*

**The symptom is back with the fix in.** So one of:

1. the latch **regressed** (something re-resolves or clears the anchor after step enter), or
2. `hero.reached:guide_gate` **never fires** even with a correct anchor — a radius/trigger/event
   problem downstream of the anchor, which WO-962 did not touch, or
3. the **hero never arrives** — a locomotion or navmesh issue, so the event is correctly not firing

⚠ **Do not re-implement WO-962.** Read it first, confirm the latch still holds at runtime, and then
look *downstream*. Re-fixing a landed fix is how the 08-08 dungeon-stair hunt burned four rounds.

⚠ **Board note:** WO-962's `Status:` line *"was never flipped in that commit, so the derived board kept
re-serving this ticket as READY."* Do not read its board state as evidence of anything.

## 3. STEP 1 — INSTRUMENT the three-way split (CLAUDE.md §12)

The `STEP-STUCK` line proves the event is absent. It does **not** say which of §2's three causes it is,
and they need opposite fixes. Split it with data before editing:

1. **Is the anchor latched and stable?** Log `guide_gate` anchor id + world position at step enter and
   again at the stall. If it moved or cleared → cause (1).
2. **Where is the hero?** Log hero position + distance-to-anchor over the stall window. Distance
   shrinking to ~0 with no event → cause (2), the trigger. Distance never shrinking → cause (3).
3. **Is the hero able to move at all?** ⚠ A `[Flow:HeroOwner]` line in the 2026-08-15 harvest reported
   **`timeScale=0.00`** with *"WORLD CLOCK FROZEN … The hero CANNOT move, turn or animate while this
   holds."* If a frozen clock is in play during the stall, that is cause (3) and the real bug is
   whatever failed to restore `timeScale`. **Check this first — it is cheap and it would explain both
   captures.**

## 4. ⚠ COORDINATE WITH WO-1031 — the despawn can turn this into a permanent block

WO-1031 §4 (owner ruling 2026-08-16) despawns the guide wolf when the tutorial ends **or a defensive
structure is placed**. If the despawn can fire while `founding_walk` is still waiting, the hero is asked
to follow a guide that no longer exists — making the hero follow a guide that no longer exists.
⚠ **Severity corrected per §1b:** the watchdog would rescue this too, so it is a worse dead wait and a
lost beat — **not** the deterministic hard block this section originally claimed.

**Whichever ticket lands second must verify the other.** Required either way: the guide survives until
`hero.reached:guide_gate` fires.

## 5. Acceptance criteria

- [ ] The cause is **named from captured data**, one of §2's three, and recorded in the RESULT
- [ ] `founding_walk` completes reliably — **10 consecutive headless FTUE runs, zero STEP-STUCK**
      (an intermittent needs repetition, not one green run)
- [ ] If the cause was a frozen `timeScale`, the **restore path** is fixed and instrumented, not
      worked around
- [ ] WO-962's latch verified still holding at runtime — and **not** re-implemented
- [ ] Guide survives until `hero.reached:guide_gate` (§4)
- [ ] The `STEP-STUCK` trace is **kept** — §12; it is what caught this twice

## 6. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. **Headless FTUE fleet ×10** — the oracle is the absence of `STEP-STUCK` across all runs
3. Owner felt-verifies the FTUE end-to-end + closes (§13)

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `TutorialFlow.cs:345,595,413` — watchdog budget landed. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
