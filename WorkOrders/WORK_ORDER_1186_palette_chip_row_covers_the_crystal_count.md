# WORK ORDER 1186 - the palette's Other chip sits on top of the crystal count

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1186 -> 1187 in the same edit)
**Parent:** none - this panel is covered by NO existing ticket. Found by the 2026-08-25 morning
capture pass, not by a report.
**Silo:** UI / Build mode

---

## The finding, from captured data

**Proving log:** `Builds/uicap-0825am.log` (fresh, 2026-08-25 06:00, marker `UI_CAPTURE_OK 89`).

```
[touch-oracle] BUTTON OVER TEXT [BuildPaletteDock_open_2340x1080 @2340x1080]
  'PaletteDock/ChipRow/Chips/Chip_Other' (x 396..603, y -192.1..-96.1)
  covers 'PaletteDock/ChipRow/Text' ("Crystals: 0") (x 468..756.6, y -194.5..-93.7)
  by 135x96 ref px.
```

**Three findings, one per captured resolution** - 1920x1080, 2340x1080 and **2670x1200, the Seeker's
real surface**. So this is not a one-aspect artifact; it reproduces at every resolution captured.

`Chip_Other` overlaps the `ChipRow/Text` element reading **"Crystals: 0"** by **135 x 96 reference
pixels**. The chip is a button and the text is not, so the chip wins every tap in the overlap - the
player cannot read a resource count that a control is sitting on, and taps meant for the readout do
something else.

## Why it matters more than the pixel count suggests

⭐ The overlapped string is a **resource readout on the build palette** - the surface the player uses
to decide whether they can afford a placement. `PROD-015` and the felt-test route both turn on the
player reading crystal counts correctly before placing (Arcane Spire / Crystal Mine). A count that is
partly under a button is the same class of defect as a truncated cost label.

⚠ It is also adjacent to **WO-1081** (the palette never says what a building does) and **WO-1167**
(palette category grouping) - both change this dock. **Coordinate: same panel, and 1167 re-groups the
very row this ticket measures.**

## Acceptance criteria

1. A **fresh** `RunCaptureHeadless` shows **zero** `touch-oracle` findings naming `BuildPaletteDock`.
2. `UI_TOUCH_FAIL` total drops from **21** to **18** with no other panel regressing.
   (⛔ 21 is the measured current total. The `x43` figure in WO-1060/1075/1076/1077/1078 is from
   `Builds/wo1060-capture.log` and is **stale** - do not compute against it.)
3. The fix is **geometric** - author the chip row and the readout into non-overlapping bands.
   ⛔ **Not** a z-order change: a transparent-but-raycasting control still steals the tap, which is
   the argument recorded against the tool fix in the Batch 7 pins.
4. ⛔ **The `LayoutOracle` allow-list (`Assets/Editor/UICaptureLaunch.cs`, `TouchBaseline`) stays at
   its two entries** - `ArmyMuster` and `EquipDrawer`. Owner ruling 2026-08-24 (batch 2, ruling 9):
   no waivers. Adding this panel to it **fails the ticket**.
5. No acceptance criterion may turn on a colour - the owner is red/green colourblind. Judge by
   position, size and finding count.

## What NOT to touch

- ⛔ `Assets/Editor/UICaptureLaunch.cs` - the harness and its baseline are a LEAD call, not part of
  this fix.
- ⛔ `Assets/_Modules/Core/UI/ElarionUiKit.cs` - WO-917 owns it and is committed. Reuse only.
- The chip row's **behaviour** and the category set - that is WO-1167's scope.

## Files likely in scope

`Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` (the dock and its chip row are built here;
confirm at source before editing - the catalog says comments lie).
