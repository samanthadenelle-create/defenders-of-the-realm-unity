# Quest Image Briefs

**Date:** 2026-08-25
**For:** the owner (art commissioning / image generation)
**Scope:** every authored quest in the shipping data, numbered, with giver, synopsis, end result, and one plain-language description of what the image should show.

## Where this came from

- `Assets/StreamingAssets/Data/Canonical/quests.json` -- 24 authored quests, 63 stages.
- `Assets/Resources/Data/Canonical/quests.json` -- the canonical twin. **The two copies are byte-identical** (35,402 bytes each). No mirror-law defect.
- `Assets/StreamingAssets/Data/Canonical/daily-quests.json` + its twin -- also byte-identical (8,103 bytes each). Daily quests are procedural templates, not authored quests; see the appendix.
- Speaker roles read from `Assets/StreamingAssets/Data/Canonical/dialogue/dialogues.json` (`speakers[]`).
- Board framing confirmed against `Assets/_Modules/Village/Hero/RumorBoardVM.cs`.

**Nothing below is invented.** Every title, objective and reward is lifted from `quests.json`.

## Two things to know before you read

**1. There is no `giver` field in the quest data.** `quests.json` stores id, type, title, stages, and an optional prerequisite -- that is all. The giver named on each row below is the NPC the quest's own objective text names by name (for example "Bring iron to Borin Emberhand at the Forge"). Where no person is named in the text, the row says `not authored` rather than guessing. The rumor board itself is titled "Brom's Rumor Board" and Brom is the Town Crier -- he is the board, not the giver of any individual quest.

**2. This document briefs one image per quest, and that is not currently the ruling.** `FOUNDATIONAL_RULINGS.md` section 11 ("ONE QUEST ILLUSTRATION PER QUESTLINE", owner 2026-08-25) rules a shared illustration per chapter / questline, reused across every quest inside it, and explicitly names one-image-per-quest as the option not chosen. This document supplies a per-quest brief anyway, because that is what was asked for, and because the per-quest briefs also reveal which quests would happily share one illustration. **Which way it goes is the owner's to settle; nothing here resolves it.** The near-duplicate list at the end is the practical bridge -- each cluster there is a candidate for one shared questline image.

---

## Group A -- The main story spine (type: `main`)

### 1. A New Defender
- **DATA ID:** `elarion.welcome`
- **GIVER:** the Village Elder (affiliation: Heart of Elarion)
- **SYNOPSIS:** The opening quest. Speak with the Village Elder at the great tree, then survive your first wave at the gate.
- **END RESULT:** 150 crystals, 20 food, and the first keystone. The player has met the Elder and held one wave.
- **IMAGE:** An old robed elder stands at the foot of a huge living tree and points a young armored newcomer toward the town gate.

### 2. Rebuild Elarion
- **DATA ID:** `vendor.steward`
- **GIVER:** not authored (the quest id implies a steward, but no person is named in any stage text)
- **SYNOPSIS:** Raise the town to its second tier -- a Silo, a stone wall on the breach, the Healing Caravan -- then rekindle the Heart and march on the Warband Deathspeaker.
- **END RESULT:** 250 crystals, 50 magic, a keystone. The town is rebuilt and the Deathspeaker is beaten.
- **IMAGE:** A town official unrolls a large building plan across a barrel while workers behind him haul stone up onto a broken wall.

### 3. Honest Steel
- **DATA ID:** `forgemasters_act1` (opens the four-act Forgemasters chain)
- **GIVER:** Borin Emberhand (the Forge)
- **SYNOPSIS:** Meet the four crafts of the town -- Forge, Blacksmith, Lumber Mill, Mill -- and hear the smith tell the legend of the broken Aegis.
- **END RESULT:** no reward authored on this stage. It unlocks quest 4.
- **IMAGE:** An old smith holds up the two halves of a broken shield to a small circle of listening villagers.
- **NOTE:** near-identical subject to quest 4 -- see the duplicate list.

