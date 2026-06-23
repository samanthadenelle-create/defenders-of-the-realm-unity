# WO-166 RESULT — playtest regressions (gates / walk-anim / pet / stairs)

**Status: PARTIAL — 2 fixed, 1 deferred (owner call), 1 blocked on missing asset.**
**Gatekeeper:** CLI session. Verified against `feat/tower-core-loop` (not the stale worktree base).
**Date:** 2026-05-30

---

## #1 Gates missing/impassable — RESOLVED (already, via WO-168)
All 4 cardinal gates ARE built in `BuildWallPerimeter` (`Gate-North-Main` `:2963`,
`Gate-South-Main` `:2971`, `Gate-East-Side` `:2979`, `Gate-West-Side` `:2987`). The
"missing" severity was a prior build state; on this branch they render. The real
issue was **passability**, fixed in **WO-168** (commit `5834479`): the gate-arch +
moat/drawbridge meshes were voxelizing solid across the openings on the NavMesh
(hero is now a NavMeshAgent). Excluded via `IsUnderPerimeterGate` + `IsNonWalkableMoatPiece`;
marked count 4164→3956; rebaked + committed. **Owner to confirm walk-out in-editor.**

## #2 Walk animation "backwards" — DEFERRED (owner verifies in-editor)
NOT root motion (the `ActionClipImporter` already bakes Action clips in-place, and
`HeroBodySwapper` has a Speed-param guard + animator re-cache). The "backwards" is
**mesh facing** — the CC5-Knight body yaw at `HeroBodySwapper.cs:68` (`90f`), which
is **owner-field-tested (2026-05-30)**. Owner chose not to blind-flip it; will verify
which way the Knight faces walking in-editor, then we set the exact yaw. No code change.

## #3 Pet T-pose — BLOCKED on a missing asset (needs a quadruped clip)
Root cause confirmed: `Assets/Resources/Pets/ice-wolf.fbx` is a **quadruped fox/coyote
CC5 mesh, animationType 2 (Generic), with ZERO embedded animation takes** — a static
mesh export. `Pet.cs` correctly skips driving Speed/Attack/Hit/Dead (WO-163 guard) so
it doesn't error, but with no clip + no controller the fox renders in its bind pose
(reads as a T/spread pose).
- **"Retarget Mixamo like heroes" is not viable:** the only Action clips are 3 upright
  **bipedal** humanoid clips (idle/walk/run). A quadruped has no valid Humanoid avatar
  mapping — the retarget would build an invalid avatar OR make the fox walk on two legs.
- **"Idle-only stopgap" is also blocked:** there is no idle clip to play (FBX has none;
  bipedal clips don't fit a fox).
- **Real fix (own WO):** source a quadruped idle/walk clip (Mixamo has no free quad
  rig), build `Resources/Pets/ice-wolf.controller` (Generic, idle+move blend on Speed),
  and load it in `Pet.cs`/`PetDeployer`. Until a quad clip exists, the pet cannot animate.

## #4 Rampart stairs mid-courtyard — FIXED (commit pending rebake)
Ramps ran perpendicular 9 m into the courtyard. Re-routed all 4 to run **parallel along
the wall** (`VillageSceneBuilder.cs` ~`:3256-3261`): ends on the walkway-edge line, climb
spans along the wall axis clearing the gate gap. Brace 513/513. **Needs a village rebake
to take visual effect** (editor-closed).

---

## Follow-ups
- Rebake the village (CLI, editor closed) so the stairs reposition lands in `Village.unity`.
- New WO: pet quadruped animation (source clip → Generic controller → Pet load).
- Owner in-editor: confirm gate walk-out + Knight walk facing.
