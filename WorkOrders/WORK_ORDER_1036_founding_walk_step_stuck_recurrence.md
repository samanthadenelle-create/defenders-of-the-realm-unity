# WORK ORDER 1036 — `founding_walk` STEP-STUCK recurs AFTER WO-962 shipped: the FTUE hard-blocks

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1036 → 1037 in the same edit
**Lane:** Tutorial V2 / guide gate. ⚠ Interacts with WO-1031's guide despawn — see §4.
**Priority:** **HIGH.** This is the second beat of the FTUE. A player who hits it cannot start the game.
**Provenance:** F8 **seq=2433** (2026-08-16, 125s) and **seq=2343** (2026-08-15, 241s).

---

## 1. The captured signal, twice

```
[Flow:Tutorial] STEP-STUCK :: founding_walk — no 'hero.reached:guide_gate' after 125s in-step
[Flow:Tutorial] STEP-STUCK :: founding_walk — no 'hero.reached:guide_gate' after 241s in-step
```

Same step, same missing event, two separate sessions a day apart. **Not a one-off.**

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
to follow a guide that no longer exists — converting this **intermittent** stall into a **deterministic
hard block on the first minute of the game**.

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
