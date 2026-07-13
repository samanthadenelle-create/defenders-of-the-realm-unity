# HANDOVER — 2026-06-09 (session: hero movement & animation fixes)

Pick-up doc for this session. All work is **committed and pushed** to `feat/tower-core-loop`.
Companion docs: `HANDOVER_2026-06-09.md` (earlier same-day), `CASTLE_CAMERA_DIAGNOSIS_2026-06-09.md`.

---

## 0. STATUS AT A GLANCE

| Item | State |
|---|---|
| **Branch** | `feat/tower-core-loop` — **pushed** (remote up to date) |
| Castle "diagonal walk" | ✅ FIXED, owner-verified (commit `fa3ced7`) |
| Two-camera fight (Main Camera) | ✅ FIXED same commit |
| Hero walk/idle/run loop snap | ✅ FIXED, owner-verified (commit `4356b55`) |

Three owner-verified fixes shipped this session. No open regressions from this work.

---

## 1. Castle "diagonal walk" — hero body facing (commit `fa3ced7`)

**Symptom:** in MainCastle_Hall, pressing Up walked the hero ~45° to the left.

**Proven via hard data** (not theory): the hero ROOT moved dead-straight north — pressing Up
changed only world-Z (+1.13), X and Y exactly 0, and rotation snapped 178°→0° to face north.
So input, locomotion, NavMesh, and the hero were all **correct**. The *visible body mesh* was
seated ~45° off, reading as a diagonal walk.

**Root cause:** `HeroBodySwapper` hard-coded `forwardYaw = -90°` — a guessed Euler that's only
right for bodies authored facing +X. The Mage/"Blaise" default body imports facing another axis.

**Fix:** `HeroBodySwapper.AlignBodyFacingToRoot` — DERIVES the body's true forward from the
hip-to-hip skeleton vector (lateral on every humanoid → `forward = right × up`, rig-independent),
rotates by only the RESIDUAL to match the root heading. 5° deadzone + residual-based ⇒ **no-op for
already-correct heroes** (no regression), fixes a misaligned one, falls back untouched on
non-humanoid rigs. Sibling fix in same commit: `SmartMobileCamera.EnforceSoleCamera` now disables
the sibling `VillageCamera` script (it could only disable Cameras on *other* GameObjects, so the
builder's two camera rigs were fighting every frame).

**Diagnostic lesson:** when "hero walks at an angle," FIRST prove the root's world-coords
(before/after). If the root is straight, it's the body seat or the camera — NOT
input/navmesh/locomotion. (A long stretch was lost chasing camera + navmesh before checking the
body.)

---

## 2. Hero walk/idle/run loop snaps & restarts (commit `4356b55`)

**Symptom:** the walk cycle played through, then snapped to a different frame and restarted
(non-seamless loop). Same for idle/run/combat-idle.

**Root cause:** `ActionClipImporter.OnPreprocessAnimation` (the AssetPostprocessor that owns every
`Assets/Action/` clip) set `loopTime = true` but **never set `loopPose`** — so Loop Pose
(`loopBlend`) reset to OFF on every import. This is why nothing else stuck: hand-editing the
`.meta` (even with Unity closed) and a one-off ModelImporter menu both got **clobbered on the next
import**, because the postprocessor rewrites `clipAnimations` every time.

**Fix:** one line — `c.loopPose = looping;` beside `c.loopTime = looping;` in
`OnPreprocessAnimation`. Now every current and future looping Action clip is seamless with no
per-clip Inspector work.

**Verified** on the *actual imported clips* via `AnimationUtility.GetAnimationClipSettings`:
`loopBlend = True` on Shared_Idle / Walk_Forward / Run_Forward / Combat_Idle.

**Lesson:** Action-clip import settings are owned by the `ActionClipImporter` postprocessor, NOT
the `.meta`. To change import behavior for Action clips, edit the postprocessor — editing the
`.meta` or a menu one-off is undone on the next reimport.

---

## 3. Tooling notes (this session)

- Headless batchmode works with the editor CLOSED: `.\run-unity-method.ps1 -Method <X> -LogName <n>.log`.
  License 505 in the log is harmless; judge by the success marker / asset readback.
- `Defenders → Animation → Fix Action Clip Root Motion (stop slide)` and `Reimport Action Clips`
  both reimport the Action library through the (now-fixed) postprocessor.

---

## 4. Recommended next (unchanged from earlier handover, owner at the wheel for felt checks)

1. **Ramp-climb confirm** in MainCastle_Hall (upper battlements) — play → walk up.
2. **ATB live test** (`ATBBattle.unity` → Play) — damage numbers once/hit, deaths once.
3. **WO-378 Town HUD** — the headline UI push.

— All this session's fixes are owner-verified, committed, and pushed. Nothing pending.
