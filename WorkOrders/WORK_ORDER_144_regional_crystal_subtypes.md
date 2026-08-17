<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 144 — Regional Crystal Subtypes: Danger Gates Reward (the risk/reward spine of the build-up economy)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-30 (Fri)
**Priority:** High — the **risk/reward spine** of the harvest→build economy. Owner ask (verbatim): *"should have crystals maybe sub types only in regions with more hordes or higher levels"* — rarer, more-valuable crystal **subtypes** harvestable only in the more dangerous, higher-level regions. Danger ⇄ reward.
**Lane:** **economy / gameplay code (CLI)** — pure data + a thin grade-aware extension of the existing crystal wallet + a node-side region gate. **NOT the frozen `VillageSceneBuilder`; no bake; no `Village.unity` hand-edit.**
**Scope:** Small–Medium, additive. ONE Core enum (`CrystalGrade`), ONE Core ledger type (per-grade crystal counts that **extends, not replaces** `GameState.AetherCrystals`), TWO fields on the WO-141 `HarvestNodeData` SO, a region-gate check on the WO-141 spawner, and the grade-aware bank/spend seams. No new top-level currency, no new assembly, no UXML, no scene edit, no bake.
**Depends on:**
- **WO-141** (Harvestable Resource Nodes) — the `ResourceNode` / `HarvestNodeData` model + the `Extract()` → `GameState` bank path. Crystal subtypes **plug into this node model**; this WO adds the *grade* dimension to the existing Crystal `ResourceType`.
- **WO-107** (Climate Regions + `ZoneManager`) — the four region identities + the N/E/S/W classifier (`ZoneManager.GetZone(Vector3)`) that gates which grade spawns where.
- **WO-142** (Outer World Regions) — the lived-in region identities (Goldfields/Stoneback/Mirewood/Ashwood, warmth-in/dread-out) the grades are themed to.
**Soft-ties (provide seams, do NOT implement):**
- **WO-143** (roaming raids / outer-world danger — *being written in parallel*): danger ⇄ crystal richness must **correlate**. This WO exposes a read-only `CrystalGradeFor(region/tier)` mapping WO-143 can read to scale raid pressure to grade richness. Do not implement raid logic here.
- **WO-114** (Wall Upgrade Tiers): the Reinforced tier already costs **40 Crystals** — the canonical rare-crystal **sink** (see §4). Do not edit WO-114 here; this WO makes the rare grade the thing that pays for it.
- **WO-137** (Catalog data model) + **WO-108** (build mode): higher build/upgrade tiers debit the crystal wallet; rare grades unlock the top of those ladders.
- **WO-117** (worker dispatch) / **WO-119** (pet auto-harvest) / **WO-115** (offline accrual): all read the same `ResourceNode` — a grade-stamped node banks its grade through the same `Bank()`/`Extract()` faucets, no fork.
**North Star:** `docs/NORTH_STAR.md` core loop **BUILD → HARVEST → DEFEND → OFFLINE**; line 56 *"HARVEST resource nodes … crystal"*; line 309 *"Wall tiers … paid from harvest (the CoC sink)"*; line 310 *"Harvest nodes — generalize `CrystalMine` → destructible auto-harvest mines you defend"*; and the **Pi utility-sink + tournament** economy (lines 177/229–252): AetherCrystals is the off-chain premium currency — rare grades are the high-end of that sink, the "go fortify it" reward that keeps the dangerous regions worth the risk.

---

## Goal

Crystals stop being one undifferentiated number. A **crystal `ResourceType`** now carries a **grade** (a small tiered set: common **Aether** → rarer regional variants). **Higher-grade crystals only drop from nodes in the more dangerous, higher-level regions** — the danger of the region is the gate on the reward. Grades feed the *top* of the build/upgrade ladder (the Reinforced wall, late tower empowerment, premium catalog unlocks, the Pi-sink), so the player has a concrete reason to walk into Ashwood and hold a node there.

This is **NOT a parallel crystal system.** Crystals already exist (`GameState.AetherCrystals`, `CrystalMine`, `CrystalEconomy.AddCrystals`). This WO adds a **grade dimension** to that one wallet and gates *which grade a node yields* by region — additive, reconciled, no second currency.

---

