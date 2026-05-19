# Avalon Dungeons — 3D Unity Layout Spec (KayKit Dungeon Remastered)

**Status:** Canonical construction spec for the 7 Avalon dungeon scenes in Unity. Supplements the existing narrative-and-lore dungeon designs (`docs/dungeon-3d-healers-cottage-design.md` and the six v1.1 dungeon design docs from the agent run). This spec is the **how-to-build-in-Unity** layer; the existing specs are the **what-the-player-feels** layer.
**Owner:** DeNelle Studios
**Date:** 2026-05-18
**Spec source:** Owner direction 2026-05-18 — _"create similar spec sheet for dungeon using files here and being as creative and open as wanted. large challenging maps with good depth are encouraged."_
**Asset pack:** KayKit Dungeon Remastered Pack 1.0 (FBX (Unity) format imported at `Assets/Models/KayKit/dungeon/`) — 211 models, single 1024×1024 atlas, CC0 license.

---

## 1. The vision in one paragraph

Dungeons in Avalon are **real explorable 3D spaces**, not corridors-with-encounters. Each one is **large, vertical, and deep** — multiple levels, hidden passages, locked sub-areas, real chambers with real props. The Keeper walks them with the Lantern radius modulating visibility; the Hollow Ones lurk where the light doesn't reach. Each dungeon's geometry expresses its narrative: the Healer's Cottage is a small home that hides a deep cellar; the Apothecary's Vault is a sprawling underground complex; the Wolfwarden's Vigil rises through a stone tower; the Cold-Wandered's Pack is a branching ice-cave network; the Last Keeper's Walk is a linear pilgrimage; At the Edge is a vertical descent into the Wound. The KayKit Dungeon Remastered pack supplies every wall, floor, door, stair, prop, and atmospheric piece needed to build these without commissioning new art.

The mood is **lived-in fairy-tale dark**, not horror. Old stone, dust on the shelves, candles long burned out. The Hollow Ones are grief that walks; the Keeper meets them with the lantern, not the sword.

## 2. What's canon-locked vs creatively free

**Canon-locked (every dungeon MUST have):**
- An **entrance room** matching the lore (e.g., Healer's Cottage = garden approach + front door; Wolfwarden's Vigil = ground floor of the watchtower)
- A **Wanderer (Bryn) presence** at the entrance via speech bubble (per `docs/dungeon-tension-spec.md` §4)
- 4–6 **lore stones** carrying the questline's journal entries (existing narrative specs lock the copy)
- A **mini-boss** at the dungeon's narrative climax (named, themed, mechanic per existing per-dungeon spec)
- A **clear exit** that returns the Keeper to Avalon and advances the questline beat
- **Lantern mechanic integration** — base 6u visibility + Lightbearer-piece bonuses per `docs/dungeon-tension-spec.md` §5
- **Checkpoint shrines** if the v1.1 checkpoint system has shipped by build time; otherwise simplified HP/MP reset on combat resolve
- All canon names: **Avalon**, **Elarion**, **Blaise**, **Alduin the Mournful**, **Bryn**, **Mira**, the **Hollow Ones**, the **Wound**

