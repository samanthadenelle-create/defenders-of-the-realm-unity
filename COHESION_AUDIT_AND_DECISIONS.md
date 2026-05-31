# Cohesion Audit & Decisions Needed (2026-05-31)

> A senior systems cohesion review of the full design corpus (19 docs). Verdict: **~70% is ONE tight,
> coherent game** (the CoC×Warcraft base-builder — the North Star), **but recent design energy inverted
> the North Star's own priority** and grew a *second* combat paradigm (the FF party-RPG) that doesn't
> connect to the monetizing endgame. This doc records the findings + the **3 decisions that unblock the
> most.** Owner-level calls. UI lane; no code/bake.

## Verdict
The spine is clean and singular: **build your stronghold → claim/auto-harvest resource nodes via
settlements → refine through the production-building tech tree (Lumbermill/Forge/Armourer/Arcane Library)
→ pour into super-linear defensive upgrades → defend vs randomized tribe raids + danger-tiered open world
→ grow offline → compete in the async Arena.** ~70% of the corpus serves this directly and reinforces
itself well. The problem is the *other ~30%* and one structural fork.

## What's COHESIVE (keep, it's working)
- **Crafting spine** = one idea across RESOURCE_ECONOMY + DEFENSE_DEPTH + WO-180 (refine→tier-gates→upgrade).
- **Danger⇄reward dial** (`ThreatLevel`) = one dimension read by zones/enemies/nodes/raids/encounters/red-skull.
- **WO-159 + WO-160** = harvest + threat, explicitly two halves of one loop.
- **WO-108 build mode** = correctly the keystone; downstream docs respect the dependency.
- **+1 perks** span both player types (combat + utility perks loop back to the economy).

