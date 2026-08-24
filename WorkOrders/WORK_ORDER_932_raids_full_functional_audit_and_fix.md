# WORK ORDER 932 — Raids: full functional audit + step-by-step to “working end-to-end”

**Status:** READY — **PARTIAL: PHASES 1–4 CODE LANDED 2026-08-08** (see `WORK_ORDER_932_raids_full_functional_audit_and_fix.RESULT.md`); Phase 0 felt matrix + Phase 5/6 gates remain. Verified in the tree 2026-08-24: `RaidCapabilityHudBridge`, `RaidDeployScreen` "BEGIN ASSAULT", `RaidScoring` "RAID CLOCK armed". *(This line read "READY TO IMPLEMENT" until 2026-08-24 — the board therefore showed shipped work as not-started.)*  
**Minted:** 2026-08-08 (CLI / Grok — owner: detailed raids audit + guidance to fully functional)  
**Silo:** Raids / Troops / Scenes (Combat + UI; no VillageSceneBuilder)  
**Roles:** CLI implements phases in order; PO felt-closes each phase  
**Type:** AUDIT (grounded) + IMPLEMENTATION PLAN + ACCEPTANCE  
**Canon:** Teleport/deploy loop is **LOCKED V1** (`FeatureFlags.RaidContinuousWalk` default **OFF**, WO-771). Walk-to outposts are optional A/B (`ff.raidwalk=1`).

---

## 0. One-line truth

Raids are **mostly built** as a spine (HUD → select → deploy screen → `RaidBase_*` scene → troop tray → combat → score → claim → return), with **headless math gates** and three **flagship** configs. They are **not fully player-functional** until entry gates, army readiness, scene registration, garrison/objective, scoring/victory, and retreat all work on a **fresh save with barracks + troops**, with no soft-lock and no silent dead buttons.

This WO is the map + the fix ladder. **Do not greenfield** a second raid system.

---

## 1. Architecture map (what “a raid” is)

### 1.1 Two product paths (easy to conflate)

| Path | Flag | Entry | Combat surface | Status |
|------|------|--------|----------------|--------|
| **A — Teleport / deploy (V1 LOCKED)** | `ff.raidwalk` **OFF** (default) | HUD Raids → selection → pre-deploy → `SceneRouter.GoRaid` | `RaidBase_*` plate: deploy tray + garrison + spire | **Primary** |
| **B — Continuous walk** | `ff.raidwalk` **ON** | HUD Raids → ping nearest outpost; walk out gate | Live `EnemyOutpost` in merged world | Secondary / A/B only |

**This WO’s “fully functional” target = Path A** unless owner re-opens walk-to as default.

### 1.2 Path A pipeline (code-grounded)

```
[Player] barracks built + ≥1 deployable troop (capability)
    → HUD Raids face visible (RaidCapabilityHudBridge → PostureSignals.RaidCapable)
    → tap Raids (HudKit → RaidEntryGate.RequestOpen)
    → RaidEntryBridge (FeatureFlags.Raid must be ON; raidwalk OFF)
    → RaidSelectionScreen.Open()
         · ArmyReadiness.Compute — if NOT full army → toast + drillmaster (WO-820)
         · cards from SceneConfigCatalog flagship ids
    → tap card → RaidDeployScreen.Open(def)
         · party row + troop list + DEPLOY CTA
    → DEPLOY → RaidDeployVM.Deploy() → SceneRouter.GoRaid(def.sceneName)
    → RaidBase_<id>.unity loads
         · RaidGarrisonSpawner: boss + composition (EnemyFactory)
         · RaidSpire: central objective (if authored)
         · RaidDeployController: bottom tray DEPLOY/RALLY/RETREAT
         · RaidScoring: 180s clock + stars + loot math
         · RaidHudController: timer / stars / destruction / troops
         · RaidVictoryController: OnCleared → claim + companion + return banner
    → GoCastle (retreat or victory return)
```

### 1.3 Key files (do not invent duplicates)

