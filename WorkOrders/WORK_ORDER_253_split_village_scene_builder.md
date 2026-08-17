<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — SUPERSEDED
> **Superseded by:** work that has already shipped. **Git first-add:** 2026-06-22.
> **Evidence:** third copy of the same request; the partial-class files listed above are in tree. (Co-claimant of WO-253 — `WORK_ORDER_253_tutorial_speech_bubble_overlay.md` is untouched.)
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

> ⚠ **UNRESOLVED NUMBER COLLISION — WO-253 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_253_split_village_scene_builder.md`, `WORK_ORDER_253_tutorial_speech_bubble_overlay.md`
> Both files were added in the SAME commit (first-on-disk is a dead tie) and neither is cited by any other doc, RESULT file, or commit message — there is no evidence on either side.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WO-253: Split VillageSceneBuilder into partial-class files
**Linear:** [DEF-154](https://linear.app/defenders-of-the-realm/issue/DEF-154/wo-207-split-villagescenebuilder-into-partial-class-files)
**Lane:** World/Environment
**Status:** SUPERSEDED by work that has already shipped (era sweep 2026-08-17)
**Priority:** Urgent — BOTTLENECK UNBLOCK

## Why this is first

`VillageSceneBuilder.cs` is the serialization bottleneck (CLAUDE.md §9). Only ONE agent can touch it at a time. Every World/Environment issue (21 total) is blocked behind it. Splitting into partial classes lets multiple agents work on separate builder methods without merge conflicts.

## Acceptance Criteria

- [ ] `VillageSceneBuilder.cs` split into partial-class files by functional area
- [ ] Suggested split: `VillageSceneBuilder.Walls.cs`, `VillageSceneBuilder.Gates.cs`, `VillageSceneBuilder.Buildings.cs`, `VillageSceneBuilder.Trees.cs`, `VillageSceneBuilder.NPCs.cs`, `VillageSceneBuilder.Terrain.cs`, `VillageSceneBuilder.Core.cs` (main entry + shared helpers)
- [ ] All partial files use `public partial class VillageSceneBuilder`
- [ ] `namespace DeNelle.Editor` on every file
- [ ] Batchmode compile passes: `DeNelle.Editor.VillageSceneBuilder.BuildVillage` still works
- [ ] Village rebake produces identical scene output (no visual diff)
- [ ] Brace balance check passes on every new file

## Files to Edit

- `Assets/Editor/VillageSceneBuilder.cs` → split into 6-8 partial files in same directory

## Do NOT Touch

- Village.unity (never hand-edit)
- Any runtime code outside `Assets/Editor/`
- Do not rename the class — only split into partials
