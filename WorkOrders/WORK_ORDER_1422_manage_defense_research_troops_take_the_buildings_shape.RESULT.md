# RESULT - WO-1422: Manage Defense, Research and Troops take the Buildings workspace; the paged list is retired

**Landed:** 2026-09-06 in `9ad5c7e3c`. Five file-disjoint Opus lanes (VM / Panel / suites / capture fixture / a polish
pass driven by opened frames), gated, RED-proved and committed by the CLI seat. **Codex was not in the loop; the lanes
were held to the Codex standard.**

## Gates - fresh logs, judged by marker, never by exit code
| Gate | Log | Marker |
|---|---|---|
| Compile | `Builds/c17` 02:16 | `COMPILE_GATE_OK`, zero `error CS` |
| Regression | `Builds/r17` 02:18 | `REGRESSION_OK 393/393 suites -- 393 green, 0 red, 0 skipped` (up from 390: three new suites) |
| Manage capture | `Builds/capman6` 02:19 | `MANAGE_OPERATIONAL_CAPTURE_OK 12/12 frames; four destinations; touch=clean`, zero `[UICap-GEO]` |

**Frames OPENED by the CLI** at 2670x1200 and 1920x1080: `ManageDefense`, `ManageResearch`, `ManageTroops`,
`ManageBuildings`. What they show is recorded below, not asserted.

## What the frames prove
- **Defense** now paints the workspace: a rail reading `Archer Tower / Level 1 . x2` and `Ballista / Level 1` with
  **real tower art**, a selected card with the `Upgradable` badge, `2 placed . lowest L1 - A defensive tower...`, cost
  chips with icons, `After upgrade: Raises Archer Tower to Level 2 of 3.`, `UPGRADE TO L2`, and the shared
  `BUILDING NOW` band with a live bar.
- **Research** paints one row per PERK with real perk art, `TIER 3` (never `LEVEL 0`), the `Available` badge, a gold
  cost chip, `RESEARCH`, and a `RESEARCHING NOW` band reading **`Warding Runes`** with its icon - a state that had
  never rendered in any capture before tonight (see finding 2).
- **Troops** closes all three parity gaps: the `Upgradable` badge paints its word, the `Army 0 / 10` line is back, and
  the benefit line reads in full - `After upgrade: L3 unlocks Sweeping Cut`, where the device frame on `357453` showed
  `4m 30s . Ready . L3 unlocks Sweepi...`.

## RED proofs recorded by the CLI (six distinct oracles, four suites)
Everything was committed first, so each mutation was restored with `git checkout --` and the tree verified clean.
| Log | Mutation | Oracle that fired |
|---|---|---|
| `rRED1` | `RenderDefenseDestination` calls `AddResearchNowBand()` instead of `AddBuildingNowBand()` | `MANAGE_DEFENSE_CARD_FAIL [defense-band-is-builder]` |
| `rRED1` | `ResearchSprite` points at `HudItems/BuildingUpgrades/` (the path the stale doc comment named) | `MANAGE_RESEARCH_CARD_FAIL [perk-icon-path]` |
| `rRED2` | the tally keeps the HIGHEST placed level instead of the lowest | `MANAGE_DEFENSE_CARD_FAIL [lowest-level-targeted]` |
| `rRED2` | **both** `"Build defense", OpenDefenseBuilder` occurrences deleted | `BUILD_COLLECTION_PLAYER_FAIL` |
| `rRED3` | `Route("Season", ...)` re-added to the Journey arm | `JOURNEY_DECK_FAIL [no-season-card]` |
| `rRED3` | the same mutation | re-pointed `PublicNavigationRetirementRegression` - *"names PanelId.BattlePass 1 time(s), expected exactly 0"* |
**Note on the first attempt:** deleting only ONE of the two `"Build defense"` occurrences did NOT fire the oracle. That
is correct - the check is file-wide and one occurrence survived. Recorded because a seat could mistake it for a hollow
pass.

## THREE FINDINGS THE LANES MEASURED THAT THE CLI HAD WRONG (CLAUDE.md section 11B)
1. **The empty Defense capture was not ceiling filtering.** The CLI wrote that `BuildDefenseBrowse` skipped the tower.
   It did not - the tower was L1 of 3. The bail is `ManageScreenVM.cs:821` (`entry == null`): `CatalogBootstrap`'s
   `[RuntimeInitializeOnLoadMethod]` **never fires under `-executeMethod`**, so `CatalogRegistry.Get` returned null for
   every `BaseLayout` row. Seeding alone would have changed nothing. The fixture now hydrates the catalog; the proof
   line `hydrated CatalogRegistry` appears **12 times** in `capman6`.
2. **`building-research:arcane-tower:warding` is an invalid job id.** `warding` is not a perk; the authored id is
   `arcane-warding-runes`. `BuildingPerkService.IsResearching` compares the WHOLE job id, so **the `Researching` state
   was unreachable in every capture ever taken.** Corrected in the fixture; `RESEARCHING NOW` now paints.
