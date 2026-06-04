# WO-87 RESULT — Cinemachine Camera System

**Status:** COMPLETE (code only — scene wiring required in Editor)  
**Date:** 2026-05-29  
**Implemented by:** CLI agent

---

## Cinemachine Version

Cinemachine **3.1.6** is already installed (`com.unity.cinemachine` in manifest.json).  
API used: `Unity.Cinemachine` (v3) — `CinemachineCamera`, `CinemachineImpulseSource`, `CinemachineThirdPersonFollow`, `CinemachineBrain`.  
`DeNelle.Village.asmdef` already references `Unity.Cinemachine`.

---

## Files Created

### `Assets/_Modules/Village/Camera/CinemachineCameraController.cs`

Public singleton MonoBehaviour. Features:
- `vcVillage` / `vcCombat` / `vcWaveClear` — three `CinemachineCamera` fields (wire in Inspector)
- Enemy proximity polling via `CheckCombatProximity()` (InvokeRepeating, configurable interval)
- `ShakeLight(float)` / `ShakeMedium(float)` / `ShakeHeavy(float)` — impulse tier methods
- `Shake(float intensity, float duration)` — **legacy shim** that `CameraShakeBridge` finds via reflection automatically (Tower.cs, Enemy.cs, CombatFeedbackManager.cs all call `CameraShakeBridge.Shake(f,f)` which reflects on any MonoBehaviour exposing that signature)
- Mobile shake scaled 0.55×
- `PlayWaveClearCinematic(float duration)` — enables `vcWaveClear` for the specified duration
- Brace check: 12/12 BALANCED

---

## Important Architecture Notes

### HeroCinemachineRig (already exists)
`Assets/_Modules/Village/Hero/HeroCinemachineRig.cs` already exists and creates a `CinemachineCamera` at **Priority 100** for the OTS hero follow. The three cameras in `CinemachineCameraController` use priorities 10/11/12 so the hero rig always takes precedence when active. This is intentional — the controller cameras are for overhead/village views in scenes without the OTS rig.

### CameraShakeBridge (no modification needed)
`CameraShakeBridge` is an `internal static class` inside `Tower.cs` that finds any MonoBehaviour exposing `Shake(float, float)` via reflection. `CinemachineCameraController.Shake(float, float)` satisfies that contract automatically — no Tower.cs edit needed.

### WaveCelebrationManager.cs
Does not exist in the codebase. When it is created, add:
```csharp
CinemachineCameraController.Instance?.PlayWaveClearCinematic(slowMoDuration + 0.5f);
```

---

## Scene Wiring Required (Editor only — CLI cannot write .unity files)

Per CLAUDE.md §3, scene files are NEVER hand-edited. Use the Defenders menu or wire in Inspector:

1. **CinemachineCameraController GameObject**: Add to Village scene. Add `CinemachineImpulseSource` component. Wire `vcVillage`, `vcCombat`, `vcWaveClear` fields.
2. **Create VC_Village** GameObject: Add `CinemachineCamera` (Priority 10) + `CinemachineImpulseListener`. Set Follow/LookAt to hero. Add `CinemachinePositionComposer` or `CinemachineThirdPersonFollow` as body.
3. **Create VC_Combat** GameObject: `CinemachineCamera` (Priority 11), closer distance, start **disabled**.
4. **Create VC_WaveClear** GameObject: `CinemachineCamera` (Priority 12), wide pull-back, start **disabled**.
5. **Main Camera**: Confirm `CinemachineBrain` is present (HeroCinemachineRig adds it at runtime; can also add manually).

---

## Brace Balance

```
CinemachineCameraController.cs   12/12  BALANCED
```
