# WO-1428: the Manage card grows to the owner's mockup - large art, a before/after STATS table, a REQUIREMENTS checklist, and a NEXT LEVEL preview

**Status:** READY TO IMPLEMENT - minted 2026-09-06 (CLI) from the owner's own mockup, pasted in-session
**Silo:** HUD / Manage (Village assembly, code-built uGUI) + one Core producer
**Owner ruling (2026-09-06):** she pasted a Manage - Buildings mockup and asked *"wasnt this the idea we were going for?"*
The answer is **yes, and what shipped in WO-1422 is a thinner version of it.** This WO closes the gap.
**ABSORBS WO-1427** ("why can't I?"). The mockup's REQUIREMENTS block answers that question visually and per-resource,
which is strictly better than a sentence. WO-1427's `UnlockPath` producer survives as the DATA behind this card - see §4.
**Base commit:** filled at dispatch. Lands after tonight's four fixes are committed.

---

## 1. The gap, measured against what shipped

Shipped today (`ManageBuildings_2670x1200.png`, `capman8`): a 112 px rail medallion, a name, `LEVEL n`, a state badge,
ONE description line, ONE cost chip row, ONE "After upgrade:" sentence, two CTAs, a queue band. About **2.3 rail rows**
are visible.

Her mockup, read off the image she pasted:
| Region | Mockup | Shipped |
|---|---|---|
| Top bar | five live RESOURCE COUNTERS (12.4K wood, 8.1K, 3.2K, 1.8K, 156) + BACK + QUEUE | BACK + QUEUE only |
| Header | `MANAGE - BUILDINGS` + subtitle `BUILD A STRONGER ELARION` | title only |
| Channel chips | Builders 0/2, Training 0/2, Research 0/2 | present, same |
| Left rail | **8+ rows visible**, medallion + name + `Level n` + chevron, scrollable, an **Edit** affordance pinned at the bottom | ~2.3 rows, no Edit |
| Centre | **LARGE illustrated building art** filling ~40% of the card | a ~150 px medallion |
| Centre text | name, `Level 1`, a two-line description | name, `LEVEL n`, one line |
| **STATS** | a 3-row table with **before -> after**: `Wood Production 300/hour -> 450/hour`, `Worker Capacity 3 -> 4`, `Storage Capacity 2,000 -> 3,500`, the after-value in green | one `After upgrade:` sentence |
| **REQUIREMENTS** | per-resource chips, each showing **cost over your balance with a tick**: `850 / 12.4K ✓` | a cost row + the word `Short` |
| Time + CTA | a clock `15m 00s` beside a large `UPGRADE` | `57s . Short` beside `UPGRADE TO L2` |
| **NEXT LEVEL** | a right column with **two art thumbnails**, `Level 1` above `Level 2`, showing the building's visual progression | nothing |
| Footer | flavour line `Stronger buildings. A greater realm.` | the Build-new action row |

## 2. Why this is the right shape, not just a prettier one

The owner reported, the same night: *"i had no way to figure out why i couldnt upgrade"* and *"i couldnt tell Oh im
missing gold, ohhh i need a foundry, whatever"*. **The REQUIREMENTS block is the answer** - a tick or a cross against
each resource, with her own balance beside the cost, tells her which resource is short in one glance and without
reading a sentence. The STATS table answers the other half - *what do I get* - which today is a single sentence that
truncated on her device.

She also said *"the disconnect is the massive amount of data in manage"*. Note that the mockup does NOT hold less data
than we ship - it holds MORE. What it does is **give each fact a fixed home and a shape**: numbers in a table, costs in
chips, progression in thumbnails. Volume is not the problem; undifferentiated text is. Build to the mockup, do not trim
to it.

## 3. RULINGS

### 3.1 The rail shows ~8 rows, not 2.3
Her rail is a genuine list. `TroopRailRowPx = 112` inside a `TroopWorkspacePx = 260` well is what caps us at ~2.3.
**The workspace row must grow** so the rail can hold ~8 rows at ~112 px, i.e. roughly 900 px of well.
⛔ **`TroopWorkspacePx`, `TroopCtaY0`, `TroopCtaY1`, `TrainingNowBandPx` and `TrainingNowRowPx` are read BY NAME by
`ManageQueueDrawerRegression.cs:205,230` and pin `DrawerModeListKeepPx = 10f + TroopWorkspacePx * (1f - TroopCtaY1)`.**
Changing them moves that pin. If you change a constant you MUST move its pin in the same commit, with the ruling cited -
never silently. The CTA band currently clears the 112 px touch floor by **1.1 px**; recompute it after any change and
state the new margin in the hand-back.

### 3.2 The stats table is DATA, not authored copy
`Wood Production 300/hour -> 450/hour` must be COMPUTED from the tier rows, never typed. The current tier `Effect`
string is prose and cannot be diffed. Author a producer that returns, per stat, `(Label, CurrentText, NextText)` read
from the two tier rows' `GameModifiers` / repo props. Where a stat is not authored for a building, omit the row - do
not invent one and do not print a placeholder.
**Green on the after-value is decoration only.** The owner is red/green colourblind: the ARROW and the two numbers
carry the meaning, the colour never does.