3. **The "missing" card descriptions and the wordless Troops badge were neither missing nor a VM fault.** Both labels
   were being created. TMP culls an entire line when its `fontSizeMin` line cannot seat in the rect, and those bands
   were **18.2 px**. The threshold was bracketed from the frames themselves (33.8 px renders, 23.4 px renders, 18.2 px
   does not). Fixed by moving them into the Buildings-proven band.
   **Rail truncation had a fourth cause the CLI did not suspect:** `ApplyOperationalMedievalSkin`'s skip-list omitted
   the two new row prefixes, so those rows went through `MedievalUiSkin.ApplyButton`, which upper-cases the label, adds
   character spacing, swaps to the wide Title face and raises the fit floor from 26 to 30. Adding the prefixes restores
   the Troops treatment - which is why the Troops rail never truncated.

## Deviations, recorded
1. **`AddBuildingNowBand()` is no longer byte-unchanged** (ruling 3.3's letter). It gained a Defense branch so the band
   can resolve a placed-structure job's name and art. `ManageBuildingsCardRegression`'s pinned literal survives verbatim.
2. **The card sub-line was folded into the description band** (`"2 placed . lowest L1 - <desc>"`). Ruling 3.1 said
   "sub-line", not "own band", and no band under 33.8 px is frame-proven on these cards.
3. **Troops' badge and benefit line use different rects from Buildings'**, because Buildings' rects overprint the Troops
   army line. No font was shrunk.
4. **`_browsePage` was deleted** beyond the WO's named deletions - same ruling 3.4, zero readers left.
5. **`DoorLabel` is null for every troop**: there is no troop skill panel in `PanelRouter`, so Troops ships with no
   second door, exactly as ruling 3.5 allows. None was invented.
6. **`[research-locked-visible]` is now pinned in two suites** (the disclosure suite and the new Research suite). The WO
   asked for both; it is duplicated state and may be collapsed to the new suite.

## ⚠ UPDATE 02:59 - three of the gaps below were CLOSED after this file was first written
Kept as written above the line, corrected here rather than rewritten (CLAUDE.md section 15).
1. **The Defense queue band is FIXED** (`5920ea35c`). The cause was not the resolver: `SeedManageCaptureQueue`
   enqueued `tower_ground_archer:7:0`, a COLON shape the live game never produces. `PlacedUpgradeKey.Compose` is the
   only composer in the tree and emits `<itemId>@<cellX>_<cellZ>`; `TryParse` requires that `@` and rejected the colon
   form outright. **That one bad fixture string was hiding THREE correct behaviours**: the band could not resolve a name
   or art, the Archer Tower card read `Upgradable` while its own job ran (`HasPlacedBuilderJob` matches the key exactly),
   and the rail never showed its Building state. All three are right now. There is an unconditional
   `[Flow:Manage] BUILDING NOW band:` trace so the next capture proves it.
2. **The locked Research card is RESHAPED** (`6d0861e41`). On the real device both its half-width faces ellipsized. A
   `CanResearch` reason is an authored SENTENCE and never fits a button, so it now paints as a body text line in
   Parchment and the card carries ONE full-width door. Ruling 3.7's "dead face beside the live door" wording above is
   therefore **STALE** - the ruling's intent (reason verbatim, prerequisite one tap away) is better served, not dropped.
3. **Rail sub-lines lead with the discriminator** (`Locked . Lumber Mill`, not `Lumber Mill . Locked`), so the ellipsis
   eats the shared half.

**STILL OPEN:** the BUILDINGS tab's own queue band resolves `<none>` when its first Builder job is a placed structure -
the new trace says so out loud; and the longest Research names still ellipsize at the 26px floor, which Buildings shares.

## Known gaps, recorded rather than hidden
- **The Defense `BUILDING NOW` band still reads `Tower Ground Archer...` beside an empty medallion** in `capman6`. The
  polish lane added a resolver for it; the frame shows it did not take. Root cause is known and written down:
  `QueueRowVM.BuildingId` is `""` for a placed-structure upgrade, and `Label` falls back to the title-cased
  `PlacedUpgradeKey`. **Not fixed tonight. Ticket it or hand it back.**
- **The longest Research names still ellipsize** (`Expanded Cap...`, `Efficient Smelt...`) at the restored 26 px floor.
  Buildings shares this; it is a name-length property, not a regression.
- **The third rail row is clipped** by ~6 px: 8 padding + 112 + 6 spacing + 112 = 238 of a 244 px viewport. Both knobs
  are frozen by two suites and the 112 px touch floor. Troops has always shown the same sliver.
- Queue-band medallions for non-troop jobs remain the generic circle.

## Open for the owner
1. Storage containers (`lumberyard` / `foundry` / `silo`), `mine_crystal` and `healing_caravan` are listed under
   **DEFENSE** because they carry an upgrade ladder. Pre-existing; deliberately not changed. Move them to Buildings?
2. The Research rail is 17 rows in one flat scroll. Group by building later?
3. The Defense card sub-line wording `"2 placed . lowest L1"`.
4. Art: `docs/ART_REQUEST_2026-09-06_manage_tab_portraits.md` - 62 files, 12 of them re-cuts of art already on disk.

## Absorbed
**WO-1405's remaining Defense half** is superseded by ruling 3.1: the rail is per TYPE, so a grid coordinate never
reaches the player. `BuildCollectionPlayerRegression:124`'s `grid " + placed.cellX` pin stays green because
`BuildDefenseBrowse` is retained.
