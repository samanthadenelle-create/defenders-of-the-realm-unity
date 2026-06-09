# WORK_ORDER_389 — Arena Mode (live Attack / set-and-watch Defend)

**Status:** SPEC FINALIZED 2026-06-09 (consolidated with owner). Data/core + venue + defense-setup + defender-spawn + patterns BUILT this session; Attack flow + AI-attacker + matchmaking remain.
**Lane:** 2 Combat/AI + 6 Economy + 4 UI.

## Goal
A fun, unique, adrenaline-filled Arena mode where the player is **ALWAYS the live actor**: **Attack = you play, Defend = you set up and watch.**

## Core Rules
- **Player is always the live side; the opposite side is always AI-controlled** (no live-vs-live for now).
- Both sides use the **same 50-point budget + troop catalog** (symmetry / fairness).
- All stats data-driven from JSON where possible — **hardcode now with clear `// TODO data-driven` comments.**

## 1. Attack Mode (the adrenaline mode)
- Player chooses Attack → spend **50 pts to recruit a squad** (Ranger/Knight/Wizard/Healer/Ballista…).
- Player enters the enemy castle as **the Captain — the single selected hero** (NOT the party of 4).
- The recruited squad **follows the Captain and auto-fights** via existing `StoryCompanion` / AI-brain logic (friendly `CombatFaction`).
- **3-minute time limit.** Win = destroy the enemy **Heart / Town Hall** (the pinnacle objective) before time runs out.
- Optional **SKR wager** for higher stakes/payout (DEFERRED — post-audit, off for beta).

## 2. Defend Mode (war-base style — set & watch)
- Player chooses Defend → spend **50 pts to place** troops/structures around their **imported castle**.
- Save as the player's **War Base / Arena Defense** (persistent).
- When attacked, **AI assaults the castle and the player WATCHES** (live or replay — the CoC model).
- The castle's **existing built defenses (towers / catapults / ballistae) also activate** (reuse `DefenseTower`/`TowerCombat` — already fire at `CombatFaction.Hostile`).

## 3. Matchmaking & Tension
- **Tiered, PRIMARY = Arena Rating (W/L)**; SECONDARY modifier = power (defensive points, level, echo affinity, upgrades). *W/L-primary resists sandbagging; keep reward ∝ tier so progression never punishes the player.*
- **Cold-start = AI-sync:** with a thin pool, **synthesize** an opponent scaled to the player's value (closest balance *by construction*) from the 7 `DefensePatternLibrary` templates × a value→composition scaler. **Fade to real player War Bases** as the pool grows.
- Before committing to Attack, show **limited intel — threat rating + blurred base preview** (not fully blind; the tier also bounds the threat band).
- Optional **SKR wagering** layer (DEFERRED).

## 4. Troop Catalog (Phase 1) — hardcoded, `// TODO data-driven`
| Troop | Cost | Role |
|---|---|---|
| Ranger | 5 | Ranged DPS |
| Knight | 10 | Melee Tank |
| Wizard | 15 | Magic / Splash DPS |
| Healer | 12 | Support Healing |
| Healing Shrine | 18 | Passive Healing Aura |
| Ballista | 20 | Heavy Siege / Anti-Hero |

## Deliverables → BUILD STATUS (2026-06-09)
1. **ArenaDefenseCatalog + troop data** — ✅ BUILT & committed (`9e1e6ed`).
2. **Defense Setup screen** (FF-style placement reuse + point pool) — ✅ BUILT & committed (`9d584a9`).
3. **Attack flow** (recruit → lead squad as Captain) — ✅ BUILT & committed (`a0e5620`). `ArenaAttackRecruitController` + palette (recruit ≤50-pt squad) + `SpawnAttacker` (hero-leashed = follows the Captain, auto-fights) + `ArenaMode.SpawnAttackSquad` hook. MVP core; **still to wire: HUD Attack button, full menu/dice/intel UI, structures-in-squad (units only for now).**
4. **Defend flow** (place → save War Base) — 🟡 PARTIAL. Placement + save ✅; defender-hold spawn (`SpawnDefenders`, guard-post, friendly) ✅ BUILT & committed (`0b6c8dd`); **still to build: the AI attacker (re-aim EnemyBrain at the War Base) + the watch/replay view + activate existing castle defenses.**
5. **Time limit + win condition** — 🟡 REUSE. `ArenaMode.RaidTimeoutSeconds` (180s) + Heart/Town-Hall destruction exist; wire to the two modes.
6. **Basic tiered matchmaking** — 🟡 SEEDED. `DefensePatternLibrary` (7 templates = AI-sync seed) ✅ BUILT & committed (`0b6c8dd`); **still to build: Arena Rating (W/L) + the value→composition AI-sync scaler + the intel UI (threat rating + blurred preview).**
7. **Comments for future JSON stats + SKR wagering** — ✅ present in the built code.

## Reuse map (the verified seams — no new combat/AI/path tech)
- **Friend/foe = `CombatFaction`** (Friendly/Hostile); every tower/hero/companion filters `Faction==Hostile`. Squad/defenders = Friendly; AI attackers = Hostile. Zero new combat code.
- **Bodies = `StoryCompanion`** (4 classes + abilities). Attack = hero-leashed (follow the Captain); Defend = guard-post tethered (hold). Structures = `StructureFactory` (Ballista=`DefenseTower`; Shrine=HealAura — TODO behavior).
- **Time limit** = `ArenaMode.RaidTimeoutSeconds`. **Objective** = Heart/Tree at the castle **pinnacle** (capture-the-flag, defense-in-depth).
- **Paths** = the rebaked navmesh (NavMeshAgents — no authored paths).
- **Venue** = `ProceduralSiegeArenaBuilder` (plate → port castle JSON → rebake). Own castle drag-and-drop (brings its plane); opponent = `Realize` JSON onto the plate.
- **AI-sync opponent** = `DefensePatternLibrary` template × value scaler (synthesize, don't search).

## What NOT to do
- No new placement system / AI / combat / path logic — reuse. No live-vs-live netcode.
- Hardcode stats with `// TODO data-driven` comments. Defer SKR wagering + tiers-at-scale until the population/audit are ready.
- Don't punish progression — keep reward ∝ value-tier.
