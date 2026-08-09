# Raids full-pipeline regression audit — 2026-08-09

**Scope:** Raid UI → army/selection → pre-deploy → scene load → in-raid drop/rally/retreat → combat (incl. WO-933 catapult) → score/victory/return → recovery.  
**Path A only** (`FeatureFlags.Raid` ON, `ff.raidwalk` OFF).  
**Method:** code + dual-copy data + EditorBuildSettings + existing Data/EditMode suites + prior post-932 audit. **Not** device PO feel.

---

## 1. Executive scorecard

| Layer | Status | One-line |
|-------|--------|----------|
| HUD Raids face (hide/dim/teach) | **GREEN** | Capability + ArmyStatus wired |
| Entry bridge (Path A teleport) | **GREEN** | RaidEntryGate → Bridge → Selection |
| Full-army gate | **GREEN / harsh** | N/Cap toast → barracks; policy choice |
| Selection grid (3 flagships) | **GREEN** | Regular / Hard / Extreme in build |
| Pre-deploy (BEGIN ASSAULT) | **GREEN** | CanDeploy = scene + build settings |
| In-raid tray drop / rally / retreat | **GREEN (code)** | Self-install RaidDeployController |
| Base layout (3 scenes + spire/garrison) | **GREEN / barren** | Nav + spire; props empty |
| Combat (troops hunt structures) | **GREEN (code)** | WO-853 + siege prefer |
| Stars / loot / victory / return | **GREEN (math)** | 50/30/20; spire win; retreat loot path |
| Recovery after raid | **GREEN** | ArmyRecoveryRegression |
| **UI / data honesty** | **YELLOW** | Clock & reward cards lie |
| **Content depth** | **YELLOW** | 3 camps, empty props, IronBastion orphan |
| **Catapult end-to-end feel** | **YELLOW** | Data/AI shipped; train UX partial; PO unfelt |
| **PO Regular clear** | **UNKNOWN** | No closed Phase 0 matrix |

**Bottom line:** The **architecture for a Regular clear is complete**. What is missing is mostly **honesty polish**, **content**, **headless coverage of the interactive deploy loop**, and **felt proof** — not a missing victory controller or deploy tray.

---

## 2. Player flow (numbered)

```
1  Town calm bar → Raids face (hidden if !RaidCapable; dim if army incomplete)
2  Tap → RaidEntryGate.RequestOpen → RaidEntryBridge
3  If army not full → toast "Army N/Cap" + open train; STOP grid
4  RaidSelectionScreen → 3 cards (Regular / Hard / Extreme)
5  Tap Regular → RaidDeployScreen (party, roster, scout report, power, Est clear)
6  Auto Recommend → toast only (not AI loadout)
7  BEGIN ASSAULT → IsSceneInBuild → SceneRouter.GoRaid
8  RaidBase_raider_camp_small loads
9  Self-install: garrison + spire + tray + scoring + HUD + victory
10 Arm troop tile → ground tap → TroopDeployer.SpawnFromArmy (one unit per tap)
11 Rally optional; troops auto-hunt (siege prefers structures)
12a Win: raze RaidSpire → stars/loot/reconcile/claim → GoCastle
12b Retreat/timeout: Finalize(false) + partial loot if any → wound → GoCastle
13 Hub: wounded recovery ticks; re-raid via claim rules
```

**Deploy methods that exist:**

| Method | Where | What it does |
|--------|-------|--------------|
| **Path A teleport** | Selection → Deploy → GoRaid | **V1 product path** |
| **In-raid ground drop** | RaidDeployController tray | One `PlayerTroop` per tap |
| **Rally point** | Same controller | Idle troops walk; combat wins over rally |
| **Retreat** | Same (confirm optional) | Reconcile wounded + home |
| **DevEnterRaid** | DevPanel | Skip UI into a RaidBase_* |
| **Walk-to (`ff.raidwalk`)** | RaidEntryBridge | **Not V1** — nearest EnemyOutpost |
| **Village2 raid silo** | Separate controller | **Do not mix** with flagship UI |
| **Auto Recommend** | Pre-deploy | Toast; **no composition change** |
| **Deploy all** | — | **Does not exist** (manual one-by-one only) |

---

## 3. File map (spine)

