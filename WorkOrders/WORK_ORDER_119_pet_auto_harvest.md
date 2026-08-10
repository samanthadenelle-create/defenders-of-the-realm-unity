# WORK ORDER 119 — Pet Auto-Harvest: A Spirit at a Building Tends

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: evolved into the Echo harvest system, canon §7 / WO-830 ruling)
**Date:** 2026-05-30
**Priority:** High — the pet half of the HARVEST pillar; completes WO-117's `SetBoost` seam and unblocks WO-115's pet-accrual no-op
**Scope:** Medium — one additive enum value + one new behaviour component in `DeNelle.Village`; an optional additive field on `PetData`. NO changes to combat AI.
**Depends on:** WO-117 (the harvest system — provides `ResourceNode.SetBoost(float)` + `RatePerSecond` + `ActiveClaimedNodes()` seams), WO-86 (SO data architecture — DONE), WO-58 (pet combat aura — **must not break**)
**Soft-ties (do not block on):** WO-115 (offline accrual reads the pet-harvest registry this WO provides), WO-112 (a pet can only tend a **claimed** node)
**Canon source:** `docs/narrative-bible.md` §5 ("A pet placed near a building tends to it... A spirit at a slot defends. A spirit at a building tends. They cannot do both at once."), `docs/NORTH_STAR.md` ("pets auto-harvest/boost"; "OFFLINE: mines + pets keep gathering up to a cap")

---

## Vision

The bible already wrote this mechanic — it just needs wiring:

> "A pet placed near a building **tends** to it. A pet placed in a defensive slot **fights** for it."
> — `narrative-bible.md` §5, *The bond*
>
> First-pet-placement bark: *"A spirit at a slot defends. A spirit at a building tends. **They cannot
> do both at once.**"*

That last line is the whole design: a pet is **one** spirit. Send it to a defensive slot and it hunts
(WO-58 / `PetMode.Defend`). Send it to a **claimed resource node** and it tends — it makes the harvest
run faster, or runs the harvest *for you* when no worker is there. **It cannot do both.** Choosing where
each pet goes is the player's standing decision: more income, or more defense. That trade-off is the
NORTH_STAR line *"pets auto-harvest/boost"* made into a placeable verb, and it feeds the offline rung
(*"mines + pets keep gathering"*, WO-115).

This WO is a **thin additive layer on top of WO-117's harvest system.** It does **not** redesign the
harvest loop, the node store, banking, the fill indicator, or the risk layer — all of that is WO-117's.
It only adds: *a pet can be assigned to tend a node, and while tending it boosts and/or stands in for a
worker.* It consumes the seams WO-117 already exposes.

---

## Reconciliation — what already exists (build-up, not rebuild)

I read the pet layer and WO-117's seam before writing this. **Pet auto-harvest behaviour does NOT exist
yet** — confirmed: `Pet.cs` only knows `Idle / Defend / Fortify` (no Tend mode); `PetData` carries only
combat + aura stats; WO-115 explicitly wires pet accrual as a **null-safe no-op awaiting this WO**
(WO-115 §1: *"Pets only if assigned to harvest (WO-111 P4) — null-safe no-op until built"*). The pieces
this hangs off, however, are all built or spec'd:

| Need | Exists? | Where / note |
|---|---|---|
| Node rate-boost seam | **spec'd (WO-117)** | `ResourceNode.SetBoost(float mult)` → sets `_boostMultiplier`; `RatePerSecond => baseRatePerSecond * _boostMultiplier`. **This WO calls that seam — does not add a new one.** |
| Node "is collecting" + claimed flags | **spec'd (WO-117)** | `ResourceNode.IsClaimed`, `IsCollecting`, `AssignedWorker`, `AccrueTick(dt)` — read these; do not re-derive. |
| Offline pet-harvest registry consumer | **spec'd (WO-115)** | WO-115 §1 loops `PetHarvestRegistry.HarvestingPets()` and reads `pet.RatePerSecond` + `pet.ResourceType`, null-safe until built. **This WO provides that registry.** |
| Node-claim gate | **spec'd (WO-112)** | only a **claimed** node can be tended (`IsClaimed`). Read the flag; do not duplicate ward state. |
| Pet roster + deploy | **BUILT** | `DeNelle.Pets.Pet` (species string: `aether-sprite`/`flame-pup`/`ice-wolf`), `PetDeployer`, `PetMode {Idle, Defend, Fortify}`. |
| Pet combat aura (WO-58) | **BUILT — DO NOT BREAK** | `PetData.level{1,3,5}EmissionRate` + `enableOrbitSparksAtL5`; aura is a **combat buff**, unrelated to harvest. This WO touches NONE of it. |
| Pet balance SO | **BUILT (WO-86)** | `Assets/Data/PetData.cs` (`DeNelle.Data` namespace) — add one additive harvest field here if per-pet flavor is wanted (§3). |
| SO data architecture | **BUILT (WO-86)** | follow the `[CreateAssetMenu]` pattern for any new data. |

