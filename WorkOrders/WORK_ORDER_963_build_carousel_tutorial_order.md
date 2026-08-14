# WO-963 — The build carousel follows the tutorial's teaching order

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at BuildPaletteVM.cs:278-281 + BuildCarouselTutorialOrderRegression.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Date:** 2026-08-10 · **Priority:** Medium (first-session legibility)
**Block:** main line (CLI) · **Lane:** Build mode / catalog data
**Owner ask 2026-08-10:** F8 seq 2302, verbatim — *"Can we order the carousel in order of how the
tutorial presents them?"*

## §1 RCA — there is no order today

`BuildPaletteVM.Rebuild()` (`:277-307`) does a plain `foreach (var e in entries)` over the registry
query for the active verb/group. **There is no sort anywhere in the palette** — not in the VM, not in
`BuildPaletteUI`, not in the group composition (`ConfigureGroup`, which splits by TYPE only).

So the carousel order IS the row order of `entries[]` in `structures-catalog.json`. It looks arbitrary
because it is arbitrary: it is authoring order, never a decision.

## §2 The teaching order (read off `tutorial-steps.json`, not assumed)

| Step | order | Teaches placing |
|---|---|---|
| `founding_stores` | 20 | **`collector_lumbermill`** ("Place your Lumbermill - it harvests timber for you") |
| `founding_defense` | 30 | **a Tower** ("Raise one tower to cover the gate") |
| post-FTUE nudge | 1050 | **`workshop`** ("When you wish: raise a roof for proper weapons") |
| post-FTUE nudge | 1060 | **Armorer** ("Next along the road: an Armorer") |

## §3 The fix — data, not code

Add an owner-tunable **display order** to `structures-catalog.json` (both dual copies, byte-identical,
version bumped), and have the palette sort by it with the CURRENT catalog order as a stable tiebreak so
unlisted rows never jump around.

Seed it to the teaching order above: **Lumbermill → Tower → Workshop → Armorer**, then everything else
in today's order.

> ⛔ **The palette must NOT read `tutorial-steps.json` at runtime.** Presentation never depends on a
> teaching script (ARCHITECTURE_PRINCIPLES §1/§2): the tutorial would become an input to the shop, and
> a step rename would silently reshuffle the shelf. The catalog carries the order; the tutorial and the
> catalog simply agree, and a regression is what keeps them agreeing.

**One order, always** — not "tutorial order during the FTUE, something else after". A shelf that
re-sorts itself under the player is worse than an arbitrary one.

## §4 Acceptance criteria

1. Opening Build shows Lumbermill first, then the Tower (in its group), with Workshop and Armorer next
   in their group.
2. The order is identical during and after the FTUE, and across a reload.
3. A row with no authored order keeps its current relative position (stable sort — prove it with a row
   deliberately left unauthored).
4. Both catalog copies stay byte-identical and the version is bumped.
5. A regression asserting the catalog's authored order MATCHES the ids the tutorial steps name — so a
   future tutorial re-author that changes what is taught first fails the gate instead of quietly
   disagreeing with the shelf.

## §5 What NOT to touch

The group split (Town / Defense / Castle Structures), the unlock/locked-id filtering (it is a UNION and
must never be loosened by a re-sort), WO-1013's visible-locked rows, and the WO-1010 rail geometry.
