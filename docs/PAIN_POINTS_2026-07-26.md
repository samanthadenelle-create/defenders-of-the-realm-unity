# Pain Points + PM Rulings — 2026-07-26

**Status:** BINDING for pipeline prioritization (PM / Grok).  
**Audience:** CLI, Claude, owner. Decisions below unblock Tier-0 gates; engineering items stay known-fix unless noted.

**Related:** `CANON_GROUND_TRUTH_2026-07-26.md`, `docs/enemy-codex.md`, `docs/qa/WORK_ORDER_771_raid_system.md`, `docs/qa/WORK_ORDER_773_obsidian_queue.md`, `docs/SME/KAYKIT_SME.md`, `.gitignore` (Models/KayKit policy).

---

## 0. How to use this doc

| Tier | Meaning |
|---|---|
| **T0 — PM gate** | Product/content decision. CLI must not invent; implement only after this ruling. |
| **T1 — Engineering known-fix** | Spec exists or is obvious; schedule after T0 unblocks. |
| **T2 — Later / V2** | Do not start for the shippable V1 spine. |

---

## 1. Highest leverage — Tier-0 chokepoints

### 1.1 Enemy codex ratification (unblocks WO-772 → 770.11 + 771.13)

**Problem:** `docs/enemy-codex.md` is marked *review-and-approve before implementation*. WO-772 is blocked until a roster is ratified.

#### RULING — APPROVE phased (2026-07-26)

| Slice | Decision | Notes |
|---|---|---|
| **Canon-locked** (Alduin, Apprentice, Hollow Ones name, Necromancer of the Wound, Withering/Wound) | **APPROVED as-written** | Never rename |
| **Hollow Ones combat roster** (Walker, Warrior, Skirmisher/Rogue, Acolyte, Mage/Caster, Reaper, Brute/Golem, Cellar Hollow) | **APPROVED for implement** | Prefer live AccuRig family + KayKit Minion/Golem/Necromancer as codex states |
| **Agent-authored Hollow stats / specials** (Reaper kit, Brute numbers, etc.) | **APPROVED provisional** | Balance in playtest; ids/names stay |
| **Dungeon mini-boss titles** (Mournful Alpha, First Wolfwarden, Vault Keeper, Inn-Keeper, Watcher) | **APPROVED names** | Layout-spec already used them; kits from codex |
| **Wildlands faction concept** (living second faction) | **APPROVED as design** | Realm-2+ / variety — schema may reserve family |
| **Wildlands full implement** (Orc Raider, Caveman, Feral Wolf, Tiefling Cultist) | **DEFER** | Do **not** block WO-772 Phase 1 on Mystery Monthly packs |
| **Feral Wolf quadruped animation path** | **DEFER** | GAP-PRIMARY in codex; no ship dependency until pack + clips staged |
| **Alduin boss fight** | **FORBIDDEN** | Dialogue only (`At the Edge`) |

**WO-772 Phase 1 scope (unblocked now):**  
`EnemyResolver` + Hollow Ones family + equippable armor/weapons **for approved Hollow types only**. Per-type-armed art for Hollow Ones. Wildlands enum/family stub OK; no art requirement.

**WO-772 Phase 2 (owner stages packs later):** Wildlands bodies + wolf rig.

**Codex banner to add (when someone edits the file):**  
`⚠ RATIFied 2026-07-26 — Hollow Ones + mini-boss names APPROVED; Wildlands DEFERRED (see PAIN_POINTS_2026-07-26 §1.1).`

---

### 1.2 KayKit / character-pack travel policy (unblocks “Bryn is a pill”, silhouettes, CI)

**Problem:** KayKit + large packs are gitignored → zero art on other machines/CI → generic silhouettes, untextured people, unarmed identical enemies. No written travel policy.

#### RULING — Tracked runtime + zip travel (2026-07-26)

**Principles**

1. **Git never holds multi‑GB source packs** (owner policy stands).  
2. **Runtime-playable silhouettes MUST exist in tracked paths** under `Assets/Resources/` (or Addressables groups that ship) so a fresh clone still *runs* and reads *distinct* enemies/NPCs.  
3. **Full packs travel by zip / local copy**, not by `git pull`.  
4. **One shared humanoid animator path** for humanoids (AccuRig `SkeletonHumanoid` / KayKit `Rig_Medium` as documented in codex). Modular weapons attach to that path — **one perfect armed type first**, then spam variants.

**Required packs (local install checklist)** — machine must have, or accept placeholders:

