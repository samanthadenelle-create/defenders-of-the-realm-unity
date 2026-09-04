# WORK ORDER 1306 — The mage needs a cost-1 castable base node (retention)

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T17:23:04, build 2026.09.04.354315). PRIOR STATUS: FIXED — owner ruled 2026-09-02 ("mirror the knight" + "the blm needs to get some healing , like drain to stay balanced (early)"); `mage.t1n3` re-authored into the cost-1 root **Siphon Ward**, granting the drainshot `mage.siphon`, with the return rate remote-tunable as `combat.drainReturnPct` (default 100 = today). See the `.RESULT.md`.
**Silo:** Progression / Hero identity
**Minted:** 2026-09-02 (CLI), from a collision between two of the owner's own rulings.

## Why this exists

The owner's retention lens, verbatim 2026-09-02:

> "we want them to unlocka few items that can go in the quick swap bar fast, why because our
> retention number is very low and people are not returning"

Measured against that, the trees stand at:

| tree | points to first bar-equippable unlock |
|---|---|
| knight | **1** (Thunderbolt) — fixed 2026-09-02, cost- and tier-neutral |
| ranger | **1** (Tumble Step) — already correct |
| shared | **1** (Arcane Bolt / Mend / Dash) — already correct |
| **mage** | **3** — the only outlier |

An attempt to fix the mage by promoting `mage.t2n5` (Blink Mastery, cost 2) onto the base row was
**REVERTED** the same day, because it violated the owner's OTHER ruling.

## The constraint that makes this design work, not a data shuffle

`Assets/Editor/Regression/TalentTreeShapeRegression.cs` rule 2 `[base]`, pinned from the owner ruling
of 2026-08-16, verbatim:

> "common or specialty should still start from a few simple then really refine to the playstyle of
> the user."

Enforced as: the bottom row of every tree and of the shared pool holds **at most three nodes, every
one a root (no prerequisites), and every one priced at the tree's CHEAPEST cost**. The oracle's own
words: *"a base is the simple, entry-level pick - never a mid-tier node that happens to have been
dragged down."*

**The mage is the one class with no cost-1 castable to promote.** Its three cost-1 tier1 nodes are all
`kind: "stat"`:

| id | name | kind | effect |
|---|---|---|---|
| `mage.t1n1` | Arcane Focus | stat | `damageBonus` 0.2 |
| `mage.t1n2` | Mana Flow | stat | `manaRegen` 0.25 |
| `mage.t1n3` | Warded Flesh | stat | `maxHpPct` 0.2 |

So satisfying BOTH rulings requires changing what a base node **does**, not where it sits. That is a
decision about the mage's identity and it belongs to the owner.

Owner ruling 2026-09-02 on the collision: **"Revert the mage, keep the knight."** She explicitly did
NOT take the option to relax the shape law.

## The owner decision this WO is blocked on

Which of the three mage base nodes (if any) becomes a `kind: "skill"` granting a castable, and which
ability it grants — **or** whether the mage is simply allowed to be the slow-start class by design.
Do not pick on her behalf; creative picks are hers.

Useful context for that conversation:
- The mage's existing `unlockAbility` nodes are `mage.t2n2` Manaweave (cost 2), `mage.t3n5` Void Rift
  (cost 3), `mage.t4n1` Cataclysm (cost 5).
- ⚠ `void-rift` and `cataclysm` carry `(NEW ability - stub)` notes and are **dead** — do not build on them.
- Four abilities in the mage pool (`frost-nova`, `heal`, `meteor`, `thunder`) have **no talent node
  granting them at all**, presumably reached via Cathedral of Magic tiers. One of those may be the
  natural cost-1 base grant. Verify before proposing.
- The mage tree is `Aetherweaver`, rows 3/6/6/5, 20 nodes.
- The shared universal strip already gives every class a castable on point one, so this is about the
  CLASS tree's own first impression, not about the player having nothing at all.

## What NOT to touch

- ⛔ Do NOT relax, re-point, or weaken `TalentTreeShapeRegression.cs`. The owner declined that option.
  The law exists because the trees once fanned wide and unshaped, and "fixed" read identically to
  "half fixed" at the gate.
- ⛔ Do NOT re-promote `mage.t2n5`. That is the reverted change; re-doing it re-breaks the law.
- ⛔ Do NOT change any `cost` to dodge the rule. `cost == tierCosts[tier]` holds on all 83 nodes with
  zero exceptions and is a load-bearing invariant.
- ⛔ Do NOT rename any node `id`. Ids are live save keys AND they encode the tier
  (`<hero>.t<tier>n<slot>`).
- ⛔ Do NOT touch the knight, ranger or shared trees. The knight fix is landed and correct.

## Acceptance criteria (once she has ruled)

1. Mage reaches a bar-equippable ability in **1 point**, from a base-row node.
2. `talent-tree-shape` passes: bottom row is at most 3, all roots, all at the tree's minimum cost.
3. All invariants hold — acyclic, fully reachable, y-law, `cost == tierCosts[tier]`, id-encodes-tier,
   17 tier4 capstones at cost 5, row shapes unchanged.
4. Both canonical JSON copies byte-equal (Resources + StreamingAssets), hashes reported.
5. The ability granted actually resolves — no stub-token effect. Check it against
   `TalentEffectLiveness` before authoring.
