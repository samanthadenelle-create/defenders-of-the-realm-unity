# STANDING GOAL: iterate the Manage screens against the mockups until they match

---

## RULING 2026-09-07 - THE GOAL IS A SCREENSHOT THE OWNER JUDGES, NOT A CAPTURE A SEAT READS

**Owner, 2026-09-07 01:10, verbatim:**
> *"fix the board so those tickets dont say done and update the goal to be screenshots proving
> these match"*

**Owner, 2026-09-07 01:12, verbatim - THE PASS THRESHOLD:**
> *"95% coverage in size font style context images"* ... *"thats the minimum threshold to pass"*

**Owner, 2026-09-07 01:14, verbatim - THE HARD CRITERION ABOVE THE FIVE AXES:**
> *"i expect these images to fill the screen, not 60% of it"*

### Why this ruling exists

Commit `949e848a0` (2026-09-06 18:51) is titled *"all nine screens match the owner's mockup -
twenty-four capture rounds"* and records `geometry = 0  touch = 0  fidelity = 0  named faults = NONE`.
**That claim was false.** The same night the owner walked all nine Manage screens on device build
358872 with `docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png` beside her, and **not one screen
matched** - see the scorecard below. Measured by classifying each ticket's own prior status line, not
quoted: **twelve board rows were sitting in the finished buckets** on the strength of that claim - ten
whose status led with `IMPLEMENTED` (board bucket Done) and two with `FIXED` - plus
`WO-2006` and `WO-2008` in this folder, which are not board rows at all because `parse_wos` globs
`WorkOrders/*.md` flat and never descends into `ManageRedesign/`.

The failure is not the layout work. It is that a seat read its own headless frames and **declared the
comparison passed**. A headless capture proves frames were written and that measurable oracles
(geometry, touch, fidelity) are green. It cannot prove the picture matches, and 24 rounds of it did
not.

### THE ACCEPTANCE, BINDING ON EVERY MANAGE TICKET

1. **The acceptance for every Manage screen is a DEVICE SCREENSHOT placed beside its mockup panel and
   judged as a match BY THE OWNER.** Nothing else is the acceptance.
2. **Headless captures and seat-read comparisons are EVIDENCE TOWARD it, and can never mark a ticket
   done.** They are how a lane decides it is ready to be looked at. They are not the verdict.
3. **A ticket may only move to DONE when the owner says the frame matches.** Until she has said it, a
   Manage ticket's status is `AWAITING OWNER MATCH` and the board buckets it **Verify** - a bucket
   added to `tools/board_build.py` on this ruling precisely so these can never read as finished again.
   `Verify` is deliberately ineligible for `board_close_pass.py`, which closes only `Fixed`.
4. **CRITERION ZERO - THE PANEL FILLS THE SCREEN.** Full bleed inside the safe area, like every mockup
   panel. **Not a 60%-width plate floating over the town.** This is judged first and it multiplies
   everything under it: a correctly-proportioned element inside a 64% plate is still the wrong size on
   the device. Measured tonight: every frame was taken through a plate at x 0.18-0.82 = 64% of the
   canvas.