| Pack | Used for | Install location (typical) |
|---|---|---|
| KayKit Skeletons 1.1 | Minion, Golem, Necromancer | `Assets/Models/…` (gitignored source) |
| KayKit Adventurers (+ Skeletons for raid) | Troop bodies (771.13) | same |
| People / AccuRig skeleton family | Live Hollow Ones + Bryn-class NPCs | `Resources/Enemies` + People (textures!) |
| KayKit Dungeon Remastered | Dungeon geometry (770.8) | Downloads → import |

**Tracked vs ignored**

| Must be **committed** (or LFS) | Stays **gitignored** |
|---|---|
| `Resources/Enemies/*` used FBX + materials that actually render | Raw KayKit pack trees under `Models/` |
| `Resources` NPC/hero textures needed for default cast | Full People texture dumps if multi‑GB |
| Prefabs that reference tracked meshes | Mystery Monthly bulk until Wildlands ships |
| A **manifest** of required assets + fallback slugs | — |

**Travel tooling (CLI to implement if missing)**

1. `tools/art/REQUIRED_PACKS.md` — human checklist.  
2. `tools/art/verify-runtime-art.ps1` — fails or warns if critical `Resources` keys missing (Bryn texture, Skeleton_Warrior, etc.).  
3. Optional: `export-runtime-art.ps1` / `import-runtime-art.ps1` for a **thin zip** of only used runtime assets (not full packs).  
4. Editor menu already patterns: `Defenders → Animation → Import Skeleton Family` — document as **required onboarding step** on a new machine.

**“Perfect one first” weapons**

- Phase 1: **one** Hollow type (Warrior) with correct grip + one weapon prop, shared controller, flag-gated.  
- Phase 2: extend to Mage staff / Rogue / Healer.  
- **Do not** enable random multi-weapon spam until Warrior is felt-good.

**Bryn / people textures**

- If mesh loads but texture is gitignored-empty → treat as **P0 for NPC** on any machine that ships dialogue.  
- Fix path: commit **used** textures into `Resources` (or LFS), not the entire People dump.

---

## 2. Raids — product hollow + doc-fragmented

### 2.1 Three fantasies → ONE product sentence

| Fantasy | Status |
|---|---|
| **Teleport → deploy army → watch auto combat → stars/loot** | **CANON raid loop** (owner 2026-07-26) |
| Hero-infiltrate fortress puzzles (RAID_PILLAR_VISION) | **Content layouts for bases**, not the control model |
| Walk-to overworld outposts (ARENA_SOLUTION) | **RETIRED as raid loop** (`ff.raidwalk` OFF) |

**RULING:** Implementers only build the CoC deploy loop for “raid.” Vision maps = `RaidBaseGenerator` layout presets.

### 2.2 Stakes loop (CoC hook)

**Problem:** V1 win = claim + companion; no army sting, shields, revenge, trophies → not sticky.

#### RULING — V1 minimum stakes + V1.5/V2 ladder

| Ship with V1 (must feel CoC) | Defer |
|---|---|
| **Army housing + train timer** (Barracks real structure when flag on) | Full async PvP |
| **Deploy consumes trained troops** from roster | Server anti-cheat |
| **Casualties:** % of deployed troops lost on defeat; partial loss even on 1★ (simple formula — tune later) | Revenge queue polish |
| **Stars 1/2/3** + soft loot table | Trophy matchmaking bands |
| **Shield after being raided** (even stub: “safe for N hours” in save) | SKR wager |

**Do NOT** build 771.3 fixed-point sim first. V1 = real-time combat + recorded deploy log re-watch. Deterministic sim = **V2 only**.

### 2.3 Doc consolidation (known-fix, high value)

- Banner older docs; single `RAID_NORTHSTAR` spine = WO-771 **V1 list** only.  
- `RAID_TROOP_UI`: **code-built uGUI**, never UXML in player.  
- Fix Avalon → **Elarion** drift in raid design copy.

---

## 3. Foundation — save / queue / economy

### 3.1 Save schema drift

**Problem:** Specs still cite v10; tree has advanced (audit: v34-class churn). Dual timers (`BuildJobs` / `PendingBuilds` / `BuildingCooldowns`).

**RULING:** Any WO that touches save **must** re-read `SaveSchema.CurrentVersion` + migrator at implement time. Prefer **one** migration per feature arc (771.1b pattern). No “drive-by” schema bumps in parallel agents.

### 3.2 Job queue (WO-773)

**Problem:** Single FIFO made train compete with build (un-CoC). Name “Obsidian” overloaded.

#### RULING — Multi-channel queue + player-facing rename

| Channel | Jobs | CoC analog |
|---|---|---|
| **Builder** | Build / repair / upgrade / tower / wall | Builder huts |
| **Train** | TrainTroop | Barracks train queue |
| **Research** | Troop upgrade tracks / learn-magic / tier unlocks | Lab |

