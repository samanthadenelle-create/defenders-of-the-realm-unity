<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 151 — Village Progression & Crafting Depth (Warcraft-style: level the village → resource buildings → Forge/Armory → combat power)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-30
**Priority:** High — the "BUILD-UP → MAKE SAFE" half of the North Star core loop; the depth sink that gives the harvest (WO-141/117) and the combat (HeroHealth/HeroAbilities) a reason to keep going.
**Scope:** Large but cleanly phased + composition-based. ONE shared `BuildingUpgrade` system + ONE `VillageLevel` gate + per-building effect data + 4 thin combat/economy hookups. No new currency, no new assembly, no UXML, **no `VillageSceneBuilder` edit, no bake.**
**Lane:** design (owner + UI) · gameplay code (CLI). Pure code + SO + GameState fields — the **Combat/AI + Monetization-adjacent** lanes (CLAUDE.md §9), NOT the architect/VillageSceneBuilder lane.
**Depends on:**
- **WO-141 (harvest nodes)** / **WO-117 (worker dispatch)** — *produce* Wood/Stone/Food into `GameState`; this WO *spends* them. Soft dep: works with starting resources if harvest hasn't landed.
- **WO-150 (roster reconcile)** — the live 5-building roster (Store/Forge/Pet Home/Tower/Farm); this WO SPECS adding Lumbermill + Ironworks + Armory to `Buildings[]` (CLI adds later — builder is frozen).
- **Existing Tower-upgrade chain** (`TowerData.upgrades[]` / `TowerUpgrade.upgradeCost` / `TowerCombat`) and **WO-114 (wall tiers)** — the tier/cost/persist pattern this WO mirrors. Tower upgrade stays **as-is** (WO-150: "Tower → tower-upgrade KEEP as-is").
**North Star:** `docs/NORTH_STAR.md` core loop **BUILD → HARVEST → DEFEND → OFFLINE**; CoC×Warcraft base-building. Village level = the Town-Hall meta-gate; resource buildings + Forge/Armory = the upgrade ladder that turns the haul into survival.

---

## Owner vision (verbatim — honor precisely)

> "think warcraft style level the village then the farm wood iron"
> "increase damage on weapons decrease damage taken"
> "upgrade forge lumber mill and blacksmith to increase damage"
> "a well divided city where you can shop for missing materials, upgrade forge lumber mill and blacksmith to increase damage"
> "implement the wood forge and ironworks or whatever we use to strengthen depth"

**The systematized loop:**
`level VILLAGE → unlocks higher building tiers → resource buildings (Lumbermill/Ironworks/Farm) yield Wood/Iron/Food → spend at Forge (+weapon dmg) & Armory (−dmg taken) → hero hits harder + takes less → survive bigger waves/raids → earn more → level village again.`

---

## CRITICAL — RECONCILE, don't reinvent (this project's #1 trap). Verified by inspection.

I read `GameState.cs`, `EconomyService.cs`, `TowerData.cs`, `HeroHealth.cs`, `HeroAbilities.cs`, `PlayerAttackController.cs`, `HeroProgression.cs`, `HeroTalentModifiers.cs`, WO-114/141/117/150, and CLAUDE.md §5/§6 before writing. **Confirmed:**

| Need | State | Where (verified) |
|---|---|---|
| Multi-resource wallet (Wood/Stone/Iron/Crystals) + `CanAfford`/`TrySpend`/`Grant`/`ResourceCost` | **BUILT — reuse, do NOT fork** | `Assets/_Modules/Village/EconomyService.cs` (`ResourceCost` struct L59-82, `CanAfford`/`TrySpend` L140-161, `Grant` L164-177) |
| Persisted resource fields | **BUILT** | `Assets/_Modules/Core/State/GameState.cs`: `Wood` L58, `Stone` L54, `Iron` L56, `AetherCrystals` L52; `Resources` (`ResourceBalance` w/ `Food`) L48 |
| Tier/upgrade authoring pattern (per-level array + per-level `upgradeCost`) | **BUILT — mirror exactly** | `Assets/_Modules/Core/Data/TowerData.cs` (`upgrades[]` L32, `TowerUpgrade.upgradeCost` L62) |
| Wall-tier precedent (global level int + per-tier data + EconomyService spend + persist in GameState) | **SPEC'd (WO-114)** | `WORK_ORDER_114_wall_upgrade_tiers.md` — this WO follows its shape for buildings |
| Hero **outgoing-damage** multiplier seam (already a product of 3 factors) | **BUILT — add one factor** | `HeroAbilities.ResolveEffect` L278: `dmg = def.Damage * HeroTalentModifiers.DamageMultiplier(_heroClass) * levelMult * _pendingTimingBonus`. `HeroProgression.DamageMultiplier` L70. `PlayerAttackController.ResolveAttack` L142 (`float damage = _baseDamage; if (isPerfect) damage *= _perfectHitMultiplier;`) |
| Hero **incoming-damage** seam | **BUILT — single chokepoint** | `HeroHealth.TakeDamage(float amount)` L125 — every damage source funnels here (contact L120, `IDamageableStructure.ApplyContactDamage` L229) |
| Harvest produces the materials this WO spends | **SPEC'd (WO-141/117)** | banks to `GameState.Wood/Stone/Resources.Food` directly (Core can't ref Village) |
| Live roster (Forge=Workshop relabeled, Farm present; Lumbermill/Ironworks/Armory absent) | **BAKED (WO-150)** | `WORK_ORDER_150_skip_deleted_generators.md` — Lumbermill explicitly "LATER (not this WO)"; this WO is that later WO |
| Code-built world UI precedent (no UXML) | **BUILT** | `CrystalMine.InjectUpgradePanel()` / `BuildBubble()` |

