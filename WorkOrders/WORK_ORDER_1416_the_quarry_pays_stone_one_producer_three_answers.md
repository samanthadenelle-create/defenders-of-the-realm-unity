# WO-1416: the Quarry pays Stone - one building currently gives three different answers

**Status:** FIXED - ON THE SEEKER in build 2026.09.06.357453 (chain 00:31-00:38: APK_OK 463MB, R2_PARITY_OK objects=271; installed 00:41, versionCode 357453 read off dumpsys; Firebase App Distribution release 0kka4h6t9u400); owner felt-test closes 2026-09-05 21:45 - code + guide + canon-strings landed, oracles moved with the ruling, COMPILE_GATE_OK + REGRESSION_OK 385/385; art question open (RESULT file); device build tonight, owner felt-test closes. *(was: READY TO IMPLEMENT - OWNER RULED 2026-09-05: "quarry pays stone")*

## Owner, verbatim (2026-09-05 10:1x-10:2x)
> "we need to put a stone collector instead" ... "can you look in the assets and see if we have
> something?" ... **"quarry pays stone"**

## Evidence (read at source this session)
One building, four places, three different answers:

| where | what it says |
|---|---|
| catalog id | `collector_farm` |
| catalog display name | **"Quarry"** (`Assets/Resources/Data/Canonical/structures-catalog.json`) |
| the code's role map | `BuildingType.Farm => RoleWord(StructureRole.FoodProducer)` = **Food** (`Assets/_Modules/Village/Buildings/BuildingInteractable.cs:719`) |
| the live harvest tick (device, 10:04-10:06) | `'farm' is in the ever-built ledger ... 13 **Food** HELD` |
| the welcome-back popup (device, 09:57) | `**STONE** WAITING +15000` |

So the building the player reads as a Quarry pays Food in the role map and the harvest tick, and
Stone in the offline summary. The owner's ruling settles which is right: **Stone.**

## The ruling
**The Quarry pays STONE.** Every producer agrees, or it is not done: the role map, the harvest
tick, the offline summary, the building's own info panel, and any copy that names its yield.

## Fix shape
1. `BuildingInteractable.cs:719` and whatever else maps `BuildingType.Farm` to a food role move to
   the stone role. ⛔ **The catalog id `collector_farm` is a LIVE SAVE KEY - do not rename it**
   (memory `structure-role-enum-and-format-normalization`; the same law that kept WO-1161 additive).
   The id stays; the ROLE and the RESOURCE change.
2. One producer for "what this building yields", read by the role word, the harvest tick and the
   offline summary - so the three can never disagree again. If two code paths compute it today,
   the second one goes.
3. Copy: the display name is already "Quarry". Any string that still says Farm for this id goes.
4. If Food loses its only producer by this change, SAY SO in the RESULT and stop - whether Food
   needs its own building is a separate owner decision, not this ticket's to make.

## Art (searched by token, reported to the owner 2026-09-05)
No model is authored as a quarry. What exists in the tree today:
- `Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx/buildings/{blue,green,red,yellow}/building_mine_*.fbx`
- `Assets/StructureContent/IronMine.fbx`, `Assets/StructureContent/CrystalMine.fbx`
- `Assets/Prefabs/Village/Generated/Building_crystal-mine.prefab`
**Owner ruling still open:** use the KayKit mine model, or re-skin IronMine? The current row also
carries the `repo.maxFootprint 5.6` carve-out documented in `structures-catalog.json` - read that
note before touching the model, it is the fix for the giant-farm defect of 2026-08-20.

## Acceptance
- [ ] RED-first pin: for `collector_farm`, the role word, the harvest tick's resource and the
      offline summary's resource are all Stone; name the one-line mutation.
- [ ] No `.json` or `.cs` player-facing string calls this building a Farm.
- [ ] Device: build one, watch it accrue, collect - stone moves and nothing else does.

## Not in scope
The New Game inheritance defect (WO-1414 - that is why the device showed HELD lines at all), the
model swap (blocked on the art ruling above), Food's future.
