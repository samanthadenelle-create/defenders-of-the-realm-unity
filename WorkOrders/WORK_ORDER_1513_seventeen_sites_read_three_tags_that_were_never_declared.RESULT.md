# WO-1513 RESULT - the HeroTarget fallbacks are gone from the Village silo; six reads survive, two of them allowlisted

**Status:** PARTIAL - uncommitted in the working tree as of 2026-09-06 21:45, awaiting the wave-two gate.
**Commit:** none. Edit-only lane.
**Files:** `Village/Camera/CinemachineCameraController.cs:102`, `Village/Hero/SmartMobileCamera.cs:788`,
`Village/Diagnostics/CastleNavTopologyDiag.cs:97`, `Village/Dungeon/PortalVFXController.cs:772,811,855`,
`Village/Enemies/Enemy.cs:1763`, `Village/Enemies/EnemyBehaviorTree.cs:55`; oracle
`Assets/Editor/Regression/StructureTargetableRegression.cs:712-756,835`.
**Gates:** none. `Builds/cg-quiet.log` `COMPILE_GATE_OK` (20:04) predates these edits; `Builds/cg-aab.log` (20:54)
is RED (42x `CS0103`, the Manage lane's half-written suites).

## 1. What landed

The dead `SafeFindWithTag("HeroTarget")` first-choice terms are DELETED from the Village targeting and camera
paths, each replaced by a comment naming WO-1513 and the reason (the hero carries `Player`; one tag per
GameObject). Same for the `"ScreenFlash"` tag read in `PortalVFXController` (`:811`, `:855`). `TagManager.asset`
still declares exactly four tags - `Tower`, `Building`, `HeartTarget`, `Player` - and nothing was added, which is
sec.3's expected answer. The tag-literal oracle lives inside the existing `StructureTargetableRegression.cs`
rather than as a new file (`:835` guards the WO-450 prose so a comment quoting the old call cannot trip it).

## 2. What survives, measured

`grep` for live reads outside `Assets/Editor/Regression` returns six:

```
Village/Buildings/DrawbridgeController.cs:61        HasTag(other, "HeroTarget")
Village/Enemies/EnemyBrain.cs:777, :1138            TryFindByTag("PetTarget")
Village/Enemies/Perception/AwarenessSensor.cs:154   TryFindByTag("PetTarget")
Editor/RegressionSuite.cs:894                       HasTagSafe(t, "HeroTarget")
Editor/CastleGateNavVerify.cs:216                   new[] { "Player", "HeroTarget" }
```

Two are **explicitly allowlisted by the oracle itself** (`StructureTargetableRegression.cs:729-732`):
`EnemyBrain.cs` is tagged "WO-1513 do-not-touch file" and `AwarenessSensor.cs` "WO-1513 owned by another lane".
The oracle MARKS matches rather than consuming them (`:756`), so the allowlist is visible rather than silent.
`DrawbridgeController.cs:61` and the two editor-only sites are simply not yet swept.

## 3. Acceptance

- [ ] Zero reads of an undeclared tag - **OPEN**, six remain (sec.2), two by design under another lane.
- [ ] The tag-literal regression exists; RED proof stated - the oracle exists
      (`StructureTargetableRegression.cs:712-756`); the deliberate bogus-tag RED is unrun (tree does not compile).
- [ ] `REGRESSION_OK n/n` on a fresh log - owed.

## 4. Owed

The wave-two gate; a ruling on the two `PetTarget` sites parked under another lane; the three remaining sweeps
(`DrawbridgeController`, `RegressionSuite`, `CastleGateNavVerify`). No device capture applies - every branch
removed was provably dead, so nothing player-visible changed.
