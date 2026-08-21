<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 335 — ATB Battle Scene: Remove Stray Purple Capsule Primitive

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Lane:** 2 (Combat/AI) — code-only, parallel-safe
**Scene:** ATBBattle
**Priority:** HIGH — visually broken; large purple primitive visible in production game view
**Screenshot evidence:** docs/screenshots/atb_capsule_bug.png (2026-06-07)

---

## What's Wrong

A large purple/violet pill-shaped primitive is floating in the right side of the
ATBBattle game view alongside Elara (player) and a Skeleton enemy. It is clearly
not a designed element — it's a stray Unity Capsule primitive from development.

Screenshot shows:
- Title: "The Last Stand"
- Elara (hero model, correct) on left
- Skeleton (enemy model, correct) center-right
- **Large purple capsule/pill** floating to the right of the Skeleton, near the
  right edge — no label, no HUD entry, not present in the enemy roster panel

---

## Root Cause Candidates (investigate in order)

1. **Forgotten test GameObject** — a placeholder Capsule mesh that was used during
   early ATB scene setup and never removed. Search ATBBattle hierarchy for any
   `Capsule`, `Primitive`, or `Placeholder` named GameObjects.

2. **URP shader mismatch** — a capsule with a Standard (Built-in) shader material;
   URP can't render it correctly so it shows as a solid unlit colour. Would appear
   purple/pink if the fallback colour happens to be violet.

3. **Second enemy / spawn placeholder** — a spawned enemy whose model failed to
   load, falling back to a primitive Capsule with its default material. Check
   ATBCombatManager or EnemySpawner for any `CreatePrimitive(PrimitiveType.Capsule)`
   calls left in from debug builds.

4. **Physics collider gizmo rendered in-game** — a CapsuleCollider with
   `hideFlags` not set to `HideInHierarchy`, or a Debug gizmo that runs in Play
   mode. Unlikely but possible.

---

## Fix Steps

```
1. Open ATBBattle.unity in the Unity editor (NOT Village.unity)
2. Window → Scene Hierarchy → search "Capsule" — note all matches
3. For each match: inspect > if it has a MeshRenderer + no gameplay purpose → DELETE
4. Also search: "Primitive", "Test", "Debug", "Placeholder" in hierarchy
5. Search codebase for CreatePrimitive(PrimitiveType.Capsule) — remove or guard
   with #if UNITY_EDITOR
6. Play-test: confirm capsule is gone in Game view
7. Check that brace-balance passes on any .cs file edited
```

---

## Acceptance Criteria

- [ ] Large purple/violet capsule no longer visible in ATBBattle Game view
- [ ] Skeleton and Elara still render correctly
- [ ] No regression to battle logic, ATB timer, or combat HUD
- [ ] If cause was a URP shader mismatch on a needed object: fix the material, do
      not just delete the GameObject

## What NOT to Touch

- Village.unity scene file
- WaveManager, VillageHudController, TowerSwapService
- Any monetization or EventTracker code

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `AtbCombatantSwapper.cs:1-16; ATBBattle.unity` — no stray capsule. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
