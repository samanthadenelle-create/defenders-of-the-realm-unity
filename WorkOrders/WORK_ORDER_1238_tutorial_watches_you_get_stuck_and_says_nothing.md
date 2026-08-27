# WORK ORDER 1238 - The tutorial watched the player get stuck for 120s and never said a word

**Status:** FIXED 2026-08-27 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 303/303 suites` (Builds/w3-c, Builds/w3-r). AWAITING OWNER FELT-VERIFY to close.
**Silo:** Onboarding / Tutorial
**Severity:** P2 for the player, P1 for FTUE conversion. The watchdog contains the damage; the
teaching failure is what costs the install.
**Origin:** Device capture seq 3610 via the WO-1227 bridge, 2026-08-26, `Main_Castle_Overworld`.

---

## PROOF - captured, not theorised

```
[Flow:Tutorial] STEP-STUCK :: founding_walk - no 'hero.reached:guide_gate' after 120s in-step
(bound 120s, builder time excluded; ff.tutorialv2 on; builderOpenedThisStep=True, coachBeats=0);
[WO-1036 clock: played-and-charged 120s, wall 153s, excluded (builder/frozen) 33s,
discarded suspend gap 0s]; RESCUED via watchdog and recorded as SKIPPED - the step was NOT
completed, its outro is suppressed (no fiction narrated), grants still applied so the player is
never half-granted.
```
Stack: `TutorialFlow.TickWatchdog()` <- `TutorialFlow.Update()`.

Screenshot candidate: `logs/f8-inbox/device/SM02G4061955851/break_00_error.png` (14:22:18).
**Open it before designing anything** - it shows what the player could actually see.

## What is CORRECT here, and must not be "fixed"

The watchdog behaved exactly as designed and its own message proves it:
- the step is recorded **SKIPPED**, not completed;
- the outro is **suppressed**, so no fiction is narrated for a beat that did not happen;
- **grants still applied**, so the player is never half-granted;
- the WO-1036 clock **excluded 33s of builder/frozen time** - the 120s bound was charged against
  played time only, which is why wall was 153s.

⛔ Do NOT weaken, lengthen, or disable the watchdog. It is the reason this is a logged annoyance
instead of a stuck player. This ticket is about the 120 seconds BEFORE it fired.

## ⭐ THE DEFECT: `coachBeats=0`

**The player was stuck for the entire step and the tutorial issued ZERO coaching prompts.** A
tutorial that can detect "no progress for 120s" well enough to rescue the step can detect it well
enough to HELP - and it did not open its mouth once.

`builderOpenedThisStep=True` is the second half: the player opened the BUILD menu during a
"walk to the gate" step. That is a player telling you, in behaviour, that they do not know what the
step wants. Nothing redirected them.

## Required

1. **Escalating coaching before the watchdog fires.** The bound is 120s of played time; a first nudge
   belongs far earlier. Instrument to find the real distribution before picking numbers - do NOT
   guess a cadence (section 12). Report what the data says about how long a succeeding player takes.
2. **Make the objective findable, not just stated.** `hero.reached:guide_gate` is a position trigger;
   if the gate is off-screen or unmarked, "walk to the gate" is a fair thing to fail. Confirm from
   the screenshot whether the target was visible, and say so.
3. **React to the builder-open signal.** Opening an unrelated menu mid-step is a strong
   confusion tell that is already being RECORDED (`builderOpenedThisStep`) and not USED. Cheapest
   real win in this ticket.
4. **Report how often this fires.** Re-run over `logs/f8-inbox/DEVICE_BACKFILL_2026-08-26.md`
   (736 entries) and count STEP-STUCK events by step id. If `founding_walk` dominates, that one step
   is the problem; if it is spread, the coaching gap is systemic. **State the number.**

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. A regression asserting a coach beat fires BEFORE the watchdog bound on a non-progressing step,
   and that the watchdog's skip/suppress/grant contract is unchanged. Prove RED first (WO-1138) -
   `coachBeats=0` is red today by capture.
3. ASCII-only strings; no meaning by colour alone (the owner is red/green colourblind).
4. Owner felt-verifies a fresh FTUE and CLOSES.

## What NOT to touch

- The watchdog's rescue contract (skip, suppress outro, still grant) or the WO-1036 played-time
  clock. Both are correct and both are why this was survivable.
- The 120s bound, until the coaching data says what it should be.
- `ff.tutorialv2`.
