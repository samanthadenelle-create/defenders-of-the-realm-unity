# WO-1293 — Bag peek strip NRE blanks the rail (device, seq 4077)

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED — root cause was NOT a kit-handle null: `AddComponent<HorizontalLayoutGroup>` on the kit scroll content (LayoutGroup is `[DisallowMultipleComponent]`) returned null and `row.spacing` threw; the explicit-X placement path is in, the remaining derefs are guarded, a LayoutGroup census probe + a regression case pin it.
**Minted:** 2026-09-01 (CLI, main-line banner bumped 1293 -> 1294 in the same edit)
**Silo:** Hero inventory UI. Disjoint from the WO-1289..1292 art lane and PROD-021.
**Source:** F8 device capture seq 4077 (`SM02G4061955851`, 2026-08-31T07:15:04Z,
`Main_Castle_Overworld`) — `logs/f8-inbox/capture-device-20260831-090217-seq4077.md`.

---

## PROVING DATA (the captured stack, verbatim)

```
[HeroInventoryController] RebuildStage FAILED (rest of inventory still shown).
[state hero=Player-found loadout=present equippedArmor=armor_mage_common job=mage store(owned=2)]
System.NullReferenceException: Object reference not set to an instance of an object.
  at DeNelle.Village.HeroInventoryController.BuildPeekStrip (UnityEngine.Transform stage)
  at DeNelle.Village.HeroInventoryController.BuildItemGrid (UnityEngine.Transform stage)
  at DeNelle.Village.HeroInventoryController.SafeRun (System.Action step, System.String label)
  at DeNelle.Village.HeroInventoryController.Render ()
  at DeNelle.Village.HeroInventoryController.SelectRail (System.Int32 railIndex)
  at DeNelle.Core.UI.ElarionUiKit+TabRowHandle.Select (System.Int32 index, System.Boolean notify)
```

Player-visible effect: tapping a gear tab blanks the peek strip; `SafeRun` swallows the throw so
the rest of the Bag still renders. The hero, loadout and equipped armor are all present in the
state dump, so this is **not** a data-empty case.

## WHERE IT LIVES (located, NOT concluded — CLAUDE.md §12)

⚠ The stack names `HeroInventoryController.BuildPeekStrip`, but at HEAD the method lives in
**`Assets/_Modules/Village/Hero/InventoryGrid.cs:290`** (called from `:215`). It moved in
`d6d3146b2 fix(inventory): replace buried rail with gear tabs`. A seat grepping
`HeroInventoryController.cs` finds nothing and wrongly concludes the code is gone — it is not.
Confirm which build the device was running before assuming HEAD reproduces it
(memory `diagnose-the-build-under-test`).

**Three unguarded dereferences in that method, and one telling asymmetry:**

| line ~ | expression | guarded? |
|---|---|---|
| 305 (portrait) | `zone.content.GetComponent<VerticalLayoutGroup>()` → `column.padding.bottom` | **NO** |
| 313 (landscape) | same call → `if (column != null)` | **yes** |
| 320 (landscape) | `zone.content.GetComponent<ContentSizeFitter>()` → `fitter.horizontalFit` | **NO** |
| 331-335 (landscape) | `zone.scrollbar.direction`; `zone.scrollbar.transform as RectTransform` → `srt.anchorMin` (an `as` cast returns null SILENTLY) | **NO** |

The landscape branch null-checks `column` and then dereferences `fitter` and `srt` without the same
care, two lines apart. The game is landscape on device, so the landscape branch is the one that ran.

## ⛔ INSTRUMENT FIRST — do not inference-fix

Three candidates is not a root cause. **Add `FlowTrace.Step("Inventory", ...)` naming each of
`column`, `fitter`, `zone.scrollbar` and `srt` as null-or-not immediately after each lookup**, plus
a `FlowTrace.Warn` on any null, then reproduce (headless AutoPilot if it repros there, otherwise a
device run) and let the trace name the dead reference. Fix THAT one. Then, and only then, harden the
other two — a null there is equally a latent blank stage.

Static reading LOCATED the candidates; it did not conclude the cause. If you cannot cite the trace
line that proves which reference was null, you have not earned the edit.

## SECONDARY QUESTION FOR THE FIX

If `ElarionUiKit.MakeScrollZone` can legitimately return a zone whose `ContentSizeFitter` or
`scrollbar` is absent, the defect is in the **kit's contract**, not in this caller — every other
`MakeScrollZone` consumer has the same hole. Check the kit before patching only here.

## ACCEPTANCE CRITERIA

- [ ] A captured `[Flow:Inventory]` line naming the null reference — the RCA is data, not reasoning.
- [ ] Bag opens and every gear tab renders its peek strip; zero `RebuildStage FAILED` in the log.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs (markers, never exit codes).
- [ ] `RunCaptureHeadless` screenshot of the Bag with a stocked peek strip, opened and looked at
      (memory `headless-screenshot-verify-ui-before-build`).
- [ ] The instrumentation **STAYS IN** — flag it off if noisy, never strip it (CLAUDE.md §12).

## DO NOT TOUCH

- The gear-tab rail from `d6d3146b2` — that is the current design, not the defect.
- `SafeRun`'s swallow-and-continue: it is why the rest of the Bag survived. It is working correctly.
