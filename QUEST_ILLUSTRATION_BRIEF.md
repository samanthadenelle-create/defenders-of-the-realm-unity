# The 24 quests - illustration brief

**Generated 2026-08-25 from `Assets/Resources/Data/Canonical/quests.json` (version 3, 24 quests,
63 stages).** Every title, objective and reward below is read straight out of that file - none of it
is invented. Regenerate rather than hand-edit if the catalog changes.

## Read this before commissioning anything

- **There is no art field on a quest.** A quest entry carries exactly four keys: `id`, `stages`,
  `title`, `type`. Adding an illustration is a data-schema change as well as an art job.
- **The quest giver is DERIVED, not authored.** Only three "talk to a person" targets exist in the
  whole catalog: the Village Elder, Fenn Wildmane, and the Healing Caravan. Where the column below
  says *(no giver in data)*, the quest is triggered by building, crafting, fighting or opening a
  panel - nobody hands it to you. If a quest should have a face, that is a design decision, not a
  missing record.
- **The end result is the FINAL STAGE plus what it pays.** That is the moment worth illustrating
  for most of these.
- ⚠ **`food` is on its way out.** WO-1163 retires food in favour of stone. Rewards below are quoted
  as the catalog reads today; do not treat a food figure as final.

---

## Quick table

| # | Quest | Type | Given by | Ends with |
|---|---|---|---|---|
| 1 | A New Defender | Main story | the Village Elder | Survive the first wave at the gate |
| 2 | The Forgemaster's Request | Gear / crafting | the Forge (weaponsmith) | Claim the Iron Longsword Borin Emberhand forged - open your pack and take it up |
| 3 | Supply Run | Side quest | the Market | Visit Coppin at the Store and take the supply run |
| 4 | The Last Ember | Gear / crafting | the Forge (weaponsmith) | Field-test the new blade: clear an Orc Raider wave at the west gate |
| 5 | Shields of the Fallen | Gear / crafting | the Armorer | Hold the line: survive a wave in the new armor |
| 6 | Roots Run Deep | Side quest | *(no giver in data)* | Defend the sapling through one night raid |
| 7 | Full Bellies, Full Ranks | Side quest | the Farm | Report the new harvest rota to Mother Wren |
| 8 | Aether's Facet | Side quest | the Jeweler | Decide what to do about Sable and the outside broker |
| 9 | The Glimmer Road | Side quest | the Market | Expand the network until the rumor board opens - Brom will have work waiting |
| 10 | Last Call | Side quest | *(no giver in data)* | Raise a Barracks as the rally and respawn point |
| 11 | Wild Hearts | Side quest | Fenn Wildmane (beast handler / pet house) | Ask the Echo Warden at the Echo Hollow to set your bonded pet to harvest |
| 12 | Rebuild Elarion | Main story | *(no giver in data)* | Rekindle the Heart of Elarion and march on the Warband Deathspeaker |
| 13 | Honest Steel | Main story | the Forge (weaponsmith) | Meet the four crafts of Elarion - the Forge, the Blacksmith, the Lumber Mill, the Mill - and hear Borin Emberhand tell the legend of the broken Aegis of Elarion |
| 14 | The Old Fire | Main story | the Armorer | Take the master's word from Borin Emberhand: the four crafts work as one again, and what was lost can be recovered |
| 15 | What Was Lost | Endgame | the Farm | All four secured - bring them to Mother Wren's table for the reforging |
| 16 | The Reforging | Endgame | *(no giver in data)* | Choose the aether for the quench - draw from the Heart of Elarion, or gather it from the cleansed lands - and reforge the Aegis of Elarion at the Workshop |
| 17 | Wild Hearts: The Green Hearth | Side quest | the Farm | Bond the Flame Pup - its hearth fire quickens what the fields give back |
| 18 | Wild Hearts: The Wounded Wolf | Side quest | the Pet House | Bond the Ice Wolf - its frost hide turns a blow meant for the Heart of Elarion |
| 19 | Wild Hearts: Ice Wolf | Side quest | Fenn Wildmane (beast handler / pet house) | Bond the Ice Wolf - its bite leaves the cold behind in the wound |
| 20 | Wild Hearts: Flame Pup | Side quest | the Pet House | Bond the Flame Pup - its bite sets the enemy alight and the burn lingers |
| 21 | Wild Hearts: The Cleansed Water | Side quest | the Pet House | Bond the Aether Sprite - its light eases what the mire left behind |
| 22 | Wild Hearts: The Flawless Stone | Side quest | the Jeweler | Bond the Aether Sprite - it scents richer crystal than any hand can find |
| 23 | Wild Hearts: The Caged Wolf | Side quest | *(no giver in data)* | The freed Ice Wolf bonds at once - it will carry, and it will cover |
| 24 | Wild Hearts: Aether Sprite | Side quest | the Village Elder | Bond the Aether Sprite - its aura lightens every ability you spend |

