# WO-1416 RESULT - the Quarry pays Stone: one building, one answer

**Status:** FIXED 2026-09-05 (gated on the combined evening tree; device build follows tonight)
**Owner ruling:** "quarry pays stone" (2026-09-05).

## What landed (by explicit path)
- `Assets/_Modules/Core/Catalog/StructureRole.cs` - `FoodProducer` deleted, `StoneProducer = "stone_producer"` (the role the catalog row already claimed).
- `Assets/_Modules/Village/Buildings/BuildingInteractable.cs`, `NPCs/CastleVendorNpcInjector.cs`, `Buildings/Progression/ResourceBuildingProgression.cs`, `ResourceBuildingHarvester.cs` - tile word, NPC label, harvest tick all read the ONE role row / `LabelFor` ("Stone"), plus a `[Flow:Harvest] yield map` trace.
- `Assets/Resources/Data/Canonical/structures-catalog.json` + StreamingAssets twin - `_quarryNote` records the ruling and the live-save-key law (`collector_farm` and `HarvestResource.Food` never move).
- `guide-content.json` (both twins) - the `building_farm` entry (id kept) now titles "Quarry" and speaks Stone; the only measured Stone sink is Barracks levels (`barracks.json:27-29` costs `food` 80/290/860/2040), so the copy names that, not "gear and upkeep".
- `canon-strings.json` (both twins) - key `farm` (a live id: `ResourceBuildingProgression.cs:173`, `BuildPaletteUI.cs:1562`, `ModifierService.cs:80`) keeps its key, value "Farm" -> "Quarry"; displayed by `EchoCardVM.NeededBuildingDisplayName` (`:485`) as the NEEDS chip.
- Oracles moved WITH the ruling: `EchoResourcePickerRegression.cs` now reads the stone-producer row's DisplayName off the role table (no literal); `RetiredVocabularyRegression.cs` guide tips debt 2 -> 1.
- `CollectorIncomeRegression` Case16 `[quarry-pays-stone]` (RED-first) pins the code side.

## Evidence
- JSON edits binary-safe: guide-content LF/CR 531/531 unchanged, canon-strings 411/411 unchanged, no BOM, twins SHA-256 identical (`145FDA58...`, `12131F52...`), all four parse.
- `COMPILE_GATE_OK` (`Builds/c3`, 21:43, 0 `error CS`); `REGRESSION_OK 385/385 suites` (`Builds/r3`, 21:45) incl. `[collector-income]`, `[echo-picker]`, `[retired-vocabulary]` green.

## Left open (named, not hidden)
- `Assets/Editor/VillageSceneBuilder.Content.cs:441` still authors `Label = "Farm"` for the deleted `Village.unity` builder (dead scene; not player-facing today). Stale comments in `ResourceBuildingProgression.cs:140`, `BuildingSignInjector.cs:3`, `CastleVendorNpcInjector.cs:99`.
- Art: the Quarry still uses the farm visual - owner ruling pending (READY_SILOS #20 / rulings list).
- Device felt-test closes the ticket.
