# WORK ORDER 1230 - The Army Muster screen is unreadable: six collisions, and the edited number wraps

**Status:** READY TO IMPLEMENT
**Silo:** UI layout  --  **routed to the UI SEAT** for the layout spec/mockup, per owner ("send this to ui")
**Severity:** P1. This is the screen that spends the player's gold, and the quantity field cannot be read.
**Origin:** Owner felt-test, Seeker build `2026.08.26.342290`, 2026-08-26. Owner verbatim:
***"i cant understand this screen"***.

**EVIDENCE (open it before designing anything):** `tmp/wo-army-muster-2026-08-26.png`, 2670x1200,
captured off the device at 12:43:58.

---

## What the screen is meant to be

The Army Muster / loadout screen (WO-934, `ArmyStorage.loadouts` = 3 named presets + `activeLoadout`).
- **Top:** the three loadout slots (`Raid` / `Hold` / `Siege`) + `Clear`.
- **Left:** the unit roster, each row a name + cost/time + a `-` / count / `+` stepper.
- **Right:** the staged summary (composition, cost, time, shortfall, queue occupancy).
- **Bottom:** name the slot, `Save slot N`, `Muster`.

Source: `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs`, `Village/Troops/ArmyLoadoutService.cs`,
`Village/Troops/ArmyComposition.cs`.

## The six collisions, measured off the capture (coords in the 2670x1200 frame)

1. **THE WORST ONE - the count wraps to two lines.** Footman's `20` renders as `2` at y~240 and `0`
   at y~304, stacked in a single narrow column at x~981. **The player cannot distinguish 20 from 200
   from 2.** This is the number they are actively editing with the steppers beside it. Archer's `0`
   sits below on the same column, so the two rows' digits also read as one number.
2. **`Raid` / `Hold` / `Siege` / `Clear` are drawn ON TOP of the panel title** (button band y~139).
   The title survives only in the gaps between buttons - single glyphs `n` (x~1055), `-` (x~1335),
   `t` (x~1615). The player never sees the panel's name.
3. **`Archer` (y~371) overlaps its own cost line** `550 Gold - 1m 00s each` (y~403). The Footman row
   above it does not overlap, so **the row height does not accommodate a two-line cost string** -
   Archer's `1m 00s each` wraps to a second line and pushes into the next row.
4. **`Save slot 1` covers another control.** A `Cl...` fragment is visible behind/above it at y~991 -
   almost certainly `Close`. `Name: Quick S...` is clipped mid-word. Three bottom-bar controls plus a
   fourth hidden one are competing for one band at y~1059.
5. **The gold rail (y~874) does not read as a value.** A coin icon at x~641 and `14` at x~2196, with
   ~1500 px of empty rail between them. Whether `14` is truncated is unknown - **identify what this
   widget is actually reporting and say so in the RESULT**; do not assume.
6. **The right summary panel is CREAM on an otherwise all-obsidian UI.** It is the only light surface
   in the frame and does not belong to `ElarionUiKit`'s palette.

## Data facts, read at source (do not re-derive, do not "fix" these here)

- `troops.json`: `troop-footman` **costGold 550**, `troop-archer` **550**, spearman 850,
  field-cleric 205, shieldguard 1150, outrider 1500, battlemage 1450, echo-legionnaire 2400,
  catapult 3400.
- So the staged `20x Footman` = **11000 Gold**, and the panel correctly says `Short of: Gold`.
  The owner's town held ~1664 Gold at capture time.
- **=> The screen opens pre-staged with an army the player cannot afford.** That is arguably correct
  behaviour for a restored slot, but combined with the unreadable count it reads as a broken screen.
  **FLAG IT, do not change it** - whether a restored loadout should clamp to affordable is an OWNER
  RULING, and it is an economy decision, not a layout one.

## Check this FIRST - likely shared root with WO-1083 and WO-1228

WO-1083's implementer proved `ElarionUiKit.BuildObsidianPanel`'s **close-band reservation**
(`ElarionUiKit.cs:628-677`) raises FrameCore's body-zone floor from the frame-measured **0.075 to
~0.3525** on a landscape canvas, crushing every element into one band. WO-1228 (TREASURE FOUND) has
the same signature. **This screen has it too**: a title overrun by the band below it, a stepper column
too narrow for its content, and a bottom bar with four controls in one lane.

If it is the same cause, all three fix the same way and **must not diverge**. If it is NOT, say so
explicitly with the measurement that rules it out.

## ROOT CAUSE FOUND AT SOURCE - it is NOT the close-band reservation

`ArmyMusterPanel.cs:107-108`:

```csharp
_selectorHost = MakeCommandBand(chrome.content.transform, "LoadoutSelectorBand", true);
_actionHost   = MakeCommandBand(chrome.content.transform, "MusterActionBand",   false);
```

