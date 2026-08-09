# WORK ORDER 934 — Army loadout bank (3 presets + persist + muster polish)

**Status:** IMPLEMENTED  
**Save schema:** v38  

## Intent
Player can author a few army loadouts, save them, and one-tap muster (auto-queue) training — fun prep loop before raids.

## Delivered
- 3 named slots on `ArmyStorage` (Raid Push / Wall Hold / Siege Prep defaults)
- Quick-fill recipes (Raid / Hold / Siege / Clear)
- Save slot + auto-save on muster / slot switch
- Barracks **Armies** button entry
- `ArmyLoadoutService` + polished `ArmyMusterPanel`
- Regression loadout bank checks; CORE_SAVE v38 migrator

## Player loop
Barracks → Armies → pick slot → recipe or [+] troops → Save → Muster → Train queue fills → Raids when army full.
