# WO-1522 RESULT - the authoring note can no longer reach the player, and the magenta was never a missing icon

**Status:** PARTIAL - uncommitted in the working tree as of 2026-09-06 21:45, awaiting the wave-two gate.
**Tree contradicts the ticket:** its Status line still reads `READY TO IMPLEMENT` while the work sits in the tree.
(Status line not edited here - RESULT-only lane.)
**Commit:** none. Edit-only lane.
**Files:** `Village/Talents/HeroSkillTreeVM.cs` (`M`, +60 - `:218,245-250,268`),
`Village/Talents/HeroSkillTreePanelMvvm.cs` (`M`, +153 - `:1316,1327`),
`Village/Talents/TalentNodeVfxRig.cs` (`M`, +52 - `:152,167,214`),
`Village/Hero/HeroInventoryController.cs` (`M`, +22),
`Assets/Editor/Regression/SkillsPanelLayoutRegression.cs:660,688,702`.
**Gates:** none. `Builds/cg-quiet.log` `COMPILE_GATE_OK` is 20:04 and the owner's ask arrived 20:23, so the gate
predates the lane. `Builds/cg-aab.log` (20:54) is RED (42x `CS0103`, the Manage lane's half-written suites).

## 1. CORRECTION to the ticket's evidence - the `icons` row was wrong (CLAUDE.md sec.11B)

The frame does not show a missing sprite. A node with no sprite draws a TWO-LETTER MONO LABEL
(`HeroSkillTreePanelMvvm.BuildGraphNode`, the `LoadIcon(...) == null` branch) and can never draw a texture; a
missing SHADER draws flat magenta, not tiles. The frame shows a multi-coloured TILE GRID filling a square around
exactly two plates - the focused one and the single owned one - the focused square visibly larger. That is
`BuildVfxPatch`: peek **0.35** for the pointer rig, **0.25** for the aura rig. The seam is `TalentNodeVfxRig`'s
RenderTexture bound to a `RawImage`. Proving lines, `Logs/device/freeze-20260904-095249.log`:
`07:48:40.676 [Flow:TalentPointer] attach: rig live` and `07:48:45.911 [Flow:TalentAura] attach: rig live`.

**NOT PROVEN:** why the render leaves the texture undefined on device - no capture names a failing call and no
`Camera.Render` / URP warning appears in any `Logs/device/*.log`. The fix therefore closes the CONSEQUENCE, not the
cause: the RT is cleared to the well ink on create and re-created + re-cleared on device context loss
(`TalentNodeVfxRig.cs:152,214`), so an undefined surface can never be sampled. `:167` states in-file that this is
not "tinting the magenta" (sec.3) - it is a different component from the one sec.3 forbids touching. A
`FlowTrace.Step` on the first `RenderTick` was added so the next device log says whether the draw executes at all.
**Consequently acceptance item 4's `KnownGaps` clause does not apply** - `MageAbilityIconRegression` is an
uncommitted lane and this was never an icon defect. Left untouched.

## 2. The authoring note

`HeroSkillTreeVM.cs:245-250` quotes the shipped string verbatim - `"NO EFFECT YET - not implemented yet (data note:
'v2'). C"` - and the VM now composes a player line saying the WORD **COMING** (`HeroSkillTreePanelMvvm.cs:1316`,
stated in words never a hue since the owner is red/green colourblind), with the dev reason routed to FlowTrace only
(`:1327`). The dev tokens are listed at `:218` (`"not wired"`, `"not implemented"`, `"hidden until"`).
`SkillsPanelLayoutRegression.cs:688-702` is the source lint - no player-facing string in the Talents silo may be
built from an authoring note - and `:702` records the current string as the RED fixture.

## 3. Acceptance

- [ ] Headless `SkillTree_*` and `Loadout_*` PNGs captured and OPENED - **OPEN**, no capture run.
- [ ] Measured no-overlap case; no label truncated - **OPEN**. The panel gained +153 lines but no measured overlap
      case is registered; sec.2 items 3/4/5 (label fit, tree fill, dialog anchored beside the node, the loadout
      rail) are not evidenced in the tree.
- [x] The source lint on authoring notes exists; RED proof stated with the current string - `:688-702`.
- [n/a] Zero magenta nodes / `KnownGaps` shrinks - void per sec.1; the defect was the RT, not an icon.
- [ ] `REGRESSION_OK n/n` on a fresh log - owed.

## 4. Owed

The wave-two gate; the layout half (dialog anchoring, label fit, loadout rail) as its own pass; a headless
`SkillTree_*` PNG opened; and one device log read for the new first-`RenderTick` line - the only thing that will
say whether the RT draw executes at all.

## 5. Second pass - 2026-09-06: the flatness, measured as a luma ladder

**Contradicts sec.3 above:** `AnchorPopupBesideNode` and the two-line `FitBlock` on the rail's ability
name are ALREADY in the tree (first pass, +153), so items 3 and 4 are built. What was missing was DEPTH:
the graph viewport painted a near-black slab with NO edge; the loadout slots sat on the bare panel frame.

**The ELEVATION LADDER** - TWO authored surfaces, deliberately not three: `WellSurface` (recessed graph
well) below `RaisedSurface` (the loadout shelf), the frame's own textured centre being the rung between.
A third constant was written and then DELETED: a plate under the graph is 100% occluded by the opaque
viewport filling the same rect, and one over the workspace would re-cover what
`MedievalUiSkin.ApplyShell` uncovers - a constant for an invisible surface is a hollow assertion. Depth
is carried only by what greyscale keeps (the owner is red/green colourblind): LUMINANCE, SIZE
(`NodeFocusPx` 168 vs `NodeSizePx` 136), POSITION - no hue. One builder, `BuildElevationPlate`; plate and
bezel stay TWO images (collapsing them is the WO-1515 tan slab) and every plate is raycast OFF.
`SkillsPanelLayoutRegression` case 8 `[elevation]` is the fixture: it reads both surfaces live and asserts
the ORDER, a Rec.709 luma gap of 2x `ElevationLumaStep`, full opacity, the focus/normal size ratio, the
three built surfaces, and that no FILL plate goes under the viewport (dead paint).
**Files:** `HeroSkillTreePanelMvvm.cs`, `SkillsPanelLayoutRegression.cs` (now 8 cases). `TalentNodeVfxRig`
and the icons UNTOUCHED - sec.1 proved the magenta was the RT. **No new registration**; `DataRegression.cs`
untouched. **Still open:** item 5's drag/tap-to-assign is a FEATURE, not this pass; headless PNGs opened;
the first-`RenderTick` device line.
