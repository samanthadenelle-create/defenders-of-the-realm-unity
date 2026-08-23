# WORK ORDER 1043 — PENDING BAKE: the dungeon exit trim is committed but not yet on screen

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Minted:** 2026-08-16 (CLI seat, Sunday sweep) — banner bumped in the same edit
**Lane:** Dungeon scenes. ⚠ Bake-only; the code and data work is DONE and committed (`dd17a793`).

---

## 1. What is already done

`dd17a793` trimmed dungeon egress from **6 ways out to 2** (front door + one back exit past the
treasure room), in BOTH the layout JSONs and the upstream **graph** JSONs — the latter matters
because `GraphDungeonComposer:512` copies `graph.extracts` verbatim into the emitted layout, so
trimming only the layouts would have been silently undone by the next Compose.

`[dungeon-egress]` (`DUNGEON_EGRESS_OK`) passes and pins the new shape.

## 2. Why it is not visible yet

**The exit pads are BAKED GameObjects.** Editing JSON changes nothing on screen until the scenes
are re-baked. Until then a player still sees six exits and five X-ray "leave" signs.

## 3. ⚠ Why I did NOT bake it tonight — the decision, with the evidence

Project memory (`dungeon-scene-shared-tree-corruption`) forbids baking these scenes in the shared
tree: `DungeonCompose/*.unity` are binary-serialized and have a history of NUL corruption when
baked in place. The sanctioned route is an isolated worktree.

**I created that worktree and then abandoned the bake, because the worktree cannot produce a good
bake.** Measured, not assumed:

| needed | present in a fresh worktree |
|---|---|
| `Assets/polyperfect` | **NO** (gitignored) |
| `Assets/Models/KayKit` | **NO** (gitignored) |
| `Assets/Blink` | **NO** (gitignored) |
| `Library/` (import cache) | **NO** — a full cold import, 30-60+ min |

A bake there would run **without the art the dungeon rooms reference**, so it would very likely
write scenes with missing references — **a worse artifact than the un-baked scenes we have now**,
produced unattended, after an hour of import. Shipping that silently would be the worst outcome
of the three.

## 4. The two honest routes — OWNER PICKS

**A — copy the packs into the worktree, then bake there.** Safe per the corruption memory, but
requires copying ~2 GB of gitignored art into a temp worktree. Slow, disk-hungry, and the copy
step is itself unverified.

**B — bake in the shared tree with the editor closed, then VERIFY the scenes.** Faster and uses
the real project (art present, Library warm). The corruption memory is the risk — but it is a
*history*, not a proven mechanism, and the outcome is checkable: a NUL scan plus a git diff of
the three scenes immediately after, reverting if either looks wrong.

**Recommendation: B, attended, with the NUL scan as the gate.** The compile gate already scans
`.cs` for NULs (WO-434); the same check applied to the three baked scenes turns the historical
risk into a verified pass/fail rather than a superstition. Do NOT run it unattended.

## 5. The command

```
Unity -batchmode -quit -projectPath <repoRoot> -buildTarget Win64 \
  -executeMethod DeNelle.Editor.RoomForge.DungeonBaker.BakeLayoutBatch -dungeon <id>
```
Once per dungeon (a bake replaces the open scene): **`dg_ember_deep`, `dg_bonecrypt`,
`dg_sunken_vault`**.
Scenes rewritten: `Assets/Scenes/DungeonCompose/{dg_ember_deep,dg_bonecrypt,dg_sunken_vault}.unity`.

⚠ `BakeLayoutBatch` exits 2/3 on a missing/unknown `-dungeon` and never silently falls back to
the default batch — so a typo fails loudly instead of baking the wrong scene.
⚠ The baker reads **StreamingAssets**; the runtime reads **Resources**. Both copies are
hash-identical and cases 5/6 of the egress oracle pin that.

## 6. Acceptance

- The three scenes re-baked; a NUL scan over each returns clean; `git diff --stat` on them is
  plausible (pad objects removed, not a whole-file rewrite).
- In play: **two** ways out of a dungeon, one at the entry and one past the treasure room.
- Dungeon world-space labels drop 13 -> 3 (⚠ NOT to zero — see §7).
- `[dungeon-egress]` still green.

## 7. ⚠ Still open after the bake, deliberately

- **One "Leave" label per dungeon still renders through walls.** The built-in `LegacyRuntime`
  font material uses `GUI/Text Shader` = `ZTest Always`. The fix was drafted and BACKED OUT: font
  atlases keep the glyph in the ALPHA channel, so naive re-hosting can render black or blank, and
  that lane had no screenshot capability. Three town-side surfaces share the defect
  (`BuildingSign:155`, `StructureAttackAlert:184`, `StructureDamageVisuals:903`). **Wants one lane
  that fixes all four WITH a capture** — an unverifiable visual fix is exactly how the bow got
  "fixed" and reverted.
- **The back exit is a quiet pad (`trueExit:false`).** Promoting it to arch+beacon would read
  better as a door onward AND remove the last label, but `DungeonRoomOwnershipRegression` hard-
  asserts `beacons == 1`. That needs an owner ruling, not a weakened assertion.
- **The zone seam is a documented, unbuilt hook** at the back exit (a non-null `onLeave`, or
  `DungeonPortLink` authored at runtime as `DressTraversalLinks` already does). Owner intent: the
  back door is the seam to a new zone. Re-point this exit — never add a third.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `dg_bonecrypt.unity; 520efe031,341599672` — scenes re-baked. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
