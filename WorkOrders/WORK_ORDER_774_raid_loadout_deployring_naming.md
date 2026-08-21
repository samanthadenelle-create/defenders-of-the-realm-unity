<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-26
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-26) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-774 — Raid V1 felt-slice: loadout handoff + deploy ring + Army/Deploy naming

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Minted:** 2026-07-26 (CLI, from Grok read-only CoC systems review, relayed by owner)
**Lane:** Raid V1 UX (single lane, no sim). Sequenced AFTER WO-771.9 integration + barracks-catalog-structure land (this lane touches the troop spawn/deploy path — do not run concurrently with 771.9 spawn-wiring).
**Program hub:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` (CoC invasion P0).
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

## Implementation map (VERIFIED seams, read-only pass 2026-07-26 — build to THIS, not the prose above)

### Edit points (file:line, confirmed against source)
| WO item | Primary edit sites |
|---|---|
| Loadout steppers/bar (View) | `RaidDeployScreen.cs:269-305` (rows), `:307-356` (bars+CTA), `:358-368` (AutoRecommend stub) |
| Loadout state + handoff (VM) | `RaidDeployVM.cs:56` (state near `_troops`), `:131-134` (`Deploy()` — the single call that must pass the bag) |
| RaidParams handoff | `SceneRouter.cs`: new `[Serializable] RaidParams` after `:115`, static `PendingRaid` field near `:658`, `GoRaid` overload at `:456` — **mirror `PatriciaLightParams`/`GoPatriciaLight` (`:107-115`,`:651-655`) and `BattleParams`/`GoBattle` (`:49-78`,`:567-578`) verbatim** |
| Tray arms loadout only | `RaidDeployController.cs:526` (`BuildTrayTiles`), `:304` (`NextDeployableOfType`), `:334` (`RemainingOfType`) — **all three re-derive from `army.Owned`+`IsDeployable`; gate ALL THREE or the player over-deploys past the loadout** |
| Deploy-ring reject + ghost | `RaidDeployController.cs:267-302` (`HandleDeployTap`; gate AFTER raycast hit `:269`, BEFORE `SpawnFromArmy` `:290` + `RecordDeploy` `:299`); ring marker added in `RaidBaseGenerator.cs:182-225` |
| "Defenders" label | `RaidHudController.cs:154` (init), `:223` (live `"Razed "+pct+"%"`) |
| Victory copy | `EndStateVM.cs:245` ("The base is CLAIMED…"), `:247` ("% razed") |
| Train queue UI | swap `TroopTrainingPanel.cs:498-525` + `TroopTrainingVM.cs:198-249` (instant `ArmyStorage.TrainNow`) to the queued `BarracksService.EnqueueTraining` (`BarracksService.cs:188-222`); read via `BuildTimerService.ActiveJobsOf/PendingJobsOf` (as `ObsidianQueueHud.cs:57` already does for the `Train` channel) |
| Star math | `RaidScoring.cs:146-155` — logic UNCHANGED for V1; copy only |

### CORRECTIONS to the scope above (source beats the review prose)
- **Copy is "Razed"/"CLAIMED", NOT "destroyed".** `DestructionPct` (`RaidScoring.cs:92-102`) already = garrison kills. The readout says **"Razed %"** (`RaidHudController.cs:223`) and victory says **"base is CLAIMED … % razed"** (`EndStateVM.cs:245-247`). "DESTROYED" exists only on the unrelated village-defense path (`EndStateVM.cs:339-343`). → **RaidCopyRegression asserts: defender readout shows "Defenders" and contains NO "razed"/"base %"; and no screen labels BOTH the modal and the tray "Deploy."** (Do not hunt for "base destroyed" — it isn't there.)
- **Only 3 raid bases are baked** (`raider_camp_small`, `fortified_garrison`, `mage_enclave` — `RaidBaseGenerator.cs:121-130`); `IronBastion` is a separate menu-only build. Deploy-ring marker must be added to all baked bases via `BuildAllRaidScenes` (NOT hand-edited scenes, §3).
- **Selection header is already "RAIDS"** (`RaidSelectionScreen.cs:114`) — no rename needed. Rename targets: pre-raid modal header "RAID: <name>" (`RaidDeployScreen.cs:111`) + its DEPLOY CTA (`:351`) → "Army/War Band"; keep the in-raid tray as "Deploy."

### FOOTGUNS (must-honor)
1. **Two training paths — REPLACE, don't add.** Instant `ArmyStorage.TrainNow` (`TroopTrainingVM.cs:244`, live in UI) vs queued `BarracksService.EnqueueTraining` (`:188`, unwired). Both spend resources AND add to the army. Wiring the panel to the queue must REPLACE the `TrainNow` call, never run both.
2. **Two Barracks panels — target `TroopTrainingPanel` ("Barracks - Train"), NOT `BarracksPanel` ("Barracks - Upgrade").**
3. **Loadout is per-TYPE counts but deploy consumes per-INSTANCE `PlayerTroop`** (`TroopDeployer.cs:78` stamps `OwnedTroopId`; retreat reconciles by id via `ArmyStorage.ReconcileAfterRaid`). Enforce the loadout as a per-type DEPLOY CAP against real owned instances — do NOT invent a new PlayerTroop selection, or wounded/reconcile accounting mismatches.
4. **Gate all three tray scan sites** (footgun-tray, see table) — `RaidLoadoutRegression` asserts the DEPLOY BUDGET, not just tile presence.
5. **`PendingRaid` must be set BEFORE `GoRaid`** — `RaidDeployController` self-installs on scene load (`RuntimeInitializeOnLoadMethod` `:114`) and reads `GameState.Army` directly (`:627-631`); the bag must arrive via `PendingRaid` read in `Start()`→`BuildTrayTiles()`.
6. **Deploy-ring reject returns before `SpawnFromArmy`/`RecordDeploy`** so a rejected drop doesn't inflate the deployed count or decrement the army.
7. **Leave the walk path unfed** — teleport + walk-to-outpost share the single `ff.raidwalk` flag (default OFF, confirmed `FeatureFlags.cs:88`). Edit only the deploy loop; do not spawn walk outposts (`RaidOutpostSystem`, `RaidEntryBridge.cs:144`).

## Full ladder recorded (from the review — for sequencing, NOT all this WO)
- **P0 (this WO):** loadout+naming, deploy ring, victory/star copy, Train-queue UI.
- **P1 (next raid WOs):** scout stub + Auto Recommend recipes; ghost preview + drop VFX/SFX; 2× speed toggle; one perfect Footman + one Archer silhouette (art lane — depends on Lane B pack tooling + KayKit Phase 2); star thresholds tied to boss/gate not only full clear.
- **P1.5 = WO-771.6 stakes:** casualties + stars + soft loot (PAIN_POINTS F1).
- **P2 / V1.5:** breach-expands-deploy-zone; structure % destruction; favorite army presets; post-raid shields; **barracks as a real upgradable catalog building** (that's the F3 follow-on this session already queued).

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `ArmyLoadoutService.cs:20; RaidDeployController.cs:28` — deploy-ring deliberately reversed. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
