# Defense Progression Depth Analysis — where the systems are too shallow (2026-05-30)

> Owner ask: *"find which areas need better depth — such as determining leveling defenses, earning new
> defensive structures."* The gap analysis found what's *missing*; this finds what's **thin** — systems
> that exist but lack the depth to carry a long-term progression/competitive game. Verified against code.
> Design/analysis only.

---

## TL;DR — the depth verdict

The defensive progression has **real scaffolding but shallow ceilings**. Three depth problems:
1. **Leveling defenses is too short + flat** — towers cap at **Level 3**, walls have minimal tiering.
2. **Earning new structures is half-wired** — a craft-skill gate (`SkillSystem`) *exists* but is thin,
   disconnected from a real unlock tree, and you can't research/discover genuinely *new* structure types.
3. **Too few defensive archetypes** — a handful of tower variants; no structural variety (traps, support
   buildings, defensive tiers of buildings) to make base-design a deep puzzle.

For a CoC×Warcraft competitive base-builder, **defensive depth IS the metagame** — it's what the arena
(NS end-game) tests and what whales optimize. Today it's a starter portion.

---

## 1. Leveling defenses — TOO SHORT and TOO FLAT

**What exists (verified):**
- `Tower.cs`: `MaxLevel = 3` — towers upgrade L1→L2→L3, then "Empowerment" (a one-time max-level prestige
  state, not a 4th level). Per-level data in `TowerData.upgrades[]`.
- Walls: `WallSegment` exists; tiers (wood→stone→reinforced) are **spec'd (WO-151) not built** — currently
  near-flat.

**Why it's shallow:**
- **3 levels is a weekend, not a progression.** CoC towers go to 15–20 levels; the *long grind* of
  inching a defense up is the retention + spend engine. 3 levels caps the climb almost immediately.
- **Flat scaling.** Levels mostly bump numbers (damage/HP). No **qualitative** changes (new behavior,
  new targeting, splash→chain, range breakpoints) that make each level *feel* different and force base
  re-design. Depth = levels that change *how* a defense plays, not just its stats.
- **Empowerment is a single binary** instead of a branching choice.

**Depth fixes (design directions):**
- **Extend the level ceiling** (e.g. 3 → 8–10+), with **qualitative breakpoints** every few levels (L3
  unlocks splash, L6 unlocks chain, L9 unlocks air-targeting) — not just +damage.
- **Branching upgrades** — at a tier, choose path A (single-target/high-dmg) vs path B (AoE/crowd) —
  so two players' same tower play differently (build-identity + arena depth).
- **Wall tiers with teeth** (WO-151): wood→stone→reinforced shouldn't just be HP — each tier changes the
  *defensive read* (reinforced reflects/slows, etc.).
- **Empowerment → a small choice tree** (pick an empowerment per maxed tower), not one fixed state.

## 2. Earning NEW defensive structures — HALF-WIRED, no real unlock tree

**What exists (verified):**
- `SkillSystem.cs` — craft-skill levels (**Blacksmith / Woodworking / Arcane**) that **gate tower
  placement** via `HasRequiredSkill(SkillRequirement)`; skills start at 0, points granted on hero
  level-up, spent via a popup. Per-tower gate levels live in `TowerData`.
- So a gating *mechanism* exists — but it's the skeleton, not the body.

**Why it's shallow:**
- **No tech/unlock TREE** — there's a gate (`HasRequiredSkill`) but no *structured progression* of what
  unlocks what. You raise a craft skill and… a tower becomes placeable. There's no map of "research X to
  unlock Y to unlock Z" — the satisfying part of "earning new defenses."
- **Few things to actually earn.** The unlock gate guards a **small set of tower variants**; there's no
  deep catalog of *new* defensive structures to chase (traps, support buildings, wall types, special
  emplacements). Earning is only meaningful if there's a rich tree of *new kinds* of defense beyond it.
- **Disconnected from the loop's rewards.** Earning a structure should come from *playing the loop*
  (clearing waves, holding nodes, dungeon finds, arena rank) — right now it's gated only on hero-level
  skill points, not on the rich activity the world now offers (nodes/tribes/dungeons).

**Depth fixes (design directions):**
- **A defensive tech tree** — a real branching unlock map: clear content / spend resources / hit skill
  levels → unlock new structure *types* (not just upgrade existing ones). The `SkillSystem` gate is the
  hook; it needs a *tree* behind it.
- **Many more earnable structures** (see §3) so the tree has rich nodes to chase.
- **Multiple unlock currencies/sources** — tie unlocks to the systems we just designed: a dungeon
  (D2–D11) drops a structure blueprint; holding a deep node unlocks a regional defense; arena rank
  unlocks prestige defenses. **Earning a new defense becomes a reason to do everything else.**
