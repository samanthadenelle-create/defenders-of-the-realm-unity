# WORK ORDER 1219 - The top-left HUD corner is over-subscribed, two labels truncate, and the action bar is missing Talk

**Status:** IMPLEMENTED + gate-green - DEVICE/FELT-VERIFY OWED (not FIXED/DONE)
**Silo:** HUD layout
**Origin:** CLI observation from owner felt-test device captures, 2026-08-26, Seeker build
`2026.08.26.341419`. Three separate defects, one lane (all HUD layout), so one ticket and one seat.

## PROOF (captured, two shots so the reader can separate cause from coincidence)

- `tmp/screen-103219.png` (2670x1200, device, no toast on screen)
- `tmp/shield-seat-101829.png` (same session, with the Repair All toast up)

### Slice A - the top-left corner collides with itself

In **both** shots the settings gear and the **Store** button overlap the minimap's lower edge, and
the status line `Elarion - Safe - N threats` runs UNDERNEATH the gear icon and is partly unreadable.

⭐ **The toast is not the cause.** The first capture showed the `REPAIR ALL / Wood 155 / Iron 78`
toast drawn across the minimap, the status line AND the Wood readout, which read as a toast
placement bug. The second capture has **no toast** and the corner is still colliding. The corner is
genuinely over-subscribed: minimap + status line + gear + Store are all claiming the same space.
**Fix the corner's layout, not the toast's position** - though confirm afterwards that the toast
still has somewhere legal to land.

### Slice B - two labels truncate

- Top-left reads **`SK... 177`** - clipped to roughly six characters plus an ellipsis. Resolve what
  the full string is meant to be and give it the width, or shorten the authored string. ⚠ If this is
  an SKR balance, it is a **money-adjacent readout** and a truncated one is worse than none.
- The action-bar face reads **`Raids ...`** - truncated, and its face renders visibly darker than
  its four neighbours. Establish whether the dark face is a real disabled state or a styling
  inconsistency before changing either.

### Slice C - the action bar shows FIVE faces; canon says six

Observed, both shots: **Build · Bag · Raids… · Quests · Manage**. **Talk is absent.**

`CLAUDE.md` §7 states the calm(town) bar is **SIX faces: Build, Talk, Bag, Raids, Quests, Manage**,
with `HudActionBarModel.MaxVisibleFaces` at 6 and `ButtonCount` held at 7 for enum identity.

⛔ **DIAGNOSE BEFORE EDITING - three very different causes, and §12 forbids guessing between them:**
1. Talk is **posture-gated** and correctly hidden here (the shot IS calm-town: Manage reads
   "3 idle", so posture is town) - in which case canon is wrong and the DOC is the fix.
2. Talk is **suppressed by a slot-geometry bug** - five slots rendering where six are configured.
3. Talk was **dropped from the face array** at some point and nobody noticed.

⛔ **NEVER RENUMBER `ActionBarButtonId`.** `Map` is deliberately dormant at **ordinal 4** and the face
arrays are indexed BY ORDINAL. `Upgrade = 6` keeps its value, its `upgradeButton` widget id and its
`hud-areas.json` row - that re-point is what dissolved the 8th-face problem. Renumbering re-opens it.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ **DEVICE SCREENSHOTS at 2670x1200, before and after, opened and looked at.**
   ⛔ `UI_CAPTURE_OK` proves a panel RENDERED, never that it looks right - two broken panels reached
   the owner behind green markers. This ticket is not done on a marker.
3. ⚠ **Per-panel counts bind; a repo-wide `UI_TOUCH_FAIL` total does NOT.** If you cite a repo-wide
   figure it must be measured for THIS ticket with its baseline sha named. File-disjoint tickets land
   in parallel and each moves the total.
4. ⭐ The capture log carries its own `UI_CAPTURE_HEAD <sha> <branch> dirty=false` provenance stamp.
   ⛔ A `dirty=true` capture MAY NOT be cited - there is no commit to diff against.
5. Slice C states in the RESULT **which of the three causes it actually was**, with the proving line.
   If it was cause (1), the fix is a `CLAUDE.md` §7 correction in the SAME commit (§15).
6. Owner felt-verifies and CLOSES.

## What NOT to touch

- ⛔ `ActionBarButtonId` ordinals, in any direction. See Slice C.
- ⛔ `HudActionBarModel.ButtonCount` (stays 7 - enum identity / array bound). The number that moved
  7 -> 6 is `MaxVisibleFaces`.
- ⛔ `ClampMinTouch` as a diagnosis - it has already been checked and ruled out at three sites
  (bands 117 / 116.7-130.6 / exactly 112.0 px). Check the band arithmetic before naming it.
