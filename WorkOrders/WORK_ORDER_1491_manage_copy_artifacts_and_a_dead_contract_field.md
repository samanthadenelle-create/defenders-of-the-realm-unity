# WO-1491: Manage copy artifacts, a text-arrow back button, CLOSE on five panels the mockup does not, and a dead contract field

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 1-9 (chrome: back arrow, CLOSE, header) not yet passed (2026-09-07); code landed in the wave-four commit, gated 440/441. The owner walked all nine Manage screens on build 358872 beside MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-07, uncommitted, awaiting the gate + a fresh Manage capture. The)*
chrome half (back-arrow sprite, CLOSE on the hub only, the "MANAGE - BUILD" spelling) landed at
source; see `WORK_ORDER_1491_manage_copy_artifacts_and_a_dead_contract_field.RESULT.md`.
⛔ **Two items are deliberately NOT done and the RESULT says why:** `ProgressText` is COMPOSED as of
`ManageVmProjection.cs:337`, so this ticket's "declared, never composed" evidence is stale and the
field was not deleted; and two of the five copy artifacts are authored DATA rows, not code literals.
*(was: READY TO IMPLEMENT)*
**Silo:** Manage 2000-block (WO-2014, copy and chrome) + `ManageViewContract`.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1491 -> 1492 in the same edit).

## 1. EVIDENCE

Captured copy artifacts on the Manage screens:

```
"12 MORE - SCROLL"                       an instruction rendered as content
"stragglers. ."                          orphaned period, double space
"A defensive tower   auto-fires"         triple space mid-sentence
back button                              rendered as the literal text "<-"
CLOSE                                    present on panels 2, 4, 6, 7, 8;
                                         the mockup shows it on panel 1 ONLY
```

And a dead field:

```
ManageViewContract.cs:284   ProgressText   -- declared, never composed, never painted
```

`ProgressText` is the same composed-but-unpainted class WO-1444 opened on `FaceCountText`; this one is worse
- it is not even composed, so there is nothing to paint.

## 2. FIX SHAPE

- Copy pass across the Manage screens: fix the three whitespace/punctuation artifacts, turn `12 MORE - SCROLL`
  into an affordance rather than a sentence, and replace the `<-` literal with the kit back glyph.
- CLOSE stays on panel 1 only, per the mockup; the other five lose it (they have the back door).
- Delete `ProgressText` from the contract. It is dead weight in the file the architecture points at.

## 3. WHAT NOT TO DO
- Do not compose `ProgressText` to "use it up". Nothing asked for it; deleting is the smaller correct change.
- Do not reword player-facing sentences beyond the artifacts. Copy is the owner's call.

## 4. ACCEPTANCE
- [ ] The five copy artifacts gone; fresh Manage PNGs opened.
- [ ] CLOSE present on panel 1 only.
- [ ] `ProgressText` deleted; zero hits repo-wide (grep pasted).
- [ ] `REGRESSION_OK n/n` on a fresh log.