| Layer | File | Role |
|-------|------|------|
| Flag | `FeatureFlags.cs` | `Raid` default **true**; `RaidContinuousWalk` default **false** |
| HUD entry | `RaidEntryGate.cs` (Core), `RaidEntryBridge.cs`, `RaidCapabilityHudBridge.cs` | Button → open; grey/hide rules |
| Select | `RaidSelectionScreen.cs` / `RaidSelectionVM.cs` | Card grid |
| Pre-deploy | `RaidDeployScreen.cs` / `RaidDeployVM.cs` | Party/army math + GoRaid |
| Route | `SceneRouter.GoRaid` | Fade load raid scene |
| Data | `scene-configs.json` + `SceneConfigCatalog` | Difficulty, garrison, sceneName, rewards |
| Bake | `RaidBaseGenerator`, `RaidNavBake`, `RaidSceneRegistrar` | Editor: geometry + build settings |
| Scenes | `Assets/Scenes/RaidBase_*.unity` | Three flagships in EditorBuildSettings |
| In-raid | `RaidDeployController`, `RaidGarrisonSpawner`, `RaidSpire`, `RaidScoring`, `RaidHudController`, `RaidVictoryController`, `RaidClaimService` | Play loop |
| World alt | `RaidOutpostSystem`, `Village2RaidController` | Walk / Village2 (out of Path A scope) |
| Tests | `RaidScoringRegression`, `RaidDeployUiRegression`, `RaidArenaShapeRegression`, EditMode tests | Headless gates |

### 1.4 Flagship raids (data)

From `RaidSelectionVM.FlagshipRaidIds` + `scene-configs.json`:

| Config id | Scene | Difficulty | Build settings |
|-----------|--------|------------|----------------|
| `raider_camp_small` | `RaidBase_raider_camp_small` | Regular | ✅ registered |
| `fortified_garrison` | `RaidBase_fortified_garrison` | Hard | ✅ registered |
| `mage_enclave` | `RaidBase_mage_enclave` | (tiered) | ✅ registered |

**On disk but not flagship / not in build list:** `RaidBase_IronBastion` — dead asset unless registered + catalogued.

---

## 2. AUDIT — what already works (do not rebuild)

| # | Capability | Evidence |
|---|------------|----------|
| A1 | Feature flag ON for raids | `FeatureFlags.Raid => defaultOn: true` |
| A2 | Teleport path is default | `RaidContinuousWalk => defaultOn: false` (WO-771 lock) |
| A3 | HUD → selection wiring | `RaidEntryBridge` subscribes `RaidEntryGate` + legacy `RaidRequested` |
| A4 | Hide Raids when unusable | `RaidCapabilityHudBridge`: flag + barracks + ≥1 deployable |
| A5 | Full-army gate on open | `RaidSelectionScreen.Open` → `ArmyReadiness.Compute` → drillmaster |
| A6 | Three raid cards from data | `RaidSelectionVM` + `SceneConfigCatalog` |
| A7 | Pre-deploy modal + DEPLOY → GoRaid | `RaidDeployScreen.OnDeploy` → `_vm.Deploy()` |
| A8 | Scenes in player build | EditorBuildSettings has three `RaidBase_*` |
| A9 | Garrison spawn | `RaidGarrisonSpawner` + `EnemyFactory` + composition JSON |
| A10 | Deploy / rally / retreat tray | `RaidDeployController` self-installs on `RaidBase_*` |
| A11 | Clock + stars + loot pure math | `RaidScoring.ComputeStars` / `ComputeLoot` + regression |
| A12 | Live raid HUD | `RaidHudController` (timer, stars, destruction, troops) |
| A13 | Victory → claim → return | `RaidVictoryController` + `RaidClaimService` + GoCastle |
| A14 | Headless regressions | `RaidScoringRegression`, deploy UI / arena shape suites |

**Stale comment trap:** `RaidEntryBridge` still logs *“victory/return not built”* when flag OFF — victory **was** built; fix the comment when touching that file.

---

## 3. AUDIT — gaps blocking “fully functional”

### P0 — Player cannot complete a real raid loop

| ID | Gap | Why it hurts | Grounding |
|----|-----|--------------|-----------|
| **G1** | **Prereq opacity** | Fresh save: no barracks / no troops → Raids face hidden; player never learns *why* | Capability bridge silent hide |
| **G2** | **Full-army gate is harsh** | Open Raids with *some* troops but not full cap → forced drillmaster; may feel “broken” not gated | `ArmyReadiness` in `Open()` |
| **G3** | **Auto Recommend is a stub** | Button logs only; does not equip/select troops | `RaidDeployScreen` “Auto Recommend (stub)” |
| **G4** | **Deploy tray vs pre-deploy confusion** | Pre-deploy is tactical briefing; real drops happen *in* raid. Easy to think DEPLOY “did nothing” if no toast / scene fail | Two different “deploy” words |
| **G5** | **Scene load failure silent to feel** | Missing scene → toast “no battleground” only if `CanDeploy` false; wrong sceneName / not in build = soft fail | `CanDeploy` / GoRaid |
| **G6** | **Objective clarity** | Win = clear garrison and/or raze **spire**; if spire missing/broken, clear rules muddy | `RaidSpire` + `RaidScoring` |
| **G7** | **Victory / scoring dual paths** | Ensure `OnCleared` and clock expiry both finalize stars/loot once; no double grant / no soft-lock | Victory + Scoring events |
| **G8** | **Retreat army reconciliation** | Wounded/recovery must match design; survivors return home | `RaidDeployController` retreat |

