# Design Review — Echoes of Elarion through the CoC lens and the WC3 lens

**Date:** 2026-08-15 · **Seat:** UI · **Status:** review + WO set (WO-1026 … WO-1029)
**Method:** every claim below is measured from the tree or the canon anchor
(`CANON_GROUND_TRUTH_2026-08-09.md`), not from impression. Where I could not verify, I say so.

---

## 0. The one-paragraph verdict

**The game has both engines built, and neither one closes its loop.** From Clash of Clans we took the
player-built town, the builder queue and the raid — but the *consequence* half is missing: nothing ever
attacks the base the player spends all that care arranging, so layout has no payoff and no feedback.
From Warcraft 3 we took the hero, the talent tree, gear and a creeping ground (the dungeons) — but two
of three heroes have no usable tree, and the creeping ground is parked. **The single highest-leverage
work in this project right now is not new systems. It is closing the two loops that are already 80%
built.**

---

## 1. What actually makes each game fun (the lens, stated honestly)

### Clash of Clans

| pillar | what it really does |
|---|---|
| **The ratchet** | Something is *always* upgrading. The ache of an idle builder is the retention engine — not the reward, the *gap*. |
| **Consequence for layout** | You design a base, then **watch replays of it failing**. Design → test → redesign is the actual game. |
| **Loot at stake** | Storages are raidable. Shields, revenge. Risk gives the numbers meaning. |
| **Clan donations** | The strongest social hook ever shipped in mobile: you get *gifts* from humans, and you give them. |
| **The 5-minute session** | Log in → collect → start an upgrade → 2–3 raids → out. Legible, complete, repeatable. |
| **A single master gate** | Town Hall level paces everything and makes "what do I do next" never ambiguous. |

### Warcraft 3

| pillar | what it really does |
|---|---|
| **The hero is the story** | Levels, items, 4 abilities. Attachment comes from a character that *grows*. |
| **Creeping** | PvE that funds PvP. Risk/reward you choose to take. The reason to leave your base. |
| **Branching tech** | Real choices that foreclose others. Identity through commitment. |
| **Counters** | Composition matters; there is no single right army. |
| **Micro expression** | Skill ceiling — positioning, focus fire, spell timing. |
| **Items & shops** | A second progression axis independent of levels. |

---

## 2. What we have — measured, not assumed

**Strong and genuinely built:**

- Player-built town, strategic placement **always on** (canon §8) — the CoC substrate is real
- **Obsidian queue** — Builder / Train / Research, depth cap 5 per line, 2 free slots, Echo-gated
  crystal-priced extra slot. This is a *better* ratchet than CoC's, mechanically
- **Raid V1 spine end-to-end** — CoC Teleport/Deploy, built in-tree (memory `raid-v1-spine-already-built`)
- **Hero + talents + gear** — 83 talent nodes, WC3-style building tech tree
  (memory `building-upgrades-warcraft3-style`), full weapon/armor catalogs via Addressables
- **Four content dungeons `PathComplete`** — floor-to-floor descent solved 2026-08-09
- **Echoes** — 6 helpers, harvest lanes + flat defense %, affinity match doubles yield
- **Wave loop** wired with smart composition
- **Leaderboard service**, daily quests, realm map (WO-826)

**Stubbed, dead, or parked (each verified this session):**

| thing | measured state |
|---|---|
| Clans | `ClanService.cs` is a **local PlayerPrefs stub** — its own header says so. No network, **no donations**, no wars |
| Ranger / Mage talents | **31 of 40 player-reachable nodes are dead** — Ranger 1 usable of 20, Mage 5 of 20 (**WO-910, open**) |
| Raid defense | **zero** hits for `RaidDefen*` / `DefenseReport` / `Revenge` / `Trophy` across `_Modules` |
| Dungeons | 4 layouts PathComplete, **parked warm** behind the demo |
| Boss difficulty | 3 of 5 multipliers computed with **zero gameplay consumers** — boss waves ignore the curve |
| Master gate | **no** `TownHall` / `CityLevel` / `SettlementLevel` symbol exists |

---

## 3. The gaps, ranked by leverage