- One `ObsidianQueueService` (or rename service to `JobQueueService` when convenient).  
- **Player copy:** “Builders” / “Training” / “Research” — **never** “Obsidian queue” in UI.  
- “Obsidian” remains: Blink UI pack + wall tier only.  
- Reuse `BuildTimerConfig` knobs (slots, ad-skip, instant finish).  
- Acceptance: **can train while a wall upgrades.**

### 3.3 Barracks is hollow

**RULING:** When `ff.barracks` is ON for real play, Barracks must be a **catalog structure**: placeable/upgradable, HP/damageable optional V1.5, upgrade tree unlocks troops. Feature flag alone is not a progression pillar.

---

## 4. Dungeon (engineering + product notes)

| Pain | Ruling / next |
|---|---|
| Folk’s Granary dead stub | **Hide or gate** until layout+controller exist — never leave a walkable broken door |
| Dual door systems (Portal + Entrance) | **One player-facing pattern**; collapse or clearly role-split (overworld portal vs hub entrance) |
| FPV default-on | ~~Keep only if felt-tested; else default third-person until motion-sick risk closed~~ → **CLOSED 2026-07-30: owner RE-AFFIRMED FPV stays default-ON.** `ff.dungeonfpv` remains `defaultOn:true`; over-the-shoulder is the A/B at `ff.dungeonfpv=0`. The two stale `DungeonCameraRig` headers that called FPV a "STUB, no independent look" and named over-the-shoulder the default were corrected in the same change. No longer an open gate. |
| Placeholder hero vitals | Wire selected-hero stats before marketing FPV combat |

---

## 5. Process / verification

| Pain | Ruling |
|---|---|
| Canon drift | Update load-bearing set in same PR as state change (Claude.md §15) |
| Stale WO citations | CLI re-verifies line numbers before implement; WOs are not source of truth for HEAD |
| Source-lint ≠ runtime | Scene-dependent behavior needs PlayMode / fleet or F8 proof before “done” |
| Unity lock | Parallelize **read-only** agents; one gate/build owner |

---

## 6. Combat / systems

| Pain | Ruling |
|---|---|
| ATB vs real-time | **Real-time BattleArena + wave/raid RT is live combat.** ATB = legacy / optional mode; no new raid path on ATB unless owner re-opens |
| Structure layer / LoS | Structure layer is **load-bearing**; builders must set it; regression on tower LoS stays green |

---

## 7. Monetization

| Lever | Ruling |
|---|---|
| Extra **Builder** slots | Premium / IAP primary (CoC builder hut) |
| Extra **Train** slots / parallel barracks | Secondary IAP or Barracks tier |
| Instant finish / ad-skip | Already in `BuildTimerConfig` — route through queue channels |
| PackStore PanelSettings | Re-enable only with own PanelSettings (known gap) |

---

## 8. Pipeline order after these rulings

```
T0 done (this doc)
  → WO-772 Phase 1 (Hollow Ones only) + art verify script
  → WO-773 multi-channel queue (or generalize BuildTimer*)
  → WO-771 V1 spine: 771.0 → 771.1 → 771.1b → 771.4 + RT combat
       → 771.9 + Train channel → 771.6 stakes (casualties + stars + loot)
       → 771.11 HUD → 771.13 Hollow/KayKit troops
  → DO NOT start 771.3 / 771.7 PvP until V1 feels sticky
  → Wildlands / wolf / full Mystery Monthly = Phase 2 content
```

---

## 9. Full pain inventory (compressed)

1. **Art pipeline** — gitignored packs; untextured people; no shared armed-rig discipline.  
2. **Raids** — three fantasies; no stakes; sim-first risk.  
3. **Save/queue** — dual timers; single-FIFO un-CoC; Obsidian name overload; hollow Barracks.  
4. **Dungeon** — dead Granary; dual doors; ~~FPV unproven~~ (owner-closed 2026-07-30, FPV default-ON stands); fake vitals *(also since corrected — `SeedHeroVitalsFromLiveHero` reads the real hero; the 120/60 literals are a Warn-guarded last-resort fallback)*.  
5. **Process** — canon drift; stale WO trees; lint ≠ runtime; Unity lock.  
6. **Combat** — ATB/RT duality; Structure layer fragility.  
7. **Monetization** — builder vs train IAP unmapped; store wiring off.

---

## 10. Change log

| Date | Change |
|---|---|
| 2026-07-26 | Created. Enemy codex phased ratification. KayKit travel policy. Raid V1 stakes. Multi-channel queue + naming. Pipeline order. |
