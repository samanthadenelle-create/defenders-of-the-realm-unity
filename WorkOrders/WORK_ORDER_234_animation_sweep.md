<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 234 — Animation Sweep

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 234
**Date:** 2026-06-02
**Closes:** DEF-5, DEF-95

---

## DEF-5: Hero walk animation never fires

**File:** `Assets/Editor/HeroAnimatorSetup.cs` (truncated at line 155 — must be repaired first)

The file ends mid-string inside `BuildController()`. CLI must restore the full file before any animation work can proceed.

1. Restore `HeroAnimatorSetup.cs` to a complete, brace-balanced state.
2. Confirm `HeroLocomotion.cs` calls `animator.SetFloat("Speed", velocity.magnitude)` each frame.
3. Run `Defenders > Setup > Hero Animator` to rebuild the animator controller with the correct walk/idle/run clips from `Resources/Heroes/<class>`.
4. Verify in Play mode: moving hero transitions from idle to walk animation.

---

## DEF-95: Pets traveling in reverse

**File:** `Assets/_Modules/Pets/PetLocomotion.cs` or equivalent pet movement script.

Pets move in the wrong direction — likely the waypoint traversal direction is inverted, or the pet's forward vector is flipped.

1. Check `PetLocomotion` movement direction: `transform.position += transform.forward * speed * Time.deltaTime` — confirm `transform.forward` is not negated.
2. Check waypoint list order — if pets traverse `waypoints[waypoints.Count - 1]` down to `[0]` instead of the reverse, flip the iteration direction.
3. If the pet model itself faces backward (root rotation issue), add `modelRoot.localRotation = Quaternion.Euler(0, 180, 0)` as a one-line fix rather than touching locomotion logic.

---

## Acceptance criteria

- [ ] Hero transitions to walk animation when moving, returns to idle when stopped
- [ ] Pets move in the correct forward direction along their paths
- [ ] Brace balance check passed on every `.cs` file edited

---

## What NOT to touch

- `Village.unity` — do not hand-edit
- `WaveManager`, `EnemyBrain`, ATB scripts
