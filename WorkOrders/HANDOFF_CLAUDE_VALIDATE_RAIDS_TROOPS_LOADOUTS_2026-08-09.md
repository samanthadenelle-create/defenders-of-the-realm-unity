# Claude validation handoff — Raids / Troops / Loadouts (2026-08-09)

**Branch:** `wip/village2-and-f8-tickets`  
**Seat:** CLI (Grok) implementation  
**PO / UI seat (Claude):** validate feel, honesty, and sign-off  
**Push:** not performed unless owner asks after your sign-off  

**Gates last green (machine):**  
- `COMPILE_GATE_OK`  
- `REGRESSION_OK` / DataRegression **~129–130/130** (see residual note below)  
- `ARMY_MUSTER_OK`, `TROOP_ROSTER_OK` (8 troops), `RAID_SCORING_OK`, CORE_SAVE **v38**  

---

## 0. One-sentence summary

This batch makes **raid troops readable and siege-capable**, makes **raid UI/loot/clock honest**, and adds a **3-slot saved army loadout bank** so the player can plan and auto-queue training before raids — without inventing a second train queue.

---

## 1. Work packages (high level)

### A. Troop art / gear (pre-WO polish)

**Problem:** All troops shared bare bodies; no weapons; roles hard to tell apart.

**What changed:**
- New **`TroopGearApplier`**: after spawn, attaches weapon/offhand Resources prefabs to Humanoid hands (bow → LeftHand).
- **`TroopDef`**: optional `weapon` / `offhand` paths.
- **`TroopFactory`**: calls gear applier after animator bind; siege machines skip humanoid gear/anim.
- **`SupercyanResourceWire`**: mirrors bodies + gear into `Resources/Heroes/SC_*` and `Resources/TroopGear/*`.
- **`troops.json` dual-copy**: models remapped (e.g. Outrider → SC_Barbarian, Battlemage → SC_Mage) + gear fields.

**PO feel:** Barracks train tray + raid tray — distinct silhouettes and held gear; no T-pose regression.

---

### B. WO-932 — Raids functional fix ladder (earlier in same tree)

**Problem:** Raid spine existed but had silent fails, dead keys, weak teach, retreat without loot, etc.

**What changed (high level):**
- Capability teach toast; full-army **Army N/Cap** toast.
- **BEGIN ASSAULT** CTA; build-settings scene check (`IsSceneInBuild` / `CanDeploy`).
- Auto Recommend no longer fully silent (later renamed/honest — see C).
- `eliteCount` **consumed** by garrison spawner (not dead key).
- Props honesty when empty.
- Retreat/timeout: **Finalize(false) + partial loot**.
- Scene-router / entry path cleanliness.

**Docs:** `WORK_ORDER_932_*`, `AUDIT_RAIDS_POST_WO932_*`.

---

### C. Raid honesty pass (test readiness)

**Problem:** UI lied about clock and rewards; train CTA ignored ownership caps.

**What changed:**

| Area | Before | After |
|------|--------|--------|
| Raid clock display | Config times 270–545s | **Clock 180s** (matches live `RaidScoring`) |
| `scene-configs` clear times | Mismatched | Dual-copy **recommendedClearTime=180**, twoStar soft band |
| Loot multiplier on cards | Shown, **ignored** by math | **`ComputeLoot` multiplies** by `rewardMultiplier` |
| Echo Shard % on cards | Shown, **no grant path** | **Removed from card copy** |
| Auto Recommend | Sounded like AI | **“Army Ready?”** status toast only |
| Scout preview | “not yet available” | “Assault to recon…” |
| Siege list glyph | MEL | **SIE** |
| Train maxOwned | Enqueue blocked, UI could look OK | **VM greys CTA + toast reason** (incl. wounded) |

**Key files:** `RaidScoring`, `RaidDeployScreen/VM`, `RaidSelectionScreen`, `TroopTrainingVM/Panel`, dual `scene-configs.json`.

---

### D. WO-933 — Siege Catapult (CoC scarcity + WC Demolisher)

**Problem:** No standoff structure-breaker; towers outrange day-one troops.

**Product rules (locked preferred):**
- **1 owned** (`maxOwned: 1`; wounded still counts).
- Role **`siege`**: prefer Hostile structures, else units.
- Fragile, slow, range ~26, heavy cost, T4 with Outrider.
- Art: machine path `Structures/Catapult` (not Supercyan humanoid).

**What changed:**
- 8th troop in dual-copy `troops.json` + upgrades (flat reach) + barracks L4 unlock + tier announce.
- `TroopDef`: `maxOwned`, `structureDamageMult`, `unitDamageMult`.
- `TroopController`: structure-prefer hunt + damage mults.
- `BarracksService` / `ArmyStorage.CountOfDef`: enforce cap at train.
- Factory: wider agent, full Resources path for models with `/`.
- Roster regression 7→8 + siege asserts; spawn-visual understands `Structures/…`.

**PO feel:** Train one → second refused; escort peels towers; naked dies; SIE glyph.

---

### E. WO-934 — Army loadout bank (3 presets + muster polish)

**Problem:** Composition/muster existed but **session-only**, hard to discover, empty-feeling.