## Reconciliation — what already exists (read before writing; build ON TOP, never duplicate)

Verified by inspection of `GameState.cs`, `NestedTypes.cs`, `CrystalEconomy.cs`, `CrystalMine.cs`, `GameStateService.cs`, WO-141/117/124/114/122/107/142, `NORTH_STAR.md`, CLAUDE.md §5/§6.

| Need | Exists? | Where / note |
|---|---|---|
| Crystal wallet | **BUILT — no new currency** | `Assets/_Modules/Core/State/GameState.cs:52` `public int AetherCrystals = 0;`. This is the **total** crystal balance. Subtypes do **not** replace it — see §1 (grade ledger is additive; `AetherCrystals` stays the sum). |
| Canonical crystal award path | **BUILT** | `Assets/_Modules/Village/CrystalEconomy.cs:106` `AddCrystals(int)` (writes `GameState.AetherCrystals` + saves). Also `GameStateService.cs:255 AddCrystals(int)`. WaveManager (`WaveManager.cs:872`), KillComboTracker, CrystalMine (`CrystalMine.cs:160`) all award through it. **Every grade still routes through this** so the total stays correct; the grade ledger is updated alongside. |
| `ResourceType` enum (Core) | **greenfield — owned by WO-117** | `enum ResourceType { Wood, Food, Crystal, Ore }` (WO-117 §2a / WO-124 / WO-141 §2a). **Crystal is already a member.** This WO does **NOT** add new `ResourceType` members per grade — grade is a SECOND, orthogonal enum (§1). Do not fork `ResourceType`. |
| Node model (`ResourceNode` + `HarvestNodeData`) | **greenfield — owned by WO-141/117** | `Assets/_Modules/Village/Harvest/ResourceNode.cs` + `HarvestNodeData.cs` (per WO-141 §2b/2c). A node has `resourceType` + `prefabKey` + `worldPosition`. This WO adds two fields to the SO: `crystalGrade` + `minRegionTier` (§3). |
| Node spawner + region classifier | **greenfield (WO-141 spawner) / BUILT (WO-107)** | WO-141 `HarvestNodeSpawner` instantiates nodes from SOs at `worldPosition`. WO-107 `ZoneManager.GetZone(Vector3)` (`Assets/_Modules/Environment/ZoneManager.cs:159`) classifies a world position into N/E/S/W zone. The spawner consults the region→tier map before spawning a graded node (§3). |
| Region identities + tiers | **BUILT (WO-107) / dressed (WO-142)** | WO-107: N=Corrupted Ashwood, E=Goldfields, W=Stoneback Ridge, S=Mirewood; 80m reach; `spawn-0..3`. WO-142 dread ladder: Goldfields **low** → Stoneback **neutral** → Mirewood **heavy** → Ashwood **front**. This WO maps dread → grade (§2). |
| Wall-tier crystal sink | **spec'd (WO-114)** | WO-114 Reinforced tier costs **40 Crystals** + Iron (`WORK_ORDER_114 §1`). The rare grade is what that sink consumes (§4). `ResourceCost` (Wood/Stone/Iron/Crystals) lives in `DeNelle.Village` (EconomyService). |
| Crystal visual life | **BUILT (WO-122)** | `CrystalVisual.cs` (spin + palette pulse via MaterialPropertyBlock). Grade nodes re-tint via the SAME `CrystalVisual` palette (§5) — no new visual system. |
| Premium / Pi sink | **vision (NORTH_STAR)** | AetherCrystals is the off-chain premium currency; Pi is the buy-in/accumulation rail (NORTH_STAR §229–252). Rare grades are the high end of the crystal sink the Pi economy sits over (§4). No Pi/Wallet code touched here. |

**So the new work is: ONE `CrystalGrade` enum + a small additive per-grade ledger that rides alongside `AetherCrystals` (the total) + TWO fields on the WO-141 SO + a region-tier gate in the WO-141 spawner + grade-aware bank/spend seams. No new top-level currency, no `ResourceType` fork, no new assembly, no scene edit, no bake.**

---

## CRITICAL — Subtypes are a GRADE on Crystal, NOT new `ResourceType` members

