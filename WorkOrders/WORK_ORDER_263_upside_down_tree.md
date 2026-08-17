<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** Village.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/Village.unity` is absent from disk and from `git ls-files`; acceptance is "absent from Village scene hierarchy after rebake".
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WO-263: Upside-down tree asset reappeared in scene
**Linear:** [DEF-96](https://linear.app/defenders-of-the-realm/issue/DEF-96/upside-down-tree-asset-reappeared-in-scene)
**Lane:** World/Environment
**Status:** CLOSED — OBSOLETE: Village.unity no longer exists (era sweep 2026-08-17)
**Priority:** High

## Acceptance Criteria
- [ ] Upside-down tree GameObject is absent from Village scene hierarchy after rebake
- [ ] `VillageSceneBuilder.cs` contains no call that places that specific asset at an inverted rotation
- [ ] Scene rebaked and tree does not reappear
- [ ] Confirmed in Play mode — no inverted tree visible from any camera angle

## Files to Edit
- `Assets/Editor/VillageSceneBuilder.cs` — find and fix the tree placement call with inverted Y rotation

## Do NOT Touch
- Village.unity (never hand-edit — fix via VillageSceneBuilder then rebake)
- Files outside World/Environment lane

## Dependencies
- VillageSceneBuilder.cs is a serialization bottleneck (CLAUDE.md S9) — coordinate with any other World/Environment WOs touching VSB
- Requires a Village rebake after fix