---

## Detail

### 1. A New Defender

- **Type:** Main story  ·  **id:** `elarion.welcome`  ·  **stages:** 2
- **Given by:** the Village Elder
- **Total reward:** 150 crystals, 20 food

**What happens:**

1. Speak with the Village Elder at the Heart of Elarion.  *(the Village Elder - talk - pays 50 crystals)*
2. Survive the first wave at the gate.  *(wave - **keystone** - pays 100 crystals, 20 food)*

**Ends with:** Survive the first wave at the gate.

### 2. The Forgemaster's Request

- **Type:** Gear / crafting  ·  **id:** `forgemaster.first-commission`  ·  **stages:** 2
- **Given by:** the Forge (weaponsmith)
- **Total reward:** 10 magic, item `knight_iron`

**What happens:**

1. Bring iron to Borin Emberhand at the Forge.  *(the Forge (weaponsmith) - talk)*
2. Claim the Iron Longsword Borin Emberhand forged - open your pack and take it up.  *(the Inventory screen - panel - pays 10 magic, item `knight_iron`)*

**Ends with:** Claim the Iron Longsword Borin Emberhand forged - open your pack and take it up.

### 3. Supply Run

- **Type:** Side quest  ·  **id:** `vendor.supply-run`  ·  **stages:** 1
- **Given by:** the Market
- **Total reward:** 25 crystals

**What happens:**

1. Visit Coppin at the Store and take the supply run.  *(the Market - talk - pays 25 crystals)*

**Ends with:** Visit Coppin at the Store and take the supply run.

### 4. The Last Ember

- **Type:** Gear / crafting  ·  **id:** `vendor.forge`  ·  **stages:** 3
- **Given by:** the Forge (weaponsmith)
- **Total reward:** 75 crystals, 10 magic

**What happens:**

1. Bring Borin Emberhand wood and iron to relight the forge.  *(the Forge (weaponsmith) - talk)*
2. Raise a Crystal Mine and bring Borin Emberhand a flawless stone to quench the first true blade.  *(the Crystal Mine - build - pays 10 magic)*
3. Field-test the new blade: clear an Orc Raider wave at the west gate.  *(wave - **keystone** - pays 75 crystals)*

**Ends with:** Field-test the new blade: clear an Orc Raider wave at the west gate.

### 5. Shields of the Fallen

- **Type:** Gear / crafting  ·  **id:** `vendor.armorer`  ·  **stages:** 3
- **Given by:** the Armorer
- **Total reward:** 75 crystals

**What happens:**

1. Recover garrison salvage - win an encounter beyond the walls.  *(arena)*
2. Reforge the plate with Halvard at the Blacksmith.  *(the Armorer - talk)*
3. Hold the line: survive a wave in the new armor.  *(wave - **keystone** - pays 75 crystals)*

**Ends with:** Hold the line: survive a wave in the new armor.

### 6. Roots Run Deep

- **Type:** Side quest  ·  **id:** `vendor.lumbermill`  ·  **stages:** 3
- **Given by:** *(no giver in data)* - triggered by arena/build/wave, not by a person
- **Total reward:** 50 crystals, 30 food

**What happens:**

1. Clear the blight from Old Pell's grove at the edge of The Thornwood.  *(arena)*
2. Carry a sapling from the Heart of Elarion, plant it, and raise a Lumber Mill to tend the new growth.  *(the Lumber Mill - build)*
3. Defend the sapling through one night raid.  *(wave - **keystone** - pays 50 crystals, 30 food)*

**Ends with:** Defend the sapling through one night raid.

### 7. Full Bellies, Full Ranks

