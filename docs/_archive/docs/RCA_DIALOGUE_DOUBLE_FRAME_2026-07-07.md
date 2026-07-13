# TRUE RCA — "frame with another frame inside" (dialogue box, F8-1 / F8-5)

> Owner mandate 2026-07-07: **"i want a full RCA off data with the dialogue."** Every claim below
> cites a captured line or a read code line. No fix in this document is inferred.
> Captures: flag_04.png (22:10 "double dialogue boxes", Woodcutter/Lumbermill), flag_00.png
> (23:27 "still here RCA first", Blacksmith/Forge), owner live report 2026-07-07 evening
> ("i see the blacksmith i click talk I get a frame with another frame inside"), and the
> `[Flow:DlgLayout]` runtime geometry trace added for this RCA (fired in the owner's live session).

## VERDICT — one dialogue, three stacked rectangles. Not two dialogues, not a broken Close seat.

**Ruled OUT by data:**
- **Two dialogue hosts / double Play** — Player.log (22:10 session): exactly ONE
  `[Flow:Dialogue] Play 'lumbermill'` (:82487), ONE `DialogueView:BuildUi` (:82529), ONE
  `PanelManager 'Dialogue' opened and verified visible` (:82595). The legacy Yarn runner is
  gone from live Assets (WO-557). One view, one host, one panel.
- **Close seated outside the panel rect** — the DlgLayout trace (owner's live session, new build):
  `close x 0.284-0.716 y 0.120-0.393 (pivot=(0.50, 0.00) anchors=(0.500, 0.120) sizeDelta=(360, 120)
  parent='PanelContent')` with `interior … y 0.100-0.935`. The Close's whole box sits INSIDE the
  panel rect and above the interior plate's floor. The seat math is working as authored
  (DialogueView.cs:195-197 anchor override + the kit's bottom-pivot from SeatSharedCloseInside,
  ElarionUiKit.cs:842-844).

**The PROVEN cause — three separately-painted rectangles inside one panel rect:**

1. **The FrameDialogue art is a LANDSCAPE STRIP stretched onto a TALL reading panel.** The owner
   ruling 2026-07-06 moved dialogue from a bottom strip to a centered reading panel
   (DialogueView.cs:108-118, panel anchors (0.29,0.20)-(0.71,0.62)). The Blink `Dialogue_Panel`
   art was pixel-measured for the OLD landscape strip (DialogueView.cs:121-130, SWEEP 9413) —
   stretched tall, its painted border/ornament distorts and its inner well no longer matches any
   content rect. **Rectangle #1 = the silver frame art.**
2. **The opaque `DialogueInterior` plate is per-screen chrome patching #1.** Because the stretched
   strip art left content floating on transparency (SWEEP 9413), the view paints its own full
   obsidian plate inset at x 0.06-0.94, y 0.10-0.935 (DialogueView.cs:139-152; trace confirms
   `interior x 0.060-0.940 y 0.100-0.935`). The visible band of frame art between the plate's edge
   and the frame's painted border **is the "second frame."** This plate violates the UI template
   canon §4 ("No per-screen chrome — the frame supplies all of it", docs/UI_BLINK_TEMPLATE_CANON.md)
   — a documented patch, not a design.
3. **The canonical gray Close plate is a third rectangle sitting on the bottom band.** Trace:
   `close y 0.120-0.393` — 27% of the panel's height, a 360x120 gray plate over the plate/border
   seam. Against the stretched art's bottom border (painted region ≈ the lower ~0.10 of the rect,
   DialogueView.cs:186-189) it reads as hanging on/below the visible silver edge (flag_00), even
   though it is geometrically inside the rect. With Continue directly above (band 0.40-0.495,
   DialogueView.cs:248-249) the bottom third stacks THREE adjacent plates: interior, Continue,
   Close.

**One trace artifact, for honesty:** the DlgLayout line reports `continue <null>` because the trace
fires inside the Close block (DialogueView.cs:~200), which runs BEFORE the Continue chip is built
(:248). Timing artifact of the instrument, not a missing button — flag_00 shows the chip rendered.

## Why the 07-06/07 sweeps kept "fixing" it and it stayed felt-broken
Every sweep adjusted geometry INSIDE the mismatch (plate inset 0.045→0.06/0.10, Close seat
0.075→0.12, body re-stacks — DialogueView.cs comments :142-145, :186-189) — the persistent-symptom
rule (memory `persistent-symptom-means-wrong-layer`): the layer being tuned (fractions) was never
the broken layer (the ART SHAPE vs the PANEL SHAPE).

## FIX OPTIONS (owner decision — recommendation first)

**A (recommended) — give the reading panel a WINDOW frame, delete the patch plate.** Build the
dialogue on a frame designed for a tall window (`FrameCore`/`panel_window` family, already
mirrored) instead of the landscape `FrameDialogue` strip: the frame then SUPPLIES the chrome
(canon §0/§4), `DialogueInterior` is deleted (kills rectangle #2), the factory's close-band
reservation + zones apply (kills the F8-5 read for this panel), and the view drops content into
`layout.header/body/medallion` like every conforming screen. FrameDialogue stays for any true
bottom-strip use.
- Cost: one frameName swap + re-pointing the view's custom zones at the kit zones; screenshot-
  compare pass per canon §7.

**B — keep FrameDialogue, author a tall variant.** Re-measure `ZonesFor(FrameDialogue)` against the
tall stretch and 9-slice the art so borders don't distort. Keeps the strip art but hand-tunes
around a shape it wasn't drawn for; the plate likely survives as patch. Slower, stays off-canon.

**C — revert dialogue to a bottom landscape strip.** The art fits again, plate deletes — but
reverts the owner's 2026-07-06 centered-reading-panel ruling (mobile readability). Named for
completeness; contradicts a settled owner decision.

_Compiled 2026-07-07 from: flag_04/flag_00 captures + owner live report, Player.log flow lines
(22:10 session), the DlgLayout runtime trace (owner's live session, build 18:57), and direct reads
of DialogueView.cs / ElarionUiKit.cs / UI_BLINK_TEMPLATE_CANON.md. QA read-only RCA (agent, 22:30)
supplied the single-host proof._
