# The Healer's Cottage — 3D Dungeon Design (proof of concept)

**Status:** Design spec. Proof-of-concept first 3D dungeon. Decides whether to pivot the dungeon system from SVG (`docs/dungeons-system-design.md`) to 3D using KayKit Dungeon Remastered.
**Owner:** DeNelle Studios
**Date:** 2026-05-18
**Spec source:** Owner ask 2026-05-18 — "can you design dungeon map with these assets?" linking to https://kaylousberg.itch.io/kaykit-dungeon-remastered

---

## 1. What this is

The first dungeon in the _The Healer's Garden_ questline (the opener of the six-questline narrative arc, per `docs/dungeons-storyline.md` §4.1). The Keeper enters Alduin the Mournful's old cottage — the place he lived as a healer before the Withering took him. Six rooms, six narrative beats, one mini-boss. Lore-driven; first dungeon a player ever sees; trains them on the Lantern mechanic + the basic dungeon loop.

If this design ships well, it becomes the template for the other six dungeons. If the 3D approach proves too expensive or too dissonant with the SVG-postcard register the bible established, we can keep SVG dungeons and revert.

## 2. The asset pack

**KayKit Dungeon Remastered** (https://kaylousberg.itch.io/kaykit-dungeon-remastered) — CC0, 200+ assets free, 275+ in EXTRA ($7.95). Single 1024×1024 gradient atlas downsampled to 128×128 for Seeker. FBX / GLTF / OBJ. The free version covers the cottage's needs; the EXTRA version adds tavern pieces + beds we'd want for future dungeons but not this one.

**Direct asset categories we'll consume:**

- **Walls + floors + ceiling** — modular stone, wood, and dungeon-tile variants
- **Doors** — open / closed / barred / arched
- **Stairs** — small, large, less-steep variants (1.1 added these)
- **Furniture** — tables, chairs, shelves, bookcases
- **Containers** — chests, barrels, crates (some broken)
- **Atmosphere** — candles, banners, traps, debris piles, water tiles
- **Lighting** — KayKit packs the assets but Three.js point/directional lights drive the actual illumination

## 3. The map — six rooms, six narrative beats

ASCII top-down for the layout. North is up. Each cell is ~6×6 world units (the Keeper's hero is ~1.8u tall, so 6u rooms feel intimate, not cavernous).

```
                    ╔═════════════════╗
                    ║   LOFT BEDROOM  ║   (Beat 5 — optional, ladder up)
                    ║   📜 journal 3  ║
                    ╚═══════╦═════════╝
                            ║
                       (ladder/stairs)
                            ║
   ╔═══════════════╗   ╔════╩════════════╗   ╔═════════════════╗
   ║ GARDEN        ║   ║ MAIN ROOM /     ║   ║ APOTHECARY      ║
   ║ APPROACH      ╠═══╣ HEARTH          ╠═══╣ (Beat 6 — boss) ║
   ║ (Beat 1)      ║   ║ (Beat 3)        ║   ║ 📜 journal 4    ║
   ║ Bryn here     ║   ║ 📜 journal 2    ║   ║ 🎁 seed jar     ║
   ║ ↓ start       ║   ║ trapdoor hidden ║   ╚═════════════════╝
   ╚═══════════════╝   ╚═════╦═══════════╝
            ║                ║
            ║          (trapdoor — lantern-gated)
            ║                ║
   ╔════════╩══════╗   ╔═════╩═══════════╗
   ║ ENTRANCE ROOM ╠═══╣ ROOT CELLAR     ║
   ║ (Beat 2)      ║   ║ (Beat 4 — hidden)║
   ║ 📜 journal 1  ║   ║ 🎁 Cloak of     ║
   ╚═══════════════╝   ║   the Lightbearer║
                       ╚═════════════════╝
```

The Keeper enters at Garden Approach (SW). Path forks: south to Entrance Room (linear, lore-rich) OR east to Main Room (faster). Reconverge at Main Room → Apothecary. Cellar is hidden until lantern is lit; Loft is reached from Main Room.

## 4. Room-by-room — assets, beats, encounters

### Beat 1 — Garden Approach (entrance courtyard, outdoor)

**The space:** an overgrown patch of garden outside the cottage. Stone path through long grass. Wooden fence, broken. A weather-worn door on the cottage front. Bryn the Wanderer (see `docs/dungeon-tension-spec.md`) paces here.

**KayKit assets:**
- `floor_dirt` × 9 tiles (3×3 patch around the path)
- `floor_stone_path` × 4 (the path itself)
- `fence_wood_broken` × 6 (perimeter, broken in places — invites entry)
- `door_wood_arched` × 1 (closed, the cottage entrance)
- `plant_bush_small` × 4, `plant_flower_dead` × 3 (overgrown garden)
- `rock_small` × 2 (clutter — easy to step around)
- `lantern_post` × 1 (unlit; can be lit by the Keeper for one of the lantern mechanic's first tutorial beats)

**Bryn's line (tier 1 — first dungeon):**
> _"The path opens easy, Keeper. But mind the rocks — they remember you. And don't walk it dark. The cottage keeps her shadows close."_

**Encounter:** 1 Hollow One (Apprentice tier — slow, low HP — the tutorial enemy). Spawns from a hedge gap to the north. Killing it drops a 5-SKR pity reward + 1 Soul Ember.

**Lore:** none yet — Bryn is the only voice.

**Lantern mechanic intro:** the `lantern_post` is interactable. Tapping it (Keeper within 2u) lights it; the post becomes a permanent ambient light source for this room. Optional. The interaction teaches the player that light = visibility before they enter the cottage proper.

**Exit:** through the arched door north → Entrance Room.

---

### Beat 2 — Entrance Room (the threshold)

**The space:** a small antechamber inside the cottage. Dusty. A toppled chair. A vase with a dried flower in it. A coat hung on a peg — Alduin's. Footprints in the dust lead to the main room AND down to a corner where the rug is rumpled (the hidden trapdoor sits underneath, but the trapdoor isn't visible yet without a lantern).

**KayKit assets:**
- `floor_wood_plank` × 9 (3×3 room)
- `wall_wood_interior` × 12 (perimeter)
- `chair_wood_tipped` × 1 (custom — or use `chair_wood` rotated 90° on Z-axis)
- `vase_clay_small` × 1 with a `plant_flower_dead` × 1 placed inside
- `coat_hung` × 1 (if available — else just a `cloak_drape` on a peg)
- `dust_pile` × 3 (decorative)
- `rug_woven` × 1 (covers the hidden trapdoor)
- `candle_unlit` × 2 (on a small shelf)

**Encounter:** none. Quiet beat. The room is the player's first moment of "I am inside this story."

**Lore stone:** small reading podium / book stand in the corner. Tapping it shows **Alduin's journal — entry 1**:

> _"The folk came again today. The well at Carrow's edge is going bad. Crystals work on the early signs but the late ones — I don't know yet. I came here to learn the green ways. I am still learning."_

**Lantern reveal:** lighting the lantern in this room reveals the trapdoor under the rug (the rug fades to translucent; trapdoor outline becomes interactable). Without a lantern, the trapdoor is invisible — the player can wander past it.

**Exit:** north through an interior arch → Main Room. (Or down through the trapdoor if discovered → Root Cellar.)

---

### Beat 3 — Main Room / Hearth (the heart of the cottage)

**The space:** the largest room. Stone hearth on the east wall, kettle still on the hook. Herbs hanging from the rafters (dusty). A long worktable in the center with mortar, pestle, bottles. Shelves of dried herbs and small clay jars. A ladder against the west wall climbs to the loft.

**KayKit assets:**
- `floor_wood_plank` × 16 (4×4 room)
- `wall_stone_dungeon` × 8 (east — the hearth wall is stone) + `wall_wood_interior` × 8 (north + west + south)
- `fireplace_stone_large` × 1 (the hearth; can be lit for atmosphere)
- `kettle_iron` × 1 (hung over the fireplace)
- `table_long_wood` × 1 (center)
- `mortar_pestle` × 1, `bottle_small` × 6, `book_open` × 1 (on the table)
- `herb_hung` × 8 (rafters; use `plant_dried` if no specific herb asset)
- `shelf_wood_tall` × 2 (against the south wall, filled with `jar_clay_small` × 12 + `bottle_potion` × 4)
- `ladder_wood_tall` × 1 (against the west wall, climbs to the loft)
- `chair_wood` × 2 (around the table)

**Encounter:** 2 Hollow Ones (Apprentice tier). They spawn from the shelf area when the Keeper crosses the room's midpoint. First real fight. Difficulty: easy with 1 pet active; survivable solo. Reward: 10 SKR + 2 Soul Embers + a chance (~30%) at 1 random Hollow shard (a crafting material for v2 — drops are inert in v1; saves don't fill up).

**Lore stone:** the open book on the worktable. **Alduin's journal — entry 2**:

> _"The Folk who came from the village this week: Wren's boy, the miller's wife, the two children of Carrow. I told them what I had. The well is poisoned, I know that now. It is not anything I can name. It is older than the names I have."_

**Lantern reveal:** lighting either of the candles on the shelves OR the fireplace reveals a small inscription on the hearthstone: _"For Mira, who walked the green ways first."_ — a flavor lore beat, not a quest hook.

**Exits:** east through an arched doorway → Apothecary. West ladder up → Loft Bedroom (optional). Trapdoor under the rug back in the Entrance Room → Root Cellar (if discovered).

---

### Beat 4 — Root Cellar (hidden, lantern-gated)

**The space:** stone-walled, low-ceilinged cellar. Damp. Water on the floor in one corner. Stacked crates and barrels. A small chest in the back, behind some broken crates.

**KayKit assets:**
- `stairs_wood_down` × 1 (descent from the trapdoor)
- `floor_stone_cellar` × 12 (4×3 room)
- `wall_stone_dungeon` × 14 (perimeter)
- `ceiling_low_wood` × 1 (low overhead — visual claustrophobia)
- `water_puddle` × 2 (corner)
- `crate_wood` × 6 (some broken), `barrel_wood` × 4
- `chest_wood_small` × 1 (the reward, in the back)
- `cobweb` × 4 (decorative)

**Encounter:** 1 Hollow One (Cellar variant — slower, weaker — was a villager who fled here long ago, now bound by the Withering). Slightly different idle than the Apprentices upstairs — kneels and rocks rather than wanders. Sad. The bible voice rules apply: they are grief, not menace.

**Lore stone:** none. The cellar is mechanically a reward room.

**Reward chest:**
- **Cloak of the Lightbearer** (1st piece of the 3-piece Lightbearer set from `docs/dungeon-tension-spec.md` §6). Earn-only. Adds +1 tile of light radius when equipped.
- 1 Soul Ember pouch (35 ✦ — meaningful but not huge)
- 1 small SKR reward (5 SKR)
- Bryn's barely-mentioned **lantern oil flask** — a one-time consumable that adds +2 tiles of light radius for 5 minutes real-time (the first Torch the player ever sees; teaches the consumable mechanic)

**Lantern mechanic emphasis:** the cellar is the room where the lantern mechanic FIRST PAYS OFF concretely. The player either:
- Found the trapdoor because they had a torch or used the entrance lantern → cellar reachable
- Didn't light any lantern, missed the trapdoor → cellar inaccessible this run (rerolled on NG+)

This makes the lantern mechanic feel rewarding rather than punishing on the first dungeon. The penalty for going dark isn't "you die" — it's "you miss a treasure room and a story beat." Gentle teaching.

**Exit:** back up the stairs to Entrance Room.

---

### Beat 5 — Loft Bedroom (optional)

**The space:** a small bedroom above the main room. Single bed, small writing desk, a window facing east with the sunrise just beyond. The bed is unmade. A pair of shoes beneath. A child's drawing on the wall.

**KayKit assets (EXTRA pack — bed pieces are EXTRA-only, $7.95):**
- `floor_wood_plank` × 9 (3×3 room)
- `wall_wood_interior` × 9 (perimeter, leaving the south wall as the railing edge over the main room)
- `bed_single_wood` × 1 (EXTRA) — _if FREE-only constraint: use `mat_straw` × 1 instead_
- `desk_wood_small` × 1 (or `table_wood_small` × 1 standing in)
- `chair_wood` × 1 (tucked under desk)
- `window_arched_small` × 1 (east wall)
- `lantern_small` × 1 (on the desk, unlit — interactable)
- `paper_pile` × 3 (on the desk, scattered)
- `drawing_child` × 1 (custom asset — or use a `book_closed` with custom material as placeholder)
- `shoes_pair` × 1 (under the bed — flavor)

**Encounter:** none. This room is pure lore.

**Lore stone:** the papers on the desk. **Alduin's journal — entry 3**:

> _"The dreams are back. The Wound, calling. I am not the same in the mornings. Mira would have known what to say. I miss her hands._
>
> _If anyone reads this — I am going to the Wound. I am going to try one more thing. The Heart will hold while I am gone. The Heart always holds._
>
> _Forgive me."_

The child's drawing on the wall is a scrawled crayon picture of a tree with a tiny figure beneath. The figure is labeled "Papa." Implied: Alduin had a daughter. She is not in the cottage. The narrative bible never resolves what happened to her; this is one of the seeds for v1.1 / v2 quest expansions.

**Lantern reveal:** lighting the desk lantern reveals a small carving on the windowsill — _"M.M. + A.M., 31st of Honeymonth"_ (Mira and Alduin's wedding date, by implication; meaningless to the player who hasn't read enough yet, but accretes meaning as the questline progresses).

**Exit:** back down the ladder to the Main Room.

---

### Beat 6 — Apothecary (final room, mini-boss)

**The space:** the room Alduin worked in. An alchemy bench dominates the east wall — bubbling glass apparatus, a small unlit forge, racks of glass vials, a wall of small drawers labeled in fading ink. Spilled ingredients on the floor. A second worktable with a single book open, a quill beside it, an ink-stained chair. A back door leading out to the woods (the dungeon exit).

**KayKit assets:**
- `floor_stone_dungeon` × 16 (4×4 room — stone floor, more "workshop" than "home")
- `wall_stone_dungeon` × 16 (perimeter)
- `bench_alchemy_large` × 1 (custom or modded `bench_wood_long` with glass vial overlays)
- `apparatus_glass` × 1, `vial_potion_rack` × 1 (on the bench)
- `forge_small_unlit` × 1 (use `fireplace_small`)
- `drawer_wall_array` × 1 (the wall of labeled drawers — custom or repeat `shelf_wood_short` × 6 with small `box_small` × 30)
- `table_wood_small` × 1 (second worktable)
- `chair_wood_high_back` × 1 (the chair where Alduin sat to write)
- `book_open_thick` × 1 (the final journal entry)
- `quill_inkwell` × 1
- `ingredient_spill_powder` × 3 (decorative)
- `door_wood_back` × 1 (the exit, west wall)

**Encounter (mini-boss):** **The Apprentice of the Apothecary** — a stronger Hollow One, an apprentice Alduin took in years ago and never spoke of. Stat block: 2.5× normal Hollow HP, +50% damage, has one special — a "tincture" attack that briefly blinds the Keeper (shrinks light radius by 50% for 6 seconds). Defeated through 2–3 ATB battle turns at average loadout.

**Lore stone:** the open book — **Alduin's journal — entry 4 (the last one):**

> _"I am going. The Wound is louder than the Heart now. I have left a thing in the cellar for whoever is next — a seed. Plant it at the Folk's table, if there is anyone to plant it. It will grow into something old and quiet. The Folk used to say there were trees like it once, before any of us. Maybe one of them will remember the song._
>
> _The Heart will hold. The Heart always holds._
>
> _Tend it, Keeper. I am sorry I could not."_

**Reward (the questline payoff):**
- The **seed-and-stone clay jar** mentioned in `docs/dungeons-storyline.md` §3 (the gift Alduin left for the next Keeper). Plantable at the Farm in the village. Grows into a unique tree over ~3 in-game waves. The tree itself does nothing mechanically — it is a memorial.
- 1 large SKR reward (40 SKR)
- 1 Soul Ember pouch (50 ✦)
- **The Healer's Garden questline beat 1 advances** — the journal entries 1–4 are now collected; beats 5 and 6 unlock when the player reaches a later dungeon (The Apothecary's Vault, the second dungeon in this questline).
- **Wave 8 (auto)** triggers in the village after the player exits this dungeon — the canonical Hollowmouth-opens moment.