### P1 — Content / data incomplete

| ID | Gap | Grounding |
|----|-----|-----------|
| **G9** | `eliteCount` authored, **not consumed** | `scene-configs.json` note + oracle allowlist |
| **G10** | `IronBastion` scene orphan | On disk, not in build settings / flagship list |
| **G11** | Props set empty in configs | `"props": { "set": [], "count": N }` — camp looks empty |
| **G12** | Only 3 flagship raids | More camps need catalog + bake + register |

### P2 — Product polish / design debt

| ID | Gap | Grounding |
|----|-----|-----------|
| **G13** | Hero portraits on pre-deploy; spectator model pending | Comment RAID_BATTLEFIELD_ANATOMY — hero may leave raids entirely later |
| **G14** | Walk path vs teleport confusion in docs/guide | Guide text says “no march; you arrive at the edge” — OK for Path A; walk path different |
| **G15** | Village2 as raid target | Separate controller; do not mix into flagship Path A until owned |
| **G16** | Arena Herald also opens RaidSelectionScreen | Same Path A; ensure not double-modals |

---

## 4. Definition of “fully functional” (acceptance product)

A raid is **fully functional** when a player with:

1. Barracks built  
2. At least **one** deployable troop (and preferably full army per WO-820)  

can:

| Step | Expected |
|------|----------|
| 1 | See **Raids** on HUD (or clear teach why not) |
| 2 | Open selection → see **3 named camps** with difficulty + time + reward hint |
| 3 | Open pre-deploy → see army/party numbers → **DEPLOY** loads correct scene |
| 4 | In raid: tray shows troop types; **tap ground** drops troops; they path/fight |
| 5 | HUD shows timer + objective/destruction |
| 6 | Clear garrison / raze spire → **victory** banner + loot + claim + return home |
| 7 | **Retreat** mid-fight returns home; survivors/wounded correct |
| 8 | Clock expiry ends raid (no infinite fight) |
| 9 | Second raid possible same session (no soft-lock, no double-claim exploit on same base without design) |

Headless: `COMPILE_GATE_OK` + `REGRESSION_OK` including raid suites.

---

## 5. Step-by-step implementation plan

### Phase 0 — Prove the live path (instrument first · ½ day)

**Do not edit combat until this passes.**

1. Fresh save OR known save with barracks + troops.  
2. Play with logs: filter `[Flow:Raid]`.  
3. Checklist (write answers into RESULT):

| Probe | Pass? | Notes |
|-------|-------|-------|
| `FeatureFlags.Raid` true | | |
| `RaidCapable` edge fires when barracks+troops | | |
| Raids face visible | | |
| Open selection — card count 3 | | |
| Full-army: blocked vs allowed | | |
| DEPLOY loads `RaidBase_*` (watch scene name) | | |
| Garrison count > 0 after 2s | | |
| Spire present (HP bar / objective) | | |
| Drop 1 troop — alive | | |
| Kill all / raze spire — victory UI | | |
| Return home works | | |
| Retreat works | | |

4. Capture `break-log` / Player.log snippets for any FAIL.

**Exit:** written matrix of G1–G8 status (PASS / FAIL / N/A).

---

### Phase 1 — Entry + teach (P0 · G1–G2)

| Step | Action |
|------|--------|
| 1.1 | When Raids **hidden**, one-time toast or codex tip: “Build a Barracks and train troops to unlock Raids.” |
| 1.2 | When Raids **visible but not full army**, keep WO-820 redirect **or** owner-rulable: allow open with ≥1 troop (dim “full army” bonus). **Default: keep full-army gate**; only change on owner ruling. |
| 1.3 | Fix stale `RaidEntryBridge` log text about victory not built. |
| 1.4 | FlowTrace on every refuse: flag / capability / army / scene. |

**Accept:** new player understands unlock; veteran with full army opens grid every time.

---

### Phase 2 — Deploy CTA honesty (P0 · G3–G5)

| Step | Action |
|------|--------|
| 2.1 | **Auto Recommend:** either implement (select all deployable / max power under cap) **or** hide the button until implemented. No stub CTA. |
| 2.2 | On DEPLOY success: toast “Assaulting {displayName}…” then GoRaid. |
| 2.3 | Before GoRaid: assert `EditorBuildSettings` / `SceneRouter.IsSceneRegistered(sceneName)`; if false → toast “Raid under construction” + FlowTrace.Fail. |
| 2.4 | Verify `RaidDeployVM.CanDeploy` requires non-empty `sceneName` matching a registered scene. |
| 2.5 | Copy: pre-deploy “DEPLOY” label → optional “BEGIN ASSAULT” to distinguish from in-raid ground drop. |