| Step | Primary files |
|------|----------------|
| Capability / dim | `RaidCapabilityHudBridge`, `PostureSignals`, `HudActionBarModel`, `ArmyReadiness`, `RaidEntryGate` |
| Entry | `RaidEntryBridge`, `FeatureFlags` |
| Select | `RaidSelectionScreen`, `RaidSelectionVM`, `SceneConfigCatalog`, `scene-configs.json` |
| Pre-deploy | `RaidDeployScreen`, `RaidDeployVM` |
| Load | `SceneRouter.GoRaid` / `IsSceneInBuild`, `EditorBuildSettings` |
| Base | `RaidBase_*.unity`, `RaidBaseGenerator`, `RaidGarrisonSpawner`, `RaidSpire`, `GarrisonTurretArmer` |
| Field command | `RaidDeployController`, `TroopDeployer`, `TroopFactory`, `TroopController`, `TroopRally` |
| Score / win | `RaidScoring`, `RaidHudController`, `RaidVictoryController`, `RaidClaimService` |
| Army | `ArmyStorage`, `BarracksService`, recovery services |
| Catapult | `troops.json` `troop-catapult`, `TroopDef.maxOwned`, siege hunt, `Structures/Catapult` |

**Flagship configs (Resources, wins at runtime):**

| Id | Scene | Diff | rec/2★ (display) | Live clock | reward× | elite | props |
|----|-------|------|------------------|------------|---------|-------|-------|
| raider_camp_small | RaidBase_raider_camp_small | Regular | 270 / 350 | **180s** | 1.0 | 0 | empty |
| fortified_garrison | …_fortified_garrison | Hard | 330 / 430 | **180s** | 1.5 | 1 | empty |
| mage_enclave | …_mage_enclave | Extreme | 420 / 545 | **180s** | 2.2 | 3 | empty |

All three scenes **in** EditorBuildSettings. `RaidBase_IronBastion` on disk + `ORPHAN.md` — **not** in build/configs.

---

## 4. Regression coverage matrix

| Concern | Suite | Headless? | Gap? |
|---------|-------|-----------|------|
| Raids face hide/dim | HudActionBarRegression | Yes | No |
| Army readiness formula | ArmyReadinessTests | EditMode | No |
| Wounded recovery | ArmyRecoveryRegression | Yes | No |
| Muster → train | ArmyMusterRegression | Yes | Not raid deploy |
| 8-troop roster + catapult data | TroopRosterRegression | Yes | No |
| Scout honesty (no shard/reward lie in scout) | RaidDeployUiRegression | Yes | Cards still show reward× |
| Star/loot pure math | RaidScoringRegression | Yes | Config times unused |
| Arena shape / dead keys / spire assets | RaidArenaShapeRegression | Yes (disk) | No PlayMode combat |
| Deploy VM grouping | RaidDeployVMTests | EditMode | CanDeploy+IsSceneInBuild risk; suite stamp stale |
| eliteCount ExpandComposition | — | — | **Missing oracle** |
| maxOwned second train | roster field only | — | **No BarracksService oracle** |
| BEGIN ASSAULT / tray / drop | — | — | **PO only** |
| Spire → claim → return | source lint partial | — | **No scene run** |
| Nav path troop → spire | — | — | **PO / unknown** |
| Catapult structure prefer live | — | — | **PO only** |
| Full Regular clear | WO-932 Phase 0 | PO | **Open** |

---

## 5. Gaps register (what we are missing)

### P0 — process / feel (code spine OK)

| ID | Missing | Why it matters |
|----|---------|----------------|
| **P0-feel** | Closed PO Regular clear matrix | Code green ≠ shippable feel |
| **P0-nav** | Proof troops path to wall/spire after drop | Stuck troops = soft fail raid |
| **P0-cat-feel** | Escort catapult peels tower; naked dies; second train blocked | WO-933 acceptance unclosed |

### P1 — product lies, UX, policy

