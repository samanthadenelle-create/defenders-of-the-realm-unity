# WORK ORDER 1076 — RumorBoardPanel: the Close button is stacked on Accept and Track; only one can win the tap

**Status:** FIXED - the PANEL was already correct (a2162f17d); the CAPTURE HARNESS was re-breaking it for the camera. Harness fix + a fail-safe floor landed 2026-08-25. Owner felt-close owed.

**Minted:** 2026-08-24, UI seat, from the `CLI_LANES_WO_NUMBERS.md` UI-seat block (1076; banner bumped 1075 → 1079 in the same edit).
**Parent:** WO-1060 (`WORK_ORDER_1060_touch_clamp_and_overlap_oracle.md`) — the touch/overlap oracle that found this.
**Silo:** UI / Village-Hero. **File-disjoint from WO-1075, WO-1077, WO-1078** — run all four in parallel.

---

## ⚠ ALREADY SHIPPED — DO NOT IMPLEMENT THIS TICKET (recorded 2026-08-24)

**This ticket was minted from a capture log that predates the fix, and describes a defect the tree no
longer has.** It was handed to a dev seat, which correctly REFUSED it (see `batch_results_state.md`,
`HANDOFF 2026-08-24 21:34`, WO-1076) — a wasted seat.

**What is actually in the tree.** `a2162f17d` *"fix(ui): RumorBoard + RealmMap stop overlapping the
Close, and the capture harness stops photographing the wrong frame"* (2026-08-21, WO-941 + WO-942, an
ancestor of HEAD) added the fix this ticket asks for, and it is the fix this ticket asks for:
`RumorBoardPanel.CloseReserveTopFraction` (`Assets/_Modules/Village/Hero/RumorBoardPanel.cs:563`)
reads the shared Close's own seated `anchorMin.y`, adds the canonical `ElarionUiKit.CanonCtaHeight`
converted to fraction space, adds `CloseReserveGapFrac`, and clamps into
`[PortraitDetailFloorY 0.16, CloseReserveMaxFrac 0.45]`. The portrait branch (`:279`) uses that as the
detail pane's FLOOR and, when the remaining span starves the declared fixed stack, grows the pane
**upward** toward `PortraitDetailTopMaxY` rather than back down into the Close's band. The hardcoded
`0.05` floor this ticket's root-cause section describes is gone.

**Proof the source log is pre-fix, stated as arithmetic rather than as a timestamp.** The floor is
clamped to a minimum of `0.16` of the panel band **on every path, including the un-measurable
fallback**. `Builds/wo1060-capture.log` shows `DetailCta/ObsBtn_Accept` resolving to y
-757.1..-645.1 at 1080x2340 while `CloseButton` resolves to y -763.1..-631.1 — i.e. the detail pane's
bottom sitting BELOW the Close's top. That is geometrically impossible with the 0.16 floor in place,
so the run that produced those lines did not carry `a2162f17d`.

⚠ **And note what that means about the log's own timestamp:** `Builds/wo1060-capture.log` carries an
mtime of 2026-08-23 12:40 and an in-log licensing timestamp of 2026-08-23T17:39:59Z — both NEWER than
the 2026-08-21 fix commit — yet its content is provably pre-fix. **A capture log's file date is not
evidence of the tree it measured.** The mechanism that would have caught this (the capture recording
the HEAD commit it was taken at, and a minted ticket citing it) is specified in **WO-1080**.

⛔ **The 18 findings in this file are STALE. Do not use them to re-edit
`Assets/_Modules/Village/Hero/RumorBoardPanel.cs`.** If the `RumorBoard_` panel labels still appear in
a touch-oracle run, that is a NEW finding against post-`a2162f17d` geometry and needs a NEW ticket
minted from a FRESH capture — not this one. Re-run first:

```
powershell -File .
un-unity-method.ps1 -Method DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless -LogName ui-capture.log
```

⚠ Its `UI_TOUCH_FAIL x43` baseline, and the "drops by exactly 18, from x43 to x25" acceptance
criterion below, are stale for the same reason — and WO-1077 and WO-1078 each computed a DIFFERENT
repo-wide drop from that SAME x43. Everything below this banner is kept for history and is NOT a
work instruction.

---

## The panel

`Assets/_Modules/Village/Hero/RumorBoardPanel.cs`

## ⭐ This is the worst of the four. 18 findings — 4 BUTTONS OVERLAP + 14 BUTTON OVER TEXT.

