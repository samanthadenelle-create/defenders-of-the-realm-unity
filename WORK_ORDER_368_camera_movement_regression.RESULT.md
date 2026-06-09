# WO-368 RESULT — Camera Movement Regression

**Status:** ✅ CLOSED (resolved by a different root cause than the spec assumed)
**Commit:** `4abde65` fix(camera): end "hero stuck on the tree" + screen-shake
**Verified:** Owner playtested Village2 — hero visible, camera follows smoothly, movement responds, no tree-lock, no shake.

## What the spec assumed vs. what was actually wrong
The WO blamed WO-367 (camera moved 65% closer) coupling movement to camera distance, and prescribed reverting to 45° + decoupling movement. **That premise was wrong.** The real cause of "movement broken in town / camera stuck on the tree" was `CameraModeController` (WO-338): at scene load it resolved TOWN mode, which **disabled SmartMobileCamera and locked the view to the town origin (the Tree of Life)**, running at `[DefaultExecutionOrder(100)]` so it overrode the follow camera every frame. The hero was fine and moving the whole time — just off-screen.

## Fix shipped
`CameraModeController.EvaluateContext` now gates TOWN mode strictly on active base-build (`BuildModeController.IsActive`); all normal play stays in the owner-validated SmartMobileCamera follow. Movement was never camera-coupled in code — no decouple needed.

## Acceptance reconciliation
- [x] Movement works (WASD/arrows respond) — owner-verified
- [x] No camera-relative movement bugs — movement uses NavMeshAgent, never camera-coupled
- [x] Works in town / battle / exploration — follow camera owns all three now
- [n/a] "Revert to 45° / 65% closer / decouple movement" — based on the incorrect WO-367 premise; not applicable once the real cause was fixed

See also: WO-338 was the regression source. Camera memory updated.
