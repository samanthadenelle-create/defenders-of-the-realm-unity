# WO-964 — Unearned structures are HIDDEN, not shown-locked: the reveal is the reward

**Status:** DONE (implemented + gated 2026-08-10; the hidden filter is now unlock-aware, so hidden means UNTIL EARNED, never forever)
**Date:** 2026-08-10 · **Priority:** Medium-High (it changes what a first-session player sees)
**Block:** main line (CLI) · **Lane:** Build mode / progression data
**Owner ruling 2026-08-10:** F8 seq 2303, verbatim — *"dont show the spire, leave as blank till earned,
allows us to unlock new items and not reveal what they are"*

## §1 ⚠ This REVERSES a decision that shipped the same day

`WORK_ORDER_1013_*.md` shipped this morning (commit `bd9d54d9`) with a **visible-locked Spire card** —
the Arcane Spire rendered in the carousel, greyed, with its normal cost, refusing to arm on tap. Both
rulings are recorded here so this cannot be silently flipped back a third time:

| | Ruling |
|---|---|
| **2026-08-10 AM (WO-1013)** | show the Spire locked, so the player can see what is coming and save for it |
| **2026-08-10 PM (this WO, supersedes)** | **do not show it at all until earned** — the reveal is the reward, and it lets us add new structures without spoiling them |

## §2 The good news: the machinery already exists — this is a DATA move

`build-categories` already carries two distinct buckets, and `BuildPaletteVM` honours both:

- **`lockedIds`** → the row is **filtered OUT** of the palette entirely (`BuildPaletteVM.cs:210-213`,
  the filter is a UNION across verbs — a row gated under ANY verb stays out of every group).
- **`visibleLockedIds`** → the row RENDERS, greyed, with a lock reason, and never arms
  (`BuildPaletteUI.cs:791`, `:864`, `:1042`; `BuildPaletteVM.cs:76-80`, `:184`, `:224`).

The Spire currently sits in `visibleLockedIds`. This ruling moves it to the hidden bucket **until the
persisted unlock flips** — the gate `ProgressionUnlocks.IsUnlocked` (`BuildPaletteVM.cs:97`) is already
the authority for "earned", so the reveal seam is built.

## §3 Scope

1. **Move the Arcane Spire** out of the shown-locked bucket into the hidden one, in the
   `build-categories` data — **both dual copies, byte-identical, version bumped.** Do not hard-code the
   id in C#.
2. **The unlock must REVEAL, not un-grey:** when `ProgressionUnlocks` records the Spire as earned, the
   row must leave the hidden filter and appear as a normal, buildable card. Verify the hidden filter and
   the unlock gate are the SAME authority — if the filter is static data and the unlock is runtime, they
   must meet at one seam, not two.
3. **Make it the POLICY, not a one-off.** The ruling's reason is general ("allows us to unlock new items
   and not reveal what they are"), so the mechanism must be reusable: any structure authored as
   unlock-gated is hidden until earned, with no code change per structure.
4. **Keep WO-1013's other halves.** The wave-2 plans drop and the catalog-priced funding are untouched —
   only the card's pre-unlock VISIBILITY changes.
5. **The unlock moment needs to land.** With the card hidden, the player has no standing hint that the
   Spire exists, so the earn beat must announce it (the WO-1013 plans drop already does this — verify it
   still reads as an arrival now that nothing preceded it).

## §4 ⚠ One question for the owner, not for the implementer

**This is the opposite policy to WO-960**, which shipped today making the ARMOR STORE show a greyed
ladder of items you cannot buy yet ("unlocks at Lv N"), deliberately, as aspiration.

Both can be right — a shop ladder within a known category is aspiration; a brand-new *structure* is a
surprise — but they are opposite answers to "do we show what you haven't earned", and only one of them
can be the house rule. **Recommendation: keep both as they are** (shop = ladder, build carousel =
hidden), and record that split as the rule. Owner's word closes it.

## §5 Open reading of "blank"

Her word is *"leave as blank till earned"*. Two readings:

- **(a) No card at all** — the carousel simply has fewer cards. Cleanest; leaks nothing, not even how
  many things exist. **Recommended default.**
- **(b) A blank/"?" placeholder card** — teases that something exists without revealing what.

Implement **(a)**; (b) is a one-line switch on the same data if she wants the tease.

## §6 Acceptance criteria

1. On a fresh save the Arcane Spire is **absent** from the build carousel — not greyed, not present.
2. After the unlock is earned, it appears as a normal buildable card, at its authored order (WO-963).
3. Both `build-categories` copies stay byte-identical, version bumped.
4. A regression asserting: hidden-before-unlock, present-after-unlock, and that the hidden bucket is
   driven by DATA (adding a second unlock-gated id needs no code edit).
5. `[castle-plans]` stays green — the WO-1013 drop and funding are unaffected.

## §7 What NOT to touch

WO-960's armor-store ladder (opposite surface, deliberate — see §4), the `lockedIds` UNION semantics
(never loosen a gate), WO-963's ordering work, and the WO-1010 rail geometry.