**Lantern emphasis:** the room is **deliberately dim** even with maximum lantern coverage. Some apothecary drawers can only be read by stepping close and lingering. This is the room where the player feels the value of upgrading their light radius.

**Exit:** back door west → returns the Keeper to the village (Elarion) via the Hollowmouth gate. The dungeon does not loop; once cleared, it's marked complete in the questline tracker.

---

## 5. Total scope summary

| Metric                          | Value                                          |
| ------------------------------- | ---------------------------------------------- |
| Rooms                           | 6 (matches 6 questline beats)                  |
| Encounters (forced)             | 4 (Garden + Main Room ×2 + Cellar + Boss)      |
| Encounters (optional)           | 0 in this dungeon — all hidden content is lore/treasure, not combat |
| Lore stones                     | 4 (one per main beat — journal entries)        |
| Treasure chests                 | 2 (cellar + boss)                              |
| Hidden content                  | Root Cellar (lantern-gated), Loft (ladder-gated)|
| Mini-boss                       | 1 (The Apprentice of the Apothecary)           |
| Unique KayKit assets needed     | ~28 distinct mesh types                        |
| Total mesh instance count       | ~150 (including repeated floor/wall tiles)     |
| Estimated draw-call budget      | ~80 after instancing (well within Seeker target) |
| Estimated playtime              | 12–18 minutes for a first-time player          |
| Soul Embers awarded             | 80 ✦ (across pity + chest + boss)              |
| SKR awarded                     | 60 SKR (modest — first dungeon)                |
| Lightbearer pieces awarded      | 1 (the Cloak)                                  |

