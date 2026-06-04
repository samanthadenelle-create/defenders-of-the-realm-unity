# Dungeon Designs — D2–D11 (11 dungeons total; full buildable specs, canon-grounded)

> Design pass for the un-built dungeons, grounded in `docs/dungeons-storyline.md` (the four acts),
> `docs/enemy-codex.md` (named mini-bosses + roster), `docs/dungeons-3d-unity-layout-spec.md` (layout
> conventions), and `docs/kaykit-asset-catalog.md` (Dungeon Remastered 1.1 = the interior kit).
> **D1 Healer's Cottage is BUILT** (reference template). This doc designs **D2–D6** as complete rooms +
> encounters + lore so CLI/a designer can build each from the existing dungeon system (`DungeonController`).
>
> **Tone (locked, from the storyline):** mourning, not war. Cozy at the edges, real stakes at the core.
> Studio Ghibli's Nausicaä by way of an old fairy tale. The Keeper mourns the Hollow Ones even while
> ending them. Short sentences. Grounded, lightly archaic vocabulary.
>
> Design only — no `.cs`, no scene, no bake. Each dungeon reuses `DungeonController` + KayKit Dungeon
> Remastered furniture + the existing enemy roster. Mini-boss **names are canon** (enemy-codex §0).

---

## Shared build conventions (all D2–D6)

- **Engine:** reuse `DungeonController` + dungeon scene pattern (as D1). Modular KayKit Dungeon Remastered
  (floors/walls/doors/stairs/columns + furniture). Enter via a **world portal** (WO-165) now that
  dungeons relocated to the world; exit returns to the world spot.
- **Structure:** 3–5 rooms: *entry → 1–2 combat/puzzle rooms → a lore/rest beat → mini-boss room → reward*.
- **Enemies:** drawn from the existing roster (enemy-codex) — Hollow Ones in most; Wildlands (Wolves/
  Caveman) in the cold/wild ones. Scaled by the dungeon's act/level gate (ties to `ThreatLevel`, WO-164).
- **Lore delivery:** lore-stones / found objects (journal pages, letters, masks) carry the mourning-story
  — every dungeon advances a questline beat. Each ends on a **revelation**, not just loot.
- **Reward:** crystals (grade by act), gear/cosmetic, and a **questline beat** + lore fragment.

---

## D2 — The Apothecary's Vault  ·  Act III  ·  mini-boss: **The Vault Keeper** (Hollow Ones)

