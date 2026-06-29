> ⚠ **PARTIALLY SUPERSEDED 2026-06-28.** Live V1 resource set is **WOOD / IRON / GRAIN** (+ gold store currency) — the Stone/Aether/Gems faucet set below is stale, and the live offline-harvest faucet is the **Echo workforce** (`ECHO_WORKFORCE_SPEC.md`, `OfflineHarvestService`). The flow/refine/sink/pacing-curve *design thinking* below still informs the (V2-gated) economy depth; treat numbers + resource list as superseded. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# Resource Economy Design — how it actually flows (the never-designed layer)

> Owner gap (2026-05-30): *"we have never designed HOW it happens, only that it needs to happen."* Every
> sink is designed (build mode, wall tiers, Forge upgrades, refining, +1 crafting) and the faucets are
> named (nodes, settlements, mines, waves, offline) — but the **economy itself** (rates, flow, balance,
> the conversion chain, the pacing curve) was never drawn. This is that doc.
>
> **Pacing (owner): HYBRID — fast early, slow late** (proven F2P curve: hook fast, long endgame grind).
> **Depth (owner): full flow + relationships, with TUNABLE numbers** (relative/placeholder values CLI
> implements as data/constants, NOT magic numbers — playtest tunes the absolutes). Design only.

---

## ⚠ PREREQUISITE — fix wallet fragmentation FIRST (a real build issue, not just design)

Verified: resources are defined in **three places** —
- `GameState.cs` top-level: `Wood / Stone / Iron / AetherCrystals`
- `NestedTypes.cs` `ResourceBalance`: `Crystals / Food / Coins / Stone / Iron / Wood` (**duplicate set**) +
  a separate `ManaCrystals`
- `EconomyService` keeps its **own** `_wood/_stone/_iron/_crystals` mirror fields

This is the WO-131 wallet-fragmentation bug, **not fully closed.** No economy design can be balanced on a
wallet that exists in 3 copies. **Step 0: one source of truth** (recommend `GameState` as canonical;
`EconomyService` reads/writes it, never mirrors; collapse the duplicate `ResourceBalance` fields; decide
`AetherCrystals` vs `Crystals` vs `ManaCrystals` = ONE crystal). Until this is done, "the HUD shows X but
spend takes from Y" keeps happening. **This is the foundation the numbers below sit on.**

---

## 1. The resource set (canonical, after the merge)

| Resource | Tier | Source | Role |
|---|---|---|---|
| **Wood** | raw | Lumbermill nodes/settlements | early building, wall tiers |
| **Stone** | raw | Stoneback/Goldfields nodes | walls, structures |
| **Iron** | raw | Stoneback nodes | weapons, mid structures |
| **Aether Crystal** | raw (premium-ish) | crystal mines (WO-153), rare spawns (WO-154), waves | high-tier upgrades, the one crystal |
| **Food** | raw | Farm | **grows population** (see §Food→Population below) — NOT an upkeep drain |
| **Coins** | soft | waves, selling, encounters | flexible soft currency, store |
| **Rare Stones** | rare raw | deep nodes, dungeons, encounters | Jeweler input (the gamble craft) |
| **Ingots / Planks / Cores / Cut Gems** | **refined** | refineries (Forge/Lumbermill/Arcane/Jeweler) | the *actual* upgrade currency |

**Key principle (from the crafting design): you don't upgrade with RAW — you refine first.** Raw is the
faucet; refined is what sinks consume. The refinery is the throttle between them.

---

## 2. The flow (faucet → refine → sink), with the pacing dial

```
FAUCETS (raw in)                 REFINE (throttle)            SINKS (refined out)
─────────────────                ─────────────────            ───────────────────
nodes/settlements  ─raw─►  Forge/Lumbermill/Arcane/Jeweler ─refined─►  build mode, wall tiers,
waves (combat)     ─raw─►        (rate gated by building tier)         Forge/building upgrades,
offline accrual    ─raw─►                                              weapon/+1 crafting,
encounters/dungeons─raw─►                                              settlement claims, towers
                                                                       ▲
store: buy raw with Coins/premium ──────────────────────────────────┘ (the un-stick valve)
```