**So the new work is: one Tend mode + a `PetHarvestAssignment` behaviour that calls `SetBoost()` and (optionally)
runs the node when no worker is present + a tiny registry seam for WO-115 — NOT a new harvest loop, node,
or combat change.** Reuse all of the above.

> **Hard dependency flag for owner/CLI:** this WO **consumes WO-117's `ResourceNode.SetBoost()` /
> `RatePerSecond` / `IsClaimed` / `IsCollecting` seams.** It should land **after WO-117 Phase 1** (the
> node + service exist). If WO-117 is not yet merged, the `PetHarvestAssignment` component compiles but
> has nothing to attach to — ship it behind WO-117, not before.

---

## 1. The mechanic — tend OR defend, never both

A pet is in exactly one of: **defending** a slot (WO-58, hunts enemies) or **tending** a claimed node
(this WO). Assigning it to one **clears** the other. This is the bible's "they cannot do both at once"
made literal — it is the *only* gameplay rule this WO enforces beyond the boost math.

**Two effects a tending pet has (v1 ships BOTH, uniform across pets):**

1. **(a) Boost** — while a pet is stationed at a claimed node that *also* has a worker collecting, the
   pet **raises the node's collect rate** by a flat % (default **+30%**, tunable). It does this by
   calling the WO-117 seam `node.SetBoost(1.30f)`; clearing the assignment calls `node.SetBoost(1f)`.
   (WO-117's `SetBoost` already clamps to `>= 1f`, so the floor is safe.)
2. **(b) Stand-in harvest** — if a claimed node has **no worker** but **does** have a tending pet, the
   pet **runs the harvest itself, slower** (default **50%** of a worker's `baseRatePerSecond`). This is
   the "auto-harvest" of the NORTH_STAR line: you don't need a worker on every node if a pet tends it —
   it just gathers at a gentler pace. The pet drives the same `ResourceNode.AccrueTick(dt)` WO-117
   already exposes; it does **not** add a second accrual path.

**Combined behaviour (the clean v1 rule):**
- Node has **worker only** → worker rate (WO-117), no pet involved.
- Node has **worker + tending pet** → worker rate × **1.30** (pet boosts via `SetBoost`).
- Node has **pet only** (no worker) → **0.50 × baseRatePerSecond** (pet stands in, slower).
- Node has **neither** → 0 (idle), as WO-117 already defines.

**The trade-off (state it in the UI):** a pet tending a node is **NOT defending** — it will not hunt
the roaming raid pack that WO-117's risk layer spawns at a collecting node. So a pet that boosts your
income is a pet absent from your line. The first-pet bark already teaches this; the assignment UI should
echo it ("This pet will tend, not fight"). **Owner balance knob:** whether a tending pet flees a raid
(like a worker) or just keeps tending obliviously — recommend **flees + stops tending** for the duration,
matching WO-117's worker-flee posture (telegraphed, recoverable, never punishing).

**Offline:** a node with a tending pet counts for WO-115 offline accrual exactly like a worked node —
the pet's contribution flows through `RatePerSecond` (boost case) and through the new
`PetHarvestRegistry` (stand-in case). See §4.

---

## 2. Assignment interaction (the player verb)

1. **Pick a pet, pick a node.** Tap a pet, then tap a **claimed** `ResourceNode` (or a "Send pet to
   tend" affordance on the node). If the node is **not claimed** (WO-112), refuse with the same cold
   "out of reach" affordance WO-117 uses for unclaimed nodes — do not duplicate the gate.
2. **It stops defending.** Assigning to tend sets the pet's mode out of `Defend` and into the new
   **`Tend`** state (§3); its old defensive slot is now empty (the bible rule). Reassigning it to a
   slot reverses this and calls `node.SetBoost(1f)` to release the boost.
3. **It travels and tends.** The pet walks to the node (reuse `Pet`'s existing kinematic
   `MoveToward` / home-post drift — **do not** add a NavMeshAgent or a second movement system) and,
   on arrival, applies its boost (if a worker is present) and/or begins stand-in accrual (if not).
4. **One pet per node (v1).** A node accepts at most one tending pet; a second assignment to the same
   node is refused (or reassigns the first home). Keep it simple — stacking pets is a later balance pass.

---

## 3. Data + code model — DESIGN ONLY (illustrative; CLI writes the real code)

Assembly discipline (CLAUDE.md §5): the tending **behaviour lives in `DeNelle.Village`** (it references
both `DeNelle.Pets.Pet` and the Village `ResourceNode` — exactly like `PetContextualBehaviour` does
today, which is *why* it sits in Village, not Pets). The optional per-pet **data field lives on
`PetData` (`DeNelle.Data`)**. **Village → Core/Pets only; Pets never references Village.**

### 3a. Tend mode — add ONE value to the existing `PetMode` enum (Pets) OR gate in Village

`Pet.PetMode` today is `{ Idle, Defend, Fortify }`. The cleanest seam is a fourth value, **`Tend`**, so
`Pet.Update()` early-returns out of the hunt loop when tending (it already early-returns for any
non-`Defend` mode — see `Pet.Update()` line ~256: *"if (_mode != PetMode.Defend) return;"*). That single
line means **a `Tend`-mode pet automatically stops hunting with zero combat-AI changes** — the bible rule
falls out for free.

```csharp
// DeNelle.Pets — Pet.cs PetMode enum (ADD ONE VALUE — does not alter existing combat paths)
public enum PetMode
{
    Idle    = 0,   // follows hero, does not fight
    Defend  = 1,   // hunts the nearest enemy (WO-58 aura active here)
    Fortify = 2,   // holds a wall span
    Tend    = 3,   // NEW — stationed at a claimed ResourceNode; boosts / stands in. Does NOT hunt.
}
```

> **If CLI prefers zero edits to the Pets assembly:** the Village-side `PetHarvestAssignment` can set the
> pet to `PetMode.Idle` (which also early-returns out of the hunt loop) and drive movement itself,
> leaving `PetMode` untouched. Either is fine — **the non-negotiable is that a tending pet does not hunt,
> and WO-58's aura/combat path is not modified.** Owner/CLI picks; `Tend` is the more legible choice.

### 3b. `PetHarvestAssignment` — the additive behaviour (Village) — the heart of this WO

One component, placed on (or added at runtime to) a tending pet. It is the **only** new gameplay code.
It calls WO-117's seams; it never reimplements them.

```csharp
using DeNelle.Pets;     // Pet, PetMode
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Assigns a pet to TEND a claimed ResourceNode (narrative-bible §5: "a pet placed
    /// near a building tends to it"). While assigned the pet (a) boosts the node's
    /// collect rate when a worker is present, and/or (b) stands in as a slow harvester
    /// when no worker is there. A tending pet does NOT defend (bible: "cannot do both").
    /// Lives in Village because it sees BOTH Pet and the Village ResourceNode — the same
    /// reason PetContextualBehaviour lives here. Does NOT touch WO-58 combat aura.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetHarvestAssignment : MonoBehaviour
    {
        [Tooltip("Flat collect-rate boost applied while a worker is also collecting (1.30 = +30%).")]
        [SerializeField] private float _boostMultiplier = 1.30f;

        [Tooltip("Fraction of baseRatePerSecond the pet harvests alone, with no worker (0.5 = half).")]
        [SerializeField, Range(0.1f, 1f)] private float _standInRateFraction = 0.5f;

        private Pet _pet;
        private ResourceNode _node;     // the claimed node being tended (null = unassigned)

        public ResourceNode TendedNode => _node;
        public bool IsTending => _node != null;

        // WO-115 offline-accrual seam: which resource + at what per-second rate this pet contributes.
        public ResourceType ResourceType => _node != null && _node.Data != null
                                            ? _node.Data.resourceType : default;
        public float RatePerSecond => StandInActive
            ? (_node.Data.baseRatePerSecond * _standInRateFraction)   // pet alone → slow stand-in
            : 0f;                                                      // boost-only contribution flows via node.RatePerSecond

        // Stand-in is active only when the node is claimed, has NO worker, and a pet is here.
        private bool StandInActive => _node != null && _node.IsClaimed && !_node.IsCollecting;

        public void Assign(Pet pet, ResourceNode node)
        {
            if (pet == null || node == null || !node.IsClaimed) return;   // WO-112 claimed gate
            Release();                                                    // leave any prior node clean
            _pet = pet; _node = node;
            _pet.Mode = PetMode.Tend;                                     // stops hunting (bible rule)
            // PetHarvestRegistry.Add(this);   // register for WO-115 offline accrual
            ApplyBoost();
        }

        public void Release()
        {
            if (_node != null) _node.SetBoost(1f);                        // WO-117 seam — clear the boost
            // PetHarvestRegistry.Remove(this);
            _node = null;
            if (_pet != null) _pet.Mode = PetMode.Defend;                 // back to the line
        }

        private void ApplyBoost()
        {
            // Boost the node only while a worker is collecting; SetBoost clamps >= 1f (WO-117).
            _node?.SetBoost(_node.IsCollecting ? _boostMultiplier : 1f);
        }

        private void Update()
        {
            if (_node == null) return;
            // Re-evaluate boost vs stand-in as the worker comes/goes (WO-117 owns AccrueTick).
            ApplyBoost();
            if (StandInActive)
                _node.AccrueTick(Time.deltaTime * _standInRateFraction); // slow stand-in harvest
            // Movement: reuse Pet's existing drift toward the node (set the pet's home post to the
            // node, exactly as PetHeroLeash re-anchors home post) — NO new NavMeshAgent.
        }
    }
}
```

> **Note on the stand-in path:** WO-117's `ResourceNode.AccrueTick` already clamps to `storeCap` and
> no-ops when `Data == null || IsFull`, so driving it from here is safe and reuses WO-117's banking,
> fill indicator, and store entirely. The pet stand-in is *slower* purely via `_standInRateFraction`.
> **CLI: confirm with WO-117's author whether stand-in should drive `AccrueTick` directly or instead set
> a node-side "pet rate" so `RatePerSecond` reflects it uniformly — prefer the latter if WO-117 grows a
> setter, to keep one accrual source. Either is design-acceptable; pick the one that keeps a single tick.**

### 3c. `PetHarvestRegistry` — the WO-115 seam (Village)

WO-115 already calls `PetHarvestRegistry.HarvestingPets()` (null-safe no-op today). This WO provides it —
a tiny static/registry that lists active tending pets and their `RatePerSecond` + `ResourceType`.

```csharp
namespace DeNelle.Village
{
    /// <summary>Read-only registry of pets currently tending a node — consumed by
    /// OfflineHarvestService (WO-115) to accrue pet harvest while the app is away.</summary>
    public static class PetHarvestRegistry
    {
        // Add/Remove called by PetHarvestAssignment.Assign/Release.
        public static System.Collections.Generic.IReadOnlyList<PetHarvestAssignment> HarvestingPets() { /* ... */ return null; }
    }
}
```

### 3d. Optional per-pet flavor — ONE additive field on `PetData` (keep v1 uniform)

Per the brief, v1 stays **simple/uniform** (every pet boosts +30% / stands in at 50%). If the owner wants
a *tiny* element-themed differentiator later, add **one optional additive field** to the existing
`PetData` SO — do **not** add a new SO, and do **not** touch the WO-58 aura fields:

```csharp
// Assets/Data/PetData.cs — DeNelle.Data — ADD (optional, v1 can leave at default 1.0):
[Header("Harvest (WO-119) — optional per-pet tend flavor")]
[Tooltip("Per-pet multiplier on the tend boost. 1.0 = uniform v1. e.g. Flame Pup 1.1 = a touch faster.")]
public float harvestBoostMultiplier = 1f;
```

`PetHarvestAssignment` would read `petData.harvestBoostMultiplier` if a `PetData` is assigned and fold it
into `_boostMultiplier` (default 1.0 = no change → uniform v1). **This is optional and additive; leaving
it at 1.0 for all three pets is the shipping default.** Suggested later flavor (owner's call, NOT v1):
Aether Sprite tends Crystal best, Flame Pup tends faster on warm nodes, Ice Wolf steadiest — purely
cosmetic-tier % nudges, never enough to invalidate the tend/defend trade-off.

---

## 4. Ties to neighbouring systems (do NOT duplicate their state)

- **WO-117 (the harvest seam):** this WO **consumes** `ResourceNode.SetBoost(float)`, `RatePerSecond`,
  `IsClaimed`, `IsCollecting`, and `AccrueTick(dt)`. It adds NO new node, store, banking, fill indicator,
  or risk code. The boost is the WO-117-provided `_boostMultiplier` hook (WO-117 §2c explicitly names this
  the "WO-111 P4 pet hook" — this WO is what plugs into it).
- **WO-115 (offline accrual):** this WO **provides** `PetHarvestRegistry.HarvestingPets()` +
  `PetHarvestAssignment.RatePerSecond`/`ResourceType` so WO-115's already-written null-safe loop starts
  returning real pet accrual. Keep the seam read-only and null-safe (WO-115 no-ops if absent).
- **WO-112 (claim gate):** a pet can tend **only a claimed node** (`node.IsClaimed`). Read the flag; do
  not re-derive ward state. Unclaimed → refuse the assignment.
- **WO-58 (pet combat aura) — DO NOT TOUCH:** the aura is a combat buff driven by
  `PetData.level{1,3,5}EmissionRate` while the pet is *defending*. A tending pet simply isn't defending,
  so the aura naturally goes quiet with it — but **no aura code, emission field, or `PetAuraVFX` is
  edited by this WO.** The only contact with `PetData` is the optional new `harvestBoostMultiplier` field
  (§3d), which is unrelated to the aura block.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Harvest/PetHarvestAssignment.cs` | **Create** — the tend behaviour: assign/release, boost via `node.SetBoost()`, stand-in accrual, WO-115 rate seam |
| `Assets/_Modules/Village/Harvest/PetHarvestRegistry.cs` | **Create** — read-only registry of tending pets for WO-115 offline accrual |
| `Assets/_Modules/Pets/Pet.cs` | **Edit (one line)** — add `Tend = 3` to `PetMode` (or, if CLI prefers zero Pets-assembly edits, reuse `Idle` from Village and leave `Pet.cs` untouched — §3a) |
| `Assets/Data/PetData.cs` | **Edit (optional)** — add additive `harvestBoostMultiplier` (default `1f`); **do NOT touch the WO-58 aura fields** |
| `Assets/_Modules/Village/Harvest/UI/PetTendAffordance.cs` | **Create (optional, code-built)** — small "tend / will not fight" assignment hint; reuse WO-117's node-UI pattern, no UXML |

**Assembly discipline (CLAUDE.md §5):** `PetHarvestAssignment` + `PetHarvestRegistry` live in
`DeNelle.Village` (they see both `Pet` and `ResourceNode` — same reason `PetContextualBehaviour` is in
Village). The optional data field is on `PetData` in `DeNelle.Data`. **Village → Core/Pets only; Pets
never references Village.** Any HUD/Audio surfacing goes through `CoreServices.Hud?` / `CoreServices.Audio?`
with `?.`. **No new `System.Reflection`.** UI is **code-built** (no UXML — PIPELINE_STATE.md §8). Run the
brace-balance gate on every `.cs` touched.

---

## Acceptance Criteria

- [ ] A pet can be **assigned to tend** a **claimed** `ResourceNode` (WO-112 gate respected; unclaimed nodes refuse)
- [ ] A tending pet **stops defending** — it does not hunt enemies (bible: "cannot do both at once"); achieved with **no change to the WO-58 combat/aura path**
- [ ] Reassigning a tending pet back to a slot **releases the boost** (`node.SetBoost(1f)`) and restores `PetMode.Defend`
- [ ] **Boost (effect a):** worker + tending pet → node collect rate raised by the tunable % (default +30%) via WO-117's `SetBoost` seam — no new boost path
- [ ] **Stand-in (effect b):** claimed node with a tending pet but **no worker** accrues at the tunable fraction of `baseRatePerSecond` (default 50%) through WO-117's existing `AccrueTick`/store — slower than a worker, no second accrual system
- [ ] A node with a tending pet **counts for WO-115 offline accrual** — `PetHarvestRegistry.HarvestingPets()` + `PetHarvestAssignment.RatePerSecond`/`ResourceType` return real values; WO-115's null-safe loop now banks pet haul
- [ ] **v1 is uniform** across the three pets (same boost % and stand-in fraction); any per-pet flavor is the optional additive `PetData.harvestBoostMultiplier` left at `1f`
- [ ] The tend/defend **trade-off is surfaced** to the player (UI hint or bark: "this pet will tend, not fight")
- [ ] WO-58 pet combat aura is **unchanged** — no aura field, `PetAuraVFX`, or emission code edited
- [ ] Movement reuses `Pet`'s existing drift (home-post re-anchor, like `PetHeroLeash`) — **no new `NavMeshAgent`** added to the pet
- [ ] Brace balance passes on every `.cs` touched; cross-module calls use `?.`; Village → Core/Pets only; Pets does not reference Village

---

## Do NOT touch

- **Do NOT break or modify the WO-58 pet combat aura** — no edits to `PetData.level{1,3,5}EmissionRate`,
  `enableOrbitSparksAtL5`, `PetAuraVFX`, or any aura emission code. A tending pet simply isn't defending;
  the aura goes quiet *because the pet left the line*, not because this WO changed aura logic.
- **Do NOT redesign or duplicate the WO-117 harvest loop** — no new `ResourceNode`, store, banking, fill
  indicator, or risk/raid code. Consume `SetBoost()` / `RatePerSecond` / `IsClaimed` / `IsCollecting` /
  `AccrueTick`. Add NO second accrual path.
- **Do NOT add a second movement system to the pet** — reuse `Pet`'s existing kinematic drift /
  home-post re-anchor (as `PetHeroLeash` does). No `NavMeshAgent` on the pet.
- **Do NOT reference `DeNelle.Village` from `DeNelle.Pets`** — the tend behaviour lives in Village (it
  sees both `Pet` and `ResourceNode`), exactly like `PetContextualBehaviour`. The only Pets-assembly edit
  is the optional one-line `PetMode.Tend` enum value, and even that is avoidable (§3a).
- **Do NOT let a pet tend AND defend at once** — that is the one bible rule this WO enforces. Assigning to
  tend must clear the defensive slot, and vice-versa.
- **Do NOT duplicate the WO-112 ward/claim state or the WO-115 offline math** — read the claim flag,
  expose a read-only registry seam; do not re-implement either.
- **Do NOT build the assignment UI in UXML** — code-built only (PIPELINE_STATE.md §8).
- **Do NOT make tending punishing** — telegraphed, recoverable (a tending pet that flees a raid stops
  tending for the duration, never permanently lost by default), mirroring WO-117's worker-flee posture.
- **Do NOT introduce `System.Reflection`** in these scripts.
- Do not touch ATB, WalletService, monetization, or clan code.

---

🤖 Spec'd by the design lane (UI). Reconciled against WO-117 (consumes its `ResourceNode.SetBoost` /
`RatePerSecond` / `IsClaimed` / `IsCollecting` / `AccrueTick` seams — does not rebuild the harvest loop),
WO-115 (provides the `PetHarvestRegistry.HarvestingPets()` seam its pet accrual no-ops against today),
WO-112 (claimed-node gate), the `narrative-bible.md` §5 "a spirit at a building tends / cannot do both"
rule, `NORTH_STAR.md` (pets auto-harvest/boost), and the existing `Pet` / `PetData` / `PetMode` /
`PetContextualBehaviour` code (confirmed: no Tend mode and no pet-harvest behaviour exist today; WO-58
aura is a separate combat buff, untouched here). Markdown work order only — no `.cs` touched, no bake fired.