## 6. System pivot implications (read carefully)

This design **assumes** the dungeon system moves from SVG to 3D. Doing that means:

### What we'd need to build
- A new R3F scene at `src/modules/dungeons/3d/` parallel to `src/modules/village/` — separate scene mount, separate camera, separate game loop
- Asset wrappers under `src/modules/dungeons/3d/kaykit-dungeon/` mirroring the village's `kaykit/` structure
- The Lantern mechanic from `docs/dungeon-tension-spec.md` reimplemented as a real Three.js `PointLight` attached to the Keeper, with a customizable distance + falloff. The SVG radial-mask design becomes obsolete (or kept as a fallback for tile-based dungeons later)
- Navigation: WASD/joystick like the village (no SVG tap-to-move)
- Combat handoff: identical to the village's breach → ATB pattern; existing AtbBattleHost handles it
- Wanderer NPC (Bryn): mounts in the Garden Approach room of every dungeon; pattern-identical to the gate Wardens spec — 3D mesh + drei `<Html>` speech bubble

### What we'd retire
- The SVG explorer view at `src/modules/dungeons/DungeonExplorer.tsx` — kept until 3D dungeons fully ship, then deleted
- The radial-mask CSS approach from the original dungeon-tension spec — replaced with real lighting math
- Tile-based encounter triggers — replaced with collider-based proximity triggers (same pattern as the gate Wardens)