## ★ GUIDING PRINCIPLE (owner 2026-05-31): FUN FIRST, MONETIZATION SECOND
Every sequencing + design call resolves through this: **build what makes the game fun to play first; layer
monetization on top once the fun is proven.** Never tune a mechanic for revenue before it's tuned for feel.
This is the right instinct — crypto/mobile graveyards are full of games that built the money first and
forgot to be fun. Concretely it means:
- **The fun loop drives the build order; monetization follows.** Timers, rewarded ads, packs, crypto, the
  Arena pot — all are **opt-in layers added AFTER the loop is enjoyable**, and must never block or uglify
  the fun (NORTH_STAR's own "sell flex not power" + "opt-in, never a wall" discipline, applied to the queue).
- **Tune for feel, then for money.** Glimmer rates, build-timer length, "what's worth the journey," node
  pacing → tune them to feel rewarding/well-paced FIRST; dial the monetization knobs second.
- **This reframes Decision 1:** the FF party-RPG is clearly a big part of the fun being chased — so the
  answer is NOT "cut it because the Arena monetizes." It's: **build the fun (party/ATB/exploration RPG) as
  a real mode, and let the Arena + monetization be the layer that comes after the fun loop works.** Fun is
  the product; money is the layer.

## ⛔ THE 3 DECISIONS THAT UNBLOCK THE MOST (owner-level)

### DECISION 1 — The combat-model fork (THE biggest hole)
**The game has TWO combat engines that don't connect:**
- **Real-time** — village tower-defense + the Arena (army-vs-base async PvP). The North Star says THIS is
  the game, and the combat-AI depth (FindBestTarget → group tactics) is justified *entirely by the Arena.*
- **Turn-based 2D ATB party** — the party of four, bard, talents, legendary gear powers all live HERE.
- **They share vocabulary (roles, focus-fire) but are NOT the same brain.** A turn-based party of 4 ≠ a
  real-time deployable army running NavMesh maneuvers.
- **The party/gear/talents/bard — the biggest recent investment — feed the ATB battle, which feeds neither
  the core loop nor the monetizing Arena.**

**DECIDE:** does the party/gear/talents/bard feed **(a)** village defense + the Arena (one combat model,
the RPG layer powers the loop), or **(b)** is the FF-RPG a **distinct MODE** (a PvE/story campaign reached
from the world — dungeons/encounters) that runs alongside the base-builder?

**Fun-first read → (b), built as a real, loved mode (not a leftover):**
- The party RPG is clearly a big part of the fun being chased — so don't cut it. **Make it a first-class
  PvE/exploration mode:** the world + dungeons + encounters → the **2D ATB party battle** is where the
  party, gear, talents, bard, and legendaries all live and matter. That's a genuinely fun game on its own.
- The **base-builder + village defense + Arena** is the *other* pillar, real-time (towers + hero + army),
  and it's where the **competitive endgame + monetization** layer eventually sits.
- **Two fun pillars, one world, shared progression** (your hero/level/resources/crystals span both): the
  RPG mode is the *adventure*, the base-builder is the *home you build and defend*. They don't need the
  SAME combat engine — they need a **shared character + economy** so playing one feeds the other.
- **The honest win:** neither combat model is half-built pretending to be the other. ATB = the RPG mode;
  real-time = defense/Arena. Build whichever is *more fun to reach first* (the village loop is closer to
  playable; the RPG mode is the deeper hook) — fun decides the order, not money.
- **Still the owner's call** — but "fun first" says **keep both, scope them as distinct modes, share the
  character/economy spine, and let monetization layer onto whichever proves fun first.**

### DECISION 2 — Close the wallet merge (nothing's real until this lands)
The wallet exists in **three** places (GameState / ResourceBalance duplicate+ManaCrystals /
EconomyService mirror). **No economy number can be trusted until it's one source of truth.** Decide:
**one crystal or N** (AetherCrystals vs Crystals vs ManaCrystals), make `GameState` canonical, EconomyService
operates on it. This is RESOURCE_ECONOMY "Step 0" — it gates WO-108, WO-151, WO-159, WO-172, WO-180.

### DECISION 3 — Resolve the canon fork (before more story layers on it)
PARTY_OF_FOUR already flagged: **living-Tree vs burned-Spire premise**, **3 apex antagonists**
(Alduin/Syndrath/Alerion), **"Avalon" still live in ~9 docs (a CLAUDE.md §7 HARD-RULE violation)**, pet-name
drift, Bram collision. LEGENDARY_GEAR + DUNGEONS already build on the unresolved premise/antagonist fork.
**Ratify the premise + the one antagonist + purge "Avalon"** before any more story/gear/dungeon content.

## Connectable GAPS (not forks — just wire them)
- **Two harvest models unreconciled** — North Star's *in-base* mines vs WO-159's *open-world* node
  settlements. Same verb, two locations. Decide: same system in two places, or two systems? (Recommend:
  one settlement/harvest system, placed both in-base and in the world via build mode.)
- **Offline accrual** — referenced by 5 docs, designed by none. The retention spine needs its flow (cap
  curve, what accrues, how it interacts with the 3-day razed lockout). **Design it.**
- **Food→Population** — connects to nothing downstream but a vague gather multiplier. Give it a real
  consumer (settlement count? garrison-unit cap? worker pool?) or keep it minimal.
- **Scroll units vs build-mode structures** — scrolls unlock *units*; build mode places *structures*. The
  unit deployment model (grid? population cost? mobile?) is undesigned. Wire it to WO-108.
- **WO-108 self-contradiction** — the build-ready header says `CatalogEntry`/`yawSteps`/`BaseLayout`; the
  body still says `BuildableItem`/`yawDeg`/`PlacedStructures`. Clean the body to match the header.

## OVERLOAD — apply the North Star's own test to the design QUEUE
The base-builder is ~1 game; the corpus describes ~2.5. Recent energy poured into the RPG/story/party layer
— the exact "off to the side / end-game later" tiers the North Star *parked.* Apply "does this feed
CREATE→HARVEST→DEFEND, or sit beside it?" to the queue:

**ESSENTIAL (build, in order):** WO-108 keystone · wallet merge · WO-159+160 (harvest+threat) · WO-180 +
DEFENSE_DEPTH (sinks) · offline accrual (design it) · WO-172 (timers+ads) · ZONE_STREAMING (design-now).

**DEFER (Phase B/C — good work, not core; the docs themselves say "after the keystone"):** PARTY_OF_FOUR ·
BATTLE_2D_PARTY · BARD (clearest defer — 3 levels removed from the loop) · TALENT_V2-as-combat ·
LEGENDARY_GEAR · ENCOUNTER · DUNGEONS D2–D11 (esp. D8–D11; rewards unsolved) · SCROLL_BLUEPRINT.
→ All of this lights up *after* the combat-model fork (Decision 1) is resolved + the keystone is built.

## Top 3 actions (in order)
1. **Decision 1** — write the combat-model reconciliation (party → defense/arena, or RPG = separate mode). Unblocks the most.
2. **Decision 2** — close the wallet merge / "one crystal" (RESOURCE_ECONOMY Step 0).
3. **Decision 3** — ratify canon (premise + antagonist) + purge "Avalon."

> The design has tremendous coverage and a genuinely cohesive *core*. The fix isn't more design — it's
> **three decisions + disciplining the build queue to the North Star's own priority.** The RPG layer is
> excellent and worth building — *later, and as a clearly-scoped mode*, once the loop it's supposed to
> live inside actually exists.

🤖 Cohesion audit by a systems-design review pass over all 19 docs. Findings + decisions; no code/bake.