Recorded output: `Builds/wo1060-capture.log:17128-17332`. Assert format strings:
`Assets/_Modules/Core/UI/LayoutOracle.cs:127` (overlap) and `:154` (button-over-text).

### The two buried buttons — verbatim

```
BUTTONS OVERLAP [RumorBoard_1080x2340 @1080x2340]
 'ObsidianPanel/PanelContent/CloseButton'                         (x -180..180,    y -763.1..-631.1) and
 'ObsidianPanel/PanelContent/DetailPane/DetailCta/ObsBtn_Accept'  (x -340.2..13.6, y -757.1..-645.1)
 share 193.6x112 ref px -- two tap targets in one place; only one can win the raycast.

 ... and 'ObsBtn_Track' (x 40.8..340.2, y -757.1..-645.1) share 139.2x112 ref px
```

At `1200x2670` (`:17320`, `:17332`) the same pair reads **193.4x112** and **139.7x112**.

⚠ **Two corrections to how this has been described.** The number is **193.6x112**, not 194x112 — the
round-up appears in WO-1060's own ruling section, but the oracle emits 193.6. And there are **TWO**
buttons buried under the Close, not one: **Accept AND Track**.

### The 14 BUTTON OVER TEXT findings — the same collision from the text side

Per aspect (7 each at 1080x2340 and 1200x2670):

| Covering | Covered | Overlap (1080x2340) | (1200x2670) |
|---|---|---|---|
| `CloseButton` | `ObsBtn_Accept/Label` "Accept" | 179.5x112 | — |
| `CloseButton` | `ObsBtn_Track/Label` "Track" | 127.2x112 | — |
| `CloseButton` | `DetailRewardRow/Chip/Fill/Label` "Food 90" | 110x6 | 109.4x6 |
| `CloseButton` | `.../Label` "Magic 45" | 121.2x6 | 119.5x6 |
| `CloseButton` | `.../Label` "Relic Drowned Ledger" | 108.8x6 | 109.6x6 |
| `ObsBtn_Accept` | `CloseButton/Label` "Close" | 179.2x112 | — |
| `ObsBtn_Track` | `CloseButton/Label` "Close" | 124.8x112 | — |

The reciprocal pairs are the tell: each button covers the other's label, which is only possible
because they occupy the same band.

## Why it matters to a player

⭐ **A player who means to accept a rumour dismisses the panel instead — or the reverse.** `Accept`
and `Close` share a **193.6 x 112 px** region, and only one of them wins the raycast. Whichever one
loses is, from the player's seat, a button that does nothing. And these are not equivalent outcomes:
one commits to a rumour, the other throws the board away. The reward chips underneath ("Food 90",
"Magic 45", "Relic Drowned Ledger") are covered too, so the player cannot even read what they are
accepting before the mis-tap resolves.

This is the failure mode the oracle's own message names in plain words: *"two tap targets in one
place; only one can win the raycast."*

## Root cause — visible in the source

- `RumorBoardPanel.cs:142` — `FooterBandPx = 200f`, commented as the band that **seats the shared Close**.
- `RumorBoardPanel.cs:134` — `public const float DetailCtaPx = 112f;` — the Accept/Track CTA row,
  authored **exactly at the floor**, placed by `DetailFixedStackPx` (`:137`) into **that same band**.

The Close is the canonical shared close: 360 wide (−180..180) x **132** tall (= `CanonCtaHeight`,
`ElarionUiKit.cs:341`), seated by the kit's `SeatSharedCloseInside` — **not** by `RumorBoardPanel`.
Because the CTA row is 112 and the Close is 132, the CTA band sits **entirely inside** the Close's
vertical span, which is why every overlap height is exactly 112.

**The fix is to stop the detail pane's bottom stack from reaching into the footer band that the shared
Close owns** — reserve the Close's band and lay the CTA row above it. ⛔ Do not solve it by shrinking
either button below `MinTouchPx = 112` (`ElarionUiKit.cs:347`); that trades an overlap for a
SUB-TOUCH-FLOOR finding and fails just as loudly.

## Acceptance

- [ ] The `[ui-touch-oracle]` suite (`Assets/Editor/Regression/UiTouchClampRegression.cs`) reports
      **ZERO** findings whose panel label starts `RumorBoard_`, at **both** captured aspects
      (1080x2340, 1200x2670).
- [ ] `CloseButton`, `ObsBtn_Accept` and `ObsBtn_Track` have **non-overlapping** vertical spans —
      prove it with the resolved rects from a fresh capture log, not by reading the source. Overlap
      tolerance is `LayoutOracle.OverlapPadPx = 2f` (`LayoutOracle.cs:52`), so a 2 px touch is legal
      and 3 px is not.
