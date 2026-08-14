# WORK_ORDER_383 — Castle ↔ OuterWorld seam connection (+ hero-strand bug fix)

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at HeroLocomotion.cs:455-522.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Lane:** 5 World/Explore
**Source:** This session (2026-06-09)
**Created:** 2026-06-09 — to track the previously-untracked castle↔OuterWorld connection work and the live seam bug found in playtest.

---

## Why this WO exists

The castle hub (`MainCastle_Hall`) → OuterWorld connection was **built across several commits but never had a tracked work order** (board-hygiene gap surfaced in the 2026-06-09 backlog reconciliation). This WO retroactively documents that work AND specs the fix for the live bug it has.

### Associated commits (the connection, already landed + pushed)
- `b3b5cef` — start in MainCastle_Hall hub + OuterWorld gate wiring
- `9c8c64f` — wire MainCastle_Hall as playable start (headless)
- `e213e25` — walkable MainCastle_Hall (invisible NavMesh floor + walk-through keep)
- `53640cf` — level-2 ramp + upper-battlements navmesh
- `fa3ced7` — castle "diagonal walk" body-facing fix (related; did NOT resolve the edge break)

### What is WIRED (verified in code)
- Both `Assets/Scenes/MainCastle_Hall.unity` and `Assets/Scenes/OuterWorld.unity` are in Build Settings.
- `WorldSceneLoader.cs` lists `MainCastle_Hall` as a hub and streams `OuterWorld` additively.
- `CastleHubBuilder.WireOuterWorldConnection` places a `SceneTransitionTrigger` + `NavMeshLink` at the south gate.
- `SceneRouter.Castle` / `GoCastle()` is the start; onboarding + returning-player route there.

---

## THE BUG (playtest-reported, root-caused 2026-06-09)

**Symptom (owner):** From the castle spawn, camera + movement + animation are correct. Walking south to the scene edge, the **camera and hero direction "break" and stay broken even after backing off** (only a respawn fixes it).

**Root cause (NOT a camera bug):** the south-gate `OuterWorldTransitionTrigger` (a `DeNelle.Village.SceneTransitionTrigger`, world box z ≈ −67…−73, x ±8 in `MainCastle_Hall.unity`) fires `OnTriggerEnter` and hard-sets the hero to `targetPosition = (0, 0.5, −80)`. But:
- `HeroLocomotion.Update` has a hard **±50 clamp** when the agent is off-mesh (`HeroLocomotion.cs:374-379`, `PlayableHalf = 50f`).
- `(0,0.5,−80)` is **30m past the clamp** and lands **off the castle NavMesh** (OuterWorld's NavMesh is a separate baked asset and may not connect at that point).
- → Every subsequent frame the hero is yanked from −80 back to −50 in off-mesh limbo; `SmartMobileCamera` (the only camera — innocent) chases the teleport-then-snap-back motion. Nothing resets until respawn.

CameraModeController-ring and second-camera theories were investigated and **killed** (CameraModeController's town code only flips on `BuildModeController.IsActive`, and no second camera exists in either scene).

---

## THE FIX

Core principle (validated against the code + a Unity-6 best-practice second-opinion review, 2026-06-09): **don't fight the locomotion system with a raw `transform.position =`.** The trigger sets position far outside the locomotion clamp and off the NavMesh, and locomotion/agent fight it every frame. Make locomotion **teleport-aware** instead.

**Primary (preferred, durable) — add a teleport-aware Warp to `HeroLocomotion`:**
```csharp
public event System.Action OnTeleported;
public void WarpTo(Vector3 worldPos, Quaternion? rot = null)
{
    _isTeleporting = true;                                  // clamp/movement skips this frame
    if (NavMesh.SamplePosition(worldPos, out var hit, 5f, NavMesh.AllAreas))
        worldPos = hit.position;                            // land on valid mesh
    if (_agent != null)
    {
        _agent.enabled = false;                             // critical before moving
        transform.position = worldPos;
        _agent.Warp(worldPos);                              // re-acquires NavMesh
        _agent.enabled = true;
        _agent.ResetPath();
    }
    else transform.position = worldPos;
    if (rot.HasValue) transform.rotation = rot.Value;
    OnTeleported?.Invoke();
    _isTeleporting = false;
}
```
- Make the off-mesh ±50 clamp (`HeroLocomotion.cs:374-379`, `PlayableHalf = 50f`) **respect `_isTeleporting`** (skip the clamp that frame) — that clamp is a stale castle-era assumption and is wrong for the streamed continuous world anyway; widen or remove it for the combined hub+OuterWorld bounds.
- `SceneTransitionTrigger.RepositionPlayerAfterLoad` (`SceneTransitionTrigger.cs:48-64`) calls `loco.WarpTo(targetPosition)` instead of `playerTransform.position = ...`.

**Camera — make `SmartMobileCamera` teleport-aware too:** subscribe to `HeroLocomotion.OnTeleported` (or read an `IsTeleporting` flag), suspend the SmoothDamp follow for the warp, and snap to the new offset — so it never chases the intermediate bad positions. (Camera stays world-absolute per WO-368; this only prevents the chase, it does not change movement.)

**Minimal fallback** (if not refactoring locomotion this pass): do the `NavMesh.SamplePosition` + `agent.Warp` directly inside `SceneTransitionTrigger` — still correct, but the teleport-aware locomotion above is the long-term pattern (reusable for AI/companions crossing seams too).

**Same-scene alternative considered:** a `NavMeshLink`/`OffMeshLink` across the gate (smart-teleporter with animation curve). Castle↔OuterWorld are separate baked NavMesh assets loaded additively, so the warp-on-trigger handoff is the fit; a NavMeshLink is the option if/when the two surfaces are baked as one.

---

## Acceptance criteria
- [ ] Walking south through the castle gate transitions to OuterWorld with the hero landing **on the OuterWorld NavMesh** (verify `_agent.isOnNavMesh == true` and hero z stays where placed, not snapped to −50).
- [ ] Camera + movement + animation remain correct after crossing the seam (no persistent break; no respawn needed).
- [ ] Backing away from the gate and returning behaves normally.
- [ ] No regression to the castle interior / ramp / spawn behaviour.
- [ ] Regression gates (WO-373) still PASS (tree at origin, WASD world-absolute, scene loads clean, camera doesn't break movement).

## What NOT to touch
- Do **not** "fix" the camera — `SmartMobileCamera` is the innocent follower; the bug is the teleport target vs. the locomotion clamp.
- Do **not** rebuild the castle or re-bake without need; this is a code fix in `SceneTransitionTrigger.cs` (+ optional `HeroLocomotion.cs`).
- Keep movement **world-absolute** (WO-368) — do not make it camera-relative.

## UNVERIFIED (confirm in-editor)
- Whether OuterWorld's baked NavMesh actually covers/connects at (0,0.5,−80) when loaded additively. If it does connect, the off-mesh strand may not fire and the residual is teleport-induced camera thrash + yaw mismatch — the NavMesh.SamplePosition fix is correct either way.