**So the new work is:** ONE data-driven `BuildingUpgrade` component (composition, reused by every building), a `VillageLevel` meta-gate, per-building effect data, **3 thin combat/economy multiplier hooks**, GameState persistence (simple ints), and a SPEC for CLI to add 3 buildings to the roster later. **No new currency, no new assembly, no scene edit, no bake.**

> **Reconcile guard (memory *wo-batch-reconcile-not-replace*, *two-combat-feel-stacks*):** do NOT add a parallel damage system. The hero already multiplies talent × level × timing for outgoing and has ONE `TakeDamage` for incoming — this WO injects exactly ONE more global factor into each. Do NOT touch the Tower upgrade chain (`TowerData`/`TowerCombat`) — it stays as-is per WO-150.

---

## DECISION — Forge vs Blacksmith: TWO buildings (recommended)

Owner used both "Forge" and "Blacksmith/armor station." **Recommendation: two distinct buildings, because they map to the two opposite combat stats the owner named** ("increase damage on weapons" vs "decrease damage taken"):

- **Forge** (already in the roster as the relabeled "Workshop", WO-150) → **+weapon/ability damage** per tier. KEEP the existing building; this WO gives its crafting panel real teeth.
- **Armory** (new building; the "Blacksmith / armor station") → **−damage taken** per tier.

Two buildings reads cleaner in a "well-divided city" (a smithing quarter with a weapon-forge and an armor-works), gives the player two independent upgrade tracks, and avoids cramming opposite stats into one confusing panel. **If the owner prefers one building:** the system supports it trivially — one `BuildingUpgrade` whose effect data carries *both* a damage-mult and a damage-reduction curve. Flagged for owner; **building it as two is the recommended default.**

---

## 1. The shared upgrade system — `BuildingUpgrade` (composition, ONE system for all buildings)

**Principle:** ONE component on every upgradeable building, driven by per-building SO data. NOT a subclass per building (composition, not inheritance — owner directive in the brief). Mirrors `TowerData.upgrades[]` + WO-114's "global level int + per-tier data + EconomyService spend + GameState persist."

### 1a. `BuildingId` enum — `DeNelle.Core` (pure data, stable save key)

```csharp
// Assets/_Modules/Core/Buildings/BuildingId.cs   (DeNelle.Core)
namespace DeNelle.Core.Buildings
{
    /// <summary>Stable identity for an upgradeable village building (save-key + effect routing).</summary>
    public enum BuildingId { Lumbermill, Ironworks, Farm, Forge, Armory }
}
```
> Resource buildings: Lumbermill, Ironworks, Farm. Crafting buildings: Forge, Armory. (Store/Tower/Pet Home are NOT in this enum — Store is a shop, Tower keeps its own upgrade chain, Pet Home has its own skill-tree.)

### 1b. `BuildingTierData` SO — `DeNelle.Core.Data` (authoring; mirror `TowerData`)

