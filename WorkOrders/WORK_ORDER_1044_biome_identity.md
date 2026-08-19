# WORK ORDER 1044 - Biome + Tunnel IDENTITY (Goldfields / Stoneback / Mirewood / Ashwood + the tunnel)

**Status:** DONE - ★ ALL ELEVEN RULINGS APPROVED BY THE OWNER 2026-08-17, FOLDED INTO CANON 2026-08-19

> ## ✅ IMPLEMENTED 2026-08-19 - what landed, and what did not (by design)
>
> Per **R12** (also ratified: *"does any of this become a build ticket tonight? My pick: no"*), this
> ticket delivered the **identity**, not the systems. What shipped:
>
> | Ruling | Landed as |
> |---|---|
> | **R1** | `BiomeRoads.TunnelDisplayName` = **"The Rootways"**. **`TunnelSceneId` UNCHANGED at `dg_hollow_roads`** - the id is a four-way contract and additionally keys the WO-1112 hero carry via `HubScenes.IsComposedDungeon`. Recorded as `tunnelName` in `canon-strings.json` (dual copy, byte-identical). |
> | **R2** | Origin = the Heart's roots, written into the graph JSON `_comment` (dual copy), `canon-strings` `tunnelOrigin`, and narrative §5c - including the *why the tunnel is quiet* logic and the audio-falloff consequence. |
> | **R3** | Short forms are the UI names; long forms prose only. Already true in `ZoneManager.Regions`; now RECORDED in `canon-strings.json` (`regionGoldfields` ... `regionAshwoodLong`) and **cross-checked against `ZoneManager` by BiomeRoadsRegression Case 3**, so the two homes cannot drift. |
> | **R4, R5, R6, R7, R8, R9, R10, R11** | Folded verbatim into `docs/ECHOES_OF_ELARION_NARRATIVE.md` **§5c** (canon lives there, CLAUDE.md §15) + `docs/regions-narrative-and-npcs.md` §9, with §8 Q4 closed by R6. **No gameplay/system code was written for these** - that is R12's whole point. |
> | **R12** | Honoured: the six follow-on build tickets are enumerated at the end of narrative §5c, **without minted WO numbers** (the `CLI_LANES_WO_NUMBERS.md` banner is the sole authority). |
>
> **Regression:** `BiomeRoadsRegression` grows **Case 7 - the ruled identity**, and Case 3 grows the
> canon-strings cross-check. Case 7 fails in BOTH directions: if the id is "tidied" to match the name
> (silently unhooking the graph file, the injector and the hero carry) **and** if the display name is
> reverted or re-typed. Suite reason string bumped `6 cases green` -> `7 cases green`.
>
> **Still open / needs a Unity run:** the compile gate + `DataRegression.RunAll` have NOT been run
> (single-Unity-lock; CLI seat). **Nothing here needs a scene edit or a bake.**