### 3.3 REQUIREMENTS ticks come from ONE producer, shared with the refusal path
Each chip shows `cost / balance` and a tick when `balance >= cost`. **The producer is WO-1427's `UnlockPath`** -
this WO absorbs that ticket but keeps its Core producer, because the same truth must drive the chip ticks, the CTA's
enabled state and the refusal sentence. Three surfaces computing affordability three ways is exactly the duplicated
state this repo keeps being corrected for.
**A capacity block outranks a shortfall on the chip too:** if the bank cannot HOLD the cost, the chip must say so
(that is tonight's WO-1425 work - reuse `TownBankCapacity.StorageBlockMessage`, do not re-derive).

### 3.4 NEXT LEVEL preview reuses the portrait ladder that already exists
`Portraits/Buildings/<id>-<level>.png` already holds tier art for six ladders (26 files). The preview column is two
thumbnails at `level` and `level+1` through the SAME loader the card uses. Where the next tier has no art, show the
current art dimmed with the level label - never an empty frame.
⚠ Defense tier art lives in `Portraits/` root and is reachable only via the level-suffixed probe added tonight. Reuse
`DefenseSprite`'s chain; do not write a third resolver.

### 3.5 The large art is the biggest ART ask in the file, and it is NOT blocking
The mockup's centre art is a rendered illustration per building per tier. We have medallion-scale portraits. **Ship
with the existing portrait scaled into the art plate**; the illustration is an art drop, tracked in
`docs/ART_REQUEST_2026-09-06_manage_tab_portraits.md`. Do not block this WO on it.

### 3.6 Text bands never go below the seat threshold
Established three times on 2026-09-06: **TMP culls an entire line when its `fontSizeMin` cannot seat in the rect** -
a band under ~24 px renders BLANK, not small. The stats table is the risk here: three rows plus a header in a short
band is exactly the shape that vanished. Every band you author states its px height in a comment.

## 4. Lanes (file-disjoint)
- **A - `ManageScreenVM.cs`**: the stats-delta producer, the requirements model (cost, balance, met, capacity-blocked),
  the next-level art keys. Extend `BuildingChoiceVM`; mirror onto Defense/Troops/Research where the concept applies.
- **B - `ManageScreenPanel.cs`**: the card re-layout - art plate, stats table, requirements chips, next-level column,
  the taller workspace row and the ~8-row rail. Compiles after A.
- **C - Core `UnlockPath`** (WO-1427's producer) + its oracle, plus the requirement-tick source of truth.
- **D - `UICaptureLaunch.cs`**: fixtures that exercise a met requirement, an unmet one, a capacity-blocked one, a max
  building, and a locked one; plus the flow-map capture already in flight.
- **E - suites**: re-point anything that pins the old card shape; new cases per §5.

## 5. Regression - the cases that matter
1. `[stats-are-computed]` no stat row's text is a literal from the catalog `Effect` prose; each is derived from two
   tier rows. RED: hardcode one.
2. `[stats-omit-unauthored]` a building with no authored worker capacity shows NO worker row, not a zero or a dash.
3. `[requirements-tick-matches-truth]` a chip's tick equals `UnlockPath`'s affordability for that resource, for every
   resource of every rung. RED: tick on `>` instead of `>=`.
4. `[capacity-outranks-shortfall-on-the-chip]` a cost above the bank ceiling shows the capacity message, not "short".
5. `[next-level-art-falls-back]` a ladder with no next-tier art shows the current art dimmed, never an empty frame.
6. `[no-blank-band]` every authored band in the card is >= the TMP seat threshold. RED: shrink one to 18 px.
7. `[rail-shows-eight]` the rail's visible row count at 2670x1200 is >= 8. RED: restore the 260 px workspace.
8. `[touch-floor]` the CTA band still clears `ElarionUiKit.MinTouchPx` after the re-layout.

## 6. Acceptance
- [ ] Brace + NUL on every `.cs`; new `.meta` guids unique.
- [ ] `COMPILE_GATE_OK`; `REGRESSION_OK n/n` with every Manage suite green and all eight new cases RED-proven.
- [ ] `MANAGE_OPERATIONAL_CAPTURE_OK 12/12` and the flow-map capture; frames OPENED by the CLI at 2670x1200 and
      1920x1080 showing: 8 rail rows, the art plate, three stats rows with arrows, requirement chips with ticks, the
      next-level column, and NO blank band.
- [ ] The owner's own case: an Archer Tower at L2 with a level-1 Lumberyard shows the capacity-blocked chip.
- [ ] Owner felt-test closes it.

## 7. Open for the owner
1. The **Edit** affordance at the rail's foot in her mockup - what does it open? Reorder the list, or enter build mode?
2. The top-bar resource counters duplicate the town HUD's. Keep both, or is Manage the only place they belong?
3. The footer flavour line (`Stronger buildings. A greater realm.`) - ship it, or is it mockup dressing?
4. Whether Troops / Defense / Research get the same four regions, or whether some (e.g. Research) stay simpler.
