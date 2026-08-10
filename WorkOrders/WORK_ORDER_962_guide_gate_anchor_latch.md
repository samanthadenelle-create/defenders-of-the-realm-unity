# WO-962 — `guide_gate` must LATCH on step enter, not re-resolve to the nearest gate

**Status:** READY TO IMPLEMENT
**Date:** 2026-08-10 · **Priority:** HIGH (it hard-blocks the second beat of the FTUE)
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