**What changed:**
- **Save schema v38:** `ArmyStorage.loadouts` (3 slots) + `activeLoadout`.
- Core DTOs: `ArmyLoadoutBank` / `ArmyLoadoutSlot` / `ArmyLoadoutRow`.
- Village: `ArmyLoadoutService` (load/save/rename/recipes).
- `ArmyComposition` ↔ loadout convert (`FromLoadout` / `ToLoadout`).
- **Polished `ArmyMusterPanel`:**
  - 3 slot tabs  
  - Quick recipes: **Raid / Hold / Siege / Clear**  
  - Save slot, cycle name, Muster (auto-saves active slot)  
  - maxOwned respected in steppers  
- **Barracks Train UI → Armies** button (discoverable).
- Migrator `MigrateToV38` + echo schema pin 38 + muster regression loadout checks.

**Player loop:**  
`Barracks → Armies → pick slot → recipe or [+] → Save → Muster → Train queue → fill army → Raids`

**PO feel:** Plans survive reload; switching slots doesn’t lose work; muster report text is clear (colourblind-safe).

---

## 2. Dual-copy data (must stay byte-aligned)

Always check **both** trees after any edit:

- `Assets/StreamingAssets/Data/Canonical/`  
- `Assets/Resources/Data/Canonical/`  

Touched: `troops.json`, `troop-upgrades.json`, `barracks.json`, `building-tiers.json`, `scene-configs.json`.

**WebGL / Resources wins at runtime** if they drift.

---

## 3. Explicitly NOT done / still open for sign-off

| Item | Notes |
|------|--------|
| PO Regular raid full matrix | Code green ≠ felt green |
| Full-army hard gate policy | Still harsh; not softened |
| IronBastion orphan | Scene on disk + ORPHAN.md; not in build |
| Props empty on bases | Accepted barren |
| Catapult rock projectile VFX | Instant damage V1 |
| Multi-deploy-all formation | Still one-tap-per-unit in raid |
| Hero-in-raid combat role | Unclear / spectator pending |
| Echo Shard real currency | Stripped from UI until grant exists |
| COMBAT/ATB WildlandsRoster canary | Occasional residual RED in DataRegression; **not** loadout path |

---

## 4. Suggested Claude validation script

### Train / loadouts
1. Barracks built → open Train → **Armies**.  
2. Raid recipe → Save → Muster → see Train jobs.  
3. Kill game / reload → slot still has plan.  
4. Switch slots; re-open; no silent wipe.  
5. T4: stage catapult once only; train second blocked with clear copy.

### Raids honesty
6. Selection cards: **Clock 3:00**, **xLoot** only (no fake shard %).  
7. Hard camp loot feels higher than Regular after clear.  
8. BEGIN ASSAULT → tray → drop → fight → spire → return.  
9. Mid-fight retreat → partial loot + wounded.

### Troops / siege
10. Distinct gear on footman/archer/spearman.  
11. Catapult silhouette + structure preference under fire.

---

## 5. File map (for review)

### New
- `Assets/_Modules/Village/Troops/TroopGearApplier.cs`
- `Assets/_Modules/Village/Troops/ArmyLoadoutService.cs`
- `Assets/_Modules/Core/State/ArmyLoadoutBank.cs`
- `Assets/Resources/TroopGear/*`, `Resources/Heroes/SC_Barbarian|SC_Mage`
- WorkOrders WO-932/933/934 + audits + this handoff

### Core systems touched
- Troops: Def, Factory, Controller, Barracks, Gear, Catalog JSON  
- Raids: Scoring, Deploy UI/VM, Selection, Entry/Capability bridges, Garrison, DeployController  
- Army: Storage, Composition, Muster panel  
- Save: Schema v38, Migrator  
- Regressions: TroopRoster, RaidScoring, RuntimeSpawnVisual, ArmyMuster, Echo pin, CombatAtb FQ fix  

---

## 6. Architecture notes for Claude (don’t break)

1. **One Train path:** muster always uses `BarracksService.EnqueueTraining` — never fork.  
2. **maxOwned** enforced at enqueue + train UI + composition stepper.  
3. **Wounded = still owned** for catapult cap.  
4. **Siege hunt** prefers `IDamageable` that also implement `IDamageableStructure` and Hostile.  
5. **Reward mult** is paid in `ComputeLoot`; cards must not invent currencies.  
6. **Clock UI** must stay aligned with `RaidScoring.DefaultClockSeconds` (180).  
7. Dual-copy JSON or WebGL ships the wrong roster/loot times.

---

## 7. Sign-off checklist (Claude → owner)

- [ ] Train gear reads correctly  
- [ ] Loadouts: save / reload / muster / recipes  
- [ ] Catapult: one owned, peels towers, train block  
- [ ] Raid UI honesty (clock + loot)  
- [ ] Regular clear + retreat loot  
- [ ] No softlock on empty assault / retreat  
- [ ] Residual gate RED acknowledged (if any) and not loadout-blocking  

**Owner after Claude OK:** push decision; PO felt close on Phase 0 matrix.

---

*End of handoff. Implementation seat will not claim “player-verified” — only machine gates + code completeness.*
