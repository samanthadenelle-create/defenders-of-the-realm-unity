# WORK ORDER 1080 — A layout ticket must cite the capture it came from, and the capture must cite the tree it measured

**Status:** READY TO IMPLEMENT

**Minted:** 2026-08-24, UI seat, from the `CLI_LANES_WO_NUMBERS.md` UI-seat block (1080; banner bumped 1080 → 1081 in the same edit).
⚠ **First drafted as WO-1079 and renumbered before it left this seat.** A concurrent UI-seat edit took 1079 for the MON mainnet-SKR canary rename while this file was being written, and that file was first-on-disk-and-referenced (CLAUDE.md §2). The banner was re-read and the mint moved to 1080 — which is itself an instance of the class this ticket is about: state read once, acted on later.
**Silo:** Tooling / capture harness + board build. **For:** CLAUDE CLI.
**Parents:** WO-1060 (the touch/overlap oracle), WO-1075 / 1076 / 1077 / 1078 (the four tickets this defect produced).

---

## The defect, stated plainly

**Four layout tickets — WO-1075, WO-1076, WO-1077, WO-1078 — were all minted from
`Builds/wo1060-capture.log`, an aged capture, and described a game that had moved on.** Each was
written from resolved rectangles that were no longer the resolved rectangles, and three of the four
were wrong in a way the ticket itself could not detect.

This is not a scolding about one bad night. It is the project's dominant failure mode — **one fact
written twice, and the copy going stale** (CLAUDE.md §2's WO-number block, §5's dependency table,
§0's hardcoded repo root, §16's inlined R2 push). Here the duplicated fact is *the geometry of a
panel*: once in the running code, once in a log file, and the ticket cites the log.

### The four consequences, all documented in `batch_results_state.md` (`HANDOFF 2026-08-24 21:34`)

| Ticket | What the capture said | What the tree said |
|---|---|---|
| **WO-1076** | 18 findings; Close buried Accept + Track | **Already fixed** in `a2162f17d` (2026-08-21, WO-941) — `RumorBoardPanel.CloseReserveTopFraction`. Handed out anyway; the dev seat refused it. **A wasted seat.** |
| **WO-1077** | `TapDismiss` covers 100% of the Repair-All CTA | The premise is **disputed by its own source**: `EndStateView.cs:720` documents the layering as DELIBERATE under WO-672. |
| **WO-1078** | shrink the `TapAdvance` overlay | A **correctly-scoped second control** (the prose-viewport button on the same handler) had already superseded it; the right move was deletion, not shrinking. |
| **WO-1075** | two CTAs under `MinTouchPx` | Held up — the only one of the four whose premise survived contact with the tree. |

### And the arithmetic that cannot all be true

All three of the following were authored against the **same** `UI_TOUCH_FAIL x43` baseline:

- WO-1075 §Acceptance: *"drops by exactly 4 — from `UI_TOUCH_FAIL x43` to `x39`"*
- WO-1076 §Acceptance: *"drops by exactly 18 — from `UI_TOUCH_FAIL x43` to `x25`"*
- WO-1078 §Acceptance: *"drops by exactly 18 — from `UI_TOUCH_FAIL x43` to `x25`"*

