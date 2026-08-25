# WORK ORDER 1189 - Accept overlaps the Rumor Board status line by 7.4px

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1189 -> 1190 in the same edit)
**Silo:** UI / panels
**Parent:** WO-1076 (different control pair - see below)

---

## Why this is a separate ticket, not WO-1076 remaining scope

WO-1076's subject was **the shared Close burying `ObsBtn_Accept` and `ObsBtn_Track`**. That is
resolved: the fresh capture shows **zero** findings naming `CloseButton` and zero naming `ObsBtn_Track`.

This is a different pair: **`ObsBtn_Accept` over a body TEXT element**, and it involves the Close not
at all.

## STOP THE PART WORTH READING: these two findings were HIDDEN, not new

`Assets/Editor/UICaptureLaunch.cs:1832-1833` used to write a stale `0.05` portrait floor onto the
detail pane before the geometry audit ran (deleted 2026-08-25, WO-1076). That override pushed the pane
DOWN, away from the status text - so these two overlaps **could not appear in any capture**.

⭐ **An instrument that lies does not only manufacture false findings. It also conceals true ones.**
The same two lines produced 18 phantom findings on `CloseButton`/`Track` AND suppressed 2 real ones on
`Accept`. Deleting the override cost 18 and revealed 2, and the 2 are the ones a player could actually
meet.

## The finding, from captured data

`Builds/uicap-wave1.log`, marker `UI_CAPTURE_OK 89`, `UI_TOUCH_FAIL x2` / `UI_GEOMETRY_FAIL x2`:

```
[UICap-GEO] BUTTON OVER TEXT [RumorBoard_1080x2340 @1080x2340]
  'ObsidianPanel/PanelContent/DetailPane/DetailCta/ObsBtn_Accept' (x -340.2..13.6, y -570.5..-458.5)
  covers 'ObsidianPanel/PanelContent/Zone_Body/Status'
  ("The talk of Elarion. Accept what calls to you.") (x -370.8..-15.4, y -466..-422)
  by 324.8x7.4 ref px.
```

Also at 1200x2670 (`y -578.2..-466.2`). **Two findings, one per portrait aspect.**

⚠ The overlap is **7.4 ref px vertically** - small, and wide (324.8 px). The CTA's top edge clips the
bottom of the status line. Not a tap-theft problem in practice, but the oracle is right that a button
is sitting on text, and the text is the line that TELLS the player what the board is for.

## Acceptance criteria

1. A **fresh** `RunCaptureHeadless` shows **zero** `touch-oracle` findings naming `RumorBoard`.
2. `UI_TOUCH_FAIL` drops from **2** to **0** with no other panel regressing.
   ⛔ The current measured total is **2**. The `x43` and `x21` figures in older tickets are both stale.
3. Geometric fix - separate the bands. ⛔ Not a z-order change, and ⛔ do not shrink `DetailCtaPx`
   (112) or any touch target to make room; `SUB-TOUCH-FLOOR` must not appear.
4. ⛔ `LayoutOracle`'s `TouchBaseline` allow-list stays at its two entries. Owner ruling 2026-08-24:
   no waivers.
5. ⛔ Do NOT re-add any anchor re-assert to `UICaptureLaunch.cs`. A harness photographs the panel; it
   never re-authors it. Re-adding a literal there re-creates the duplicated constant WO-1076 removed.
6. No acceptance criterion may turn on colour - the owner is red/green colourblind.

## Where to look

`Assets/_Modules/Village/Hero/RumorBoardPanel.cs`. WO-1076 moved the portrait floor out of the anchor
into a pixel offset (`offsetMin`) so an overwrite can only push the pane FURTHER from the Close. The
detail pane's top edge against `Zone_Body/Status` is the seam this ticket owns.
⚠ Note the pane bottom is now at its TRUE device position for the first time, so this geometry has
never actually been measured before today. Read the numbers off a fresh capture, not off the source.
