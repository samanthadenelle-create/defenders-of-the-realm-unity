# WORK ORDER 156 — Village Camera: clear & pivot over the high castle walls

**Status: READY TO IMPLEMENT**
**Priority:** HIGH — playtest blocker. Camera is buried inside the new tall wall; the view is a stone slab and the player can't see/reach buildings ("nothing opens").
**Date:** 2026-05-30
**Lane:** Combat/Camera — **code only (`SmartMobileCamera.cs`). No `VillageSceneBuilder`, no bake by UI.**
**Source:** owner playtest screenshot + *"nothing opens and camera needs to pivot on these high walls."*

---

## ⚠ STATUS 2026-05-31 — NOT IMPLEMENTED YET (owner: "wall camera fix didn't work")
Verified: **no RESULT file, and `SmartMobileCamera` is unchanged** — still `_followOffset = (0, 5.5, -9)`,
**none** of Part A (lift above wall + clip-avoidance), Part B (orbit/pitch), or Part C (wall-fade) was
added. So the fix "not working" = **it hasn't been built.** This WO is still open — implement it.

**Also — a CAMERA CONFLICT to resolve first (new finding):** there are now THREE camera controllers —
`SmartMobileCamera` (`EnforceSoleCamera` disables others), `CinemachineCameraController` (WO-87, a vcam
stack), and **`HeroCinemachineRig` (priority 100)** which "owns the hero's OTS rig and takes priority."
With a priority-100 Cinemachine rig live, `SmartMobileCamera`'s offset may be **overridden entirely** —
which is likely why the view is wrong (pitched at the horizon, hero off-screen). **CLI must first decide
which camera is authoritative in the village**, then implement the over-wall framing on THAT one (don't
tune SmartMobileCamera if a priority-100 Cinemachine rig is actually driving the view). The current symptom
(camera aimed at the brown sky, hero below frame) points at the wrong camera being live / no over-wall
pitch on whichever wins.

## Root cause (verified in code)

The live village camera is **`SmartMobileCamera`** (`Assets/_Modules/Village/Hero/SmartMobileCamera.cs`;
`EnforceSoleCamera()` disables `VillageCamera` and owns the view). Two facts break it against the new castle:

1. **Offset is shorter than the new walls.** `_followOffset = (0, 5.5, -9)` — 5.5 m up, 9 m back. The
   WO-136 castle wall is **5 m tall + ~1.4 m parapet ≈ 6.4 m**. So the camera sits **below the wall top**
   and 9 m back lands it **inside/behind the stone** → the pale-slab view in the screenshot.
2. **No obstruction handling + no pivot.** The camera has **no** wall raycast/clip avoidance (it never
   checks for geometry between it and the hero) and **no orbit/pitch/pivot input** at all — it's a fixed
   offset. So it can neither auto-clear the wall nor be rotated over it.

Consequence chain: camera buried in wall → player can't see or navigate to buildings → building [F]
interactions never get reached → **"nothing opens."** Fixing the camera fixes both symptoms.

---

## Fix — two parts

### Part A (immediate) — lift the camera above the wall line + clip avoidance
- Raise the idle framing so the camera sits **above the parapet (≥ ~8–9 m up)** and looks **down into**
  the village over the walls — a higher, steeper pitch than the old low-wall offset. Tune so the whole
  walled interior reads from above the battlements, not from inside them.
- Add **camera-obstruction handling**: a `SphereCast`/`Raycast` from the look-at target to the desired
  camera position; if a wall/structure is between, pull the camera in (or up) so it never renders inside
  geometry. Standard third-person clip-avoidance. (The scan should hit the wall-barrier/parapet/building
  colliders — which now exist on the visible wall per WO-136.)

### Part B (the ask) — pivot/orbit over the high walls
- Add **orbit + pitch input** so the player can rotate the camera around the hero and tilt it to look
  over/around the tall walls:
  - **Mobile:** two-finger drag = orbit/pitch (the project uses Lean.Touch — reuse that driver pattern,
    same as the build-mode/aim cameras). One-finger stays movement.
  - **Desktop:** RMB-drag or arrow/Q-E = orbit, mouse-wheel = the existing zoom.
- Clamp pitch to a sane band (e.g. ~20°–70°) so it can look down into the courtyard from above the
  parapet but never flips under the ground or stares at the skybox.
- Keep the existing **combat-zoom** behavior; orbit composes on top of it (orbit sets the *angle*, combat
  zoom sets the *distance*).

---

## Part C (owner decision 2026-05-30) — walls FADE transparent when they block the view

Keep the full-height castle walls (do **not** lower them) — instead, **walls between the camera and the
hero fade to transparent**, dynamically and per-wall, so the player never loses sight of their character.
Owner: *"as you get out of lens of camera it compensates"* — it's continuous and live, not a fixed state.

