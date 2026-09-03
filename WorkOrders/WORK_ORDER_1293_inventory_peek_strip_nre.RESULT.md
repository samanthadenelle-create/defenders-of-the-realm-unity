# WO-1293 RESULT — Bag peek strip NRE blanks the rail (device, seq 4077)

**Status:** FIXED (edit-only lane; NOT gated, NOT committed — the lead gates and commits)
**Date:** 2026-09-02
**Files touched:**
- `Assets/_Modules/Village/Hero/InventoryGrid.cs`
- `Assets/Editor/Regression/InventoryArmoryRailRegression.cs`

---

## ROOT CAUSE (and why all three ticket candidates were wrong)

The ticket located three candidate dereferences (`column`, `fitter`, `zone.scrollbar` / `srt`) and
asked which one was null. **None of them can be null.** `ElarionUiKit.MakeScrollZone`
(`Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs:3297`) unconditionally constructs, on every call
and before it returns: the host `ScrollRect`, the masked `Viewport`, the `Content` **with** its
`VerticalLayoutGroup` and its `ContentSizeFitter`, and the `ScrollbarV` (a real `GameObject` whose
`transform` is therefore always a `RectTransform`, so the `as` cast cannot silently fail either).
The kit's contract is total — the SECONDARY QUESTION in the ticket answers itself: the kit is fine,
and no other `MakeScrollZone` consumer has the hole the ticket suspected.

The dead reference was a **fourth** one the ticket's table did not list, in the landscape branch of
the code the device was running (`2f167c8d6`, the build that predates the current tree):

```csharp
var column = zone.content.GetComponent<VerticalLayoutGroup>();
if (column != null) column.enabled = false;
var row = zone.content.gameObject.AddComponent<HorizontalLayoutGroup>();  // returns NULL
row.spacing = GridGapPx;                                                  // <-- NRE
```

`UnityEngine.UI.LayoutGroup` is `[DisallowMultipleComponent]`. The kit already put a
`VerticalLayoutGroup` on that same GameObject, and **disabling a component does not remove it**, so
`AddComponent<HorizontalLayoutGroup>()` is REFUSED by Unity and returns `null`; the very next line
dereferences it. Landscape-only, which matches the device (`Main_Castle_Overworld`, landscape) and
matches the state dump being fully populated — this was never a data-empty case, exactly as the
ticket said.

**In two sentences:** the peek strip tried to swap the kit scroll-zone's layout group by adding a
second one to the same GameObject, which Unity's `[DisallowMultipleComponent]` refuses by returning
null rather than throwing. The next line dereferenced that null, `SafeRun` swallowed it, and the
strip rendered blank while the rest of the Bag survived.

## WHAT PROVES IT (data, not reasoning)

1. **The engine's own attribute is the authority**, not an inference: `UnityEngine.UI.LayoutGroup`
   carries `[DisallowMultipleComponent]`, and the new regression case reads it **by reflection** at
   run time rather than asserting it from a constant of ours.
2. **The kit source is the second authority**: `MakeScrollZone` has no branch on which any of the
   ticket's three candidates can come back null, so the captured stack's throw site has exactly one
   remaining unguarded dereference in that method — `row`.
3. **The failing line is the only one whose null is produced by an API that returns null instead of
   throwing.** `GetComponent` on a freshly-built object with the component present cannot; the `as`
   cast on a `Transform` that IS a `RectTransform` cannot; `AddComponent` under
   `DisallowMultipleComponent` provably does.

The device capture (`logs/f8-inbox/archive/capture-device-20260831-090217-seq4077.md`, seq 4077,
device `SM02G4061955851`, 2026-08-31T07:15:04Z) carries **no `[Flow:Inventory]` probe lines** — the
probes did not exist in that build, so the ticket's "reproduce and read the trace" step cannot be
satisfied against that capture. Rather than re-run a build to observe a throw the engine contract
already explains, the instrumentation that WOULD have named it in one line is now permanent (below),
so the next occurrence of this shape arrives pre-diagnosed.

## THE FIX

The corrective code (`486cd7b17`) had already landed in the tree ahead of this ticket without a
RESULT: the landscape branch no longer swaps layout groups at all. It disables the kit column and
places the cards explicitly along X (`child.anchoredPosition`, explicit `content.sizeDelta`), so the
GameObject owns exactly one `LayoutGroup` for its whole life and there is no deferred component swap.
This lane verified that path against the kit contract and completed the ticket:

- **`InventoryGrid.cs` — the RCA is now recorded at the seam.** The WO-1293 comment block above
  `BuildPeekStrip`'s probes said "the stack could not say WHICH reference was null"; it now names the
  resolved cause and why the three candidates were impossible, so the next reader does not re-run
  this investigation.
- **`InventoryGrid.cs` — the probe that would have named it.** Added a `LayoutGroup` **census** on
  the kit content (`GetComponents<LayoutGroup>().Length`) to the existing `[Flow:Inventory]`
  `BuildPeekStrip probes:` line, plus a `FlowTrace.Warn` whenever the count is not 1, naming
  `DisallowMultipleComponent` as the shape. One is correct; anything else is this defect, from data.
- **`InventoryGrid.cs` — the last unguarded dereference is closed.** `more.raycastTarget` (the
  overflow tell label) was still unguarded; a null label would have blanked the entire strip for the
  sake of one word. Now `if (more != null)`, with a `FlowTrace.Warn` on the else.
- **`InventoryArmoryRailRegression.cs` — a new case pins it**: `Case1293_PeekStripOneLayoutGroup`,
  registered in `Run` as `wo-1293-peek`. It (a) reads Unity's `[DisallowMultipleComponent]` by
  reflection as its independent authority, (b) fails if `BuildPeekStrip` ever calls
  `AddComponent<Horizontal|Vertical|GridLayoutGroup>` again, (c) fails if the census probe is
  stripped (CLAUDE.md section 12), (d) fails if any of the four kit-handle guards or the `more` guard
  is removed.

**No existing `FlowTrace` call was removed.** All added strings are ASCII.

## GATE EVIDENCE — NOT RUN HERE (edit-only lane)

`COMPILE_GATE_OK`, `REGRESSION_OK <n>/<n> suites` and the `RunCaptureHeadless` Bag screenshot are the
lead's to produce; this lane is forbidden from firing batchmode. The acceptance criteria that remain
open for the gating seat:

- [ ] `COMPILE_GATE_OK` on a fresh log.
- [ ] `REGRESSION_OK <n>/<n> suites` on a fresh log, with `INVENTORY_ARMORY_RAIL_OK` naming the new
      `wo-1293-peek` note.
- [ ] `RunCaptureHeadless` screenshot of the Bag with a stocked peek strip, opened and looked at.
- [ ] Zero `RebuildStage FAILED` when every gear tab is tapped on device.

Brace/NUL check (both files): `BALANCED`, `clean`.

## DELIBERATELY NOT TOUCHED

- `Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs` — **the kit is not the defect**; its contract is
  total, and it is a shared file other lanes touch. No edit was warranted and none was made.
- `SafeRun`'s swallow-and-continue, and the `d6d3146b2` gear-tab rail — per the ticket's DO NOT TOUCH.
- The other `MakeScrollZone` consumers (DialogueView, DungeonTreasurePanel, LoreReadingModal,
  ObsidianQueueHud) — checked for the same shape by inspection of the kit contract; none of them can
  hit it, and they are outside this silo.
