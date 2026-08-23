> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: `Assets/Editor/AnimatorSetup.cs` is still present (VERIFIED at source); sec.3 of this WO required it retired.
> The previous Status line read "READY - child of WO-872." and was wrong.

# WORK ORDER 877 — Animation rework: kill placeholder/wrong-clip reuse (retarget from owned libs)

**Status:** DONE 2026-08-23 (owner-confirmed: "WO-877 done").
**Origin:** owner 2026-08-04 — *"look over ALL animations and add rework everywhere."* Audit-backed (WO-872 §2, A1–A9).
**Owned retarget libraries:** 401 Mixamo Humanoid clips (`Assets/Action/`) + KayKit Character Animations 1.1
(`docs/asset-inventory/01_kaykit.md`). Retarget — author no new clips.

## 1. The placeholders / wrong-clip reuse (audit)
- **A2 — Hero Ranger: all 4 ability casts reuse ONE `Ranger_Aim_Idle`** (`HeroAnimatorFactory.cs:216-225`) — a single
  idle pose stands in for q/w/e/r. Add distinct bow draw/loose (+ Sylas kit motions) from `Action/Ranger/`.
- **A3 — Hero Knight cast fallback is a logged PLACEHOLDER** (`HeroAnimatorFactory.cs:396-402`) — wire the real
  `Action/Knight/` combo clips.
- **A4 — KayKit vendor / drillmaster NPCs are IDLE-ONLY on a single-point-of-failure controller** (T-pose if
  `KayKitNpcIdle.controller` missing — owner F8 2026-08-02, `KayKitNpcBody.cs:93-134`). Harden (fallback clip) +
  give them at least a couple of ambient clips so the town isn't statues.
- **A6 — Enemy Necromancer → generic `Boss` controller** (reuse mismatch, unverified, `EnemyAnimatorFactory.cs:29-95`)
  — verify clips vs rig or give it its own set.
- **A8 — stale `AnimatorSetup.Hero/Npc/Pet.controller`** is a DEAD parallel to the live per-class controllers
  (`AnimatorSetup.cs:205-215`) — retire it so nothing loads the stale path.
- **A9 — Pet quadruped (Ice-Wolf) GAP** — only a humanoid `Pet.controller`, no quadruped rig (flag; needs its own rig).

## 2. Fix
Retarget the above from the owned Mixamo + KayKit libraries (Humanoid retarget — bone names/counts don't matter with a
valid avatar). Keep the T-pose guards/verifiers (`EnemyPoseVerifier`, `HeroBodySwapper` deferred verify) — extend them
to the NPC controller so a missing clip degrades to a fallback pose, never a live T-pose.

## 3. Acceptance
- [ ] Ranger's 4 abilities play distinct motions (not one aim-idle); Knight cast uses real combo clips.
- [ ] KayKit town NPCs are not idle-only statues and cannot live-T-pose (fallback wired).
- [ ] The stale `AnimatorSetup` parallel is retired; enemy Necromancer clips verified. `CompileGate` green; on-device.

## 4. Do NOT
- Author no new animation clips — retarget from the owned libs. Do NOT remove the T-pose guards. (Build-worker anim =
  WO-871; pet quadruped rig = flagged, separate.)