**Accept:** never a silent DEPLOY; Auto Recommend not a fake button.

---

### Phase 3 — In-raid loop integrity (P0 · G6–G8)

| Step | Action |
|------|--------|
| 3.1 | On raid start: FlowTrace peak garrison count, spire max HP, clock seconds. |
| 3.2 | If garrison composition empty or all spawn fails → Fail loud + allow Retreat (no soft-lock). |
| 3.3 | Wire **one** win condition doc in RESULT: spire razed **OR** garrison cleared (state actual code). Align HUD copy. |
| 3.4 | `RaidScoring` finalize once: victory path + time expiry + retreat each call finalize without double loot. |
| 3.5 | Victory: stars + loot grant (EconomyService) + claim + companion unlock (if new) + return. |
| 3.6 | Retreat: survivors home, wounded recovery timer, GoCastle. |
| 3.7 | Manual: clear Regular camp under clock → 2–3 stars; die/retreat → 0–1 stars. |

**Accept:** no soft-lock after clear; loot once; retreat safe.

---

### Phase 4 — Data / content (P1 · G9–G12)

| Step | Action |
|------|--------|
| 4.1 | Implement `eliteCount` in `RaidGarrisonSpawner` **or** remove from JSON + oracle allowlist (no dead keys). |
| 4.2 | Either register + catalog `IronBastion` as 4th raid **or** delete/archive scene to stop confusion. |
| 4.3 | Props: fill `props.set` with real KayKit tokens **or** drop count to 0 and stop pretending. |
| 4.4 | Re-bake any config change via `RaidBaseGenerator` + `RaidNavBake` + build settings. |
| 4.5 | Nav smoke: deploy troop reaches wall/enemy (not stuck off-mesh). |

**Accept:** three flagships playable and legible; no orphan scenes claiming to be raids.

---

### Phase 5 — Polish / product debt (P2 · G13–G16)

| Step | Action |
|------|--------|
| 5.1 | Owner ruling: hero in raid vs spectator-only → update pre-deploy party row. |
| 5.2 | Guide copy sync with Path A (already mostly correct). |
| 5.3 | Herald + HUD both open same selection — ensure PanelManager single-modal. |
| 5.4 | Optional: star-reward breakdown panel (WO-431 style) — **not** required for “functional.” |

---

### Phase 6 — Verification battery (always last)

| Gate | Command / action |
|------|------------------|
| Compile | `CompileGate.Run` → `COMPILE_GATE_OK` |
| Data | `DataRegression.RunAll` → `REGRESSION_OK` including raid suites |
| Manual matrix | Phase 0 table all PASS on Regular camp |
| Capture | Selection → deploy → in-raid HUD → victory PNGs opened |
| RESULT | This WO’s RESULT with commit SHAs + residual backlog |

---

## 6. Recommended work order for implementers

**Minimum path to “fully functional Regular raid”:**  
**Phase 0 → 1 → 2 → 3 → 6.**  

Phases 4–5 can follow without blocking the Regular loop.

Do **not**:
- Reintroduce `ff.raidwalk` as default without owner OK  
- Build a second selection UI  
- Hand-edit `.unity` raid scenes (use generator)  
- Claim done from regressions alone without a manual Regular clear  

---

## 7. Quick reference — commands

```text
# Headless
powershell -File ./run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName compile-gate.log
powershell -File ./run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression.log

# Feel (editor closed for batch; open editor for play)
# Play hub → train troops → Raids → Small Raider Camp → DEPLOY → clear → Return
```

Dev force: if needed, `ff.raid=1`, ensure barracks, train via drillmaster / TroopDialogueCommands.

---

## 8. RESULT template

`WorkOrders/WORK_ORDER_932_raids_full_functional_audit_and_fix.RESULT.md`

Must include:
- Phase 0 probe matrix  
- Phases completed  
- Any owner rulings (full-army gate, hero-in-raid, IronBastion keep/drop)  
- Commit SHAs  
- “PO felt: Regular clear + retreat” checkbox  

---

## 9. Related WOs (do not renumber; point here)

| Topic | Existing |
|-------|----------|
| Full army gate | WO-820 |
| Empty army teach | WO-813 |
| Capability hide | WO-835 |
| Scoring V1 | WO-771.6 / regressions |
| Walk-to path | WO-449 / `ff.raidwalk` |
| Village2 | separate controller |

This WO **supersedes** informal “make raids work” tickets: implement against **this** ladder.
