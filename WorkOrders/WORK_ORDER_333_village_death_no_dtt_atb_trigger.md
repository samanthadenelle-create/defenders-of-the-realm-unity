# ⚠ WORK ORDER 333 — Re-wire Tree of Life <30% HP → Defense Modal (Broken) — **CHECK CANON 2026-07-04**

> **NOTE:** This WO references "no DTT/ATB trigger" but the Defend-the-Tower system was removed 2026-06-09. Verify this WO is still valid against current combat flow (BattleArena vs ATB).

**Status:** REVIEW — verify against current canon (no DTT)  
**Lane:** 2 (Combat/AI) — code-only, parallel-safe  
**Priority:** HIGH — core escalation loop broken; modal never fires  
**Screenshot evidence:** docs/screenshots/village_death_screen.png  
**Nature:** FIX — logic existed and worked; something broke it. Do NOT rewrite from scratch.

---

## What Should Happen (was working)

When the **Heart of Elarion** (Tree of Life, `HeartController`) drops below **30% HP**:
1. A **modal window** opens asking the player which defense mode to enter:
   - **Defend the Tower** → loads `PatriciaLight_TD` via `SceneRouter`
   - **Enter Battle (ATB)** → loads `ATBBattle` via `SceneRouter`
   - *(Retreat / dismiss was probably also an option — confirm in code)*
2. The modal pauses the scene while the player decides.
3. Choosing a mode saves state (via `SceneRouter.LoadSceneWithFade`) and transitions.

---

## What's Broken

The modal is never shown even when the heart takes heavy damage. The death screen
("THE ROOT WENT SILENT") fires instead — implying the heart reaches 0 HP before
the 30% modal ever triggers.

---

## Investigation Checklist (run in order)

1. **Find the threshold check** — search `HeartController.cs` for `0.3f`, `0.30`, `30`,
   `LowHpThreshold`, or any similar constant. Confirm it exists.

2. **Find where it fires** — the threshold check should invoke an event or method.
   Look for: `OnHeartCritical`, `HeartBelowThreshold`, `TriggerDefenseModal`,
   `OnLowHealth`, or a `UnityEvent` named similarly.

3. **Find the modal** — search for a `DefenseModal`, `DefenseChoiceDialog`,
   `EscalationModal`, or similar MonoBehaviour/UIDocument. Check if the scene
   reference to it is null (most likely cause of silent failure).

4. **Check the wiring** — the event in step 2 must have the modal's `Open()` (or
   equivalent) subscribed. If using `UnityEvent`, open the scene and check the
   inspector for missing/null listeners. If using C# events, check `OnEnable`/
   `OnDisable` subscription in the modal component.

5. **Check `SceneRouter` calls** — confirm `SceneRouter.LoadSceneWithFade("PatriciaLight_TD")`
   and `SceneRouter.LoadSceneWithFade("ATBBattle")` still exist and the scene names
   match the actual build settings.

---

## Most Likely Root Cause

Scene reference to the modal GameObject went null after a VillageSceneBuilder rebuild.
The event fires into the void and the modal is never shown.

Fix: re-wire the modal reference in the scene (or switch from scene-wired `UnityEvent`
to a `FindObjectOfType<>` call + `null` guard in `HeartController` so it doesn't
silently die on a missing reference).

---

## Secondary Bug (from screenshot)

The skill-point panel (Level 2! Spend a skill point) remains open behind the death overlay.
On `HeartController.OnHeartDestroyed` (HP == 0) force-close all open HUD panels before
showing the "THE ROOT WENT SILENT" screen. This is separate from the modal fix above.

---

## Acceptance Criteria

- [ ] When Heart of Elarion drops to ≤30% HP, the defense modal opens (not at 0%)
- [ ] Modal offers at minimum: **Defend the Tower** and **Enter Battle (ATB)**
- [ ] Each choice transitions via `SceneRouter` (saves state before scene load)
- [ ] Modal does NOT fire again if already dismissed once per session
- [ ] Skill-point panel and any other open HUD panels close before the death screen shows
- [ ] No regression to Village.unity scene file (no hand-edits)

---

## Files to Investigate (do not rewrite unless truly missing)

```
Assets/_Modules/Village/Buildings/HeartController.cs   ← threshold check + event
Assets/_Modules/Village/UI/DefenseModal.cs             ← (or whatever the modal is named)
Assets/_Modules/Core/SceneRouter.cs                    ← LoadSceneWithFade calls
Assets/_Modules/Village/                               ← search for "30" + "modal" + "defense"
```

## What NOT to Touch

- Village.unity scene file (use SceneBuilder menu or inspector only)
- TowerSwapService, WalletService, monetization code
- CLAUDE.md