```csharp
// Assets/_Modules/Core/Data/BuildingTierData.cs   (DeNelle.Core.Data)
using UnityEngine;
using DeNelle.Core.Buildings;

namespace DeNelle.Core.Data
{
    [CreateAssetMenu(menuName = "Defenders/Building Tier Data", fileName = "BuildingTierData")]
    public sealed class BuildingTierData : ScriptableObject
    {
        public BuildingId building;
        public string displayName = "Lumbermill";
        public BuildingTier[] tiers;   // [0] = built/level 1 baseline; [n] = higher tiers
    }

    [System.Serializable]
    public sealed class BuildingTier
    {
        public string tierName = "Tier 1";

        [Header("Village-level gate (the meta-spine, §2)")]
        public int requiredVillageLevel = 1;   // village must be >= this to buy this tier

        [Header("Upgrade cost INTO this tier (paid from EconomyService / GameState wallet)")]
        // ResourceCost is the EXISTING struct (Wood/Stone/Iron/Crystals) — DeNelle.Village.
        // NOTE assembly seam below: keep cost as 4 ints here, build ResourceCost in Village.
        public int costWood, costStone, costIron, costCrystals;

        [Header("Effect at this tier (read by §3 hooks — only the relevant fields matter per building)")]
        public float yieldPerCyclePerSec = 1f;  // resource buildings: +production rate (units/sec)
        public float weaponDamageMult    = 1f;  // Forge: outgoing-damage multiplier (1.0 = no bonus)
        public float damageTakenMult     = 1f;  // Armory: incoming-damage multiplier (1.0 = no reduction, 0.85 = -15%)
    }
}
```

