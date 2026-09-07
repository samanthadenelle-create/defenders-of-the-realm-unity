# WO-1569 RESULT - breach probe reads a felled tower after destroy

**Outcome:** IMPLEMENTED, edit-only. Uncommitted, no Unity run, no git.

## What was wrong
`TraceBreachProbe` null-checked an `IDamageable` **interface** reference (`destroyed != null`), which
never reaches `UnityEngine.Object`'s overloaded `==`. A `DefenseTower` is `Destroy(gameObject)`d by
`Destructible.NotifyBroken` (`DefenseTower.cs:170`, `:349`) and still passes it, so
`destroyed.WorldPosition` threw in `Component.get_transform`. Device build 2026.09.07.358872, scene
RaidBase_raider_camp_small, F8 seq 4688 + 4689. Walls never reproduced it: a collapsed `WallSegment`
keeps its component (`TroopController.cs:619`).

**Premise corrected (11B):** NOT per-frame. `NearestHostile` is OverlapSphere-based so it cannot
re-return a destroyed collider, and `previousFoe` changes after the rescan - it fires once per felled
structure per troop. The damage is that WO-1438's `holeNavmesh=` sample was **never emitted for a
tower**: the lambda died before its `FlowTrace.Step`.

## Changes
| File:line | Change |
|---|---|
| `TroopController.cs:1024-1060` | `public static bool IsLiveTarget(IDamageable)` - the Unity-aware check. THE fix |
| `TroopController.cs:586`, `:591-604`, `:619-628`, `:702-707`, fields `:136-146` | `foeValid` uses it; new `foe-destroyed` reason; `_lastFoePos`/`_lastFoePosValid` record the foe's position **while live** |
| `TroopController.cs:1104-1132`, `:1160-1170` | probe takes `destroyedPos`/`destroyedPosValid`; `replacementLive` replaces `replacement != null`; hole sample reads the carried value, never the corpse |
| `DefenseTower.cs:262-293` | `WorldPosition` caches on read behind a Unity alive check. Safety net, not the fix |
| `TroopTargetPreferenceRegression.cs:73`, `:205-286` | Case 5: destroyed tower - interface null check still passes, `IsLiveTarget` refuses it, `WorldPosition` returns the last live value without throwing |

## Deviations, declared (11B-B)
1. **`FlowTrace.Throttle` NOT applied to the probe trace.** It is a once-per-event line; a per-troop
   1 s throttle would swallow the second breach when two structures fall fast - the exact WO-1438
   case. The flood it was meant to contain was the *exception*, which can no longer occur.
2. **Two `.cs` files got a python byte-level pass** (`rb`/`wb`), not the Edit tool: to swap six
   em-dashes I had introduced for ASCII, and to restore `DefenseTower.cs` to LF after the Edit tool
   flipped the whole file to CRLF (HEAD is LF).

## Evidence, and what is NOT proven
Measured: braces 181/181, 156/156, 20/20; zero NUL; CRLF=0 in all three (matches HEAD); zero
non-ASCII bytes in added lines; `DefenseTower.cs` diff stat 32+/1-. `_aimBeamMat` has no inline
initializer (`:531`, assigned only at `:1362` from Start), so Case 5's `DestroyImmediate` cannot
reach `OnDestroy`'s edit-mode-illegal `Destroy`. NOT proven: no gate, no play session, no
`TROOP_TARGET_PREF_OK` marker - the Unity lock is the CLI lead's; Case 5's RED is **reasoned from
source, not executed**, and is labelled that way in the file.
