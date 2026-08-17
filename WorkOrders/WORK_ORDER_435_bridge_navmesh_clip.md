> ⚠ **NUMBER COLLISION — this document does not own WO-435; `WORK_ORDER_435_weapon_grip_orientation.md` does.**
> Referred to hereafter as **WO-435-C (bridge navmesh clip)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WO-435 — P1 Bug: Hero clips through/under bridge — NavMesh not covering bridge surface

**Status:** READY TO IMPLEMENT  
**Priority:** P1  
**Lane:** 2 World/Environment  
**Minted:** 2026-07-03

---

## Bug

Hero character partially clips through or walks underneath the bridge in `MainCastle_Hall`
near the "Enter Elarion" trigger. Bridge geometry has no collision plane on its top surface,
or the NavMesh was baked before WO-593 collider fix landed — so the nav surface passes
through the mesh rather than sitting on top of it.

## RCA

**File:** `Assets/_Modules/Village/World/RuntimeRegionGate.cs`

- WO-593 added colliders to `Drawbridge_Approach` and the runtime-spawned
  `RuntimeSeam_Bridge_South` sibling object (lines ~315–352).
- However, the NavMesh bake predates or didn't include the bridge surface, so agents
  nav-walk at the wrong Y (through or under the bridge deck).
- The runtime seam object (`RuntimeSeam_Bridge_South`) may also be missing its collider
  on the top face — needs runtime verification.

**Symptom path:**
NavMeshAgent.destination set → agent pathfinds on stale baked nav surface → path passes
through bridge geometry → character renders partially submerged.

## Fix — two-step

### Step 1 — Instrument + verify (HEADLESS, before touching anything)
Add a `FlowTrace.Step` in the bridge spawn path that logs:
- Whether `Drawbridge_Approach` has a collider component attached
- Whether the runtime seam object has a collider
- The Y position of the NavMesh hit directly above the bridge center
  (`NavMesh.SamplePosition(bridgeCenter + Vector3.up * 3f, out hit, 5f, NavMesh.AllAreas)`)

Run headless, read the captured data. If collider is missing → Step 2a.
If collider present but NavMesh Y is wrong → Step 2b.

### Step 2a — If collider missing on bridge top
Add a `BoxCollider` to `Drawbridge_Approach` (or its runtime seam) sized to cover the
walkable deck surface. Do NOT hand-edit the `.unity` scene — use the `VillageSceneBuilder`
pipeline or a runtime `AddComponent<BoxCollider>` call in `RuntimeRegionGate.cs`.

### Step 2b — If NavMesh bake is stale
Trigger a NavMesh rebake via `UnityEngine.AI.NavMeshBuilder` in batchmode AFTER collider
is confirmed present. Do not bake with the editor open.

## Files to touch
- `Assets/_Modules/Village/World/RuntimeRegionGate.cs` — FlowTrace instrumentation +
  optional BoxCollider add
- NavMesh asset rebake (batchmode only, CLI-owned)

## Do NOT touch
- `MainCastle_Hall.unity` (hand-edit forbidden)
- Any hero locomotion or camera code

## Acceptance criteria
- [ ] FlowTrace data confirms collider present on bridge deck
- [ ] NavMesh.SamplePosition above bridge center returns Y ≥ bridge deck Y (not below)
- [ ] Hero walks across bridge without clipping through or going under
- [ ] Headless AutoPilot smoke run: no nav exceptions near bridge zone