- **Blueprints/recipes as loot** — the dungeon "what's worth the journey?" question (parked) — *new
  defensive structures* is a strong candidate answer.

## 3. Too few defensive ARCHETYPES — base-design isn't a deep puzzle yet

**What exists:** a few tower variants (Flame/Ice/Aether/Physical per the BuildMenu table) + walls + the
5 gameplay buildings. That's enough to *start*, not enough for a **base-design metagame**.

**Why it's shallow:** in CoC, base-design depth comes from **many interacting defensive roles** — splash
vs single-target, air vs ground (the NS calls out the air/ground counter axis as the evergreen lever),
traps, walls as funnels, support buildings. With only ~4 tower flavors, there's little **layout puzzle** —
and the arena (where this depth is *tested*) needs it most.

**Depth fixes (design directions):**
- **More tower roles** along real axes: single/splash, ground/air (already half-built via dragons!),
  short/long range, support (buff/slow/heal-defenders), anti-ranged, anti-swarm.
- **Traps & one-shot defenses** (CoC's bombs/spring traps) — cheap, hidden, reset between attacks; huge
  base-design depth for low art cost.
- **Defensive support buildings** (a tower that buffs adjacent towers; a wall type that funnels) — makes
  *placement* matter (the CREATE verb's whole point).
- **The air/ground axis** (NS evergreen lever) — formalize flying vs land as a defense-targeting dimension
  (some towers hit air, some don't), which the dragons already set up.

---

## The crafting spine — Warcraft model (owner 2026-05-30): resources → upgrade the Forge → unlock better defenses

Owner's crafting model, and it's the **unifying spine** for everything above: *"like Warcraft — collect
resources to upgrade the forge."* The Forge (and the resource buildings) **are the tech tree** — you
don't unlock a better tower from an abstract menu; you **pour your harvest into upgrading the Forge, and
the Forge's tier is the gate** that unlocks the next tier of weapons, towers, and defensive structures.

```
HARVEST resources (nodes/settlements/mines)
        ↓  spend
UPGRADE the Forge / resource buildings (Blacksmith, Lumbermill, Arcane, etc.)  ← WO-151
        ↓  raises
your craft tier (Blacksmith / Woodworking / Arcane — the EXISTING SkillSystem gate)
        ↓  unlocks
the next tier of DEFENSIVE STRUCTURES + tower-upgrade levels  ← §1/§2 depth
        ↓  enables
holding deeper/richer nodes + bigger raids + arena climb
        ↓  earns more resources → upgrade the Forge again (repeat, bigger)
```

**This unifies three things this doc treated separately:**
- **WO-151** (Forge/Armory building upgrades) = the buildings whose **tier is the gate**.
- **The "earn new structures" gap (§2)** = earning IS "upgrade the Forge to the tier that unlocks it" —
  Warcraft's "upgrade the Blacksmith to research the next weapon tier." **Not a separate abstract tree —
  the building upgrades ARE the tree.**
- **`SkillSystem`** (Blacksmith/Woodworking/Arcane craft levels, already the placement gate) = should be
  **driven by the building upgrades**, not float independently. Upgrading the Forge raises Blacksmith tier
  → Blacksmith tier gates the better towers (the gate `HasRequiredSkill` already checks). The plumbing
  half-exists; this connects it into the Warcraft loop.

**Design implication:** the **defensive tech tree (§2 fix) = the Forge/building upgrade ladder.** Each
Forge tier (paid in harvested resources) unlocks: a new tower archetype, the next tower-level breakpoint,
a wall tier, a trap. So **resources are the currency of defensive depth**, the Forge is the spine, and the
whole "collect → upgrade → unlock → defend → collect more" loop is one Warcraft-shaped engine. This is the
canonical crafting/unlock model — fold it into the tech-tree + tower-progression specs below.

### Refined-goods production chain (owner 2026-05-30): raw → ingot → weapon upgrade

Resources aren't spent **raw** — upgrading the Forge **unlocks the ability to refine** them, and the
*refined* good is what upgrades weapons/defenses. The first chain: **Forge tier unlocks smelting raw
Iron → Iron Ingots → Ingots upgrade weapons.** You can't upgrade a weapon with ore; you need ingots, and
only an upgraded Forge can smelt them.

```
mine raw Iron (nodes/settlements)
   ↓  (requires Forge tier N — UNLOCKED by upgrading the Forge)
smelt Iron → Iron Ingots   (a refining step at the Forge)
   ↓
spend Ingots → upgrade weapons / defensive structures
```

**Why this is real depth (not just a longer cost string):**
- **Multi-step economy** — gather → **refine** → craft. The intermediate good (ingot) is a gate and a
  buffer; refining capacity (Forge tier) becomes its own thing to invest in.
- **Unlocking the refinement is a reward** — "you can now form Iron into Ingots" is a satisfying Forge-tier
  unlock in itself (Warcraft/survival-craft beat), separate from the upgrade it enables.
- **Extensible pattern** — the same chain generalizes: Wood → Planks (Lumbermill tier), Aether → refined
  Aether/cores (Arcane tier), etc. Each resource building unlocks its own refine step → its own craft
  outputs. A **production-chain tree**, not a flat shopping list.
- **Ties harvest → buildings → defense tightly** — raw nodes feed refineries (the buildings you upgrade)
  feed weapon/defense upgrades. Every link is a place to invest and a place depth lives.

**Data shape (for the eventual WO):** add **refined-good resource types** (IronIngot, Plank, AetherCore…)
to the economy; a **refine recipe** (raw + Forge-tier requirement → refined) run at the building; weapon/
structure upgrades consume the **refined** good. Reuse `EconomyService` + the WO-151 `BuildingUpgrade`
tier as the unlock gate. No new currency *system* — new resource *entries* + a refine step.

### The refinery family + the Jeweler (owner 2026-05-30): rare stones, and craft FAILURE as a depth lever

The refine step is a **family of buildings**, one per material branch — same gather→refine→craft spine:

| Refinery | Raw → Refined | Crafts/upgrades | Skill gate |
|---|---|---|---|
| **Forge / Blacksmith** | Iron → **Ingots** | weapons, metal defenses | Blacksmith tier |
| **Lumbermill** | Wood → **Planks** | wall tiers, wood structures | Woodworking tier |
| **Arcane** | Aether → **Cores** | arcane towers, spells | Arcane tier |
| **Jeweler** (later) | **rare stones → cut gems / jewelry** | high-end gear sockets, prestige/cosmetic, power gems | **Jeweler tier** |

**The Jeweler introduces CRAFT FAILURE — a new depth lever (owner: "sometimes jewelry can fail if not
skilled enough").** Unlike smelting (deterministic), **cutting a rare stone can FAIL if your Jeweler tier
is too low for that stone's rarity** — a skill-vs-rarity gamble. The rarer the stone, the higher the tier
needed to reliably succeed; attempt above your skill and you risk **losing the stone**.
- **Why it's good depth:** makes Jeweler tier *matter* (you level it to cut failure odds), makes rare
  stones genuinely **precious** (a failed cut hurts), and adds a **risk/decision** to crafting that the
  deterministic refineries don't — the connoisseur/whale end of the craft tree.
- **The success curve = `f(Jeweler tier, stone rarity)`** — high tier vs low-rarity stone ≈ guaranteed;
  low tier vs top-rarity stone = real failure %. Same danger⇄reward dial as the rest of the game, applied
  to crafting. Tune in data, not hard-coded.
- **⚠ Craft-failure knife-edge (design with care):** failure-on-craft is *satisfying* or *infuriating*
  purely on tuning. Guardrails to bake in: **telegraph the odds** (show success % before committing — an
  informed gamble, never a surprise), **partial mitigation** (a failed cut returns *some* of the stone /
  a lesser gem, not pure loss — or a consumable/insurance that prevents loss), and **never gate core
  power behind a coin-flip** (jewelry = prestige/flex/marginal edge per the NS "sell flex not power"
  rule, not mandatory power you must gamble for). Failure should sting, not wall.
- **Ties to:** rare stones as **dungeon loot** (the parked "what's worth the journey?" question — rare
  gem-stones are a strong answer), to the cozy/collector loop (jewelry = cosmetic flex), and to the
  monetization "flex not power" guardrail.

### High skill → "+1" crit-craft with a random perk (owner 2026-05-30): the Final Fantasy upside

Skill doesn't just *reduce failure* — at **high skill it unlocks a better-than-normal result**: a
**"+1" version of the item with a random perk** (Final Fantasy HQ/synthesis: FFXIV high-quality procs,
FFXI HQ crafts). So a craft is a **three-way outcome**, not pass/fail:

| Outcome | When | Result |
|---|---|---|
| **Fail** | skill ≪ rarity | lose/partial (mitigated, telegraphed — see above) |
| **Success** | skill ≈ rarity | the normal item |
| **+1 crit-craft** | **skill ≫ rarity** (high skill) | a **superior "+1" item with a RANDOM PERK** (a unique modifier) |

- **Random perk** = a rolled modifier on the +1 item, from a data-authored perk pool per item type. Two
  broad **categories** (perks are NOT combat-only — owner 2026-05-30):
  - **Combat perks** — *elemental riders* (+adds fire/ice/aether on hit), *status procs* (+20% bleed,
    poison, slow), *% modifiers* (+crit, +burn duration, +range).
  - **Utility / economy perks** — *+increase harvest rate*, *+refine yield*, *+build speed*, *+offline
    cap*, *+move speed*, etc. (owner example: "+increase harvest rate"). These feed the harvest/build loop
    instead of a fight.
  - **Why two categories matters:** +1 crafting then rewards **both player types** — the conqueror chases
    combat perks; the cozy/economy player chases utility perks (harvest rate, yield, build speed). One
    crafting system, both audiences invested.
  - The **full perk list is a creative/design open item** — seed examples above, not a locked set; the
    pool is data-authored per item type so creative can expand it freely (combat pool for weapons/towers,
    utility pool for tools/buildings, mixed for gear). Every +1 rolls one (or a few) — every +1 is a
    little different, which is the chase.
- **Why this is the best depth lever yet:** it makes **high skill aspirational, not just safe** — a master
  doesn't merely avoid failure, they *roll for greatness*. It creates **replayable crafting** (re-craft
  chasing a good perk roll — strong retention), a **loot/identity layer** (your +1 with *its* perk is
  uniquely yours — a flex), and a **connoisseur/whale sink** that pays into mastery.
- **+1 chance scales with skill-over-rarity** — same dial, top end: the further your skill exceeds the
  recipe's rarity, the higher the +1 proc %. So leveling the Jeweler/Forge has a *ceiling reward* (more
  +1s), not just a floor (fewer fails). One curve, both ends.
- **⚠ Guardrail (NS "flex not power"):** +1 perks should be **lateral/flavorful or marginal**, not
  mandatory power you *must* roll to compete — otherwise it becomes pay/grind-to-win that poisons the
  arena. Make perks *interesting* (a new behavior, a counter-pick edge) more than *strictly stronger*.
  The +1 is a **chase + identity**, not a power gate. Telegraph the +1 odds alongside the success odds.

**This applies to ALL refineries, not just the Jeweler** — a master Blacksmith rolls +1 ingots/weapons,
a master Arcane rolls +1 cores. The Jeweler is just where it's *most* visible (gems = the showcase).

> The Jeweler + failure + **+1 perk** mechanics are **later** (after the Forge/refinery spine is built) —
> captured here as the canonical extension so the production-chain WO designs the refine step generically
> enough to support **three outcome tiers** (fail / success / +1-with-random-perk) and a per-item **perk
> pool**, alongside the simple deterministic recipes. Build deterministic first; layer outcomes later.

## Priority — where depth pays back most

| Area | Current depth | Payoff of deepening | When |
|---|---|---|---|
| **Tower level ceiling + qualitative breakpoints** | 3 flat levels | the core grind/retention/spend; arena depth | high — after build mode (it's what you upgrade) |
| **Defensive tech/unlock tree** (earn new structures) | gate exists, no tree | "earning new defenses" = the chase; ties world→base | high — pairs with build mode |
| **More defensive archetypes + traps + air/ground** | ~4 tower flavors | base-design metagame; the arena's whole point; evergreen meta lever | medium — feeds the arena end-game |
| **Wall tiers with qualitative change** | near-flat (WO-151 spec) | the CoC sink; defensive read | medium — WO-151 |

> **The throughline:** all three depend on **player build mode (the keystone gap)** existing first —
> there's no point in deep upgrade trees and many archetypes until the player is *placing and arranging*
> defenses themselves. So: **build mode → then deepen leveling + the unlock tree + archetypes.** Depth
> without the CREATE verb is upgrading a base you didn't build.

## Recommended next docs/WOs (when prioritized — NOT now, CLI is mid-build)
1. **Defensive Tech Tree spec** — the branching unlock map (what earns what; sources = waves/nodes/
   dungeons/arena). The "earning new structures" answer.
2. **Tower Progression v2 spec** — extend ceiling + qualitative breakpoints + branching upgrade paths.
3. **Defensive Archetype Catalog** — the full roster of tower roles, traps, support buildings, air/ground.
   (Pairs with the parked "dungeon rewards = blueprints" question.)

🤖 Analysis (UI lane). Verified against `Tower.cs` (MaxLevel=3), `SkillSystem.cs` (craft-skill gate),
`TowerData`/BuildMenu variants, WO-151 (wall tiers spec). No code/scene/bake.
