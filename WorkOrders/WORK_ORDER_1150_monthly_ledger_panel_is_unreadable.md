**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.

# WORK ORDER 1150 — The Monthly Ledger panel is unreadable, unframed, and colour-only

**Minted:** 2026-08-22 (CLI, banner bumped 1150 -> 1151 in the SAME edit)
**Lane:** HUD / UI. **Class:** SHIPPED SCREEN, VISIBLY BROKEN.
**Evidence:** `docs/ui-evidence/wo1150/01_seeker_monthly_ledger.png` — captured off the owner's
Seeker at 2670x1200, 2026-08-22 23:20. Owner: *"i also saw a really bad season pass UI"*.

## ⛔ WRITTEN FROM THE FRAME, NOT FROM A DESCRIPTION

Every defect below is visible in that one capture. This is deliberate: WO-1058 shipped with
citations to a function that had nothing to do with its defect because it was written from a
description, and a whole evening went into the jeweler because reasoning outran looking. Open the
PNG before you touch code, and re-shoot it before you claim done.

**Panel:** `Assets/_Modules/Wallet/UI/SeasonTrackPanel.cs` (592 lines, live; touched 2026-08-21).

## THE DEFECTS

### A. Containment — things are outside the panel
1. **`Close` is clipped by the top of the screen.** It sits half off the display, above the panel
   frame, not inside it. It is the primary exit control.
2. **The `Echoes 1/6` HUD chip renders THROUGH the modal**, on top of the right-hand column. A
   world HUD element is drawing over a modal surface.
3. **"Ledgers are not on sale in this build..." is BOTH truncated AND overhanging** the panel's
   bottom-right corner.

### B. The panel has no frame
It is a flat mustard/olive slab. Every other panel in the game wears the obsidian frame. It reads
as an unstyled placeholder next to Manage, Bag or the Night Market.

### C. Information design — thirty identical cards
All thirty days read `250 Wood  120 Iron  90 Food  30 Coins` / `UPCOMING`, except three that add
`60 Crystals`. There is no progression and no reason to anticipate day 30 over day 2. The reward
text is also too small to read comfortably at arm's length, while the right column carries large
empty vertical bands — the space exists, it is just not spent where the reading happens.

## ⭐ OWNER RULING 2026-08-22 — WEEK TABS, NOT A 30-CARD WALL

Owner, verbatim: *"i think we need to show a week by week on tabs or something otherwise its too
tiny too busy"*.

This is the fix for defect C and it is the right shape: **five tabs of seven days** (the last
short) instead of thirty cards at once. Seven cards across the same body gives each card roughly
FOUR TIMES the width, so the reward line becomes legible **by spending the plentiful axis rather
than shrinking text** — which is the only move landscape allows.

⚠ Two constraints the tabs themselves must satisfy, or this trades one defect for another:
- Each tab is an interactive control, so each is subject to `MinTouchPx = 112`. Five tabs across
  a ~2120-unit body is comfortable; do not let them shrink to fit a sixth.
- ⛔ **The SELECTED tab may not be indicated by colour alone.** The Manage screen already solves
  this the right way — a gold UNDERLINE plus the label — so copy that pattern rather than
  inventing a tint. Same for which week is claimable.

The milestone days (7 / 14 / 21 / 30) land one-per-tab under this split, which is a natural place
to give each week a worded header rather than a coloured bar.

### D. ⛔ COLOURBLIND VIOLATIONS — the owner is RED/GREEN COLOURBLIND
Meaning is carried by hue alone in at least four places:
- the gold accent bars on days 7 / 14 / 21 / 30 (apparently the crystal days) — the ONLY marker
- `0 claims left` in gold
- the reward line in green
- "Nothing here expires, so nothing here counts down." in green

Every one of these needs a word or a glyph. A greyscale pass is the acceptance test, not a
preference.

## SCOPE
1. Bring `Close` inside the frame, and give the panel the obsidian frame the rest of the game uses.
2. Stop the HUD chip drawing over the modal (sorting/canvas order, or hide the rail while a modal
   is up — whichever matches the house pattern; do not invent a third).
3. Re-lay the day grid so a day is READABLE and the milestone days are distinguishable **without
   colour**. Spend WIDTH — this is landscape and vertical is the scarce axis.
4. Put the truncated build notice inside the frame and let it fit.
5. Replace every colour-only signal with a word or glyph.

## ⛔ CONSTRAINTS
- `MinTouchPx = 112`. Do NOT shrink a control to make text fit, and do NOT touch `CanonCtaWidth`
  (360) / `CanonCtaHeight` (132) — ~25 files derive from them.
- Code-built uGUI only; **UXML does not work in player builds**.
- TMP is **ASCII-only** — no emoji, no smart quotes.
- Player-facing strings belong in `canon-strings.json`, BOTH canonical copies, byte-identical.
- ⚠ **Do not touch the reward/claim logic.** This is a presentation ticket. `BattlePassService`
  drives this panel and is live.

## ⚠ ONE OPEN OWNER DECISION TOUCHES THIS SCREEN
`BattlePassService.cs:51-84` carries a self-declared conflict block: `BattlePassManager` is
dormant while `BattlePassService` shipped and runs, and it says outright that this needs an owner
decision and **must not sit**. The owner ruled KEEP on the manager when nothing used a battle pass;
a second, data-driven one has since shipped. Settle that BEFORE redesigning, or the redesign may
land on the wrong one.

Related: `WO-1122 season pass and revenue KPI` is a SPEC gated behind R6 (WO-1117) — a different
ticket. This one is the visible defect on the shipped panel.

## ACCEPTANCE
- [ ] A fresh Seeker capture at 2670x1200 shows `Close` inside the frame, nothing overhanging, and
      no HUD chip over the modal
- [ ] The same frame in GREYSCALE still distinguishes milestone days, claim state and expiry
- [ ] Reward text is legible at arm's length; no truncated strings
- [ ] Verified by DEVICE SCREENSHOT, not by reading layout code