### What stays
- All six questlines from `docs/dungeons-storyline.md` — voice unchanged, beats unchanged
- The Wanderer NPC (Bryn) — concept unchanged, just rendered in 3D instead of as an SVG sprite
- The Lightbearer gear set — drops, glow visuals, mechanics unchanged
- The Hollowmouth as the world-tree-side entry point — same world position, just leads to a 3D scene instead of an SVG one

### Estimated total effort to ship the full 7-dungeon system in 3D
- The Healer's Cottage (this spec): **~3 days** (room building + asset wiring + lore stones + boss tuning)
- Each subsequent dungeon: **~1.5–2 days** (system already built; just new layout + assets + lore)
- 7 dungeons total: **~12–15 working days** with focused build time
- Anti-cheat / cyber audit / Seeker perf passes: **~3–5 extra days** specifically for the dungeon scene (FPS targets on real Seeker hardware)

This is a substantial chunk of work. Recommendation:

## 7. Recommendation — three paths

**Path A — Full pivot to 3D dungeons.** Build this dungeon as a proof of concept, then commit the full system pivot. ~12–15 working days for all 7 dungeons. The strongest visual identity match with the village; consistent player experience. Highest cost. Best long-term polish.

**Path B — Hybrid: 3D for one or two flagship dungeons, SVG for the rest.** The Healer's Cottage (this design) becomes the "flagship intro dungeon" — the one a Foundation reviewer or screenshot would showcase. The other six remain SVG-postcard style (lower build cost). Player goes from immersive 3D first impression to gentler SVG exploration later. Risk: tonal whiplash if not handled carefully.