> **Assembly seam (CLAUDE.md §5):** `ResourceCost` lives in `DeNelle.Village` (EconomyService.cs), but `BuildingTierData` is `DeNelle.Core` (Core can't ref Village). So the SO stores the cost as **4 plain ints** (`costWood/Stone/Iron/Crystals`); the Village-side `BuildingUpgrade` builds a `ResourceCost` from them when calling `EconomyService.TrySpend`. (WO-114 put `WallTier` in Core and `using DeNelle.Village;` for ResourceCost — that's a Core→Village ref which violates §5; **this WO avoids it with the 4-int approach.** Flag: if CLI prefers, move `ResourceCost` to `DeNelle.Core` and have EconomyService consume it — but that's a wider refactor; 4-ints is the low-risk path.)

### 1c. `BuildingUpgrade` MonoBehaviour — `DeNelle.Village` (the one component on every building)

```csharp
// Assets/_Modules/Village/Buildings/BuildingUpgrade.cs   (DeNelle.Village)
using UnityEngine;
using DeNelle.Core.Buildings;
using DeNelle.Core.Data;
using DeNelle.Core.State;   // GameStateService

namespace DeNelle.Village
{
    /// <summary>Data-driven per-building upgrade. ONE component, every upgradeable building
    /// uses it. Reads/writes the building's level on GameState (persisted), spends via
    /// EconomyService, and publishes its current-tier effect to BuildingEffects (§3).</summary>
    public sealed class BuildingUpgrade : MonoBehaviour
    {
        [SerializeField] private BuildingTierData _data;

        public BuildingId Id => _data != null ? _data.building : default;
        public int Level { get; private set; }          // 1-based; persisted (§4)
        public int MaxLevel => _data != null ? _data.tiers.Length : 1;
        public BuildingTier Current => (_data != null && Level >= 1 && Level <= _data.tiers.Length)
                                       ? _data.tiers[Level - 1] : null;

        public bool TryUpgrade()
        {
            if (_data == null || Level >= MaxLevel) return false;
            var next = _data.tiers[Level];                                  // tier we're buying INTO
            int villageLevel = VillageLevel.Current;                       // §2
            if (villageLevel < next.requiredVillageLevel) return false;    // META-GATE
            var econ = EconomyService.Instance; if (econ == null) return false;
            var cost = new ResourceCost(next.costWood, next.costStone, next.costIron, next.costCrystals);
            if (!econ.TrySpend(cost)) return false;                        // EXISTING spend path
            Level += 1;
            Persist();                                                     // §4
            BuildingEffects.Publish(Id, _data.tiers[Level - 1]);          // §3 — push new effect
            CoreServices.Audio?.PlaySfx(SfxId.Upgrade);                   // ?. always (CLAUDE.md §6)
            return true;
        }
        // Awake/Start: read persisted Level (§4), clamp 1..MaxLevel, BuildingEffects.Publish(current).
        // Persist(): write the per-building level int to GameState (§4).
    }
}
```

> The same `BuildingUpgrade` rides Lumbermill, Ironworks, Farm, Forge, Armory — the SO assigned in the prefab/builder decides which building it is and what its tiers do. **One system, many buildings.**

---

## 2. `VillageLevel` — the meta-gate (the Town-Hall spine)

The village level is the spine: leveling it **unlocks higher tiers for every other building** (the `requiredVillageLevel` gate in §1b). It is itself leveled by spending the haul + (recommended) a small `AetherCrystals` premium so it stays the deliberate, paced decision.

```csharp
// Assets/_Modules/Village/Buildings/VillageLevel.cs   (DeNelle.Village)
namespace DeNelle.Village
{
    /// <summary>The Town-Hall meta-progression. Its level gates every building's tier ceiling.</summary>
    public static class VillageLevel   // (or a MonoBehaviour singleton — CLI's call)
    {
        public static int Current => GameStateService.Instance?.State?.VillageLevel ?? 1;  // §4
        // TryLevelUp(): cost curve below; village level N is gated behind "all your other
        // buildings reaching ~level N-1" + a resource+crystal cost. On success: write
        // GameState.VillageLevel, fire SfxId.Upgrade, push a HUD ping via CoreServices.Hud?.
    }
}
```

**What leveling the village costs / gates (placeholder curve — designer tunes in the SO/constants):**

| Village Lv | Unlocks (building tier ceiling) | Cost to reach (placeholder) | Gate prerequisite (recommended) |
|---|---|---|---|
| 1 | All buildings tier 1 (start) | — | — |
| 2 | Building tier 2 | 200 Wood, 150 Stone | most buildings at tier 1 |
| 3 | Building tier 3 | 400 Wood, 300 Stone, 100 Iron | most buildings at tier 2 |
| 4 | Building tier 4 | 700 Wood, 500 Stone, 250 Iron, 20 Crystals | most buildings at tier 3 |
| 5 | Building tier 5 (cap) | 1200 Stone, 500 Iron, 60 Crystals | most buildings at tier 4 |

> "Warcraft style — level the village THEN the farm/wood/iron": the village level is the **ceiling**; resource/craft buildings climb *up to* that ceiling, then you raise the village again. This is the explicit progression cadence the owner asked for. Numbers are placeholders for playtest tuning — keep them in the SO / a `ProgressionConstants`, never hard-coded in logic.

---

## 3. Per-building effects + the EXACT combat/economy hookpoints

`BuildingEffects` is a tiny static registry (the cross-module seam — Village owns it; combat reads it). Each `BuildingUpgrade` publishes its current tier's effect; the combat/economy code reads the relevant scalar. **This is the only new "global modifier" surface — it injects ONE factor into each existing pipeline, no new combat system.**

```csharp
// Assets/_Modules/Village/Buildings/BuildingEffects.cs   (DeNelle.Village)
namespace DeNelle.Village
{
    /// <summary>Aggregates the live per-building effect scalars. Combat reads these;
    /// BuildingUpgrade publishes them on level-up + on load. All default to "no bonus"
    /// so absent buildings = baseline (fresh-clone / no-Armory safe).</summary>
    public static class BuildingEffects
    {
        public static float WeaponDamageMultiplier { get; private set; } = 1f;  // Forge
        public static float DamageTakenMultiplier  { get; private set; } = 1f;  // Armory (<=1 reduces)
        // Publish(BuildingId, BuildingTier): switch on id → set the matching scalar.
        // Resource yield is read per-node/per-building, not globalized here.
    }
}
```

### 3a. Forge → +weapon/ability damage (OUTGOING)

The hero's outgoing damage is already a product of factors. Add `BuildingEffects.WeaponDamageMultiplier` as one more:

- **`HeroAbilities.ResolveEffect`** (`Assets/_Modules/Village/Hero/HeroAbilities.cs` L278): the dmg line becomes
  `float dmg = def.Damage * HeroTalentModifiers.DamageMultiplier(_heroClass) * levelMult * _pendingTimingBonus * BuildingEffects.WeaponDamageMultiplier;`
  (same file/asmdef — `BuildingEffects` is `DeNelle.Village`, zero new ref.)
- **`PlayerAttackController.ResolveAttack`** (`Assets/_Modules/Village/Enemies/PlayerAttackController.cs` L142): `float damage = _baseDamage * BuildingEffects.WeaponDamageMultiplier;` (then the existing `if (isPerfect) damage *= _perfectHitMultiplier;`).
- **Tower damage is OUT of scope** — towers keep their own `TowerData.upgrades[].damage` chain (WO-150 "keep as-is"). Forge buffs the HERO's weapons/abilities, matching "increase damage on weapons." (Owner may later extend Forge to towers — flagged, not now.)

**Forge tier curve (placeholder):** Lv1 1.00× → Lv2 1.10× → Lv3 1.25× → Lv4 1.45× → Lv5 1.70×.

### 3b. Armory → −damage taken (INCOMING)

`HeroHealth.TakeDamage` (`Assets/_Modules/Village/Hero/HeroHealth.cs` L125) is the single incoming chokepoint — every damage source (contact L120, `IDamageableStructure.ApplyContactDamage` L229) funnels through it. Inject the reduction at the top:

```csharp
public void TakeDamage(float amount)
{
    if (_hp <= 0f || amount <= 0f) return;
    amount *= BuildingEffects.DamageTakenMultiplier;   // Armory: <=1 reduces (WO-151)
    _hp = Mathf.Max(0f, _hp - amount);
    ...
}
```
One line, one chokepoint — all incoming damage benefits. (`BuildingEffects` is same-asmdef `DeNelle.Village`.)

**Armory tier curve (placeholder, damageTakenMult):** Lv1 1.00 (0%) → Lv2 0.90 (−10%) → Lv3 0.80 (−20%) → Lv4 0.70 (−30%) → Lv5 0.60 (−40%). Clamp the floor (e.g. ≥0.4) so it can never reach invulnerability.

### 3c. Resource buildings → +yield/tier (Lumbermill/Ironworks/Farm)

These FEED the harvest economy, they don't replace it. A resource building's tier raises a **passive trickle** of its material into the wallet (a "production rate" that runs while in the village / accrues offline via WO-115), independent of the manual harvest nodes (WO-141) and worker dispatch (WO-117). The trickle is `BuildingTier.yieldPerCyclePerSec`:

- **Lumbermill** → `GameState.Wood` (banks directly — Core can't ref Village; same path WO-141/117 use).
- **Ironworks** → `GameState.Iron`.
- **Farm** → `GameState.Resources.Food`.

A small `ResourceBuildingProducer` (or fold into `BuildingUpgrade`) accumulates `yieldPerCyclePerSec * dt` and banks whole units periodically, pushing `CoreServices.Hud?.SetResource(...)` (WO-124 seam) so the wallet visibly ticks. **Reconcile with WO-117/141:** this is a *third faucet* into the same wallet (building-passive vs node-manual vs worker-auto) — NOT a new currency, NOT a duplicate node. If WO-117's `HarvestService` exists, the producer may register with it; otherwise it's a self-contained ticker. Keep it additive.

**Resource yield curve (placeholder, units/sec):** Lv1 0.2 → Lv2 0.4 → Lv3 0.7 → Lv4 1.1 → Lv5 1.6.

> **Tower-upgrade reuse note (memory + WO-114):** the upgrade *interaction* (tap building → panel → `CanAfford`-gated button → `TrySpend` → effect + SFX) is the exact UX the player already knows from `TowerUpgradeButton`/`TowerUpgradeMenu`. Reuse that flow shape for the building-upgrade panel; do not invent a new affordance.

---

## 4. Persistence — simple ints on GameState (no schema drama)

Per the brief: "these are simple ints/levels on the save state." Add a small persisted block to `GameState` (`Assets/_Modules/Core/State/GameState.cs`, the Village region near `WallLevel` L77-78):

```csharp
// ── Village progression (villageSlice) — WO-151 ──
public int VillageLevel = 1;                                  // §2 meta-gate
public SerializableDict<string, int> BuildingLevels = new SerializableDict<string, int>();
// key = BuildingId.ToString(), value = 1-based level. Absent key = level 1.
```

- **Why a dict not 5 fields:** matches the existing `BuildingCooldowns`/`BuildingDamage` `SerializableDict` pattern (GameState L80/L84) and is forward-proof as buildings are added — no schema field churn per building.
- **Save round-trip:** `VillageLevel` + `BuildingLevels` must be added to `SaveSchema` / `GameStateService` load+sync+reset (mirror exactly how `WallLevel` is round-tripped — WO-114 §7 cites `SaveSchema.cs:112`, `GameStateService.cs:282/335/528`). **This DOES require a SaveSchema field-add** (unlike WO-114 which reused an existing field) — bump per the existing migrator convention; CLI owns the schema-version call. Flag: confirm the schema-bump approach with whoever owns `SaveMigrator` before landing.
- **New Game** resets `VillageLevel = 1` and clears `BuildingLevels` (add to the reset carve-out alongside `WallLevel`).
- Reading: `BuildingUpgrade` reads `BuildingLevels[Id.ToString()]` on load (default 1); `VillageLevel.Current` reads `GameState.VillageLevel`.

---

## 5. Shop buys missing materials (the un-block valve)

So progression is never hard-blocked waiting on one resource, the **Store** (Market, already in the roster) sells Wood/Iron/Food for a soft/premium currency. Reconcile with the existing store, do NOT greenfield (memory *monetization-stack-already-built*; PIPELINE_STATE.md §8 PackStore exists at ~70%).

- Add a small **"Materials" stall** to the Store panel: buttons "Buy 50 Wood", "Buy 25 Iron", "Buy 50 Food" priced in a currency the player earns (recommend **`AetherCrystals`**, or soft Coins via `Resources.Coins` — owner's call; recommend Coins for soft-buy, Crystals for a premium bulk option).
- Flow: `if (spend currency ok) EconomyService.Instance?.Grant(wood:50)` (or write `GameState.Iron += 25` / `Resources.Food += 50` for the wallet not tracked by EconomyService). Push HUD via `CoreServices.Hud?.SetResource(...)`.
- This is a thin addition to the existing Store interactor/`MarketplaceInteractor` — a materials sub-panel, not a new store. **Do NOT rebuild PackStore.** Build the panel in code (no UXML — PIPELINE_STATE.md §8). If the store re-wire is risky to touch this WO, ship the materials-buy as a separate small step and flag it; the progression loop functions without it (just less forgiving).

---

## 6. New buildings to add to the roster (SPEC for CLI — builder is FROZEN)

`VillageSceneBuilder.cs` is the single-writer serialization bottleneck (CLAUDE.md §9; WO-150 just baked it). **This WO does NOT edit it.** It SPECS the additions for CLI to apply in a coordinated builder pass later:

| Building | New? | `Buildings[]` entry (spec) | Effect | Layout — "well-divided city / production cluster" |
|---|---|---|---|---|
| **Lumbermill** | **NEW** | Type=resource; id="lumbermill"; [F] → upgrade panel | +Wood yield/tier | **Production cluster** near the Store + Farm (the owner's "shop for missing materials" quarter) |
| **Ironworks** | **NEW** | Type=resource; id="ironworks"; [F] → upgrade panel | +Iron yield/tier | Production cluster (smithing quarter, beside Forge) |
| **Armory** | **NEW** (the "Blacksmith/armor station") | Type=craft; id="armory"; [F] → upgrade panel | −damage taken/tier | Smithing quarter, paired with the Forge |
| **Forge** | **EXISTING** (Workshop, relabeled WO-150) | id="forge"; [F] → upgrade panel (give it real effect) | +weapon dmg/tier | Smithing quarter (already placed) |
| **Farm** | **EXISTING** (in roster WO-150) | id="farm"; [F] → upgrade panel | +Food yield/tier | Already placed; pair with Lumbermill |

> **Layout intent (owner: "well divided city … production cluster near the store"):** group Farm + Lumbermill + Ironworks as a *production cluster* near the Store, and Forge + Armory as a *smithing quarter*. WO-150 placed the 5 current buildings one-per-quadrant; adding 3 more, CLI clusters the production/smithing buildings rather than one-per-quadrant (the owner's "well-divided" districts read better than maximal spread). **This is layout SPEC only — CLI decides exact plots when it edits the builder + adds the `BuildingUpgrade`+SO to each new prefab/entry in a future coordinated pass.**
> Each new building also needs a `BuildingInteractable`/interactor [F]-prompt opening its upgrade panel (mirror the Forge/Tower interact already in the builder). Missing poly mesh → `Debug.LogWarning` + stub (CLAUDE.md §4).

---

## Assembly placement (CLAUDE.md §5/§6)

- `BuildingId` enum → **`DeNelle.Core.Buildings`** (`Assets/_Modules/Core/Buildings/BuildingId.cs`).
- `BuildingTierData` SO → **`DeNelle.Core.Data`** (`Assets/_Modules/Core/Data/BuildingTierData.cs`) — pure data, 4-int cost (NO `using DeNelle.Village` — avoids the Core→Village ref WO-114 risked).
- `BuildingUpgrade`, `VillageLevel`, `BuildingEffects`, `ResourceBuildingProducer` → **`DeNelle.Village`**.
- GameState fields → **`DeNelle.Core.State`**. **Village → Core only.** Banking writes `GameState` directly (Core can't ref Village — memory *core-cannot-reference-village-award-crystals-via-gamestate*). All HUD/Audio via `CoreServices.Hud?` / `CoreServices.Audio?` with `?.`. No `System.Reflection`. No UXML.

---

## Files to Create / Edit

| File | Action | Note |
|---|---|---|
| `Assets/_Modules/Core/Buildings/BuildingId.cs` | **Create** | enum (Core) |
| `Assets/_Modules/Core/Data/BuildingTierData.cs` | **Create** | `[CreateAssetMenu]` SO (mirror TowerData), 4-int cost |
| `Assets/_Modules/Village/Buildings/BuildingUpgrade.cs` | **Create** | the ONE shared upgrade component |
| `Assets/_Modules/Village/Buildings/VillageLevel.cs` | **Create** | meta-gate + level-up cost curve |
| `Assets/_Modules/Village/Buildings/BuildingEffects.cs` | **Create** | static effect registry read by combat |
| `Assets/_Modules/Village/Buildings/ResourceBuildingProducer.cs` | **Create (or fold into BuildingUpgrade)** | passive yield trickle → GameState wallet |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs` | **Edit (1 line, L278)** | × `BuildingEffects.WeaponDamageMultiplier` |
| `Assets/_Modules/Village/Enemies/PlayerAttackController.cs` | **Edit (1 line, L142)** | × `BuildingEffects.WeaponDamageMultiplier` |
| `Assets/_Modules/Village/Hero/HeroHealth.cs` | **Edit (1 line, top of TakeDamage L125)** | `amount *= BuildingEffects.DamageTakenMultiplier;` |
| `Assets/_Modules/Core/State/GameState.cs` | **Edit** | add `VillageLevel` int + `BuildingLevels` dict (Village region) |
| `Assets/_Modules/Core/State/SaveSchema.cs` + `GameStateService.cs` | **Edit** | round-trip the 2 new fields (mirror `WallLevel`); schema bump — coordinate w/ SaveMigrator owner |
| `Assets/_Modules/Village/Buildings/MarketplaceInteractor.cs` (or Store panel) | **Edit (§5, optional/flagged)** | "Buy materials" sub-panel; reuse PackStore, do NOT rebuild |
| `Assets/.../UpgradePanel` (code-built, mirror TowerUpgradeButton) | **Create** | per-building upgrade UI, code-built (no UXML) |
| `Assets/Data/Buildings/*.asset` (Lumbermill/Ironworks/Farm/Forge/Armory tiers) | **Create** | one `BuildingTierData` per building, placeholder curves |
| `Assets/_Modules/Core/Audio/SfxId.cs` | **Reference / edit if `Upgrade` absent** | reuse existing upgrade SFX id |
| `Assets/Editor/VillageSceneBuilder.cs` | **DO NOT EDIT — SPEC ONLY (§6)** | roster additions (Lumbermill/Ironworks/Armory) are a future coordinated CLI builder pass |

---

## What NOT to touch

- **Do NOT edit `VillageSceneBuilder.cs` or fire any bake/batchmode** (CLAUDE.md §3/§9; frozen single-writer). The 3 new buildings are SPEC'd in §6 for a later coordinated CLI pass — this WO does the CODE + DATA only.
- **Do NOT hand-edit `Village.unity`** (CLAUDE.md §3).
- **Do NOT touch the Tower upgrade chain** (`TowerData`/`TowerCombat`/`TowerUpgradeButton`) — keep as-is (WO-150). Forge buffs the HERO, not towers (this cut).
- **Do NOT add a new currency** — spend/produce existing `GameState.Wood/Stone/Iron/AetherCrystals` + `Resources.Food/Coins` via the existing `EconomyService` (memory *core-cannot-reference-village...*).
- **Do NOT fork a parallel damage system** — inject ONE factor into the existing `HeroAbilities`/`PlayerAttackController` outgoing math and the single `HeroHealth.TakeDamage` chokepoint (memory *two-combat-feel-stacks*).
- **Do NOT duplicate the harvest node/worker systems** (WO-141/117) — the resource-building trickle is a third faucet into the SAME wallet, additive; reconcile, don't replace (memory *wo-batch-reconcile-not-replace*).
- **Do NOT rebuild PackStore** — the materials-buy is a thin sub-panel on the existing Store (memory *monetization-stack-already-built*).
- **Do NOT build any UI in UXML** — code-built panels only (PIPELINE_STATE.md §8; memory *uxml-uidocuments-dont-render-in-builds*).
- **Do NOT introduce `System.Reflection`** in these scripts.
- **Do NOT create `DeNelle.Core.Debug`/duplicate-name namespaces** (memory *core-namespace-shadows-unityengine-statics*) or a 2nd DoorController-style duplicate.
- Do not touch ATB, clan, telemetry, or WalletService code.

---

## Acceptance Criteria

- [ ] ONE shared `BuildingUpgrade` component (composition) drives Lumbermill, Ironworks, Farm, Forge, Armory — **no subclass-per-building**
- [ ] `BuildingTierData` SO authorable via `[CreateAssetMenu]` (mirrors `TowerData.upgrades[]`); a tier asset exists for each of the 5 buildings with placeholder curves
- [ ] `VillageLevel` meta-gate: a building tier above `requiredVillageLevel` cannot be bought; leveling the village raises the ceiling
- [ ] Upgrade spends via the EXISTING `EconomyService.TrySpend(ResourceCost)` (4-int → ResourceCost in Village); gated on `CanAfford`; shows "Max"/"Need village Lv N" when blocked
- [ ] **Forge** tier raises hero outgoing damage — verified `HeroAbilities.ResolveEffect` (L278) and `PlayerAttackController.ResolveAttack` (L142) multiply by `BuildingEffects.WeaponDamageMultiplier`
- [ ] **Armory** tier reduces incoming damage — verified ONE line at the top of `HeroHealth.TakeDamage` (L125) scales `amount` by `BuildingEffects.DamageTakenMultiplier`; floor-clamped (no invulnerability)
- [ ] **Resource buildings** (Lumbermill→Wood, Ironworks→Iron, Farm→Food) trickle their material into the EXISTING `GameState` wallet at `yieldPerCyclePerSec`; HUD ticks via `CoreServices.Hud?.SetResource(...)`; NOT a new currency, NOT a duplicate node
- [ ] `VillageLevel` (int) + `BuildingLevels` (dict) persisted on `GameState`, round-tripped in `SaveSchema`/`GameStateService` (mirror `WallLevel`), reset on New Game
- [ ] Store sells missing materials (Wood/Iron/Food) for currency via `EconomyService.Grant` / GameState write — thin sub-panel on the existing Store, PackStore NOT rebuilt (or shipped as a flagged follow-up step)
- [ ] 3 new buildings (Lumbermill/Ironworks/Armory) SPEC'd for the roster in §6 — **`VillageSceneBuilder.cs` NOT edited, no bake fired**
- [ ] Upgrade UI is code-built (no UXML/UIDocument source); mirrors the TowerUpgrade affordance
- [ ] `DeNelle.Village` → `DeNelle.Core` only; `BuildingTierData` (Core) has NO `using DeNelle.Village`; all cross-module calls use `?.`; no `System.Reflection`
- [ ] Forge-vs-Armory shipped as TWO buildings (recommended); owner confirmed (or one-building fallback noted)

---

## Done checklist (CLAUDE.md §10)

- [ ] Brace-balance check passed on every `.cs` edited (BuildingUpgrade, VillageLevel, BuildingEffects, ResourceBuildingProducer, HeroAbilities, PlayerAttackController, HeroHealth, GameState, SaveSchema, GameStateService)
- [ ] No `.unity` scene file hand-edited; `VillageSceneBuilder.cs` NOT touched; no bake/batchmode fired
- [ ] No new `System.Reflection` introduced
- [ ] `using DeNelle.Core.Combat;` present where needed; Village → Core only (Core never refs Village)
- [ ] Null-conditional (`?.`) on all cross-module `CoreServices.Hud`/`CoreServices.Audio` calls
- [ ] No new currency; existing `EconomyService`/`GameState` wallet reused
- [ ] Acceptance criteria reviewed line by line
- [ ] SaveSchema bump coordinated with the SaveMigrator owner before landing
- [ ] `WORK_ORDER_151_village_progression_crafting.RESULT.md` written by CLI when complete

---

🤖 Spec'd by the design lane (UI). Reconciled against `EconomyService.cs` (`ResourceCost`/`CanAfford`/`TrySpend`/`Grant` — reused, not forked), `GameState.cs` (Wood L58 / Stone L54 / Iron L56 / AetherCrystals L52 / Resources.Food — no net-new currency; `WallLevel`/`BuildingCooldowns`/`BuildingDamage` persist pattern mirrored), `TowerData.cs` (`upgrades[]`/`upgradeCost` tier pattern mirrored; Tower chain left as-is), `HeroAbilities.cs` (L278 outgoing-dmg product), `PlayerAttackController.cs` (L142 `_baseDamage`), `HeroHealth.cs` (L125 `TakeDamage` single incoming chokepoint), `HeroProgression`/`HeroTalentModifiers` (existing dmg multipliers — Forge stacks as one more factor), and WO-114/117/141/150, CLAUDE.md §5/§6/§9, PIPELINE_STATE.md §8. Markdown work order only — no `.cs` touched, no bake fired, `VillageSceneBuilder` not edited.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `PopulationBootstrap.cs:17, structures-catalog.json, BuildingUpgradeRegression.cs` — tier gate shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