> Owner ruling, verbatim: **"yes to all defaults on 1044"** (2026-08-17).
> Every recommendation in §5 (R1-R11) is RATIFIED AS WRITTEN. The line that used to sit here -
> *"nothing here is canon until she rules"* - is spent: **this document IS canon now.** Read §5 as
> decisions, not proposals, and §1-§4 as the authored biome identity rather than a pitch.
>
> The eleven, resolved:
> **R1** tunnel display name = **The Rootways** (id `dg_hollow_roads` UNCHANGED - it is a hard contract
> in `BiomeRoads.ArmRoomIdFor`, the graph json, the injector and BiomeRoadsRegression; renaming the id
> breaks all four). **R2** origin = the Heart's roots. **R3** short code names in UI ("Stoneback",
> "Ashwood"), long forms ("Stoneback Ridge", "Corrupted Ashwood") in prose only. **R4** Elowen/Goldfields,
> Doran/Stoneback, Corvin/Mirewood, Bran/Ashwood - **flavour only, NEVER a harvest gate** (CLAUDE.md §7).
> **R5** Doran takes Stoneback; Aldwin stays unattached to any march (he is the founding Echo, tied to
> the Heart). **R6** Ashwood's "forgetting" is a REAL mechanic - HUD dim + audio mute past the dark ward -
> and it is **fully reversible and never punishing**. **R7** the four first-arrival frames in §3 are the
> authored arrival beats. **R8** yes to the moving cart with a person in Goldfields' first frame (one
> walking NPC on a path). **R9** request the one new light-shaft VFX key for Mirewood. **R10** LEAVE the
> z=-404 cave mouth pointed at the outpost; the tunnel keeps its own portal. **R11** one distinct ambient
> bed per march - wind / stone-silence / water-drip / total quiet.
>
> ⚠ R11 is load-bearing for R7, not decoration: Ashwood's authored arrival beat is *"the sound stops"*,
> which cannot exist while all four regions share the single overworld pool at `WorldMusicDirector.cs:12`.
> Implementing R7 without R11 silently drops that beat.
**Seat:** UI / creative lane (markdown only; no code, no data, no bake)
**Date:** 2026-08-16
**Trigger:** owner, 2026-08-16 - *"place a portal to simple tunnel system that will drop into the new biomes."*
The access structure landed tonight (portal -> `dg_hollow_roads` -> four arm drops). The doors exist.
This WO is the half nobody did: **what is on the other side of each door.**

> **READ THIS FIRST - I INVENTED ALMOST NOTHING.** All four biomes are already authored, in depth, in
> `docs/ECHOES_OF_ELARION_NARRATIVE.md` sec.3-5b and `docs/regions-narrative-and-npcs.md` sec.2-5, with
> NPCs, crystal grades, danger tiers and region intro VOICE LINES already written. This document
> **assembles** that into one rulable page and fills the four genuine gaps: the **first-arrival image**,
> the **palette in value/texture/light** (not hue), the **Echo association**, and the **tunnel's name and
> origin**. Where I propose something new it is marked **[NEW]**.

---

## 0. What is already true (cited, so we do not re-decide it)

| Region | Code display name | Dir | Danger tier | Crystal grade | Enemy faction |
|---|---|---|---|---|---|
| Goldfields | `"Goldfields"` | East | 1 (calm) | Aether -> Verdant | Wildlands, living |
| Stoneback | `"Stoneback"` | West | 2 (uneasy) | Verdant | Wildlands, living |
| Mirewood | `"Mirewood"` | South | 3 (dangerous) | Mire | Wound-tied, corrupted |
| Ashwood | `"Ashwood"` | North | 4 (deadly) | Wraith | Wound-tied, corrupted |

Sources: `Assets/_Modules/Core/World/ZoneManager.cs:48-52` (names/tiers/cardinals),
`Assets/_Modules/Core/World/BiomeRoads.cs:322-333` (the calm/uneasy/dangerous/deadly travel labels
already on the tunnel drops), `docs/REGION_ENEMY_ROSTER.md:18-21`,
`docs/ECHOES_OF_ELARION_NARRATIVE.md:132-137` (grades).

**Load-bearing lore already ruled:** danger and crystal richness are *the same fact told twice* - the
echo is strongest where the Fall was worst (`ECHOES_OF_ELARION_NARRATIVE.md:125-130`). Every biome
identity below obeys that. **Do not** let art make Ashwood look poor or Goldfields look rich.

---

## 1. The four biomes

Each block: what it IS / what the Hollowed did / what the player FEELS / palette by **value, texture,
light** (never hue - owner is red/green colourblind, so a description that only names colours is
unusable to her and to a greyscale check) / signature hazard / Echo association.

