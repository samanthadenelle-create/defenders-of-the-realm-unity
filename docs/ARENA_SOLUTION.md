# Full Arena Solution — synthesis (2026-06-10)

Synthesis of the 4-agent solutioning pass (WO-388 defender base, WO-389 defense system,
WO-386 battle visualization, WO-390/#40 raid loop). **Headline: the arena is ~75% already
built.** What's left is connective tissue, a 2D-art presenter layer, and the "defend & watch"
inversion — not new combat, AI, economy, or base systems.

---

## 0. The two owner decisions that anchor everything

1. **The defender base is the player's OWN self-designed base** (`GameState.BaseLayout`),
   never the fixed `MainCastle_Hall`. Building your base is a prerequisite. *Must be their
   own design.* (memory: [[arena-defender-is-self-designed-base]])
2. **The fixed castle is hostile to AI** — its multi-level geometry + complex mesh make a
   clean NavMesh nearly impossible, so AI can't path it. The self-designed base **solves this
   technically, not just thematically:** `BaseLayout` is a **flat grid** of catalog pieces,
   reconstructed onto a pre-baked flat **plate** → a clean, bounded runtime NavMesh bake that
   AI *can* path. The castle problem and the self-designed-base decision converge on the same
   answer: **flat-grid recipe bases, not the art castle.**

3. **Base-built UNLOCKS the arena** (owner, 2026-06-10). The arena is gated behind building
   your base — onboarding sequences the player into the **build loop first (prerequisite),
   then the arena opens as the payoff.** Unlock condition (a one-flag check on state already
   tracked): the base has a core/heart + ≥N structures + ≥1 placed defender (`BaseLayout` +
   `ArenaDefense` cross a small threshold) → *"Your stronghold stands — the Arena opens."*
   This gives base-building purpose (you build to unlock + to defend) and a natural ramp
   (a better base → better arena performance). Markers/herald stay hidden until unlocked.

This is the keystone. Everything below assumes flat-grid `BaseLayout`/`ArenaDefense` recipes
on the `SiegeArena` plate — never the multi-level castle mesh.

---

## 1. The unifying architecture (why it's only ~25% left)

Three primitives already do the heavy lifting; the arena is mostly *wiring them together*:

- **`EnemyOutpost` = the universal battle resolver.** Builds a fort, spawns a threat-scaled
  garrison via `EnemyFactory` (zero new combat), counts kills, fires `OnCleared`, runs the
  full threat-scaled **loot table** (resources/crystals/XP/gear/quests — all already routed).
  Both the open-world raid and the Arena funnel through it.
- **`GarrisonRecipe` = the universal difficulty descriptor.** `levelRange` already = the
  difficulty band; `threat` already scales stats. One JSON line = a tuned encounter.
- **`PlacedStructureData` / `BaseLayout` = the self-designed base.** Grid-cell + discrete-yaw,
  JSON-serializable, headless-realizable via `OutpostFoundationGenerator.Realize` —
  deliberately built so *"an async-raid server can re-verify it."* The async-PvP wire format
  already exists.

---

## 2. The four pillars — state & the real gaps

### A. Defender base (WO-388) — ~80% built
- DONE: base→recipe snapshot (`BaseLayout`), reconstruct (`Realize`), the full async-PvP
  flow controller (`ArenaMode`), the `UsePlayerCastle` defender path, runtime NavMesh bake.
- GAPS: (a) wire the dedicated **`SiegeArena.unity` venue plate** into `ArenaMode` (built by
  `ProceduralSiegeArenaBuilder`, not yet wired) — the plate is what makes the AI-pathing bake
  reliable; (b) honor per-piece `level` in `Realize` (a L3 wall should defend as L3); (c)
  async-PvP opponent JSON source (backend-gated).

### B. Defense system (WO-389) — ~80% built
- DONE: the 6-defender catalog + 50-pt budget, placement (`ArenaDefenseSetupController`),
  persistence (`ArenaDefense`), raid-time **friendly defender spawn** (units guard-post
  tethered + structures), built towers auto-fire at Hostile. **"Player raids a defended base"
  (PvE attack) already works.**
- GAP: the **inverse — "defend & watch"**: a new `ArenaDefendController` (mirror of
  `ArenaMode`) that spawns an **AI attacker wave** (the one genuinely new unit path: a
  def→Hostile-`EnemyDef` mapper) against the player's base while the player watches, plus
  base-destruction-% win/lose scoring. Everything else is a mirror of the existing flow.

### C. Battle visualization + 2D art (WO-386 / #34/#35) — engine done, presenter new
- DONE: the deterministic ATB engine + `ATBRuntimeState` events + an **append-only log**
  (`SourceId/TargetId/Event/Amount`) + a working **log-cursor replay** (currently driving 2
  3D capsules).
- PLAN: swap the presenter art from 3D → **2D FF-style sprites** (the owner's direction):
  a slicer (clone `ItemIconSlicer`) → `Resources/Sprites/ATB/` → a sprite library + presenter
  + deterministic stage (party left / enemies right, flipX) + a director that reuses the
  *exact* `_lastProcessedLogIndex` cursor. **Less code than the 3D path.**
- ART CONSTRAINT: heroes ship **per-state** poses (idle/attack/cast/hurt/victory/death);
  enemies ship **idle-only** family poses → animate enemies with **procedural tweens**
  (lunge/shake/fade) until per-state enemy art exists. **Art gaps to flag: no colour Sylas
  sheet (greyscale only); no per-state enemy poses.**

### D. Raid loop + economy (WO-390 / #40) — ~70% built, 3 disconnected slices
- DONE: the resolver (`EnemyOutpost`), loot, the dual-rail economy (soft: wood/iron/crystals/
  XP/gear, already wired; SKR: `ArenaWalletService` stub → `WalletService` real, swap-behind-
  interface), the W/L ledger, the recipe catalog (runtime, WebGL-safe).
- GAPS (thin connective tissue — 4 small files): `RaidTargetSystem` (a **field of overworld
  markers placed by `levelRange` = distance = difficulty**), `RaidBriefPanel`/router (garrison
  vs cave/ATB by `recipe.Kind`), `RaidLoadoutController` (pre-raid troops + potions),
  `RaidCooldownService` (the missing **repeatable-PvE beat**: cleared→cooldown→respawn at
  threat+1). WO-390 potions layer in **last**.

---

## 3. Sequenced plan (phased, scope-disciplined — TD-RPG, not an MMO)

**Phase 1 — Make the self-designed-base raid playable (PvE attack).**
1. Wire `SiegeArena` plate into `ArenaMode` (WO-388a) — reliable AI-pathing bake.
2. `RaidTargetSystem` + `RaidBriefPanel`/router (raid-loop A/B) — overworld markers by
   difficulty → route into `EnemyOutpost`.
3. Honor per-piece `level` in `Realize` (WO-388b).
*Outcome: build your base → walk to a difficulty-tiered marker → raid a defended base.*

**Phase 2 — Defend & watch + the repeatable loop.**
4. `ArenaDefendController` + AI-attacker mapper (WO-389a/b) — watch your base get raided.
5. `RaidLoadoutController` (troops first) + `RaidCooldownService` (re-raid scaling).
*Outcome: the full CoC loop — build, defend, raid, cooldown, scale.*

**Phase 3 — FF-style ATB battles.**
6. ATB 2D sprite layer (WO-386 A–E) — slicer → library → presenter → stage → director.
*Outcome: cave/dungeon markers resolve as animated 2D turn-based battles.*

**Phase 4 — Polish + economy depth.**
7. WO-390 potion belt, base-destruction star scoring, status tints, the result/intel UI.

**Phase 5 — Async PvP (backend-gated, later).**
8. Swap seeded opponents for fetched real-base `BaseLayout` JSON (same `Realize`); incoming
   raids damage your `BaseLayout`. **No live netcode** — async snapshot model (CoC-style).
   *Live synchronized PvP is a separate, heavy netcode project deliberately out of scope.*

---

## 4. Cross-cutting risks (all have existing mitigations)
- **Runtime NavMesh under the base** (the recurring "garrison failed → false win"): the
  `SiegeArena` plate + `ArenaNavMeshBaker` fuse-bake + `OnArenaSpawnFailed` guard solve it.
  *This is exactly the castle-mesh problem — the flat base + plate is the fix.*
- **Read/Write-enabled meshes in builds** (cf. the sword-grip crash): the plate is a
  primitive; carved walls use PhysicsColliders, not mesh — safe; verify catalog prefabs.
- **Scene serialization bottleneck (§9):** everything is code-built + additive + recipe-driven;
  never hand-edit `SiegeArena.unity` (owned by `ProceduralSiegeArenaBuilder`).
- **Persistence is PlayerPrefs-mirrored**, not yet in `SaveSchema` for cooldowns/W/L — a
  save-owner follow-up before backend sync.

**Bottom line:** the arena is mostly *already there*. The self-designed flat-grid base isn't a
compromise — it's the design that makes AI pathing, async PvP, and replayability all work at
once, and it sidesteps the castle's multi-level-mesh problem entirely.