- [ ] All three `DetailRewardRow` chip labels are fully uncovered.
- [ ] No `SUB-TOUCH-FLOOR BAND` finding appears on `RumorBoard_` — every button's shortest side stays
      **>= 112** ref px.
- [ ] The repo-wide marker count drops by exactly 18 — from `UI_TOUCH_FAIL x43` to `x25` — with no
      other panel's findings changed.
- [ ] COMPILE_GATE_OK + the regression marker.

⛔ **THE ALLOW-LIST IS NOT AN OPTION.** `TouchBaseline` (`Assets/Editor/UICaptureLaunch.cs:3771`) stays
at its **two** entries — `ArmyMuster` and `EquipDrawer`. The owner ruled 2026-08-24 (batch 2, ruling 9):
*"Do not celebrate creating a smoke alarm by taking the batteries out when it starts beeping."*
Adding `RumorBoard` to it fails this ticket.

⚠ **No acceptance criterion here depends on colour.** Every check is a pixel measurement or a finding
count, both readable without hue.

## Do NOT touch

- ⛔ `Assets/_Modules/Core/UI/LayoutOracle.cs` — do not narrow the rule to go green.
- ⛔ `TouchBaseline` in `UICaptureLaunch.cs`.
- ⛔ `ElarionUiKit.MinTouchPx` / `CanonCtaHeight` / `SeatSharedCloseInside` — the shared Close is
      shared. Fixing this panel by moving the canonical close moves every other panel with it.

---

## REOPENED 2026-08-25 - the FIXED flip was refuted by captured data

**Verified by the CLI lead. Proving log: `Builds/uicap-0825am.log` (fresh, 06:00, marker `UI_CAPTURE_OK 89`).**

The ticket was flipped to FIXED on 2026-08-24 on the reasoning that `a2162f17d` had already shipped
the fix. That commit **is** real and **did** touch this panel (+90 lines) and shipped
`RumorBoardLayoutRegression.cs`, which is **green in today's `REGRESSION_OK 274/274`**. The panel is
still broken anyway.

### The proving lines, verbatim from the fresh capture

```
[touch-oracle] BUTTON OVER TEXT [RumorBoard] 'ObsidianPanel/PanelContent/CloseButton'                    x10
[touch-oracle] BUTTONS OVERLAP  [RumorBoard] 'ObsidianPanel/PanelContent/CloseButton'                    x4
[touch-oracle] BUTTON OVER TEXT [RumorBoard] '.../DetailPane/DetailCta/ObsBtn_Track'                     x2
[touch-oracle] BUTTON OVER TEXT [RumorBoard] '.../DetailPane/DetailCta/ObsBtn_Accept'                    x2
```

**18 findings, composed exactly as this ticket originally described** (14 BUTTON OVER TEXT + 4
BUTTONS OVERLAP). The pin warning that **BOTH `ObsBtn_Accept` AND `ObsBtn_Track` are buried** is
confirmed - a fix clearing only Accept leaves the panel red.

### WHY THE GREEN GATE DID NOT CATCH IT - the transferable part

`RumorBoardLayoutRegression` is a **source-structural** check: it reads authored constants out of the
source (`ConstFloat(kit, "MinTouchPx", ...)`) and asserts they are correct. The touch oracle measures
the **resolved rectangles of the rendered panel**. Those are different claims, and only the second one
is about what the player's thumb hits.

⛔ **So the suite can be green while the panel is broken, forever.** This is
`docs/INSTRUMENTATION_STANDARD.md` §1.4b - *assert outcomes, not intent; resolved sizes rather than
authored ones* - and it is the WO-1138 hollow-pass family arriving through a door nobody was watching.

⚠ **Do not "fix" this by weakening either check.** The structural regression is useful; it is simply
not evidence about geometry. The acceptance criterion for this ticket is **a fresh capture showing
RumorBoard at zero findings**, not a green suite.

### The baseline number in this ticket is stale - use 21, not 43

⛔ This ticket (and 1075/1077/1078) each computed a drop from `UI_TOUCH_FAIL x43`, taken from
`Builds/wo1060-capture.log`. **The current measured total is 21**, and 18 of them are this panel.
The other three panels are now **clean** - `RaidDeployScreen`, `EndStateView` and `DialogueView` each
return **zero** touch findings on the fresh capture, so their fixes are proven, not merely reasoned.

**Remaining after this ticket lands: 3**, all on `BuildPaletteDock` - a panel no ticket covered until
today. See the newly minted work order for it.

