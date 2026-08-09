# WORK_ORDER_385 — Castle camera "fights the castle": world-locked seat + wall-collision jam

**Status:** READY TO IMPLEMENT - UNVERIFIED (reconciled 2026-08-09 - the claimed fix commit `4291324` does NOT resolve in this repo's history (git: fatal: ambiguous argument), and no other commit references WO-385, so the FADE FIX LANDED claim below cannot be confirmed from the tree. Re-verify the symptom before re-implementing)

**Status:** FADE FIX LANDED (commit 4291324, COMPILE_GATE_OK) — PENDING OWNER PLAYTEST. Global, not castle-only.
**Lane:** Camera / Hero (DeNelle.Village.Hero) — `SmartMobileCamera.cs`. Code-only, no scene/bake.
**Source:** This session (2026-06-09)
**Related:** WO-383 (seam teleport — a *separate* edge event). This WO is the *in-castle* angle-loss.

---

## Symptom (owner playtest, 2026-06-09)
At the castle spawn the camera/angle is correct. Walking **out** (toward the gate/OuterWorld) stays fine. Walking **back toward/into the castle**, the camera angle "gets lost" — and the **lost state persists** even after backing off (only respawn/scene-reload clears it). "The camera is fighting with the castle."

## Root cause (code-confirmed)
`SmartMobileCamera`:
- The follow seat is **world-locked behind the hero on −Z** — `_orbitBehind = false` (line 139), so the camera does NOT swing to stay behind the hero's actual facing/travel. It always sits on the hero's −Z side.
- `ApplyCollision` (line 667) spherecasts pivot→seat against `_collisionMask` (**Default + Building + Tower**, line 188) and pulls the seat IN fast (`_collisionApproachSpeed = 40`), eases OUT slow (`_collisionReturnSpeed = 8`), clamped to `_minCollisionDistance = 1.2m`.

Interaction that produces the bug: the castle gate/exit is on the −Z (south) side; OuterWorld is further −Z.
- **Out (−Z):** camera trails into open field → no occluder → fine.
- **Back in (+Z):** the −Z-locked camera lags on the gate side, so the **south gate + perimeter walls sit between camera and hero** → spherecast hits them every frame → camera jams to 1.2m ("lost"). Moving deeper in keeps *more* castle geometry between the fixed-south camera and the hero, so the slow ease-out never wins → **persists.**

Net: a world-locked camera can't reach the hero's open side inside an enclosed structure, and the wall-collision then jams on the dense castle geometry.

## Fix (proper — address the root, don't just disable collision)
Disabling collision alone would stop the jam but let the camera clip through walls (the very thing DEF-151 added it to prevent). Do both:
1. **Keep the camera behind the hero inside enclosed hubs.** Drive the seat to stay behind the hero's **facing** (NOT velocity — velocity-chasing caused the retired "always-turn-left" curl/spiral; see `CAMERA_INPUT_OVERHAUL.md` §2 + the `_orbitBehind` comments). Options: enable a facing-driven orbit/recenter for the castle (`_facingRecenterEnabled` exists, currently off), or a dedicated "enclosed/hub" framing mode. This keeps the camera on the open side so the occluder rarely sits between camera and hero.
2. **Lighten the collision for the hub** so interior structures you walk *among* don't yank the view: e.g. restrict `_collisionMask` to the outer perimeter only (drop Building/Tower for the hub), and/or replace pull-in with **wall-fade** (fade obstructing geometry, keep the camera at offset — the AAA approach, cf. the "camera over walls" WO-156/204 lineage).

Keep **movement world-absolute** (WO-368) and the **"close 3rd-person, pull up only for base-build"** decision intact. This is a felt change — ship behind a flag if needed and owner playtests.

## As-built — 2026-06-09 (commit 4291324, COMPILE_GATE_OK) — PENDING OWNER PLAYTEST
Implemented the occluder-FADE half (higher-leverage, lower-risk, global), NOT the orbit/recenter change:
- `SmartMobileCamera.ApplyCollision` now `SphereCastNonAlloc` pivot→seat and **fades occluding renderers to `ShadowsOnly`** (exact-restore via `Dictionary<Renderer,ShadowCastingMode>` the instant they stop occluding), keeping the camera at its proper seat/angle. The old hard pull-in survives ONLY as a point-blank (`< _occluderPullInDistance` 0.6m) safety backstop. Gated behind `_collisionEnabled`; restores all faded renderers on disable/destroy/target-loss.
- GLOBAL — every corner, every scene (village, dungeons, camps), not castle-only.
- No movement / yaw / `_orbitBehind` changes.
- Mis-read corrected: the camera force-enables orbit-behind at runtime (`_forceCameraFix`) with `_facingRecenterEnabled` OFF, so it holds the player's last pan-yaw rather than auto-swinging behind the hero's facing. If, after playtest, the camera still doesn't *swing* behind you at corners (distinct from the wall-jam this fixes), enabling `_facingRecenterEnabled` is the follow-up lever — its own small change.

## Acceptance criteria
- [ ] Returning into the castle keeps a clean third-person angle — no jam-to-close, no persistent "lost" state.
- [ ] Camera does not clip through / embed in castle walls (collision intent preserved via fade or perimeter-only mask).
- [ ] No "always-turn-left" curl/spiral regression (yaw must not be velocity-driven).
- [ ] Movement stays world-absolute; spawn + going-out behaviour unchanged.
- [ ] Owner playtest confirms the feel.

## What NOT to touch
- Do not make movement camera-relative (WO-368).
- Do not re-introduce velocity-driven yaw (the curl).
- No scene hand-edit / no bake needed — this is `SmartMobileCamera.cs` behaviour only.