- **Type:** Side quest  ·  **id:** `vendor.granary`  ·  **stages:** 3
- **Given by:** the Farm
- **Total reward:** 50 crystals, 90 food

**What happens:**

1. Restore food flow: open the upgrade for Mother Wren's Mill and raise what it yields.  *(the Upgrade panel - panel - pays 40 food)*
2. Grow the stores so the ranks can grow: raise a Silo.  *(the Silo - build)*
3. Report the new harvest rota to Mother Wren.  *(the Farm - talk - **keystone** - pays 50 crystals, 50 food)*

**Ends with:** Report the new harvest rota to Mother Wren.

### 8. Aether's Facet

- **Type:** Side quest  ·  **id:** `vendor.jeweler`  ·  **stages:** 3
- **Given by:** the Jeweler
- **Total reward:** 75 crystals, 15 magic

**What happens:**

1. Raise a Crystal Mine and harvest a rare stone before the seam fades.  *(the Crystal Mine - build)*
2. Cut your first gem at the Jeweler bench and socket it.  *(the Jeweler bench - panel - pays 15 magic)*
3. Decide what to do about Sable and the outside broker.  *(the Jeweler - talk - **keystone** - pays 75 crystals)*

**Ends with:** Decide what to do about Sable and the outside broker.

### 9. The Glimmer Road

- **Type:** Side quest  ·  **id:** `vendor.market`  ·  **stages:** 3
- **Given by:** the Market
- **Total reward:** 125 crystals

**What happens:**

