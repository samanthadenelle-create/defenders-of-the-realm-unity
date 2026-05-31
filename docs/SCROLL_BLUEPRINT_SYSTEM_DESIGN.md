# Encrypted Scrolls → Decoded Blueprints → New Defensive Units

> Owner ask (2026-05-30): hidden treasures (scrolls) found while exploring that **teach how to build new
> defenses** — they're encrypted and must be **decoded by someone special** (a Scholar/Sage), and decoding
> **unlocks new defensive UNITS**. This is the tangible "earn new defenses" loop the defense-depth doc was
> reaching for — discovery + a gatekeeper NPC, not an abstract skill unlock. Creative/design only.
> Reconciles to ENCOUNTER_SYSTEM, DUNGEON_DESIGNS, DEFENSE_DEPTH_ANALYSIS, the mourning lore. No code/bake.

---

## The loop (one line)
**Explore → find a hidden encrypted scroll → bring it to the Scholar → instant decode → a new defensive
UNIT is unlocked in your build catalog → build/deploy it.**

Knowledge is the gate, not just resources or level. You don't *buy* a new defender — you **discover the
lost knowledge of one and have it translated.**

## The four pieces

### 1. The hidden scroll (the treasure)
- A **findable item** seeded in the world + dungeons: behind illusory walls, in caches, off the beaten
  path (the Elden-Ring "thorough exploration" reward; ties ENCOUNTER_SYSTEM discovery finds + DUNGEON
  hidden items). Deeper/more-dangerous = rarer scrolls (danger⇄reward).
- **Encrypted on pickup** — you can read the *flavor* ("a brittle scroll in a script older than Elarion")
  but NOT use it. It sits in your inventory as a locked item with a "needs decoding" tag.
- **On-tone (mourning lore):** scrolls are the **lost knowledge of the dead** — old Keepers, the First
  Light, fallen defenders, the Wound's victims. A blueprint is *recovered* knowledge, grief made useful.
  Some scrolls carry a lore fragment that decodes alongside the unit (story + reward in one).

### 2. The Scholar / Sage NPC (the decoder — owner: dedicated NPC)
- A **dedicated Scholar/Sage** character — a Loremaster, or the Apothecary's scholarly successor (ties
  the healer/apothecary lineage in canon). Lives in the **village** (a study/library — could be a building
  interior, ties WO-161) so the player always has somewhere to bring scrolls.
- **Bring scroll → instant decode on delivery** (owner-locked): hand it over, it's translated immediately,
  the blueprint unlocks now. No timer, no cost — clean and satisfying; the *finding* was the work.
- The Scholar is also a **lore voice** — on decode, a short line about what was recovered (keeps the
  mourning thread + makes the unlock feel earned/storied, not a vending machine).
- *(Optional later: a "find the Scholar first" early beat — but base case is the Scholar is in the village.)*

### 3. The decoded blueprint (the unlock)
- Decoding converts the locked scroll → a **permanent blueprint** added to your **defensive catalog /
  tech tree** (DEFENSE_DEPTH_ANALYSIS — this is a concrete unlock source feeding that tree).
- The blueprint unlocks a **new defensive UNIT** (owner) — see below. Once unlocked, it's buildable like
  any catalog entry (build mode WO-108 / the StructureFactory catalog).
- Persisted (a decoded-blueprint set in GameState) — once learned, always available.

### 4. New defensive UNITS (the payoff — owner: units, not just structures)
The reward is **defensive UNITS** — deployable defenders that fight, distinct from static towers/walls.
Examples (data-authored, creative-expandable):
- **Garrison troops** — spearmen/archers you station on the walls/ramparts (man the battlements you built).
- **Guardian constructs / automatons** — a built golem/sentinel that patrols and fights (lost First-Light
  craft = strong scroll fantasy).
- **Beast/creature defenders** — a tamed or summoned creature bound to defend (ties the pet/companion motif).
- **Specialist defenders** — a healer-unit that mends walls/allies, a sapper that counters siege, an
  anti-air archer (the air/ground counter axis — NS evergreen lever).
> Units complement the existing **towers/traps/walls** archetypes (DEFENSE_DEPTH_ANALYSIS §3) — now the
> defense roster grows in *unit* form, deepening the base-design puzzle (where do I station my troops?).

## Why this is strong
- **Answers "earn new defenses" concretely** — discovery + decode is the mechanic; the Scholar is the
  gatekeeper; the tech tree is the destination. Knowledge as the gate is more flavorful than a skill bar.
- **Makes exploration *matter for your base*** — a scroll found in deep Ashwood becomes a wall-garrison
  back home. Welds the open-world loop to the base-building loop (the two halves of the game meet).
- **Answers the parked dungeon-reward question** — scrolls/blueprints are a top "what's worth the journey"
  reward, alongside legendaries and rare stones.
- **On-tone** — recovered knowledge of the dead = grief made useful = the mourning story, mechanized.
- **Cheap evergreen content** — a new defender = a new scroll + catalog entry. Drip new units for years.

## How it reconciles (build on, don't reinvent)
- **Scroll item + decoded-blueprint set** → data + GameState (persisted). Scholar = an NPC + a simple
  "deliver scroll → unlock blueprint id" interaction (instant).
- **Blueprint → catalog entry** → reuse `CatalogRegistry`/`StructureFactory` (WO-148) + build mode (WO-108)
  — a decoded blueprint just registers/unlocks a catalog id. Defensive **units** are catalog entries of a
  unit type (deployable defenders) alongside structures.
- **Scrolls placed by** the encounter/dungeon loot system (ENCOUNTER_SYSTEM, DUNGEON_DESIGNS hidden items).
- **Feeds** the defensive tech tree (DEFENSE_DEPTH_ANALYSIS) — scrolls are an unlock source for it.
- No new currency; no UXML (code-built Scholar UI); reuse the catalog/build systems.

## Open questions for owner
- **Scholar location** — a village building (a Library/Study — ties WO-161 interiors), or a standalone NPC?
- **Defensive unit cap / upkeep** — do deployed units cost population (Food→population loop) or a unit cap?
  (Recommend units draw on population — ties the Food/population economy; a reason population matters.)
- **Are some scrolls partial** — collect 2-3 fragments to decode one blueprint (a collection chase), or
  one scroll = one unlock? (Recommend mostly 1:1, a few multi-fragment legendaries for the big units.)
- **Do scrolls also unlock non-unit things** (a tower tier, a trap, a craft recipe), or strictly units?
  (Owner said units — keep units as the headline; could extend later.)

🤖 Creative/design doc (UI lane). Reconciled to ENCOUNTER_SYSTEM_DESIGN, DUNGEON_DESIGNS,
DEFENSE_DEPTH_ANALYSIS, catalog/build-mode (WO-148/108), the Food→population loop, the mourning lore.
No code/scene/bake.
