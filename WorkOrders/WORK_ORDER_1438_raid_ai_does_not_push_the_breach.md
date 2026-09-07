# WO-1438: raid AI keeps hitting walls after the gate falls, and barely fights

**Status:** IMPLEMENTED - gated 2026-09-06 (COMPILE_GATE_OK cg-quiet.log; regression 417/419, both reds in other lanes); not yet on the Seeker; navmesh hole (`holeNavmesh=`) still unmeasured
**Silo:** raid combat AI / target selection. Disjoint from WO-1436 (HUD) and WO-1437 (session lifecycle).
**Source:** owner felt-test 2026-09-06 on build **2026.09.06.358161**, two messages:
> *"when the AI in raid destroys a gate, they should push in not keep attacking adjoining walls"*
> *"the AI didnt really fight either"*

---

## 1. THE TWO COMPLAINTS ARE PROBABLY ONE DEFECT

**A breach is not being recognised as a route.** If the attackers' target selection scores every wall
segment the same way it scores a gate, then destroying the gate changes nothing - the nearest
still-standing barrier is simply attacked next. To the player that reads as *both* "they won't push in"
**and** "they aren't really fighting", because the squad spends the raid hitting masonry instead of
closing with defenders.

⚠ **That is a HYPOTHESIS from the owner's description, not a finding.** It is written down so the lane
has a flow-first frame, and it must be **proven or killed with captured data before any edit**
(CLAUDE.md section 12: static reading LOCATES, it never CONCLUDES).

## 2. INSTRUMENT FIRST - this is the gate, not a suggestion

There is **no `[Flow:` trace for raid squad target selection** in the two device logs captured this
session (`logs/debug/raid-ai-and-pets-2026-09-06.log`, 18 MB;
`logs/debug/raid-stuck-2026-09-06.log`, 9 MB). Searching them for troop/squad AI returns only
`[Flow:Army] TroopRecoveryService: recovery advanced` - a **town** service. **The AI is currently
invisible, which is why this ticket cannot be answered by reading code.**

**First deliverable: instrumentation, before any behaviour change.** Per unit, per retarget:
what it chose, the candidates it scored, and why the winner won. Include an explicit line when a
barrier's destruction opens a path, and whether anything re-evaluated as a result. Then run it and read
it. `FlowTrace.Throttle` for the hot loop; instrumentation is **permanent** (never stripped).

## 3. WHAT TO ESTABLISH, IN ORDER

1. Is there a pathing concept of "the wall is a barrier on my route", or are structures just targets?
2. When a gate dies, does anything invalidate cached paths / re-run target selection for units that
   were attacking it or its neighbours?
3. Are attackers preferring structures over live defenders? If so, is that authored or emergent?
4. Do the owner's own deployed troops (`Troops 8/10` on screen, `Rally ON`) share this selector, or a
   different one? **Her "AI didn't really fight" may be about HER troops, HIS defenders, or both** - the
   trace must distinguish them, and the RESULT must say which she was watching.

## 4. THE DESIGN INTENT, FROM THE OWNER

**A breach is a route.** Once the gate is down, attackers should prefer going THROUGH it over continuing
to chew adjacent walls. This is the Clash-of-Clans behaviour the project defaults to on ambiguous
build/UI calls (memory: `design-tiebreaker-what-would-coc-do`) - a funnel exists to be used.

**Do not turn this into a pathing rewrite.** Establish the minimum that makes a breach preferred, and
bring anything larger back as a recommendation.

## 5. ACCEPTANCE

- [ ] The instrumentation lands FIRST and a captured run is quoted in the RESULT.
- [ ] The proven root cause is stated with the trace line that proves it. **An inferred cause is a guess
      and is not acceptable** (memory: `never-inference-fix`).
- [ ] After the fix, a captured run shows units routing through a destroyed gate rather than attacking
      intact adjacent segments.
- [ ] A regression pins it: with a gate destroyed and a route open, target selection must prefer the
      breach. It must FAIL against today's build - state the RED proof in-file.
- [ ] Section 3.4 answered explicitly: whose AI was underfighting.
- [ ] `REGRESSION_OK n/n`.

## 6. CONTEXT THE LANE MUST NOT TRIP OVER
⛔ **Enemy spawns resolve BY COMPONENT, never by tag. The `SpawnPoint` tag DOES NOT EXIST** - only four
tags are declared (`Tower`, `Building`, `HeartTarget`, `Player`) and `FindGameObjectsWithTag` **THROWS**
on an undeclared tag. That exact mistake shipped WO-1038, where every scan died and nothing spawned. The
real seam is `WaveSpawnPoint` via `FindObjectsByType`, ordered deterministically by `WaveSpawnResolver`
(`FindObjectsByType` is UNORDERED).
