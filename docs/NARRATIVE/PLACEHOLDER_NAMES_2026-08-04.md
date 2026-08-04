# Placeholder Names -- the dev strings a player can read

**DRAFT -- owner sign-off required.** Nothing in this document is canon until Samantha approves it.
No code, no `.asset`, no `.json` edit is authorized by this file. It is copy + a defect ledger for an
implementation agent to consume AFTER sign-off.

- **Date:** 2026-08-04
- **Author:** narrative agent (drafting only -- creative authority is the owner's)
- **Trigger:** `Assets/Resources/Towers/DevTower.asset:15` ships `towerName: DevTower` as a
  **player-facing** name, and `BuildMenuVM.cs:98` pins it as the ONLY tower the Build Menu ever raises.
- **Why now:** the Solana dApp Store listing puts player-facing strings into screenshots.
- **Words only.** This document proposes names and lists defects. It changes nothing.

---

## Canon absorbed before writing

| Source | What I took from it |
|---|---|
| `docs/STORYLINE.md` | Tone, the Withering, the Hollow Ones, Alduin, the Folk, the cycle. **Its Spire-replaces-the-Tree frame is superseded** (its own top banner + the DESIGN-DECISIONS reversal). The one thing I DID keep from it: section 3's ruling that the tower is *the one defensive structure the Folk still know how to build*, and section 7.9's ward-stone framing. |
| `docs/DESIGN-DECISIONS.md` | Top banner: **the living world-Tree is canon** (owner ruling 2026-06-26) -- so no name of mine leans on the Spire. **#21**: internal ids stay frozen while display strings change (`pet-house` displays as "Echo Hollow"). That precedent is the whole shape of my recommendation below. |
| `Assets/Resources/Data/Canonical/canon-strings.json` | Elarion (never Avalon). The Keeper. The Folk. The Heart. Tagline "Echoes of a Forgotten Civilization". The note at line 42 that the **Spire / Chord / Lantern / Stone-Choir motifs are RETIRED**. |
| `Assets/Resources/Data/Canonical/glossary.json` | The shipped register: plain, second-person, functional, no purple. "Heart of Elarion -- the world tree and stone reliquary at the centre of the village." I matched that plainness. |
| `Assets/Resources/Data/Canonical/en.json` | The already-shipped tower copy -- line 172 `buildingDesc.arcaneTower` and lines 195-196 `tooltip.buttonTowers`: *"Stone spires raised by the first Keepers. Their **ward-stones** answer your call against the dark."* This is the only authored in-fiction vocabulary the towers currently own. |
| `Assets/Resources/Data/Canonical/structures-catalog.json` | The live display-name field for every buildable, checked for collisions. |
| `Assets/Resources/Data/Canonical/towers.json` | The legacy arcane ladder: **Arcane Spire / Runed Spire / Warded Spire**. Checked for collisions. |
| The four `Assets/Resources/Towers/*.asset` files | The real stats, read directly, quoted below. |
| `docs/narrative/QUEST_CAST_VOICES_2026-08-03.md` | House conventions -- ASCII only, `--` not an em dash, DRAFT banner, owner-questions listed rather than decided. |

**Dash convention:** ASCII only throughout (the build TMP font renders non-ASCII as tofu on device).
I use `--`. Where I quote a shipped string that contains a real em dash, I mark it `[EM DASH]` rather
than reproducing the byte -- and that byte is itself a defect, logged in the sweep below.

---

## TASK 1 -- naming the starter tower

### The stats, read from the assets (not from comments)

| Asset | `towerName` | Cost | Build time | Damage L1/L2/L3 | Range L1/L2/L3 | Skill gate |
|---|---|---|---|---|---|---|
| `DevTower.asset` | **`DevTower`** | **0** | **2s** | **6 / 9 / 12** | **8 / 10 / 12** | none |
| `ArcherTower.asset` | `Archer Tower` | 150 | 5s | 22 / 35 / 50 | 18 / 20 / 22 | none |
| `FrostTower.asset` | `Frost Tower` | 200 | 5s | 8 / 14 / 22 | 12 / 14 / 16 | type 1, L1 |
| `MageTower.asset` | `Mage Tower` | 220 | 5s | 18 / 28 / 40 | 14 / 16 / 18 | type 3, L1 |

Read plainly: this thing is **free**, raises in **two seconds**, has the **shortest reach in the game**,
and hits for roughly a **quarter** of what an Archer Tower hits for. It is not broken and it is not a
stub -- it is a fully authored *humble first defence*, and it happens to be the first structure a
player ever places. The name is the only thing wrong with it.

### The naming brief I worked to

1. **Do not out-write the siblings.** Archer / Frost / Mage Tower are plain, functional, two words,
   `<one word> Tower`. A starter called "Bulwark of the First Watch" would make the *cheapest* tower
   the most ornate name on the screen. Any candidate must be `<one word> Tower`.
2. **Read as a starter, not as a joke and not as a placeholder.** "Basic Tower", "Starter Tower" and
   "Simple Tower" all fail -- they read like the dev never came back.
3. **Fit Elarion as it is authored today** -- world-Tree canon, no king, no soldiers, the Folk
   remember how to raise exactly one defensive structure. No new lore, no new places, no new factions.
4. **No collision** with any live display string. I checked all 28 `displayName` values in
   `structures-catalog.json` and the three level names in `towers.json`.

### The three candidates

**1. Watch Tower** -- *recommended*

The Folk have no king, no court and no standing army (`STORYLINE.md` section 2) -- what a village in that
position keeps is a **watch**. That makes "Watch" parse the same way "Archer" and "Mage" do: the word
names *who mans it*, so it sits in the row without a seam. It also earns the stats honestly -- at range
8 and damage 6 this tower is more early-warning than firepower, so the name and the numbers agree, and
a player reading it expects a modest thing and gets one. Plain, unmistakably the first rung, and
nobody has ever mistaken "Watch Tower" for a working title.

**2. Ward Tower**

The strongest *canon* pull of the three: `en.json` already ships **ward-stones** as the in-fiction
thing a tower holds (`tooltip.buttonTowers.body`, line 196), so this name is sourced rather than
invented and quietly teaches the vocabulary at the exact moment the player places their first tower.
It costs one thing: "ward" describes what *every* tower does, so it reads as generic-defence rather
than specifically-first, and it sits one syllable away from `towers.json`'s legacy level-3 name
**"Warded Spire"** -- harmless today, but a real collision if that ladder is ever surfaced.

**3. Stone Tower**

The plainest option, and the one that leans on a pattern the catalog already established: the basic
tier of a thing is named for the material it is made of -- **Stone Wall**, **Stone Gate** (and
**Wooden Palisade** below them). "Stone Tower" slots into that family instantly and needs no
explanation at all. The risk is that a material-word in a display field is exactly what a placeholder
looks like, which is the defect class we are here to close -- and "Stone" says nothing about a tower
being *first*, only about what it is made of.

**Also considered and set aside:** *Sentry Tower* (right meaning, but reads as sci-fi/tower-defense
genre rather than Elarion), *Guard Tower* (implies guards the Folk do not have), *Signal Tower*
(implies a signalling system that is not authored), *Hearth Tower* (borrows the Flame Pup's hearth
motif and over-writes the siblings).

### Recommendation

> ### **Watch Tower**

It is the only candidate that is simultaneously plain enough to disappear beside Archer / Frost / Mage,
specific enough to read as the *first* one, and true to the stats a player will actually feel. Ward
Tower is the strong second and the better pick if the owner wants the ward-stone vocabulary taught
early -- the two are close, and this is a creative call, not a correctness one.

### Implementation notes for whoever takes the eventual WO (NOT authorized by this file)

- **Change `towerName` only. Do not rename the asset, the file, or `m_Name`.** This is
  DESIGN-DECISIONS **#21** applied exactly: `pet-house` stayed an id while it became "Echo Hollow".
  `BuildMenuVM.cs:98` pins the Resources path `"Towers/DevTower"`, `TowerConstructionQueue.cs:95`
  builds the GameObject name from it, and a rename would break the load for zero player benefit.
  The internal id `DevTower` is not a defect. The **display string** is.
- `PlacedTowerListVM.PrettifyTowerName` is documented idempotent on an already-clean name
  ("Archer Tower" returns unchanged), so an authored `Watch Tower` passes through untouched. No
  formatting change is needed, and the existing camel-hump split stops being load-bearing.
- **Separate defect, separate decision:** even after the rename, `BuildMenuVM.cs:98` means a player who
  selects **Archer Tower** in the build menu still places this asset and sees the starter name in the
  placed-tower list. Renaming makes the screenshot safe; it does not make the menu honest. See
  owner question **Q1**.

---

## TASK 2 -- sweep for the same defect class

Scope: `Assets/Resources/Data/Canonical/**` (all 68 JSON files) and `Assets/Resources/**/*.asset`
(all 18). Method: display-bearing fields only (`displayName`, `name`, `title`, `label`, `description`,
`text`, `body`, `tooltip`, `towerName`, `abilityName`), cross-checked against the C# consumer wherever
reachability was in doubt.

**The DESIGN-DECISIONS #21 filter was applied throughout.** Internal ids are NOT reported. `pet-house`,
`collector_lumbermill`, `arcane-tower`, `tower_ground_archer`, `blink_armor_*` as *ids*, the
`BuildingType.PetHouse` enum, the `"PetHouse"` Yarn node -- all deliberate, all excluded. Only strings
a player reads on screen are below.

### CONFIRMED player-facing -- fix before the store listing

**P1. Every tower a player builds is called "Dev Tower"** *(the trigger; severity: highest)*

- `Assets/Resources/Towers/DevTower.asset:15` -- `towerName: DevTower`
- `Assets/_Modules/Village/Buildings/UI/BuildMenuVM.cs:98` --
  `PlacedTowerResourcePath = "Towers/DevTower"` (const; every menu placement resolves it at line 276)
- `Assets/_Modules/Village/Buildings/UI/PlacedTowerListVM.cs:137-141` -- `DisplayNameFor` reads
  `Data.towerName` and runs it through `PrettifyTowerName`, which splits the camel hump
- **What a player sees:** they tap Build Tower, pick "Archer Tower", and the placed-tower list then
  shows **"Dev Tower"**. Same for Frost and Mage. There is no path to a correctly-named tower.
- The authored `ArcherTower` / `FrostTower` / `MageTower` assets have correct `towerName` values and
  are never loaded by the live placement path.
- **Confidence: HIGH** (asset + call site + display site all read directly).

**P2. Two quest NPCs literally say "PLACEHOLDER LINE"**

- `Assets/Resources/Data/Canonical/dialogue/dialogues.json:1377` -- Village Elder:
  `"PLACEHOLDER LINE - awaiting owner-approved copy. Quest elarion.welcome, stage meet-elder."`
- `Assets/Resources/Data/Canonical/dialogue/dialogues.json:1393` -- Fenn Wildmane:
  `"PLACEHOLDER LINE - awaiting owner-approved copy. Quest vendor.stable, stage train-ability."`
- Both nodes' own `_note` fields (lines 1369, 1385) say "TEXT IS A PLACEHOLDER". Nothing gates or
  filters these before display.
- **What a player sees:** the literal sentence above, spoken in the dialogue panel, on the
  `elarion.welcome` starter quest -- i.e. **in the first few minutes of a new game**.
- **Approved copy already exists** in `docs/narrative/QUEST_CAST_VOICES_2026-08-03.md` (both speakers
  are in its six-character scope), pending the same owner sign-off this document is pending. This is
  the cheapest fix on the list: sign off that doc and it is a data swap.
- **Confidence: HIGH**

**P3. A readable dungeon lore stone shows "[PLACEHOLDER -- NOT CANON]"**

- `Assets/Resources/Data/Canonical/lore-fragments.json:79` -- fragment `journal-vault`, `body[1]`
  opens with `[` + `PLACEHOLDER` + `[EM DASH]` + `NOT CANON]` and continues
  *"...The Hidden Vault is a Unity-side expansion room; this fragment has no verbatim source in the
  narrative bible. Source from the narrative team before ship, or cut the journal-vault stone."*
- The row sets `"placeholder": true` (line 75), but that flag is only surfaced as an optional dev
  warning -- nothing gates display on it.
- Reachable: `journal-vault` is wired into `dungeons/healers-cottage.json:256`, placed by
  `Assets/Editor/DungeonSceneBuilder.cs:1042` into the `hidden-vault` room, and a
  `LoreStone_journal-vault` object exists in `Assets/Scenes/Dungeon_HealersCottage.unity`.
- **What a player sees:** they read the stone titled *"A carving, and a struck-through draft"* and the
  second paragraph is a note from the development team to itself.
- **Two additional problems in the same string:** (a) it is signed to **Alduin the Mournful**, so a
  named canon antagonist appears to be speaking build notes; (b) the `[EM DASH]` byte is **non-ASCII**
  and will render as **tofu** on device per the glossary's own authoring law. The paragraph needs
  replacing or the stone needs cutting -- see owner question **Q2**.
- **Confidence: HIGH**

**P4. The Mage's default starting armor is called "Dragonic (Blink)"**

- `Assets/Resources/Data/Canonical/armor.json:447` -- `"id": "blink_armor_dragonic"`,
  `"name": "Dragonic (Blink)"`
- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs:966` -- `HeroClass.Mage => "blink_armor_dragonic"`
  is the **default equipped armor for every new Mage**
- `Assets/_Modules/Village/Hero/EquipVM.cs:352,469` -- binds the raw `.name` straight into the
  on-screen item VM
- **What a player sees:** a new Mage opens Equipment and their chest slot reads **"Dragonic (Blink)"**
  -- "Blink" being the art pack, and "Dragonic" being its file label, not a word.
- This is a **display name**, not an id, so #21 does not cover it. The other two class defaults were
  given real names -- `blink_armor_centurion` shows "Centurion Harness", `blink_armor_beasthunter`
  shows "Beasthunter Garb". Only the Mage default was missed. A name in that register is a one-line
  fix; I have not proposed one here because the owner may want the Mage's starting fiction to say
  something specific. See owner question **Q3**.
- **Confidence: HIGH**

### FLAGGED -- same defect class, believed unreachable today

**P5. ~60 catalog rows carry raw art-pack labels as their display `name`**

- `Assets/Resources/Data/Canonical/weapons.json` -- 59 rows such as `"Axe1h 12 (Blink)"` (line 604),
  `"Bow2h 01 (Blink)"` (line 979), `"Sword2h 01 (Blink)"` (line 2088), `"Shield1h 04 (Blink)"`
  (line 1421)
- `Assets/Resources/Data/Canonical/armor.json:474` -- `"Basic1 (Blink)"`
- These are deliberately kept off vendor shelves by a data-driven `excludeIdPrefixes: ["blink_"]`
  filter (`vendors.json:12`, honoured in `VendorStockResolver.cs:233-276`, whose own comment calls
  them "~65 art-pack placeholder rows"). No loot-table or crafting-recipe reference was found.
- **Why it is still flagged:** the exclusion comment notes these stay *ownable and equippable*, and
  **P4 proves one of them already leaks** -- through a default-equip path the vendor filter never sees.
  The filter guards the shop; it does not guard the inventory. Any future grant, reward, debug tool or
  starting-kit path that names an item by its catalog `name` will surface these verbatim.
- **Confidence that they are player-facing TODAY: LOW-MEDIUM.** Confidence that the defect class is
  real: high -- P4 is the existence proof. Recommend a display-name pass or a hard "never nameable"
  guard rather than relying on one vendor-side filter.

**P6. A wallet label reading "Dev / Staging wallet"**

- `Assets/Resources/Data/Canonical/wallets.json:23` --
  `"label": "Devnet Pack-Purchase Recipient (Dev / Staging wallet)"`
- `WalletEntry.Label` is deserialized but has **zero consumers** -- `PackStore.cs:268` builds its own
  `"Rewards Distributor - {address}"` string instead. **Not player-facing today.**
- But `wallets.json`'s own `_schemaNotes` (line 8) says this label is intended to be *"shown verbatim
  in the v2 store/settings transparency line"*. If that wiring lands unedited, the store page shows a
  string containing the word "Dev" -- on the monetization surface, which is the worst possible place
  for it given the dApp Store context.
- **Confidence: LOW (dormant).** Logged so it is not discovered by a reviewer.

### CHECKED AND CLEAN -- coverage proof

These looked like hits and are not. Recorded so the sweep does not get re-run on them.

- **`buildings.json`** displayName values (`crystalMine`, `farm`, `petHouse`, `workshop`,
  `arcaneTower`, `lumbermill`, `forge`, `market`, `jeweler`) -- camelCase in a field named
  `displayName`, but the file's own schema note (line 15) says these are **lookup keys** into
  `canon-strings.json`, and they resolve correctly (`petHouse` -> "Echo Hollow",
  `arcaneTower` -> "Cathedral of Magic"). This is DESIGN-DECISIONS #21 working as designed. Not a defect.
- **`widget-params.json`** -- "Placeholder" object names and "Lorem ipsum" strings. Inert extraction
  metadata: `ElarionUiKitObsidian.cs:213-222` deserializes only geometry fields; there is no
  `text`/`content` member on the C# side, so none of it is ever read. "Placeholder" is also just the
  stock Unity uGUI InputField child name.
- **`scene-configs.json:6`** -- `"displayName": "player-facing name"` sits inside the `_schema`
  documentation block, not a live record.
- **`canon-strings.json:75`** -- `_namesNotInSources` is a meta note about verification status.
  (It does flag Bryn / Mara / Tovin / Eira / Aelf / Mira as unverified against the narrative sources.
  Bryn currently ships as a live dialogue speaker. Not a placeholder-string defect, but see **Q4**.)
- **All 18 `Assets/Resources/**/*.asset` files** -- `ArcherTower` / `FrostTower` / `MageTower` all
  correct; `Dungeons/FolksGranary.asset` and `Dungeons/HealersCottage.asset` carry proper
  `DisplayName`s ("Folk's Granary", "Healer's Cottage"); the font, VFX catalog, VFX manifest, enemy
  VFX set and DOTween assets have no display-name field at all. **`DevTower.asset` is the only
  `.asset` defect in the project.**
- **The remaining 60+ Canonical JSON files** were scanned for display-bearing fields and read as
  intentional, lore-appropriate English: abilities, accessories, barracks, build-categories,
  building-tiers, chat-phrases, consumables + recipes, cosmetics, crafting/gear/jeweler/garrison
  recipes, daily-quests, damage-states, difficulty-profile, dungeon-graphs, dungeon-layouts,
  echoes-balance, en, enemies, enemy-roles, gear-levels, glossary, guide-content, heart, hero-talents,
  hud-areas, loot-tables, materials, motion-castings, packs, pets, population-milestones, quests,
  realm-map, skin, stake-rewards, structures-catalog, themes, tower-perks, towers, troops,
  troop-upgrades, tutorial-steps, vendors, walls, waves, weaponskill-animations.

### Ledger at a glance

| # | Where | What a player sees | Reachable now |
|---|---|---|---|
| P1 | `Towers/DevTower.asset:15` + `BuildMenuVM.cs:98` | "Dev Tower" on every tower they build | **Yes** |
| P2 | `dialogue/dialogues.json:1377, 1393` | "PLACEHOLDER LINE - awaiting owner-approved copy." | **Yes -- first-hour quest** |
| P3 | `lore-fragments.json:79` | "[PLACEHOLDER ... NOT CANON]" on a lore stone, signed Alduin | **Yes** |
| P4 | `armor.json:447` | "Dragonic (Blink)" as the Mage's starting armor | **Yes** |
| P5 | `weapons.json` (59 rows), `armor.json:474` | "Axe1h 12 (Blink)" etc. if ever granted | Not today |
| P6 | `wallets.json:23` | "Dev / Staging wallet" in a future store line | Not today |

---

## Owner questions -- canon does not answer these, so I did not choose

**Q1. Should the build menu place the tower the player picked?**
`BuildMenuVM.cs:98` is a hard-coded const, so Archer / Frost / Mage exist as authored assets that the
live path never loads. Renaming `DevTower` makes the screenshot safe; it does not make the menu honest.
Is the single-tower behaviour intentional for this build (and the starter tower is simply *the* tower),
or is the pin a leftover that should be routed through the selected catalog row? This is a design call
with a code consequence -- it needs a WO either way, and the answer changes what the starter's name has
to carry.

**Q2. The `journal-vault` lore stone -- replace the paragraph, or cut the stone?**
The row's own note says paragraph 1 is canon-verbatim and paragraph 2 has no source, and offers both
options. Cutting is safe and free. Replacing needs new prose from the owner, and the entry is signed to
**Alduin the Mournful**, whose voice is not something I will invent. Which?

**Q3. What should the Mage's starting armor be called?**
The Knight/Cleric and Ranger defaults read "Centurion Harness" and "Beasthunter Garb" -- plain, one
adjective plus a garment noun. The Mage default needs a name in that register. I have not proposed one
because the starting robe is the Keeper's first characterising object and the owner may want it to say
something specific (an inheritance, a Chorister's robe, a Heart-splinter tie). A name, or a licence to
draft three, would close this.

**Q4. Are Bryn / Mara / Tovin / Eira / Aelf / Mira ratified names?**
`canon-strings.json:75` flags all six as **not appearing in the narrative sources** -- "placeholder
values mirror the requested spelling; verify against canon before shipping". **Bryn already ships as a
live dialogue speaker.** This is not a broken-looking string and I have not counted it as a defect, but
it is the same "unratified thing reached the player" shape. Are they canon now by use, or does that
note still stand?

**Q5. Is the "Spire" vocabulary retired or not?**
`canon-strings.json:42` states the Spire / Chord / Lantern / Stone-Choir motifs are RETIRED, and the
world-Tree reversal supersedes the Spire premise -- yet `structures-catalog.json:882` still ships a
buildable named **"Arcane Spire"**, and `towers.json` still carries **Arcane / Runed / Warded Spire**
as a level ladder. Not a placeholder defect, so it is not in the sweep, but it is a live inconsistency
in player-facing strings that a dApp Store screenshot could catch. Flagging only; no action proposed.

---

_DRAFT -- owner sign-off required. No implementation is authorized by this document._