**Creatively free (Unity agent's call):**
- Total room count (≥6 per dungeon; encouraged 10–18 for larger / deeper feel)
- Vertical structure (number of levels, stair placement, vault depth)
- Hidden passages, secret rooms, trap rooms, locked sub-areas
- Specific KayKit asset choices within the per-theme palette
- Optional puzzle mechanics (the Lantern light reveals hidden glyphs; weighted floor plates trigger doors; etc.)
- Encounter density beyond the locked scripted fights (random encounters per the encounters spec, when v1.1 lands)
- Decorative density (more props = more lived-in feel; spend the budget)
- Lighting design (which torches lit, which candles snuffed, where the Lantern is the only light)
- Optional ambient creatures (rats in cellars, bats in upper floors — purely visual, no gameplay)

## 3. The render approach — Unity Scenes per dungeon, room-based prefab construction

Each dungeon is its own **Unity Scene file** (e.g., `Assets/Scenes/Dungeons/Dungeon_HealersCottage.unity`). Scenes are constructed from **modular room prefabs** built from KayKit Dungeon Remastered pieces:

- **Wall pieces** assemble room perimeters (`wall_straight`, `wall_corner`, `wall_T`, `wall_archway`, `wall_window`)
- **Floor tiles** carpet the floor (`floor_tile_small`, `floor_tile_large`, `floor_tile_grate`, `floor_tile_decorated`, broken variants)
- **Ceiling tiles** close the top for true enclosed feel (`ceiling_tile`, `ceiling_arched`)
- **Doors** transition between rooms (`door_wood`, `door_iron`, `door_barred`, `door_arched`, opened/closed variants)
- **Stairs** transition between levels (`stairs_small`, `stairs_large`, `stairs_lessteep`)
- **Furniture + props** fill the space (`table_wood`, `chair_wood`, `bed_wood`, `bookcase`, `chest`, `barrel`, `crate`)
- **Atmosphere** sets mood (`banner_*`, `candle_unlit`, `candle_lit`, `torch_lit`, `cobweb`, `dust_pile`, `bloodstain` — yes the pack includes this, but use sparingly per cozy register)
- **Floor traps** for trap rooms (`floor_tile_big_spikes`, `floor_tile_big_grate`, `floor_tile_big_grate_open`)
- **Water tiles** for sewer / well sections (`water_puddle`, `water_full`)

Each room is a **Unity Prefab** so they can be reused across dungeons (e.g., the "lore stone room" prefab variant appears in multiple dungeons, with different journal text content per usage).

Construction is **NOT auto-generated**. The Unity agent (or human level designer) places prefabs by hand or via an Editor script that reads a per-dungeon JSON layout spec (similar to `data/realm-map.json`). Each dungeon gets its own JSON if data-driven layout proves cleaner than scene-baked.

## 4. Vertical depth — make every dungeon multi-level

The bible voice loves quiet stakes. **Verticality reinforces stakes** because going down feels heavier than going forward. Every dungeon should have **at least 2 levels**; most should have 3+. Stairs are cheap (KayKit ships several variants) and pay back enormously in spatial richness.

### 4.1 Standard vertical patterns

**Pattern A — Surface + Cellar (Healer's Cottage, Folk Who Forgot)**
- Ground floor + 1 underground level
- Underground entry via trapdoor (rug overlay) OR via visible stairs in a back room
- Underground is lantern-mandatory (no ambient light)

**Pattern B — Multi-Story Tower (Wolfwarden's Vigil)**
- 3-4 levels going UP (ground / mid / belfry / roof)
- Stairs visible from the entry (a clear "climb the tower" affordance)
- Each level smaller than the one below (architectural narrowing)

**Pattern C — Sprawling Underground (Apothecary's Vault, At the Edge)**
- Ground entry leads to expansive underground complex
- 2-3 sub-levels, each branching
- Vaults, crypts, alchemy labs at the deepest level

**Pattern D — Cave Network (Cold-Wandered's Pack)**
- No clear "level" divisions; topology is organic
- Branching paths at variable depths
- Some passages climb up to icy ledges; others descend to flooded sub-chambers

**Pattern E — Linear Vertical Descent (At the Edge)**
- The Wound is a literal descent into the unknown
- Successive levels going DOWN, each darker than the last
- No going back up except via the questline's payoff exit

### 4.2 Stairs as architecture, not just connectors

Stairs are visible from rooms they connect — the Keeper sees them, anticipates the next level. **No off-camera teleport between floors.** A stair leading down from the Main Room is part of the room's silhouette; the player approaches it physically. KayKit's stair pieces are designed for this.

**Stair varieties from the pack:**
- `stairs_small` — single-flight, low-rise. Use for short level transitions.
- `stairs_large` — longer flight, more imposing. Use for major level changes (entrance to underground).
- `stairs_lessteep` — gentler grade. Use for outdoor approaches or formal grand stairs.

### 4.3 Vertical landmarks

Each multi-level dungeon should have at least **one vertical landmark** — a feature visible from multiple floors:
- A central well or pit in the Apothecary's Vault, dropping through 3 levels
- A bell tower shaft in the Wolfwarden's Vigil, with the bell visible from below
- A frozen waterfall in the Cold-Wandered's Pack, falling between cave levels
- The Wound itself in At the Edge, perpetually descending past the camera

These landmarks anchor the player's spatial memory and pay off when the player progresses past them.

## 5. Room taxonomy — the 8 archetypes every dungeon uses

Each room belongs to one (or sometimes two) of these archetypes. The Unity agent uses this taxonomy to decide what to build:

### 5.1 Entrance Room
- First room the Keeper sees on entering
- Wanderer (Bryn) present — speech bubble fires on proximity
- Lighting: ambient sun OR torchlit (warmer than deeper rooms)
- Props: doormat, coat-on-peg, dust on the floor — "someone lived here once"
- Optionally: a lore stone for journal entry 1
- **No combat**

### 5.2 Lore Room
- Quiet beat — the player reads a journal entry, looks at a child's drawing, finds a memento
- 1 lore stone (tap-to-read)
- Optional environmental storytelling props (an opened book, a half-empty wine cup, a single dried flower in a vase)
- Lighting: low, intimate. One lit candle or torch.
- **No combat in dedicated lore rooms.** Lore stones can also appear in combat rooms after the fight ends.

### 5.3 Combat Room
- 1-3 Hollow Ones spawn here
- Room large enough for the ATB battle transition to read clearly (~6×6 hex floor tiles minimum)
- Cover props (pillars, knocked-over tables) for visual interest
- Lighting: medium — enough to see the enemies but not so bright the Lantern feels redundant
- Often: a lore stone after the combat resolves

### 5.4 Treasure Room
- Contains a chest with meaningful reward (SKR, Soul Embers, equipment piece, crafting shard)
- Often **hidden** — accessed via trapdoor, secret passage, or lantern-revealed wall
- Lighting: dark, lantern-required
- Cobwebs, broken crates, dust piles — disused
- Optionally: 1 weak enemy guarding the chest (lore: a Hollow One bound to the treasure)

### 5.5 Trap Room
- 1-2 dangerous floor tiles (`floor_tile_big_spikes`, `floor_tile_big_grate_open`)
- Visible warning (a Hollow corpse on the spikes; a broken floor pattern)
- The Lantern reveals the safe path; without good light, the player blunders into traps
- Light damage on trigger (~15% HP); the trap rooms are about **tension**, not death
- Reset after triggering — the Keeper doesn't permanently break the trap

### 5.6 Puzzle Room (optional, per dungeon)
- Light-based puzzle (the Lantern reveals a hidden inscription)
- Weighted floor plate triggers a hidden door
- A switch that opens a passage
- Avoid heavy puzzles (cozy register doesn't tolerate Zelda-grade complexity); ONE light puzzle per dungeon is plenty
- Reward: opens a treasure room OR an alternate boss approach

### 5.7 Checkpoint Shrine Room
- A small alcove or chamber containing a checkpoint shrine
- Per `docs/dungeon-encounters-and-checkpoints-spec.md` §4 — heals HP/MP + saves run state
- Visually distinct — a stone pedestal with a glowing crystal, two flowers and two candles (perpetually lit), warm violet ambient light
- Almost always near the entry + before the boss room

### 5.8 Boss Room
- Largest room in the dungeon, climax space
- Sufficient floor area for ATB battle staging (8×8 hex minimum)
- Visual identity matches the boss's theme (an alchemy bench for the Apothecary's Apprentice, a bell tower for the Wolfwarden's Echo, a Wound-edge for At the Edge)
- Pre-battle silence beat (audio mix nudges dungeon track to 0.0 — per `docs/audio-mix-spec.md` §4)
- Post-battle: reward chest + journal final entry + exit door

## 6. The KayKit Dungeon Remastered asset palette by theme

The pack supports 4-5 distinct visual themes depending on prop + lighting choice. Mix-and-match for per-dungeon character.

### 6.1 Cozy-domestic (Healer's Cottage)
- Wood-plank floor tiles, plaster walls
- Furniture: tables, chairs, bed, bookcase, kitchen items
- Atmosphere: hanging herbs (`prop_herb`), copper pots, dried flowers
- Lighting: warm candle + fireplace
- Color register: amber, honey, soft brown
- **Asset density: medium** — homey but not cluttered

### 6.2 Apothecary-workshop (Apothecary's Vault parts)
- Stone floor tiles, stone walls
- Furniture: alchemy bench, vial rack, drawer wall, writing desk
- Atmosphere: bottles, glassware, books, alchemical residue stains
- Lighting: dim — one lit candle by the desk, the rest cold
- Color register: muted teal, faded violet, cracked tan
- **Asset density: high** — packed with apothecary objects

### 6.3 Stone-fortress (Wolfwarden's Vigil, Last Keeper's Walk)
- Dungeon stone floor + walls
- Furniture: barracks beds, weapon racks, banners
- Atmosphere: shields-on-walls, watch banners, hung furs
- Lighting: brazier-lit (warm but martial)
- Color register: cool grey, deep blue banners, soft amber braziers
- **Asset density: medium-low** — disciplined, austere

### 6.4 Cave-natural (Cold-Wandered's Pack)
- Use `floor_tile_large_rocks` heavily, `wall_natural_stone` if available
- Minimal furniture (the Pack doesn't decorate)
- Atmosphere: ice props if any (else custom shader on stone — Unity material with snow effect)
- Lighting: blue-cold light, narrow Lantern radius
- Color register: ice-blue, frost-white, bone-grey
- **Asset density: low** — natural, sparse

### 6.5 Ruined-village (Folk Who Forgot)
- Broken floor tiles (`floor_tile_small_broken_A`, `_broken_B`), partial walls
- Furniture: overturned tables, smashed chairs, half-buried chests
- Atmosphere: heavy cobwebs, dust piles, debris everywhere
- Lighting: faint daylight from holes in the ceiling, plus the Lantern
- Color register: faded green moss, weathered grey, earthen brown
- **Asset density: high but chaotic** — like time stopped mid-collapse

### 6.6 Cosmic-void (At the Edge)
- Minimal physical structure — the Wound is mostly empty space
- Stone platforms floating in darkness
- No KayKit props for the deepest level — pure shader work + particle systems
- Color register: deep violet, void-black, occasional gold star-glints
- **Asset density: minimal** — the absence is the point

## 7. Lighting design — the lantern is the rule

Inside dungeons, **the Lantern is the dominant light source**. Ambient light is set very low (~0.05 intensity). The Keeper's PointLight (radius 6u + Lightbearer bonuses) defines what's visible.

**Lighting exceptions:**
- Entrance rooms get partial ambient (sun from the doorway)
- Rooms with lit torches / candles have small static PointLights at those props
- Checkpoint shrines emit ambient violet glow (~3u radius)
- Boss rooms get a dramatic lighting setup — maybe one strong rim light from a specific direction, suggesting drama
- Lit fixtures (`torch_lit`, `candle_lit`, `brazier_lit`) cast small light pools (~2u radius each)

**Visual reward for the player:** lighting a fresh torch (interaction prompt on `candle_unlit` / `torch_unlit`) adds a small persistent light to the room. This is a **discoverable affordance** — players who light torches see more.

## 8. Encounter density — the 60/20/20 rule

Per dungeon, distribute room types roughly:
- **60% non-combat rooms** (lore, treasure, puzzle, dressing, checkpoint, corridor) — the Keeper walks and reads more than fights
- **20% combat rooms** (scripted encounters per the existing per-dungeon specs)
- **20% trap / interactive / puzzle rooms** (the Lantern reveals; the floor breaks; the door locks; etc.)

For a 12-room dungeon: ~7-8 quiet rooms + ~2-3 combat + ~2 trap/puzzle. This keeps the cozy register dominant.

Random encounters from the v1.1 encounter spec layer on top — they fire in corridors and large rooms, not in dedicated lore / checkpoint / treasure rooms.

## 9. The Healer's Cottage — concrete instance, expanded for v1

The first dungeon. Owner directive: "large + challenging + deep." Expanded from the original 6-room spec to **3 levels, ~12 rooms**.

### 9.1 Ground floor (6 rooms)

```
                    [LOFT BEDROOM]  ← reached via ladder from Main Room
                          ↑
   [Garden     ] → [Entrance Room ] → [Main Room      ] → [Kitchen       ]
   [Approach   ]   [+ trapdoor    ]   [+ ladder up    ]   [+ pantry alc. ]
   [(Bryn)     ]   [Lore Stone 1  ]   [Lore Stone 2   ]                  
        ↓                ↓                  ↓                  ↓
   [Outdoor   ]    [Trapdoor      ]   [Stair down to ]   [Pantry leads  ]
   [transition]    [hidden, lantern]  [Cellar       ]   [to Workshop   ]
                   [-gated         ]                                     
                                                              ↓
                                                        [Workshop     ]
                                                        [(boss room)  ]
                                                        [Lore Stone 4 ]
                                                        [Mini-boss    ]
```

- **Garden Approach** (entry) — outdoor, Wanderer Bryn speaks here, single weak Hollow encounter
- **Entrance Room** — dust + chair, trapdoor under rug (lantern-revealed), Lore Stone 1
- **Main Room / Hearth** — large central room, 2 Apprentice Hollow encounter, ladder up to Loft, stairs down to Cellar, Lore Stone 2
- **Kitchen** — pantry alcove, kitchen-themed dressing, no combat, leads to Workshop
- **Workshop** (boss room) — alchemy bench, Apprentice of the Apothecary mini-boss, Lore Stone 4, exit back to Avalon

### 9.2 Upper floor (2 rooms — reached via ladder from Main Room)

- **Loft Bedroom** — small bedroom over Main Room. Mira's bedroom. Lore Stone 3 (her journal entry). No combat.
- **Loft Study** — adjacent to bedroom, narrow attic-style. Window-pane lighting from above. A second hidden lore stone (M.M. + A.M. carving on the windowsill) revealed only with lantern light.

### 9.3 Underground (4 rooms — reached via trapdoor in Entrance Room OR stair in Main Room)

- **Root Cellar** — original spec. Damp, water puddle in corner. 1 Cellar-variant Hollow encounter (slow, sad). Treasure chest: Cloak of the Lightbearer.
- **Storage** — adjacent to Cellar. Crates + barrels. 1 weak Hollow. Secondary chest with Soul Embers.
- **Crypt Sub-Level** — hidden, lantern-revealed wall. Cooler than Cellar. Contains a stone sarcophagus (Mira's? — Alduin's? — narrative-ambiguous). Trap floor: spike-tile triggered by stepping wrong path. Pre-boss checkpoint shrine here.
- **Hidden Vault** — accessed via light-puzzle in Crypt (Lantern reveals hidden lever). Optional content. Contains: third journal entry (4-a, the redacted draft from the v1.1 spec), one rare crafting shard, narrative reveal.

### 9.4 Encounter + room budget summary

- **Total rooms: 12** (vs original spec's 6 — 2× depth)
- **Combat encounters: 5 scripted** (Garden, Main ×2, Cellar, Storage) + **1 mini-boss** (Workshop)
- **Lore stones: 5** (Entrance, Main, Loft Bedroom, Workshop, optional Hidden Vault)
- **Hidden rooms: 3** (Root Cellar [trapdoor], Hidden Vault [puzzle], Loft Study [lantern reveal])
- **Trap rooms: 1** (Crypt Sub-Level)
- **Checkpoint shrines: 2** (Entrance Room post-Bryn beat, Crypt pre-boss)
- **Estimated playtime: 25-35 minutes** for a first-time Keeper (vs original 12-18 min — depth pays back in screentime)

### 9.5 Asset palette mapping

Uses the **cozy-domestic** palette (§6.1) for ground floor + upper. Switches to **apothecary-workshop** (§6.2) for the Workshop boss room. Underground (§4.1 Pattern A) uses **stone-fortress austere** (§6.3) with **cave-natural** (§6.4) accents in the deepest hidden vault.

## 10. Per-dungeon supplements — D2 through D7

Each refers back to the existing per-dungeon design docs for narrative + lore. This section adds the **Unity build expansion** (room count, vertical structure, asset palette).

### 10.1 D2 — Apothecary's Vault

- **Pattern C — Sprawling Underground** (per §4.1)
- **Levels: 3** (entry hall + alchemy floor + deep vault)
- **Room count: 14-16**
- **Vertical landmark: a central pit** with the entry hall's stairs spiraling down past three balcony alcoves
- **Asset palette: apothecary-workshop** (§6.2) — bottles, glassware, alchemy benches everywhere
- **Combat density: 4 scripted + 1 mini-boss** ("The Vault Keeper" per existing D2 spec)
- **Lore stones: 6** (carries journal entries 5 + 6, plus 4 environmental beats)
- **Hidden content: 2 sealed cabinets** (one lantern-gated, one weighted-floor-plate-gated)

### 10.2 D3 — Wolfwarden's Vigil

- **Pattern B — Multi-Story Tower** (per §4.1)
- **Levels: 4** (ground / mid / belfry / roof)
- **Room count: 10-12** (smaller per-level footprints than the sprawling vaults — towers are vertically dense, horizontally narrow)
- **Vertical landmark: the bell shaft** running through all 4 floors, visible from each level
- **Asset palette: stone-fortress** (§6.3) — barracks beds, weapon racks, fur cloaks, banners
- **Combat density: 4 scripted + 1 mini-boss** ("The First Wolfwarden" per existing D3 spec)
- **Lore stones: 5** (Ice Wolf bond progression beats)
- **Special: a bell mechanic** — ringing the bell on the belfry floor calls the boss to fight you outdoors on the roof OR triggers the climactic conversation. Player's choice.
- **Hidden content: a wolf-bone shrine on the roof** (visible only after the bell mechanic resolves)

### 10.3 D4 — Folk Who Forgot

- **Pattern A — Surface + Cellar variation** (but the "surface" IS underground — the ruined village exists in a vast cavern)
- **Levels: 2** (the open ruined-village floor + the well beneath the village square)
- **Room count: 15-18** (the ruined village is sprawling — many open-air-style "rooms" that are actually wall-fragments suggesting former buildings)
- **Vertical landmark: the village well**, the source of the corruption, deep with dark water
- **Asset palette: ruined-village** (§6.5) — broken floor tiles, partial walls, cobweb-heavy
- **Combat density: 5 scripted + 1 mini-boss** ("The Inn-Keeper" per existing D4 spec, tragic register)
- **Lore stones: 7** (one in each ruined building's remains: schoolhouse, mill, inn, several houses)
- **Special: the village layout mirrors Avalon's** — schoolhouse where the residential cluster sits, mill where the Mill sits, well where Avalon's plaza Well sits. The Keeper experiences a haunting déjà vu walking through it. **Optional creative addition:** a half-buried "Heart" — a dead world-tree skeleton at the village's centre, narrative payoff for the cosmology.

### 10.4 D5 — Cold-Wandered's Pack

- **Pattern D — Cave Network** (per §4.1)
- **Levels: variable** (organic — no clear floor divisions; some passages climb to icy ledges, others descend to flooded sub-chambers)
- **Room count: 12-15**
- **Vertical landmarks: a frozen waterfall** falling between cave levels + **the Old Alpha's grave** at the network's deepest point
- **Asset palette: cave-natural** (§6.4) — rocky floor tiles, minimal furniture, custom ice shader work
- **Combat density: 4 scripted + 1 mini-boss** ("The Mournful Alpha" per existing D5 spec — not hostile until the final encounter)
- **Lore stones: 4** (Ice Wolf origin beats — fewer because the cave network's emptiness IS the lore)
- **Drops: Lightbearer Bracer** (per the v1.1 spec — D5 is one of two tier-3 dungeons holding a Lightbearer piece)
- **Special: ice-puzzle** — the Lantern's warmth melts a frozen passage at one point. Visible ice mass that slowly thaws when the Keeper stands near with the Lantern lit for 30 seconds.

### 10.5 D6 — Last Keeper's Walk

- **Pattern E — Linear Vertical Descent** variation (but going FORWARD along Mira's path, not down)
- **Levels: 2** (Mira's cottage at the start, the Crossing at the end — the path between IS the dungeon)
- **Room count: 10-12** (sequential pilgrimage stops, mostly linear)
- **Vertical landmark: a single distant mountain visible from multiple stops along the path** — Mira walked toward it
- **Asset palette: stone-fortress austere** (§6.3) with **ruined-village** (§6.5) accents for Mira's final cottage
- **Combat density: 3 scripted + 1 final encounter** ("The Watcher" — Mira's grief, NOT Mira herself)
- **Lore stones: 6** (one per Mira's letter, per the existing D6 spec)
- **Drops: Lightbearer Circlet** (the second tier-3 piece, completes the set per v1.1)
- **Special: optional "Letter 3a" room** — a struck-through draft letter, lantern-revealed in a side cottage. The reveal "Mira was Alduin's wife" lands here in its most direct form per the existing D6 spec.

### 10.6 D7 — At the Edge

- **Pattern E — Linear Vertical Descent** (literal — going DOWN into the Wound)
- **Levels: 5+** (each level smaller and darker than the one above)
- **Room count: 8-10** (the Wound is more void than chamber — fewer enclosed spaces, more "stone platforms in darkness")
- **Vertical landmark: the Wound itself** — perpetually descending past the camera; the Keeper never reaches "bottom"
- **Asset palette: cosmic-void** (§6.6) — minimal physical structure, custom shaders, particle systems
- **Combat: NO traditional boss** — the climax is a **conversation with Alduin** at the Edge per the existing D7 spec
- **Lore stones: 3** (sparse — the descent IS the lore)
- **Special: four canonical response paths** to Alduin per the existing D7 spec. The "win" condition is choosing the response that fits the Keeper the player has been playing as.
- **Reward: NG+ unlock + the letter-to-the-next textarea**

## 11. Audio + ambient pass (per `docs/audio-mix-spec.md`)

- **Default music: dungeon track** (`echoes-beneath-elarion.mp3`) at 0.25 volume per audio mix spec
- **Lore stone read:** music dips to 0.12 for 6 seconds (per audio mix §4)
- **Pre-boss intro:** music dips to silence; battle track hard-cuts in when combat starts
- **Ambient sound design (per-dungeon):**
  - Healer's Cottage: distant wind, occasional creak, ticking clock
  - Apothecary's Vault: dripping water, bubbling reagents, occasional bottle clink
  - Wolfwarden's Vigil: wind through arrow slits, distant wolf howl, bell creak
  - Folk Who Forgot: distant village ghost-sound (faint laughter, children's playing — fades when Keeper approaches), wind through ruins
  - Cold-Wandered's Pack: ice cracking, dripping melt-water, distant wolf-pack call
  - Last Keeper's Walk: footsteps echoing on stone, distant chime, wind across the moor
  - At the Edge: cosmic hum (extremely low frequency), occasional whisper, the Wound's breathing

Sound design is **owner-provided** (or v2 commission). Stub silence for v1 build; spec the placements so the audio layer is ready when files land.

## 12. Wanderer (Bryn) integration

Per `docs/dungeon-tension-spec.md` §4 — Bryn appears at the dungeon select screen, NOT inside dungeons. Inside the dungeon, the entrance room has a speech bubble overlay carrying Bryn's per-dungeon line. The line is set by tier:

- Tier 1 (Healer's Cottage, Apothecary's Vault): _"The path opens easy, Keeper. But mind the rocks — they remember you."_
- Tier 2 (Wolfwarden's Vigil, Folk Who Forgot, Cold-Wandered's Pack): _"Bring a lantern. What's in there has been there longer than I have."_
- Tier 3 (Last Keeper's Walk, At the Edge): _"Don't go light. What's in there has teeth I haven't named."_

These lines fire once per save on first dungeon entry.

## 13. Checkpoint shrine integration

Per `docs/dungeon-encounters-and-checkpoints-spec.md` §4 — **2-3 shrines per dungeon**:
1. Entrance area (mandatory)
2. Mid-dungeon (1-2 per length of dungeon)
3. Boss antechamber (mandatory)

For the Healer's Cottage expanded layout (§9):
- Shrine 1: Entrance Room (after Bryn's intro beat)
- Shrine 2: Crypt Sub-Level (pre-boss, underground)

For larger dungeons (D2, D3, D4):
- Shrine 1: Entry chamber
- Shrine 2-3: Mid-dungeon transit rooms
- Shrine 4: Boss antechamber

## 14. Acceptance criteria (per dungeon)

The Unity agent confirms a dungeon is "done" when:

1. **Room count** matches or exceeds the supplement minimum for that dungeon (§10)
2. **Vertical structure** matches the pattern (§4.1)
3. **All lore stones are placed** with copy from the existing per-dungeon design doc
4. **All scripted encounters fire correctly** in their designated rooms
5. **The mini-boss room reads as climactic** (largest space, dramatic lighting, pre-battle silence beat)
6. **Hidden content is hidden** (trapdoor + lantern-reveal + puzzle work correctly)
7. **Checkpoint shrines function** (heal HP/MP, persist run state)
8. **Lantern PointLight illuminates correctly** — base 6u radius, ambient floor 0.05, sufficient darkness for tension without making navigation impossible
9. **Audio integration works** — dungeon track plays at 0.25, lore stones dip music, boss intro silence
10. **Bryn's per-tier line fires** on first entry
11. **Dungeon exit returns to Avalon** and advances questline beat correctly
12. **FPS holds 60 on Seeker target hardware** for a stationary scene + during a typical 5-minute playthrough
13. **No walk-through-walls** (all wall meshes have correct colliders per T60 lesson)
14. **Screenshot rendered** to `docs/screenshot-dungeon-{name}.png` for owner review

## 15. Decisions log entries (for `docs/unity-decisions.md`)

```
| 2026-05-18 | Dungeons expand from 6 rooms to 10-18 per dungeon | Stay at 6 rooms | Owner directive: "large challenging maps with good depth" | Yes — rooms can be cut if budget pressure |
| 2026-05-18 | Multi-level vertical structure per Pattern A-E | Single-level dungeons | Verticality reinforces stakes; KayKit stairs are ready | Yes — flatten if FPS pressure |
| 2026-05-18 | Healer's Cottage expanded to 3 levels, 12 rooms | Original 6-room single-level | Owner direction + Foundation pitch needs depth showcase | Yes |
| 2026-05-18 | 60/20/20 encounter density (quiet/combat/interactive) | 50/50 combat-heavy | Cozy register preserved at scale | Yes — tune per playtest |
| 2026-05-18 | Hidden content density: 2-4 hidden rooms per dungeon | None | Lantern mechanic needs payoff content; rewards exploration | Yes |
| 2026-05-18 | Trap rooms use floor_tile_big_spikes from KayKit | Custom traps | KayKit ships ready-to-use trap tiles | Yes |
| 2026-05-18 | Each dungeon gets a vertical landmark visible from multiple floors | Floors as discrete sealed units | Anchors player's spatial memory + pays off progression | Yes |
| 2026-05-18 | Cosmic-void palette (§6.6) custom shader work for D7 | Reuse stone-fortress assets | The Wound is meant to feel non-architectural | Partial — shader work is ~1 day extra |
```

## 16. Open questions for the owner

1. **Dungeon room count budget per Seeker target hardware**: my estimate of 10-18 rooms per dungeon × 7 dungeons = ~100 rooms total. KayKit's atlas + Unity's batching should handle this, but FPS validation is the gate. If Seeker FPS drops below 50 on any dungeon, reduce that dungeon's room count by ~25%. **Default: build to 10-18, profile, reduce if needed.**

2. **Hidden Vault narrative payoff for D1**: the expanded Healer's Cottage adds a "Hidden Vault" room with optional Letter 4-a content. Worth the extra room? **Default: yes — gives the cleanest Foundation-pitch screenshot moment ("optional secret room rewards exploration").**

3. **Mira's bone-shrine reveal in D5**: should the player be able to physically interact with it (kneel/place an offering) or just observe it? **Default: observe only for v1; interactable in v1.1 if owner wants.**

4. **D7 cosmic shader work**: ~1 day of custom shader effort (warps, parallax space, gold-flecks). Is the budget there? **Default: yes — it's THE endgame reveal; the spec budget includes it.**

5. **Wolfwarden's Vigil bell mechanic**: ringing the bell triggers the climactic conversation OR the boss fight. Two paths. Both have unique narrative outcomes (per existing D3 spec). Build both, or pick one for v1? **Default: build both — the dungeon design's emotional weight requires the choice.**

6. **Folk Who Forgot — Avalon mirror layout**: the suggested creative addition where the ruined village's layout mirrors Avalon's. Add this, or keep dungeon layout independent? **Default: yes — it's the strongest creative addition the spec proposes; the déjà vu beat lands hard.**

## 17. Sequencing

Per the v1 scope lock + the owner's recent expansion to ship the Healer's Cottage in v1:

- **v1 ships D1 (Healer's Cottage)** — full multi-level layout per §9. Other 6 dungeons stay v1.1.
- **v1.1 sprint adds D2-D7** — agent picks up the per-dungeon supplements in §10 and builds them out using the established system from D1.
- **v1.2 adds optional content** — Hidden Vault refinements, bell-mechanic variants, the bone-shrine interaction, custom shader polish.

The KayKit Dungeon Remastered asset import is shared across all 7 dungeons — pay the import cost once for D1 in v1; v1.1 dungeons start with the asset library ready.

---

_The Folk built shallow homes. The dark made them deep. By lantern. By oath. By Heart._
