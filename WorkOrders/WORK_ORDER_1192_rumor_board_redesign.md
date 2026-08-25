# WORK ORDER 1192 - Brom's Rumor Board: full layout redesign

**Status:** READY TO IMPLEMENT (design pass first)
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
