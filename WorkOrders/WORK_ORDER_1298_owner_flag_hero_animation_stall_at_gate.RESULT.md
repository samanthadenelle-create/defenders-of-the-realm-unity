# WORK ORDER 1298 — RESULT

**Status:** FIXED (code) — **NOT CLOSED.** The owner closes (§13).
**Date:** 2026-09-02
**Silo:** Hero locomotion / animation

> ## ⚠ THIS NEEDS THE OWNER'S EYES TO CLOSE — NO HEADLESS GATE CAN SEE IT
> An animation stall is a **felt** defect: the hero's transform is correct, her position is correct,
> her destination is correct, and every compile/data gate is green in both the broken and the fixed
> build. The only detector is a human watching the rig walk out of the gate. `COMPILE_GATE_OK` and
> `REGRESSION_OK` prove the *rule* holds in a pure function; they cannot prove the *hero looks like
> she is walking*. Do not mark this DONE from a headless run.

---

## Root cause — named by the captured line, not inferred

The proving line, quoted verbatim from the owner's F8 capture seq **4362**
(`logs/f8-inbox/capture-20260902-013506-seq4362.md`, Session-A `Player.log` tail):

```
[Flow:HeroOwner] scene='Main_Castle_Overworld' owner=HeroLocomotion ownerCC=none ownerAgent=on-mesh
  scriptedMove=off velSelf=0.00 velRoot=14.49 animFeed=velSelf animSpeed=0.00 rootYaw=270.0
  ... inputSuppressed=True autoWalk=False
```

Read the four fields together and they select exactly one branch of the source:

| Captured field | What it rules out |
|---|---|
| `ownerCC=none` | no CharacterController → `ForeignMoverOwnsTransform()` is **false** → the WO-1016 mover-agnostic feed **does not apply** |
| `autoWalk=False` | not the WO-277 auto-walk path |
| `scriptedMove=off` | not the headless turn probe |
| `inputSuppressed=True` | **the dialogue/tutorial suppression branch is the live path this frame** |

That branch (`HeroLocomotion.Update`, formerly at `:920-928`) read, in full:

```csharp
if (InputSuppressed && !probeDriving)
{
    Velocity = Vector3.zero;
    ResolveAnimator();
    if (_animator != null && _hasSpeedParam) _animator.SetFloat(AnimSpeed, 0f);
    _actor?.SetLocomotion(0f);
    return;
}
```

**Two sentences:** the suppression branch treated "the player's input is taken away" as if it meant
"the hero is standing still", and on that assumption wrote a hard `Speed = 0` into the animator
*every frame* and returned. It therefore did not merely leave the animator stale while something
else slid the hero west out of the gate at 14.49 m/s — it **actively overwrote the live walk cycle
with a dead zero**, which is precisely the glide the owner saw and precisely what `animFeed=velSelf`
+ `animSpeed=0.00` + `velRoot=14.49` say in one line.

