# WO-436 — P0 Bug: Enemy renders solid green + no animation (sliding)

**Status:** READY TO IMPLEMENT  
**Priority:** P0  
**Lane:** 3 Combat/AI  
**Minted:** 2026-07-03

---

## Bug

Enemy renders as **solid bright green** with **no animation** — slides across the ground
via NavMeshAgent with zero animation playing. Screenshot confirms: horned creature type,
uniform green surface, no walk/attack animation cycle.

## RCA — two independent failures

### Failure A — Solid green = URP material not applied

`VisualFactory.Skin()` loads the enemy visual from `Resources/Enemies/<modelName>`.
The URP material fix (`FixTripoMaterials` / `Defenders > Art > Fix Polyperfect URP Materials`)
only covers specific rig families. If:
- The Polyperfect pack was re-imported on a fresh clone but the URP fix menu item was NOT run, OR
- This enemy's rig family falls outside the families `FixTripoMaterials` covers

…then the raw FBX surface material renders as solid unlit green under URP (Unity's fallback
for an unassigned/incompatible material in URP).

### Failure B — No animation = RuntimeAnimatorController not loaded

`EnemyAnimatorFactory.Apply()` does:
```csharp
Resources.Load<RuntimeAnimatorController>("Enemies/<name>")
```
If `Defenders > Animation > Build Animator Controllers` + `EnemyAnimatorSetup` haven't
been run (or their output wasn't committed to the repo), this load returns null, the
controller is never assigned, and the Animator idles in its empty default state.
The NavMeshAgent then moves the transform with no clip playing → "sliding."

## Fix sequence

### Step 1 — Instrument (read before touching anything, per §12)
Add `FlowTrace` in `EnemyAnimatorFactory.Apply()`:
```
FlowTrace.Step("EnemyAnim", $"Load controller for {modelName}: {(ctrl == null ? "NULL" : "OK")}");
```
And in `VisualFactory.Skin()`:
```
FlowTrace.Step("EnemyVisual", $"Material on {modelName}: {renderer.material.name}");
```
Run headless, capture which enemy type is null and what material name appears.

### Step 2 — Fix material (if Polyperfect fix needed)
Run `Defenders > Art > Fix Polyperfect URP Materials` in the editor.
If the enemy's rig family is not covered by the fix, identify its material in:
`Assets/polyperfect/Low Poly Ultimate Pack/_M/` and add its GUID to `FixTripoMaterials`
case list.

### Step 3 — Fix animation (if controller missing)
Run `Defenders > Animation > Build Animator Controllers` then `EnemyAnimatorSetup`
to populate `Resources/Enemies/`. Commit the generated controller assets.
If the specific enemy type has no controller defined, add a minimal one
(Idle + Walk states driven by `Speed` float parameter) following the existing pattern.

### Step 4 — Verify
Add `FlowTrace.Warn` if `ctrl == null` after load (permanent guard — never silently slide again).
Run headless AutoPilot: confirm enemy animates walk cycle and displays correct material color.

## Files to touch
- `Assets/_Modules/Village/Enemies/EnemyAnimatorFactory.cs` — FlowTrace + null guard
- `Assets/_Modules/Village/Enemies/VisualFactory.cs` — FlowTrace + material log
- `Assets/Editor/FixTripoMaterials.cs` (or equivalent) — add missing rig family if needed
- `Resources/Enemies/` — generated controller assets (commit output)

## Do NOT touch
- `EnemyBrain.cs` NavMesh movement logic (this is a setup bug, not a movement bug)
- Any scene files

## Acceptance criteria
- [ ] Enemy renders with correct Polyperfect LOW POLY visual (not solid green)
- [ ] Enemy plays walk animation when moving toward target
- [ ] `FlowTrace.Warn` fires if controller is null at runtime (permanent guard)
- [ ] Headless run: no null AnimatorController warnings in captured log
- [ ] FlowTrace confirms material name is NOT Unity default/fallback
