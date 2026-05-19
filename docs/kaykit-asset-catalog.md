# KayKit Asset Catalog — Defenders of the Realm (Unity port)

**Purpose:** One opinionated pick-list of the entire KayKit collection for the team building game content.
**Game:** Stylized low-poly magical + medieval crossover — a tower-defense village (defend the Heart of Elarion against waves) plus a dungeon-crawler (the Healer's Cottage and future dungeons). Unity 6, URP.
**Asset root:** `Assets/Models/KayKit/`
**Last explored:** 2026-05-19

> **How to read paths.** Every pack ships its models twice: a raw `fbx/` folder and a Unity-tuned `fbx(unity)/` folder. **Always import from `fbx(unity)/`** — those have the import settings KayKit pre-baked for Unity. The short-name folders (`characters/`, `enemies/`, `medieval/`, `dungeon/`, `weapons/`, `anim/`) are a small curated `.glb` subset already wired into the game — treat them as "the live set," and the full packs as the warehouse to pull more from.

---

## 1. Summary table — every pack

| Pack | Theme | Rough model count | Status |
| --- | --- | --- | --- |
| Medieval Hexagon Pack 1.0.1 | Hex-tile medieval town builder (buildings, terrain, props in blue/green/red team colors) | ~330 | **In use** — Village hub tiles |
| Dungeon Remastered 1.1 | Modular dungeon interiors (walls, floors, furniture, props, traps) | ~370 | **In use** — Healer's Cottage |
| Adventurers 2.0 | 7 hero classes + weapons/potions | ~9 chars + ~55 props | **In use** — Mage is the hero; others untapped |
| Skeletons 1.1 | 6 undead characters + skeleton weapons | 6 chars + ~22 weapons | **In use** — wave enemies + boss |
| Character Animations 1.1 | Shared mocap animation library + mannequin rigs | 2 mannequins + ~15 anim sets | **In use** — drives all rigged characters |
| Forest Nature Pack 1.0 | Trees, bushes, rocks, hills, terrain (multi-color seasons) | ~1,500+ | **Partly used** — village dressing |
| `characters/`, `enemies/`, `medieval/`, `dungeon/`, `weapons/`, `anim/` | Curated `.glb` live-set subsets | ~20 glb total | **In use** — the wired set |
| City Builder Bits 1.0 | Modern city blocks, cars, roads, parks | ~110 | Untapped |
| Furniture Bits 1.0 | Cozy indoor furniture (beds, shelves, desks, rugs, lamps) | ~75 | **Untapped — high value** |
| Fantasy Weapons Bits 1.0 | Swords, axes, staves, wands, bows, shields (A–G variants) | ~48 | Untapped |
| RPG Tools Bits 1.0 | Crafting/exploration tools (anvil, pickaxe, lantern, keys, maps) | ~70 | **Untapped — high value** |
| Resource Bits 1.0 | Gems, ore, gold, crates, food, money piles | ~150 | **Partly used** — crystal economy |
| Halloween Bits 1.0 | Graveyard/spooky (gravestones, coffins, pumpkins, dead trees, candles) | ~110 | **Untapped — high value** |
| Holiday Bits 1.0 | Christmas/winter (trees, presents, snowmen, gingerbread, lanterns) | ~180 | Untapped (seasonal) |
| Board Game Bits 1.0 | Chess, cards, dice, poker chips, coins | ~190 | Untapped |
| Block Bits 1.0 | Minecraft-style voxel cubes (terrain + items) | ~60 | Untapped |
| Platformer Pack 1.0 | Platforms, traps, hazards, hoops, conveyors (multi-color) | ~600 | Untapped |
| Space Base Bits 1.0 | Sci-fi base modules, landers, terrain | ~70 | Untapped (off-theme) |
| Restaurant Bits 1.0 | Kitchen, food prep, ingredients, pizza/burger | ~250 | Untapped (off-theme) |
| Prototype Bits 1.1 | Greybox primitives + targets/dummy + a Bat | ~90 | **Untapped — useful for blockout** |
| Mystery Monthly Series 4 | 12 monthly character packs (Jul 2023 – Jun 2024) | ~15 characters + props | **Untapped — enemy/NPC goldmine** |
| Mystery Monthly Series 5 | 12 monthly character packs (Jul 2024 – Jun 2025) | ~16 characters + props | **Untapped — enemy/NPC goldmine** |

**Totals:** ~25 distinct content packs (27 top-level folders counting the curated `.glb` subsets and the loose `weapons` folder). **~15 packs are essentially untapped.** The two Mystery Monthly series alone hold **~31 fully-rigged characters** that nothing in the game uses yet — this is the single biggest unrealized opportunity in the collection.

---

## 2. Per-pack notes & creative applications

### Medieval Hexagon Pack 1.0.1 — `KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/`
The backbone of the village. Buildings come in **blue / green / red team-color variants** plus **neutral** structural pieces (walls, gates, bridges, fences). Building types: archery range, barracks, blacksmith, castle, church, docks, two homes, lumbermill, market, mine, shipyard, shrine, stables, tavern, tent, multiple towers (A/B/base/cannon/catapult/watchtower), townhall, watermill, well, windmill, workshop.
- **Creative use:** This *is* the village. Map Elarion's buildings: `building_mine_*` → Crystal Mine, `building_tower_A` or `building_watchtower` → Arcane Tower, `building_workshop` → Workshop, `building_stables` → Pet House (cozier than a generic barn), `building_windmill`/`building_watermill` → Farm. The `building_shrine` and `building_church` are perfect for **the Heart's sanctuary** — the magical core the player defends. `building_castle` is your endgame/late-realm hub. Use the neutral `wall_*` + `building_tower_cannon/catapult` pieces to literally build the **defensive perimeter** the waves attack. `building_destroyed` and `building_scaffolding` sell wave damage and "rebuild" beats.

### Dungeon Remastered 1.1 — `KayKit Dungeon Remastered 1.1/Assets/fbx(unity)/`
A complete modular dungeon kit: floors, walls, doors, stairs, columns, plus dense furniture/props — beds (A/B, single/double/stacked/floor/decorated), bookcases, barrels, boxes, banners (every color + pattern + shield/triple/thin variants), bottles, books, candles, bars/bartops (a full tavern counter set), barriers.
- **Creative use:** Already the Healer's Cottage skeleton. The **bottle / book / candle / bookcase** clutter is exactly the apothecary dressing the Cottage needs. The full **bar set** (`bar_straight`, `bartop`, `bar_corner`) can build a tavern room — a natural "rest/quest-giver" space between dungeon floors. Banners in violet/blue carry Elarion's heraldry through every dungeon. Use `barrier_*` pieces as breakable cover in dungeon combat rooms.

### Adventurers 2.0 — `KayKit Adventurers 2.0/Characters/fbx(unity)/` + `Assets/fbx(unity)/`
The hero pack: 9 character models (see §4) plus their gear — swords (1H/2H), axes, daggers, bows, crossbows, staves, wands, druid staff, engineer wrench, shields (round/square/spikes/badge, plain + color), quiver, spellbook (open/closed), potions in **4 sizes × 4 colors**, mugs, ammo crates, a turret base.
- **Creative use:** Heroes are §4. The **potion set** is the in-world reward/pickup currency — color-code them to the crystal rarity tiers. The `turret_base` + a mounted weapon is a ready-made **buildable defense tower** for the village. `spellbook_open` floating with an emissive glow is a great quest-objective prop.

### Skeletons 1.1 — `KayKit Skeletons 1.1/characters/fbx(unity)/` + `assets/fbx(unity)/`
The core enemy pack — 6 undead characters (see §3) plus a full skeleton arsenal: axe, blade, dagger, mace (+large), scythe, staff, crossbow, quiver, arrows (whole/broken/half), shields (large/small A/B), and the Golem's oversized axe.
- **Creative use:** This is the "Hollow Ones" wave roster. Randomly assign a kind-appropriate weapon per spawn for visual variety (warriors → blade/mace/axe, rogues → dagger/crossbow, mages → staff, the scythe reads as an elite/reaper). Broken arrows scattered on the ground sell a battlefield.

### Character Animations 1.1 — `KayKit Character Animations 1.1/Animations/`
Not props — the shared animation library. `Rig_Medium` and `Rig_Large` skeletons each ship General, MovementBasic/Advanced, CombatMelee, CombatRanged, Special, Simulation, Tools clip sets. Includes two `Mannequin` characters for previewing.
- **Creative use:** Every KayKit humanoid (Adventurers, Skeletons, all Mystery Monthly characters) shares the Rig_Medium/Rig_Large skeleton, so **one retargeted animator controller drives the entire cast.** Build it once. The `Special` set has cast/channel-style clips for the Mage's abilities; `Tools` covers mining/harvesting for village idle activity.

### Forest Nature Pack 1.0 — `KayKit Forest Nature Pack 1.0/Assets/fbx(unity)/`
Massive (~1,500+ files): trees, bushes, grass, rocks, mushrooms, flowers, logs, plus a full modular **hill/cliff terrain system** — all in multiple `Color` variants (seasons/biomes).
- **Creative use:** Surround the village and frame dungeon entrances. The **Color variants are your day/night and biome swap** — pick a cool/violet-leaning color set for areas near the Heart so the environment itself reads "magical." Mushrooms and flowers add the cozy pet-friendly charm. The hill/cliff kit can sculpt the realm's terrain without external tools.

### City Builder Bits 1.0 — `KayKit City Builder Bits 1.0/Assets/fbx(unity)/`
Modern city: 8 building shells (A–H, with/without base), 5 car types, roads, parks, street furniture.
- **Creative use:** Mostly off-theme, but the **`building_A`–`building_H` shells are clean geometric volumes** — useful as far-distance background silhouettes or as a fast greybox before medieval art is placed. Skip the cars.

### Furniture Bits 1.0 — `KayKit Furniture Bits 1.0/Assets/fbx (unity)/`  *(note the space in the folder name)*
Cozy interior furniture: beds (single/double, A/B), armchairs, couches, cabinets, shelves, desks, tables, chairs, stools, rugs, lamps, pillows, picture frames, books, cups, plus cacti and a game console.
- **Creative use:** **Dress the Healer's Cottage interior.** Beds for the recovery room, `cabinet`/`shelf_B_large_decorated` and `book_set` for the apothecary, `desk` + `lamp_table` for the Healer's study, rugs and armchairs for warmth. This pack turns the Cottage from "dungeon room" into "a home worth defending." Drop the cacti/console (off-theme).

### Fantasy Weapons Bits 1.0 — `KayKit Fantasy Weapons Bits 1.0/Assets/fbx(unity)/`
Higher-detail standalone weapons: swords A–G, axes A–D, hammers A–D, daggers A–C, bows A–C, staves A–D, wands A–B, spears, halberd, scythe, fist weapons, shields A–D, arrows.
- **Creative use:** A loot/upgrade table. The Adventurers each have one bound weapon — this pack gives **weapon tiers** (a hero's sword visibly upgrades A→G). `staff_*` and `wand_*` with emissive tints sell arcane gear. Mount any on a `Weaponrack` (Prototype Bits) for the Workshop or an armory room.

### RPG Tools Bits 1.0 — `KayKit RPG Tools Bits 1.0/Assets/fbx(unity)/`
Crafting and exploration tools: anvil, grindstone, pickaxe, hammer, saw, axe, shovel, lantern, torch, keys A–C, locks, lockpicks, maps (rolled/empty), journal, blueprint, compass, magnifying glass, rope, fishing kit.
- **Creative use:** High value. **`pickaxe` + `lantern` + `bucket` build the Crystal Mine's interactable details.** `anvil`/`grindstone`/`hammer`/`blueprint` are the Workshop's signature props. `key_*` + `lock_*` are ready-made dungeon-progression objects (locked doors, treasure). `map_rolled` + `journal` + `compass` make a great quest-board / Keeper's-desk vignette. `torch`/`lantern` are the warm light sources for dungeon corridors.

### Resource Bits 1.0 — `KayKit Resource Bits 1.0/Assets/fbx(unity)/`
Economy props: gems (`Gem_Small/Medium/Large`, `Gems_Pile_*`, `Gems_Chest`, `Gems_Sack`), ore nuggets/bars (copper/iron/gold), money (coins/bills/piles), crates/boxes/piles, food (apples, berries, cheese, baskets), fuel barrels, cogs/parts.
- **Creative use:** Drives the crystal economy. `Gem_Small→Medium→Large` = common→rare→epic tiers (re-tint to violet for Elarion). `Gems_Pile_Large` on the mine animates as production accrues; `Gems_Chest` = a claim/reward state. Ore + crate clutter dresses the Mine and Workshop yards. Food baskets stock the Farm.

### Halloween Bits 1.0 — `KayKit Halloween Bits 1.0/Assets/fbx(unity)/`
Spooky graveyard: gravestones, graves, gravemarkers, coffins, crypt, shrine, bones (A/B/C), ribcage, skulls, dead trees, orange/yellow pine trees, candles, lanterns, scarecrow, pitchfork, fences, hay, candy.
- **Creative use:** **Don't think "Halloween event" — think "the corrupted lands the Hollow Ones come from."** A graveyard biome with crypts, bones, and dead trees is the perfect origin-zone for the undead waves and a strong dungeon theme. `shrine_candles`, `coffin`, and `crypt` make a chilling mini-boss arena. Skulls and bones scatter naturally around skeleton spawn points. Skip the candy.

### Holiday Bits 1.0 — `KayKit Holiday Bits 1.0/Assets/fbx(unity)/`
Christmas/winter: christmas trees (lit/unlit/decorated), presents (A–F, sphere, ~6 colors), gingerbread building kit, candy canes, bells, mistletoe, hot chocolate, lanterns, cozy chairs.
- **Creative use:** A seasonal winter dress for the village behind a date flag. The `lantern`/`lantern_decorated` and warm chairs are reusable year-round for cozy interiors. Presents = a holiday-event reward chest. The gingerbread kit could build a whimsical secret/bonus dungeon.

### Board Game Bits 1.0 — `KayKit Board Game Bits 1.0/Assets/fbx(unity)/`
Chess sets (black/white, all pieces), full playing-card deck, poker chips, coins (copper/silver/gold, denominations), boards, dice, buildings.
- **Creative use:** Mostly off-theme, but the **coins are a clean currency pickup/UI prop**, and the **chess pieces** are a charming option for a tabletop-themed puzzle room or a "the realm is a game board" narrative flourish. Dice work as a luck/gacha visual.

### Block Bits 1.0 — `KayKit Block Bits 1.0/Assets/fbx(unity)/`
Voxel cubes: terrain (dirt/grass/stone/sand/snow/gravel/lava/water), ore-bearing stone, and item-cubes (chest, vault, anvil, books, dynamite, gift).
- **Creative use:** Off the main art style, but the `stone_with_gold/copper/silver` cubes and `chest`/`vault` could power a **mining mini-game** with a deliberately distinct blocky look. Otherwise low priority.

### Platformer Pack 1.0 — `KayKit Platformer Pack 1.0/Assets/fbx(unity)/`
~600 platforming pieces in multiple colors: platforms (every size), slopes, traps (saw, spikes, hammer), conveyors, cannons, levers, buttons, hoops, flags, hazards, collectibles (diamond, heart, power).
- **Creative use:** Off-theme as scenery, but the **trap mechanisms** (`saw_trap`, `floor_spikes_trap`, `hammer_spikes`, `lever`, `button`) are genuinely useful as **dungeon hazards and puzzle triggers** if you re-tint them stone/metal. The `heart` collectible could even be a literal pickup tying to the Heart-of-Elarion motif.

### Space Base Bits 1.0 / Restaurant Bits 1.0
Both fully off-theme (sci-fi base / modern kitchen). **Recommend: shelve both** unless a future minigame or realm calls for them. Restaurant's raw food ingredients could marginally feed an apothecary/cooking system, but Resource Bits + Witch pack cover that better.

### Prototype Bits 1.1 — `KayKit Prototype Bits 1.1/Assets/fbx(unity)/`
Greybox kit: primitive walls/floors/slopes/stairs, doors, pillars, plus shooting-range targets, a training `Dummy_Base`, weapon racks, workbenches, lockers, barrels, and a **`Bat.fbx`** (baseball bat).
- **Creative use:** The **primitives are the fastest way to block out a new dungeon or village layout** before final art. `Dummy_Base` + `target` make a believable training-yard for a tutorial. `Weaponrack`/`Workbench`/`Locker` dress the Workshop and armory rooms.

---

## 3. Enemies & Creatures

The combat roster. **The Skeletons pack is the village's core "Hollow Ones" wave enemy.** The Mystery Monthly characters expand this into a full bestiary of dungeon enemies and bosses. All are rigged on the shared Rig_Medium/Rig_Large skeleton, so they animate from the one shared controller.

> Paths point at the Unity-ready model. Mystery Monthly characters are FBX; the Skeletons live-set is also exposed as `.glb` in `Assets/Models/KayKit/enemies/`.

### Core wave enemies — the Hollow Ones (Skeletons 1.1)
Path prefix: `KayKit Skeletons 1.1/characters/fbx(unity)/`

| Model | File | Visual | Suggested role |
| --- | --- | --- | --- |
| Skeleton Minion | `Skeleton_Minion.fbx` | Small, bare, fragile skeleton | Wave 1 fodder — the basic Hollow One; spawn in swarms |
| Skeleton Warrior | `Skeleton_Warrior.fbx` | Armed, armored standard skeleton | Standard mid-wave melee enemy |
| Skeleton Rogue | `Skeleton_Rogue.fbx` | Hooded, lean, fast | Fast flanker — rushes the Heart, low HP |
| Skeleton Mage | `Skeleton_Mage.fbx` | Robed staff-wielding skeleton | Ranged caster — hangs back, hits towers/Heart at distance |
| Skeleton Golem | `Skeleton_Golem.fbx` | Large bone-construct, oversized axe | Heavy/"brute" — slow, bulky, high HP; mini-boss material |
| Necromancer | `Necromancer.fbx` | Hooded non-skeleton leader, staff | **Wave boss** — see §6; can summon the minions above |

Curated `.glb` copies already wired: `enemies/Skeleton_Minion.glb`, `Skeleton_Warrior.glb`, `Skeleton_Rogue.glb`, `Skeleton_Mage.glb`, `Necromancer.glb`. (The Golem `.glb` is in the full pack only — promote it if you want a brute.)

### Monster & creature roster (Mystery Monthly Series 4 & 5)
These are the expansion bestiary — dungeon encounter enemies and bosses.

| Creature | Path | Visual | Suggested role |
| --- | --- | --- | --- |
| Orc Raider | `KayKit Mystery Monthly Series 4/1 - July 2023 - Orc Raider/character/OrcRaider.fbx` | Hulking green orc, axe/club, war-drum | Heavy raider — a "living" (non-undead) enemy faction; great realm-2 wave or dungeon brute |
| Werewolf (beast) | `KayKit Mystery Monthly Series 4/4 - October 2023 - Werewolf/characters/fbx/Werewolf_Wolf.fbx` | Full feral wolf-beast | Fast savage dungeon predator; pack-hunter encounter |
| Werewolf (man form) | `.../4 - October 2023 - Werewolf/characters/fbx/Werewolf_Man.fbx` | Human/transitional form | Mini-boss with a transform beat: starts as Man, shifts to Wolf at half HP |
| Monster (kaiju) | `KayKit Mystery Monthly Series 4/3 - September 2023 - Monster Costume/character/fbx/Monster.fbx` | Large lumbering monster | Big slow dungeon boss or a giant realm-finale threat |
| Monster Costume | `.../3 - September 2023 - Monster Costume/character/fbx/MonsterCostume.fbx` | Person in a monster suit | Comic NPC, or a "false monster" reveal in a quest |
| Vampire | `KayKit Mystery Monthly Series 5/4 - October 2024 - Vampire/characters/Vampire.fbx` | Caped pale aristocrat, throne + gem props | **Dungeon lord boss** — pairs with `Vampire_Throne`, `Gem_*`, `Vampire_Goblet` for a full lair |
| Witch | `KayKit Mystery Monthly Series 5/5 - November 2024 - Witch/characters/Witch.fbx` | Classic witch, broom + cauldron + potion station | **Healer's Cottage mini-boss** (see §6) — or a corrupted-healer NPC |
| Frost Golem | `KayKit Mystery Monthly Series 5/7 - January 2025 - FrostGolem/characters/FrostGolem.fbx` | Hulking ice golem, large axe | Ice-biome boss / heavy elite; the "Frost Wolf" wave's true face |
| Black Knight | `KayKit Mystery Monthly Series 5/3 - September 2024 - Black Knight/characters/BlackKnight.fbx` | Dark-armored knight, sword + shield (large variants) | **Gate Warden / elite boss** — a fallen champion guarding a dungeon |
| Tiefling | `KayKit Mystery Monthly Series 5/12 - June 2025 - Tiefling/characters/Tiefling.fbx` | Horned demon-kin, swords + back-scabbard | Demonic dungeon enemy or an antihero boss; strongly "magical" |
| Caveman | `KayKit Mystery Monthly Series 5/8 - February 2025 - Caveman/characters/Caveman.fbx` | Primitive brute, club/spear/axe | Wildlands brute enemy; a non-undead faction variant |
| Clanker | `KayKit Mystery Monthly Series 5/9 - March 2025 - Clanker/characters/Clanker.fbx` | Junk/scrap robot | Off-theme — reskin as an arcane construct/golem if used at all |
| Combat Mech | `KayKit Mystery Monthly Series 5/1 - July 2024 - Combat Mech/characters/CombatMech.fbx` | Bulky war mech, minigun/axe | Off-theme — only for a clockwork-construct boss with heavy retint |
| Animatronic (creepy) | `KayKit Mystery Monthly Series 4/5 - November 2023 - Animatronic/characters/fbx/Animatronic_Creepy.fbx` | Damaged sinister animatronic | Cursed-toy / haunted enemy for a horror-flavored dungeon room |
| Animatronic (normal) | `.../5 - November 2023 - Animatronic/characters/fbx/Animatronic_Normal.fbx` | Intact animatronic | Same family, "before corruption" state |
| Clown | `KayKit Mystery Monthly Series 4/11 - May 2024 - Clown/characters/Clown.fbx` | Circus clown, balloons/bombs/juggling props | Off-theme — only as a one-off cursed-carnival mini-event |
| Robot One / Two | `KayKit Mystery Monthly Series 4/12 - June 2024 - Robot/characters/Robot_One.fbx`, `Robot_Two.fbx` | Friendly cartoon robots | Off-theme — possible quirky pet/helper if reskinned |

**Bat (prop, not rigged):** `KayKit Prototype Bits 1.1/Assets/fbx(unity)/Bat.fbx` is a baseball bat, **not** a flying bat — there is no animal bat in the collection. For a flying "aether bat" wave enemy, the closest options are the Skeleton Rogue (hooded, fast silhouette) or a custom billboard.

### Off-theme "human" character packs (Mystery Monthly) — low enemy value
Driver, Action Figure, Space Ranger, Ninja, Survivalist, Superhero, Hiker, Helpers, Caveman's modern siblings, Protagonists — these are modern/sci-fi humans. **Ninja** (`Series 4/8 - February 2024 - Ninja/character/Ninja.fbx`) is the one exception worth noting: with a dark retint it works as a **rogue/assassin enemy or a stealth hero class.** The rest are best left shelved unless a crossover event calls for them.

---

## 4. Characters & NPCs

### Hero-class candidates — Adventurers 2.0
Path prefix: `KayKit Adventurers 2.0/Characters/fbx(unity)/`

| Class | File | Notes |
| --- | --- | --- |
| Mage | `Mage.fbx` | **The current hero.** Robed wizard with a staff — the magical-realm protagonist. |
| Knight | `Knight.fbx` | Sword-and-board hero; obvious "tank" class for hero-select. |
| Ranger | `Ranger.fbx` | Bow user; ranged hero. |
| Rogue / Rogue (Hooded) | `Rogue.fbx`, `Rogue_Hooded.fbx` | Dagger/crossbow; fast hero. Hooded variant is moodier. |
| Barbarian / Barbarian (Large) | `Barbarian.fbx`, `Barbarian_Large.fbx` | Axe brute; the Large variant reads as a heavyweight. |
| Druid | `Druid.fbx` | Staff caster — nature/healing flavor; a strong thematic partner to the Mage. |
| Engineer | `Engineer.fbx` | Wrench + turret-base gear; fits the Workshop and a "tower-builder" hero fantasy. |

Curated `.glb` live-set: `characters/Knight.glb`, `Mage.glb`, `Ranger.glb` are already exposed — the other four classes need promoting from the full pack when hero-select ships.

### NPC candidates
KayKit has no purpose-built villager pack, so NPCs come from re-purposing characters:
- **The Keeper / quest-giver:** `Druid.fbx` (wise, robed) or the un-hooded `Rogue.fbx`. The Druid sells "guardian of the Heart."
- **Bryn the Wanderer:** `Ranger.fbx` (traveler with a bow) or the Mystery Monthly **Hiker** (`Series 5/11 - May 2025 - Hiker/characters/Hiker.fbx`) — the Hiker literally reads as a wanderer with a pack.
- **The Healer (Cottage NPC):** the **Witch** model used *benignly* (`Series 5/5 - November 2024 - Witch/characters/Witch.fbx`) — cauldron and potion station make her unmistakably an apothecary/healer.
- **Villagers / townsfolk:** the **Protagonists** pair (`Series 5/10 - April 2025 - Protagonists/characters/Protagonist_A.fbx`, `Protagonist_B.fbx`) and **Helpers** pair (`Series 5/6 - December 2024 - Helpers/characters/Helper_A.fbx`, `Helper_B.fbx`) give four neutral civilian bodies — the best stand-in villagers in the collection.
- **Paladin** (`Series 4/10 - April 2024 - Paladin/characters/fbx/Paladin.fbx`, plus a `Paladin_with_Helmet` variant) — a holy knight; excellent as an **allied captain / Heart-guard NPC** or an 8th hero class. Ships with statue/hammer/shield props for a shrine vignette.

### Pet candidates
The collection has **no dedicated pet/companion creatures.** Honest options:
- **Werewolf_Wolf** (`Series 4/4 - October 2023 - Werewolf/.../Werewolf_Wolf.fbx`) — the only true quadruped beast; could be a tamed wolf companion.
- **Robot One / Robot Two** — small friendly bots; a quirky arcane-construct pet if retinted.
- **Skeleton Minion** at small scale with a friendly tint — a "summoned" pet that fits the magical theme.
- Otherwise, pets likely need 2D billboards or commissioned models. Flag this for the owner.

---

## 5. The magical layer

Assets that sell the *magical* half of the crossover and Elarion's violet crystalline identity:

- **Crystals & gems** — `Resource Bits/Gem_Small/Medium/Large`, `Gems_Pile_*`, `Gems_Chest`, `Gems_Sack`. Re-tint to violet and add emissive bloom: these are **the Heart of Elarion's visual language**. Cluster `Gem_Large` around the Heart sanctuary; the chest/sack vary the silhouette.
- **Vampire gem props** — `Series 5/4 - October 2024 - Vampire/assets/fbx(unity)/Gem_Large/Medium/Small.fbx` are a *second, chunkier* gem set — good for large set-piece crystals and crystal-formation outcrops.
- **Arcane gear** — `Adventurers/staff`, `wand`, `druid_staff`, `spellbook_open/closed`; `Fantasy Weapons/staff_A–D`, `wand_A–B`. With emissive tints these are floating arcane props, spell foci, and the Mage's loadout.
- **Shrines & sanctuaries** — Medieval Hexagon `building_shrine`, `building_church`; Halloween `shrine`, `shrine_candles`. The shrine pieces are the natural form for **the Heart's altar**.
- **Mystical light** — `RPG Tools/lantern`, `torch`; `Holiday Bits/lantern_decorated`, `lantern_mini`; `Dungeon Remastered/candle_lit`. Tint cool-violet for an enchanted glow rather than warm firelight near the Heart.
- **The witch's apothecary** — `Series 5 Witch/assets/fbx(unity)/Cauldron`, `Potionstation`, `Potionstation_decorated`, `Mortar`, `Pestle`, `Broom`, `Basket_Mushrooms`. The single best magical-workshop prop set in the collection — built for the Healer's Cottage.
- **Potions** — `Adventurers` potions (4 sizes × blue/green/orange/red) and `Dungeon Remastered` labeled bottles — glowing alchemy props and pickups.
- **Banners & heraldry** — `Dungeon Remastered` banners come in every color and pattern; standardize on a violet/blue Elarion banner to thread the realm's identity through every scene.

---

## 6. Boss & set-piece candidates (quick reference)

| Encounter | Model | Supporting props |
| --- | --- | --- |
| Village wave boss | `Skeletons 1.1/.../Necromancer.fbx` (scale ~1.8×) | Summons `Skeleton_Minion`s mid-fight |
| Village brute / alt-boss | `Skeletons 1.1/.../Skeleton_Golem.fbx` (scale ~2.2×, red emissive) | `Skeleton_Golem_Axe_Large` |
| **Healer's Cottage mini-boss** | `Series 5 Witch/characters/Witch.fbx` | Cauldron, broom, potion station — fight staged in her apothecary |
| Vampire lair boss | `Series 5/Vampire/characters/Vampire.fbx` | `Vampire_Throne`, `Gem_*`, `Vampire_Goblet`, dungeon banners |
| Gate Warden / elite | `Series 5/Black Knight/characters/BlackKnight.fbx` | `BlackKnight_Sword_Large`, `BlackKnight_Shield_Large` |
| Ice-realm boss | `Series 5/FrostGolem/characters/FrostGolem.fbx` | `FrostGolem_Axe_Large`; Holiday Bits snow dressing |
| Beast mini-boss (transform) | `Series 4/Werewolf` Man → Wolf | `axe`, `log_*` for a woodcutter's-camp arena |
| Demonic boss | `Series 5/Tiefling/characters/Tiefling.fbx` | `Tiefling_Sword`, violet shrine props |

---

## Top creative opportunities

1. **The Mystery Monthly characters are an untapped bestiary of ~31 rigged characters.** Vampire, Witch, Frost Golem, Black Knight, Werewolf, Tiefling, Orc Raider, and the Monster give you a full slate of named dungeon bosses and a second non-undead enemy faction — without commissioning a single new model. They share the rig, so they animate for free.
2. **Furniture Bits + RPG Tools Bits + the Witch's apothecary set will transform the Healer's Cottage** from a bare dungeon room into a believable, cozy home. That emotional "a home worth defending" beat is currently missing and is cheap to add — both packs are completely untapped.
3. **Halloween Bits is mis-labeled in your head — it's actually the origin-lands of the Hollow Ones.** Crypts, gravestones, bones, and dead trees give the undead waves a coherent home biome and a ready-made graveyard dungeon, deepening the world rather than being a throwaway seasonal pack.
