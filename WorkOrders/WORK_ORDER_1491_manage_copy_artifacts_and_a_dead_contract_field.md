# WO-1491: Manage copy artifacts, a text-arrow back button, CLOSE on five panels the mockup does not, and a dead contract field

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 1-9 (chrome: back arrow, CLOSE, header) not yet passed (2026-09-07); code landed in the wave-four commit, gated 440/441. The owner walked all nine Manage screens on build 358872 beside MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-07, uncommitted, awaiting the gate + a fresh Manage capture. The)*
chrome half (back-arrow sprite, CLOSE on the hub only, the "MANAGE - BUILD" spelling) landed at
source; see `WORK_ORDER_1491_manage_copy_artifacts_and_a_dead_contract_field.RESULT.md`.
⛔ **Two items are deliberately NOT done and the RESULT says why:** `ProgressText` is COMPOSED as of
`ManageVmProjection.cs:337`, so this ticket's "declared, never composed" evidence is stale and the
field was not deleted; and two of the five copy artifacts are authored DATA rows, not code literals.
*(was: READY TO IMPLEMENT)*
**Silo:** Manage 2000-block (WO-2014, copy and chrome) + `ManageViewContract`.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1491 -> 1492 in the same edit).

## 1. EVIDENCE

Captured copy artifacts on the Manage screens:

```
"12 MORE - SCROLL"                       an instruction rendered as content
"stragglers. ."                          orphaned period, double space
"A defensive tower   auto-fires"         triple space mid-sentence
back button                              rendered as the literal text "<-"
CLOSE                                    present on panels 2, 4, 6, 7, 8;
                                         the mockup shows it on panel 1 ONLY
```

And a dead field:

```
ManageViewContract.cs:284   ProgressText   -- declared, never composed, never painted
```

`ProgressText` is the same composed-but-unpainted class WO-1444 opened on `FaceCountText`; this one is worse
- it is not even composed, so there is nothing to paint.

## 2. FIX SHAPE

- Copy pass across the Manage screens: fix the three whitespace/punctuation artifacts, turn `12 MORE - SCROLL`
  into an affordance rather than a sentence, and replace the `<-` literal with the kit back glyph.
- CLOSE stays on panel 1 only, per the mockup; the other five lose it (they have the back door).
- Delete `ProgressText` from the contract. It is dead weight in the file the architecture points at.

## 3. WHAT NOT TO DO
- Do not compose `ProgressText` to "use it up". Nothing asked for it; deleting is the smaller correct change.
- Do not reword player-facing sentences beyond the artifacts. Copy is the owner's call.

## 4. ACCEPTANCE
- [ ] The five copy artifacts gone; fresh Manage PNGs opened.
- [ ] CLOSE present on panel 1 only.
- [ ] `ProgressText` deleted; zero hits repo-wide (grep pasted).
- [ ] `REGRESSION_OK n/n` on a fresh log.

---

## 5. OWNER RULING 2026-09-07 08:3x - A CONSTANT EXIT, TOP RIGHT, ON EVERY MANAGE SCREEN

⚠ The ticket's state is **UNCHANGED** - it stays `AWAITING OWNER MATCH` on the one `**Status:**` line
at the top of this file. This section records a ruling that lands ON TOP of this ticket's chrome
pass; it does not close it. *(Worded to avoid a second line beginning "\*\*Status" -
`tools/board_build.py`'s `_STATUS_MALFORMED` regex would read one as a near-miss status row.)*

Owner, verbatim:

> **"on all the manage screens there is no way to exit. can we add a const exit button top right"**

### What it supersedes, and what it does not

⛔ **THE §2 LINE "CLOSE stays on panel 1 only, per the mockup; the other five lose it (they have the
back door)" IS SUPERSEDED IN ITS REASONING, NOT IN ITS IMPLEMENTATION.**

- The **implementation stands**: the kit's drawn bottom **CLOSE** (`_chromeClose`) is still shown on
  the **hub alone**, exactly as `MANAGE_MOCKUP_8_SCREENS.png` draws it, and
  `ManageMockupConformanceRegression`'s `[chrome-close-on-hub-only]` case still pins it.
- The **premise was wrong**: *"they have the back door"*. The back arrow walks the **model's screen
  graph** - it navigates **within** Manage and never leaves it. So on BUILD / ARMY / RESEARCH, on a
  detail card, on the research tree and on the queue overlay, the player had **no route back to
  town at all**. The mockup sheet did not carry that because a sheet cannot draw a route.

### What was built

A **separate** control with a **separate** field - never a re-gating of `_chromeClose`:

| | bottom `CLOSE` (`_chromeClose`) | top-right `X` (`_manageExit`) |
|---|---|---|
| Drawn on | the **hub only** (mockup panel 1) | **every** Manage screen, always |
| Job | the hub's own exit | exit Manage from anywhere |
| Ruling | WO-1491 / the mockup sheet | this section, 2026-09-07 |

