# WO-1563: the BUILD and ARMY grids discard the state text the model already composes — and the fallback is a glyph a colourblind player cannot read

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:15:00, build 2026.09.07.359076) - "owner in chat 2026-09-07 09:1x, verbatim: 'the 15 verify are the new screen UI work correct? THose I verified' - the board panel listed Fixed rows only, so t...". PRIOR STATUS: AWAITING OWNER MATCH - device frame vs mockup panel 2 (BUILDINGS grid) not yet passed (2026-09-07); code landed uncommitted in the working tree. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate)*
**Priority:** P1 — this is the accessibility one.
**Silo:** `Assets/_Modules/Core/Manage/ManageWorkspacePanel.cs` (`BuildTile` only).
**CLEAN in the working tree as of 2026-09-06 21:50** — file-disjoint from WO-1564's `ManageScreenVM.cs`.
**Parent:** WO-1534 §B2. **Source:** read-only review 2026-09-06 (CLI seat), re-read at source.
**Minted** from the banner (`CLI_LANES_WO_NUMBERS.md`, renumbered to the banner's hundred-and-second-pass reconciliation, 2026-09-06 22:12).

---

## 1. EVIDENCE

`ManageWorkspacePanel.BuildTile` (`:754-845`) reads `tile.Title`, `PortraitKey`, `FrameKey` and
`StateIconKey`. **It references `tile.Subtitle` and `tile.StateText` exactly ZERO times** — measured by
scanning the whole method body — and `:826-827` states the intent outright:

> *"THE NAME STRIP — one band, and the only text on the tile."*

Both fields are declared on the contract (`ManageViewContract.cs:196`, `:206`) and **the sibling renderer
paints both**: `BuildListRow` (`:641-730`) draws `tile.Subtitle` at `:677` and `tile.StateText` at `:717`.

So the same screen family answers "what can I act on?" two opposite ways. `ManageFlow_RESEARCH_school`
proves it: the research rows read **`RESEARCHED`**, **`QUEUE FULL`**, **`RESEARCHING`** in words, while
`ManageFlow_BUILD_gridtop` and `ManageFlow_ARMY_gridtop` show portrait + name and nothing else.

**The model is already composing the state. The grid renderer throws it away.**

## 2. ⚠ WHY THIS IS THE ACCESSIBILITY TICKET

**The owner is red/green colourblind** (memory `owner-colorblind-delegate-visual-creative`), and with no
state word the grid's ONLY state channel is a small glyph: `ManageArt.cs:74-78` and `StatusFor`
(`:112-120`) collapse five distinct states onto five badges **that differ partly by a red dot.**

⛔ **AND THE LANDED WO-1516 LANE MAKES IT WORSE, NOT BETTER.** `ProjectAffordanceTile` **withholds** the
status medallion for the Available catch-all — correctly, because WO-1516's own acceptance says *"the
green up-arrow means nothing: state a REAL affordance or remove it"*. But with no word to fall back on,
those tiles now carry **neither glyph nor text**. Removing a meaningless signal without adding a
meaningful one leaves the tile mute.

**This is unticketed anywhere.** WO-2006 and WO-2008 both mandate level + state on the tile and are both
marked **DONE** (see WO-1560 — they certify a superseded design).

## 3. WHAT TO DO

Paint the `StateText` the model already composes, in the same channel `BuildListRow` uses. **This is a
BINDING, not a new concept** — no new contract field, no new VM logic, no inferred state in the View.

⛔ **Do NOT let the View derive or map state.** UI is dumb (locked ruling 1); `StateText` comes from the
model exactly as it does for the list rows.

⛔ **Do NOT solve this with colour.** Propose contrast, a word, and shape. The existing selection
treatment is the right precedent — `BuildTile`'s LAYER 5 comment records that selection is a gold
**border**, i.e. a shape, never hue alone.

### The layout constraint — read it before you take a band

The tile is short and the name band is already fought over:
- `ManageScreenPanel.cs:3941-3949` records this class of bug on the neighbouring card: a band starved to
  **18.2 px** made TMP's Ellipsis **cull the whole line**, so the plate painted and the words did not.
  It ends: *"⛔ Never re-shrink a text band below ~24px on this card."*
- `ElarionUiKitObsidian.cs:3044` sets `FontHardFloor = 20f` and clamps any sub-floor minimum **up**
  (`:3062`), so you cannot buy space by asking for smaller text — you will get an ellipsis instead.

So the state line needs **real** vertical budget. `ManageWorkspacePanel.cs:540-548` already emits a
`FlowTrace.Warn` for a "WELL SHORTFALL" when the band cannot seat the authored rows — **read that trace on
a real run before choosing a geometry**, rather than guessing at pixel numbers.

## 4. ACCEPTANCE

1. BUILD and ARMY grid tiles show the state in **words**, sourced from `tile.StateText`.
2. No state is inferred, mapped or derived in the View.
3. Every state is distinguishable **without relying on hue** — verify in greyscale (the standing gate for
   this owner).
4. No text band on the tile is below the ~24 px floor, and no label renders as a bare plate. Check the
   `[Flow:Manage]` well-shortfall warning is silent on the captured frames.
5. An oracle pins that a grid tile paints `StateText` whenever the model supplies one — so the renderer
   cannot silently drop it again. **Proven RED before green**, both runs recorded.
6. **Fresh** captures of BUILD and ARMY. ⛔ **The 18:39 frames PREDATE commit `949e848a0` (18:51) and the
   uncommitted Manage edits (20:47) — no frame in the repo shows the current code.** Do not judge against
   them.
7. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on **fresh** logs, judged by the marker.

## 5. WHAT NOT TO TOUCH

- The grid's **10 tiles** (5 × 2) and the **five** filter chips. Both are CORRECT — they are the owner's
  mockup (`CAPTURE_LOOP_GOAL.md:82`, `BuildFilter.cs:59-73`). The canon files that say otherwise are being
  bannered by **WO-1560**.
- `ManageScreenVM.cs` — **WO-1564** owns it this wave.
- The research picker's geometry — **WO-1564**.
- Tile ART and portraits — **WO-2015** (READY) and WO-1489 own the art contract and the missing frames.
- The activity strip — **WO-2012**.
