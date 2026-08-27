# WORK ORDER 1192 - Brom's Rumor Board: full layout redesign

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated).
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1192 -> 1193 in the same edit)
**Silo:** UI / panels
**ROUTED TO: the UI seat.** Owner directive, 2026-08-25: *"That needs handed back to UI to redo
because it doesn't look good."*

> **The UI seat does RCA, specs, work orders and mockups. It does NOT write `.cs`** (CLAUDE.md
> section 2, and it is a hard rule with no exceptions). The deliverable here is a **design spec plus a
> mockup**, handed to the CLI seat to implement. Nothing in this ticket asks anyone to edit code.

---

## The evidence - open these two PNGs before reading anything else

Captured 2026-08-25 by `RunCaptureHeadless`, marker `UI_CAPTURE_OK 89`, on the current committed tree:

- `Builds/ui-capture/RumorBoard_1080x2340.png` - portrait
- `Builds/ui-capture/RumorBoard_2670x1200.png` - landscape, the Seeker's real surface

Also captured: `RumorBoard_1200x2670`, `RumorBoard_1920x1080`, `RumorBoard_2340x1080`.

**A screenshot is the primary evidence for a visual defect.** FlowTrace shows what the code believes;
the screenshot shows what the player sees. Everything below was read off those two images, not
inferred from source.

## What is wrong - PORTRAIT

1. **A large empty parchment slab dominates the upper right.** It fills roughly a third of the panel
   and contains nothing at all. It is the first thing the eye lands on, and it says nothing.
2. **Every quest title truncates to noise.** All three In Progress rows read `Standing Wa...`, and
   their state buttons truncate to `Tra...` and `Und...`. The player cannot tell three quests apart,
   which defeats the panel's whole job.
3. **The `Daily` tab is sheared off** at the right edge of the tab strip. There are five tabs
   (`All / Story / Daily / Gear / Endgame`) and portrait shows two and a half.
4. **The objective text clips mid-word** - *"...wakes the lantern eels. Sh"*.
5. The detail card is stacked underneath the empty slab rather than using it.

## What is wrong - LANDSCAPE (worse, and differently)

1. **The status line overlaps a quest row.** *"The talk of Elarion. Accept what calls to you."*
   renders across the second In Progress card, bisecting it - the row behind it is cut in half and
   unreadable. This is a real, player-visible collision.
2. **The bottom ~40 percent of the panel is empty black**, with the `Close` button floating alone in
   the middle of it. The list column stops after roughly one and a half rows while a screen's worth
   of space sits unused directly below it.
3. **The objective text clips mid-word** - *"...have begun to sin"* (the word is "sing").
4. A thin gold sliver of the detail frame continues below the detail card, framing nothing.

## The point worth carrying: the gate said this panel was almost clean

At the moment these were captured the panel reported **2** `touch-oracle` findings. It looks like
this anyway.

**The oracle checks control overlap and touch-target size. It cannot see emptiness, truncation,
balance, or whether a screen is worth looking at.** That is not a defect in the oracle - it is the
boundary of what any headless marker can prove, and it is why the standing rule is to OPEN THE PNGs.
A green marker never meant the screen was right.

## What the redesign must preserve (constraints, not suggestions)

- **ASCII-only strings.** A non-ASCII glyph renders as a tofu box on device.
- **Never carry meaning by colour alone.** The owner is red/green colourblind. Every state needs a
  word or a shape - `Tracked` / `Underway` as words is correct and should survive.
- **Controls are at least `ElarionUiKit.MinTouchPx` (112) on their touch axis.** Do not shrink a
  button to win space.
- **Build through `ElarionUiKit`** (`BuildObsidianPanel` and friends). `ObsidianFill` is near-black
  and is only legible because it ships with its own gold edge; anything hand-rolled inherits the
  near-black with no edge. There is also a `[ui-obsidian]` ratchet that hard-fails new hand-rolled
  widgets.
- **Both orientations are designed, not one adapted.** Portrait and landscape fail differently here,
  which is the tell that one layout is being stretched to cover both.
- **`LayoutOracle`'s `TouchBaseline` allow-list stays at its two entries** (`ArmyMuster`,
  `EquipDrawer`). Owner ruling 2026-08-24: no waivers. Adding this panel to it is not a fix.
- **Do not put layout back into the capture harness.** `UICaptureLaunch.cs` had a panel-specific
  anchor re-assert; it was deleted 2026-08-25 after it manufactured 18 phantom findings and concealed
  2 real ones. A harness PHOTOGRAPHS the panel; it never RE-AUTHORS it.
- **The reward chip row must not assume four chips or fixed labels.** It currently reads
  `Crystals 220 / Food 90 / Magic 45 / Relic Drowned Ledger`. WO-1163 retires Food in favour of
  Stone, so a layout that hardcodes those four will break on a data change.

## Relationship to WO-1189

WO-1189 fixes a 7.4 ref px overlap between `ObsBtn_Accept` and the status line by re-parenting the
portrait status band to the list column's floor. That fix is correct, is landing now, and diagnoses
the same structural fault visible in the landscape screenshot above - the status band was hung off
the wrong zone.

