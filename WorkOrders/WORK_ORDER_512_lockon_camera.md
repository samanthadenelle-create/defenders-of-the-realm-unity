# WORK_ORDER_512 — soft lock-on + lock-on camera (battle)

**Status:** READY TO IMPLEMENT (owner picked "highest value" 2026-06-24) · Combat/Camera lane · flag `ff.lockon` (default OFF until proven)
**Origin:** owner-approved design; architect plan grounded in the real code (SME map below). REUSE-FIRST — no new camera rig, no Cinemachine, no new reticle. Felt-sensitive (the fight already "feels amazing" — do NOT regress it; mobile-nausea is the top risk).

## Behavior (approved)
Auto-lock nearest enemy on engaging the battle (or tap the Lock toggle). While locked: camera keeps the locked orc framed (Knight + orc both readable, orc roughly centered), Knight auto-faces + strafes around it. Switch via the HUD roster/cycle; tap Lock again to release to free-look. Reticle ring marks the locked enemy.

## SME map (the REAL seams — reuse these, do not greenfield)
- **Battle camera = `SmartMobileCamera`** (`Assets/_Modules/Village/Hero/SmartMobileCamera.cs`), runtime singleton, enforces sole-camera every LateUpdate (a NEW rig would be fought to death — reuse mandatory). It ALREADY has the auto-framing seam (~L624-632): when framing+combat, `leadTarget` lerps toward `Lerp(heroBase, _nearestEnemyPos, _framingBias)` via a `_leadPoint` SmoothDamp. Lock-on = a focused variant fed the LOCKED target instead of auto-nearest. Public API present: `Instance`, `FramingEnabled`, `AddYaw/AddPitch`, `ForceFollowImmediate`, `CameraYaw`, `Shake`. Precedent for borrow/return = `ArenaDeathCam` (but that HARD-suspends SMC — we do the opposite: keep SMC running, steer it).
- **Lock owner = `HeroTargetIndicator`** (`Assets/_Modules/Village/Hero/HeroTargetIndicator.cs`): owns `_locked`/`CurrentTarget`, writes `_abilities.AimPointOverride`/`LockedTarget` every LateUpdate, has `RebuildCandidates()` (nearest-first hostiles), `CycleTarget()`, `ClearLock()`, and the **existing red/gold reticle billboard** (reuse — no new art).
- **HUD has a HALF-WIRED duplicate lock** (`BattleHud9Zone.cs` `ToggleLock`/`_lockEngaged`/`SelectCycleRow`/BR cluster) that writes aim fields directly and is overwritten by the indicator next frame (two-owner bug). Collapse into the one owner.
- **Hero facing/strafe = `HeroLocomotion`** (NavMeshAgent, `updateRotation=false`, sole rotation writer, camera-relative move). Has `FaceToward()` but it cancels on movement input (brief attack-turn only). Need a NEW lock-face mode that biases yaw toward the locked target EVEN while moving — overriding only the `LookRotation(Velocity)` writer (~L655-657), leaving the camera-relative move vector untouched so strafing falls out naturally.
- **Flags:** `Assets/_Modules/Core/FeatureFlags.cs` — add `LockOn => Get("lockon", false)` + editor menu (mirror `OverworldEncounterMenu`).

## Guardrails (BINDING)
- `ff.lockon` default OFF; every new branch wrapped so OFF == today's exact path. SMC `SetLockTarget` is a no-op when flag off.
- NO snap ever — only change the TARGET fed into the existing `_leadPoint`/`_posVelocity` SmoothDamps; never set transform directly. Switching = move the damp goal (eases).
- Cap `_lockFramingBias <= ~0.45` (Knight always in frame). Yaw-assist (centering) is the nausea hotspot: opt-in sub-flag, low gain, loop gain <1, SUSPEND while player is panning (player drag always wins). Use `Time.unscaledDeltaTime` (SMC already does) so hit-stop/death-cam don't lurch.
- Free-look invariant: with lock released, SMC LateUpdate + HeroLocomotion Update byte-identical to today (guard on `_lockTarget!=null` / `_lockFaceActive`).
- DO NOT touch: SMC `_posVelocity`/`_smoothTime`/wall-collision/occluder-fade/`EnforceSoleCamera`; HeroLocomotion move vector/NavMesh Move/seam-crossing/ground-snap/`InputSuppressed`; `HeroAbilities` aim plumbing; `ArenaDeathCam` (just ensure lock clears when it suspends SMC); the ATB (`DeNelle.BattleATB`).

## Instrumentation (FlowTrace category "BattleArena" — matches ArenaCombatOracle/F8)
`LOCKON acquire target='..' (auto-nearest)`, `LOCKON engage/release -> free-look`, `LOCKON switch -> '..'`, SMC `LOCKON camera framing target bound '..'`/clear, HeroLocomotion lock-face one-liners. Acquire/switch/release verifiable headless.

## Slices (smallest first; each flag-gated + independently gate-able + felt-testable)
- **Slice 0 — Flag + single lock owner (no behavior change):** add `FeatureFlags.LockOn` + menu; add `EngageLock/ReleaseLock/CycleLock/LockEngaged/LockedEnemyTarget` to HeroTargetIndicator (thin wrappers over `_locked`/`CycleTarget`/`ClearLock`); repoint HUD `ToggleLock`/`SelectCycleRow`/BR cluster to call them + read `LockEngaged` for the label; remove the HUD's duplicate aim writes. (Bonus: fixes the two-owner bug.) Gate: reticle reds on toggle, label Locked/Unlocked, abilities still aim.
- **Slice 1 — Auto-lock on engage + reticle:** `BattleArena.StageRoutine` (after SpawnFamily, enemies>0) calls `indicator.EngageLock()` behind the flag. Gate: entering battle auto-locks nearest orc, red reticle, acquire FlowTrace fires.
- **Slice 2 — Camera framing:** SMC `SetLockTarget/ClearLockTarget` + lock-framing branch (reuse `_leadPoint` damp + framing path); wire from BattleArena (bind on stage, clear on Resolve + on switch). Yaw-assist OFF initially. Owner felt-test: no nausea, fight still amazing.
- **Slice 3 — Face/strafe:** HeroLocomotion `SetLockFace/ClearLockFace` + facing-override branch (reuse `StepYaw`/`_rotationSpeed`), driven by HeroTargetIndicator engage/release. Gate: A/D strafes around orc, Knight keeps facing; release restores `LookRotation(Velocity)` identically.
- **Slice 4 — HUD toggle + cycle polish:** top-center toggle + BR lock disc + mid-left roster all route through the one owner + reflect `LockEngaged`; cycle disc steps targets; optional conservative yaw-assist behind its sub-flag. Gate: full mobile+desktop loop.

## Files
Edit: FeatureFlags.cs, HeroTargetIndicator.cs, SmartMobileCamera.cs, HeroLocomotion.cs, BattleArena.cs, BattleHud9Zone.cs. Create: none (all reuse). 

## Acceptance
Per-slice gates above; final = auto-lock on engage -> framed duel, strafe-around, switch via roster/cycle, release to free-look, ZERO regression with flag off, no nausea (owner felt-call). Headless: acquire/switch/release FlowTrace lines fire.
