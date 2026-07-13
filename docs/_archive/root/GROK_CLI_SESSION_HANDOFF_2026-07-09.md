# Grok CLI Session Handoff → Claude (2026-07-09)

**Branch:** `wip/village2-and-f8-tickets`  
**Last Grok commit (harvest spine):** `b08293c9` — *feat: CoC harvest collector spine with pending buffer and siege loot targeting*  
**Audit / WO spec file:** `GROK_WORK_ORDERS_FULL_AUDIT.md` (WO-615 → WO-664)  
**Owner:** Samantha — felt-verify before push; Claude orchestrates + specs; **CLI implements all `.cs`**

---

## 1. What Grok did this session (summary)

### A. Documented (spec-only) — `GROK_WORK_ORDERS_FULL_AUDIT.md`

Full audit minted **50 work orders** in one file:

| Block | WOs | Theme |
|-------|-----|--------|
| P0 spell/VFX | 615–621 | Chain unification, knight projectiles, enemy cast, weapon elements, tower bolts, debug VFX removal, spells pack mirror |
| P0 animation | 622–625 | Controller gate, ActorAnimator-only, pack gestures |
| P0 feel (owner) | 651–655 | AoE ground rings, meteor landing sync, tower arrow damage-on-arrival, hub wanderers, NPC work anims |
| P1 harvest/CoC | 656–664 | Registry, finite reserves, echo assign, embodied workers, raids, attackable collectors, companion revive |
| P1–P3 misc | 626–650 | Spawn kit, HUD seam, perf, canon, consolidation |

Owner design conversations captured in audit:

- Splash/AoE rings, tower arrows, town NPC life
- Harvest structure: **CoC collectors**, pipe home, **typed town targets**, attackable collectors

### B. Implemented + committed — CoC harvest spine (`b08293c9`)

**Spine landed (partial WO-656 / WO-663 / WO-664):**

| Piece | Path | Status |
|-------|------|--------|
| Harvest source contract | `Assets/_Modules/Core/World/IHarvestSource.cs` | Done |
| Siege loot target contract | `Assets/_Modules/Core/Combat/ISiegeLootTarget.cs` | Done |
| Source registry | `Assets/_Modules/Village/Harvest/HarvestSourceRegistry.cs` | Done |
| Collector component | `Assets/_Modules/Village/Buildings/Progression/ResourceCollector.cs` | Done |
| Collector registry + Collect All | `ResourceCollectorRegistry.cs`, `ResourceCollectorService.cs` | Done |
| Hub auto-wire | `ResourceCollectorBootstrap.cs` | Done (storefront names + DDOL fallback) |
| Tick → pending | `ResourceBuildingHarvester.cs` | Done |
| HUD Collect All | `EchoWorkforceHud.cs` | Done (was Dump All) |
| Siege AI priority | `EnemyBrain.cs`, `Enemy.cs` | Done |

**Behaviour today:**

1. Farm / Lumbermill / Forge (level **> 1**) accrue into **Pending** (not wallet).
2. **Collect All** (harvest panel) sweeps collector pending + echo silo → `GrantSpendable`.
3. Hub storefronts (`Windmill_Food_Storefront`, `Lumbermill_Wood_Storefront`, `Forge_Armor_Storefront`) get `ResourceCollector` + hitbox.
4. Raiders prefer collectors when pending bubble is full (`ISiegeLootTarget`).
5. Collector destroyed → 50% pending stolen, `Broken` until `Repair()` (no auto-repair UI yet).

**Verified:** `COMPILE_GATE_OK` (compile-gate-harvest.log).

**Not done (fast-follow — see §4):** pending bubble VFX, GameState save for pending (uses PlayerPrefs), offline accrual into pending, outpost collectors on `MineNode`, repair UX, wave-end repair hook, crystal collector, `OfflineHarvestService` → registry unification.

### C. In working tree — NOT in `b08293c9` (reconcile before next push)

Uncommitted changes on same branch (likely earlier session / parallel lane):