| ID | Missing / wrong | Evidence |
|----|-----------------|----------|
| **G-clock-lie** | UI Target/Est clear uses 270–545s; live clock is **180s** | `RaidDeployVM.TargetTime` vs `RaidScoring._clockSeconds = 180` |
| **G-data-1** | Cards show loot× / shard %; loot math **ignores** them | `RaidSelectionScreen.RewardHint` vs `RaidScoring.ComputeLoot` |
| **G-ui-1** | Auto Recommend not a loadout AI | Toast only |
| **G-ui-2** | Scout sketch placeholder | Explicit “not yet available” |
| **G-policy-1** | Full army required to open list | Harsh vs “≥1 troop” soft start |
| **G-tray-1** | No deploy-all / formation drop | One-by-one only vs power “full army” copy |
| **G-train-maxOwned** | Train UI `CanTrain` ignores `maxOwned` | `TroopTrainingVM` uses `CanTrain(id, SlotOf)` without maxOwned seam → CTA can look OK then enqueue refuses |
| **G-cat-glyph** | Siege shown as melee-style ranged flag | `RaidDeployVM` only `Ranged` bool; siege → MEL-ish |
| **G-content-1..3** | 3 camps; empty props; IronBastion orphan | scene-configs + ORPHAN.md |
| **G-content-4** | Elites = duplicate units not elite kits | ExpandComposition |
| **G-hero-1** | Hero/companion role on battlefield unclear | Party row + control ensurer |

### P2 — polish / debt

| ID | Item |
|----|------|
| **G-test-1** | ExpandComposition + maxOwned train EditMode |
| **G-test-2** | Refresh RaidDeployVMTests for IsSceneInBuild |
| **G-copy-1** | Stale “Auto Recommend stub” headers |
| **G-cat-vfx** | No rock projectile (instant damage) |
| **G-path-1** | Walk-to re-enableable via prefs |
| **G-path-2** | Village2 silo confusion |
| **G-claim-1** | Re-raid after claim underdocumented |
| **G-audio-1** | No fail sting on timeout |

### Explicitly **not** missing (closed)

Capability toast · Army N/Cap · BEGIN ASSAULT build check · 3 scenes in build · eliteCount consumer · structure damage WO-853 · spire win · retreat Finalize(false) · scoring weights 50/30/20 · dual-copy troops including catapult · structure-prefer siege AI code.

---

## 6. Catapult integration (WO-933) checklist

| Surface | Status |
|---------|--------|
| Catalog dual-copy + T4 unlock + announce | **OK** |
| maxOwned=1 at EnqueueTraining | **OK (code)** |
| Train panel reflects maxOwned before click | **Partial** (G-train-maxOwned) |
| Deploy tray when owned | **OK** (generic tile) |
| Spawn path Structures/Catapult | **OK (path + factory)** |
| Structure prefer + damage mult | **OK (code)** |
| Siege glyph / icon on deploy row | **Weak** |
| Projectile VFX | **Missing (by design V1)** |
| PO escort peel | **Open** |

Catapult is **optional T4** — Regular clear does **not** require it.

---

## 7. Recommended work slices (priority order)

1. **PO Phase 0 feel matrix** (blocking for “raids done”) — Regular camp end-to-end + retreat loot.  
2. **Honesty pass** — clock/star display vs 180s; wire or strip rewardMultiplier/shardDropChance.  
3. **Train maxOwned UX** — feed `maxOwned` into TroopTrainingVM.CanTrain + clear refuse copy.  
4. **Auto Recommend product** — hide or real scout/RPS picker.  
5. **Full-army policy** — keep hard gate or allow ≥1 with “fill army” CTA.  
6. **Content** — props or accept barren; IronBastion keep/drop.  
7. **Headless oracles** — ExpandComposition; maxOwned train; optional CanDeploy build-list fake.  
8. **Catapult polish** — siege glyph; felt escort; rock VFX later.  
9. **Hero-in-raid ruling** — spectator vs combatant.

---

## 8. Minimal PO checklist (copy for playtest)

- [ ] Barracks built, ≥1 deployable; Raids face appears  
- [ ] Incomplete army: dim + N/Cap toast + no grid  
- [ ] Full army: Regular card → BEGIN ASSAULT → loads  
- [ ] Tray lists all deployable types (incl. catapult if T4 owned)  
- [ ] Drop footman/archer; they move and fight  
- [ ] (T4) Drop catapult outside tower range; peels structure; dies if under fire  
- [ ] (T4) Second catapult train refused while first owned/wounded  
- [ ] Raze spire → stars/loot → return hub  
- [ ] Mid-fight retreat with damage done → partial loot + wounded  
- [ ] No softlock if you open retreat immediately  

---

*Audit date: 2026-08-09. Supersedes nothing dated earlier for open gaps; still valid with AUDIT_RAIDS_POST_WO932 + WO-933 RESULT.*