**Both command bands are parented to `chrome.content.transform` - the raw frame content - while every
other element in this panel is correctly parented into a LAYOUT ZONE** (`layout.bodyLeft` for the
roster, `layout.bodyRight` for the summary, `layout.footer` for the wallet; lines 91-102). The two
bands therefore sit OUTSIDE the zone system and overlay whatever the layout already placed:

- `LoadoutSelectorBand` (Raid/Hold/Siege/Clear) lands over the **title zone** -> collision 2. The
  surviving `-` glyph at x~1335 is the dash in the panel's own title, **"Armies - Loadouts"** (line 86).
- `MusterActionBand` (Name / Save slot / Muster army; lines 441-446) lands over the **footer zone**,
  where `BuildWalletRow` was already built (line 109) -> collisions 4 and 5.

**This is a DIFFERENT root from WO-1083 / WO-1228.** Do not fix it by touching the close-band
reservation - fix the parenting so the bands occupy real zones. If the layout has no band zone,
adding one is the correct change, and say so.

## The `Cl...` control - IDENTIFY IT, do not guess

Something whose label begins `Cl` renders at y~991 in large type, partly behind `Save slot 1`. The
action band declares only three children (Name 0.00-0.32, Save 0.34-0.66, Muster 0.68-1.00), so it is
NOT one of them - it belongs to a lower layer the band is covering. Strongest candidates: the ONE
shared Close from `BuildObsidianPanel` (line 87), or the `Clear` recipe face. **Name it in the RESULT
with the line number.** A control the player cannot see is worse than one that is missing.

## Confirmed behaviour - the screen DOES train troops (owner asked)

`ArmyMusterService.Muster(s_composition)` (line 163) **auto-queues Train jobs** into the Obsidian train
queue; the file header states *"Muster army (auto-queues Train jobs) ... army prepares while you play"*.
`Muster 5 of 20` is not a step counter - it is `TrainQueueDepthCap` (5 per line) telling the player how
many start now while the remainder stays staged. **The label is accurate and still failed to
communicate.** Whatever the layout fix is, the muster CTA must make "5 start now, 15 stay staged"
legible without the player reading the summary panel.

## OWNER RULING 2026-08-26 - "MUSTER" IS THE DEFECT, not just the layout

Owner verbatim, shown the UI seat's mockup: ***"what dos muster army mean? Thats where im lost"***.

"Muster" is archaic military jargon (to assemble troops for a roll-call). The screen is a TRAINING
ORDER FORM - `ArmyMusterService.Muster()` enqueues Train jobs into the Obsidian queue and they build
while the player plays - and the word on the CTA is the one word that does not say so. **No layout
work fixes this**, and the word appears in several places, so it must be settled before the mockup is
implemented.

**RULED:**
- The CTA reads **`Train Army`**, keeping the two-line explainer beneath it:
  `Train Army` / `5 start now - 15 stay staged`.
- The tip line becomes **"Training auto-saves this slot. Fill the army, then Raids."**
- Scope is **PLAYER-FACING STRINGS ONLY**. Sweep the panel title, CTA, tip line, and any toast for
  the word "Muster" and replace it.
- ⛔ **Do NOT rename code identifiers.** `ArmyMusterPanel`, `ArmyMusterService.Muster()`,
  `ArmyMusterPlanner` and the `"Muster"` FlowTrace tags STAY. They are live identifiers and a
  rename is a wide mechanical diff across several files and regressions with zero player benefit.
  A regression that greps for the FlowTrace tag must keep passing.

## The UI seat's mockup is APPROVED as the layout direction

The mockup resolves all six collisions: the count field fits three digits (`Spearman 120`), the title
has its own band above the slot buttons, `SHORT OF: Gold` is a readable WORD-CHIP rather than a colour
(correct for a red/green colourblind owner), the wallet reads as one value (`Gold: 1664`), and the
list carries a `+ 4 more (scroll)` affordance. Implement to it, applying the rename above.

## Required

- The count field is wide enough for **three digits without wrapping**, at the roster's font size.
  State what happens at 4 digits.
- Title in its own band; the loadout-slot buttons below it, never over it.
- Row height accommodates a **two-line** cost/time string, or the string is shortened so it fits one.
- The bottom bar's controls each get an exclusive lane, including whatever `Cl...` is - **find it and
  account for it**; a control the player cannot see is worse than one that is missing.
- The gold readout reads as a single value (icon and number adjacent).
- Right panel adopts the obsidian palette.

## Constraints

- **`MinTouchPx = 112`**, and satisfying it may not create a new overlap - that is exactly what broke
  hero-select. DO NOT name `ClampMinTouch` as a cause; ruled out at three sites.
- **The owner is red/green colourblind.** Nothing may be distinguished by hue alone. Greyscale check
  is the gate.