```
 M Assets/Editor/BuildOrcHumanoidController.cs
 M Assets/Editor/PeopleCharacterImporter.cs
 M Assets/Resources/Enemies/OrcHumanoid.controller
 M Assets/Resources/Enemies/SkeletonHumanoid.controller
 M Assets/_Modules/Village/Arena/BattleArena.cs
 M Assets/_Modules/Village/Enemies/OverworldEncounterSpawner.cs
 M Assets/_Modules/Village/Families/FamilyLeader.cs
 M Assets/_Modules/Village/Waves/EnemyGroupSpawner.cs
```

Themes: **overworld family packs** (variable 1–7), **tactics**, **CombatLocomotion / InCombat** on orc/skeleton controllers, arena/group spawner alignment.  
**Action for CLI:** review diffs, brace-check, gate, commit by **explicit path** (do not `git add -A`).

---

## 2. What Claude should read (in order)

**Binding gates (read before any code routing):**

1. `PREFLIGHT_GATE.md` — answer YES to every item before CLI touches code
2. `SESSION_CANON_LOADER.md` — session SME primer
3. `docs/MASTER_CATALOG.md` + `docs/MASTER_CATALOG/village-npcs.md` (if town WOs)
4. `docs/TICKET_PIPELINE.md` — QA triage → CLI implement → PO felt-close
5. `Claude.md` — **UI never writes `.cs`**; CLI sole committer; instrument first (§12)

**This session:**

6. **This file** — `GROK_CLI_SESSION_HANDOFF_2026-07-09.md`
7. **`GROK_WORK_ORDERS_FULL_AUDIT.md`** — full WO specs + execution order diagram
8. **`ECHO_WORKFORCE_SPEC.md`** — echo/silo canon (now folded into Collect All)
9. **`DESIGN_CORE_LOOP_AND_STRUCTURE.md`** — seat tier, pipe home, CoC walls sink

**Harvest spine (implement / extend):**

10. `ResourceCollector.cs`, `ResourceCollectorBootstrap.cs`, `ResourceCollectorService.cs`
11. `ResourceBuildingProgression.cs` + `ResourceBuildingHarvester.cs`
12. `CastleHubBuilder.cs` — storefront names for collector wire

**Spell/VFX (if prioritizing P0 feel):**

13. `docs/MAGIC_VFX_LIBRARY.md`, `Assets/_Modules/Village/Vfx/SpellVfxFactory.cs`
14. `Assets/_Modules/Village/Hero/HeroAbilities.cs`, `AbilityVfxKit.cs`
15. Reference: `ArcaneTower.cs` + `ProjectileVFXCatalog.cs`

**Role split reminder:**

| Role | Does |
|------|------|
| **Claude (UI)** | Triage, groom WOs, copy specs to `WorkOrders/`, Notion mint, felt-verify, close tickets |
| **CLI** | All `.cs`, compile gate, headless verify, commit by path |
| **Owner** | Priority call, playtest, push approval |

---

## 3. On deck — recommended implementation order

### Tier 0 — Owner felt-verify (before push `b08293c9`)

- [ ] Upgrade lumbermill to level 2+ → pending fills → **Collect All** → wallet moves
- [ ] Wave with full collector → raiders path to lumbermill
- [ ] Confirm no double-grant (pending + wallet same tick)

### Tier 1 — Harvest fast-follow (new WOs — §4)

Complete the CoC loop before new features.

### Tier 2 — P0 owner-visible combat feel (audit WOs)

| Priority | WO | Why |
|----------|-----|-----|
| 1 | **615** + **620** + **621** | Spell chain foundation + remove debug bursts + pack mirror |
| 2 | **653** | Tower arrows damage on arrival (hitscan today) |
| 3 | **651** + **652** | AoE rings + meteor lands before damage |
| 4 | **616**–**618** | Knight projectiles, enemy cast, weapon elements |
| 5 | **654** + **655** | Town life |

### Tier 3 — Reconcile uncommitted family/animation lane

Commit or merge explicit paths from §1C; run `ORC_CTRL_OK` / `SKELETON_CTRL_OK` if controllers changed.

### Tier 4 — P1 consistency (when Tier 2 stable)

656 offline integrator (registry exists; **OfflineHarvestService not rewired**), 627–631 spawn/HUD, 662 companion revive.

---

## 4. Additional work orders to mint (gaps not fully spec’d)

Copy to `WorkOrders/` + Notion when claiming. Suggested numbers **after 664** (confirm `CLI_LANES_WO_NUMBERS.md`).