- **Where/when:** Wintermere/Thornwood deep; unlocked ~Wave 18, hero 10–15. Completes *The Healer's Garden*.
- **Emotional beat:** the journal she's been reading is **Alduin's** — the handwriting changes in the last pages.
- **Rooms:**
  1. *Sealed antechamber* — a vault door puzzle (find the apothecary's sigil-key among shelves).
  2. *Hall of masks* — healer-masks on the walls (KayKit banners/props); Cellar Hollows kneel and rock
     (the sorrow variant) — pitiable, not aggressive until disturbed.
  3. *The reading room* — a rest/lore beat: the journal's final pages, handwriting shifting to Alduin's.
  4. *Vault core* — **The Vault Keeper** mini-boss: a robed Hollow guarding the apothecary's legacy;
     fights defensively, "seals" (shields) itself periodically — break the seal to damage it.
- **Reward:** high-grade crystals + the journal completion (Healer's Garden questline closes).
- **Signature:** the dawning realization through handwriting — quiet horror, no jump scare.

## D3 — The Wolfwarden's Vigil  ·  Act II  ·  mini-boss: **The First Wolfwarden** (Hollow Ones)

- **Where/when:** Thornwood deep; unlocked ~Wave 12, hero 6–10. Cold-biome edge.
- **Emotional beat:** this war is old; many already lost. The frost-spirits who came to seal the Wound from the north.
- **Rooms:**
  1. *Frozen kennels* — Feral Wolves (Wildlands pack-hunters) in packs; cold-biome dressing (frost, broken pens).
  2. *The vigil hall* — a long room where the Wolfwardens kept watch; Hollow Ones frozen mid-march.
  3. *Lore beat* — the first Wolfwarden's standing-stone: who they were, why they climbed down.
  4. *Boss room* — **The First Wolfwarden**: a large Hollow + wolf-pack adds; summons wolves at HP
     thresholds — kill the warden, the pack scatters.
- **Reward:** crystals + a frost-themed cosmetic; advances *The Cold-Wandered's Pack* questline.
- **Signature:** the Ice Wolf companion reacts at the threshold (ties to the storyline's Ice Wolf).

## D4 — The Folk Who Forgot (the Old Granary / Sunken Bell-Tower)  ·  Act II  ·  mini-boss: **The Inn-Keeper** (Hollow Ones)

- **Where/when:** Elarion outskirts → Thornwood; unlocked ~Wave 12, hero 6–10. Drives *The Folk Who Forgot* (5 beats).
- **Emotional beat:** the Hollow Ones here are the villagers of **Old Elarion** — they walked toward the
  Wound when a previous Keeper called for help, and never came back as themselves.
- **Rooms:**
  1. *The common room* — a ruined tavern/inn (KayKit bar set: bartop, bar_corner, mugs); Hollow villagers
     going through the motions of daily life (a haunting tableau).
  2. *The granary floor* — grain sacks, Hollow Warriors among the stores.
  3. *Lore beat* — the inn's ledger: names of the folk who left, in the Keeper's-predecessor's hand.
  4. *Boss room* — **The Inn-Keeper**: a Hollow who still "tends" the empty inn; tanky, calls the patrons
     (Hollow adds) to defend "his" house.
- **Reward:** crystals + a homestead cosmetic; the ledger fragment (Folk Who Forgot beat).
- **Signature:** the tableau of the dead keeping house — cozy turned elegiac.

## D5 — The Cold-Wandered's Pack (Frost-Stair)  ·  Acts II–III  ·  mini-boss: **The Mournful Alpha** (Wildlands)

- **Where/when:** Wintermere lower passes → highlands; unlocked ~Wave 12→18. Completes *The Cold-Wandered's Pack* (8 beats).
- **Emotional beat:** the frost-spirit pack made their last stand here sealing the Wound from the north;
  the Glass Cathedral (where *hers* still sleeps) is beyond.
- **Rooms:**
  1. *The frost-stair* — a vertical climb (KayKit stairs/columns) up the mountain interior; Feral Wolves + ice.
  2. *The denning hall* — the pack's home; Wolves + a Wildlands Caveman brute.
  3. *Lore beat* — the threshold the Ice Wolf won't cross; the pack's standing-stones.
  4. *Boss room* — **The Mournful Alpha**: the last of the pack; fast, leaping attacks, summons a wolf
     pack; a grief-fight, not a monster-fight (the Keeper mourns it).
- **Reward:** high crystals + the Ice-Wolf bond beat (companion lore); frost cosmetic.
- **Signature:** verticality (the only climbing dungeon) + the companion-at-the-threshold beat.

## D6 — The Last Keeper's Walk (Hollowmouth Antechamber)  ·  Act III  ·  mini-boss: **The Watcher** (Hollow Ones)

- **Where/when:** the road to the Hollow Deep; unlocked ~Wave 18, hero 10–15. Drives *The Last Keeper's Walk* (7 beats).
- **Emotional beat:** the previous Keeper (her master) walked toward the Wound to slow the Withering,
  knowing she wouldn't return — leaving **letters** at each threshold for the next Keeper.
- **Rooms:**
  1. *The threshold path* — a long approach lined with the master's letters (lore-stones, one per few beats).
  2. *The watch-room* — Hollow Ones that only animate when looked away from (a "Watcher" mechanic — they
     advance when off-screen/behind you; tension, not gore).
  3. *Lore beat* — the master's last letter, **ending mid-sentence**; the rest burned. (Per storyline, the
     player finishes it themselves in the questlog — the game records what they wrote.)
  4. *Boss room* — **The Watcher**: a tall, still Hollow that watches; teleports/repositions when unobserved;
     punishes tunnel-vision — fight while managing what you can see.
- **Reward:** top-grade crystals + the master's-letter completion (the most weighty beat pre-finale).
- **Signature:** the off-screen-advance mechanic + the player literally writing the ending of a letter.

---

## D8–D11 — additional dungeons (to reach the 10+ target, owner 2026-05-30)

Owner wants **at least 10 dungeons** with **Elden-Ring-grade depth** — real exploration, **hidden items
+ treasure**, branching rather than a single corridor. D1–D7 = the canon story arc (7); these four are
**side/optional dungeons** that fill out the world to 11 total and lean into exploration over story.
They're canon-adjacent (new names flagged for owner ratification) and slot into existing regions.

> **Depth principle (all dungeons, retrofit D2–D6 too):** Elden-Ring legacy-dungeon feel —
> **branching paths, optional side-rooms, hidden walls/items, a shortcut unlocked from deep back to
> entrance, and treasure that rewards thorough exploration** (not just the boss-room reward). Each
> dungeon should have ≥1 **hidden item** (illusory wall / off-path chest) and a **risk-reward optional
> branch** (a tougher sub-path with better loot). Build the critical path first, layer the optional
> depth second.

- **D8 — The Drowned Archive** (Mirewood/swamp; optional). A flooded library sinking into the mire —
  wade through rooms, rising water as a hazard/puzzle, books and a lost cartographer's map. Hidden:
  a submerged vault behind a collapsed shelf. Mini-boss: **The Archivist** (Hollow caster, flagged).
- **D9 — The Ember Forge-Deep** (Stoneback/Ashwood rock; optional). An abandoned dwarven-style forge in
  the rock — lava/heat hazards, ore veins (ties to the harvest economy), broken machinery. Hidden:
  a master-smith's cache. Mini-boss: **The Slag-Warden** (Wildlands brute / Bone-Golem variant, flagged).
- **D10 — The Hollow Orchard** (Goldfields edge; optional, cozy-dark). A once-beautiful orchard gone to
  the Withering — twisted trees, a caretaker's cottage, fruit that isn't fruit. Lighter horror, ties to
  the Healer's-garden motif. Hidden: a seed-vault. Mini-boss: **The Orchard-Keeper** (Hollow, flagged).
- **D11 — The Glass Cathedral (inner)** (Wintermere; story-adjacent). Named in the storyline (Act II/III)
  — where one of the frost-spirits, *hers*, still sleeps. A reverent, luminous ice-cathedral; mostly
  exploration + lore, a guardian rather than a brawl. Hidden: the sleeping frost-spirit's chamber.
  (Canon-named in `dungeons-storyline.md` — flesh out the kit, honor the reverent tone.)

**Total: 11 dungeons** — D1 (built), D2–D7 (story arc), D8–D11 (side/optional). Comfortably clears the
owner's 10+ target with room for more later.

## ⚠ FUTURE NOTE (TBD — owner, do not solve now): what makes a dungeon worth the journey?

Owner flag (2026-05-30): *"figure out what is of value in dungeons to justify such a journey."* **This is
deliberately unsolved for now — a parked design question, not part of any build WO.** Before the
non-story dungeons are built, decide what the **reward/value proposition** is so deep, dangerous
exploration feels *worth it* (the Elden-Ring "I must see what's down there" pull). Candidate value axes
to evaluate later (NOT decided):
- **Unique gear / weapons** found only in dungeons (not buyable) — the classic pull.
- **Crystal/resource caches** richer than open-world nodes (danger⇄reward at the dungeon scale).
- **Cosmetics / home & pet decor** (ties the cozy-collector loop to exploration — see WO-161).
- **Lore / questline progress** (story dungeons already do this; is lore *enough* for the optional ones?).
- **Permanent power** (a stat node, an ability, a recipe) — strongest pull, biggest balance risk.
- **Hidden/secret items** (illusory-wall treasure) as the connoisseur reward.
**Decision deferred.** When tackled, write it as its own "Dungeon Reward Economy" WO and retrofit the
treasure tables into D2–D11. For now the dungeons are designed *spatially + narratively*; the
*reward-value* layer is the open question to resolve before optional dungeons ship.

## Build order recommendation
D4 (Act II, reuses the cozy tavern kit — closest to D1) → D3 → D5 (verticality) → D2 → D6 (the Watcher
mechanic is the most novel/risky). D7 *At the Edge* (the Alduin **dialogue** finale, not a fight) is its
own narrative WO — designed in `dungeons-storyline.md` §4, not here.

## What this enables / reconciles
- All five reuse `DungeonController` + KayKit Dungeon Remastered + existing roster + WO-165 world portals
  + WO-164 ThreatLevel gating — **no new dungeon engine.** Each is room layout + encounter + lore content.
- Mini-boss names are canon (enemy-codex §0); kits are sketched here, stats live in `enemies.json`/`Defs.cs`
  when built (flag for owner ratification where a kit is new).
- Each advances a named questline beat from `dungeons-storyline.md` — they slot into the existing arc, not beside it.

🤖 Design doc (UI lane). No code/scene/bake. Grounded in dungeons-storyline.md, enemy-codex.md,
dungeons-3d-unity-layout-spec.md, kaykit-asset-catalog.md.