**Path C — Keep SVG dungeons; use this design as a future-state spec.** Park this 3D design as the post-launch v2 blueprint. v1 ships with SVG dungeons (faster, already partly built). After launch and the SKR yield economy is proven, build out the 3D dungeons for v2 as a major content drop. Lowest risk to launch timeline; preserves the design for when it makes sense to commit.

**My lean: Path C.** Launch is close; the SVG approach is already specced and partly built; the 3D pivot is a meaningful timeline risk for v1. Park this beautiful spec for v2, ship SVG dungeons now. The Healer's Cottage in 3D becomes a launch hook for the **post-launch content roadmap** the white paper already promises — and gives the Foundation grant pitch a concrete "what we'll build with the funding" beat.

That said, your call. The design itself is independent of when it lands.

## 8. Asset-staging checklist — STATUS

- [x] **KayKit Dungeon Remastered FREE pack downloaded** (owner, 2026-05-18)
- [x] **Assets staged at `public/kaykit/dungeon/`** — 211 `.gltf` + 211 `.bin` + `dungeon_texture.png` atlas. Vite serves them as static files at the URL root.
- [x] **Texture atlas in place** — `public/kaykit/dungeon/dungeon_texture.png` (the gradient atlas every GLTF references via relative `uri`).
- [ ] **Cleanup the misplaced src/ copy** — owner runs `Remove-Item -Recurse -Force src\modules\village\kaykit\KayKit_DungeonRemastered` in PowerShell. The pack was initially extracted into `src/` which would have caused Vite to bundle it; the working copy at `public/kaykit/dungeon/` is correct.
- [ ] **`.gitignore` entry** to prevent the pack from re-landing in `src/` on future re-extracts:
  ```
  # KayKit pack source distributions — assets live under public/kaykit/, not src/
  src/**/KayKit_*Remastered/
  src/**/KayKit_*/Assets/
  ```