### WO-665 — Harvest Spine Fast-Follow (CoC polish)

**Priority:** P0  
**Depends on:** spine commit `b08293c9`

- Pending **bubble VFX** on storefronts (fill ratio → particle/stack)
- **Repair** interact or auto-repair on wave victory for `Broken` collectors
- Move pending/HP from PlayerPrefs → **GameState** (schema bump + migrator)
- HUD toast: "Lumbermill is full — Collect or defend!"
- Acceptance: owner CoC read without reading logs

### WO-666 — Outpost Collector Placement (MineNode → building)

**Priority:** P1  
**Depends on:** 665

- Claim `MineNode` → spawn placeable collector (build mode WO-108 seam)
- Outpost pending accrues on-site; **Collect All at Heart** still sweeps all
- Finite reserve (WO-657) data file `harvest-nodes.json`

### WO-667 — Offline Accrual → Pending (single clock)

**Priority:** P1  
**Depends on:** 656 registry (partial done)

- `OfflineHarvestService` accrues into collector **Pending**, not wallet
- No double-grant with echo silo
- Headless: `OfflineHarvestRegression` updated

### WO-668 — Branch Reconcile: Family + CombatLocomotion

**Priority:** P1  
**Lane:** Combat/Animation  
**Files:** §1C uncommitted list

- Review + commit family/tactics + controller bake as explicit-path commit
- Headless: overworld pack spawn regression
- Owner felt-verify: packs animate in combat stance

### WO-669 — Notion + Lane Mint (WO-615–669 block)

**Priority:** P2 (meta)  
- Register all audit WOs in Notion board + `CLI_LANES_WO_NUMBERS.md`
- Mark **663 partial DONE** (spine); **664 partial DONE** (AI + wire; no bubble VFX)

### Optional — if monetization returns

- Convenience token tray (audit `docs/audit/missing-components.md` MON-TK-1) — separate lane, do not mix with harvest.

---

## 5. What NOT to duplicate

| Topic | Status |
|-------|--------|
| CoC pending + Collect All | **Spine done** — extend via 665–667, don't greenfield second silo |
| `EchoService` silo | Still accrues; Collect All also calls `DumpSilos` — OK for V1 |
| `ArcaneTower` fireball chain | Done — copy pattern for 615/619 |
| Overworld family packs | Code in tree; verify committed vs §1C |
| Hand-edit `Village.unity` | **Banned** — use builders |

---

## 6. CLI copy-paste: claim harvest fast-follow

```text
Claim WO-665. Read GROK_CLI_SESSION_HANDOFF_2026-07-09.md §1B + ResourceCollector.cs.
Add: pending bubble VFX on hub storefronts, Repair() UX on wave clear, GameState persist for pending.
Instrument: [Flow:Harvest] accrue-pending, collect-all, collector-repair.
Gate: COMPILE_GATE_OK. Do not touch unrelated §1C files.
```

```text
Claim WO-615 + WO-620 in parallel lane (disjoint files from harvest).
Read GROK_WORK_ORDERS_FULL_AUDIT.md § WO-615. Instrument before fix. Gate once.
```

---

## 7. Files index (Grok touched this session)

**Committed (`b08293c9`):**

- `Assets/_Modules/Core/World/IHarvestSource.cs`
- `Assets/_Modules/Core/Combat/ISiegeLootTarget.cs`
- `Assets/_Modules/Village/Harvest/HarvestSourceRegistry.cs`
- `Assets/_Modules/Village/Buildings/Progression/ResourceCollector*.cs` (4 files)
- `Assets/_Modules/Village/Buildings/Progression/ResourceBuildingHarvester.cs`
- `Assets/_Modules/Village/Harvest/EchoWorkforceHud.cs`
- `Assets/_Modules/Village/Enemies/Enemy.cs`, `EnemyBrain.cs`
- `GROK_WORK_ORDERS_FULL_AUDIT.md`

**Spec-only (not implemented):** WO-615–662 except partial 656/663/664 as noted above.

---

*Handoff complete. Claude: groom §4 into Notion, route Tier 1–3 to CLI with specs from `GROK_WORK_ORDERS_FULL_AUDIT.md`, owner felt-closes Tier 0 before push.*