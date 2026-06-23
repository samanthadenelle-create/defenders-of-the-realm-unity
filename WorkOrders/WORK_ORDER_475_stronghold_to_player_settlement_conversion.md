# WORK ORDER 460 — Stronghold → Player Settlement Conversion (Village2 post-clear)

**Status:** READY TO IMPLEMENT (spec complete)
**Classification:** NEW FEATURE (capability does not exist — see "What exists today")
**Silo:** Combat/AI + World/Environment (code-only; one new runtime component + one persistence field). No `.unity` hand-edits.
**Source:** F8 ticket (owner, Village2): *"after getting here and destroying the enemy stronghold how do you convert to a player settlement?"*
**Lane:** Camps/Raid (`Assets/_Modules/Village/World/Camps/`) — file-disjoint from VillageSceneBuilder.

---

## 1. Problem

The core loop's payoff beat is incomplete. When the player clears the Village2 enemy
stronghold, **the win is recorded but nothing visibly converts the enemy base into a
player settlement.** There is no way to "make it yours" beyond a banner + a flag.

### What exists today (verified from code, not comments)

- **`Village2RaidController.cs`** — on `GarrisonController.OnCleared`:
  1. plays Victory music,
  2. `ClaimBase()` → `RaidClaimService.MarkClaimed("Village2")` (PlayerPrefs `dotr-raid-owner-Village2 = "1"`) **+** `SceneOwnership.SetEnemyOwned(false)` (runtime flag flip only),
  3. unlocks the next companion,
  4. shows a "STRONGHOLD CLEARED" banner and **routes the player back to the castle** (`SceneRouter.GoCastle`).
- **`RaidClaimService.cs`** — persists the claimed-set in PlayerPrefs only. Its own header notes: *"A later WO can fold the set into SaveSchema v24 (OwnedOutposts) for cloud sync."*
- **`SceneOwnership.cs`** — `IsEnemyOwned` is a runtime-only bool resolved from `scene-configs.json` on every load. **The claim's `SetEnemyOwned(false)` is NOT persisted**, so on the next load of Village2 the JSON re-resolves it back to **Enemy-owned** (Village2's config ownership is Enemy). The claim does not survive a reload of the scene itself.

### The four gaps (this is why it's a NEW feature, not a bug)

1. **No visible conversion.** The enemy stronghold geometry (`StrongholdRoot/Environment`: walls, `MainGate`, `Platform_Keep`, watchtowers, traps, banners, bones, rubble) stays exactly as the enemy left it. Nothing swaps enemy dressing → player dressing, removes hostile props (traps, bones, enemy banners), or plants any player-owned content.
2. **No player economy/buildings enabled.** `ClaimableCamp` (the proven outer-world clear→claim→build→harvest loop) plants courtyard `MineNode` harvest faucets + an `OutpostHub` when a camp is claimed — **none of that fires for Village2.** A claimed stronghold yields zero ongoing benefit.
3. **No persisted ownership of the settlement.** The runtime ownership flip is lost on reload (gap above). There is no `OwnedOutposts`/settlement record keyed to `Village2` in `SaveSchema`, so "this is my settlement now" does not survive a session.
4. **No re-entry as a player settlement.** Re-entering Village2 after claiming re-spawns the garrison (`SceneOwnership` reads Enemy again → death-retreat + build-mode-blocked + turrets arm against the player). The owner's question — *"how do you convert to a player settlement?"* — has no flow.

> **Owner's design intent (from memory `world-architecture-gated-regions-playable-connectors`):**
> *"Clear base → claim → gain companion/gear → stronger → next ring out."* The CLAIM step is
> meant to convert the base into a held, beneficial, persistent player asset — the closing of
> the loop. That conversion is the missing capability.

---

## 2. Design — `StrongholdConversionService` (the conversion verb)

