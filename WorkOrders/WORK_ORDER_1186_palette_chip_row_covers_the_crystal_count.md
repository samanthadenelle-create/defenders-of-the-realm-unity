# WORK ORDER 1186 - the palette's Other chip sits on top of the crystal count

**Status:** FIXED - landed 2026-08-25 at `22f59afde` (`Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs`). Verified at source this session: bands re-cut with a real gutter, a fit-to-band pass against the `ElarionUiKit.MinTouchPx` floor, and RectMask2D containment. Cause was HorizontalLayoutGroup overflow (143px), not the authored anchors. Owner felt-close owed.
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

---

## RCA CORRECTION 2026-08-25 - this ticket's stated cause was WRONG

**Verified at source by the implementing seat, confirmed by the lead.**

Acceptance criterion 3 said *"author the chip row and the readout into non-overlapping bands."*
**They were ALREADY authored non-overlapping** - chip host `0..0.80` of the 1560px dock (x -780..468),
readout `0.80..0.985` (x 468..756.6). A seat reading AC3 literally would have checked the anchors,
found nothing wrong, and shipped nothing.

**The real cause:** `HorizontalLayoutGroup` with `childControlWidth = false` **does not shrink its
children**. Six chips at natural width ran **1391px inside a 1248px host** - a 143px overflow that put
`Chip_Other`'s right edge at x 603, exactly **135px** past the readout's left edge. That 135 is the
overlap figure in all three captured findings, which is what confirms the mechanism.

⭐ **The transferable lesson:** the finding named a SYMPTOM (two rectangles overlap) and the ticket
turned that into an assumed CAUSE (the rectangles are authored wrong). They are not the same claim.
A geometric oracle reports where things ENDED UP; it cannot report WHY. This is CLAUDE.md section 12
at the layout layer - the capture LOCATED it, only reading the layout code CONCLUDED it.

**Fix as landed:** bands re-cut with a real 15.6px gutter (readout right edge unchanged at 756.6, so
the number stays where the owner reads it), a fit-to-band pass that scales the run with a hard
`ElarionUiKit.MinTouchPx` floor, and `RectMask2D` on the host as containment. ⭐ The mask was chosen
because it is a RAYCAST filter as well as a visual clip - a masked region does not steal the tap,
which is the objection AC3 raises against a z-order fix. Nothing was reordered or made transparent.

## LEAD CALL OWED - an adjacent defect the oracle did not flag

Chips resolve **96px tall** inside the 112px band (`ChipPadVertPx` 8 top and bottom). Chips are
CONTROLS, so 96 is **below `MinTouchPx` (112)**.

⛔ `LayoutOracle`'s Assert A did NOT flag this in `Builds/uicap-0825am.log`, and no ticket covers it.
So there are two open questions and they are different:
1. Should the chips be 112 tall (removing the vertical padding puts them flush to the band edge)?
2. **Why did the touch-floor assert not fire on a 96px control?** That is a possible gap in the
   oracle itself, and a gap in a gate is worth more than the defect that revealed it
   (`docs/INSTRUMENTATION_STANDARD.md` section 1.4b - an assertion that cannot fail on the broken
   state is decoration).

⚠ Question 2 must NOT be closed by adding this panel to `TouchBaseline`. The allow-list stays at two
entries; owner ruling 2026-08-24, no waivers.

## Coordination, reported not resolved

- **WO-1081 shares this file.** Its line citations (`:1014-1353`, `:820-950`, `:866-867`,
  `:1101-1138`, `:1129-1134`) are now **STALE** - this change adds +149 lines above and inside
  `RebuildChips`. No semantic conflict (`BuildCard`, `OnCardTapped`, `CardTapGuard` untouched), but a
  seat working from line numbers rather than symbol names will land in the wrong place.
  ⛔ Work WO-1081 by SYMBOL, not by line.
- **WO-1082 is JSON-only and does not conflict** - but it reorders the catalog, which changes group
  card counts, which changes chip caption lengths, which feeds the new width math. The fit pass
  handles any count, so this is a dependency, not a collision.

---

## OWNER RULING 2026-08-25 - a control may never sit on top of a resource count

**Owner, 2026-08-25.** Binding text lives in `FOUNDATIONAL_RULINGS.md` **section 13** - ⛔ cite it, do
not paraphrase it here.

⭐ **What survives for THIS ticket, and it is the whole of this ticket's defect:** a control may never
sit on top of a resource count on the surface where the player judges affordability. An occluded
number is not a smaller number, it is an unreadable one, and the player commits resources from that
read. The fix **reclaims space or re-flows** - which is what the landed fix did (bands re-cut with a
real gutter plus a fit-to-band pass), so ⭐ **the landed fix remains correct**, and any FOLLOW-UP on
this dock - the open lead call on the 96px chip height, the WO-1167 re-grouping - may not buy space
back by putting a control over a count again.

⚠ **An earlier version of this note claimed the ruling meant "never a reason to drop, collapse or
tap-gate a resource". That claim is REMOVED and was wrong.** The shipped ambient dock does exactly
that by posture - town collapsed, explore gold-only with a 6-second tap reveal, build and combat with
no readout at all - and it is not in breach of anything. Section 13 is about build-screen cost
strings, not about the dock's posture behaviour.
