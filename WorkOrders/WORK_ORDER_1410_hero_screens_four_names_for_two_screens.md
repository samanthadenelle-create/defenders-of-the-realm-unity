# WO-1410: Hero screens carry four names for two screens; WISDOM is a mystery number; the Loadout empty state is a sentence, not a door

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:03:11, build 2026.09.07.359076). PRIOR STATUS: FIXED - ON THE SEEKER in build 2026.09.06.357453 (chain 00:31-00:38: APK_OK 463MB, R2_PARITY_OK objects=271; installed 00:41, versionCode 357453 read off dumpsys; Firebase App Distribution release 0kka4h6t9u400); owner felt-test closes 2026-09-05 23:11 - Codex lane landed (one canon source for BAG/SKILLS/LOADOUT, Wisdom copy, Loadout owns sockets), two lead-review fixes (popup word, Wisdom plate width) + the OPEN SKILLS door authored at the touch floor; COMPILE_GATE_OK + REGRESSION_OK 388/388 + captures clean, HeroSkillTree/HeroLoadout frames opened (RESULT file); device build after the owner's reboot; felt-test closes. *(was: READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review)*

## Evidence
- AGREED by both reviewers (`REVIEW_MERGED.md` row 9; `REVIEW_A_independent.md` D-1 / D-2 / D-3,
  `REVIEW_B_independent.md` D1 / D2); CLI SEEN `Builds/ui-capture/HeroWorkspace_2670x1200.png` (07:02).
  Frames: `HeroSkillTree_2670x1200.png` (07:02) reads `TALENT TREE`, `WISDOM 0`, quick-swap `1 EMPTY / 2 EMPTY /
  3 EMPTY`; `HeroLoadout_2670x1200.png` (00:29) reads `Hot-Swap Skills` and `No unlocked skills yet. Unlock SKILL
  nodes in the tree.`; `Bag_2670x1200.png` (09-01, stale) titles `INVENTORY` under a deck card `BAG`.
- CODE: `Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs:22` crest title `TALENT TREE`, `:558` / `:2047`
  `WISDOM  0`; `HeroLoadoutVM.cs:100` `Title => "Hot-Swap Skills"`, `HeroLoadoutPanelMvvm.cs:325`;
  `Hero/EquipmentPanel.cs:962` cross-button `TALENTS`, `:799` heading `INVENTORY`; `Hero/InventoryUIBuilder.cs:123`
  `INVENTORY`; `Hero/HeroEquipHud.cs:256` `BAG`. Every one is a literal.

## What the player experiences
The deck says SKILLS, the screen says TALENT TREE, a button says TALENTS, the loadout says Hot-Swap Skills and
"SKILL nodes". BAG opens INVENTORY. `WISDOM 0` has no source and no next point. The empty Loadout tells the
player to go to the tree in a sentence with no button, and the tree also owns the same three sockets.

## Fix shape (one mechanism)
The WO-1398 one-source pattern: each Hero screen has ONE noun in `canon-strings.json` (`heroBag`, `heroSkills`,
`heroLoadout`), read through `HudStrings.Get`; the deck card, the chrome title, every cross-button and every
sentence render that key. Source scan forbids the literals. `WISDOM 0` -> `WISDOM 0 - next point at Level 2`
from the progression rule that grants points (one clause; the VM computes it). Loadout empty state gains an
`OPEN SKILLS` button routing to the skill tree. Socket assignment has ONE owner - the Loadout; the tree's
quick-swap rail becomes read-only display or is removed (ruling #11).

```
Hero deck   [ BAG ] [ SKILLS ] [ LOADOUT ] ...      (labels = canon keys)
SKILLS      WISDOM 0 - next point at Level 2
LOADOUT     No skills unlocked yet.   [ OPEN SKILLS ]
```
Trace: `FlowTrace.Step("Hero", "face label='<key value>' source=canon-strings site=<deck|chrome|button>")`.

## Acceptance
- [ ] RED first: `HeroNameSingleSourceRegression` - source scan: `"TALENT TREE"`, `"Hot-Swap Skills"`, `"INVENTORY"`,
      `"TALENTS"` occur in NO `.cs` under `Assets/_Modules` (comments excepted); the Loadout empty state exposes a
      button whose label equals the `heroSkills` key value; the Wisdom chip text contains `next point`. Fails on the
      current tree (files above).
- [ ] Headless: `HeroWorkspace`, `HeroSkillTree`, `HeroLoadout`, `Bag` `_2670x1200.png` regenerated (`UI_CAPTURE_OK`),
      opened: one noun per screen, chip clause, door button.
- [ ] Device: HERO > each card; titles match the deck; screencaps read.

## Not in scope
Skill content or Wisdom economy; the quick-swap rail geometry (WO-1401 fixed); back-to-deck (WO-1400); the Bag
portrait `GROM` (queued CLI check).

## Owner ruling
- Section 2 #10 Names? - written to the default SKILLS (`BAG / SKILLS / LOADOUT`, matching the deck).
- Section 2 #11 Sockets-owner? - written to the default LOADOUT.
- Section 2 #12 Wisdom-source? - no default; the clause is written as `next point at Level N` from the grant
  rule the code already has, pending the owner's sentence.