> **Why fade, not lower (owner 2026-05-30):** the full wall height is a *gameplay-readable signal* —
> *"the full walls really play well into seeing how defensive your village is from a distance."* Wall
> height communicates defensive investment at a glance: a tall wall reads as a fortified, hold-strong
> base; a short one reads as vulnerable. This matters doubly once node settlements + raids exist (WO-159/
> 160) — a tall wall is a **visible flex** ("well-defended, don't bother") and silhouette-readable from
> across a zone. Lowering the walls would erase that signal. So the camera bends around the walls (fade),
> the walls never shrink. This is the locked rationale — fade is correct, lowering is the last-resort
> fallback only.

**Mechanic:**
- Each frame (throttled), determine which wall/structure segments sit **between the camera and the
  hero** — a `SphereCast`/`Raycast` (or camera-frustum-vs-hero occlusion test) from camera → hero;
  anything it hits that's a wall/structure is "occluding."
- **Occluding segments fade OUT** to transparent (or a dither/cutout fade); the moment a segment is no
  longer between camera and hero, it **fades back IN**. As the player moves/orbits (Part B), the occluding
  set updates live — the view "compensates" continuously.
- **Per-wall, not the whole ring** — only the specific segments blocking the hero fade, so the rest of
  the castle stays solid and imposing. This preserves the WO-136 "real castle" feel AND keeps the hero
  always visible — the have-both answer (chosen over lowering the walls).

**Implementation notes:**
- Fade needs the wall material to support transparency (a fade/dither mode). Polyperfect wall mats are
  URP/Lit — either toggle surface type to Transparent + animate alpha, or use a **dither/alpha-cutout**
  fade (cheaper, no sort issues, reads fine on mobile). Recommend dither cutout for perf + no transparency
  sorting headaches with the stacked wall segments.
- The wall-barrier **collision (WO-136) is unaffected** — only the *visual* fades; the hero/enemies still
  collide with the wall whether it's faded or not. (Fade = renderer alpha only, never disable the collider.)
- Smooth the fade (lerp over ~0.15–0.25 s) so segments don't pop as the occluding set changes.
- Throttle the occlusion test (not necessarily every frame); reuse the same cast Part A uses for camera
  clip-avoidance if convenient.
- Fallback (only if dither/fade proves too fiddly): lower walls ~5m→2.5m. **Not preferred** — fade is the
  chosen path; lowering is the safety net.

## Constraints
- `SmartMobileCamera.cs` only (one file); no scene edit, no bake (the camera is added by the builder but
  its *values/behavior* are code — no `VillageSceneBuilder` change needed).
- Reuse the existing Lean.Touch input pattern (no new input system); reuse the combat-zoom + shake seams.
- Don't reintroduce `VillageCamera` as a second live camera — `SmartMobileCamera` stays sole.
- No UXML, no `System.Reflection`. Brace-gate; CLI compile-verifies + bakes/commits.

## Acceptance criteria
1. On village load the camera sits **above the wall/parapet** and looks down into the interior — no
   stone-slab/buried view; the buildings + hero are clearly visible.
2. Camera **never renders inside** a wall/building — obstruction `SphereCast` pulls it clear.
3. Player can **orbit + pitch** the camera (two-finger mobile / RMB-drag desktop) to look over and around
   the high walls; pitch clamped to a sane band.
4. With the camera fixed, the hero can navigate to buildings and **[F] interactions open** (Store/Forge/
   Pet Home/Tower) — confirms the "nothing opens" symptom is resolved by the camera fix (if interactions
   still fail with a clear camera, that's a separate WO — flag it).
5. Combat-zoom still works; orbit composes with it; no second camera active.
6. **Walls between camera and hero fade transparent** (per-wall, dynamic) and fade back in when no longer
   occluding — the hero is never hidden by a wall; the rest of the castle stays solid. Wall **collision is
   unaffected** (visual-only fade). Full wall height retained (NOT lowered).
7. Brace balance on `SmartMobileCamera.cs` (+ any wall-fade helper); no scene/bake side effects.

## Note on the 54 console errors
The screenshot also shows **54 errors / 22 warnings** in the console. Those are **out of scope here** and
need their own triage — they may or may not relate to the camera. **Flag to CLI:** capture the console
text / `Editor.log` so the errors get their own WO; don't assume this camera fix clears them.

## Done checklist (CLAUDE.md §10)
- [ ] Camera clears the wall (idle framing above parapet, looks down in)
- [ ] Obstruction SphereCast prevents rendering inside geometry
- [ ] Orbit + pitch input (mobile two-finger / desktop RMB), pitch clamped
- [ ] Building [F] interactions reachable with the fixed camera
- [ ] Combat-zoom intact; SmartMobileCamera sole; brace balance
- [ ] `WORK_ORDER_156_camera_pivot_high_walls.RESULT.md` when complete
