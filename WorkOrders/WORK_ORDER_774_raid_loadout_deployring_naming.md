# WO-774 — Raid V1 felt-slice: loadout handoff + deploy ring + Army/Deploy naming

**Status:** READY TO IMPLEMENT
**Minted:** 2026-07-26 (CLI, from Grok read-only CoC systems review, relayed by owner)
**Lane:** Raid V1 UX (single lane, no sim). Sequenced AFTER WO-771.9 integration + barracks-catalog-structure land (this lane touches the troop spawn/deploy path — do not run concurrently with 771.9 spawn-wiring).
**Anchor:** `docs/RAID_NORTHSTAR.md` · `PAIN_POINTS_2026-07-26.md` (F1 stakes ladder, pipeline line 217)

## Why (review verdict, absorbed)
The raid V1 spine ALREADY EXISTS end-to-end and is CoC-shaped: train (TroopTrainingVM/Panel + multi-channel queue) → army storage (ArmyStorage cap+perk+veterancy) → pick target (RaidSelectionScreen/VM, `ff.raidwalk=0`) → pre-raid (RaidDeployScreen) → teleport (SceneRouter.GoRaid → RaidBase_*) → tap-deploy (RaidDeployController tray + ground raycast + TroopDeployer.SpawnFromArmy) → auto-fight (TroopController hunts Hostile) → stars/loot/clock (RaidScoring 180s + RaidHudController) → victory/claim. **This is polish + clarity, NOT a rebuild.**

The three P0 gaps that make it feel broken / un-CoC:
1. **No loadout** — pre-raid `RaidDeployScreen.Deploy()` ≈ `GoRaid(scene)`; the field tray uses the FULL `GetDeployable()`. No "bring 6 footmen, leave the rest home."
2. **Two "deploy" concepts** — the pre-raid modal AND the in-raid tray both read as "Deploy." Un-teachable; docs + players blur them.
3. **Deploy anywhere on NavMesh** — CoC deploys outside walls first; anywhere = cheese + unreadable.
Plus victory/star copy must match the star math (kills, not "base destroyed").

## Scope (P0 only — the review's #1 ROI slice)

### 1. Naming (copy only — no logic change)
| Screen | Rename to | Player job |
|---|---|---|
| Barracks | **Train** | queue units, see timer |
| Selection | **Raids** | pick target difficulty |
| Pre-raid modal (RaidDeployScreen) | **Army** (or **War Band**) | choose who comes |
| In-raid tray (RaidDeployController) | **Deploy** | drop units on the map |
Never label both the modal and the tray "Deploy." Pure string/label edits + any header constants.

### 2. Loadout handoff (highest UI leverage)
- Pre-raid **Army** screen: per-type **steppers** (− / count / +), each capped by `owned` AND raid housing cap.
- **Loadout bar** at bottom: icons × counts the player WILL take (not the whole roster).
- **Housing fill bar** for the raid party (distinct from global Army N/M).
- Pass the chosen loadout into the raid scene via **`RaidParams` / SceneRouter pending-bag** (mirror the existing PendingBattle handoff pattern in SceneRouter). The in-raid tray **arms ONLY what is in the loadout** (replace the field tray's `GetDeployable()` source with the loadout bag).
- `Auto Recommend` = fill a simple recipe (e.g. 50% melee / 30% ranged / 20% siege), NOT "select all." (Recipe can be a const table for V1.)
- Scout strip = **stub OK** for V1 (walls/towers/boss one-liner) — drives "why this army"; real scout data deferred.

### 3. Deploy ring (spatial rule)
- Define a **deploy ring / spawn apron** on each `RaidBase_*` (outside the outer wall). Only that polygon accepts first drops.
- Field raycast: reject a tap outside the ring; **ghost preview** silhouette under finger + **forbidden red** outline outside the ring.
- Breach-expand (open interior after a gate/wall dies) is **V1.5 — DO NOT build here** (park).

### 4. Victory/defeat copy matches star math
- RaidScoring destruction% is **garrison kills**, not structure destruction. Label the HUD/summary readout **"Defenders"** (e.g. "Defenders 40%"), NOT "Base %."
- Victory/defeat panel copy must match: "defenders fallen / retreated," not "base destroyed." Stars stay as-is for V1 (full clear under clock = 3★) but the COPY must not over-promise CoC structure-destruction.

### 5. Train channel UI visible
- Barracks **Train** tab shows the **Train channel queue** (active + pending) from the multi-channel queue — not a silent `TrainNow`. A tiny global chip ("Builders 1/2 · Training 0:42") is nice-to-have (can fold into VillageHudController later).

## Files (expected — verify seams first, §12)
- `Assets/_Modules/.../Raid/RaidDeployScreen*.cs` + its VM (Army screen: steppers, loadout bar, housing fill, Auto Recommend, scout stub, MARCH).
- `Assets/_Modules/Core/SceneRouter.cs` (RaidParams / pending loadout bag — mirror PendingBattle).
- `Assets/_Modules/.../Raid/RaidDeployController.cs` + `TroopDeployer.cs` (tray arms loadout only; deploy-ring raycast + ghost/forbidden preview).
- `RaidBase_*` bases: deploy-ring polygon (data/marker; a builder edit if the ring is baked — coordinate the single Unity gate; do NOT hand-edit scene files, §3 — add via the RaidBaseGenerator).
- `RaidScoring*.cs` / `RaidHudController*.cs` (Defenders label + victory copy).
- Barracks Train panel/VM (show Train channel queue).

## Acceptance (data-verified — §12, no source-lint-only)
Add these regression oracles (wired into `DataRegression.RunAll`) + PlayMode where the Unity gate allows:
- **RaidLoadoutRegression** — pre-raid steppers cap at min(owned, housing); the loadout bag passed to the raid scene == the stepper selection; the field tray's armable set == the loadout (NOT full GetDeployable).
- **RaidDeployRingRegression** — a drop inside the ring succeeds (OwnedTroopId stamped, count decrements); a drop outside the ring is rejected (no spawn, count unchanged).
- **RaidCopyRegression** — HUD/victory strings read "Defenders"/"defenders fallen," never "base destroyed"; no screen labels both the modal and the tray "Deploy."
- **PlayMode (gate-permitting):** "GoRaid → arm from loadout → drop inside ring → kill one defender → retreat → summary shows wounded + stars." Add `[Flow:Raid] deploy drop def=… owned=… pos=…` + `[Flow:Raid] score stars=… def%=… loot=…` instrumentation.

## Do NOT touch (park)
- Fixed-point RaidSim / async PvP (WO-771.3 / 771.7).
- Walk-to-outpost as the primary loop (`ff.raidwalk` stays OFF; do not delete the walk path, just don't feed it).
- Breach-expand deploy zone, structure % destruction, army presets, post-raid shields — **V1.5** (see ladder below).
- Hero micro through the fortress; UXML raid panels.

## Full ladder recorded (from the review — for sequencing, NOT all this WO)
- **P0 (this WO):** loadout+naming, deploy ring, victory/star copy, Train-queue UI.
- **P1 (next raid WOs):** scout stub + Auto Recommend recipes; ghost preview + drop VFX/SFX; 2× speed toggle; one perfect Footman + one Archer silhouette (art lane — depends on Lane B pack tooling + KayKit Phase 2); star thresholds tied to boss/gate not only full clear.
- **P1.5 = WO-771.6 stakes:** casualties + stars + soft loot (PAIN_POINTS F1).
- **P2 / V1.5:** breach-expands-deploy-zone; structure % destruction; favorite army presets; post-raid shields; **barracks as a real upgradable catalog building** (that's the F3 follow-on this session already queued).