Add ONE new runtime component that runs **after** the existing claim flip, reusing the
proven `ClaimableCamp` / settlement systems rather than greenfielding. The owner is
hand-authoring Village2 + offsets soon, so this must be **layout-agnostic** — it reads
the live `StrongholdRoot` and works off named markers, never hard-coded positions.

### 2.1 Hook point (reuse, don't fork)

`Village2RaidController.ClaimBase()` already runs at the moment of victory. Extend the
claim step to invoke the new conversion service **after** `RaidClaimService.MarkClaimed`
returns a NEW claim (so a re-clear never re-converts). Do NOT add a second OnCleared
subscriber — chain off the existing one to keep a single victory path.

```
HandleCleared → ClaimBase()
    → RaidClaimService.MarkClaimed("Village2")   (existing)
    → SceneOwnership.SetEnemyOwned(false)        (existing, runtime)
    → NEW: persist ownership (see 2.3)
    → NEW: StrongholdConversionService.Convert(strongholdRoot)   (only on newClaim)
```

### 2.2 What `Convert(Transform strongholdRoot)` does (the visible conversion)

Phased, each step `FlowTrace`-instrumented (system `"Convert"`) and `Guard`-wrapped so one
bad piece logs + is skipped, never aborting the conversion (CLAUDE.md §12):

1. **Strip hostile dressing.** Find + destroy (or hide) the enemy-only props under
   `StrongholdRoot/Props` and `StrongholdRoot/Traps`: objects named `Trap_*`, `*_bones`,
   `Skull_*`, enemy `Flag_*`/`banner` instances. Keep structural geometry (walls, gates,
   towers, platforms, floor) — the player inherits the fortress.
2. **Re-flag the watchtower turrets friendly.** `GarrisonController.ArmGarrisonTurrets`
   armed `Watchtower_*` as `EnemyOwned DefenseTower` (via `GarrisonTurretArmer`). On
   conversion, **disarm/destroy those enemy `DefenseTower` components** (they are guarded by
   `SceneOwnership.IsEnemyOwned`, now false, but the live components persist for the session).
   Optionally re-arm them as PLAYER-owned towers (defends the settlement) — flagged optional
   below.