### GOLDFIELDS - East, tier 1 (calm)
**Is:** the realm's old breadbasket and the last open road. Wheat still grows because the fields lie in
the Heart's lee; carts still come, fewer each season. It is the echo of the realm *at its best*, heard faint.
**What the Hollowed did:** almost nothing yet, and that is the horror - the browning edge creeps a few
rows closer every season. This is loss by inches, not by siege.
**Feels:** relief, then guilt. The one march where you meet living strangers.
**Palette (value/texture/light):** the brightest place in the game and the *lowest contrast* - a wide pale
field with almost no dark in frame. Light is low and raking, so every stalk throws a long soft shadow and
the ground reads as texture, not colour. Surfaces are dry, fine, and in constant slow motion (wind). The
only hard dark shapes are lone trees and the cart. Greyscale test: it should read as a near-white page with
three or four charcoal silhouettes on it.
**Signature hazard [NEW]:** *open ground.* Nothing hides here - enemies cross in formation and are visible
from a long way off, which means the threat is **time**, not surprise: you see them coming and cannot stop
them coming. (Grounded in `regions-narrative-and-npcs.md:64` - "enemies cross the open ground in formation,
seen from far off".)
**Echo association [NEW, flavour only]:** **Elowen, the Nature Echo** - "the grove-warden who once walked
Elarion's every furrow" (`EchoRosterCatalog.cs:164-169`). The Goldfields *are* her furrows.

### STONEBACK - West, tier 2 (uneasy)
**Is:** the realm's old quarry, and bones older than the realm. High cold stone where frost-spirits dwelled
before the Heart was planted. The workings are abandoned; the cairns are left to the wind. The ridge does
not care who wins.
**What the Hollowed did:** the Fall passed the ridge by. Nothing here is corrupted - it is simply *indifferent*,
which the player will find more unsettling than hostility.
**Feels:** small. This is the one march that is hard because of the **climb**, not the enemy.
**Palette:** the highest *local* contrast and the flattest *global* light - overcast, near shadowless, so the
rock's own faceting does all the modelling. Everything matte, dusty, hard-edged, mid-value; sky brighter than
ground; snow patches are the only true whites in the frame. Nothing moves except the player. Greyscale test:
a page of mid-grey faceted shapes with white cut into the crevices.
**Signature hazard [NEW]:** *the ground itself* - steep grades slow everyone (player included), so fights are
positional. Fewer enemies, each tougher; retreat is uphill.
**Echo association [NEW, flavour only]:** **Doran, the Earth Echo** - "the mason who raised Elarion's stones"
(`EchoRosterCatalog.cs:198-203`). Stoneback is where the stones were cut. *(Alternate: Aldwin the Ice Echo
suits the cold - but Aldwin is the founding Echo and belongs to the Heart, not to a march. See ruling R5.)*

### MIREWOOD - South, tier 3 (dangerous)
**Is:** the **first valley the Withering ever took**, drowned under black water, its hall and homes still
standing beneath the murk. The main tide of Hollow Ones marches up from here.
**What the Hollowed did:** they were *made* here. This is the rot's oldest ground and its front door - the
Withering pools in still water the way cold pools in a cellar.
**Feels:** already lost. The fog never lifts, the footing lies, and something is watching that you never see.
**Palette:** the narrowest value range in the game - everything crushed into a dark mid-band with no highlights
and no true blacks. Light arrives only in vertical shafts through the canopy. **The water is the brightest thing
in frame** because it mirrors the sky, so the player's eye is pulled downward, at the drowned town. Textures
are wet, sheened, slick. Fog eats distance past roughly twenty metres. Greyscale test: near-uniform dark grey
with bright ribbons of water threading through it.
**Signature hazard:** *false footing* (`regions-narrative-and-npcs.md:92` - "the footing lies"). **[NEW]** Ground
that reads walkable and is not; a hero slowed or stranded in water while a march comes up the causeway. Plus the
canon fact that this is the **heaviest enemy pressure in the realm** - you must hold a forward position to harvest.
**Echo association [NEW, flavour only]:** **Corvin, the Void Echo** - "the scout who ranged the far dark for
Elarion and never came home" (`EchoRosterCatalog.cs:174-179`). Mirewood is the road that does not give people back.

### ASHWOOD - North, tier 4 (deadly)
**Is:** the failing front line. A dead forest of trees that died standing, closest the living realm reaches
toward the **Wound**. The first Keepers' strongest ward-stones stand here and are going dark one by one.
**What the Hollowed did:** they are *doing it now*. Ashwood's ruins are **recent**, not ancient - the rot is
still spreading through them while the player watches.
**Feels:** wrong, and quiet. Canon already gives it the forgetting: linger past a dark ward and the audio mutes,
the HUD dims, the Heart's voice fades (`regions-narrative-and-npcs.md:116`).
**Palette:** the **highest contrast in the game and almost no mid-tones** - near-black trunks standing on a pale
powdery ground, like ink on ash. Light is flat and shadowless (no sun reaches here), so silhouette does all the
work. Textures are dry, matte, dead, and shed particles. The corruption-fog and the ward-stones are the only
things that *emit* - they must read as brighter than everything around them, so they survive a greyscale check
without depending on their hue. Greyscale test: two values only, plus two glows.
**Signature hazard:** *the forgetting* (canon, above) - reversible, never punishing, relit by a ward-stone.
**[NEW]** Pair it with the ward-stones as the only safe islands, so the biome is played as stone-to-stone.
**Echo association [NEW, flavour only]:** **Bran, the Storm Echo** - "the watchman who held Elarion's wall
through every gale... he fell at his post" (`EchoRosterCatalog.cs:186-191`). The north wards *are* the posts,
and they are failing. Bran belongs to the ground where the watch broke.

> **The two Echoes I deliberately did NOT assign:** **Aldwin** (the founding Echo, first soul the Heart kept  - 
> he belongs to the Heart and the FTUE, not to a march) and **Maren** (the hearth-keeper - she belongs to the
> town forge). Four marches, four Echoes, two kept at home. That is the shape I recommend.
> **These associations are LORE ONLY - arrival lines, fragments, quest flavour. They are NOT a harvest gate.**
> CLAUDE.md sec.7 is explicit that affinity is a match bonus and never a lock; nothing here may become one.
> *(Sanity check against the three disclosed pair synergies in `echoes-balance.json:23-27` - Provisions
> Elowen+Aldwin, Forge Doran+Maren, Fortune Corvin+Bran: the two I keep at home are one from Provisions and
> one from Forge, so no disclosed pair loses both members to the marches. Fortune sits entirely out on the
> two hardest marches, which reads correctly - fortune is what you go looking for.)*

---

## 2. The tunnel - name and origin **[the one thing that is genuinely undecided]**

The tunnel currently ships as **"The Hollow Roads"** (`dg_hollow_roads.json`, `BiomeRoads.cs`,
`HollowRoadsDropInjector.cs`). The graph is deliberately **simple**: nine rooms, two intersections, no
encounters, no traps, no keys, no chests - "a CROSSROADS, not a dungeon" (`dg_hollow_roads.json` `_comment`).

**The problem with the current name is not taste, it is contradiction.** "Hollow" reads as *the Hollowed's*.
If the Hollowed dug these tunnels, an empty tunnel reads as unfinished content - the player expects them in it,
finds nothing, and concludes the game is missing a fight. **The name has to explain why the tunnel is quiet.**

| Candidate | Origin it implies | The case |
|---|---|---|
| **The Hollow Roads** (current) | the Hollowed dug them | Zero churn; matches the enemy vocabulary. **But it promises enemies the design deliberately does not have**, and it hands the player's own travel network to the enemy, which is a strange thing for the Keeper to be using freely. |
| **The Rootways** **[MY PICK]** | the Heart's roots made them | Explains the emptiness *as lore*: the song runs down there, so the rot does not. Explains why they reach exactly the four marches (roots reach where the wards are). Ties travel to the Heart, so every crossing is the Heart carrying you. Plain, Anglo-Saxon, sits beside `dg_ember_deep` / `dg_bonecrypt` without straining. |
| **The Underroads** | the forgotten civilisation built them | Plainest of the three and the safest. Reinforces the tagline ("Echoes of a Forgotten Civilization") and makes the tunnel itself an artefact. Weaker than Rootways only because it does not explain the quiet. |

**Recommendation: rename the player-facing string to "The Rootways"; leave the id `dg_hollow_roads` alone.**
The id is a hard code contract (`BiomeRoads.ArmRoomIdFor`, the graph json, the injector, a regression) and
renaming it buys nothing. A display-string change costs one line and no risk. If she prefers the current name,
say so and section 3's arrival beats still stand unchanged.

**Consequence of picking Rootways:** the tunnel should feel like the inside of something *living and old*  - 
walls that are root and packed earth, not cut stone; faint light in the wood itself; the Heart's tone audible
and getting fainter the further down an arm you walk, so the four mouths are the moment the song runs out.
That last beat is free drama and it costs one audio falloff.

---

## 3. First arrival - the one image per biome **[NEW, and the cheapest high-leverage thing here]**

What the player sees in the first two seconds of stepping out of the arm. Author these four frames and the
biomes have identity even before the art passes land.

- **GOLDFIELDS:** you step out of the dark into flat open light, and the first thing in frame is **a cart on a
  road, moving, with someone walking beside it.** Not a ruin. Somebody else is still alive out here. The whole
  tonal argument of the east march delivered without a line of dialogue.
- **STONEBACK:** the mouth opens **on a ledge**, and the ground drops away - and down there, small, is **the
  valley you defend.** The first time in the game the player sees home from outside. It should look worth it,
  and it should look tiny.
- **MIREWOOD:** you come out **ankle-deep**, and the first thing in frame is **a rooftop** - a house's roof,
  standing out of black water at the wrong height, with one lantern lit on it. The town is *under* you.
- **ASHWOOD:** you come out and **the sound stops.** First thing in frame is **a dark ward-stone**, and behind
  it, among the trunks, **a standing figure that does not move toward you.** (The One Who Remembers,
  `ECHOES_OF_ELARION_NARRATIVE.md:230` - already canon, already the game's emotional centre. Put it in the
  doorway.)

Each one is a single composed sightline from a fixed drop point. No new systems.

---

## 4. What is already in the tree that can serve these

**Search-before-build was done. Cited.**

- **Scatter direction, per biome, already authored down to prefab names**  - 
  `docs/WORLD_BIOME_SCATTER_DIRECTION.md:14-67`: Goldfields = grass hero, sparse lone trees, `Wheat_Plant`;
  Stoneback = `Rock_Large/Sharp/Pillar/Terrasse` clusters; Mirewood = `Tree_Forest/Tall/Beech` + dense fern
  understory; Ashwood = `Tree_Dead/Dead_Broken/Dead_Torn_A/B/Bare/Old` + debris. It even notes Mirewood and
  Ashwood **share the same dead-tree prefabs, separated by tint only.** Nothing new needs authoring for scatter.
- **Region intro VOICE LINES are already written** for all four (`ECHOES_OF_ELARION_NARRATIVE.md:159, 183, 207, 234`). They are good and they are in the right register. **Use them verbatim as the
  arrival line over the section 3 image.** Do not rewrite them.
- **Nine NPCs already lored** across the four marches (`regions-narrative-and-npcs.md:135-143`) - Maeren,
  Brightwheat, Sister Wren, the Frostmother, Garrick, Old Sedge, Vessa, Old Bram, the One Who Remembers.
  !! **None of them exist in `Assets/Resources/Data/Canonical/dialogue/dialogues.json`** - the inventory is
  lore-complete and data-empty. That is a build ticket, not a creative one.
- **Enemy rosters per region already assigned** (`docs/REGION_ENEMY_ROSTER.md:18-21`), including the
  living -> corrupted faction flip at the Stoneback/Mirewood line. No new enemy design needed.
- **Portal visual language exists** - glowing ground disc + billboard sign, `DESIGN-DECISIONS.md` #12; the
  tunnel drops already carry danger-tiered prompt text (`BiomeRoads.TravelLabel`). The arrival side needs no
  new UI.
- **Crystal grade names exist** (Aether / Verdant / Mire / Wraith, WO-144) and are per-region exclusive.

### 4a. VFX keys that already exist for these palettes (nothing new needed for a first pass)

Two registries: the `VFXType` enum (`Assets/_Modules/Village/Vfx/VFXType.cs`) and the Hovl string catalog
(`Assets/Resources/VFX/HovlVfxCatalog.asset`). Mapped to the four palettes above:

- **Goldfields:** `PP_DustMotesEffect`, `PP_FireFlies` (evening motes over the field), `PP_SandSwirlsEffect`
  for the dry wind. Global fog is already warm-dusk and procedural
  (`WorldFeelInjector.cs:116-117,288-291`) - the Goldfields' look is close to the *current default*, which is
  why it is the cheapest of the four to stand up.
- **Stoneback:** `PP_StoneImpacts`, `PP_DustStorm`, `PP_EarthShatter`, `Frost_Impact` / `Freezing_Impact` for
  the snow patches. `Aura_Dust`.
- **Mirewood:** `PP_GroundFog` / `Env_GroundFog`, `PP_WaterLeak`, `PP_BigSplash`, `PP_Steam` / `PP_RisingSteam`,
  `PP_PoisonGas`, `Env_LanternGlow` for Vessa's lantern and the drowned rooftop.
- **Ashwood:** `Damage_Smolder`, `PP_Smoke`/`PP_SmokeEffect`, `Env_TitleEmbers`, `Aura_SmokeReaper`,
  `PP_Dissolve`, `Env_DestructionDust`. Ward-stones can reuse `Node_Aura` / `Poi_NodeAura` / `Holy_Aura`
  (lit) and simply not emit (dark) - the on/off *is* the mechanic.
- !! **No light-shaft / god-ray key exists in either registry.** Mirewood's "light only in vertical shafts"
  is the one visual in section 1 that has no existing key behind it. Either it becomes a small ask, or
  Mirewood leans on `PP_GroundFog` + `Env_LanternGlow` instead. Flagged as ruling R10.

### 4b. Tunnel geometry and a ready-made mouth

- The tunnel is already built from the shipped room kit: `Assets/Dungeon/Rooms/` (`EntryHall`, `Straight`,
  `Intersection`), socket contract in `Assets/Dungeon/Rooms/DEFAULT_ROOMS.md`. Nine rooms, no new art.
- **A walk-up cave and a flat cave-road corridor already exist in the overworld** - roughly z=-404, with the
  road held at Y=0 for |x|<=20 across z in [-700,-76] (`CavePortalRepointInjector.cs:154-161`). That is a
  finished tunnel mouth looking for a tunnel. !! It is currently repointed to the **outpost**, by owner ruling
  (`CavePortalRepointInjector.cs:41-45`) - do not steal it without her say. Raised as R11.
- Portal art exists: `Assets/Art/Dungeon/Exit/Portal.fbx`, `Resources/Structures/Materials/
  Portal_To_Dungeon_basecolor.mat`, VFX `Dungeon_Portal_Gate` / `Portal_Threshold_Aura` / `Env_DungeonPortal`.
- Polyperfect ships literal tunnel tiles: `_M/Prefabs_M/Tiles_M/Tunnels_M/` (`Tile_Mainroad_Tunnel`,
  `_Curve`, `_Entrance`) - useful if the tunnel ever wants a surface-level mouth instead of a portal disc.
- Portals already support **discovery**: they spawn dimmed at 0.12 alpha and fade in within 26 m
  (`DungeonWorldPortalSpawner.cs:71-78`). The tunnel mouth gets that behaviour for free.

### 4c. The name is a ONE-LINE change

`BiomeRoads.cs:98` - `TunnelDisplayName = "The Hollow Roads"` is the **only** authored display-name constant
in the entire dungeon set (no other `dg_*` has one). Ruling R1 costs one string.

### 4d. Two real gaps the inventory found

- **The four biomes are narratively SILENT in data.** Zero hits for Goldfields / Stoneback / Mirewood /
  Ashwood across `en.json` and `dialogue/dialogues.json`. The only per-region prose that ships is four
  one-line `note` fields in `spawn-areas.json` (L7/20/34/48). The excellent region voice lines in
  section 4's first bullet live only in a **doc**, not in the game.
- **No per-region music or audio identity.** `WorldMusicDirector.cs:12` collapses all four regions into one
  cycled overworld pool. Four biomes that sound identical will not feel like four places, and audio is the
  cheapest differentiator we have. Raised as R12.

---

## 5. RULINGS - ★ ALL ELEVEN APPROVED AS WRITTEN (owner, 2026-08-17)

> **"yes to all defaults on 1044"** - the owner took the one-word option. Every `my pick` below is now
> THE DECISION. Nothing in this section is open. Read each bullet as: the recommendation won.
>
> Implementers: do NOT re-litigate these, and do not treat the alternatives listed alongside each pick
> as still-live options - they are recorded only so a future reader can see what was weighed. If one of
> these turns out to be wrong in practice, that is a NEW ruling with a new date, not a reopening of this one.

- **R1 - Tunnel name.** Keep **The Hollow Roads**, or rename the display string to **The Rootways** (my pick,
  because it explains why the tunnel is empty), or **The Underroads**?
- **R2 - Tunnel origin.** Heart's roots (my pick), the forgotten civilisation's build, or the Hollowed's dig?
  This sets the material and sound of every crossing. R1 and R2 should be answered together.
- **R3 - Display names.** Code says `"Stoneback"` and `"Ashwood"`; narrative canon says **"Stoneback Ridge"**
  and **"Corrupted Ashwood"**. They disagree today. My pick: **use the short code names in UI** (they fit the
  travel prompt) and keep the long forms for prose only. Say the word and it is one table.
- **R4 - Echo-to-biome association.** Elowen/Goldfields, Doran/Stoneback, Corvin/Mirewood, Bran/Ashwood, with
  Aldwin and Maren staying home. Approve, or reshuffle? (Reminder: flavour only, never a harvest gate.)
- **R5 - Aldwin vs Doran for Stoneback.** Aldwin is the Ice Echo and Stoneback is the cold march - but he is
  the founding Echo tied to the Heart. My pick: **Doran**, keep Aldwin unattached.
- **R6 - Ashwood's "forgetting".** Canon proposes HUD dim + audio mute past a dark ward. Real mechanic
  (my pick: yes, fully reversible, never punishing) or flavour only?
- **R7 - First-arrival beats.** Approve the four images in section 3 as the authored arrival frames? These are
  the cheapest identity in the whole project and the easiest to lose if we do not fix them now.
- **R8 - Goldfields' living NPC at arrival.** Section 3 puts a *moving cart with a person* in the first frame.
  That needs one walking NPC on a path. Approve, or should the east arrival be empty like the other three?
- **R9 - Mirewood's light shafts.** No god-ray / light-shaft VFX key exists in either registry (sec.4a). Ask for
  one small addition (my pick), or drop the shafts and let `PP_GroundFog` + a lantern carry the biome?
- **R10 - The existing cave mouth.** There is a finished walk-up cave and flat cave-road corridor at
  roughly z=-404, currently pointed at the **outpost by your own ruling**. Leave it alone (my pick - the
  tunnel keeps its own portal), or repoint it as the tunnel's surface entrance?
- **R11 - Per-region audio.** All four regions currently share one overworld music pool
  (`WorldMusicDirector.cs:12`). My pick: give each march one distinct ambient bed (wind / stone-silence /
  water-drip / total quiet). It is the cheapest way to make four places feel like four places, and Ashwood's
  "the sound stops" arrival beat in section 3 does not work without it.
- **R12 - Does any of this become a build ticket tonight?** My pick: **no** - rule the identity first, then
  section 3 + the tunnel material become one small WO on the main line, and the nine missing NPC dialogue
  records become another. Ruling first keeps both cheap.

---

~~**Nothing in this document is canon until ruled.**~~ **RULED 2026-08-17 and FOLDED 2026-08-19.**
The approved blocks now live in `docs/ECHOES_OF_ELARION_NARRATIVE.md` **§5c** and
`docs/regions-narrative-and-npcs.md` **§9** (canon lives there, not here, per CLAUDE.md sec.15).
**Read the canon from those two docs, not from this WO** - this file is the record of how it was
decided, and the §5 bullets are preserved in their original question form only so a future reader can
see what was weighed. This WO is DONE.