5. **THE FIVE AXES, AND THE 95% FLOOR.** A screen passes only when the owner judges the device frame
   **at least 95% matched** to its mockup panel on each of:
   - **SIZE** - element and type dimensions relative to the panel.
   - **FONT** - face, weight and case. (Truncation and clipping are recorded under SIZE and CONTEXT,
     not here - the axis names are the owner's five words and she has not defined them further.)
   - **STYLE** - shape language, borders, fills, dimming, badge treatment.
   - **CONTEXT** - what is on the screen and where it sits. A missing stat table or a bare number with
     no label is a CONTEXT failure even when the type is perfect.
   - **IMAGES** - the art that is present, and its treatment (crop, mask, aspect, ring vs square).

   **Anything under 95% on any axis is a FAIL, whatever the headless capture says.**
6. Everything in sections 2-6 below still stands. This ruling does not replace the loop; it names who
   closes it.

### CURRENT STATE - the owner's walk of device build 358872, 2026-09-07 00:47-01:04

**Scorecard legend:** `no` = the owner's walk recorded a divergence on this axis tonight.
`unscored` = she has not scored that axis yet; it is NOT a pass. **No row passes.**
Frames are under `Logs/device/screens/`.

| # | Screen | Frame | FILLS SCREEN | SIZE | FONT | STYLE | CONTEXT | IMAGES | The gap she named |
|---|---|---|---|---|---|---|---|---|---|
| 1 | MANAGE hub | `owner-screen-20260907-004724.png` | no | no | unscored | unscored | no | no | no card art; cards tiny (~2.2:1 against the mockup's 0.9:1); every description truncated |
| 2 | MANAGE / BUILD grid | `owner-screen-20260907-004825.png` | no | no | unscored | unscored | unscored | no | ring medallions instead of square art; "SHORT 28..." truncates; blank tier tiles |
| 3 | BUILDING detail | `owner-screen-20260907-004903.png` | no | no | unscored | unscored | no | no | no before/after stat table; bare cost numbers naming no resource; circular art |
| 4 | MANAGE / ARMY grid | `owner-screen-20260907-005136.png` | no | unscored | unscored | no | unscored | no | ring medallions; locked troops at full brightness, not dimmed (dimming is a STYLE call, so this row scores STYLE) |
| 5 | TROOP detail | `owner-screen-20260907-005222.png` | no | unscored | unscored | unscored | no | unscored | no cost band |
| 6 | TROOP detail, locked | `owner-screen-20260907-005311.png` | no | unscored | unscored | unscored | no | unscored | requirement glued into the description line |
| 7 | RESEARCH picker | `owner-screen-20260907-005358.png` | no | no | unscored | no | no | no | 2x2 of short wide tiles with a dead well beneath; banner art stretched through an oval mask |
| 8 | RESEARCH tree | `owner-screen-20260907-010151.png` | no | unscored | unscored | unscored | no | no | no school painting; requirements glued to the benefit line |
| 9 | QUEUE overlay | `owner-screen-20260907-010257.png`, `-010356.png` | no | no | unscored | unscored | no | unscored | the well is two rows tall; rows clipped top and bottom |

**Tickets held at `AWAITING OWNER MATCH` on this ruling** (board bucket `Verify`, none of them Done):
WO-1405, WO-1422, WO-1443, WO-1479, WO-1488, WO-1516, WO-1517, WO-1518, WO-1541, WO-1563, WO-1564,
WO-1565 - twelve board rows - and, in this folder and invisible to the board, WO-2006 and WO-2008.

**Left DONE / CLOSED deliberately, with the reason, so nobody re-opens them by sweep:** WO-1382,
WO-1390, WO-1418, WO-1435, WO-1436 are `CLOSED - owner felt-test PASS` and carry her own sign-off in
`proof/owner-validations.json` - **hers to reopen, not a seat's.** WO-2001 is SUPERSEDED (a closure,
not a match claim); WO-2002 is a UI contract; WO-2003 and WO-2011 are model/data lanes whose FIXED is
about the gate, not the pixels; WO-2005 is inventory reconciliation; WO-2013 is navigation behaviour;
WO-2017's Heart surface is not one of the nine mockup panels. WO-1487 is already SPEC (blocked on art).

---

**Owner directive, 2026-09-06, verbatim:**
> *"i want you to run these as a goal. Keep doing them and testing with images till they match the mock ups ok?"*

**Status: ACTIVE.** This is not a work order that completes; it is a LOOP that runs until the screens match.
It survives context loss deliberately - if a seat picks this file up cold, it can resume the loop from
section 3 without re-deriving anything.

---

## 1. WHY THIS LOOP EXISTS

Every Manage defect found on 2026-09-06 was found by the OWNER, on her phone, mid-play:
three stacked headings, chips nobody needs, empty portraits, a dead band, four buildings under a filter
that says ALL. **She should never have been the detector** (memory: `never-dragdrop-or-manual-playtest`).

The loop exists because **the whole comparison can be run headless.** There is no reason to spend her
attention on a difference a capture would show.

## 2. THE CYCLE - one iteration

1. A lane implements against its WO.
2. **GATE:** `CompileGate.Run` -> `COMPILE_GATE_OK`, then `DataRegression.RunAll` -> `REGRESSION_OK n/n`.
   Judge by MARKER on a FRESH log, never the exit code.
3. **CAPTURE:** `DeNelle.Editor.UICaptureLaunch.RunManageFlowMapCaptureHeadless`
   (16 frames, marker `MANAGE_FLOW_MAP_OK`) and/or `RunManageOperationalCaptureHeadless`
   (12 frames, marker `MANAGE_OPERATIONAL_CAPTURE_OK`).
   **⛔ CORRECTED 2026-09-06 (WO-1444): OUTPUT IS `Builds/ui-capture/`, NOT `docs/manage-flow-map/`.**
   `UICaptureLaunch.OutDir` is `Builds/ui-capture/` and nothing in the harness has ever written into
   `docs/`. That folder holds the **frozen 09:17 baseline** every WO-200x cites as "run `Builds/flowmap1`";
   a seat following the old line was opening pre-redesign pixels and calling them this round's capture.
   Compare against `Builds/ui-capture/ManageFlow_*` — see `docs/manage-flow-map/00_FRAMES_ARE_A_FROZEN_BASELINE.md`.
   **The frame count is DERIVED from the plan in code (`BuildManageFlowPlan`), never from this line** —
   read it off `CAPTURE_LEDGER_SWEPT ... expected=<n>` on a fresh log, which is the only source that
   cannot go stale. 21 → 16 because WO-2001 collapsed four legacy tabs to three (Defense and Buildings
   are one BUILD tab) and retired the hub; the Research school→perks screen replaces it.
   **Every run now SWEEPS the frames it owns before it starts and HASHES them afterwards** — a missing
   frame is honestly absent (`CAPTURE_LEDGER_MISSING`) and two filenames with identical bytes is a
   `CAPTURE_LEDGER_DUPLICATE` failure, so a stale or duplicated frame can no longer read as fresh.
4. **⛔ OPEN THE PNGs AND LOOK.** Not the marker - the PIXELS. A green marker proves frames were
   written, never that they look right (memory: `headless-screenshot-verify-ui-before-build`).
5. **COMPARE** against section 3's checklist.
6. **FEED BACK** every difference to the lane, with the frame that shows it.
7. Repeat from 1.

**The loop ends when section 3 is all ticked and a fresh capture shows no new difference.**
Not when a lane says it is done.

### ⛔ THE ACCEPTANCE IS EXACT, NOT SIMILAR. Owner, 2026-09-06, verbatim:
> *"I don't want similar ideas. I want this is exactly what shows. If it's not, take another pass.
> Take the screenshots. Check it. If it's not, send it back to another pass."*

**So the test is binary, panel by panel.** Put the capture beside its mockup panel and ask one question:
**does it look like that?** Not "is it better than last round", not "is it close", not "is the difference
defensible". **Any visible difference = another pass.** There is no partial credit and no negotiating a
difference down into an acceptable one.

**The seat running this loop does NOT get to decide a difference does not matter.** If the mockup draws a
back ARROW and the build shows a BACK button, that is a difference. If the mockup shows ten tiles and the
build shows four, that is a difference. Send it back with the frame and the panel it fails against.

**Iterate silently.** She has asked for nothing until it matches - no progress reports, no questions, no
partial results. The only things that reach her are: it matches, or something is genuinely impossible and
she must know why.

## 3. THE TARGET = `docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png`

⛔ **THE MOCKUP IS THE SPEC. OPEN IT. It is eight screens and it answers nearly every question a seat
would otherwise ask her.** Saved to the repo 2026-09-06 because it had been living in a chat attachment
while three lanes built from prose paraphrases of it - which is why the built screens diverged.

**WHERE THE MOCKUP AND A TEXT RULING DISAGREE, THE MOCKUP WINS.** It is the picture she drew of the
thing she wants; a ruling is a sentence about it. Do not stop to ask which - build the mockup, record
the divergence here, and let her overrule it if she cares.

### 3.0 WHAT THE EIGHT SCREENS SPECIFY

| # | Screen | The binding details |
|---|---|---|
| 1 | **MANAGE (main)** | A HUB: three large cards - BUILD / ARMY / RESEARCH - each with a one-line description. `CLOSE` beneath. |
| 2 | **BUILDINGS (grid)** | "Warcraft-style grid", **5 columns x 2 rows = 10 tiles visible**. Filter chips: ALL / ECONOMY / DEFENSE / CRAFT / STORAGE. |
| 3 | **BUILDING DETAIL** | Big art LEFT; right = name, level, one-line purpose, a **before -> after stats table**, upgrade cost with icons, time, one gold `UPGRADE` button. |
| 4 | **TROOPS (grid)** | **"All 9 troops visible, no scrolling"** - an explicit 3x3. |
| 5 | **TROOP DETAIL** | Big art left; stats (Health/Attack/Range/Speed); train cost + time; one gold `TRAIN 1 <UNIT>` button. |
| 6 | **RESEARCH** | Pick a research BUILDING first (4 cards), then its tree. |
| 7 | **RESEARCH TREE** | A simple list of upgrades for the selected building, each with state: Researched / RESEARCH / requirement. |
| 8 | **QUEUE (overlay)** | Tabs BUILDERS / TRAINING / RESEARCH with (n/n); numbered rows; a `SPEED UP` button with a crystal price. |

### 3.0b THE CHROME, ON EVERY SCREEN - this is the part the build got wrong
- **Back is a `<-` ARROW at top-LEFT.** Not a `BACK` word-button.
- **Title is CENTRED**, breadcrumb style (`MANAGE - BUILD`).
- ⛔ **`QUEUE` IS A SMALL PILL AT TOP-RIGHT WITH A RED COUNT BADGE.** Not a tab, not a big chip, not a
  band. Owner, 2026-09-06, saying the same thing in words: *"the queuing doesn't deserve a place here or
  maybe it should be something small up with like the previous next back kind of buttons - I don't think
  it deserves its own lane."* **The mockup had said it since 09:26 that morning.**
- **No `HEART L<n>` chip anywhere in the mockup.** (It currently survives as the only door to the Heart
  surface - see 3d.)
- Selected tile carries a **gold border**. Locked carries a **padlock** and stays selectable.

### 3.0c ⛔ OWNER DIRECTIVE 2026-09-06 - THE MOCKUP IS ABSOLUTE. DO NOT ASK HER ANYTHING.

Verbatim: *"When those screens match, you are done. Until then I don't want anything. I don't want any
questions. I want this to look like those. I gave you all of the assets, I gave you the mock up. This is
your job."*

**She has given the assets and the picture. Every remaining question is answered by opening the mockup.**
Where a text ruling and the mockup disagree, **the mockup wins and no one asks** - it is the picture of
the thing she wants; a ruling is a sentence about it, written before the picture existed.

**The two that were about to be asked, now RULED by the mockup:**
1. **FIVE filter chips - ALL / ECONOMY / DEFENSE / CRAFT / STORAGE. There is no CIVIC chip.** Ruling 5's
   six is superseded. Barracks, Cathedral, Echo Hollow, Store and Healing Caravan must be re-homed into
   the five that exist - **decide it from what each building DOES** and record the mapping in the RESULT.
   Nothing may become unreachable.
2. **Screen 1 IS a hub** - three cards, BUILD / ARMY / RESEARCH, each with its one-line description,
   `CLOSE` beneath. WO-2001's launcher retirement is superseded for this screen.

⛔ **Do not open an AskUserQuestion about the Manage screens.** Not about chips, not about the hub, not
about capacity, not about colour. Build the picture. If something is genuinely not in the mockup, choose
the option most consistent with what IS drawn there, and write the choice into section 6 so it is visible
rather than hidden.

### 3.1 CHECKABLE ITEMS
**Every item is checkable from a capture.** Tick only what a PNG proves.

### 3a. The shell - applies to BUILD, ARMY and RESEARCH alike
- [ ] **ONE heading, not three.** The breadcrumb (`MANAGE / BUILD`) lives in the TITLE position.
      No separate section heading, no sub-line (`Every troop, unlocked or not.` / `Filter: ALL`).
- [ ] **No `HEART L1` chip, no `QUEUE` chip, no `IDLE . 0 OF 5`** in the top row - **gated on proving
      both surfaces keep a door elsewhere** (WO-1430: three panels shipped with no door).
- [ ] **No hint sentence** (`Pick one to see what it does...`). The CLOSE button carries it.
- [ ] **The selection band COLLAPSES when nothing is selected.** No empty bordered box.
- [ ] Top row reads exactly: `BACK        MANAGE / <TAB>`.

### 3b. Content
- [ ] **Every troop portrait renders.** Nine `troop-*.png` exist as single sprites (`spriteMode: 1`);
      the key omits the `troop-` prefix. Not an art request.
- [ ] **Every building portrait renders**, or a missing one is NAMED as an art request - never a silent
      blank, never an invented fallback.
- [ ] **A filter that says ALL shows all**, or says how many it is showing and how to reach the rest.
- [ ] Capacity is DERIVED from the geometry, never a constant that happens to fit today's count.

### 3c. Invariants that must survive every iteration
- [ ] No text band under ~24 px (it renders BLANK, not small).
- [ ] Every tappable target >= `ElarionUiKit.MinTouchPx` (112).
- [ ] **Meaning never carried by hue alone** - the owner is red/green colourblind. Greyscale is the gate.
- [ ] No element overlaps another; nothing clipped mid-word.
- [ ] Every panel has a door `PanelDoorRegression` accepts.

## 4. WHAT THIS LOOP MUST NOT DO

⛔ **Do not tick an item from a lane's report.** A lane saying "the heading is fixed" is a claim; the
capture is the evidence. Every tick cites a frame.
⛔ **Do not ask the owner to check a difference a capture would show.** That is the failure this
loop exists to end.
⛔ **Do not let an interim state become the shipped state.** WO-2006/2008 build the real grids; until
then, RECORD that only N of M items are reachable rather than quietly accepting it.
⛔ **Do not silently redesign.** Her rulings are the target. A better idea goes to her as a question,
not into a capture.

## 5. THE ONE THING THAT STILL NEEDS HER
**Whether it LOOKS right.** The checklist catches structure, overlap, legibility and reachability -
everything measurable. Taste is hers. Show her a capture when the checklist is clean, not before, and
not for each round.

## 6. ITERATION LOG
Append one line per round: date, what changed, which frames were opened, what the comparison found.

- **2026-09-06 - round 0 (baseline).** Defects captured from the owner's own device, not headless:
  `Logs/device/screens/owner-screen-144143.png` (MANAGE / BUILD) and the Army capture in the same
  session. Both show the full 3a stack plus 3b portrait and capacity failures. WO-1443 dispatched and
  its scope widened from Army to the whole shell. **No headless capture taken yet this round** - the
  first one lands after WO-1443 gates.
- **2026-09-06 - the INSTRUMENT was repaired before round 1 could run (WO-1444).** Nothing was
  compared this round; the loop could not run at all. Measured, not inferred:
  - The 14:59 `RunManageFlowMapCaptureHeadless` run **did write 21/21 frames** - to
    `Builds/ui-capture/`, where the harness has always written. `MANAGE_FLOW_MAP_OK` was withheld for
    `geometry=132 touch=116`, NOT for missing frames. `docs/manage-flow-map/` was stale at 09:18
    because **nothing has ever copied there**; step 3 above said otherwise and was wrong.
  - **16 of the 21 frames were byte-identical to a sibling** (`ManageFlow_Defense_*` all 1319974 B,
    `Troops` all 1198852 B, `Research` all 1386082 B). One image, five filenames: the scroll seam
    (`*SelectorRail`) and the selection seam (`_selected*Id`) both died with WO-2001's workspace
    renderer, and the harness reported neither as an error.
  - Fixed: the harness resolves `ManageGridScroll` under `_workspaceHost`; selection is driven through
    the tile's own `Activate` -> `ManageNavEntry(Kind=Detail)`; the frame set is 3 real tabs x 5 states
    + the Research school screen (16), derived from a plan; `manage.lasttab` is pinned so the opening
    screen no longer depends on the previous run; and every run **sweeps then hashes** its frames.
  - **Still open, and it is loop feedback for the workspace lane, not an instrument defect:** the
    132 geometry / 116 touch failures are real. Every `ManageTabs/ObsBtn_*`, `ManageQueueDoor` and
    `ManageFilters/ObsBtn_*` resolves **110.4 px** against `ElarionUiKit.MinTouchPx` **112** (checklist
    3c), and `ManageActivity/Label` overflows its ZoneBacking by 3.7 px. Do not relax the gate.

## 7. QUEUED BEHIND THIS LOOP - do not fold in, do not lose

Raised by the owner mid-loop and deliberately NOT absorbed, because she asked for the nine mockup
panels and nothing else until they match.

- **The MOVE / MANAGE PLACED door (owner ruling 25).** Verified at source 2026-09-06: the capability
  EXISTS - `BuildSelectionUI` builds Move alongside Sell, Upgrade and Cancel, created by
  `BuildModeController` - but it is reachable ONLY from inside build mode, by tapping a placed
  structure. That is the exact door her friend could not find after mis-placing a palisade
  ("he accidentally put a palisade down and now he has no way to move it").
  **None of the nine mockup panels shows a placed-structure management screen**, so it is out of
  scope for this loop and in scope the moment the loop closes. This is the WO-1430 species again:
  a built, working capability behind a mode with no signposted door.
