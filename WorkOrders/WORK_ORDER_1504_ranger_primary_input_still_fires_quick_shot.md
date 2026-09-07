# WO-1504: canon says the phone's one attack button never spends an arrow; the code fires Quick Shot from it

**Status:** SPEC - needs an owner ruling (which of the two is canon)
**Silo:** `Assets/_Modules/Village/Hero/HeroAbilities.cs` + `CLAUDE.md` sec.7.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1504 -> 1505 in the same edit).

## 1. EVIDENCE

CLAUDE.md sec.7 (WO-1105 R5 ruling): the Ranger's BOW is an action-bar ability at slot Q; the PRIMARY attack
is the melee sweep, and *"the phone's one attack button never spends an arrow"*.

The code disagrees:

```
HeroAbilities.cs:499-509   TryGetRangedPrimary returns TRUE for ranger.q
                           range 15 vs melee reach 3.2  =  4.69x, over the 2x threshold
```

So the primary input resolves to a ranged primary and fires Quick Shot. Both the doc and the code came from
WO-1105 (`562f3d3e5`) - the ruling landed in the doc and this predicate survived the deletion of the other
ranged-primary paths. The Knight is unaffected.

## 2. THE RULING NEEDED

**Does the Ranger's primary tap fire the bow, or the melee sweep?**

- **Melee (canon as written):** delete or narrow `TryGetRangedPrimary` so `ranger.q` cannot satisfy it. Q stays
  the locked bar ability. Matches WO-1105 R5 exactly.
- **Bow (code as shipped):** the ruling changes, and CLAUDE.md sec.7's Ranger paragraph is rewritten. Note this
  is the version the owner has actually been playing since WO-1105 shipped.

Recommendation, stated so the ruling is one word: **melee** - the ruling was explicit and reasoned about the
phone's single button, and nothing in this session's evidence says she has since preferred the bow.

## 3. FIX SHAPE (once ruled)

- One of code or doc changes, in a commit that touches both files so they cannot diverge again.
- Regression pinning whichever answer wins, for the Ranger specifically.

## 4. ACCEPTANCE
- [ ] The ruling recorded here and in `DESIGN-DECISIONS.md`.
- [ ] Code and CLAUDE.md sec.7 agree, changed in the same commit.
- [ ] Regression pins the Ranger primary; RED proof stated.
- [ ] `REGRESSION_OK n/n` on a fresh log.
