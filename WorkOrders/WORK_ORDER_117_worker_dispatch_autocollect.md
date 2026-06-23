# WORK ORDER 117 — Worker Dispatch & Auto-Collect: Send Them Out, Keep Them Safe

**Status:** READY TO IMPLEMENT — **Phase 1 is the immediate CLI target (SUNDAY deadline)**
**Date:** 2026-05-29 (Fri) — owner's **#1 priority**, minimal slice demoable by **Sunday**
**Priority:** Top — the concrete worker layer of the HARVEST pillar; the Warcraft harvest verb made playable
**Scope:** Large (phased). Phase 1 small + self-contained; Phases 2–3 build on it.
**Lanes:** design (owner + UI) · gameplay code (CLI) · **runtime node spawn (no `VillageSceneBuilder` for the MVP)**
**Depends on:** WO-86 (SO data architecture — DONE; follow its pattern). **Soft-ties (do not block on):** WO-111 (resource pillar design — this WO is its concrete worker layer), WO-112 (ward-tether claims nodes), WO-115 (offline accrual reads node stores), WO-122 (crystal mine site).
**Canon source:** `docs/NORTH_STAR.md` (core loop BUILD → HARVEST → DEFEND → OFFLINE; *"DEFEND base + mines from waves and roaming enemies — or lose them"*), owner's vision below.

---

## Vision (owner's words)

> "Send workers to a node (Mine/Crystal/Food/Wood) and set off an **auto-collect** system that runs
> till the collection store is full, but **needs to be safe** as **random encounters could invade**."

This is **Warcraft's harvest** (dispatch a worker → it gathers on its own → bank the haul) fused with
this game's **tower-defense core** (the node is exposed; roaming enemies can raid it; you defend or you
lose the haul). It is the NORTH_STAR line *"defend your mines or lose them"* made into a single,
repeatable verb: **claim a node → send a worker → it auto-fills → keep it safe → bank it.**

The harvest is **not pure idle**. The tension is the whole point: an unattended node is income *at risk*.

---

## Reconciliation — what already exists (confirmed by inspection; build-up, not rebuild)

I read the economy, save, enemy and wave layers before writing this. **The worker/harvest layer is
genuinely greenfield** — there is NO existing `Worker`, `ResourceNode`, `CollectionPoint`, `HarvestService`,
`WorkerManager`, or `ResourceType` code anywhere in `Assets/`. You are designing it fresh. But the
pieces it *hangs off* are all built:

| Need | Exists? | Where / note |
|---|---|---|
| Resource wallet (the 4 node payouts) | **BUILT — all four already exist** | `GameState`: `Wood`, `Stone` (= the Ore/Stone-Mine payout), `AetherCrystals` + `Resources.Crystals` (Crystal), `Resources.Food` (Food). **No new currency needed** — see §1. |
| SO data architecture | **BUILT (WO-86)** | `Assets/Data/EnemyData.cs`, `WaveData.cs` — `[CreateAssetMenu]` SO pattern; **follow it for `ResourceNodeData`.** |
| Enemy unit + NavMesh AI | **BUILT** | `Assets/_Modules/Village/Enemies/Enemy.cs`, `EnemyBrain.cs` (role targeting + NavMesh, tag-based `FindClosestTarget`/`SearchByTag`). |
| Spawn path (for invasions) | **BUILT** | `WaveManager.cs` + `EnemyGroupSpawner.cs` spawn one `Enemy` per entry at spawn points. **Reuse this path — do NOT fork a new spawner.** |
| Award-to-economy seam | **BUILT** | Village writes `GameState` resource fields directly (Core can't reference Village — same path `CrystalMine` uses). |
| Damageable contract | **BUILT** | `IDamageableStructure` in `DeNelle.Core.Combat` — if a node becomes attackable in Phase 2, it implements this with `using DeNelle.Core.Combat;`. |
| Code-built world UI precedent | **BUILT** | `CrystalMine.InjectUpgradePanel()` builds its UI in C# — mirror it for the fill indicator (no UXML — PIPELINE_STATE.md §8). |
| Offline accrual consumer | **spec'd (WO-115)** | WO-115 reads `node.RatePerSecond` + claimed state off a registry seam — this WO **provides** that seam. |
| Node claim gate | **spec'd (WO-112)** | ward-tether flips a node's claimed flag — this WO **exposes** that flag (Phase 3 tie). |

**So the new work is the worker + node + dispatch + risk SYSTEM — not a new economy, enemy, spawner,
or currency.** Reuse all of the above.

---

## 1. The four resource / node types — all map to EXISTING currencies (none net-new)

| Node type | Payout currency | Field (already in `GameState`) | New currency? |
|---|---|---|---|
| **Wood** (logging camp) | Wood | `GameState.Wood` | **No — exists** |
| **Food** (farm / forage) | Food | `GameState.Resources.Food` | **No — exists** |
| **Crystal** (crystal vein) | Aether Crystals | `GameState.AetherCrystals` (and/or `Resources.Crystals`) | **No — exists** |
| **Ore / Stone-Mine** (the Warcraft "gold mine") | Stone | `GameState.Stone` | **No — exists** |

> **Flag for owner:** all four payouts route to fields that already exist, so there is **no new currency
> to add to the save round-trip** — a meaningful scope saving for Sunday. Iron also exists if you ever
> want a 5th node. Owner decides whether Crystal banks to `AetherCrystals` or `Resources.Crystals`
> (recommend `AetherCrystals` — it's the established `CrystalMine` payout target). The **Phase-1 MVP
> ships ONE node type — recommend Wood** (cleanest, lowest-stakes currency to balance).

---

## 2. Data model — DESIGN ONLY (illustrative; CLI writes the real code)

These blocks show **shape and intent**, not final code. Assembly discipline (CLAUDE.md §5): the
**enum lives in `DeNelle.Core`** (pure data, so Village + future HUD readouts can both see it); the
**MonoBehaviours + service live in `DeNelle.Village`**. Village → Core only.

### 2a. `ResourceType` enum — `DeNelle.Core`

```csharp
namespace DeNelle.Core
{
    /// <summary>The harvestable node kinds. Maps 1:1 to an existing GameState wallet field (§1).</summary>
    public enum ResourceType { Wood, Food, Crystal, Ore }   // Ore banks to GameState.Stone
}
```

### 2b. `ResourceNodeData` — ScriptableObject (the catalog entry) — `DeNelle.Village`

Authoring-time definition of one node. Follows the WO-86 `[CreateAssetMenu]` pattern exactly.

```csharp
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Village
{
    [CreateAssetMenu(menuName = "Defenders/Resource Node", fileName = "Node_")]
    public sealed class ResourceNodeData : ScriptableObject
    {
        public string       id;            // stable save key, e.g. "node_wood_east"
        public ResourceType resourceType;  // §1 — which wallet it banks to

        [Header("Harvest")]
        public float baseRatePerSecond = 1f;   // units accrued per second of active collection
        public int   storeCap          = 200;  // node fills to here, then idles "full"

        [Header("Placement (runtime spawn for the MVP — §7)")]
        public Vector3 worldPosition;          // where the node spawns at runtime (no VillageSceneBuilder)
        public string  prefabKey;              // poly prefab to instance (LogWarning + stub if missing)
    }
}
```

### 2c. `ResourceNode` — runtime MonoBehaviour (the in-world node) — `DeNelle.Village`

Holds the live store, fill %, claimed state, and the assigned worker. Exposes a read-only seam the
offline-accrual service (WO-115) and the HUD can read **without writing**.

```csharp
using UnityEngine;
using DeNelle.Core;
// Phase 2+ only, when the node becomes raidable:
// using DeNelle.Core.Combat;   // for IDamageableStructure

namespace DeNelle.Village
{
    /// <summary>One on-map resource node. Accrues into its own store while a worker
    /// is collecting; banks on return/tap. Phase 2 adds the raid/defense layer.</summary>
    public sealed class ResourceNode : MonoBehaviour
    {
        public ResourceNodeData Data;

        public bool  IsClaimed   { get; private set; }   // WO-112 ward-tether flips this (Phase 3)
        public float CurrentStore{ get; private set; }   // units banked into the node, <= storeCap
        public float FillPercent => Data == null || Data.storeCap <= 0 ? 0f
                                    : Mathf.Clamp01(CurrentStore / Data.storeCap);
        public bool  IsFull      => CurrentStore >= (Data?.storeCap ?? 0);

        public Worker AssignedWorker { get; private set; }
        public bool   IsCollecting   => AssignedWorker != null && AssignedWorker.IsHarvestingAt(this);

        // WO-115 offline-accrual seam (read-only): effective rate while a worker is on station.
        public float RatePerSecond => IsCollecting ? Data.baseRatePerSecond * _boostMultiplier : 0f;
        float _boostMultiplier = 1f;                       // Phase 3 pet boost sets this (NORTH_STAR pets)

        public void AccrueTick(float dt)                   // called by HarvestService while collecting
        {
            if (Data == null || IsFull) return;
            CurrentStore = Mathf.Min(Data.storeCap, CurrentStore + RatePerSecond * dt);
        }

        public int Bank()                                  // pull the store out → caller writes GameState
        {
            int haul = Mathf.FloorToInt(CurrentStore);
            CurrentStore -= haul;
            return haul;
        }

        public void SetClaimed(bool claimed) => IsClaimed = claimed;     // WO-112 hook
        public void SetBoost(float mult)     => _boostMultiplier = Mathf.Max(1f, mult); // WO-111 P4 pet hook
    }
}
```

### 2d. `Worker` — the dispatched unit — `DeNelle.Village`

Travels to its node (NavMesh, exactly like `Enemy`'s agent), harvests, can be lost in a raid.

```csharp
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    public enum WorkerState { Idle, Traveling, Collecting, Returning, Fleeing }

    /// <summary>A harvest worker. Dispatched to a claimed node, walks there on the NavMesh,
    /// triggers the node's auto-collect, and walks the haul home (or flees a raid).</summary>
    public sealed class Worker : MonoBehaviour
    {
        public WorkerState State { get; private set; } = WorkerState.Idle;
        ResourceNode _target;
        NavMeshAgent _agent;          // same agent pattern Enemy.cs uses

        public bool IsHarvestingAt(ResourceNode n) => State == WorkerState.Collecting && _target == n;

        public void DispatchTo(ResourceNode node)        // called by WorkerManager on assign
        {
            _target = node; State = WorkerState.Traveling;
            _agent.SetDestination(node.transform.position);
        }

        // Update(): on arrival → State = Collecting (HarvestService ticks the node).
        // On Bank request or node Full → State = Returning, walk to drop-off, write GameState, Idle.
        // Phase 2: on raid → Flee/Lost (see §5).
    }
}
```

### 2e. `WorkerManager` / `HarvestService` — the orchestrator — `DeNelle.Village`

One scene MonoBehaviour. Owns the worker roster + the live node registry, runs the per-frame
collection tick, and exposes the registry seam WO-115 reads.

```csharp
namespace DeNelle.Village
{
    public sealed class HarvestService : MonoBehaviour
    {
        public static HarvestService Instance { get; private set; }

        // Phase 1: spawn nodes from ResourceNodeData at runtime (§7) — no VillageSceneBuilder.
        // Update(): foreach collecting node → node.AccrueTick(Time.deltaTime).
        // Dispatch(worker, node): worker.DispatchTo(node) (node must be claimed — Phase 3 gate).
        // BankNode(node): haul = node.Bank(); write the haul to the matching GameState field (§1).

        // WO-115 seam — read-only list of claimed + collecting nodes and their rates:
        public System.Collections.Generic.IReadOnlyList<ResourceNode> ActiveClaimedNodes() { /* ... */ return null; }

        // Cross-module feedback (collect chime, full ping) via CoreServices with ?. :
        //   CoreServices.Audio?.PlaySfx(SfxId.HarvestBank);
    }
}
```

---

## 3. Dispatch interaction (the player verb)

1. **Select a worker.** Tap a worker (or a "send worker" button on a claimed node). Phase 1 may have a
   **single worker** at the village — selection is implicit (tap node → the free worker goes).
2. **Assign to a claimed node.** Tap a claimed `ResourceNode`. (Phase 1: nodes start pre-claimed; the
   ward-tether claim gate is the **Phase 3** tie to WO-112.) If the node is not claimed, show a cold
   "out of reach" affordance and refuse.
3. **Travel.** `Worker.DispatchTo(node)` sets a NavMesh destination — the worker walks there using the
   **same `NavMeshAgent` pattern `Enemy.cs` already uses** (the Village scene's baked NavMesh).
4. **Auto-collect begins.** On arrival the worker enters `Collecting`; `HarvestService` starts ticking
   that node's store. The player can walk away — it runs on its own (the Warcraft beat).

---

## 4. The auto-collect loop + banking

**Accrual:** while a worker is `Collecting`, `HarvestService` calls `node.AccrueTick(dt)` each frame:
`CurrentStore += baseRatePerSecond × boost × dt`, clamped to `storeCap`. When `CurrentStore` reaches
`storeCap` the node is **Full** → it stops accruing and the worker idles on station (or auto-returns —
owner's call; Phase-1 default: **idle full, wait to be banked**).

**Fill indicator (CODE-BUILT world UI — PIPELINE_STATE.md §8, no UXML):** a small world-space bar /
ring above the node showing `FillPercent`, mirroring how `CrystalMine.InjectUpgradePanel()` builds its
`VisualElement` tree in code. States: *collecting* (filling), *full* (pulse/glow), *idle* (dim).
Build it in C#; do **not** author a `.uxml` or `UIDocument` source asset.

**Banking (collect the haul):** two paths, both write the matching `GameState` field (§1):
- **Worker returns** — when Full (or on a "recall" tap), the worker walks home to a drop-off and the
  haul is added to `GameState` on arrival; or
- **Player taps to collect** — tapping a Full/partly-full node banks `node.Bank()` immediately.

Banking writes `GameState` **directly** (Core can't reference Village — the established `CrystalMine`
award path). A collect chime via `CoreServices.Audio?.PlaySfx(...)` with `?.`.

---

## 5. THE SAFETY / RISK LAYER — random encounters can invade (core to the vision)

This is the heart of the owner's vision: an unattended node is **income at risk**. The harvest is only
safe if you defend it.

**Telegraphed, never a gotcha:**
1. **Random roaming encounter spawns near a working node.** On a randomized timer (weighted by how
   long the node has been collecting and how full it is — a fuller store is a juicier target), a small
   roaming pack spawns near an actively-collecting node. **Reuse the existing spawn path** —
   `EnemyGroupSpawner` / `WaveManager`'s spawn call with the village `Enemy` + `EnemyBrain` (NavMesh +
   role targeting already work). **Do NOT fork a new spawner.** Spawn from a `SpawnPoint`-tagged point
   (the existing convention: 12m outside the relevant gate) and aim the pack at the node.
2. **Telegraph window (the chance to respond).** Before the pack reaches the node, fire a clear warning
   — a world ping at the node + a HUD alert via `CoreServices.Hud?` (with `?.`) and an audio sting via
   `CoreServices.Audio?`. The player gets **a few seconds of lead time** to send the hero / a pet, or
   to rely on a nearby tower. Make the alert obvious; the risk must be *responded to*, not *suffered*.
3. **Consequence if undefended (meaningful, not punishing):** if the pack reaches a collecting node and
   no defense intervenes:
   - The **worker flees** (`WorkerState.Fleeing`) — runs home; collection is **interrupted** (node
     stops accruing). Phase-1-friendly default: worker survives but is benched briefly ("shaken").
   - A **percentage of the node's store is raided** (default **25–40%** of `CurrentStore`, tunable) —
     *not* the whole store, never the worker permanently "lost" on a first offense. Owner can dial a
     harsher "worker captured / lost" outcome later, but **default to recoverable.**
   - The node returns to **claimable/idle** and can be re-worked once the area is clear.
4. **Defense = the existing TD core.** The player defends the node with **nearby towers** (auto-fire at
   the pack — towers already target enemies), the **hero** (walk over and fight — `HeroTarget` tag
   AI already engages), or **pets**. No new combat system — the invasion enemies ARE village enemies,
   so every existing defense works on them for free.
5. **If defended:** the pack is killed, collection resumes, **no store lost.** Defending a node should
   feel like a small, winnable skirmish — the reward is "you kept your haul."

> **Balance flag for owner:** the encounter timer, pack size, lead-time window, and raid-% are the four
> tuning knobs. Default posture: **telegraphed + recoverable** — meaningful (you *can* lose part of a
> haul) but never a rug-pull (you never lose the worker or the whole store on a first hit). Tune in
> playtest. **Phase 1 ships with NO invasions (or a disabled stub)** so the core dispatch verb is
> demoable Sunday without combat-balancing risk — invasions land in Phase 2.

---

## 6. Ties to neighbouring systems (do NOT duplicate their state)

- **Ward-tether claim (WO-112):** a node is worked only when **claimed**. WO-112 lights a node-ward →
  flips `ResourceNode.SetClaimed(true)`. Phase 1 nodes start **pre-claimed** (skip the gate); Phase 3
  wires the real ward gate. Read the claim flag — do not re-derive ward state here.
- **Offline accrual (WO-115):** WO-115 reads `HarvestService.ActiveClaimedNodes()` + each node's
  `RatePerSecond` to accrue while away. This WO **provides that registry seam**; WO-115 consumes it.
  Keep the seam read-only and null-safe (WO-115 is already written to no-op if it's absent).
- **Pet boost (WO-111 Phase 4 / NORTH_STAR pets auto-harvest):** assigning a pet to a node calls
  `ResourceNode.SetBoost(mult)` to raise its rate (and/or stand in for a worker). Phase 3 hook —
  the seam exists in 2c now; the pet behaviour is out of scope here.
- **Crystal mine (WO-122):** the on-map crystal becomes a Crystal-type `ResourceNode` once this system
  lands — reconcile, don't duplicate; the passive `CrystalMine` yield can be retired in favour of the
  worker-collect model when the owner chooses (note only — not Phase 1).

---

## 7. World placement — RUNTIME SPAWN for the MVP (avoid the VillageSceneBuilder bottleneck)

`VillageSceneBuilder.cs` is the single-touch serialization bottleneck (CLAUDE.md §9) and `Village.unity`
is never hand-edited (CLAUDE.md §3). To keep Phase 1 **unblocked for Sunday**, nodes **spawn at runtime**:

- `HarvestService` on `Start()` instantiates a `ResourceNode` per `ResourceNodeData` asset at its
  `worldPosition`, seating it on the baked NavMesh, instancing `prefabKey` (LogWarning + a primitive
  stub if the poly prefab is missing — pack may not be imported, CLAUDE.md §4).
- The worker spawns at the village (near the drop-off) the same way.
- **No `VillageSceneBuilder` edit, no bake, no scene hand-edit** for Phase 1. If the owner later wants
  authored node placement baked into the scene, that becomes a Phase-3 architect-lane line that rides
  the next rebake — **not now.**

---

## Files to Create / Edit

| File | Action | Phase |
|---|---|---|
| `Assets/_Modules/Core/ResourceType.cs` | **Create** — `ResourceType` enum (Core) | 1 |
| `Assets/_Modules/Village/Harvest/ResourceNodeData.cs` | **Create** — `[CreateAssetMenu]` SO (WO-86 pattern) | 1 |
| `Assets/_Modules/Village/Harvest/ResourceNode.cs` | **Create** — runtime node: store, fill %, claimed, boost seam | 1 |
| `Assets/_Modules/Village/Harvest/Worker.cs` | **Create** — NavMesh travel + collect/return/flee states | 1 |
| `Assets/_Modules/Village/Harvest/HarvestService.cs` | **Create** — orchestrator, runtime node spawn, tick, bank, WO-115 seam | 1 |
| `Assets/_Modules/Village/Harvest/UI/NodeFillIndicator.cs` | **Create** — **code-built** world fill bar (no UXML) | 1 |
| `Assets/Data/ResourceNodes/Node_Wood.asset` | **Create** — the Phase-1 Wood node SO instance | 1 |
| `Assets/_Modules/Village/Harvest/NodeRaidController.cs` | **Create** — telegraphed encounter spawn (reuses `EnemyGroupSpawner`/`WaveManager` path) + raid consequence | 2 |
| `Assets/_Modules/Core/Audio/SfxId.cs` (or equiv) | **Edit (if needed)** — add `HarvestBank` / `NodeRaidWarning` ids | 2 |
| `Assets/_Modules/Core/HUD/IVillageHud.cs` | **Edit (if needed)** — add a node-raid alert hook (passive display) | 2 |
| `ResourceNode.cs` | **Edit** — implement `IDamageableStructure` (`using DeNelle.Core.Combat;`) if the node itself becomes raidable | 2 |
| `Assets/Data/ResourceNodes/Node_{Food,Crystal,Ore}.asset` | **Create** — remaining 3 node SOs | 3 |
| `HarvestService.cs` | **Edit** — multi-worker dispatch + selection; pet-boost wiring; ward-claim gate (WO-112) | 3 |

**Assembly discipline (CLAUDE.md §5):** `ResourceType` enum in `DeNelle.Core`; everything else in
`DeNelle.Village`. **Village → Core only.** Banking writes `GameState` resource fields directly (Core
can't reference Village — the established award path). All HUD/Audio calls go through `CoreServices.Hud?`
/ `CoreServices.Audio?` with `?.` — never a direct `DeNelle.HUD` reference. Any file implementing
`IDamageableStructure` needs `using DeNelle.Core.Combat;`. **No new `System.Reflection`.** UI is
**code-built** (no UXML). Run the brace-balance gate on every `.cs` touched.

---

## Acceptance Criteria — by phase

### Phase 1 — Sunday MVP (the immediate CLI target)
- [ ] `ResourceType` enum exists in `DeNelle.Core` (Wood, Food, Crystal, Ore)
- [ ] `ResourceNodeData` SO authorable via `[CreateAssetMenu]`; a **Wood** node asset created
- [ ] `HarvestService` spawns the node(s) **at runtime** from the SO — **no `VillageSceneBuilder` edit, no bake, no `Village.unity` hand-edit**
- [ ] A worker can be **dispatched** to a (pre-claimed) node, **travels via NavMesh**, and enters Collecting on arrival
- [ ] Node store **auto-fills** at `baseRatePerSecond` until `storeCap`, then idles **Full**
- [ ] **Code-built** world fill indicator shows `FillPercent` (no UXML), updates as it fills, signals Full
- [ ] **Banking** (worker return or tap) writes the haul to the matching `GameState` field (Wood) — verified persisted
- [ ] **No invasions yet** (or a disabled stub) — the dispatch → collect → bank verb is demoable end-to-end
- [ ] `HarvestService.ActiveClaimedNodes()` + `node.RatePerSecond` seam present and read-only (WO-115 can consume it null-safe)
- [ ] Brace balance passes on every `.cs`; cross-module calls use `?.`; Village → Core only

### Phase 2 — the risk layer
- [ ] `NodeRaidController` spawns a roaming pack near an actively-collecting node on a randomized, store-weighted timer — **reusing `EnemyGroupSpawner`/`WaveManager`'s spawn path (no new spawner)**
- [ ] A clear **telegraph** fires before the pack arrives (node world ping + `CoreServices.Hud?` alert + `CoreServices.Audio?` sting) with a few seconds of lead time
- [ ] If undefended: worker **flees** (collection interrupts) and a **tunable % (default 25–40%)** of the store is raided — never the whole store, never the worker permanently lost by default
- [ ] If defended (towers / hero / pets — existing TD systems engage the pack for free): pack dies, collection resumes, **no store lost**
- [ ] Encounter timer, pack size, lead-time, raid-% are inspector-tunable knobs
- [ ] If `ResourceNode` is made directly raidable it implements `IDamageableStructure` with `using DeNelle.Core.Combat;`

### Phase 3 — depth + integration polish
- [ ] **Multiple workers** with explicit selection + multi-node dispatch
- [ ] All **4 node types** (Wood, Food, Crystal, Ore→Stone) authorable and working
- [ ] **Pet boost** wired: assigning a pet calls `ResourceNode.SetBoost(mult)` (NORTH_STAR pets auto-harvest / WO-111 P4)
- [ ] **Ward-tether claim gate** wired (WO-112): nodes start unclaimed; lighting a node-ward flips `SetClaimed(true)` — no duplicated ward state
- [ ] **Offline accrual** (WO-115) confirmed reading the `ActiveClaimedNodes()` seam end-to-end
- [ ] Crystal node reconciled with WO-122 `CrystalMine` (no duplicate Crystal income path)

---

## Do NOT touch

- **Do NOT edit `VillageSceneBuilder.cs` or fire any bake/batchmode for Phase 1** — nodes spawn at
  runtime (§7). Any authored placement is a later architect-lane line, not the MVP.
- **Do NOT hand-edit `Village.unity`** (CLAUDE.md §3) — nothing in this WO touches the scene file.
- **Do NOT fork a new enemy spawner** for invasions — reuse `EnemyGroupSpawner` / `WaveManager`'s
  existing spawn path with the village `Enemy` + `EnemyBrain` (Phase 2).
- **Do NOT add a new currency or new save round-trip for the payouts** — all four node types bank to
  existing `GameState` fields (Wood / Stone / AetherCrystals / Resources.Food). (Persisted node-store
  state, if needed beyond WO-115's accrual, is a Phase-3 question — keep Phase 1 in-session only.)
- **Do NOT build any UI in UXML** — code-built only (PIPELINE_STATE.md §8).
- **Do NOT reference `DeNelle.HUD` from `DeNelle.Village`** — alerts/feedback go through `CoreServices`.
- **Do NOT duplicate ward (WO-112) / offline-accrual (WO-115) / CrystalMine (WO-122) state** — expose
  seams, read the existing flags; this WO is the worker layer, not a re-implementation of those.
- **Do NOT make the risk punishing** — telegraphed, recoverable, partial loss by default; never a
  rug-pull. (Mirrors WO-112's "meaningful but not punishing" discipline.)
- **Do NOT introduce `System.Reflection`** in these scripts.
- Do not touch ATB, WalletService, monetization, or clan code.

---

🤖 Spec'd by the design lane (UI). Reconciled against `GameState` (all four payout currencies already
exist — no net-new currency), `EconomyService`, `Enemy`/`EnemyBrain`/`WaveManager`/`EnemyGroupSpawner`
(reusable invasion spawn path), the WO-86 SO pattern, and WO-111/112/115/122. Confirmed greenfield:
no `Worker`/`ResourceNode`/`CollectionPoint`/`HarvestService`/`ResourceType` exists today. Markdown
work order only — no `.cs` touched, no bake fired.