---

## CORRECTION 2026-08-25 (CLI lead) - I REOPENED THIS TICKET ON A FALSE READING

⛔ **The "REOPENED 2026-08-25" section above is WRONG in its conclusion and is superseded by this
block.** It is kept, not deleted, because the mistake is the useful part.

### What I asserted this morning, and why it was wrong

I read 18 findings on a fresh capture, saw `RumorBoardLayoutRegression` green in the same run, and
concluded the panel was broken and the regression was a hollow gate asserting authored intent instead
of resolved geometry. **The panel was fine. The harness was re-authoring it before photographing it.**

`Assets/Editor/UICaptureLaunch.cs:1832-1833` held a PRIVATE COPY of the retired `0.05` portrait floor
and wrote it onto `_detailPane` **after `Open()` returned and before `RenderCanvasToPng` ->
`AuditGeometry` ran**. So the oracle measured the harness's number, not the panel's.
`grep -rn "detailPane\.anchor"` finds exactly one external writer, and that was it.

`a2162f17d` had ALREADY replaced that literal with `CloseReserveTopFraction`, which reads the Close's
own seated anchor - its own comment says *"instead of hoping a hardcoded 0.05 clears it."* The
regression was green **because the panel is correct**, not because the check is hollow.

⭐ **On a real device the portrait Rumor Board has been correct since 2026-08-21**, with roughly 60 ref
px between the CTA row and the Close. No player ever saw this defect. The 18 findings were phantoms
produced by the measuring instrument.

### The arithmetic that proves it, entirely from `Builds/uicap-0825am.log`

Flow line at `:3872` for 1080x2340: `canvasH=2120 panelAnchors=(0.08,0.10)-(0.92,0.90)
closeBandTop=0.128`. The panel computes its floor as `Clamp(0.128 + 0.02, 0.16, 0.45)` = **0.16**.
The captured pane bottom resolves to exactly **0.050**. Both cannot be true: something wrote 0.05
after the panel computed 0.16. Same at 1200x2670.

### It IS still this repo's dominant failure - just not the one I named

**One fact written in two places, and the copies drifted.** The portrait floor lived in the panel AND
in the harness; the panel was corrected and the harness's copy was not. Identical in shape to the
stale WO-number block, the retired dependency table, the hardcoded repo root and the 3-of-28 fallback
catalog.

⭐ **And the new rule it earns: a harness must PHOTOGRAPH the panel, never RE-AUTHOR it.** Any
re-assert of production layout inside the capture path is a second source of truth for that layout,
and it will drift. When it does, it manufactures findings that look exactly like real defects - this
one reopened a finished ticket and cost a morning.

### What landed

1. **`UICaptureLaunch.cs:1832-1833` DELETED** (lead, not updated to a new number - updating it would
   have preserved the duplicate). A comment in its place explains why nothing may be re-added there.
2. **`RumorBoardPanel.cs`** - the portrait floor moved out of the ANCHOR and into a **pixel offset**
   (`offsetMin`), so it now fails safe: an anchor overwrite can only push the pane further from the
   Close, never back into it. Purely geometric; nothing re-sorted, no `SetAsLastSibling`, `DetailCtaPx`
   still 112 so no SUB-TOUCH-FLOOR band can appear. `TouchBaseline` untouched.
3. A `FlowTrace` line now publishes the resolved floor in both fraction and px, so the next capture
   NAMES the number instead of requiring this reconstruction. That is the falsifiable-assertion rule
   (`docs/INSTRUMENTATION_STANDARD.md` section 1.4b) applied to the thing that fooled me.

### Ticket errors found on the way, recorded so nobody re-inherits them

- The ticket's "Root cause - visible in the source" section blames `FooterBandPx = 200f` as "the band
  that seats the shared Close". `BuildFooterBand` returns early on portrait and does not seat the
  Close on any path. The Close is seated by `ElarionUiKit.SeatSharedCloseInside`.
- The findings are emitted as `[RumorBoard_1080x2340 @1080x2340]`, not the `[RumorBoard]` this ticket
  quotes. A matcher keyed on the short form matches nothing.
- `193.6` confirmed at 1080x2340 and `193.4` at 1200x2670. The rounded "194" in WO-1060's prose is
  wrong, as the pin said.

⚠ **ACCEPTANCE IS A FRESH CAPTURE SHOWING RumorBoard AT ZERO FINDINGS** - not this reasoning. The
numbers in the implementing seat's report are arithmetic reproduced from the log, not from a new run.