- `ManageScreenPanel.BuildConstantExit` builds an **`X` at `ManageExitPx` (= `ElarionUiKit.MinTouchPx`,
  112 ref px)**, pinned by px with pivot 1 to `ManageChromeRightX` (0.945 of the panel - the chrome
  row's own right edge, inside the frame art), vertically centred in the header band
  `WorkspaceHeaderY0..WorkspaceHeaderY1`.
- It is parented to **`chrome.content`, NOT `_tabsHost`**, and built last so it is the top sibling.
  Two reasons, either alone sufficient: `ApplyDrawerPlacement` **deactivates the whole chrome row**
  under the queue overlay (so a child of it is absent on panel 8 - the very screen the ruling
  names), and `BuildTabs` **destroys every child of the row** on entry (the bug that made the back
  arrow vanish for a round).
- `ApplyScreenVisibility` asserts it **ON unconditionally**. "Const" is a state guarantee.
- **ONE ROUTE.** A single `Action exitRoute = Close;` in `BuildChrome` is handed to
  `ElarionUiKit.BuildObsidianPanel` as its `onClose` (so it is literally what the hub's CLOSE
  invokes), to the scrim as its tap-out, and to this X. There is no second close path.
- The **QUEUE pill sits immediately to its left**: `SeatQueuePillLeftOfExit` is the one writer of
  that seat and offsets the pill by `ManageExitPx + ManageExitGapPx` (112 + 12 = 124 ref px) from
  the same right edge. Called at construction **and** from `SizeQueuePillToLabel`, because the
  latter early-returns while `rowW < 1f` and the old authored fraction would have left the pill
  under the X on that path.
- The **back arrow is kept**. Arrow = navigate within Manage. X = leave Manage.

### Pinned by

`ManageMockupConformanceRegression.CheckConstantExit` - assertions across existence, the one route,
the parent, the unconditional state, the touch floor, the header band (token AND arithmetic), and
the pill's derived clearance. **The count is deliberately not written here** - read the method; a
hand-kept number beside the thing it counts is the failure CLAUDE.md §2/§5/§8 each describe. The
suite's banner moves `MANAGE_MOCKUP_OK 9 cases` -> `10 cases`.

### Not proven from here (§11B)

- **No capture was taken** - this lane is edit-only, no Unity. The rect is printed as
  `MANAGE_EXIT_RECT` beside `MANAGE_QUEUE_PILL_RECT` and `MANAGE_TITLE_RECT`, in the same world-corner
  units, so the next `ManageFlow` capture **names** it instead of anyone theorising.
- **`TitleLocalX0/X1` were deliberately NOT moved**, and the reason is arithmetic, not a picture:
  the pill's right edge moves left by 124 ref px = 0.060 of the reference panel width (2062 px,
  from `RefWellWidthPx` 1835 / 0.89), so its **left** edge moves content 0.764 -> 0.749 while the
  title's right edge stays at content 0.675. Clearance narrows 0.089 -> 0.074 of the panel; **no
  overlap opens.** Narrowing the title as well would squeeze a rect whose longest breadcrumb
  ("MANAGE - RESEARCH - SCHOOL") is already recorded UNVERIFIED. **Derived, not captured.**
- **A UNIT MISMATCH THAT IS PINNED BUT NOT MEASURED ON A DEVICE.** The exit is a fixed **px** square;
  the band it sits in is a **fraction** (`WorkspaceHeaderY1 - WorkspaceHeaderY0` = 0.124 of the
  panel). Below a panel height of **~903 ref px** (= 112 / 0.124) the square is taller than its band
  and its top crosses 0.962 onto the frame's border art (interior edge measured at v 0.966 on
  `frame_core.png`). At `RefPanelPx` 927 the band is ~115 px and the box clears by ~1.5 px a side.
  `CheckConstantExit` now does that arithmetic against the ONE stated reference surface - it cannot
  do it against an arbitrary device, and an EditMode suite never could. **A short-panel device is the
  open risk here, and only a frame closes it.**

### Contradictions carried forward for the owner (not acted on)

1. **The hub now has TWO exits** - the drawn bottom CLOSE (panel 1, WO-1491) and the top-right X
   (this ruling). WO-1491's own thesis was *"two exits on one panel teach neither"*. The ruling says
   **all** the manage screens, and the hub is one of them, so the X is built there too. If the owner
   wants the hub to keep only its drawn CLOSE, that is a one-line change in
   `ApplyScreenVisibility` - and it should be **her** call, not an inferred exception.
2. **The queue overlay now shows TWO X glyphs on the right**, at different heights: the drawer's own
   X (in `Drawer_Header`, closes the **overlay**) and this one (in the header band, exits
   **Manage**). They do not overlap - the drawer header grows **downward** from
   `WorkspaceHeaderY0` while the exit sits in the band above it - but the same glyph at the same
   size meaning two different things is a real ambiguity, and the frames should be read for it.