- The `MinTouchPx` 112 floor. Nothing here may shrink a touch target below it.


---

## UI SEAT DELIVERABLE (2026-08-26) - APPROVED CORNER RE-SEAT + TOAST ZONE + RULINGS

**Owner approved the design this session ("go").**
**Mockup:** `WorkOrders/WORK_ORDER_1219_mockup_2670x1200.png` (also `tmp/hudcorner_mockup_2670x1200.png`).
The white outline in the mockup marks the reserved toast ZONE - it does not ship.

### Slice A - the left column becomes ONE vertical stack of exclusive bands
Screen fractions (x left->right, y BOTTOM->top), px @2670x1200 (top-down y):

| Band            | xMin  | yMin  | xMax  | yMax  | px |
|-----------------|-------|-------|-------|-------|----|
| Hero plate      | 0.011 | 0.883 | 0.240 | 0.983 | x 30-640, y 20-140 |
| SKR chip        | 0.011 | 0.818 | 0.124 | 0.870 | x 30-330, y 156-218 |
| Heart bar       | 0.011 | 0.748 | 0.240 | 0.805 | x 30-640, y 234-302 |
| Minimap         | 0.011 | 0.485 | 0.124 | 0.735 | x 30-330, y 318-618 |
| Status line     | 0.011 | 0.428 | 0.240 | 0.472 | x 30-640, y 634-686 - its OWN band, below the minimap, never across it |
| Gear            | 0.011 | 0.322 | 0.055 | 0.415 | 116x112 px |
| Store           | 0.061 | 0.322 | 0.142 | 0.415 | beside the gear, not under the status text |

### The legal toast zone (Repair All and any transient toast on this screen)
x 0.375-0.625, y-up 0.203-0.308 (px x 1000-1670, y 830-956) - centered above the action bar,
overlapping nothing in either capture's state. Toasts land HERE, never in the corner.

### Slice B rulings
- **SKR chip: WIDTH, not a shorter string.** The chip fits `SKR 177` whole, sized for six digits
  before FitLine autoshrink. A truncated money-adjacent readout is worse than none (WO's own law).
- **Dark Raids face: CLI diagnosis, required in the RESULT with the proving line** - real disabled
  state (`RaidCapable` false) vs styling bug. If it IS a disabled state, the treatment must be
  non-hue (dimmed label + count, e.g. `Raids 0/3`), greyscale-separable from enabled peers.

### Slice C - CLOSED BY CANON, no defect
CLAUDE.md sec.7 corrected 2026-08-26: the calm(town) bar is FOUR always-on faces + Talk ONLY while
`TalkPromptRegistry.Count > 0` (TalkHudBridge.cs:69) and Raids gated on RaidCapable. Both captures
are open ground -> FIVE faces is the feature working. CLI verifies the gate (stand at an NPC, see
Talk appear) and records it; nobody "fixes" the five-face bar. `MaxVisibleFaces = 6` is a MAXIMUM.
`ActionBarButtonId` ordinals and `ButtonCount = 7` untouched, per this WO's own fence.

---

## IMPLEMENTATION CLOSEOUT AUDIT (2026-08-26)

The implementation is present and the fresh Batch 0 gates are green:

- `Builds/batch0-compile-2.log:1966` - `COMPILE_GATE_OK :: scripts compiled clean`
- `Builds/batch0-regression-2.log:24804` and `:83504` - `HUDUI_OK`; the marker reports five
  resolution/cutout cases for the safe-area corner and the shared resource-rail checks green.
- `Builds/batch0-regression-2.log:83814` - `REGRESSION_OK 291/291 suites -- 291 green, 0 red, 0 skipped`

Relevant implementation/proof files inspected in this closeout:

- `Assets/_Modules/Core/UI/HudLayoutBands.cs`
- `Assets/_Modules/HUD/Kit/HudAreasHost.cs`
- `Assets/_Modules/HUD/Kit/HudKitController.cs`
- `Assets/_Modules/HUD/Kit/HudMinimapWidget.cs`
- `Assets/_Modules/Village/Walls/HubRepairAffordance.cs`
- `Assets/Editor/Regression/HudUiRegression.cs`

This does **not** earn FIXED/DONE yet. Acceptance still owed: a post-fix 2670x1200 device capture
opened and visually inspected; its capture log's `UI_CAPTURE_HEAD <sha> <branch> dirty=false`
stamp; device confirmation that Talk appears while standing at an NPC; and owner felt-verification
and close. The pre-fix screenshots in this ticket prove the defect, not the repaired pixels.