### 4. The Old Fire
- **DATA ID:** `forgemasters_act2` (requires `forgemasters_act1`)
- **GIVER:** Halvard (Armorer's Hall) opens it; Borin Emberhand (the Forge) closes it
- **SYNOPSIS:** Hear the armorer's side of an old quarrel, reconcile the woodcutter with the forge, then gather all four crafts to one shared meal.
- **END RESULT:** 100 crystals, 60 food, 25 magic, a keystone. The four crafts work as one again.
- **IMAGE:** Two old craftsmen argue across an anvil while an older farm woman calmly sets bowls out on a long table between them.

---

## Group B -- The Forgemasters endgame (type: `endgame`)

### 5. What Was Lost
- **DATA ID:** `forgemasters_act3` (requires `forgemasters_act2`)
- **GIVER:** not authored (the quest ends at Mother Wren's table, but no one is named as sending you)
- **SYNOPSIS:** Recover four scattered smithing techniques from four regions -- four won encounters beyond the walls -- and bring them home.
- **END RESULT:** 150 crystals, 50 magic, a keystone. All four techniques secured for the reforging.
- **IMAGE:** A travel-worn fighter lays four cloth-wrapped bundles out on a farmhouse table in front of a group of craftspeople.

### 6. The Reforging
- **DATA ID:** `forgemasters_act4` (requires `forgemasters_act3`)
- **GIVER:** not authored
- **SYNOPSIS:** Choose where the quenching aether comes from -- the Heart, or the cleansed lands -- and reforge the Aegis of Elarion at the Workshop.
- **END RESULT:** 300 crystals, 100 magic, a keystone. The Aegis of Elarion is whole again. This is the largest single payout in the file.
- **IMAGE:** A smith brings a hammer down on a shield laid across an anvil, sparks scattering, with two onlookers watching from behind him.
- **NOTE:** near-identical subject to quests 3, 7 and 8 -- see the duplicate list.

---

## Group C -- Gear questlines (type: `gear`)

### 7. The Forgemaster's Request
- **DATA ID:** `forgemaster.first-commission`
- **GIVER:** Borin Emberhand (the Forge)
- **SYNOPSIS:** Bring the forgemaster iron, then open your pack and take up the sword he made from it.
- **END RESULT:** the Iron Longsword (`knight_iron`) and 10 magic. The player's first real weapon.
- **IMAGE:** An old smith hands a newly forged longsword hilt-first across an anvil to a young armored fighter.

### 8. The Last Ember
- **DATA ID:** `vendor.forge`
- **GIVER:** Borin Emberhand (the Forge)
- **SYNOPSIS:** Bring wood and iron to relight a dead forge, raise a Crystal Mine for a flawless stone to quench the blade, then field-test it against an orc wave at the west gate.
- **END RESULT:** 10 magic, 75 crystals, a keystone. The forge is running and the first true blade is proven.
- **IMAGE:** A smith and a young helper heave together on a great bellows handle, coaxing the first flame back into a cold forge.

### 9. Shields of the Fallen
- **DATA ID:** `vendor.armorer`
- **GIVER:** Halvard (Armorer's Hall)
- **SYNOPSIS:** Recover garrison salvage beyond the walls, have the armorer reforge the plate, then survive a wave wearing it.
- **END RESULT:** 75 crystals and a keystone. The player is wearing armor made from the fallen garrison's plate.
- **IMAGE:** An armorer hammers the dents out of a battered breastplate mounted on a wooden stand.

---

## Group D -- Village craft and vendor side quests (type: `side`)

### 10. Supply Run
- **DATA ID:** `vendor.supply-run`
- **GIVER:** Coppin (the Marketplace)
- **SYNOPSIS:** Visit the storekeeper and take on a supply run. Single stage -- the shortest quest in the file.
- **END RESULT:** 25 crystals.
- **IMAGE:** A market shopkeeper hands a loaded crate over his stall counter to a traveler with a pack.

### 11. Roots Run Deep
- **DATA ID:** `vendor.lumbermill`
- **GIVER:** Old Pell (the Lumbermill)
- **SYNOPSIS:** Clear the blight from the woodcutter's grove, carry a sapling out from the Heart and plant it, raise a Lumber Mill over it, then defend it through a night raid.
- **END RESULT:** 50 crystals, 30 food, a keystone. A Lumber Mill standing and a surviving sapling.
- **IMAGE:** An old woodcutter kneels to press a young sapling into cleared earth, his axe laid on the ground beside him.

### 12. Full Bellies, Full Ranks
- **DATA ID:** `vendor.granary`
- **GIVER:** Mother Wren (the Windmill)
- **SYNOPSIS:** Upgrade the mill so it yields more, raise a Silo so the stores can hold a season, then report the new harvest rota back.
- **END RESULT:** 90 food, 50 crystals, a keystone. Food flow restored and storage raised.
- **IMAGE:** An older farm woman stands in a windmill doorway hauling a full grain sack onto her shoulder, more sacks stacked around her.

### 13. Aether's Facet
- **DATA ID:** `vendor.jeweler`
- **GIVER:** Sable (the Jeweler's Bench)
- **SYNOPSIS:** Raise a Crystal Mine and take a rare stone before the seam fades, cut your first gem and socket it, then decide what to do about the jeweler's outside broker.
- **END RESULT:** 15 magic, 75 crystals, a keystone. A socketed gem and a decision made about the broker.
- **IMAGE:** A jeweler bends low over a workbench, turning a gemstone against a fine cutting tool.

### 14. The Glimmer Road
- **DATA ID:** `vendor.market`
- **GIVER:** Coppin (the Marketplace)
- **SYNOPSIS:** Clear the trade road to the next outpost, set up a steady route for a trickle of crystals, and expand the network until the rumor board opens.
- **END RESULT:** 125 crystals and a keystone. A working trade route, and Brom's rumor board open for business.
- **IMAGE:** A merchant and a traveler clasp hands over a loaded pack-cart on an open road.

### 15. Last Call
- **DATA ID:** `vendor.inn`
- **GIVER:** not authored
- **SYNOPSIS:** Defend the hall through a surprise raid, then raise a Barracks as the town's rally and respawn point.
- **END RESULT:** 100 crystals and a keystone. A Barracks standing as the rally point.
- **IMAGE:** Villagers shove tables against a tavern door to barricade it while an armored fighter waits beside it with a drawn sword.

### 16. Wild Hearts
- **DATA ID:** `vendor.stable`
- **GIVER:** Fenn Wildmane (the Echo Hollow), with the Echo Warden closing it
- **SYNOPSIS:** Track a wild echo beyond the walls and win its measure, train a companion ability, then set the bonded companion to harvest work.
- **END RESULT:** 50 crystals and a keystone. A bonded companion put to work harvesting.
- **IMAGE:** A beast-handler kneels outside a stable, feeding a young wolf from her open hand.

---

## Group E -- The Wild Hearts bonding quests (type: `side`)

Eight quests, one per bond. The data folds them all onto the three companion species that actually ship (ice wolf, flame pup, aether sprite), which is why several of these produce near-identical pictures -- flagged individually below and again at the end.

### 17. Wild Hearts: The Green Hearth
- **DATA ID:** `petbond.sproutling`
- **GIVER:** not authored (the second stage passes Mother Wren's fields, but she does not send you)
- **SYNOPSIS:** Cleanse a blighted harvest site and leave an offering, walk the flame pup home past the fields, and let the bond set.
- **END RESULT:** 30 food and a bonded flame pup whose hearth fire quickens what the fields give back.
- **IMAGE:** A young farmhand crouches at the edge of a field, holding out a scrap of food to a small pup.

### 18. Wild Hearts: The Wounded Wolf
- **DATA ID:** `petbond.craghound`
- **GIVER:** not authored (the wolf is brought home to the Echo Hollow, but no person is named)
- **SYNOPSIS:** Protect a wounded wolf through a raid at the walls, bring it home through the west gate, and bond with it.
- **END RESULT:** 40 crystals and a bonded ice wolf whose hide turns a blow meant for the Heart.
- **IMAGE:** An armored fighter stands over a limping wolf with her shield raised, arrows striking the wall behind them.

### 19. Wild Hearts: Ice Wolf
- **DATA ID:** `petbond.frostkit`
- **GIVER:** Fenn Wildmane (the Echo Hollow) -- named at the second stage
- **SYNOPSIS:** Make a slow, patient approach on cold ground, bring the wolf down out of the cold to a warm stall, and bond with it.
- **END RESULT:** 40 crystals and a bonded ice wolf whose bite leaves the cold behind in the wound.
- **IMAGE:** A kneeling figure holds perfectly still on frozen ground with one hand extended toward a wary wolf.
- **NOTE:** near-identical subject to quests 18 and 23.

### 20. Wild Hearts: Flame Pup
- **DATA ID:** `petbond.emberpup`
- **GIVER:** not authored
- **SYNOPSIS:** Stand in the worst heat of the Emberwastes beside the pup, bring it home to settle at the Echo Hollow, and bond with it.
- **END RESULT:** 40 crystals and a bonded flame pup whose bite sets enemies alight.
- **IMAGE:** A traveler shields her face from the heat with a raised arm while a small pup stands calmly beside her on cracked, scorched ground.
- **NOTE:** near-identical subject to quest 17.

### 21. Wild Hearts: The Cleansed Water
- **DATA ID:** `petbond.mirewing`
- **GIVER:** not authored
- **SYNOPSIS:** Cleanse the poisoned water of the Mirewood at its source, bring the sprite home to roost, and bond with it.
- **END RESULT:** 15 magic and a bonded aether sprite whose light eases what the mire left behind.
- **IMAGE:** A person pours clear water from a clay jug into a fouled marsh spring while a small winged creature hovers just above the surface.

### 22. Wild Hearts: The Flawless Stone
- **DATA ID:** `petbond.glimmermoth`
- **GIVER:** Sable (the Jeweler's Bench)
- **SYNOPSIS:** Obtain a flawless stone from the jeweler, raise a Crystal Mine and coax the sprite to the new seam with it, then bond.
- **END RESULT:** 60 crystals and a bonded aether sprite that scents richer crystal than any hand can find.
- **IMAGE:** A jeweler holds a flawless cut stone out flat on her palm while a small winged creature drifts down toward it.

### 23. Wild Hearts: The Caged Wolf
- **DATA ID:** `petbond.stoneback`
- **GIVER:** not authored
- **SYNOPSIS:** Clear an orc raider camp beyond the walls and free the caged wolf inside. It bonds at once -- two stages, the shortest of the bonds.
- **END RESULT:** 50 crystals and a bonded ice wolf that will carry and will cover.
- **IMAGE:** A fighter levers open the bars of a wooden cage in a raider camp while the wolf inside pushes toward the gap.

### 24. Wild Hearts: Aether Sprite
- **DATA ID:** `petbond.aetherfox`
- **GIVER:** the Village Elder (Heart of Elarion) -- named at the second stage
- **SYNOPSIS:** Hold the restored ground through a wave in the light of the Heart, present the sprite to the Elder, and bond with it. The richest bond in the file.
- **END RESULT:** 100 crystals, 50 magic, and a bonded aether sprite whose aura lightens every ability the player spends.
- **IMAGE:** An old robed elder cups a small winged creature in both hands and presents it to a young defender at the foot of a great tree.
- **NOTE:** near-identical staging to quest 1.

---

## Appendix -- the daily quest pool (not numbered, and deliberately so)

`daily-quests.json` holds **38 procedural templates** across three slots (combat, exploration, wildcard), drawn three at a time into the board's Daily tab and reset at local midnight. They have **no giver and no authored outcome** -- the reward is a fixed per-slot payout (combat: 25 crystals + 1 wisdom; exploration: 15 crystals + 20 food; wildcard: 1 wisdom + a random item). Eighteen of them are the same "clear N waves" objective with different flavour text; eleven are the same "pass N gates".

Briefing 38 images would produce 38 near-duplicates, so the honest recommendation is **three slot-level images reused across the whole pool**:

- **Daily / combat:** A helmeted defender braces a spear along a town wall as figures come up the slope below.
- **Daily / exploration:** A cloaked walker checks a shuttered gate along a wall, lantern in hand.
- **Daily / wildcard:** A young fighter practices a sword form in a training yard while an older instructor corrects her grip.

---

## Gap list -- givers not authored in the data (9 of 24, plus the whole daily pool)

Quests **2, 5, 6, 15, 17, 18, 20, 21, 23** name no person who sends the player, and neither does any daily template. If you want a face on those cards, the name has to be authored -- it does not exist in `quests.json` today.

## Gap list -- outcomes not authored

Every quest's payout lands on its final stage; intermediate stages pay nothing, which is by design. The one real gap is **quest 3 (Honest Steel)**, whose single stage grants no crystals, no food, no magic, no item and no keystone. It is the only quest in the file that ends with nothing in hand -- its whole payoff is unlocking quest 4.

## Near-duplicate image briefs -- the real clusters

These are the groups that would produce interchangeable art. Each cluster is also the natural candidate for one shared questline illustration under the section 11 ruling.

1. **Smith at the forge or anvil -- quests 3, 6, 7, 8.** Four separate images of an old smith working metal. Quests 3 and 6 already share the Forgemasters chain; 7 and 8 are separate gear quests but are the same man at the same anvil.
2. **Wolf bonding -- quests 18, 19, 23.** Three quests that all resolve to bonding an ice wolf. The only staging differences are wounded / wary / caged.
3. **Flame pup bonding -- quests 17, 20.** Both are a person coaxing the same pup; one in a field, one on hot ground.
4. **Aether sprite bonding -- quests 21, 22, 24.** Three quests bonding the same sprite, differing mainly in who is standing there.
5. **Elder at the great tree -- quests 1 and 24.** Same character, same location, same framing; the only difference is what is in his hands.
6. **Market handover -- quests 10 and 14.** Coppin passing goods to a traveler, twice.

Clusters 2, 3 and 4 exist because the data deliberately folded eight authored bond quests onto the three companion species that ship. That is a content fact, not an art oversight -- eight bond quests will keep producing three pictures until there are more than three species.
