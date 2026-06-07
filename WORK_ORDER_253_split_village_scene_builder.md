# WO-253: Split VillageSceneBuilder into partial-class files
**Linear:** [DEF-154](https://linear.app/defenders-of-the-realm/issue/DEF-154/wo-207-split-villagescenebuilder-into-partial-class-files)
**Lane:** World/Environment
**Status:** READY TO IMPLEMENT
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