The trap (memory *core-namespace-shadows-unityengine-statics* / *wo-batch-reconcile-not-replace*): do **not** add `CrystalCommon`, `CrystalRare`, … to `ResourceType`. That would fork the enum WO-117/124/141 all consume, churn the HUD (WO-124 displays four wallets), and force a new `GameState` int per grade + a SaveSchema bump per grade.

**Resolution (decide before coding):**
- `ResourceType.Crystal` stays **one** resource type (one HUD wallet, one save total).
- **Grade is a second, orthogonal enum `CrystalGrade`** in `DeNelle.Core`. A crystal is `(ResourceType.Crystal, CrystalGrade.X)`.
- `GameState.AetherCrystals` remains the **TOTAL** crystal balance (back-compat: every existing caller keeps working; the HUD keeps showing one crystal number). The **per-grade breakdown** is an additive ledger (§1) that sums to `AetherCrystals`.
- This keeps **one wallet, graded** — exactly the WO-114/141 discipline ("reuse the field, don't add a parallel one").

> **Flag for owner:** two viable storage shapes for the per-grade breakdown — (A) a `CrystalLedger` value-type on `GameState` (one `int` per grade, `AetherCrystals` kept as the cached sum) requiring a small SaveSchema add; or (B) keep grades **session-only / derived** for the first cut (rare grades spent the run they're earned; only the `AetherCrystals` total persists). **Recommend (A)** so a rare-grade stockpile survives a session (the whole point of "go fortify the dangerous region"), but it adds one nested save object. Ship (B) if a SaveSchema bump is unwanted this cut, and promote to (A) when persistence is greenlit. Either way `AetherCrystals` stays the authoritative total. **See §6 persistence.**

---

## 1. Crystal grade model — `DeNelle.Core` (pure data; CLI writes final code)

Assembly discipline (CLAUDE.md §5): **enum + pure data in `DeNelle.Core`**; runtime award/spend in `DeNelle.Village`. Village → Core only; Core can NOT reference Village (memory *core-cannot-reference-village-award-crystals-via-gamestate* → write `GameStateService.Instance.State.*` directly / go through `CrystalEconomy`).

### 1a. `CrystalGrade` enum — `DeNelle.Core`

```csharp
// Assets/_Modules/Core/CrystalGrade.cs
namespace DeNelle.Core
{
    /// <summary>Quality grade of a Crystal-type resource. Orthogonal to ResourceType:
    /// a crystal is always ResourceType.Crystal; CrystalGrade is its rarity band.
    /// Higher grades only drop from higher-danger regions (WO-107/142/143). Order = rarity.</summary>
    public enum CrystalGrade
    {
        Aether   = 0,   // common — the baseline crystal (today's AetherCrystals); any region
        Verdant  = 1,   // Goldfields/Stoneback — low/neutral danger
        Mire     = 2,   // Mirewood — heavy danger
        Wraith   = 3    // Corrupted Ashwood — the front line; rarest, richest
    }
}
```

> Names are themed to the WO-142 region tonal gradient (Aether=baseline, Verdant=living lands, Mire=the murk, Wraith=the rot). Owner may rename; the **order = rarity = danger** is the load-bearing part. Keep the set **small (4)** to avoid HUD/balance sprawl.

### 1b. Per-grade ledger — additive on `GameState` (Core), `AetherCrystals` stays the total

```csharp
// Illustrative — recommend option (A) in the CRITICAL section.
// Lives in DeNelle.Core (NestedTypes.cs sibling), NOT a new top-level currency.
namespace DeNelle.Core.State
{
    [System.Serializable]
    public struct CrystalLedger   // sums to GameState.AetherCrystals (the cached total)
    {
        public int aether;   // CrystalGrade.Aether
        public int verdant;  // CrystalGrade.Verdant
        public int mire;     // CrystalGrade.Mire
        public int wraith;   // CrystalGrade.Wraith

        public int Total => aether + verdant + mire + wraith;
        public int Get(CrystalGrade g) => g switch {
            CrystalGrade.Verdant => verdant, CrystalGrade.Mire => mire,
            CrystalGrade.Wraith  => wraith,  _ => aether };
        public void Add(CrystalGrade g, int n) { /* add to the matching field */ }
    }
}
```

- `GameState` gains `public CrystalLedger Crystals;` (name to avoid clashing with `ResourceBalance.Crystals` — pick `CrystalGrades` if `Crystals` collides; **flag for CLI to verify no name clash**, NestedTypes already has `ResourceBalance.Crystals` at L43).
- **Invariant:** `GameState.AetherCrystals == Crystals.Total` at all times. The award helper (§3) updates both. Existing callers that touch only `AetherCrystals` stay correct as the total; on the first session after upgrade, un-graded legacy crystals are treated as `Aether` (migration note §6).

---

## 2. Region → grade mapping (danger gates reward)

The mapping ties WO-107 region identity + WO-142 dread level to the grade a region's crystal nodes may yield. **A region yields its tier grade and any lower grade**, so common Aether is harvestable anywhere and the rare grades concentrate at the dangerous edges:

| Region (WO-107/142) | Dir | Dread (WO-142) | Region tier | Top crystal grade dropped here | Rationale |
|---|---|---|---|---|---|
| **Goldfields** | E | low | 0 | `Verdant` | the safe, peopled march — modest reward |
| **Stoneback Ridge** | W | neutral | 1 | `Verdant` | old, sparse; still safe-ish |
| **Mirewood** | S | heavy | 2 | `Mire` | oppressive; the main enemy lane (WO-107 §S) → richer |
| **Corrupted Ashwood** | N | front | 3 | `Wraith` | the front line, most hostile → rarest/richest |
| *(village interior / starter)* | — | — | 0 | `Aether` | `CrystalMine` baseline (today) stays `Aether` |

- **Classifier:** the WO-141 spawner calls `ZoneManager.Instance?.GetZone(node.worldPosition)` (WO-107 `ZoneManager.cs:159`, N/E/S/W by position) → region → `minRegionTier`. A graded node only spawns if the region's tier ≥ the node's `minRegionTier` (§3). Null-safe: if `ZoneManager` is absent, fall back to `Aether`-only (never error).
- **Correlation seam for WO-143:** expose a static read-only `CrystalRegion.TopGradeFor(int regionTier)` (Core, pure) so the roaming-raid danger system can scale pressure to grade richness — danger ⇄ reward stays a single source of truth, not two hand-tuned tables. WO-143 reads it; this WO does not implement raids.

---

## 3. Node integration — how a `HarvestNode` declares its grade + region restriction (WO-141)

The WO-141 `HarvestNodeData` SO gains **two fields** (additive — do not restate WO-141's fields):

```csharp
// added to Assets/_Modules/Village/Harvest/HarvestNodeData.cs (WO-141 SO) — additive
[Header("Crystal grade (WO-144 — only meaningful when resourceType == Crystal)")]
public DeNelle.Core.CrystalGrade crystalGrade = DeNelle.Core.CrystalGrade.Aether;
public int minRegionTier = 0;   // node only spawns if its region's tier >= this (0 = anywhere)
```

**Spawn rule (in WO-141's `HarvestNodeSpawner`, additive):**
1. Resolve the node's region tier from `ZoneManager.Instance?.GetZone(data.worldPosition)` (§2).
2. If `data.resourceType == ResourceType.Crystal` **and** `regionTier < data.minRegionTier` → **skip spawn** (or downgrade to `Aether` — owner's call; recommend **skip** so rare grades are genuinely region-locked). `LogWarning` once, never error.
3. Non-crystal nodes ignore `crystalGrade`/`minRegionTier` entirely (no behaviour change for Wood/Stone/Food).

**Extract/bank rule (in WO-141's `ResourceNode.Extract()` crystal branch, additive):**
- WO-141 already routes a Crystal extract through `CrystalEconomy.AddCrystals(amt)`. This WO adds, alongside it, a grade-aware update: `CrystalEconomy.Instance?.AddCrystals(amt, data.crystalGrade)` — an **overload** that (a) calls the existing `AddCrystals(amt)` (keeps `AetherCrystals` total + save correct) and (b) records the grade in the ledger (§1b). Single award path, graded. The HUD push (WO-124 `CoreServices.Hud?.SetResource(ResourceType.Crystal, total)`) is unchanged — it still shows one crystal number; an optional grade breakdown is a WO-124 follow-up, not this WO.

```csharp
// CrystalEconomy.cs (DeNelle.Village) — ADD an overload; do NOT change the existing AddCrystals(int)
public void AddCrystals(int amount, DeNelle.Core.CrystalGrade grade)
{
    AddCrystals(amount);                       // existing path: total + save (back-compat)
    var s = GameStateService.Instance?.State;  // Core can't ref Village; write state directly
    s?.Crystals.Add(grade, amount);            // §1b ledger; AetherCrystals stays == Crystals.Total
}
```

---

## 4. How subtypes feed the economy — the SINK (so rare grades aren't hoarded)

Rare grades buy what base Aether cannot. The sink lives in **already-planned systems** — this WO fills the graded wallet they read; it does not rebuild them:

| Sink | Owner WO | Grade gate (recommended; tune in data) |
|---|---|---|
| **Reinforced wall tier** | WO-114 (Reinforced = 40 Crystals + Iron) | require the 40 to include **Mire+** grade — the late wall is paid in danger-won crystal, not farm-safe Aether. (WO-114 `ResourceCost` is grade-blind today; add an optional `minCrystalGrade` to its cost check as a WO-114 follow-up — flag, don't edit WO-114 here.) |
| **Top tower empowerment / catalog tier** | WO-137 catalog + tower-empowerment-spec | apex defensive pieces (the WO-137 "dragon-tier" catalog entries) cost **Wraith** — only obtainable by holding an Ashwood node. Closes the placement=role thesis: the best piece needs the most dangerous harvest. |
| **Premium cosmetics / Pi-sink** | NORTH_STAR Pi economy (WalletService/PackStore) | rare grades are a **soft-currency mirror** of the premium tier — a player can grind Wraith in Ashwood OR buy the equivalent (the Pi/IAP path). Keeps F2P↔pay parity and gives the Pi-sink a non-pay earn route. **No Wallet/Pi code here** — just note the grade as the soft mirror. |
| **Forge / enchant (future)** | resource-idle-economy-roadmap | reserved: high grades feed later enchant tiers. Documented sink target, not built now. |

**Sink principle:** every grade must have a thing it *uniquely* buys (above), so it drains. Aether = bread-and-butter (towers, repairs). Verdant = mid upgrades. Mire = Reinforced wall / high tower. Wraith = apex catalog piece / top empowerment. No grade should be hoardable with nothing to spend it on (the anti-hoard rule).

---

## 5. Balance framing — the danger=reward curve

- **Richness scales with danger, two knobs, both data (WO-141 SO fields):** a higher-grade node has **higher `yieldPerExtract`** *and* **smaller `totalDeposit` + slower `respawnSeconds`** (WO-141 §2b). So a Wraith node pays big per pull but is scarce and slow — you must *hold the ground* to drain it (this is the WO-141 §5 / WO-143 "defend or lose it" tension target).
- **Recommended starting curve (tune in SO assets, no code branches — placement=role):**

| Grade | Region tier | `yieldPerExtract` | `totalDeposit` | `respawnSeconds` | Feel |
|---|---|---|---|---|---|
| Aether | 0 (anywhere) | 5 | 200 | 60 | safe, plentiful |
| Verdant | 0–1 | 8 | 120 | 120 | step out, modest reward |
| Mire | 2 | 14 | 70 | 240 | risky, juicy |
| Wraith | 3 | 24 | 40 | 480 | fortify-it-or-lose-it |

- **Anti-trivialise:** rare grades must **not** be buyable with Aether (no in-economy grade exchange) — the only way to a Wraith crystal is harvesting Ashwood (or the premium/Pi mirror, §4). That preserves "danger gates reward."
- **Sink targets above** ensure each grade drains; if a grade has no live sink yet (future forge), cap its hoard or hold it back from the spawn set until its sink ships (avoid dead inventory).

---

## 6. Persistence (reuse `AetherCrystals` as the total; ledger is the additive part)

- `GameState.AetherCrystals` (`GameState.cs:52`) **stays the authoritative crystal total** — already saved/loaded/reset/synced (`SaveSchema.cs:150`, `GameStateService.cs:279/332/525/674`). **No change to that field or its round-trip.**
- Option (A) adds a `CrystalLedger Crystals;` to `GameState` + a small `crystalGrades` object to `SaveSchema` (one nested int-bag) with the **invariant** `AetherCrystals == ledger.Total`. **Migration:** on load, if the ledger object is absent (old save) or sums to less than `AetherCrystals`, fold the difference into `Aether` (legacy crystals are common-grade). This is the only save-shape change and it is purely additive — **flag for CLI** to add a `SaveSchema` field + a `NonNegInt`-style clamp (mirror `SaveSchema.cs:222`) and a reset line (zero the ledger alongside `AetherCrystals = 0` at `GameStateService.cs:525`).
- Option (B) (session-only) needs **zero** SaveSchema change — the ledger lives on the runtime `GameState` instance only; rare grades are spent the run they're won. Ship (B) if no SaveSchema bump is wanted this cut.
- **Do NOT** add a top-level currency field per grade (the WO-114/141 anti-pattern). One total + one nested breakdown.

---

## Assembly placement (CLAUDE.md §5/§6)

- `CrystalGrade` enum + `CrystalLedger` struct + `CrystalRegion.TopGradeFor(tier)` helper → **`DeNelle.Core`** (`Assets/_Modules/Core/CrystalGrade.cs`, and the ledger beside `NestedTypes.cs`). Pure data/logic, no UnityEngine gameplay refs.
- `CrystalEconomy.AddCrystals(int, CrystalGrade)` overload + the spawner region-gate + the SO fields → **`DeNelle.Village`** (`CrystalEconomy.cs`, WO-141 `HarvestNodeSpawner.cs`, `HarvestNodeData.cs`).
- **Village → Core only.** Crystal award still routes through `CrystalEconomy` / `GameStateService.Instance.State` (Core can't ref Village). HUD/Audio only via `CoreServices.*?.` with `?.`.
- **No `System.Reflection`** introduced (memory *reflection-bridge-pattern* is editor-only).
- `ZoneManager` lives in `DeNelle.Environment`; the spawner (Village) referencing it must respect existing asmdef refs — if Village does not already ref `DeNelle.Environment`, gate the call behind a null-safe lookup (`FindObjectOfType`) or a Core-side seam rather than adding a new hard asmdef edge. **Flag for CLI** to confirm the existing Village↔Environment ref before wiring `ZoneManager` directly.

---

## Files to Create / Edit

| File | Action | Note |
|---|---|---|
| `Assets/_Modules/Core/CrystalGrade.cs` | **Create** | `enum CrystalGrade { Aether, Verdant, Mire, Wraith }` + `CrystalRegion.TopGradeFor(int tier)` pure helper. |
| `Assets/_Modules/Core/State/NestedTypes.cs` | **Edit (additive)** | Add `CrystalLedger` struct (§1b) — option (A). Verify no name clash with `ResourceBalance.Crystals` (L43). |
| `Assets/_Modules/Core/State/GameState.cs` | **Edit (additive, option A)** | Add `public CrystalLedger Crystals;` (or `CrystalGrades`). Keep `AetherCrystals` as the total. NO removal/rename of `AetherCrystals`. |
| `Assets/_Modules/Core/State/SaveSchema.cs` | **Edit (additive, option A only)** | Add a `crystalGrades` nested object + clamp (mirror `NonNegInt`, L222). Migration: absent → fold into Aether. Skip entirely for option (B). |
| `Assets/_Modules/Core/State/GameStateService.cs` | **Edit (additive, option A only)** | Round-trip the ledger in save-snapshot (L279), patch (L332), reset (L525 → zero ledger), server-sync (L674). Mirror the `AetherCrystals` lines. |
| `Assets/_Modules/Village/CrystalEconomy.cs` | **Edit (additive)** | Add `AddCrystals(int, CrystalGrade)` overload (§3). Do NOT change existing `AddCrystals(int)`. |
| `Assets/_Modules/Village/Harvest/HarvestNodeData.cs` | **Edit (additive — WO-141 SO)** | Add `crystalGrade` + `minRegionTier` fields (§3). |
| `Assets/_Modules/Village/Harvest/HarvestNodeSpawner.cs` | **Edit (additive — WO-141 spawner)** | Region-tier gate before spawning a graded crystal node (§3); null-safe `ZoneManager` lookup. |
| `Assets/_Modules/Village/Harvest/ResourceNode.cs` | **Edit (additive — WO-141 node)** | Crystal `Extract()` branch calls the graded overload. |
| `Assets/Data/HarvestNodes/Node_Crystal_*.asset` | **Create (data, after WO-141 lands)** | Per-grade crystal node SO instances (e.g. `Node_Crystal_Mire`, `Node_Crystal_Wraith`) with the §5 curve + `minRegionTier`. Defer if WO-141 deferred Crystal nodes (see WO-141 Crystal reconciliation). |
| `WORK_ORDER_114` / `WORK_ORDER_137` | **Reference only — do NOT edit** | Note the grade-gate follow-up for each sink (§4); they own their own files. |
| `CrystalMine.cs` | **Reference only — do NOT edit** | Stays `Aether` baseline (WO-141 keeps `CrystalMine` as the crystal source for the first cut; no double-pay). |

---

## What NOT to touch

- **Do NOT add new `ResourceType` members per grade** — grade is the orthogonal `CrystalGrade` enum; `ResourceType.Crystal` stays one type (one HUD wallet, one save total). (CRITICAL section; memory *core-namespace-shadows-unityengine-statics*.)
- **Do NOT add a new top-level currency or a `GameState int` per grade** — one total (`AetherCrystals`) + one additive nested ledger. (WO-114/141 anti-pattern.)
- **Do NOT remove, rename, or repurpose `GameState.AetherCrystals`** — every existing caller (WaveManager, KillCombo, CrystalMine, BattlePass, AdminOverlay) depends on it; it stays the authoritative total.
- **Do NOT change the existing `CrystalEconomy.AddCrystals(int)`** — add an overload; the legacy path must stay byte-for-byte behaviour-compatible.
- **Do NOT create a second/parallel crystal income path** — leave `CrystalMine.cs` as the Aether baseline (WO-122/141); no double-pay.
- **Do NOT implement an in-economy grade exchange** (buy Wraith with Aether) — that breaks "danger gates reward."
- **Do NOT edit `VillageSceneBuilder.cs`, hand-edit `Village.unity`, or fire any bake** (CLAUDE.md §3/§9). Nodes spawn at runtime (WO-141 path).
- **Do NOT edit WO-114 / WO-137 / WO-124 files** — note the grade-gate follow-ups; those WOs own their sinks/HUD.
- **Do NOT implement WO-143 roaming raids, WO-117 workers, WO-119 pets, or WO-115 offline accrual** — expose the `TopGradeFor` / graded-`Bank` seams only.
- **Do NOT touch WalletService / PackStore / Pi / monetization / ATB / clan / backend** — note the soft-currency mirror only; no code.
- **Do NOT introduce `System.Reflection`** in these scripts.
- **Do NOT build any UI in UXML** (PIPELINE_STATE.md §8) — HUD push stays the existing `CoreServices.Hud?.SetResource(...)` seam; grade breakdown UI is a WO-124 follow-up.

---

## Acceptance Criteria

- [ ] `CrystalGrade` enum present in `DeNelle.Core` (`Aether`/`Verdant`/`Mire`/`Wraith`, order = rarity = danger); a pure `CrystalRegion.TopGradeFor(int regionTier)` helper exists for WO-143 to read.
- [ ] **No new `ResourceType` member** — `ResourceType.Crystal` stays one type; grade is the orthogonal enum.
- [ ] `GameState.AetherCrystals` is **unchanged** and remains the authoritative crystal **total**; the per-grade ledger (option A) sums to it (invariant `AetherCrystals == ledger.Total`), or is session-only (option B) — owner picks; either way no top-level per-grade currency field.
- [ ] `CrystalEconomy.AddCrystals(int, CrystalGrade)` overload exists and (a) calls the existing `AddCrystals(int)` unchanged and (b) records the grade; existing `AddCrystals(int)` callers are byte-for-byte unaffected.
- [ ] `HarvestNodeData` (WO-141 SO) gains `crystalGrade` + `minRegionTier` (additive); non-crystal nodes ignore them (no behaviour change for Wood/Stone/Food).
- [ ] **Region gate works:** a graded crystal node spawns ONLY where `regionTier >= minRegionTier` (via `ZoneManager.GetZone`); `Wraith` nodes spawn only in Ashwood (N), `Mire` only in Mirewood (S); `Aether` anywhere. Null `ZoneManager` → Aether-only fallback, never error.
- [ ] Harvesting a graded node increases the crystal **total** (`AetherCrystals`) AND records the grade in the ledger; HUD still shows one crystal number via the existing `CoreServices.Hud?.SetResource(...)` seam.
- [ ] **Danger=reward curve** is data-only (§5): higher grade = higher `yieldPerExtract`, smaller `totalDeposit`, slower `respawnSeconds`; no per-grade code branches (placement=role).
- [ ] **Sink documented + reachable:** at least the Reinforced-wall (WO-114) and apex-catalog (WO-137) grade gates are noted as follow-ups; no grade is left with zero sink in the shipped spawn set.
- [ ] **No in-economy grade exchange** (can't buy Wraith with Aether).
- [ ] Option (A) only: ledger round-trips through `SaveSchema`/`GameStateService` (save/patch/reset/sync), with the absent-ledger → Aether migration; reset zeroes the ledger with `AetherCrystals`.
- [ ] `DeNelle.Village` → `DeNelle.Core` only; crystal award via `CrystalEconomy`/`GameStateService.Instance.State`; all cross-module calls use `?.`; no `DeNelle.HUD` ref introduced.
- [ ] No `ResourceType` fork, no second crystal income path, no `CrystalMine`/`VillageSceneBuilder`/`Village.unity` edit, no bake, no UXML, no `System.Reflection`.
- [ ] **Brace balance passes on every `.cs` touched** (CLAUDE.md §1).

---

## Done checklist (CLAUDE.md §10)

- [ ] Brace balance check passed on every `.cs` file edited (`CrystalGrade.cs`, `NestedTypes.cs`, `GameState.cs`, `CrystalEconomy.cs`, WO-141 `HarvestNodeData.cs`/`HarvestNodeSpawner.cs`/`ResourceNode.cs`, and `SaveSchema.cs`/`GameStateService.cs` if option A).
- [ ] No `.unity` scene file hand-edited; no bake fired (nodes spawn at runtime via WO-141).
- [ ] No new `System.Reflection` usage introduced.
- [ ] `using DeNelle.Core.Combat;` — N/A (no `IDamageableStructure` here); `using DeNelle.Core;` present where `CrystalGrade` is used.
- [ ] Null-conditional operators (`?.`) used on all cross-module service calls (`ZoneManager.Instance?.`, `CrystalEconomy.Instance?.`, `GameStateService.Instance?.`, `CoreServices.Hud?.`).
- [ ] `AetherCrystals` unchanged as the total; ledger invariant verified; legacy-save migration folds into Aether.
- [ ] Acceptance criteria reviewed line by line.
- [ ] Coordinated with WO-141 (node model owner) so grade fields land additively on the ONE `HarvestNodeData`/`ResourceNode`, not a fork.

---

🤖 Spec'd by the economy/design lane (UI). Reconciled against: `GameState.cs:52` (`AetherCrystals` — kept as the authoritative total, no fork), `CrystalEconomy.cs:106` `AddCrystals(int)` (overloaded, not changed — the project-standard award path used by `WaveManager.cs:872` / KillComboTracker / `CrystalMine.cs:160`), `NestedTypes.cs:43` (`ResourceBalance.Crystals` — name-clash flagged), `SaveSchema.cs:150/222` + `GameStateService.cs:279/332/525/674` (existing crystal round-trip — ledger rides alongside additively), WO-141 (`ResourceNode`/`HarvestNodeData` node model — grade is two additive SO fields), WO-107 `ZoneManager.cs:159` (`GetZone` region classifier — the danger gate), WO-142 (region identities + warmth-in/dread-out tiers), WO-114 (Reinforced wall = 40 Crystals — the rare-grade sink), WO-137 (apex catalog sink), NORTH_STAR (BUILD→HARVEST→DEFEND→OFFLINE + Pi utility-sink). Grade is an ORTHOGONAL `CrystalGrade` enum (NOT new `ResourceType` members) per CLAUDE.md §5 + memory *core-namespace-shadows-unityengine-statics* / *wo-batch-reconcile-not-replace*. Markdown work order only — no `.cs` touched, no bake fired.