This is the SAME defect shape as WO-1016 (the dungeon Keeper sliding in a single idle clip), one
branch upstream. WO-1016 made the *normal* feed mover-agnostic via
`ResolveAnimatorFeed(...)` — but only for a **live CharacterController**. The suppression branch
never reached that function at all, so a mover that is neither this component nor a CC (which is
exactly the un-owned mover the trace's own diagnosis names) fell through the hole.

## What was NOT the cause

- **Not WO-1295.** The gate seam / `GateWarp` / runtime `NavMeshLink` retirement is untouched, and
  the owner's felt-verified hero fix (*"i can now go through gates normally"*) is not disturbed by
  anything here. The `[Flow:Seam] WarpTo` lines in that tail identify *a* mover in that pre-WO-1295
  session; the animator defect is independent of which mover it was and survives WO-1295 for **any**
  mover, which is why the fix is written mover-agnostically rather than against the warp.
- **Not the navmesh, the gate geometry, or the bake.** Not opened.

---

## Files changed

### 1. `Assets/_Modules/Village/Hero/HeroLocomotion.cs`

- **NEW pure seam `ResolveSuppressedAnimatorFeed(float measuredRootSpeed, float runSpeedCap)`**
  (beside `ResolveAnimatorFeed`, same testable-without-a-scene idiom). Below
  `AnimStallRootSpeed` (0.5 m/s) it returns a hard `0f` — **byte-identical to the old behaviour**,
  so the WO-377 "hold the hero still during a story beat" contract is untouched and a stationary
  suppressed hero still settles to idle. Above it, it publishes the measured root speed clamped to
  the run tier, so a large single-frame displacement cannot drive the blend tree past its authored
  top child.
- **The suppression branch now calls it** instead of hardcoding `0f` into both `SetFloat(AnimSpeed, …)`
  and `_actor?.SetLocomotion(…)`. `Velocity` still zeroes — this component genuinely is not the
  mover during a beat; only the *animator feed* changed.
- **NEW `[Flow:HeroOwner] "suppressed-but-moving"` trace (§12, 1 Hz, gated on `FlowTrace.Enabled`)** —
  a hero travelling with her input taken away now **reports itself** with velRoot, autoWalk, ownerCC,
  pos, yaw and scene, and says in words that an unclaimed mover here is still a defect upstream. This
  is the "so a dead animator reports itself next time" requirement: the next occurrence names itself
  in one line instead of needing the four-field correlation above.
- **Teleport rebase (`_rootMeasureRebase`, set in `WarpTo`, consumed in `LateUpdate`).** The root-speed
  sample is a raw delta-position/dt, so **a warp published a single enormous `velRoot`** — a false
  `ANIMATION-VELOCITY STALL` today, and (now that the suppression branch consumes the measurement) one
  frame of run clip on arrival. A teleport is not travel; the sample is rebased on the landed pose.
- **The heartbeat's `animFeed=` field no longer lies.** It printed `velSelf` whenever no CC was live;
  with suppression as a third feed source that would have been a false reading in exactly the
  situation this WO is about. It now prints `velRoot(measured,suppressed)`.
- **No FlowTrace stripped or disabled.** The `ANIMATION-VELOCITY STALL` trace at the old `:1690` is
  untouched (acceptance criterion 3).

### 2. `Assets/Editor/Regression/DungeonMoverOwnershipRegression.cs` (existing registered suite)

Acceptance criterion 2, as assertions rather than a play session:

- **Case 2 — the owner's captured numbers as a test.** `ResolveSuppressedAnimatorFeed(14.49f, 6f)`
  fed into `IsAnimationStalled` must be **false**: the exact seq-4362 state can no longer produce a
  moving root with an idle animator. Plus: velRoot `1.01` m/s under suppression must feed above
  `AnimStallAnimSpeed` (the WO's "velRoot > 1 m/s while animSpeed == 0" bar, expressed on the pure
  function so it needs no timed crossing); the feed must not exceed the run tier; and both the
  stationary (`0f`) and sub-threshold (`AnimStallRootSpeed`) cases must still return a hard `0f` so
  the WO-377 dialogue hold cannot regress into a twitching walk cycle during a story beat.
- **Wiring guard (2b)** so those assertions can never pass over dead code: the source must call
  `ResolveSuppressedAnimatorFeed(`, and a re-introduced unconditional `_actor?.SetLocomotion(0f)`
  **fails the suite by regex** — that literal *is* the defect, and naming it is cheaper than
  re-deriving it from a capture a third time.

## Brace / NUL gate (CLAUDE.md §1)

```
Assets/_Modules/Village/Hero/HeroLocomotion.cs                  BALANCED clean
Assets/Editor/Regression/DungeonMoverOwnershipRegression.cs     BALANCED clean
```

## Deliberately NOT touched

- `Assets/_Modules/Village/World/GateTraversalInjector.cs` — WO-1295 owns it (What-NOT-to-touch).
- Gate geometry, the navmesh bake, `SyntyCastlePerimeterBuilder.cs`, and the enemy-pathing half of
  WO-1295 (`NavMesh.CalculatePath == PathComplete`) — all still WO-1295's, all untouched.
- `ResolveAnimatorFeed` itself — signature and semantics unchanged, so the existing WO-1016 Case-2
  assertions (town feed = `selfSpeed`, seam feed wins, foreign-CC feed = measured) still bind.
- **Acceptance criterion 4 (the empty `DialogueViewUI/…/Zone_Header/Label`) is NOT done.** It is a
  dialogue-UI file and another lane owns that surface this session; it is a separate, unowned
  secondary and should be re-routed rather than left implied-fixed by this RESULT.
- The upstream mover itself. This fix makes the animator honest about **whatever** moves the root.
  If the owner still sees the hero *travelling* while her input is taken away, that is a distinct
  defect and the new `suppressed-but-moving` trace will now name it on sight.

## Verification status

- **Gate:** not run here — the lead batch-gates and commits (edit-only lane; nothing staged, nothing
  committed).
- **Headless:** cannot settle this. See the banner.
- **Owner felt-test to close:** walk out of a castle gate while the founding tutorial is pointing at
  it and confirm the walk cycle plays through the crossing. If a capture is taken, `[Flow:HeroOwner]`
  should now read `animFeed=velRoot(measured,suppressed)` with a non-zero `animSpeed`, and no
  `ANIMATION-VELOCITY STALL` line.
