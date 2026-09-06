# WO-1421: the Journey deck drops DUNGEONS, REALM MAP and SEASON - two cards, not five

**Status:** FIXED 2026-09-06 - landed in `9ad5c7e3c`; COMPILE_GATE_OK (c17) + REGRESSION_OK 393/393 (r17) + MANAGE_OPERATIONAL_CAPTURE_OK 12/12 (capman6); both Journey oracles proven RED then GREEN (rRED3); RESULT file has the deviations. Device build in flight; owner felt-test closes. *(was: READY TO IMPLEMENT - minted 2026-09-06 (CLI) from the owner's ruling, dispatched to an Opus lane the same night)*
**Silo:** HUD / PlayerDeckWorkspace (DeNelle.HUD) - one code file + one suite re-point
**Owner ruling (2026-09-06, verbatim):** *"Under journey, please remove dungeons season in realm map as they should
not be displayed there right now we don't have anything for seasons at all dungeons are very vague and ambiguous and
there is no realm map other than the regular realm right now so please remove those"*
**Base commit:** `0e274bf25` (clean tree at dispatch).

---

## 1. The defect, measured

Device screencap, Seeker build `2026.09.06.357453`, 2026-09-06 00:55 (read by the CLI, saved
`logs/device/screens/seeker-357453-journey.png`). The Journey deck paints FIVE cards:

```
                              JOURNEY
        Quests, raids, dungeons, the realm map, and the season.
  [ QUESTS   0 active . 0 ready to claim ]  [ RAIDS  Army 8 / 10 . train to open a camp ]
  [ (D) DUNGEONS  Descend into forgotten places. Return with what ... ]  [ (R) REALM MAP  Explore the lands beyond... ]
  [ (S) SEASON   Face the challenge shaping the Realm now. ]
                              [CLOSE]
```

Three of the five are doors to content the owner says is not ready. Two of them paint a bare LETTER medallion
(`D`, `R`, `S`) where Quests and Raids paint real art - the screen itself shows they are stubs. One of them,
DUNGEONS, does not even open a dungeon: it opens `PanelId.RealmMap` with the argument `"dungeons"`
(`PlayerDeckWorkspace.cs:747`).

## 2. The target

TWO cards - QUESTS and RAIDS - and a deck subtitle that describes two cards. Nothing else on the screen changes.

⚠ **Geometry consequence, expected, not a defect:** `RenderPage` computes
`rows = Mathf.Max(2, CeilToInt(cards.Count / 2f))` (`PlayerDeckWorkspace.cs:115`, 2 columns at `:107`). Five cards
gave three rows; two cards clamp to **two** rows, so each card cell gets taller. Record the new cell height in the
hand-back and open the capture frame to confirm nothing overflows.

## 3. The ruling that governs the shape of this change

**REMOVE THE CARD. DO NOT DELETE THE DESTINATION, AND DO NOT ADD A FEATURE FLAG.**

- She said *"right now"*. The panels stay compiled, registered and intact so a later card re-add is one line.
- ⛔ **A flag is the WRONG mechanism here and would fail the gate.** `FeatureFlags.MapTab` was DELETED on 2026-09-05
  (`FeatureFlags.cs:832-838`) and its ABSENCE is now pinned by `PublicNavigationRetirementRegression.cs:80-81`.
  Reintroducing a `public static bool` of that shape fails that suite. Delete the card entries.
- **Accept two orphans, deliberately.** After this change `PanelId.RealmMap` and `PanelId.BattlePass` are registered
  with **no player-facing door** (`RealmMapPanel.cs:206`, `BattleMonthlyPanelsBootstrap.cs:64`; the only other
  `RealmMap` opener is `DevTools/DevPanelController.cs:871`, a dev panel). That is the owner's call, taken with the
  facts in front of her. It reverses WO-1394, which existed to give the Season panel a door - see section 7.
- **DUNGEONS keeps a real door** and is the one clean case: the world portal
  (`Village/Buildings/DungeonPortal.cs` -> `Dungeons/DungeonController.EnterDungeon:166`), which AutoPilot already
  drives (`AutoPilotDriver.cs:752,1376`). Removing the card orphans nothing here.

## 4. The edit surface (all of it)

### 4a. `Assets/_Modules/HUD/PlayerDeckWorkspace.cs` - the only runtime file
1. **Delete the three card entries** from the `case PlayerDeckKind.Journey:` arm of `CardsFor` (`:672-763`):
   - `new Card { Title = "Dungeons" ... }` at `:738-748`
   - `Route("Realm Map", ..., PanelId.RealmMap)` at `:756`
   - `Route("Season", ..., PanelId.BattlePass)` at `:762`
   Delete the WO-1376/1394/1396 comment block at `:716-737` that introduces them, and fix the trailing comma so the
   Raids entry terminates the list.
2. **Rewrite the deck subtitle.** It is a hardcoded literal in the `default:` arm of `SubtitleFor` at `:88`
   (NOT in canon-strings; verified one occurrence repo-wide; no suite pins it). Add a
   `case PlayerDeckKind.Journey:` for symmetry with `:86` Realm and `:87` Hero, returning a two-card sentence.
   **Authored text: `"Your quests, and the camps your army can raid."`** Owner may re-word at felt-test.
3. **`AnyDungeonOpen()` (`:782-795`) and `DescribeDungeonDoors()` (`:797-808`) become unreferenced.**
   **RULING: DELETE BOTH.** They exist only to decide whether the removed card is available; `DungeonStatusCatalog`
   itself stays (it is used by `DungeonStatusRegression`, `MaintenanceTogglesRegression` and the portal), so nothing
   else loses a dependency. Leaving dead private helpers behind is the drift CLAUDE.md section 5 was corrected for.
   ⚠ This is a DELETION of code, not of instrumentation - CLAUDE.md section 12's never-strip-FlowTrace rule does not
   apply to a `FlowTrace.Step` that lives inside a deleted helper. If either helper contains a trace line that is also
   reachable from a surviving path, keep that path. Say in the hand-back which traces went.

### 4b. `Assets/Editor/Regression/PublicNavigationRetirementRegression.cs` - RE-POINT, NEVER DELETE
This suite is the one that goes RED, and its own header says **"STRICTER, NEVER DELETED"** (`:7`). It was already
re-pointed once (absence -> presence, 2026-09-05); it now re-points back the other way, WITH the ruling, in the SAME
commit (CLAUDE.md section 15).

Cases that must flip from presence to ABSENCE, each against the Journey deck source:
| line | today asserts | after |
|---|---|---|
| `:57` | `AssertCountExactly(deckCode, "PanelId.BattlePass", 1)` | count == 0 |
| `:58` | `AssertCountExactly(deckCode, "Route(\"Realm Map\",", 1)` | count == 0 |
| `:59` | `AssertCountExactly(deckCode, "Route(\"Season\",", 1)` | count == 0 |
| `:62-65` | `AssertPresent(journey, "PanelId.RealmMap", "PanelId.BattlePass", "Route(\"Realm Map\",", "Route(\"Season\",", "Title = \"Dungeons\"", "DungeonStatusCatalog")` | all six ABSENT from the deck file |
| `:67-70` | `AssertPresent(journey, HudStrings.Get(KeyJourneySeason / RealmMap / Dungeons))` | all three ABSENT from the deck file |

- ⛔ **Keep the suite REGISTERED at `DataRegression.cs:1019`.** `RegressionMarkerRegression.TryGetExpectedSuiteCount`
  counts registration call-sites in source (~`:1585-1590`), so removing a registration shifts the pinned denominator
  and breaks the marker count.
- **Do NOT touch** `:73-85` (PackStore / InventoryUIBuilder / HeroInventoryController / FeatureFlags-MapTab /
  InventoryPaperDoll / HudKitController absence blocks) or `:88-92` (the Realm-deck art pins). They are unrelated and
  still true.
- Update the suite's doc comment at `:33-36`: its stated RED recipe ("delete the Route line") is now the GREEN state.
  The new RED recipe is "re-add a `Route("Season", ...)` entry to the Journey arm".

### 4c. What to leave ALONE - the trap
`HudStrings.cs:116-136` (`KeyJourneyDungeons` / `KeyJourneyRealmMap` / `KeyJourneySeason` and their presence in
`AllKeys`) and the canon-strings rows at `:409-411` in **BOTH** copies
(`Assets/Resources/Data/Canonical/canon-strings.json` and `Assets/StreamingAssets/Data/Canonical/canon-strings.json`)
**stay exactly as they are**, dormant. `HudLabelFitRegression.cs:305` `Case1_CanonParity` iterates `AllKeys` and
requires every key in both copies; deleting a key from one of the four places and not the others is the failure mode.
Only the stale authoring note `_journeyCardSubtitleNote` (canon-strings `:406`) may be re-worded, in both copies, with
the newline count proven (memory `canonical-json-edits-binary-only-verify-newlines`: patch from HEAD bytes, byte-mode
write, assert the LF count is unchanged).

## 5. Regression - `JourneyDeckTwoCardRegression` (new file), marker `JOURNEY_DECK_OK` / `JOURNEY_DECK_FAIL <case>`

The lane authors the suite AND hands back its one registration line for `Assets/Editor/DataRegression.cs` **as text**;
that file is a CLI-owned merge point, do not edit it. Every case carries a one-line REVERT RECIPE in a comment; the CLI
applies it, proves RED, restores, proves GREEN, and records both in the RESULT.

Source-scoped cases against `PlayerDeckWorkspace.cs`:
1. `[journey-two-cards]` the `case PlayerDeckKind.Journey:` body contains exactly two `Title =` / `Route(` card
   constructions. RED: re-add a `Route("Season", ...)` line.
2. `[no-dungeons-card]` the Journey arm does not contain `Title = "Dungeons"`. RED: re-add it.
3. `[no-realm-map-card]` / 4. `[no-season-card]` same for the two `Route(` literals.
5. `[subtitle-names-two]` the Journey subtitle string contains neither `dungeon` nor `season` nor `realm map`
   (case-insensitive) AND is non-empty. Both directions, so it cannot pass on an empty string.
6. `[dungeon-helpers-gone]` `AnyDungeonOpen` and `DescribeDungeonDoors` are absent from the file. RED: restore either.
7. `[quests-and-raids-survive]` the Journey arm still contains `TraceJourneySubtitle("Quests"` and
   `TraceJourneySubtitle("Raids"` - the removal must not take the two good cards with it. RED: delete one.
8. `[canon-keys-dormant-not-deleted]` `HudStrings.cs` still declares all three Journey keys and `AllKeys` still lists
   them. RED: delete a key. **This case exists because the tempting "tidy up" is exactly what breaks
   `HudLabelFitRegression` Case 1.**

A missing fixture is a FAIL that names itself. No hollow passes.

## 6. Acceptance
- [ ] Brace balance + NUL scan on every `.cs` touched (counts in the hand-back); new `.meta` guid unique.
- [ ] `COMPILE_GATE_OK` on a fresh log.
- [ ] `REGRESSION_OK n/n` with `PublicNavigationRetirementRegression`, `JourneyDeckSubtitleRegression`,
      `HudLabelFitRegression`, `RaidsDiscoverabilityRegression`, `DungeonStatusRegression`,
      `TutorialStepReachabilityRegression` and `SessionShapeRegression` green, and `JourneyDeckTwoCardRegression`
      green **with all eight RED proofs on record**.
- [ ] `RunCaptureHeadless` -> `UI_CAPTURE_OK`; the `JourneyWorkspace` frame OPENED by the CLI at 2670x1200 and
      1920x1080: two cards, taller cells, real art on both, no clipping by CLOSE, zero `[UICap-GEO]` lines.
- [ ] Device: the owner opens Journey on the tester build and closes the ticket.
- [ ] Deviation recorded: the deck falls from three rows to two (section 2).

## 7. Canon that this ruling supersedes (section 15 - banner, do not rewrite bodies)
- **WO-1394** existed to give the Season panel a player door. That door is now removed by owner ruling; banner it
  SUPERSEDED 2026-09-06 and say why.
- **WO-1376 / WO-1396** (the Realm Map and Dungeons cards) likewise.
- `FeatureFlags.cs:832-838`'s retirement note and canon-strings `_journeyCardSubtitleNote` (`:406`) are now stale in
  their description of the deck - re-word, do not delete.
- The doc references at `docs/CREATIVE_CANON_ELARION_2026-09-04.md:438-440` and
  `docs/qa/UI_SCREEN_GRAPH_2026-09-04.md` are dated ledgers: **banner them, do not rewrite**.

## 8. Not in scope
The Quests and Raids cards; the dungeon portal and every dungeon system behind it; the Realm Map and Season PANELS
(they stay compiled and registered); the Realm deck; the action bar; any canon-strings key deletion.

## 9. Open for the owner at felt-test
- The new deck subtitle wording (section 4a item 2 ships an authored default).
- Whether the Season and Realm Map panels should later be retired outright, or wait for content. This WO deliberately
  leaves them intact and does not decide it.
