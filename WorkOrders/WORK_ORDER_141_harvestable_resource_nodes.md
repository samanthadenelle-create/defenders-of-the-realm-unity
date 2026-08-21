<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 141 — Harvestable Resource Nodes (the in-world extractable that feeds the build economy)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-30 (Fri)
**Priority:** High — the *node* half of the HARVEST pillar. Owner ask: "harvestable nodes for building and extracting resources." This WO is the **player-extractable node** layer that the worker/pet/offline systems plug into; it makes "walk up, hold to extract, watch the wallet climb" playable on its own — before workers (WO-117 Ph2+) or pets (WO-119) exist.
**Scope:** Medium, self-contained, additive. ONE shared enum (or consume WO-117's), one SO, one runtime MonoBehaviour, one code-built world prompt, and the GameState bank-write. No new currency, no new assembly, no UXML, no scene hand-edit, no bake.
**Lane:** **design (owner + UI) · gameplay code (CLI)** — runtime-spawned or builder-placed later; **NOT the frozen `VillageSceneBuilder` for this WO.**
**Depends on:** none hard. **Shared-file dependency:** `Assets/_Modules/Core/ResourceType.cs` (the enum) — **owned by WO-117**; this WO consumes it and creates it only if WO-117 hasn't landed (see §Ordering).
**Soft-ties (provide seams, do NOT implement):** WO-117 (worker dispatch — auto-extracts the same node), WO-119 (pet auto-harvest — boosts the same node), WO-115 (offline accrual — reads the node's rate/store seam), WO-124 (Resource HUD — displays the four wallets as they tick), WO-122 (`CrystalMine` — the Crystal-type node reconciles with the existing passive mine), WO-108 (player build mode) + WO-137 (catalog) as the **downstream sinks** the harvest pays for.
**North Star:** `docs/NORTH_STAR.md` core loop **BUILD → HARVEST → DEFEND → OFFLINE**; line 56 *"HARVEST resource nodes (Warcraft gold mine / crystal — auto-harvest)"*; line 310 *"Harvest nodes — generalize `CrystalMine` → destructible auto-harvest mines you defend"*.

---

## Goal

A **`HarvestNode`** — a placed world object the **player** can walk up to and extract a resource from (proximity + `[F]`/tap, matching the existing village interact pattern), with **yield**, **regen/cooldown**, **depletion**, and **respawn**. The same node exposes read-only seams so **workers (WO-117)**, **pets (WO-119)** and **offline accrual (WO-115)** can later auto-extract it **without re-implementing the node**. Banked resources flow into the **existing `GameState` wallet** (no net-new currency), which the **build mode / catalog (WO-108/137)** already spends.

This is the **manual / direct-extract** sibling of WO-117's worker-dispatch model. Both target the same node data and the same wallet — they are two extraction *triggers* over one node model, not two parallel systems.

---

## Reconciliation — what already exists (read before writing; build ON TOP, never duplicate)

I read `GameState.cs`, `NestedTypes.cs` (ResourceBalance), `CrystalMine.cs`, `NORTH_STAR.md`, `docs/polyperfect-asset-catalog.md`, WO-111/115/117/122/124, and CLAUDE.md §5/§6 before writing this. **Confirmed by inspection:**

| Need | Exists? | Where / note |
|---|---|---|
| Resource wallet (all 4 payouts) | **BUILT — no new currency** | `Assets/_Modules/Core/State/GameState.cs`: `AetherCrystals` (L52), `Stone` (L54), `Iron` (L56), `Wood` (L58); `Resources` is a `ResourceBalance` (`Assets/_Modules/Core/State/NestedTypes.cs` L41–45: `Crystals`, `Food`, `Coins`). Every node type banks to one of these. |
| Award-to-economy seam (Core can't ref Village) | **BUILT** | Village writes `GameStateService.Instance.State.<field>` directly; Crystal additionally has `CrystalEconomy.AddCrystals(n)` (`CrystalMine.cs` L154–160). See memory *core-cannot-reference-village-award-crystals-via-gamestate*. |
| **Proximity + `[F]` interact pattern** | **BUILT — mirror this exactly** | `Assets/_Modules/Village/Buildings/CrystalMine.cs`: `_isInRange` range check (L135–143), `Input.GetKeyDown(KeyCode.F)` (L119/143), world-space prompt bubble `BuildBubble(...)` / `ShowPrompt()` / `_promptGo` (L341–455), `_promptHeight` (L59). **The HarvestNode reuses this interact + world-prompt shape — do NOT invent a new prompt UI.** |
| `[CreateAssetMenu]` SO data pattern | **BUILT (WO-86)** | `Assets/Data/EnemyData.cs`, `WaveData.cs`. Follow it for `HarvestNodeData`. |
| `ResourceType` enum (Core) | **greenfield / owned by WO-117** | No `Assets/_Modules/Core/ResourceType.cs` exists yet (confirmed). WO-117 §2a defines `enum ResourceType { Wood, Food, Crystal, Ore }`. **This WO consumes it; creates it only if WO-117 hasn't landed (§Ordering).** |
| Worker / `ResourceNode` / `HarvestService` (WO-117) | **greenfield — NOT yet built** | Confirmed: no `Assets/_Modules/Village/Harvest/*.cs` exists. WO-117 specs a `ResourceNode` MonoBehaviour. **This WO and WO-117 must converge on ONE node component — see §Reconcile-with-WO-117 (critical).** |
| Crystal node | **BUILT (passive) — reconcile** | `CrystalMine.cs` already yields Aether Crystals + has `_useExternalVisual` (WO-122). The Crystal-type `HarvestNode` does **not** replace it yet — see §Crystal reconciliation. |
| Resource HUD (4 wallets tick up) | **spec'd (WO-124)** | Pushes via `CoreServices.Hud?.SetResource(ResourceType, int)`. This WO calls that seam after a bank so the player SEES it climb. |
| Code-built world UI (no UXML) | **BUILT precedent** | `CrystalMine.InjectUpgradePanel()` / `BuildBubble()` build `VisualElement`/world-space UI in C#. PIPELINE_STATE.md §8: **UXML does not render in builds.** |
| Candidate node meshes | **catalog'd** | `docs/polyperfect-asset-catalog.md`: `Tree_Oak`/`Tree_Dead_Log_A/B` (Wood), `Rock_Large`/`Stone_Big`/`Rock_Pillar` (Ore/Stone), `Well`/`Timber` (camp dressing), plus the `Art/Crystals` mesh (Crystal). Pack is gitignored (CLAUDE.md §4) → `LogWarning` + primitive stub on miss. |

**So the new work is: ONE node model (yield/regen/deplete/respawn) + the PLAYER manual-extract trigger on top of the existing interact pattern + the integration seams for worker/pet/offline. No new currency, no new assembly, no scene edit, no bake.**

---

## CRITICAL — Reconcile with WO-117 (do NOT ship two node components)

WO-117 §2c specs a `ResourceNode` MonoBehaviour with `CurrentStore`, `FillPercent`, `IsClaimed`, `RatePerSecond`, `AccrueTick(dt)`, `Bank()`, `SetClaimed`, `SetBoost`. **That is the same in-world object this WO calls a "harvest node."** There must be exactly **one** runtime component.

**Resolution (decide before coding):**
- **The canonical runtime component is `ResourceNode`** (WO-117's name — it is the broader pillar). This WO does **not** introduce a separately-named `HarvestNode` class; "HarvestNode" is the *design concept*, the implemented type is **`ResourceNode`**.
- **This WO owns the node's MANUAL-EXTRACT path + depletion/regen/respawn**; WO-117 owns the WORKER auto-extract path. They share the same `ResourceNode` + `HarvestNodeData` SO. Whichever lands first **creates** `ResourceNode.cs` / the SO; the second **extends** it additively (the project's #1 rule — reconcile, don't replace; memory *wo-batch-reconcile-not-replace*).
- If WO-117 has **not** landed when this is built: this WO creates `ResourceNode.cs` with the manual-extract + regen/deplete model below, leaving WO-117's `CurrentStore`/`AccrueTick`/`Bank`/worker seams as the documented extension points (stub the read-only seams so WO-115 can no-op against them).
- If WO-117 **has** landed: this WO **edits** `ResourceNode.cs` to add the manual player-extract trigger + the regen/depletion/respawn fields, reusing its `CurrentStore`/`Bank()`.

> **Flag for owner/CLI:** the two WOs were written from different angles (WO-117 = "send a worker, fills a store"; WO-141 = "walk up, extract a yield"). They must converge on one `ResourceNode`. Recommended unifying model: a node has both a **manual pull** (player tap → grant `yieldPerExtract`, start cooldown) **and** a **continuous store** (worker/pet fills `CurrentStore` over time → banked on return). Same node, two faucets. Keep `Bank()` as the single haul-out used by both.

---

## 1. Resource types — all map to EXISTING `GameState` fields (none net-new)

| Node concept | `ResourceType` | Banks to (existing field) | New currency? |
|---|---|---|---|
| **Wood** (logging — `Tree_*` / `Tree_Dead_Log`) | `Wood` | `GameState.Wood` | **No — L58** |
| **Stone / Ore** (the Warcraft "gold mine" — `Rock_Large`/`Stone_Big`) | `Ore` | `GameState.Stone` | **No — L54** |
| **Crystal** (the `Art/Crystals` mesh) | `Crystal` | `GameState.AetherCrystals` (established `CrystalMine` target) | **No — L52** |
| **Food** (farm/forage — `Well`/`Hay_Pile` dressing) | `Food` | `GameState.Resources.Food` | **No — NestedTypes L44** |
| *(reserve)* **Iron** (rare) | *(future 5th)* | `GameState.Iron` (L56) — exists if a rare tier is added | No |

> **Owner decision points:** (1) Crystal banks to `AetherCrystals` (recommend — matches `CrystalMine`). (2) `ResourceType` is **Wood/Food/Crystal/Ore** per WO-117 §2a — keep that exact set so HUD (WO-124) and worker (WO-117) agree; Iron is a documented future tier, **not** added to the enum now (avoid interface churn).

---

## 2. Data model — DESIGN ONLY (illustrative shape; CLI writes the real code)

Assembly discipline (CLAUDE.md §5): **enum + pure data in `DeNelle.Core`**; **MonoBehaviour + SO in `DeNelle.Village`**. Village → Core only. Core can NOT reference Village (memory *core-cannot-reference-village...*).

### 2a. `ResourceType` enum — `DeNelle.Core` (shared, owned by WO-117)

```csharp
// Assets/_Modules/Core/ResourceType.cs  — DO NOT redefine if WO-117 already created it.
namespace DeNelle.Core
{
    /// <summary>Harvestable node kinds. Each maps 1:1 to an existing GameState wallet field (§1).</summary>
    public enum ResourceType { Wood, Food, Crystal, Ore }   // Ore banks to GameState.Stone
}
```

### 2b. `HarvestNodeData` — ScriptableObject (authoring entry) — `DeNelle.Village`

The catalog/authoring definition of one node. **Superset of WO-117 §2b `ResourceNodeData`** — if that file exists, extend it with the regen/deplete/respawn fields rather than creating a new SO. (Owner/CLI: pick ONE SO name; recommend keeping WO-117's `ResourceNodeData` if it landed first, else name it `HarvestNodeData` and have WO-117 extend it.)

```csharp
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Village
{
    [CreateAssetMenu(menuName = "Defenders/Harvest Node", fileName = "Node_")]
    public sealed class HarvestNodeData : ScriptableObject   // == WO-117 ResourceNodeData (reconcile to ONE)
    {
        public string       id;            // stable save key, e.g. "node_wood_east"
        public ResourceType resourceType;  // §1 — which wallet it banks to
        [TextArea] public string displayName = "Wood";  // for the [F] prompt label

        [Header("Manual extract (this WO — player pull)")]
        public int   yieldPerExtract   = 5;     // units granted per player [F]/tap
        public float extractCooldown   = 1.0f;  // seconds between manual pulls (anti-spam)

        [Header("Depletion / respawn (this WO)")]
        public int   totalDeposit      = 100;   // total units the node holds before depleted (-1 = infinite/regen-only)
        public bool  depletes          = true;  // false → infinite node (e.g. tutorial)
        public float respawnSeconds    = 60f;   // depleted node regrows after this (0 = never; manual rebuild)
        public float regenPerSecond    = 0f;    // optional slow self-refill of the deposit while not depleted (0 = off)

        [Header("Auto-extract store (WO-117 worker/pet/offline) — shared seam")]
        public float baseRatePerSecond = 1f;    // continuous fill rate while a worker/pet is on station
        public int   storeCap          = 200;   // continuous-store cap (worker model)

        [Header("Placement (NOT VillageSceneBuilder for this WO — §7)")]
        public Vector3 worldPosition;           // runtime-spawn position (or builder-placed later)
        public string  prefabKey;               // poly/Art mesh to instance (LogWarning + stub if missing)
    }
}
```

### 2c. `ResourceNode` — runtime MonoBehaviour (the in-world node) — `DeNelle.Village`

THE single canonical node component (== WO-117 §2c). This WO adds the **manual-extract + deplete/regen/respawn** members; WO-117 adds the worker store/`AccrueTick` members. Both share `Bank()` and the read-only seams.

```csharp
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Village
{
    public enum NodeState { Available, OnCooldown, Depleted, Respawning }

    /// <summary>One on-map harvestable node. Players manually extract via proximity+[F] (this WO);
    /// workers/pets auto-extract over time (WO-117/119); offline accrual reads its rate (WO-115).</summary>
    public sealed class ResourceNode : MonoBehaviour
    {
        public HarvestNodeData Data;

        // ── Depletion / state (this WO) ──
        public NodeState State { get; private set; } = NodeState.Available;
        public int  RemainingDeposit { get; private set; }   // counts down as units leave the node
        public float DepositPercent => Data == null || Data.totalDeposit <= 0 ? 1f
                                       : Mathf.Clamp01((float)RemainingDeposit / Data.totalDeposit);

        // ── Read-only seams for WO-115 offline / WO-117 worker / WO-124 HUD (do NOT duplicate elsewhere) ──
        public bool  IsClaimed   { get; private set; }        // WO-112 ward-tether flips this
        public float CurrentStore{ get; private set; }        // WO-117 continuous worker store
        public float RatePerSecond => IsCollecting ? Data.baseRatePerSecond * _boost : 0f; // WO-115 reads
        bool  IsCollecting => /* WO-117: a worker/pet is on station */ false;
        float _boost = 1f;

        // ── Manual extract (THIS WO — the player verb) ──
        float _cooldownUntil;
        public bool CanExtractNow => State == NodeState.Available && Time.time >= _cooldownUntil
                                     && (!Data.depletes || RemainingDeposit > 0);

        /// <summary>Player pulled the node. Returns units granted (0 if not extractable).
        /// Caller (or this method) writes the matching GameState field + pings HUD/Audio.</summary>
        public int Extract()
        {
            if (!CanExtractNow) return 0;
            int amt = Data.depletes ? Mathf.Min(Data.yieldPerExtract, RemainingDeposit) : Data.yieldPerExtract;
            if (Data.depletes) RemainingDeposit -= amt;
            _cooldownUntil = Time.time + Data.extractCooldown;
            State = NodeState.OnCooldown;
            if (Data.depletes && RemainingDeposit <= 0) BeginDepleted();   // → Depleted → respawn timer
            return amt;
        }

        // Update(): OnCooldown → Available when Time.time >= _cooldownUntil; Depleted → Respawning after
        // respawnSeconds → refill RemainingDeposit, State = Available; optional regenPerSecond top-up.

        public int Bank() { int h = Mathf.FloorToInt(CurrentStore); CurrentStore -= h; return h; } // WO-117 shared haul-out
        public void SetClaimed(bool c) => IsClaimed = c;   // WO-112 hook
        public void SetBoost(float m)  => _boost = Mathf.Max(1f, m);  // WO-119 pet hook
        void BeginDepleted() { /* State=Depleted; start respawnSeconds timer; dim visual */ }
    }
}
```

### 2d. Manual-extract trigger — reuse the `CrystalMine` interact pattern (this WO's playable core)

The player verb mirrors `CrystalMine.cs` **exactly** — do NOT invent a new prompt:
- A **proximity range check** (`_isInRange` against the `Player`/`HeroTarget`, like `CrystalMine` L135–143).
- A **world-space prompt bubble** built in **code** (`BuildBubble(...)`/`ShowPrompt()` shape, `_promptHeight`, no UXML), e.g. `"〔 F 〕 Harvest Wood (87 left)"`, or `"Depleted — regrows in 42s"` when `State == Depleted`.
- On `Input.GetKeyDown(KeyCode.F)` **or** a tap (mobile — the project is touch-first), call `node.Extract()`. If `> 0`: write the matching `GameState` field, push HUD + audio:
  ```csharp
  GameStateService.Instance.State.Wood += amt;        // or Stone / AetherCrystals / Resources.Food per §1
  // Crystal may also route through CrystalEconomy.AddCrystals(amt) to match CrystalMine.
  CoreServices.Hud?.SetResource(ResourceType.Wood, GameStateService.Instance.State.Wood);  // WO-124 seam
  CoreServices.Audio?.PlaySfx(SfxId.HarvestExtract);   // add id if absent; ?. always (CLAUDE.md §6)
  ```
- This can live as a small interaction component on the node (mirroring how `CrystalMine` carries its own prompt), or be folded into `ResourceNode`. Keep it in `DeNelle.Village`; HUD/Audio only via `CoreServices.*?.` — never a direct `DeNelle.HUD` reference.

---

## 3. Integration seams (PROVIDE only — do NOT implement these here)

| System | Seam this WO exposes | Note |
|---|---|---|
| **Worker dispatch (WO-117)** | `ResourceNode.CurrentStore` / `Bank()` / `RatePerSecond` / `IsCollecting` | Worker fills the continuous store while on station; this WO leaves those members as the shared contract. Same component, no fork. |
| **Pet auto-harvest (WO-119)** | `ResourceNode.SetBoost(mult)` | Assigning a pet raises the rate (and/or stands in for a worker). Hook present; pet behaviour out of scope. |
| **Offline accrual (WO-115)** | read-only `RatePerSecond` + a registry of claimed/active nodes (WO-117's `HarvestService.ActiveClaimedNodes()`) | WO-115 grants `min(rate × elapsed, cap)` per node on load. This WO keeps the seam read-only + null-safe so WO-115 no-ops if absent. **Do NOT implement the offline math here.** |
| **Resource HUD (WO-124)** | `CoreServices.Hud?.SetResource(type, total)` after every bank/extract; optional `SetNodeStatus(type, DepositPercent, state)` | So the player SEES the wallet climb. Push on change only — HUD never polls. |
| **Ward-tether claim (WO-112)** | `ResourceNode.SetClaimed(bool)` | Manual extract may be allowed pre-claim (owner's call); worker auto-extract is claim-gated. Read the flag — don't re-derive ward state. |

---

## 4. How harvest feeds BUILDING (the downstream sink — closes the loop)

The banked resources are spent by the **already-planned build economy** — this WO does not build the sink, it **fills the wallet the sink reads**:
- **WO-108 player build mode** + **WO-137 catalog** place walls/towers/structures from the polyperfect palette; their **cost is paid from `GameState`** (`Wood`/`Stone`/`AetherCrystals`/`Coins`).
- **Wall tiers** (NORTH_STAR line 309: wood → stone → reinforced) are the canonical CoC-style sink — harvested Wood/Stone pays the upgrade.
- No code here writes to build mode; the contract is simply: **harvest banks into `GameState`; build mode debits `GameState`.** One wallet, two ends of the loop (NORTH_STAR BUILD → HARVEST).

---

## 5. Node types / tiers (placement = role)

Per the catalog/placement=role thesis (memory *catalog-thesis-validated-live*), the node's **role is its `resourceType` + its mesh**, set by the SO — the same `ResourceNode` component is every node type; only the data differs:

| Tier | Example nodes | Mesh (catalog) | Deposit / respawn feel |
|---|---|---|---|
| **Common** | Wood grove, Stone outcrop | `Tree_Oak`, `Rock_Large`/`Stone_Big` | large deposit, fast respawn — bread-and-butter |
| **Standard** | Food forage, Crystal vein | `Well`+`Hay_Pile`, `Art/Crystals` | medium deposit, medium respawn |
| **Rare** (future) | Iron lode | `Rock_Pillar` (darker mat) | small deposit, slow respawn, higher yield/pull — the "go fortify it" target |

Tiering is **pure data** (`totalDeposit` / `respawnSeconds` / `yieldPerExtract` on the SO) — no code branches per tier. Rare = the WO-117 §5 "defend or lose it" tension target (richer haul = juicier raid bait).

---

## 6. Art approach

- **No hand-placed nodes in `Village.unity`** (CLAUDE.md §3). For this WO, nodes **spawn at runtime** from the SO (§7) like WO-117 §7; a designer/builder lane can author placements into the scene **later** (rides a future rebake, not this WO).
- Meshes by **`prefabKey`** from the catalog (`docs/polyperfect-asset-catalog.md`): Wood→`Tree_Oak`/`Tree_Dead_Log_A`; Ore→`Rock_Large`/`Stone_Big`; Crystal→the `Art/Crystals` mesh; Food→`Well`+`Hay_Pile`. Use `_M` tier prefabs (CLAUDE.md §4).
- **Pack is gitignored** — on a missing prefab, `Debug.LogWarning` + instance a primitive stub (CLAUDE.md §4; memory *fresh-clone-missing-models*). Never error.
- Watch the **Tripo/scale displacement traps** (memory *tripo-mesh-displacement-trap*, *heart-collider-scale-trap*) if any node uses a scaled imported mesh — seat by renderer bounds, keep the interact collider on a sane-scale child.
- Crystal node may reuse `CrystalVisual.cs` (spin + pulse) for life (WO-122).

---

## 7. World placement — runtime spawn (avoid the VillageSceneBuilder bottleneck)

`VillageSceneBuilder.cs` is the single-touch serialization bottleneck (CLAUDE.md §9) and is **frozen for this WO**. Nodes spawn at runtime:
- A small spawner (or WO-117's `HarvestService` if it exists — reuse it, don't fork) instantiates a `ResourceNode` per `HarvestNodeData` asset at its `worldPosition`, seated on the baked NavMesh, instancing `prefabKey` (LogWarning + stub on miss).
- **No `VillageSceneBuilder` edit, no bake, no `Village.unity` hand-edit.** Authored placement is a later architect-lane line that rides the next rebake — not this WO.

---

## Crystal reconciliation (do NOT duplicate the crystal income path)

`CrystalMine.cs` already yields Aether Crystals passively (per-wave) and has `_useExternalVisual` (WO-122). **Do not create a second crystal income path that double-pays.** For this WO:
- Either (a) ship the Crystal-type `ResourceNode` **only** at new crystal veins distinct from the `CrystalMine` plot, or
- (b) leave Crystal out of this WO's runtime set and let `CrystalMine` remain the crystal source until the owner chooses to migrate it to the node model (WO-117 §6 / WO-122 Phase 2).
- **Recommend (b) for the first cut** — ship Wood + Stone(Ore) manual-extract nodes (cleanest, no crystal double-count), add Crystal/Food once the owner confirms the `CrystalMine`→`ResourceNode` migration. Flag for owner.

---

## Assembly placement (CLAUDE.md §5/§6)

- `ResourceType` enum → **`DeNelle.Core`** (`Assets/_Modules/Core/ResourceType.cs`) — owned by WO-117, consumed here.
- `HarvestNodeData` SO + `ResourceNode` MonoBehaviour + interact + spawner → **`DeNelle.Village`** (`Assets/_Modules/Village/Harvest/`).
- **Village → Core only.** Banking writes `GameState` fields directly (Core can't ref Village). All HUD/Audio via `CoreServices.Hud?` / `CoreServices.Audio?` with `?.`. If the node ever becomes directly raidable, it implements `IDamageableStructure` with `using DeNelle.Core.Combat;` (that's the WO-117 §5 risk layer, not this WO).
- **No `System.Reflection`** in these scripts (memory *reflection-bridge-pattern* only for the established cross-asmdef bridge — not introduced here).

---

## Files to Create / Edit

| File | Action | Note |
|---|---|---|
| `Assets/_Modules/Core/ResourceType.cs` | **Reference / create-iff-absent** | Owned by WO-117. Create ONLY if WO-117 hasn't landed; otherwise consume. Do NOT redefine. |
| `Assets/_Modules/Village/Harvest/HarvestNodeData.cs` | **Create (or extend WO-117's `ResourceNodeData`)** | `[CreateAssetMenu]` SO (WO-86 pattern) with manual-extract + deplete/regen/respawn fields. Reconcile to ONE SO with WO-117. |
| `Assets/_Modules/Village/Harvest/ResourceNode.cs` | **Create (or edit WO-117's)** | THE single node component. Adds manual `Extract()` + `NodeState` + deplete/respawn; keeps WO-117's `CurrentStore`/`Bank()`/`RatePerSecond`/`SetClaimed`/`SetBoost` seams. |
| `Assets/_Modules/Village/Harvest/HarvestNodeInteractor.cs` | **Create** | Proximity + `[F]`/tap manual-extract, code-built world prompt (mirror `CrystalMine` `BuildBubble`/`ShowPrompt`). May be folded into `ResourceNode`. |
| `Assets/_Modules/Village/Harvest/HarvestNodeSpawner.cs` | **Create (or reuse WO-117 `HarvestService`)** | Runtime-spawn nodes from SOs at `worldPosition` (LogWarning + stub on missing mesh). Do NOT fork if `HarvestService` exists. |
| `Assets/Data/HarvestNodes/Node_Wood.asset`, `Node_Stone.asset` | **Create** | First playable node SO instances (Wood + Ore→Stone; Crystal/Food deferred — see Crystal reconciliation). |
| `Assets/_Modules/Core/Audio/SfxId.cs` (or equiv) | **Edit (if needed)** | Add `HarvestExtract` (+ `NodeDepleted`/`NodeRespawn`) ids if absent. |
| `IVillageHud` / `VillageHudController` | **Reference only (WO-124 lane)** | Push via existing `CoreServices.Hud?.SetResource(...)`. Do NOT edit the HUD here — WO-124 owns it. |
| `CrystalMine.cs` | **Do NOT edit** | Crystal stays on `CrystalMine` for the first cut (avoid double-pay). |

---

## Acceptance Criteria

- [ ] `ResourceType` enum present in `DeNelle.Core` (Wood, Food, Crystal, Ore) — consumed, **not** redefined if WO-117 created it
- [ ] `HarvestNodeData` SO authorable via `[CreateAssetMenu]` (WO-86 pattern) with `yieldPerExtract`, `extractCooldown`, `totalDeposit`, `depletes`, `respawnSeconds`, `regenPerSecond`, `resourceType`, `prefabKey` — and a **Wood** + **Stone** node asset created
- [ ] Exactly **ONE** runtime node component (`ResourceNode`) — reconciled with WO-117, **no second/parallel node class**
- [ ] Node **spawns at runtime** from the SO — **no `VillageSceneBuilder` edit, no bake, no `Village.unity` hand-edit**
- [ ] Player can walk into range and see a **code-built** world prompt (no UXML), mirroring the `CrystalMine` `[F]`/bubble pattern; tap/`[F]` calls `Extract()`
- [ ] `Extract()` grants `yieldPerExtract`, **decrements the deposit**, enforces `extractCooldown`, and writes the matching **existing `GameState` field** (Wood→`Wood`, Stone→`Stone`) — verified the wallet increases
- [ ] Banked total pushes to the HUD via `CoreServices.Hud?.SetResource(...)` (with `?.`) so the player SEES it tick (WO-124 seam); a collect SFX plays via `CoreServices.Audio?.PlaySfx(...)`
- [ ] **Depletion** works: a depleting node hits `Depleted` at 0, shows a "regrows in Ns" prompt, and **respawns** after `respawnSeconds` (or stays depleted if `respawnSeconds == 0`); `depletes == false` → infinite node
- [ ] **Integration seams present + read-only/no-op-safe** (NOT implemented here): `RatePerSecond`/`CurrentStore`/`Bank()` (WO-117), `SetBoost` (WO-119), claimed flag (WO-112), rate seam consumable by WO-115
- [ ] **No new currency / no new save round-trip** — all payouts route to existing `GameState` fields
- [ ] **No crystal double-pay** — Crystal income stays on `CrystalMine` for the first cut (or distinct veins per the Crystal-reconciliation note)
- [ ] `DeNelle.Village` references **DeNelle.Core only**; HUD/Audio only via `CoreServices.*?.`; no `DeNelle.HUD` reference introduced
- [ ] No UXML/`UIDocument` source asset (PIPELINE_STATE.md §8); no `System.Reflection` introduced
- [ ] Missing poly/Art mesh → `Debug.LogWarning` + primitive stub (never error); `_M` tier prefab keys used
- [ ] **Brace balance passes on every `.cs` touched** (CLAUDE.md §1)

---

## Do NOT touch

- **Do NOT edit `VillageSceneBuilder.cs` or fire any bake/batchmode** (CLAUDE.md §3/§9; this WO's lane excludes it). Nodes spawn at runtime.
- **Do NOT hand-edit `Village.unity`** (CLAUDE.md §3).
- **Do NOT create a second node component/SO that parallels WO-117's `ResourceNode`/`ResourceNodeData`** — converge on ONE (§Reconcile-with-WO-117). Reconcile, never blind-replace (memory *wo-batch-reconcile-not-replace*).
- **Do NOT redefine `ResourceType`** — WO-117 owns the file (memory *core-namespace-shadows-unityengine-statics*: don't shadow Core types either).
- **Do NOT add a new currency or new save field** — bank to existing `GameState.Wood/Stone/AetherCrystals/Resources.Food` (Core can't ref Village — write `GameStateService.Instance.State.*` directly; memory *core-cannot-reference-village...*).
- **Do NOT double-pay crystals** — leave `CrystalMine.cs` as the crystal source for the first cut; don't add a competing Crystal income path.
- **Do NOT implement worker dispatch (WO-117), pet auto-harvest (WO-119), or offline accrual (WO-115)** here — expose seams only.
- **Do NOT edit the HUD** (`IVillageHud`/`VillageHudController`) — WO-124 owns it; just call the `CoreServices.Hud?.SetResource(...)` seam.
- **Do NOT build any UI in UXML** — code-built world prompt only (PIPELINE_STATE.md §8).
- **Do NOT introduce `System.Reflection`** in these scripts.
- Do not touch ATB, WalletService, monetization, or clan code.

---

## Ordering / dependency note

- The `ResourceType` enum (`DeNelle.Core`) is the one shared file with WO-117. **WO-117 owns creating it**; this WO consumes it (creates only if WO-117 hasn't landed).
- The `ResourceNode` component + `HarvestNodeData` SO must be ONE artifact shared with WO-117 — whichever lands first creates it; the second extends additively. Coordinate via work orders (CLAUDE.md §2).
- This WO is otherwise self-contained: the manual-extract verb + deplete/respawn + GameState bank can be built and brace-checked on their own, demoable as "walk up, harvest, watch the wallet climb" without workers/pets/offline existing.

---

🤖 Spec'd by the design lane (UI). Reconciled against `GameState.cs` (Wood L58 / Stone L54 / Iron L56 / AetherCrystals L52; `ResourceBalance.Food` NestedTypes L44 — **no net-new currency**), `CrystalMine.cs` (the proximity+`[F]`+world-bubble interact pattern at L119/135–143/341–455 — mirrored, not reinvented; its `CrystalEconomy.AddCrystals` award path), the WO-86 `[CreateAssetMenu]` SO pattern, and WO-111/115/117/119/122/124, NORTH_STAR (BUILD→HARVEST→DEFEND→OFFLINE), CLAUDE.md §5/§6 (Village→Core only, `CoreServices.*?.`), PIPELINE_STATE.md §8 (no UXML in builds). **Confirmed WO-117's harvest layer is still greenfield** (`Assets/_Modules/Village/Harvest/*` and `Assets/_Modules/Core/ResourceType.cs` do not exist yet) — hence the explicit "converge on ONE `ResourceNode`" reconciliation. Markdown work order only — no `.cs` touched, no bake fired.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `MineNode.cs:1-16, ResourceType.cs, HarvestSourceRegistry.cs` — extract node shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