### ⓵ The base is never attacked — so layout has no consequence *(CoC pillar, broken)* → **WO-1026**

Strategic placement is always on. The player arranges a town. **Nothing ever tests that arrangement in
a way they can see.** There is no raid-defense report, no replay, no revenge, no trophy pressure —
verified by grep, all zero.

This is the highest-leverage gap in the project because **it does not need new systems — it needs the
existing ones connected.** Waves already attack the town. The raid spine already simulates attacker vs
base. What is missing is the *mirror*: showing the player their own base under attack and letting them
respond. Without it, every hour spent on placement, walls, and tower positioning is invisible to the
player, and the whole CoC substrate is decorative.

### ⓶ Two of three heroes have no hero progression *(WC3 pillar, broken)* → **WO-910, already open**

⚠ **Do not mint a new ticket for this.** `WO-910` is READY FOR OWNER RULING and has been since
2026-08-06. In WC3 terms this is the pillar: the hero *is* the story. A Ranger player reaches the talent
screen — the screen we have spent this entire session polishing — and finds **one** usable node in
twenty. **Polishing that screen while 31 of its nodes are dead is optimising the frame around an empty
canvas.** This review's recommendation: WO-910 outranks WO-1021.

### ⓷ The creeping ground is built and parked *(WC3 pillar, latent)* → **WO-1028**

Four dungeons are `PathComplete` with a torch/oil/darkness risk-reward system ~90% built
(memory `dungeon-pillar-roadmap`). In WC3, creeping is *why you leave the base* — PvE that funds your
power. We have the ground and no loop wired to it: no reason to descend, no reward that feeds the town.
This is the cheapest large win in the project, because the expensive half already shipped.

### ⓸ The ratchet has no ache *(CoC pillar, half-built)* → **WO-1027**

The queue is mechanically better than CoC's. But CoC's engine is not the queue — it is **the discomfort
of an idle builder**. We have no idle-builder pressure, no "what do I do next" answer, and no legible
5-minute session shape. A player who logs in does not know when they are *done*.

### ⓹ No social layer *(CoC's retention engine, stubbed)* → **WO-1029**

Clans are a PlayerPrefs stub. The specific mechanic worth having first is **donations** — receiving a
gift from a human is the strongest retention hook in the genre, and it is far cheaper than clan wars.

### ⓺ No single master gate *(CoC pacing, absent)*

No Town Hall equivalent. Stockpiles cap capacity (memory `stockpiles-cap-capacity`) but nothing paces
the whole game or answers "what unlocks next". **Filed as a finding, not a ticket** — it is a
structural design decision the owner should rule on before anyone implements it.

### ⓻ Boss waves ignore the difficulty curve *(already in the anchor's open-gaps list)*

`EnemyCountMultiplier` / `BossHpMultiplier` / `BossDamageMultiplier` have zero consumers. Already
recorded in `CANON_GROUND_TRUTH_2026-08-09.md` §10. **Not re-ticketed here** — flagged so it is not
lost.

---

## 4. What NOT to do

- **Do not add a new pillar.** Six exist and two are unclosed. A seventh makes it worse.
- **Do not build clan wars before donations.** Wars need population; donations work at N=2.
- **Do not treat this review as licence to refactor.** Every item above is *connective* work on shipped
  systems — the HP B2B rule holds: never smuggle structural refactors into player-facing work
  (`docs/ARCHITECTURE_PRINCIPLES.md`).
- **Do not implement ⓺ (master gate) from this doc.** It is an owner ruling, not a spec.

---

## 5. The recommended order, and why

1. **WO-910** (hero trees) — a broken pillar on the screen we are actively polishing
2. **WO-1026** (raid defense loop) — gives every existing placement decision a consequence
3. **WO-1028** (dungeon creeping loop) — largest built-but-parked value in the tree
4. **WO-1027** (session shape / builder ache) — cheap, and it makes 1–3 legible
5. **WO-1029** (donations) — retention, once there is something worth returning to

⚠ **The talent-tree presentation work (WO-1021) is deliberately NOT in this list.** It is correct work
and it should ship — but it is polish on a surface whose *content* is 31/40 dead. Sequence WO-910 first
or the polish lands on an empty screen.
