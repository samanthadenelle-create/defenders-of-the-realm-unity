# WO-1491 RESULT - the back arrow is a sprite, CLOSE is the hub's alone, the title joins on a hyphen

**Status:** IMPLEMENTED AT SOURCE, UNGATED. Uncommitted in the working tree, 2026-09-07.
No Unity run was in this lane's scope, so nothing below is proven on a frame. The evidence for every
DEFECT is the owner's own device capture, named per item; the evidence for every FIX will be the next
`COMPILE_GATE_OK` + `REGRESSION_OK` + `MANAGE_FLOW_MAP_OK` with the PNGs opened.
**Commit:** none - working tree only.

---

## 1. WHAT LANDED

| Item | File:line | What changed |
|---|---|---|
| The `<-` text back button | `Assets/_Modules/Core/Manage/ManageArt.cs` (`IconBack`), `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` (`ApplyBackGlyph`, called from `BuildBackArrow`) | The face is now the delivered kit sprite `UI/ElarionMedieval/Manage/icon-back`, bound as a raycast-off child image inside the existing obsidian plate. The ASCII literal survives **only** as the miss fallback, and the miss is announced by key. |
| CLOSE on panels the mockup draws without one | `Assets/_Modules/Core/UI/ElarionUiKit.cs` (`BuildObsidianPanel`, new `withClose` parameter), `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` (`_chromeClose`, `ApplyScreenVisibility`) | Two levers, deliberately separate: `withClose: false` is the **per-panel** build-time option the ticket asked for (default `true`, so all ~19 existing callers are unchanged); Manage builds ONE chrome and swaps SCREENS inside it, so it toggles `chrome.close` per screen instead. CLOSE is visible on the hub and nowhere else. |
| Header spelling | `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs` (`HeaderJoiner`, `HeaderTitle`) | `"MANAGE / BUILD"` -> `"MANAGE - BUILD"`, off ONE shared constant read by all three arms. |
| `12 MORE - SCROLL` | `Assets/_Modules/Core/Manage/ManageWorkspacePanel.cs` (the overflow strip) | Now `"+12 MORE"` - a badge, not an instruction telling the player how to use a touchscreen. |

**Oracle:** `Assets/Editor/Regression/ManageMockupConformanceRegression.cs` - new case
`CheckChrome` with three assertions and a RED recipe each (`[chrome-back-glyph]`,
`[chrome-close-on-hub-only]`, `[chrome-title-spelling]`). The suite's reason line moves from
`6 cases` to `8 cases`.

## 2. ⛔ TWO ITEMS ARE **NOT** DONE, AND ONE OF THEM IS A CORRECTION TO THIS TICKET

### 2a. `ProgressText` IS NOT DEAD ANY MORE - the ticket's evidence went stale, so it was NOT deleted

WO-1491 section 1 records `ManageViewContract.cs:284  ProgressText  -- declared, never composed,
never painted` and section 2 says to delete it. **Re-read at source 2026-09-07, that is no longer
true:** `ManageVmProjection.cs:337` composes it -
`ProgressText = running != null ? FormatDuration(running.RemainingSeconds) : null`. Deleting the
field today would break the projection, not remove dead weight.

**Owed, and it is a smaller question than the ticket asked:** does any renderer PAINT
`ManageSelectionVM.ProgressText`? If not, the WO-1444 "composed but unpainted" class applies and
the fix is a renderer binding or a deletion of both halves - not a deletion of the field alone.
Left open rather than guessed at.

### 2b. Two of the five copy artifacts were not found in the tree

`"stragglers. ."` and `"A defensive tower   auto-fires"` are authored data, not code literals -
neither string is in any `.cs` under `Assets/_Modules`. They belong to a catalog/description row and
need a data edit this lane does not own. Not fixed, not silently ticked.

## 3. ACCEPTANCE

- [x] The back-arrow artifact and the `12 MORE - SCROLL` artifact gone at source.
- [ ] The other two copy artifacts - see 2b, they are data rows.
- [x] CLOSE present on panel 1 only (source; the per-screen toggle is `ApplyScreenVisibility`).
- [ ] `ProgressText` deleted - **DELIBERATELY NOT DONE**, see 2a. The ticket's premise is stale.
- [ ] Fresh Manage PNGs opened. OPEN - no Unity run in this lane.
- [ ] `REGRESSION_OK n/n` on a fresh log. OPEN - same reason.
