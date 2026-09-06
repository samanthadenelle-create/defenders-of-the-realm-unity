# STANDING GOAL: iterate the Manage screens against the mockups until they match

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