**Three faucet types (different cadences, so the economy breathes):**
1. **Active** — you harvest/fight now (nodes while present, wave drops, encounter finds). Fast, engaged.
2. **Passive** — settlements/mines trickle while you play elsewhere in the world.
3. **Offline** — accrues while the app is closed, **up to a cap** (the "come back richer" hook). Cap forces
   you to return + spend (anti-hoard, pro-retention).

---

## 3. The HYBRID pacing curve (fast early → slow late) — the numbers' shape

The numbers serve **time-to-milestone**, not the reverse. Target *feel*:

| Milestone | Target time-to-reach | Why |
|---|---|---|
| First wall/tower placed | minutes (starting resources cover it) | instant agency — the hook |
| First building upgrade (Forge L2) | ~first session | prove the loop fast |
| First refined good (smelt ingots) | early — session 1–2 | teach the refine step quickly |
| First node settlement claimed | ~session 2–3 | the territory loop opens |
| Stone-tier walls / mid towers | days | the climb begins |
| Deep-zone settlement + rare-stone crafting | weeks | the long endgame grind (+ spend pressure) |
| Max Forge / +1 gear chasing | open-ended | the evergreen sink |

**Curve shape:** costs scale **super-linearly** (each tier costs noticeably more than the last — CoC-style),
while faucet rates scale **sub-linearly** (more nodes help, but not 1:1) → the gap *widens* with progress =
fast early, slow late, **automatically.** Tunable knobs: cost-growth exponent, faucet-rate curve, offline
cap, refine ratios. CLI implements these as a **`ProgressionConstants`/SO**, never inline literals.

---

## 4. The conversion chain (raw → refined ratios — the throttle)

Each refinery converts at a **ratio gated by its tier**, and the *rate* (throughput/min) also scales with
tier. Example structure (numbers tunable):

| Refinery | Recipe (raw → refined) | Ratio @ T1 → Tn | Throughput |
|---|---|---|---|
| Forge | Iron → Iron Ingot | 5:1 → 3:1 (better tier = less waste) | X ingots/min, rises with tier |
| Lumbermill | Wood → Plank | 4:1 → 2:1 | … |
| Arcane | Aether → Core | 8:1 → 5:1 | … |
| Jeweler | Rare Stone → Cut Gem | **gamble** (fail/success/+1, skill-vs-rarity) | low, deliberate |

The refinery is the **economic throttle**: raw can flow fast, but how fast you turn it into *usable*
refined goods is gated by building tier → upgrading the refinery is itself a meaningful investment (you
upgrade the Forge to refine faster *and* to unlock what you can build — the Warcraft spine).

---

## 5. Sinks, sized to the curve (the demand side)

Every sink reads from a tunable cost table, scaled super-linearly by tier/level:
- **Build placement** — cheap (raw or low refined) — keep agency cheap so building is fun.
- **Wall tiers** (WO-151) — Planks + Stone, rising per tier.
- **Building/Forge upgrades** (WO-151) — refined + raw, the milestone gates.
- **Weapon/+1 crafting** — Ingots/Gems, the deep sink (and the +1 gamble).
- **Settlement claim** (WO-159) — upfront cost + the defense you must fund.
- **Town/Village level** (WO-151 meta-gate) — the big periodic resource dump that raises the ceiling.

**Anti-stuck valve:** the **store buys missing raw** for Coins/premium (WO-151 §5) so progress is never
hard-walled on one resource — and that's a **monetization touchpoint** (buy your way past a grind = the
spend lever, kept to *convenience not power* per the NS guardrail).

---

## 6. Food → Population → gathering speed (Food's purpose — owner 2026-05-30)