(WO-1077's is open-ended — *"at least 3"* — and is the only one that does not assert an end state.)
**If the tickets land together, at most one of those end states can be observed.** Each ticket
silently assumed it was the only change in flight. A seat that lands second reads a "failed"
acceptance criterion on work that succeeded, and the natural next move is to go re-edit a file that
was already correct.

### ⚠ The trap that makes a date check insufficient — read this before designing the fix

`Builds/wo1060-capture.log` carries an **mtime of 2026-08-23 12:40** and an in-log licensing
timestamp of **2026-08-23T17:39:59Z**. The fix it failed to reflect landed **2026-08-21**. The log is
*newer than the commit it does not contain.*

The proof that it is nonetheless pre-fix is arithmetic, not chronological:
`CloseReserveTopFraction` clamps the portrait detail floor to a **minimum of 0.16** of the panel band
on every path, including the un-measurable fallback — yet the log resolves
`DetailPane/DetailCta/ObsBtn_Accept` to y -757.1..-645.1 while `CloseButton` resolves to
y -763.1..-631.1, i.e. the detail pane's bottom BELOW the Close's top. Geometrically impossible with
that floor in place.

⛔ **Therefore: a capture log's file date is NOT evidence of the tree it measured.** Any design that
compares mtimes is already defeated by the case that motivated it. **The capture must record the
commit, and the ticket must cite it.**

---

## Requirements

### R1 — The capture records the tree it measured, and the ticket cites it (MECHANICAL, not prose)

> **A layout/touch ticket may not be minted from a capture log older than the newest commit touching
> the file it targets.**

That rule must be **checkable by a machine**, because a rule that lives only in prose is exactly the
fact-written-twice this ticket exists to kill. The chain:

1. **`RunCaptureHeadless` emits a provenance marker** alongside its existing markers — a new, DISTINCT
   marker line (CLAUDE.md §8: one marker per entry point, never a shared string):

   ```
   UI_CAPTURE_HEAD <40-char sha> <branch> dirty=<true|false>
   ```

   Read it from git at run time. `dirty=true` when the working tree has uncommitted changes under
   `Assets/` — a capture of an uncommitted tree can never be cited by a ticket, because there is no
   commit for a later reader to diff against.

2. **A minted layout ticket carries a provenance line** in its header block, in a fixed, parseable
   shape:

   ```
   **Capture:** `Builds/<log>.log` @ `<sha>` — targets `Assets/.../<File>.cs`
   ```

3. **`tools/board_build.py` enforces the pair.** For every WO whose body carries a `**Capture:**`
   line, resolve `git log -1 --format=%H -- <target path>` and compare against `<sha>`: if the
   newest commit touching the target is **not** reachable from the cited capture sha, the ticket is
   **STALE-CAPTURE** and the board says so, loudly, in its own column — the same way it already
   flags `DUPLICATE_WO_NUMBERS` rather than silently repairing it.
   A ticket with no `**Capture:**` line is untouched: this gate binds layout/touch tickets, not the
   whole board.

**Judge by the marker on a fresh log, never the exit code** (CLAUDE.md §16; memory
`gates-report-success-without-proving-it`).

### R2 — Per-panel counts bind; a repo-wide total must name its baseline

- The **binding number in a layout ticket is its own panel's finding count.** That number is local to
  the file the ticket targets and survives a sibling ticket landing beside it.
- A **repo-wide total** (`UI_TOUCH_FAIL x<n>`) may appear in an acceptance criterion **only** when it
  is (a) measured from a capture taken for THAT ticket, and (b) written with its baseline named:
  *"from `x43` as measured at `<sha>`"*. Never a bare "drops to x25".
- ⛔ **A repo-wide end state must never be the sole acceptance criterion**, because file-disjoint
  tickets are designed to land in parallel (CLAUDE.md §11) and each one moves that total.
- Retro-fix the three live instances: WO-1075, WO-1076 and WO-1078 each assert a bare end state, and
  WO-1076's is already annotated stale.

### R3 — Re-run the capture before minting, with the command written down

Before a layout/touch ticket is minted, **re-run the capture.** The command, which this WO states so
the requirement is executable rather than aspirational:

```
powershell -File .\run-unity-method.ps1 `
  -Method DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless -LogName ui-capture.log
```

Output lands in `Builds/ui-capture/<PanelName>_<w>x<h>.png` plus the marker lines the caller greps:
`UI_CAPTURE_OK <count>`, `UI_CAPTURE_FIDELITY_OK <n> builds`, `UI_GEOMETRY_OK <n> canvases`,
`UI_TOUCH_OK <clean>/<checked> panels` (or `UI_TOUCH_FAIL x<n>`), `UI_ENDSTATE_FIT_OK <n> banners` —
and, after this ticket, `UI_CAPTURE_HEAD <sha>`.

⚠ **`UI_CAPTURE_OK` is a pre-ship gate and opening the PNGs still binds** (CLAUDE.md §8; memory
`headless-screenshot-verify-ui-before-build`). This ticket adds provenance to that run; it does not
replace any part of it.

---

## Files to edit

- `Assets/Editor/UICaptureLaunch.cs` — emit `UI_CAPTURE_HEAD` from `RunCaptureHeadless` (beside the
  existing `ReportFidelity` / `ReportGeometry` / `ReportTouchOracle` / `ReportEndStateFit` calls at
  `:556`-`:561`), plus its own doc-header line in the OUTPUT block at `:32`-`:43`.
- `tools/board_build.py` — parse the `**Capture:**` line in `parse_wos()`; add the STALE-CAPTURE check
  and surface it in `build_html()` beside the existing duplicate-number flag.
- `docs/INSTRUMENTATION_STANDARD.md` — record the provenance marker + the `**Capture:**` line shape as
  the standard for capture-derived tickets.
- `WorkOrders/WORK_ORDER_1075_raid_deploy_footer_below_touch_floor.md`,
  `WorkOrders/WORK_ORDER_1078_dialogue_tapadvance_covers_options.md` — annotate the bare repo-wide
  end-state criteria per R2 (WO-1076 is already annotated).

## Acceptance

- [ ] A fresh `RunCaptureHeadless` log contains `UI_CAPTURE_HEAD <sha> <branch> dirty=<bool>`, and the
      sha matches `git rev-parse HEAD` at the time of the run. Prove it from the log, not the source.
- [ ] `dirty=true` is emitted when a tracked `.cs` under `Assets/` is modified and uncommitted, and
      `dirty=false` on a clean tree. Both directions demonstrated.
- [ ] `python tools/board_build.py` flags a WO carrying a `**Capture:**` line whose cited sha predates
      the newest commit touching its named target file — demonstrated on a deliberately-stale
      fixture — and does NOT flag a WO with no `**Capture:**` line.
- [ ] `board_build.py` still runs in ~2 s and its existing output (statuses, RESULT markers, banner
      parse, `BANNER_OK`, `DUPLICATE_WO_NUMBERS`) is byte-unchanged for every WO with no
      `**Capture:**` line.
- [ ] WO-1075 and WO-1078 no longer assert a bare repo-wide end state.
- [ ] `COMPILE_GATE_OK` + the regression marker, judged on a **fresh** log by marker, never by exit
      code.

⚠ **No acceptance criterion here depends on colour** (memory `owner-colorblind-delegate-visual-creative`).

## Do NOT touch

- ⛔ `Assets/_Modules/Core/UI/LayoutOracle.cs` — this ticket changes **provenance**, not what the
  oracle asserts. Narrowing a rule to reduce findings is a separate, owner-ruled decision (it is the
  open lead call surfaced by WO-1077 + WO-1078, and it stays open).
- ⛔ `TouchBaseline` in `UICaptureLaunch.cs` — stays at its **two** entries (`ArmyMuster`,
  `EquipDrawer`), owner ruling 2026-08-24 batch 2 ruling 9. *"Do not celebrate creating a smoke alarm
  by taking the batteries out when it starts beeping."*
- ⛔ `ElarionUiKit.MinTouchPx` / `CanonCtaHeight` / `SeatSharedCloseInside`.
- ⛔ Any panel `.cs` — no layout is changed by this ticket. `RumorBoardPanel.cs` in particular is
  **already fixed** (`a2162f17d`) and must not be re-edited from the stale findings in WO-1076.
- ⛔ `Builds/wo1060-capture.log` — leave it on disk as the evidence this ticket is built on. Do not
  delete it and do not regenerate over it; a fresh run writes `Builds/ui-capture.log`.
- ⛔ Do not re-inline the capture command into a second script or doc. If a chain needs it, it calls
  the one runner (`run-unity-method.ps1`) — CLAUDE.md §16's rule, same reason.
