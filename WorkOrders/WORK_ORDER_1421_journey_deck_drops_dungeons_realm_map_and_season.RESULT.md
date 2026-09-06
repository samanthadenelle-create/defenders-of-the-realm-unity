# RESULT - WO-1421: the Journey deck drops DUNGEONS, REALM MAP and SEASON

**Landed:** 2026-09-06 in `9ad5c7e3c` (with WO-1422; one commit, five file-disjoint Opus lanes, gated by the CLI).
**Implementer:** an Opus lane. **Gate, RED proofs, commit:** the CLI seat.

## What shipped
- `Assets/_Modules/HUD/PlayerDeckWorkspace.cs`: the three card entries and their four comment blocks are deleted from
  the `case PlayerDeckKind.Journey:` arm; the deck subtitle becomes **"Your quests, and the camps your army can raid."**
  authored as a labelled `case PlayerDeckKind.Journey:` stacked on `default:`; the two card-only helpers
  `AnyDungeonOpen()` and `DescribeDungeonDoors()` are deleted.
- `PublicNavigationRetirementRegression` **re-pointed, not deleted** (its header says NEVER DELETED and it has now
  flipped twice). Presence assertions become absence assertions against the raw Journey slice; the 09-04/09-05
  five-card history is kept as a HISTORY paragraph so the next reader knows why it flips; the RED recipe is inverted.
  The PackStore / InventoryUIBuilder / FeatureFlags-MapTab / paper-doll / HudKit blocks and the Realm-deck art pins are
  **byte-identical**.
- New `JourneyDeckTwoCardRegression` (8 cases), registered by the CLI in `DataRegression.cs`.
- `HudStrings.cs` keys and **both** canon-strings copies are untouched and dormant, deliberately: `HudLabelFitRegression`
  Case 1 iterates `AllKeys` and requires each key in both copies, so a tidy-up there is the failure mode. Case 8 of the
  new suite exists to pin exactly that.

## Gates - all on fresh logs, judged by marker
| Gate | Log | Marker |
|---|---|---|
| Compile | `Builds/c17` 02:16 | `COMPILE_GATE_OK` |
| Regression | `Builds/r17` 02:18 | `REGRESSION_OK 393/393 suites -- 393 green, 0 red, 0 skipped` |
| Manage capture | `Builds/capman6` 02:19 | `MANAGE_OPERATIONAL_CAPTURE_OK 12/12 frames; touch=clean` |

## RED proofs recorded by the CLI
| Case | Mutation applied | Result |
|---|---|---|
| `[no-season-card]` | re-added `Route("Season", "x", "season", PanelId.BattlePass)` to the Journey arm | `JOURNEY_DECK_FAIL [no-season-card] 'Route("Season",' is back - there is no season content at all yet` |
| re-pointed `PublicNavigationRetirementRegression` | the same one-line mutation | `PlayerDeckWorkspace.cs (code) names PanelId.BattlePass 1 time(s), expected exactly 0` |
Both fired from a single mutation on `Builds/rRED3`, then the tree was restored with `git checkout --` and the working
tree verified clean. This proves the re-point works **in the new direction**, which is the thing a re-pointed oracle
most needs to demonstrate.

## Deviations, recorded
1. **The lane redesigned the WO's revert recipes.** The WO's section 5 gave two recipes that were the same mutation and
   one that would trip a different case, so eight cases could not have been distinguished. The invariants asserted are
   exactly the eight specified; only the mutations differ. The lane also ordered the cases 2,3,4,1,5-8 so the count case
   cannot mask the three specific ones.
2. **Four comment blocks were deleted, not one.** The WO named only the intro block; the flipped assertions read the raw
   slice, so a surviving comment naming `PanelId.RealmMap` would have failed them.
3. **The canon-strings `_journeyCardSubtitleNote` re-word was SKIPPED**, as the WO permitted. It gates nothing.
4. **The section 7 canon banners were NOT applied by the lane** (WO-1394 / WO-1376 / WO-1396, the `FeatureFlags.cs`
   note, and the two dated docs). Outstanding - see below.

## Findings worth keeping
- `HudStrings.KeyDungeonSealedHeadline` now has **zero readers of the constant**; the deleted Dungeons card was its only
  one. The underlying canon row is still live, read by `DungeonSealedDoorPanel` and `DungeonStatusCatalog`. Left in
  place deliberately.
- `UICaptureLaunch`'s Journey fixture still seeds a dungeon payload and lists `PanelId.RealmMap` / `PanelId.BattlePass`
  in `fixtureDoors`. Now inert, harmless, not cleaned up - a follow-up decision, not a defect.
- The deck falls from three rows to two, so each card cell grows. Expected (`RenderPage` clamps rows to a minimum of 2).

## Still open
- [ ] **Owner felt-test on the device closes this ticket** (build below).
- [ ] The deck subtitle wording is an authored default; she may re-word it.
- [ ] Section 7 canon banners for WO-1394 / WO-1376 / WO-1396 and the two dated docs.
- [ ] Whether the Season and Realm Map panels are later retired outright or wait for content. Deliberately not decided.