Food was an orphan resource (a node drops 20, nothing consumes it). Its job: **grow population.** The loop
(Warcraft "more peasants = faster mining," **positive-only — NO starvation/upkeep drain**):

```
spend Food → raise POPULATION (a settlement-size meter / workforce count)
        ↓
higher population → FASTER resource gathering (a gather-rate multiplier)
        ↓
faster gathering → more Food (+ everything) → grow population again
```

- **Food → Population:** spend Food to add population (a rising "how big is my settlement" number — a
  satisfying growth meter, not a chore). Population has a **cap** that rises with village level / housing.
- **Population → gather speed:** more population = a higher **gather-rate multiplier** on your nodes/
  settlements (the workforce works the faucets faster). Ties to **worker dispatch (WO-117)** — workers
  *are* population; population is the labor pool the auto-harvest draws from.
- **Positive-only (locked):** feeding **grows** you; there is **no starvation, no upkeep drain, no
  punishment** for not feeding (that was an over-complication — removed). You only ever gain. Simplest
  version: Food raises a population int → population gives a flat gather multiplier; no per-worker micro
  unless WO-117 worker-assignment is in.
- **Why it's good:** gives Food a clear job, makes population a visible growth reward, and turns "gather
  faster" into "grow your people" (not just "build more mines") — a second-order economy that fits the
  build-a-settlement fantasy. **Keep it simple now** — feed → grow → faster, one direction.

## 7. Pressure management (so the economy stays healthy)
- **Offline cap** — accrual stops at a cap → you must return + spend (no infinite AFK hoard).
- **Super-linear costs** — late upgrades soak huge sums → no resource ever feels "solved."
- **Refinery throttle** — even with infinite raw, refined output is tier-gated → upgrading refineries is
  always worth it.
- **Population cap** — gather-speed growth is gated by the population cap (raised via village level) → keeps
  the multiplier from running away; gives village-level another reason to climb.
- **Resource sinks at every loop step** → the haul always has somewhere to go (no dead-end currency).

---

## 8. What CLI builds (reconcile, don't reinvent)
- **Step 0:** merge the wallet to one source of truth (`GameState`); `EconomyService` operates on it.
- **Extend `ResourceCost`/the wallet** to carry the full set (Food, Coins, Rare Stones, refined goods) —
  ideally a **dictionary/`ResourceType`-keyed** ledger, not N hard fields, so adding a resource is data.
- **`ProgressionConstants` / SOs** hold ALL rates, ratios, cost curves, caps — **zero magic numbers in
  logic.** This is what makes "fast early, slow late" tunable in playtest without code changes.
- Faucets (nodes/waves/offline/encounters) write the wallet; refineries convert; sinks read the cost table.
- **Build the structure now; tune the absolutes in playtest** — the doc gives relationships + relative
  magnitudes + the curve shape, not final numbers.

## Open questions for owner (tuning calls, non-blocking)
- **Offline cap length** — how many hours of accrual before the cap? (Sets how often you must log in.)
- **Food → Population tuning** — how much Food per population point, how big a gather bonus per population,
  and the population cap curve (vs village level). All tunable; positive-only model is locked (§6). NO
  upkeep/starvation (cut). Open: does population also gate *other* things later (settlers, army size)? — park for now.
- **Premium-buy scope** — can you buy *refined* goods / *time-skips*, or only raw? (Monetization depth vs fairness.)
- **One crystal or two?** (Merge AetherCrystal/Crystals/ManaCrystals — confirm it's ONE, or if mana-crystal is a deliberate separate magic currency.)

🤖 Design doc (UI lane). Built on the verified wallet state (GameState/NestedTypes/EconomyService — flags
the 3-way fragmentation), the crafting spine (DEFENSE_DEPTH_ANALYSIS), WO-151/153/154/159, and the
offline/node faucets. No code/scene/bake. Numbers are relative/tunable, not final.