**This ticket SUPERSEDES that layout.** WO-1189 is the interim correctness fix so the shipped panel
is not broken while the redesign is designed. ⛔ The redesign is not obliged to keep its geometry -
it owns the layout outright.

⚠ One consequence the owner has already been asked about and has NOT ruled on: WO-1189 gives portrait
the same 52 px bottom reservation landscape has, which costs roughly one visible row at rest. If the
redesign changes the list well, that question dissolves and no answer is needed.

## The open design questions - these are the owner's, not the seat's

1. ~~**What is the parchment slab FOR?**~~ **RULED 2026-08-25 by the owner: it holds the QUEST
   ILLUSTRATION.** The slab is the art plate for the selected quest.

   > **BUT THE ART DOES NOT EXIST YET, AND THAT IS THIS TICKET'S BLOCKER.** Verified at source
   > 2026-08-25: `Assets/Resources/Data/Canonical/quests.json` holds **24 quests** whose entries carry
   > exactly four keys - `id`, `stages`, `title`, `type`. **There is no illustration, art, image or
   > portrait field on a quest at all**, and the only quest-related art in `Assets/Resources` is
   > `HudIcons/hud_quest.png`, a HUD icon. `daily-quests.json` is rewards-only (`slot`,
   > `rewardCrystals`, `rewardFood`, `rewardRandomItem`, `rewardWisdom`).
   >
   > So the ruling needs two things that are the OWNER'S, not a seat's:
   > - **The art itself.** 24 quests. Per-quest illustrations, or per-`type` plates reused across
   >   quests of a kind - that choice changes the art budget by an order of magnitude and is hers.
   > - **What the slab shows when a quest has no illustration.** ⚠ This is the load-bearing one.
   >   `WO-831` is the standing precedent: the six Echo emergence PNGs were specified, the code
   >   shipped with a safe fallback, the art was never made, and the beat has degraded to a portrait
   >   ever since. ⛔ A redesign that centres an empty art plate reproduces exactly the defect this
   >   ticket exists to remove - the slab is being redesigned BECAUSE it is empty.
   >
   > ⭐ **Design consequence:** the layout must read as finished with the plate absent, not merely
   >   tolerate it. A frame that only looks right once art arrives is a promise, not a design.
   >   Whether the plate collapses (and the other columns take the space) or shows an authored
   >   default is a design decision the spec must answer explicitly.
2. **What does the player come to this panel to DO?** Accept new work, or track existing work? The
   answer decides which column dominates and which one collapses. Right now neither wins and both are
   cramped.
3. **How many quest rows should be visible at rest** in each orientation?
4. **Does the objective text need to fit in full, or is it a scroll?** Both screenshots clip it
   mid-word, which is the one thing it must never do - a clipped word reads as a bug, whereas an
   obviously scrollable body reads as a design.

## Deliverable

A design spec plus a mockup covering both orientations, with the constraints above satisfied, handed
to the CLI seat for implementation. ⛔ No `.cs` edits from the UI seat.

---

## OWNER RULING 2026-08-25 - illustrations may land later

Ship the orientation-specific redesign without blocking on quest illustrations. The UI map is:

- portrait: narrow quest-list rail at left, optional illustration plate at upper right, and the
  selected quest detail card across the lower region;
- landscape: quest list at left, with selected quest detail and optional illustration sharing the
  right region; do not preserve the current empty lower field.

When illustration data or art is absent, the art region **collapses and the remaining authored
content takes its space**. It must never render an empty parchment promise. Optional per-type or
per-quest illustrations can be added later through quest data without another layout rewrite.

WO-1201/1202's landed `QuestRewardLine` / `QuestRewardMath` structure is the reward-row authority.
WO-1192 must reuse it and must not author a fixed reward-chip count or a second reward schema.


---

## UI SEAT EYES-ON (2026-08-26) - FRESH CAPTURES OPENED: NOT CLOSED, portrait still broken

Fresh headed captures generated this session on a COMPILE_GATE_OK tree (log
`Builds/wo1192-ui-capture.log`, marker `UI_CAPTURE_OK 89`, PNGs mtime 2026-08-26 ~13:2x):

- **PORTRAIT (`Builds/ui-capture/RumorBoard_1080x2340.png`): FAIL.** The detail pane overlays
  the entire list - cards render UNDER it, the `* All` tab chip floats orphaned over the
  "In Progress" heading, reward chips truncate to `X... / Crys... / St... / Ma...`, left status
  truncates to "The tal...". Matches the run's oracle lines verbatim (BUTTON OVER TEXT:
  `Card_uicap_rumor_active1/2` over `Chip_all` label by 103.9x48 ref px and over `DetailBody`
  by 42x124 ref px, at BOTH 1080x2340 and 1200x2670).
- **LANDSCAPE (`Builds/ui-capture/RumorBoard_2670x1200.png`): close, four residuals.**
  (1) the second In-Progress card sits half-buried under the status line "The talk of
  Elarion..."; (2) objective text ends mid-word ("begun to sin") with no ellipsis or scroll
  affordance; (3) both card titles truncate to the identical string "Standing Watch Over the
  Wester..." - indistinguishable rows; (4) the lower ~third below the status line is dead black.

