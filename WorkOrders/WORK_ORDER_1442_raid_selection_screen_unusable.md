# WO-1442: the RAIDS selection screen is hard to use - a stray bar covers card one, card three is clipped, world text bleeds through

**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-07T00:50:58, build 2026.09.07.358574). PRIOR STATUS: FIXED - ON THE SEEKER 2026.09.07.358574 - landed in `32659c0f6` (see RESULT); post-fix PNG still owed
**Silo:** `RaidSelectionScreen` + its card rows. Disjoint from the raid lifecycle (WO-1437, landed), the
HUD posture work (WO-1436, landed) and the wallet lanes.
**Source:** owner felt-test 2026-09-06 on build **2026.09.06.358245** (the raid-fix build), verbatim:
> *"won and back to camp could attack but the screen and UI was very hard to use"*

She confirmed the complaint covers **BOTH** this selection screen **and** the in-fight HUD. The in-fight
half is a separate ticket once a mid-raid capture exists - **do not absorb it here.**

**Evidence: `adb screencap` pulled from her device this session** (scratchpad `raid-ui.png`). Every defect
below is visible in that one frame; none is inferred.

---

## 1. THE GOOD NEWS FIRST, SO IT IS NOT LOST IN A REWRITE

**The lock copy is correct and should survive any redesign:**
> *"The Heart cannot reach this far yet - win 1 more raid to press on."*

That names the gate AND the exact distance to clearing it, in player words, with no jargon. It is the
shape WO-1427 asked for everywhere. **Do not replace it with a padlock icon or a bare "Locked".**

## 2. THE THREE DEFECTS

**D1 - a stray bar is painted ACROSS the first card.** A gold-framed horizontal element sits over
**The Forsaken Camp**, swallowing its `Clock: 3:00` line, mangling `- x1 Loot` and cutting through
`Spoils: ~1800 wood, ~1100 iron, ~2200 gold`. It is the wrong size and in the wrong place. Establish
what it is - a selection highlight, a focus ring, a progress/loot pill - **before moving it**; a
mis-sized highlight and a mis-anchored pill need different fixes.

**D2 - the list is CLIPPED and content is unreachable.** **The Veiled Enclave** is chopped at the
bottom: no spoils line, no flavour line. A FOURTH camp (`iron_bastion`, present in the same session's
logs) is not reachable on screen at all. Either the viewport does not scroll, or it scrolls and gives no
affordance saying so. Determine which - they are different defects with the same symptom.

**D3 - world text bleeds THROUGH the modal.** `wood 113 iron 38` from the scene behind is legible
through the panel body. Either the panel has no opaque backing or a world-space text layer is drawing
over the modal canvas. Read the sorting, do not guess.

## 3. WHAT TO ESTABLISH BEFORE EDITING

1. **What is the stray bar?** Name it from source. D1 cannot be fixed correctly until it has a name.
2. **Does the card list scroll?** If yes, the defect is a missing affordance. If no, the defect is a
   viewport that cannot show its content. Say which.
3. **How many cards can this screen hold at 2670x1200?** The owner has four camps and can see two and a
   half. **The count grows as she wins** - a fix that happens to seat four is the same bug at five.
   Derive the capacity; do not pick a number.
4. **What draws `wood 113 iron 38`?** It is a world/harvest readout appearing over a modal - find the
   layer, not a z-order nudge that happens to hide it.

## 4. CONSTRAINTS

- **The owner is red/green colourblind** (memory: `owner-colorblind-delegate-visual-creative`). The
  difficulty pips today are green/yellow/red bars down the left edge - **that is meaning carried in hue
  alone.** The words `Regular` / `Hard` / `Extreme` are present and carry it properly; **keep the words,
  and do not add any new hue-only signal.** Greyscale check is the gate.
- Touch targets: `ElarionUiKit.MinTouchPx` (112). A card row the player taps must clear it.
- **Any text band under ~24 px renders BLANK, not small.** If a fix shrinks a band to fit more cards, it
  deletes the text instead. This is a documented trap in this codebase.
- Do not restate `MaxVisibleFaces` or any pinned constant - read it at source.

## 5. ACCEPTANCE

- [ ] No element overlaps another on any card, proven by a **headless capture with the PNG opened and
      looked at** (memory: `headless-screenshot-verify-ui-before-build`). Compile-green never proves a
      panel looks right.
- [ ] Every card is fully readable and every camp is reachable, proven at **four AND at eight camps** -
      so the fix cannot pass by coincidentally matching today's count.
- [ ] Nothing from the world renders through the panel.
- [ ] A regression MEASURES the laid-out card rects and asserts zero overlap plus full containment. It
      must FAIL against today's build - state the RED proof in-file.
- [ ] The lock copy of section 1 is preserved verbatim.
- [ ] `REGRESSION_OK n/n`.
