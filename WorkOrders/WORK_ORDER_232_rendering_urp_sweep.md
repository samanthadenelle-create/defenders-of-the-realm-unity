<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 232 — Rendering & URP Sweep

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 232
**Date:** 2026-06-02
**Closes:** DEF-6, DEF-103, DEF-99, DEF-106, DEF-96, DEF-94, DEF-114

---

## Summary

Single-pass fix for all outstanding visual/rendering bugs. Most stem from URP materials not being converted after a rebake, or VillageSceneBuilder placing assets incorrectly. Run these in order — the URP fix resolves ~half the list automatically.

---

## Step 1 — Run URP material conversion (do this FIRST)

Run menu item: `Defenders > Art > Fix Polyperfect URP Materials`

This should immediately resolve or reveal the true state of:
- DEF-103: Purple wall/tower segment
- DEF-99: Purple castle door
- DEF-106: Double wall ring (may still need VillageSceneBuilder fix after)
- DEF-94: Portal color incorrect

After running, open the Village scene and screenshot. Anything still purple after this step is a separate material assignment issue.

---

## Step 2 — DEF-6: Ranger renders as purple spiky creature

**File:** `Assets/Editor/HeroAnimatorSetup.cs` and hero prefab assignment for Ranger class.

The Ranger (Sylas) model is rendering as a dark spiky mesh — wrong prefab assigned or materials not applied. Check:
1. `HeroBodySwapper.cs` — confirm `rangerPrefab` field points to the correct `_M/Prefabs_M/Characters_M/` asset
2. Run `Defenders > Setup > Hero Animator` to re-apply correct model
3. If model itself is correct but purple: rerun URP conversion (Step 1) targeted at the ranger's material atlas

---

## Step 3 — DEF-106: Double wall ring (BuildWallRing + BuildWallPerimeter both active)

**File:** `Assets/Editor/VillageSceneBuilder.cs`

`BuildWallRing` (KayKit) and `BuildWallPerimeter` (polyperfect) are both firing. Remove the `BuildWallRing` and `BuildGates` calls — `BuildWallPerimeter` supersedes them. Requires a Village rebake after the change.

Search for calls to `BuildWallRing` and `BuildGates` (old KayKit versions) and comment out / remove.

---

## Step 4 — DEF-96: Upside-down tree reappeared

**File:** `Assets/Editor/VillageSceneBuilder.cs`

A tree prefab is being placed with `rotation = Quaternion.Euler(180, 0, 0)` or similar. Search VillageSceneBuilder for tree placement code and ensure rotation is `Quaternion.identity` or correct upright value. Requires rebake.

---

## Step 5 — DEF-114: Gate bottom edges — z-fighting / floating

**File:** `Assets/Editor/VillageSceneBuilder.cs` — `BuildGates` / `BuildWallPerimeter` gate Y placement.

Gates are floating slightly above the terrain. Adjust the Y offset for gate placements to snap to ground plane. Check `IsUnderPerimeterGate` logic — ensure gate base Y = terrain Y at that position. Requires rebake.

---

## Rebake

After Steps 3–5, run one Village rebake:
`Defenders > Week 3 > Build Village Scene`

Do NOT bake with the Unity editor open.

---

## Acceptance criteria

- [ ] URP conversion run — no pink/purple polyperfect assets remain
- [ ] Ranger (Sylas) renders as correct character model
- [ ] Only one wall ring visible — no z-fighting/doubling
- [ ] Upside-down tree gone
- [ ] Gate bases flush with ground plane
- [ ] Portal renders correct color (verify after URP fix)
- [ ] Brace balance check passed on every `.cs` file edited

---

## What NOT to touch

- `Village.unity` — do not hand-edit
- Hero locomotion / combat scripts
- Any ATB or dungeon files