- [ ] **`KK_DUNGEON` constant** added alongside the existing `KK` in `src/modules/village/kaykit/assetManager.ts` (or in a new `src/modules/dungeons/3d/kaykit/assetManager.ts`):
  ```ts
  /** KayKit Medieval pack root — village assets. */
  export const KK = '/kaykit/medieval/';
  /** KayKit Dungeon Remastered pack root — dungeon assets. */
  export const KK_DUNGEON = '/kaykit/dungeon/';
  ```
- [ ] Confirm Seeker FPS target with ~80 draw calls in the dungeon scene (perf budget worksheet — runs alongside the dungeon-system implementation; not a blocker for design)
- [ ] **Owner-decision: Path A / B / C** — see §7 — gates whether Claude Code starts building from this spec or parks it for v2

### Actual GLTF filenames the design uses (verified against the staged pack)

The design spec's asset names need to be cross-checked against what the pack actually contains. Verified ones (sampled): `banner_blue.gltf`, `banner_brown.gltf`, plus 209 more covering walls, floors, stairs, doors, furniture, containers, traps, decorative debris. Some asset names in §4 above are **conceptual placeholders** that may need to be remapped to the pack's actual naming (e.g. `fence_wood_broken` may be `fence_broken.gltf`; `chair_wood_tipped` may need to be `chair_wood.gltf` rotated in JSX). Claude Code does the cross-reference at implementation time using:

```bash
ls public/kaykit/dungeon/*.gltf | head -50
```

…to enumerate the actual asset names. The design's INTENT is the contract; specific filenames are an implementation detail.

## 9. Acceptance (if built per this spec)

1. Player walks the six rooms in any order, all six beats fire correctly.
2. Bryn the Wanderer is visible and speaks from the Garden Approach.
3. The lantern mechanic gates the cellar and reveals the journal-1 trapdoor.
4. The four journal entries are collectible in any order; collecting all four advances the questline.
5. The boss is winnable on the player's default starting loadout (1 hero + 1 pet bonded at rank 1).
6. Cloak of the Lightbearer drops from the cellar chest; on equip, light radius grows by +1 tile.
7. The seed-and-stone clay jar drops from the boss; on use at the Farm in the village, the unique tree grows.
8. Reduced-motion respected (no jarring camera moves; lantern flicker disabled).
9. FPS holds at 60 on Seeker baseline throughout the run.
10. The dungeon does not loop; on exit, the player returns to Elarion and the questline beat completes.

---

_The first dungeon. Where the player learns: this game is patient. The dark wants you to bring a light. The dead were people. Tend the Heart._
