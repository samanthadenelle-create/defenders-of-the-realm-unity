# WO-1565: every tower shares one description, the Catapult is called a tower, and an unauthored description paints prose instead of failing

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 3 (BUILDING DETAIL) not yet passed (2026-09-07); code landed uncommitted in the working tree. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate)*
**Priority:** P2 — small code change, real content task. **Read §4 before scheduling: part of this is copy,
and copy has a voice.**
**Silo:** `Assets/_Modules/Village/.../StructureCardVM.cs` (the fallback + the gate) and
`Assets/Resources/Data/Canonical/structures-catalog.json` **plus its `Assets/StreamingAssets/` twin**.
**Parent:** WO-1534 §B5. **Source:** read-only review 2026-09-06 (CLI seat), re-read at source.
**Minted** from the banner (`CLI_LANES_WO_NUMBERS.md`, renumbered to the banner's hundred-and-second-pass reconciliation, 2026-09-06 22:12).

---

## 1. EVIDENCE

`Builds/ui-capture/ManageFlow_BUILD_max_2670x1200.png` — the **Catapult**'s detail card reads:

> *"A defensive tower … auto-fires on enemies in range."*

Source: `StructureCardVM.DescriptionFor` (`:459-469`) is a **type-level FALLBACK** that fires only when
`CatalogEntry.description` is unauthored, and logs `desc-unauthored-<id>` when it does.

So **every `CatalogType.Tower` gets the same sentence**: Archer Tower, Ballista, **Sky Ballista
(anti-air)** and Catapult all read identically — and the Catapult, a siege engine, **is described as a
tower**. The one line on the card that answers *"what does this thing do?"* answers it wrong for at least
two of the four, and identically for all of them.

**Not covered by WO-2014**, which is about *removing* copy and reducing density. Nothing tickets
**authoring** `CatalogEntry.description`. **WO-1491 saw this exact string** and diagnosed it as a
*"triple space"* — that is an em-dash the font does not carry, a different fix on the same line. Neither
ticket noticed the sentence is wrong.

## 2. THE STRUCTURAL HALF — do this regardless

An unauthored description currently **paints plausible prose**, which is why this survived: nothing looks
broken. `desc-unauthored-<id>` is logged, and nobody reads it.

**Make the unauthored case FAIL the catalog gate** rather than render. A row that ships without a
description is a data defect, and this repo's standing rule is that a silent fallback is worse than a loud
failure (CLAUDE.md §12: *"No silent failures"*).

⚠ **Sequence matters:** turning the gate red before the descriptions exist will red the build. **Author
the copy first, then tighten the gate in the same commit** (CLAUDE.md §15 — the change and its canon move
together).

## 3. THE DATA HALF — ⛔ CANONICAL JSON IS A BYTE-MODE EDIT

Memory `canonical-json-edits-binary-only-verify-newlines`, and it has bitten this repo before:

- ⛔ **TWO copies must stay identical** — `Assets/Resources/Data/Canonical/` and
  `Assets/StreamingAssets/Data/Canonical/`. **A parity oracle reads both.**
- ⛔ **Edit in BYTE mode and PROVE the LF count.** `Set-Content` / text-mode rewrites once flattened
  **12 files to zero newlines** while still parsing as valid JSON — the oracles grep per line, so they
  went blind. Patch from HEAD bytes and verify the newline count before and after.

## 4. ⛔ THE COPY IS THE OWNER'S VOICE — ASK BEFORE WRITING TWENTY-FOUR DESCRIPTIONS

This is the part to raise rather than assume. There are ~24 build rows, and a description is
player-facing prose that sets tone. **Options, for the owner to pick:**

- **(a)** the CLI drafts all of them against the existing voice and she reviews the list in one pass;
- **(b)** she writes the handful that carry the most character (Cathedral, Echo Hollow, the Heart) and the
  CLI drafts the plain functional ones;
- **(c)** ship the structural half only (§2 gate + the four tower rows that are actively WRONG), and let
  the rest keep the fallback until there is time.

**(c) is the smallest honest fix** and unblocks the gate work immediately. **Recommend (c) then (a).**

⚠ **Whatever is chosen, the four tower rows are not optional** — "a defensive tower" on a **Catapult** and
on a **Sky Ballista (anti-air)** is actively misleading about what the player is buying.

## 5. ACCEPTANCE

1. Archer Tower, Ballista, Sky Ballista and Catapult each carry an authored description that says what
   **that** structure does. The anti-air one says anti-air; the Catapult is not called a tower.
2. An unauthored `CatalogEntry.description` **fails the catalog gate** instead of painting the type-level
   fallback.
3. Both canonical JSON copies are byte-identical and the parity oracle is green. **LF counts proven and
   recorded in the RESULT** — before and after.
4. Whichever option in §4 the owner picks is recorded in the RESULT, with the rows still on the fallback
   listed by id so the remainder is a known set, not a surprise.
5. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` + the catalog gate marker, all on **fresh** logs and judged
   by the marker, never the exit code.

## 6. WHAT NOT TO TOUCH

- Costs, tiers, baskets, `manageFilters`, `manageArtKey`, `singleton` flags, `heightMul` — this ticket
  touches **`description` and nothing else** in those rows.
- The em-dash / spacing artifact on the same line — **WO-1491**.
- Copy density and removal elsewhere in Manage — **WO-2014**.
- The Cathedral's cost basket — **owner rulings 22 and 24**, live and unsettled.