3. **Plant the player economy (reuse `ClaimableCamp`'s proven path).** Plant the same
   courtyard harvest faucets `ClaimableCamp.SpawnHarvestNodes` uses — one renewable
   `MineNode` per `{Wood, Iron, Food, AetherCrystal}` at static local offsets in the keep
   courtyard (centre derived from the live `Platform_Keep` bounds, NOT hard-coded). These are
   the "player economy/buildings enabled" the ticket asks for. **Do not duplicate** the node
   spawn code — extract `ClaimableCamp.SpawnHarvestNodes`'s body into a shared static helper
   (`CampHarvestNodes.Plant(Transform root, Vector3 courtyardCentre, RegionId region)`) and
   call it from both. (If extraction is too invasive this sprint, replicate the 4-node pattern
   in the new service and file a follow-up to dedupe — flag it.)
4. **Enable player building.** With ownership persisted player-owned (2.3), `BuildModeController.Enter`
   no longer gates off (it checks `SceneOwnership.IsEnemyOwned`). No code needed beyond the
   ownership flip persisting — verify the gate reads the persisted value on re-entry.
5. **Friendly tell.** Swap the moody enemy banners for a player banner at the `MainGate`
   (reuse the catalog `banner`/`Flag_Medieval` role; pack-agnostic — `LogWarning` + skip if
   absent). Lightweight; the conversion must degrade gracefully on a pack-less clone.

### 2.3 Persistence (the missing "survives reload" piece)

The runtime `SetEnemyOwned(false)` is lost on reload. Two correct options — **pick A**
(local-first, lowest risk, matches the existing convention); B is the cloud-sync upgrade.

- **A (DO THIS): keep PlayerPrefs as source of truth + make `SceneOwnership` honour it.**
  `SceneOwnership.Resolve(sceneName)` currently reads ownership from `scene-configs.json`
  only. Add: after the JSON resolve, **if `RaidClaimService.IsClaimed(sceneName)` is true,
  force `IsEnemyOwned = false`** (a claimed scene is player-owned regardless of its config
  default). This makes the existing `dotr-raid-owner-Village2` PlayerPref the persistent
  ownership record — zero schema change, and it fixes re-entry (no garrison re-spawn against
  a claimed base; build mode allowed; death no longer retreats).
  - **Gate the garrison re-spawn too:** `Village2RaidController.BindRoutine` must **not**
    `Activate()` the garrison if `RaidClaimService.IsClaimed("Village2")` — a claimed
    stronghold loads peaceful + converted, not re-populated. Restore the converted state
    (re-plant harvest nodes, strip hostile dressing) on a claimed re-entry instead.

- **B (OPTIONAL, follow-up): fold into `SaveSchema`.** Add `OwnedOutposts : List<string>`
  (or reuse `Settlements : List<SettlementState>` with a `Village2` site record) to
  `PersistedState`, bump `CurrentVersion` 24→25 with a `SaveMigrator` no-op default `[]`,
  round-trip in `GameStateRoundtripTests`. Do this only if cloud sync of claimed strongholds
  is needed; A is sufficient to close the ticket.

### 2.4 Self-install / scope

- Live in `Assets/_Modules/Village/World/Camps/StrongholdConversionService.cs`
  (assembly `DeNelle.Village`, namespace `DeNelle.Village.World.Camps`) — same lane as
  `RaidClaimService` / `Village2RaidController`.
- It is a **static service** (`Convert(...)`, `RestoreConverted(...)`) called by
  `Village2RaidController`, not a self-installing MonoBehaviour (one victory path; no new
  scene hook to avoid double-fire).
- Code-built only. ASCII runtime strings. Canon: Elarion (never Avalon). `LogWarning`
  (never error) on a missing pack prefab.

---

## 3. Files

| File | Change |
|---|---|
| `Assets/_Modules/Village/World/Camps/StrongholdConversionService.cs` | **NEW** — `Convert(strongholdRoot)` + `RestoreConverted(strongholdRoot)` (strip hostile dressing, disarm enemy turrets, plant harvest economy, friendly banner). FlowTrace `"Convert"` + Guard per step. |
| `Assets/_Modules/Village/World/Camps/Village2RaidController.cs` | EDIT — in `ClaimBase()`, after `MarkClaimed` returns newClaim, call `StrongholdConversionService.Convert(strongholdRoot)`. In `BindRoutine()`, skip `Activate()` + run `RestoreConverted` when `RaidClaimService.IsClaimed("Village2")`. |
| `Assets/_Modules/Village/SceneOwnership.cs` | EDIT — in `Resolve()`, force `IsEnemyOwned=false` when `RaidClaimService.IsClaimed(sceneName)` (persists the claim across reloads). |
| `Assets/_Modules/Village/World/Camps/ClaimableCamp.cs` | EDIT (optional, preferred) — extract `SpawnHarvestNodes` body into a shared `CampHarvestNodes.Plant(...)` helper so the conversion reuses the exact node-spawn path (no duplicate economy). |
| `Assets/_Modules/Village/World/Camps/RaidVictoryController.cs` | EDIT (optional) — apply the same `Convert` call so `RaidBase_*` strongholds convert identically (the conversion is generic; do this once the Village2 path is proven). |

---

## 4. Data / Persistence

- **Primary (option A):** no schema change. `dotr-raid-owner-<sceneName>` (existing
  `RaidClaimService` PlayerPref) becomes the durable ownership record; `SceneOwnership`
  reads it. Harvest nodes re-plant deterministically on each claimed re-entry (the same
  "effectively persistent / seamed into the world" pattern `ClaimableCamp.SpawnHarvestNodes`
  already uses — see its header).
- **Optional (option B):** `SaveSchema` v24→v25 `OwnedOutposts`/`Settlements` record, with a
  `SaveMigrator` default + `GameStateRoundtripTests` coverage.

---

## 5. Acceptance Criteria

1. Clearing the Village2 garrison **visibly converts** the base: enemy traps/bones/enemy
   banners are gone, watchtowers no longer fire on the player, the courtyard has the 4
   renewable harvest nodes, and a friendly banner stands at the gate (or `LogWarning` if
   the pack prefab is absent — never an empty silent skip).
2. The conversion fires **once per claim** (a re-clear / re-entry never re-converts or
   double-plants — idempotent, guarded by `RaidClaimService.IsClaimed`).
3. **Re-entering Village2 after claiming** loads it **peaceful + player-owned**: no garrison
   re-spawns, build mode is permitted, hero death does NOT retreat-as-enemy-territory, and
   the harvest nodes are present. Proven across a scene reload.
4. The harvest nodes bank into the wallet via the **existing** `MineNode`/economy path (no
   parallel economy).
5. Headless-verifiable: FlowTrace `[Flow:Convert]` lines show strip / disarm / plant / banner
   steps; the AutoPilot Village2 phase clears → converts → re-enters claimed without a
   garrison. (Per §12: instrument FIRST — land the `"Convert"` traces, run the fleet, read the
   trace to confirm each step ran, THEN tune.)
6. Brace-balance gate passes on every edited `.cs`; `COMPILE_GATE_OK`.

---

## 6. What NOT to touch

- **Do NOT hand-edit `Village2.unity`** (corruption history; §3). The owner is hand-authoring
  Village2 + offsets — the conversion must be **runtime + layout-agnostic** (read live
  `StrongholdRoot` markers, never bake into the scene file).
- **Do NOT greenfield a new economy or settlement system.** Reuse `ClaimableCamp`'s harvest
  nodes + `MineNode` + `OutpostHub` + the `SceneOwnership` gate.
- **Do NOT add a second `OnCleared` subscriber** — chain off `Village2RaidController`'s
  existing victory path (two victory paths = double-grant / double-convert).
- **Do NOT bump `SaveSchema` unless doing option B** (and if so, with a migrator + round-trip
  test — never an unmigrated shape change).
- **Do NOT remove structural geometry** (walls/gates/towers/platforms/floor) — the player
  inherits the fortress; only hostile *dressing* is stripped.
- No `System.Reflection` in runtime bridge scripts; `?.` on all cross-module service calls.

---

## 7. Instrument-First Plan (CLAUDE.md §12 — BINDING)

1. Land `StrongholdConversionService` with `FlowTrace.Enter/Step/Warn/Fail` (system
   `"Convert"`) at: strip-dressing (count removed), disarm-turrets (count), plant-economy
   (nodes planted N/4), banner (placed/skipped), and `RestoreConverted` on re-entry.
2. Add a `Guard.Try`/`Guard.TryEach` around each destructive + spawn op.
3. Run the **headless** AutoPilot Village2 phase: enter → clear → read `[Flow:Convert]` to
   confirm every step ran and the node count is 4. Then reload Village2 → confirm
   `[Flow:World] SceneOwnership resolved 'Village2' -> Player-owned` (the new claimed-override
   path) and **no** `Village2 garrison ACTIVATED` line.
4. Only after the trace proves the flow, tune offsets/visuals. No inference-fixing.

---

## 8. Notes for the implementer

- Courtyard centre for node placement: derive from the live `Platform_Keep` renderer bounds
  (`StrongholdRoot/Environment/Platform_Keep`) so it tracks the owner's authored offsets —
  do not reuse `EnemyStrongholdBuilder`'s hard-coded `keepHalf*0.4` (that's build-time only).
- `RegionId` for the nodes: resolve via `ZoneManager.GetZone(strongholdRoot.position)` (same
  as `Settlement.Region`), so the conversion is region-correct when Village2 is offset into a
  region.
- The existing `Village2RaidController` already saves on claim (`GameStateService.Save()`),
  so option A needs no extra save call — the PlayerPref write in `RaidClaimService.MarkClaimed`
  is already durable.