The `5e990f8d1` responsive slice is NOT sufficient; the ticket stays OPEN. Next: owner rulings
on the parked design questions, then a full both-orientation layout spec + mockup from the UI
seat, then CLI re-implementation.

## OWNER RULINGS (2026-08-26, via the UI seat - three explicit choices)

1. **The board is for ACCEPTING new quests.** Browse-first: the rumor list leads, the detail
   pane sells the quest, Accept is the hero action. Tracking is the HUD tracker's job.
2. **Quest art = per-TYPE plates** (Story / Daily / Gear / Endgame - 4 pieces, not 24). The
   art region COLLAPSES until a plate lands (per the standing rule: never an empty parchment
   promise).
3. **Objective text SCROLLS in the detail pane** - full text always available inside its own
   well; never truncates mid-word; Accept/Track never move.

Redesign mockups from the UI seat follow on these rulings.


---

## ⚠ SUPERSEDES the earlier two-pane direction (2026-08-26, same day)
The two-pane browse layout implied by this morning's rulings section is RETIRED by two further
owner rulings: **the game is LANDSCAPE-ONLY** (portrait work is out of scope entirely - do not
re-litigate portrait), and the two-pane density was rejected: *"that writing would be too small
for a mobile screen. needs to be less detail more simple concept."*

## APPROVED CONCEPT v3 (owner: "i like it" + tweaks, then "go") - THE spec to implement

**Mockup:** `WorkOrders/WORK_ORDER_1192_mockup_v3_2670x1200.png` (the diff target).

**The concept: three self-contained rumor POSTERS. No tabs, no detail pane, no In-Progress.**
- Accept-first taken to its conclusion: this board only OFFERS work. In-Progress/tracking does
  not appear here at all - that is the HUD tracker's job (owner ruling, this morning).
- Each poster: a standout TYPE TAG, a two-line title, a ONE-line hook, reward chips, and its
  OWN Accept button. No selection step anywhere.
- The paragraph lore lives behind a "Read the letter >" tap - a simple full-card overlay with
  the scrolling text and a Back face. The board itself never shows dense copy.
- Paging: a single **Next >** button UP TOP that advances a page of 3 posters and WRAPS
  (owner chose the keep-going form; no bottom arrows, no page dots). Swipe also pages,
  same gesture family as hero-select.
- **Close is a LABELED BUTTON next to Next** - no X glyph (owner ruling).

Anchor rects - fractions of the 2670x1200 screen (x left->right, y BOTTOM->top):

| Element        | xMin  | yMin  | xMax  | yMax  | px @2670x1200 |
|----------------|-------|-------|-------|-------|----------------|
| Title (left)   | 0.056 | 0.860 | 0.600 | 0.935 | baseline y ~108 |
| Next > button  | 0.742 | 0.823 | 0.839 | 0.917 | x 1980-2240, y 100-212 |
| Close button   | 0.858 | 0.823 | 0.955 | 0.917 | x 2290-2550, y 100-212 |
| Poster 1       | 0.056 | 0.083 | 0.318 | 0.767 | x 150-850, y 280-1100 |
| Poster 2       | 0.375 | 0.083 | 0.637 | 0.767 | x 1000-1700 |
| Poster 3       | 0.693 | 0.083 | 0.955 | 0.767 | x 1850-2550 |

Poster internals (px @1200, top-down, per card of width 700):
- TYPE TAG: overhangs the card's TOP-LEFT corner (y 252-330), FILLED gold, ink text - the
  loudest element on the card; distinct by fill+position, not hue (greyscale-safe). Label text
  comes from the quest `type` field via a display map - owner showed MAIN/SIDE/DAILY in the
  mockup; final label wording is a canon-strings row, not hardcoded.
- NEW chip: small outlined chip on the top-RIGHT card edge, only when new.
- Title: up to TWO lines at 46px, centered, FitLine per line.
- Hook: ONE line, 32px. Smallest type anywhere on the board is 30px - that is a floor.
- "Read the letter >": 30px gilt link line -> the lore overlay.
- Reward chips: **ICON + number, never a letter** (WO-1195 law; the icon set including the
  new magic/wisdom picks applies). XP and Relic render as words.
- Accept: the card's own button, 520x140px (>= MinTouchPx with margin), gold-framed.

Acceptance (unchanged in spirit from the WO body, tightened to v3):
1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` fresh; the touch/layout oracle finds
   ZERO overlaps on this panel (the fresh-capture BUTTON OVER TEXT findings above must be gone).
2. A LANDSCAPE 2670x1200 capture, opened and looked at, diffed against the v3 mockup - posters,
   tags, Next/Close, chips. `UI_CAPTURE_OK` alone is not acceptance.
3. Greyscale check: type tag, NEW chip, and Accept all separable without hue.
4. Next wraps across all pages of available rumors; Read overlay opens/closes; Accept on a
   poster accepts THAT quest with no selection step.
5. Owner felt-verifies and CLOSES.
