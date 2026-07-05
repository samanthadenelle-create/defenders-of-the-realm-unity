# ⚠ WORK ORDER 332 — DTT Aim / Camera Rotation Sensitivity Reduction — **SUPERSEDED 2026-07-04**

> **SUPERSEDED:** The Defend-the-Tower / PatriciaLight system was removed 2026-06-09.

**Status:** CLOSED — SUPERSEDED (system removed 2026-06-09)  
**Lane:** 2 (Combat/AI) — code-only, parallel-safe  
**Scene:** PatriciaLight_TD  
**Priority:** MEDIUM — camera feels uncontrollable at current sensitivity; combat precision lost

---

## Problem

The aim/camera rotation in Defend the Tower is too sensitive on Windows (mouse).
Small mouse movements swing the view/aim angle too far, making it hard to track
individual enemies.

Also note WO-318 (aim stays north + head-only pivot clamp) is related — fix WO-318
first if these share the same controller, then tune sensitivity as part of this WO
or fold them together.

---

## Desired Behaviour

- Horizontal mouse delta multiplied by `_aimSensitivity` (default: **0.8** — tune down
  from whatever it currently is)
- Vertical look: clamped to ±40° (turret stance — don't let the hero look straight up/down)
- Sensitivity exposed as `[SerializeField] private float _aimSensitivity = 0.8f;` so it
  can be tweaked in the Inspector without code changes
- Optionally: separate `_horizontalSensitivity` and `_verticalSensitivity` if the feel
  needs asymmetric tuning

---

## Acceptance Criteria

- [ ] Default sensitivity set to a value where a full mouse sweep (~20 cm) rotates ~180°
      (current appears to be ~90° per centimetre — needs to be roughly 5–8× slower)
- [ ] Sensitivity is a serialized inspector field
- [ ] Vertical look is clamped (no flipping upside-down)
- [ ] No regression to WO-318 aim-north fix (if already landed)
- [ ] Works the same on both mouse and touchpad

---

## Files to Edit

```
Assets/_Modules/BattleATB/PatriciaLight_TD/   ← find the camera/aim controller
  (likely: DTTCameraController.cs, PatriciaLightCamera.cs, TowerDefendCamera.cs, or similar)
```

Search for `mouseDelta`, `Input.GetAxis("Mouse X")`, or `_sensitivity` to locate the
current multiplier. Lower it or make it configurable.

## What NOT to Touch

- Village camera (VillageCamera.cs) — different controller
- WO-319 DTT firing animation — separate WO
