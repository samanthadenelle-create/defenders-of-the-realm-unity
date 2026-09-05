# WO-1390: Manage - Research shows nothing when no perk is researchable; show the locked tier with its prerequisite and a door to upgrade it

**Status:** FIXED - in 65d5a7eae, on the Seeker in build 2026.09.05.355952 (locked tiers listed with the CanResearch reason verbatim and an UPGRADE <BUILDING> door to the upgrade page; [research-locked-visible] green). Awaiting owner felt-test: Manage - Research shows the locked rows and the door opens the Cathedral page.

## Owner, verbatim (2026-09-04 23:33-23:35, Seeker, build 355905)
> "under manange research it shows nothing, should it show Tier one and show locked with a link to upgrade
> the prerequsite" -> "upgrade lumbermill to research those skills"

## Evidence (device, adb logcat, same minute)
`[Flow:Manage] research browse (this town): 15 placed type(s), 6 with a tier ladder -> 0 perk row(s).`
Six owned buildings carry a tier ladder; the tab rendered zero rows.

## Root cause (read at source)
`ManageScreenVM.BuildResearchBrowse` (`ManageScreenVM.cs`, the per-perk loop): after
`bool can = BuildingPerkService.CanResearch(bId, pId, out _);` it does `if (!can) continue;` under the comment
"Progressive disclosure: prerequisites teach themselves when satisfied; a locked perk is not a manageable
structure action yet." - so every locked perk is DROPPED, and the `out reason` (discarded as `_`) is the exact
sentence the owner asked for: `BuildingPerkService.CanResearch` (`BuildingPerkService.cs:172-187`) returns
`"Upgrade the building to Tier N first."` or `"Locked - needs Village Tier N."`. The disclosure comment
contradicts the standing Manage rule that the Troops tab already follows ("Build a Barracks to unlock" +
`BuildLockBadge`, `ManageScreenPanel.cs:632,684,748`).

## The ruling, made precise
- The Research tab lists, per OWNED building with a ladder, its NEXT tier's perks: researchable ones as today;
  locked ones as a LOCKED row (dim, `BuildLockBadge`, greyscale-safe) whose state sentence is the `CanResearch`
  reason verbatim, and whose primary face is the DOOR: `UPGRADE LUMBERMILL` -> the existing
  `PlacedStructureUpgradeService` / Buildings-tab upgrade page for that building (the ONE upgrade start path,
  ARCHITECTURE s6). Never a dead "Locked" button.
- Village-Tier-locked perks (`"Locked - needs Village Tier N."`): the door is the Heart of Elarion tier upgrade
  (the WO-432 tech gate) - same shape, different destination.
- Owned/researched perks are not listed (as today). Order: researchable first, then locked by tier ascending.
- Colourblind law: state by words + badge; ASCII only; touch >= MinTouchPx.

## Acceptance
- [ ] On the Seeker with the Lumbermill at tier 1: Research shows its Tier 2 perks locked with
      "Upgrade the building to Tier 2 first." and an UPGRADE LUMBERMILL door that opens the upgrade page.
- [ ] `[Flow:Manage] research browse` logs `N perk row(s) (M locked)`, never `0 perk row(s)` while a ladder exists.
- [ ] `ManageProgressiveDisclosureRegression` + `ManageApprovedLauncherRegression` green; new pin
      `[research-locked-visible]`: a fixture town with one tier-1 laddered building yields >= 1 locked row whose
      StateText is the CanResearch reason; proven RED by restoring `if (!can) continue;`.
- [ ] Owner felt-test.