- **ASCII-only TMP strings.**
- **Code-built uGUI via `ElarionUiKit`. NO UXML** - project law.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. **A DEVICE SCREENSHOT at 2670x1200, opened and looked at**, showing a roster with a 3-digit count
   staged. `UI_CAPTURE_OK` alone is NOT acceptance - two broken panels reached the owner behind green
   markers today.
3. A greyscale check of that capture.
4. A regression that FAILS on today's tree - assert the count label's rect fits its formatted string
   without wrapping, and that no two of the named elements' rects intersect. Prove it RED first
   (WO-1138).
5. The RESULT states whether the close-band reservation was the cause, with the measurement, and
   identifies the `Cl...` control and the gold widget.
6. Owner felt-verifies and CLOSES.

## What NOT to touch

- The troop costs in `troops.json`, the loadout save/restore semantics, or `ArmyStorage` schema v38.
  This is presentation only.
- `BuildObsidianPanel`'s close-band reservation without reading WO-1083's RESULT first - other
  screens depend on it.
- The "opens unaffordable" behaviour - flagged above, owner's call.

---

## UI SEAT DELIVERABLE (2026-08-26) - APPROVED LAYOUT SPEC + MOCKUP

**Owner approved the design this session ("go").**
**Mockup (the diff target for the acceptance screenshot):**
`WorkOrders/WORK_ORDER_1230_mockup_2670x1200.png` (also `tmp/armymuster_mockup_2670x1200.png`).

Normative anchor rects - fractions of the 2670x1200 SCREEN (x left->right, y BOTTOM->top).
Implementer maps into the panel's real layout zones (per the RCA above, the fix IS the zone
parenting - the two command bands move INTO zones matching these bands).

| Band / element                | xMin  | yMin  | xMax  | yMax  | notes |
|-------------------------------|-------|-------|-------|-------|-------|
| TITLE band (exclusive)        | 0.120 | 0.892 | 0.880 | 0.975 | title alone, nothing overlays |
| Close X (the `Cl...` control) | 0.911 | 0.888 | 0.955 | 0.982 | shared kit Close, header right |
| Selector band                 | 0.262 | 0.770 | 0.816 | 0.867 | below title, never over it |
| - Raid slot                   | 0.262 | 0.770 | 0.382 | 0.867 | active: gold frame + "ACTIVE - <name>" subline (word, not hue) |
| - Hold slot                   | 0.397 | 0.770 | 0.517 | 0.867 | subline "slot 2" |
| - Siege slot                  | 0.532 | 0.770 | 0.652 | 0.867 | subline "slot 3" |
| - Clear (offset gap)          | 0.697 | 0.770 | 0.816 | 0.867 | visually apart from the slots |
| Roster band (scrolls)         | 0.049 | 0.217 | 0.580 | 0.750 | 5 rows visible, row height 128px @1200 |
| - stepper minus               | 0.356 | (row) | 0.398 | (row) | 112x112 px |
| - COUNT FIELD                 | 0.404 | (row) | 0.479 | (row) | 200px wide: 3 digits + headroom for 4, NO wrap; gold border |
| - stepper plus                | 0.485 | (row) | 0.527 | (row) | 112x112 px |
| Summary panel (obsidian)      | 0.607 | 0.217 | 0.951 | 0.750 | CARD fill, kit palette - cream retired |
| - SHORT OF chip               |   -   |   -   |   -   |   -   | framed word-chip "SHORT OF: <res>", never hue alone |
| Wallet row                    | 0.049 | 0.160 | 0.337 | 0.197 | coin icon ADJACENT to "Gold: <n>" - one value |
| Bottom bar - Name field       | 0.049 | 0.052 | 0.337 | 0.147 | full name, no clip (FitLine) |
| Bottom bar - Save slot N      | 0.360 | 0.052 | 0.584 | 0.147 | exclusive lane |
| Bottom bar - Muster CTA       | 0.607 | 0.052 | 0.951 | 0.147 | gold-framed; TWO lines: "Muster Army" + "<f> start now - <r> stay staged" from live data |

Spec rules the mockup encodes (binding):
- Cost/time strings are ONE line: `<gold> Gold - <t>` with compact time (`45s`, `60s`, `90s`,
  `2m`), never `1m 00s each`. Row height still accommodates two lines as the fallback guard.
- At 4 digits the count field autosizes down (FitLine) inside its 200px box - it never wraps.
- Roster overflow scrolls with a `+ N more (scroll)` hint line under the visible rows.
- The Muster CTA subline is computed from the live queue numbers (the `Fits now` values), so
  "5 start now, 15 stay staged" is legible without reading the summary panel.
- Greyscale-safe: active slot = frame weight + ACTIVE word; shortfall = framed chip + word;
  count emphasis = border, not hue.
