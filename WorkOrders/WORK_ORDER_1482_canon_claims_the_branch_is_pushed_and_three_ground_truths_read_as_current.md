# WO-1482: canon says the branch is pushed - 103 commits are not - and three CANON_GROUND_TRUTH files read as current

**Status:** READY TO IMPLEMENT
**Silo:** docs/canon. Pairs with WO-1481 (same class, different section).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1482 -> 1483 in the same edit).

## 1. EVIDENCE

```
git rev-list --count origin/feat/synty-art-retheme..HEAD   = 101   (103 by git status -sb)
```

while FIVE load-bearing docs say the branch is pushed:

```
CANON_GROUND_TRUTH_2026-09-03.md:21 | docs/HANDOVER.md:10 | PROJECT_INDEX.md:5
SESSION_CANON_LOADER.md:8 | KEY_FACTS.md:61
```

Anchors and pointers that contradict the tree:

```
SESSION_CANON_LOADER.md:638,682   anchor to 09-02, while :1 names 09-03; ~12 stacked "live anchor is X" headers
CANON_GROUND_TRUTH_2026-08-23.md, _2026-09-02.md   no SUPERSEDED banner - both read as current
docs/MASTER_CATALOG.md:189        names wip/village2-and-f8-tickets
docs/ARCHITECTURE.md:108-118      names MainCastle_Hall as the hub and OuterWorld as streaming additively
                                  (hub is Main_Castle_Overworld, SceneRouter.cs:146,168;
                                   OuterWorld.unity has ZERO hits in git ls-files)
docs/MASTER_CATALOG/devtools-settings-onboarding.md:242,245   describes TutorialDirector.cs as live
                                  (removed by 17cf8736b, WO-971)
docs/MASTER_CATALOG/hud.md:51     HudKitController "1,836 lines"   (actual: 5,298)
PROJECT_INDEX.md:110              cites HUDManager.cs + VirtualDPadLean.cs - NEITHER EXISTS
```

CLAUDE.md sec.15 says exactly ONE ground truth may be current. Three are.

## 2. FIX SHAPE

- Banner the two stale `CANON_GROUND_TRUTH_*.md` as SUPERSEDED (do not rewrite their bodies - frozen ledgers).
  Archive the 18 stale root `CANON_GROUND_TRUTH_*.md` under `docs/_archive/`, leaving 09-03 and the 07-22
  module anchor at root.
- Replace the five "pushed" assertions with the git command, per WO-1481's pointer-table rule.
- Fix the seven pointer lines above to name what exists.
- REWRITE `SESSION_CANON_LOADER.md` (700 lines, ~12 stacked anchors) rather than patching it; a loader whose
  own anchor contradicts its title cannot be repaired by another header.
- Add a `board_build`-style check that FAILS when more than one root `CANON_GROUND_TRUTH_*.md` lacks a banner.

## 3. WHAT NOT TO DO
- Do not push the 103 commits to make the docs true. Whether to push is the owner's call.

## 4. ACCEPTANCE
- [ ] Exactly one unbannered ground truth at root; the check exists and goes red on a second.
- [ ] Zero "pushed" assertions; each replaced by the git command.
- [ ] The seven pointer lines corrected; grep for `MainCastle_Hall`, `HUDManager.cs`, `TutorialDirector`
      pasted in the RESULT.
