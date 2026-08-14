# WO-962 — `guide_gate` must LATCH on step enter, not re-resolve to the nearest gate

**Status:** DONE
**Date:** 2026-08-10 · **Priority:** HIGH (it hard-blocks the second beat of the FTUE)

> **LANDED in commit `e2759f1e9`** — *"fix(tutorial): WO-962 - guide_gate LATCHES on step enter
> instead of chasing the nearest gate"*. The Status line above was never flipped in that commit,
> so the derived board (`tools/board_build.py`) kept re-serving this ticket as READY and it was
> re-routed to an implementation agent on 2026-08-14; that agent found the fix already present
> at HEAD and flipped the line rather than re-implementing it. Verified at source, not inferred:
> - `Assets/_Modules/Village/Tutorial/V2/TutorialWorldAnchors.cs` — `LatchAnchor` / `ClearLatch` /
>   `IsLatched`; `TryResolveAnchor` reads the latch before any live resolve; `TraceDivergenceOnce`
>   records the would-have-moved answer and never writes it back; the `world.gate_direction`
>   resolver points at the LATCHED gate transform while a latch is held.
> - `Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs` — `EnterStep` latches a
>   `hero.reached:<anchor>` step's anchor (L588-592); `TickProximityProbe` re-calls `LatchAnchor`
>   idempotently for a late-resolving anchor (L1718-1724); `ClearLatch` on teardown (L473),
>   `CompleteCurrentStep` (L1238) and `FinishFlow` (L1271). `ReachedRadius` is still `6f` and
>   `WatchdogSeconds` still `120f` — the two forbidden "fixes" of §3 were not taken.
> - `Assets/Editor/Regression/TutorialAnchorLatchRegression.cs` — acceptance 4, replaying the
>   F8 seq 2301 south→east→north resolver through `LiveResolverOverride`; registered in
>   `DataRegression.RunAll` (`[tutorial-anchor-latch]`).
>
> Acceptance 2 and 3 (the hero physically reaches the latched gate in a clean FTUE run) are
> felt/AutoPilot verification and remain the PO's to close.
**Block:** main line (CLI) · **Lane:** Tutorial / world anchors
**Source:** owner F8 seq 2301, 2026-08-10 16:04, `Main_Castle_Overworld`

## §1 The proving lines (captured, not inferred)

From the owner's own `Player.log`, in order, inside one `founding_walk` step:

```
[Flow:Tutorial] STEP-ENTER :: founding_walk (order=15, completes on 'hero.reached:guide_gate').
[Flow:Tutorial] WALK anchor 'guide_gate' resolved at (-3.43, 0.08, -38.63) - nearest gate
                'WaveSpawnPoint-S' pulled 14m toward the Heart (inside the walls, never the spawn ring).
[Flow:Pets] guide-lead SET -> (-3.43, 0.08, -38.63)
[Flow:Pets] guide-lead SET -> (37.29, 0.08, -0.21)
[Flow:Pets] guide-lead SET -> (3.07, 0.08, 38.68)
[Flow:Tutorial] coach :: step 'founding_walk' idle 123s awaiting 'hero.reached:guide_gate' ...
[BREAK] error: [Flow:Tutorial] STEP-STUCK :: founding_walk - no 'hero.reached:guide_gate' after 123s
                ... RESCUED via watchdog and recorded as SKIPPED
```

Three different gates in one step: **south** (-3.4, -38.6), then **east** (37.3, -0.2), then **north**
(3.1, 38.7). The player walked toward the first; the target moved to another side of the town; repeat.
`hero.reached:guide_gate` was therefore not reachable by walking, and the watchdog SKIPPED the beat.

## §2 Root cause

`TutorialWorldAnchors` registers `guide_gate` / `world.gate_direction` as **live resolvers**
(`ResolveNearestGate()`), and `TutorialFlow.TickProximityProbe` re-asserts the lead **every frame**
(documented in `tutorial-steps.json`'s own beat-2 note). "Nearest" is measured from the HERO, so every
step the player takes toward the south gate can make a different gate nearer, and the anchor follows.

This is the anchor equivalent of a moving goalpost: the guidance is self-defeating precisely because the
player obeyed it.

## §3 The fix

**Resolve ONCE on step ENTER; latch for the life of the step.**

- On `STEP-ENTER` of any step whose completion is `hero.reached:<anchor>`, resolve the anchor and cache
  the resolved WORLD POSITION on the step instance.
- The proximity probe, the guide lead (`PetHeroLeash.SetLeadTarget`) and the `world.gate_direction`
  highlight all read that latched position. The resolver stays live for anything else.
- Clear the latch on step exit / flow reset, so a re-entered step re-resolves once.
- Trace the latch on enter (`anchor 'guide_gate' LATCHED at <pos> (gate '<name>')`) and, if the live
  resolver would now answer differently, `FlowTrace.Step` that divergence once rather than acting on it —
  that line is the regression's evidence.

**Do NOT** fix this by widening the reach radius or by shortening the watchdog. Both hide it.

## §4 Acceptance criteria

1. Within one step, `guide-lead SET` fires with **one** target (re-asserts are allowed; a CHANGED target
   is a failure).
2. Walking to the latched gate raises `hero.reached:guide_gate` and completes `founding_walk`.
3. No `STEP-STUCK :: founding_walk` in a clean FTUE run.
4. A regression asserting the latch: with a moving probe position, the resolved anchor for an active
   `hero.reached:*` step does not change; and it DOES re-resolve after a step exit/re-enter.
5. The pull-back rule is untouched — the anchor stays ~14 m inside the walls, never the spawn ring
   (owner F8 2026-07-08).

## §5 What NOT to touch

The watchdog/rescue semantics (they behaved correctly and are what surfaced this), the gate pull-back
distance, `PetHeroLeash`'s natural-exploration behaviour outside a lead, and WO-961's guide-body work —
this ticket is the anchor only and must land independently of whether a body is visible.