1. Clear the trade road to the next outpost - win an encounter beyond the walls.  *(arena)*
2. Establish a steady trade route with Coppin for a trickle of crystals.  *(the Market - talk - pays 50 crystals)*
3. Expand the network until the rumor board opens - Brom will have work waiting.  *(Brom's Rumor Board - panel - **keystone** - pays 75 crystals)*

**Ends with:** Expand the network until the rumor board opens - Brom will have work waiting.

### 10. Last Call

- **Type:** Side quest  ·  **id:** `vendor.inn`  ·  **stages:** 2
- **Given by:** *(no giver in data)* - triggered by build/wave, not by a person
- **Total reward:** 100 crystals, 30 food

**What happens:**

1. Defend the hall through a surprise raid.  *(wave - pays 50 crystals, 30 food)*
2. Raise a Barracks as the rally and respawn point.  *(the Barracks - build - **keystone** - pays 50 crystals)*

**Ends with:** Raise a Barracks as the rally and respawn point.

### 11. Wild Hearts

- **Type:** Side quest  ·  **id:** `vendor.stable`  ·  **stages:** 3
- **Given by:** Fenn Wildmane (beast handler / pet house)
- **Total reward:** 50 crystals

**What happens:**

1. Track a wild echo beyond the walls and win its measure.  *(arena)*
2. Train a pet ability with Fenn Wildmane.  *(Fenn Wildmane (beast handler / pet house) - talk)*
3. Ask the Echo Warden at the Echo Hollow to set your bonded pet to harvest.  *(the Pet House - talk - **keystone** - pays 50 crystals)*

**Ends with:** Ask the Echo Warden at the Echo Hollow to set your bonded pet to harvest.

### 12. Rebuild Elarion

- **Type:** Main story  ·  **id:** `vendor.steward`  ·  **stages:** 4
- **Given by:** *(no giver in data)* - triggered by arena/build, not by a person
- **Total reward:** 250 crystals, 50 magic

**What happens:**

1. Raise Elarion to its second tier: raise a Silo so the stores can hold a season.  *(the Silo - build)*
2. Re-arm the walls: raise a Stone Wall on the breach.  *(the Stone Wall - build)*
3. Rekindle the wards: raise the Healing Caravan.  *(the Healing Caravan - build)*
4. Rekindle the Heart of Elarion and march on the Warband Deathspeaker.  *(arena - **keystone** - pays 250 crystals, 50 magic)*

**Ends with:** Rekindle the Heart of Elarion and march on the Warband Deathspeaker.

### 13. Honest Steel

- **Type:** Main story  ·  **id:** `forgemasters_act1`  ·  **stages:** 1
- **Given by:** the Forge (weaponsmith)
- **Total reward:** none

**What happens:**

1. Meet the four crafts of Elarion - the Forge, the Blacksmith, the Lumber Mill, the Mill - and hear Borin Emberhand tell the legend of the broken Aegis of Elarion.  *(the Forge (weaponsmith) - talk)*

**Ends with:** Meet the four crafts of Elarion - the Forge, the Blacksmith, the Lumber Mill, the Mill - and hear Borin Emberhand tell the legend of the broken Aegis of Elarion.

### 14. The Old Fire

- **Type:** Main story  ·  **id:** `forgemasters_act2`  ·  **stages:** 4
- **Given by:** the Armorer
- **Total reward:** 100 crystals, 60 food, 25 magic

**What happens:**

1. Hear Halvard's side of the old quarrel at the Blacksmith - the truth of the fallen Aegis of Elarion.  *(the Armorer - talk)*
2. Reconcile Old Pell and the forge - prove the bough is cut to defend the Heart of Elarion, not to bleed it.  *(the Lumber Mill - talk)*
3. Gather all four to Mother Wren's table for one shared meal - the night the wound closes.  *(the Farm - talk - pays 100 crystals, 60 food)*
4. Take the master's word from Borin Emberhand: the four crafts work as one again, and what was lost can be recovered.  *(the Forge (weaponsmith) - talk - **keystone** - pays 25 magic)*

**Ends with:** Take the master's word from Borin Emberhand: the four crafts work as one again, and what was lost can be recovered.

### 15. What Was Lost

- **Type:** Endgame  ·  **id:** `forgemasters_act3`  ·  **stages:** 2
- **Given by:** the Farm
- **Total reward:** 150 crystals, 50 magic

**What happens:**

1. Recover the four scattered techniques - the threefold fold of The Starfall Reach, the oathweld of Hollowfrost Vale, the heartwood bough of The Thornwood, the last pressing of The Emberwastes. Win four encounters beyond the walls to bring them home.  *(arena x4)*
2. All four secured - bring them to Mother Wren's table for the reforging.  *(the Farm - talk - **keystone** - pays 150 crystals, 50 magic)*

**Ends with:** All four secured - bring them to Mother Wren's table for the reforging.

### 16. The Reforging

- **Type:** Endgame  ·  **id:** `forgemasters_act4`  ·  **stages:** 1
- **Given by:** *(no giver in data)* - triggered by panel, not by a person
- **Total reward:** 300 crystals, 100 magic

**What happens:**

1. Choose the aether for the quench - draw from the Heart of Elarion, or gather it from the cleansed lands - and reforge the Aegis of Elarion at the Workshop.  *(the Crafting bench - panel - **keystone** - pays 300 crystals, 100 magic)*

**Ends with:** Choose the aether for the quench - draw from the Heart of Elarion, or gather it from the cleansed lands - and reforge the Aegis of Elarion at the Workshop.

### 17. Wild Hearts: The Green Hearth

- **Type:** Side quest  ·  **id:** `petbond.sproutling`  ·  **stages:** 3
- **Given by:** the Farm
- **Total reward:** 30 food

**What happens:**

1. Cleanse a blighted harvest site at the edge of The Thornwood and leave an offering.  *(arena)*
2. Walk the Flame Pup home past Mother Wren's fields and let the bond set.  *(the Farm - talk)*
3. Bond the Flame Pup - its hearth fire quickens what the fields give back.  *(the Flame Pup - pet - pays 30 food)*

**Ends with:** Bond the Flame Pup - its hearth fire quickens what the fields give back.

### 18. Wild Hearts: The Wounded Wolf

- **Type:** Side quest  ·  **id:** `petbond.craghound`  ·  **stages:** 3
- **Given by:** the Pet House
- **Total reward:** 40 crystals

**What happens:**

1. Protect a wounded wolf through a raid at the walls.  *(wave)*
2. Bring the Ice Wolf home through the west gate to the Echo Hollow.  *(the Pet House - talk)*
3. Bond the Ice Wolf - its frost hide turns a blow meant for the Heart of Elarion.  *(the Ice Wolf - pet - pays 40 crystals)*

**Ends with:** Bond the Ice Wolf - its frost hide turns a blow meant for the Heart of Elarion.

### 19. Wild Hearts: Ice Wolf

- **Type:** Side quest  ·  **id:** `petbond.frostkit`  ·  **stages:** 3
- **Given by:** Fenn Wildmane (beast handler / pet house)
- **Total reward:** 40 crystals

**What happens:**

1. Make a slow, patient approach on the cold ground below Hollowfrost Vale.  *(arena)*
2. Bring the Ice Wolf down out of the cold - Fenn Wildmane will make it a warm stall.  *(Fenn Wildmane (beast handler / pet house) - talk)*
3. Bond the Ice Wolf - its bite leaves the cold behind in the wound.  *(the Ice Wolf - pet - pays 40 crystals)*

**Ends with:** Bond the Ice Wolf - its bite leaves the cold behind in the wound.

### 20. Wild Hearts: Flame Pup

- **Type:** Side quest  ·  **id:** `petbond.emberpup`  ·  **stages:** 3
- **Given by:** the Pet House
- **Total reward:** 40 crystals

**What happens:**

1. Stand in the worst heat of The Emberwastes beside the Flame Pup.  *(arena)*
2. Bring the Flame Pup home to settle at the Echo Hollow.  *(the Pet House - talk)*
3. Bond the Flame Pup - its bite sets the enemy alight and the burn lingers.  *(the Flame Pup - pet - pays 40 crystals)*

**Ends with:** Bond the Flame Pup - its bite sets the enemy alight and the burn lingers.

### 21. Wild Hearts: The Cleansed Water

- **Type:** Side quest  ·  **id:** `petbond.mirewing`  ·  **stages:** 3
- **Given by:** the Pet House
- **Total reward:** 15 magic

**What happens:**

1. Cleanse the poisoned water of The Mirewood at its source.  *(arena)*
2. Bring the Aether Sprite home to roost at the Echo Hollow.  *(the Pet House - talk)*
3. Bond the Aether Sprite - its light eases what the mire left behind.  *(the Aether Sprite - pet - pays 15 magic)*

**Ends with:** Bond the Aether Sprite - its light eases what the mire left behind.

### 22. Wild Hearts: The Flawless Stone

- **Type:** Side quest  ·  **id:** `petbond.glimmermoth`  ·  **stages:** 3
- **Given by:** the Jeweler
- **Total reward:** 60 crystals

**What happens:**

1. Obtain a flawless stone from Sable at the Jeweler.  *(the Jeweler - talk)*
2. Raise a Crystal Mine and coax the Aether Sprite to the new seam with the flawless stone.  *(the Crystal Mine - build)*
3. Bond the Aether Sprite - it scents richer crystal than any hand can find.  *(the Aether Sprite - pet - pays 60 crystals)*

**Ends with:** Bond the Aether Sprite - it scents richer crystal than any hand can find.

### 23. Wild Hearts: The Caged Wolf

- **Type:** Side quest  ·  **id:** `petbond.stoneback`  ·  **stages:** 2
- **Given by:** *(no giver in data)* - triggered by arena/pet, not by a person
- **Total reward:** 50 crystals

**What happens:**

1. Clear an Orc Raider camp beyond the walls and free the caged wolf.  *(arena)*
2. The freed Ice Wolf bonds at once - it will carry, and it will cover.  *(the Ice Wolf - pet - pays 50 crystals)*

**Ends with:** The freed Ice Wolf bonds at once - it will carry, and it will cover.

### 24. Wild Hearts: Aether Sprite

- **Type:** Side quest  ·  **id:** `petbond.aetherfox`  ·  **stages:** 3
- **Given by:** the Village Elder
- **Total reward:** 100 crystals, 50 magic

**What happens:**

1. Hold the restored ground in the light of the Heart of Elarion and let the Aether Sprite find you worthy.  *(wave)*
2. Bring the Aether Sprite to the Village Elder at the Heart of Elarion.  *(the Village Elder - talk)*
3. Bond the Aether Sprite - its aura lightens every ability you spend.  *(the Aether Sprite - pet - pays 100 crystals, 50 magic)*

**Ends with:** Bond the Aether Sprite - its aura lightens every ability you spend.

---

## Summary for the art brief

- **4 main story**, **15 side**, **3 gear/crafting**, **2 endgame** = 24 total.
- **19 of 24 have a named giver in data**; the other 5 are triggered by an action.
- Keystone stages (story beats the catalog marks as significant): 13.

⭐ **If per-quest art is too many pieces, the cheap axis is `type`** - four plates instead of
twenty-four, reused across every quest of a kind. That is the order-of-magnitude decision.
