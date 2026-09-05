# Production readiness triage - 2026-09-05 10:15 (owner asked: "can we push to production")

Feature freeze in force from 2026-09-05 ~10:05 (owner: "nothing new just bug fixes and stability now").
Every line below is evidence read this session. Nothing here is inferred.

## VERDICT: NO. One blocker, proven on your device 15 minutes ago.

**A new game is broken three ways at once, and they are one chain.** Build 2026.09.05.356468,
installed on SM02G4061955851 at 09:57:38.

| # | What the device shows | Evidence |
|---|---|---|
| 1 | START NEW opens the welcome-back popup claiming **"YOUR REALM WORKED FOR 8h 22m"** with +11520 wood / +6912 iron / +15000 stone. A second new game said 1h 56m. 8h22m is exactly the wall time since your previous session, so the window came from the OLD save's claim stamp. | `logs/f8-inbox/device/SM02G4061955851/break_01_error.png` |
| 2 | That popup's panel sits over the tutorial SKIP control, so the founding dialogue never ends: `STEP-STUCK :: founding_greet - no 'dialogue.ended:tut_founding_greet' after 120s ... RESCUED via watchdog and recorded as SKIPPED`. **The first-run tutorial is silently skipped on every new game.** | F8 seq 4681 + 4682 |
| 3 | The fresh town inherited the ever-built ledger, so it pays buildings that do not exist: `'farm' is in the ever-built ledger but NO ResourceCollector is registered - 13 Food HELD` and the same for `lumbermill`, every 10 s from 10:04:55 to 10:06:25. The town says it is producing and nothing ever banks. | device logcat |

This is the FIFTH instance of the shape `GameStateService.cs:1543-1547` already names by number
(WO-860 equip, WO-1019 hot-swap bar, WO-1220 talents, WO-1371 collector prefs): state that lives
outside the save envelope, or in memory, that `ResetToNewGame` has never heard of.

**Ticket: WO-1414** (READY, dispatched 10:12). Fix lands, build, you re-test START NEW, then we
talk about production again. Nothing else should go in ahead of it.

## The stone collector question, answered from the catalog

You said: "we need to put a stone collector instead". At source, **the building you mean already
exists and has three different identities**:

| Where | What it says |
|---|---|
| catalog id | `collector_farm` |
| catalog display name | **"Quarry"** (`structures-catalog.json`) |
| the code's role map | `BuildingType.Farm => RoleWord(StructureRole.FoodProducer)` - **Food** (`BuildingInteractable.cs:719`) |
| the welcome-back popup | **Stone** (+15000 on your screenshot) |
| the harvest tick | **Food** (`'farm' ... 13 Food HELD`) |

So one building is named Quarry, pays Food in two places and Stone in a third. That is a data
defect, not a missing feature - and it is exactly the `collector_farm` naming question that has
been parked awaiting your word since 2026-09-04.

**Art we already have for a stone collector** (searched by token, not by name):
- `Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/.../buildings/{blue,green,red,yellow}/building_mine_*.fbx` - a mine building, four colourways, already in the tree.
- `Assets/StructureContent/IronMine.fbx` and `CrystalMine.fbx` - the two mines already shipped as buildings.
- `Assets/Prefabs/Village/Generated/Building_crystal-mine.prefab`.
- There is **no** model authored as a quarry. The closest honest choice is the KayKit mine building or a re-skin of IronMine.

**Ruling needed (one word each):**
1. Does the Quarry pay **stone** (rename the role away from Food) or **food** (rename the building back to Farm)? The three producers must agree.
2. If stone: use the **KayKit mine** model, or re-skin **IronMine**?

## What is already on your phone and worth felt-testing while WO-1414 is fixed

Build 356468 carries 20 fixed tickets from tonight. The ones that need your eyes:
Journey now has five cards (Quests, Raids, Dungeons, Realm Map, Season); Heartfire is the ONE raid
gate; the Cathedral upgrade page names its shortfall; harvest never burns; the queue drawer and the
close-frame tap; Builder's Hour in the Night Market; the post-first-raid beat; the Wardrobe card.
Full list with felt-test lines: `docs/HANDOVER_2026-09-05_overnight.md`.

## What I stopped, per the freeze

Two finished-but-unshipped feature lanes were REVERTED out of the tree at 10:04 so the production
candidate is exactly what you are holding: WO-1402 (spoils estimate on raid rows) and WO-1407 (HUD
"how to become raid-capable" line). Both are saved as patches and can land after the freeze;
WO-1407 also redded an architecture pin, which is a second reason it is not in.
WO-1403 (empty-army deploy door) died mid-edit on a session limit and is parked.
