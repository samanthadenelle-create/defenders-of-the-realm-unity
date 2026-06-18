// =============================================================================
// ResourceBuildingProgression — the data-driven leveling model for the three
// resource buildings (Farm, Lumbermill, Forge). WO-151 / DEF-121 (WO-230).
// -----------------------------------------------------------------------------
// CoC-style flat-step progression: each resource building has a small static
// table of per-level UPGRADE COSTS (which harvestables + amounts) and per-level
// YIELD (how much of its harvestable it produces per harvest tick). Data lives
// in ONE place (the static tables below) — the existing catalog convention in
// this codebase (BuildingCatalog / CraftingRecipeCatalog) keeps content out of
// behaviour code, and these are pure C# definitions like the other catalogs.
//
// RECONCILIATION (deliberate, per DEF-121 / memory "reconcile-not-replace"):
//   * The four harvestables are Wood / Food / Iron / Crystals (DEF-121 owner
//     correction; Magic is NOT a harvestable — it is a separate tech axis, out
//     of scope for THIS pass which is the per-building upgrade loop).
//   * Those map onto EXISTING GameState fields — no new currency invented:
//       Crystals -> GameState.Resources.Crystals
//       Food     -> GameState.Resources.Food
//       Wood     -> GameState.Wood
//       Iron     -> GameState.Iron
//     The ResourceLedger helper below is the single read/spend surface over
//     those fields (mirrors BuildMenu.SpendCrystals' GameStateService pattern).
//   * Buildings are modelled by stable string id (the Building.BuildingId /
//     BuildingDef.Id convention) — NOT a new BuildingType enum value — so this
//     touches neither the BuildingType enum nor VillageSceneBuilder (the
//     serialization bottleneck). Farm already exists as a BuildingType; Lumbermill
//     and Forge are id-keyed resource buildings.
//
// DeNelle.Village -> DeNelle.Core only (asmdef rule). This file references the
// Core GameState via GameStateService.Instance, never the other way around.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// The four harvestable resources (DEF-121 owner correction). Each maps onto
    /// an existing GameState field — see <see cref="ResourceLedger"/>. Magic is
    /// deliberately absent: it is a tech axis, not a harvestable.
    /// </summary>
    public enum HarvestResource
    {
        /// <summary>Crystals — GameState.Resources.Crystals.</summary>
        Crystals = 0,
        /// <summary>Food — GameState.Resources.Food.</summary>
        Food = 1,
        /// <summary>Wood — GameState.Wood.</summary>
        Wood = 2,
        /// <summary>Iron — GameState.Iron.</summary>
        Iron = 3,
    }

    /// <summary>A single resource cost line — <paramref name="Amount"/> of a resource.</summary>
    public readonly struct ResourceCost
    {
        public readonly HarvestResource Resource;
        public readonly int Amount;

        public ResourceCost(HarvestResource resource, int amount)
        {
            Resource = resource;
            Amount = amount;
        }
    }

    /// <summary>
    /// The per-level snapshot of a resource building: the YIELD it produces at
    /// this level and the COST to reach the NEXT level (empty when maxed).
    /// </summary>
    public sealed class ResourceLevelDef
    {
        /// <summary>1-based building level this def describes.</summary>
        public int Level;

        /// <summary>The harvestable this building produces.</summary>
        public HarvestResource Yields;

        /// <summary>Units of <see cref="Yields"/> produced per harvest tick at this level.</summary>
        public int YieldPerTick;

        /// <summary>
        /// HARVEST SPEED (T-025): seconds between auto-harvest ticks at this level.
        /// SMALLER = faster. The forge-tree "increase harvest speed" axis — each level
        /// shortens the cooldown, so an upgrade visibly speeds income. Consumed by
        /// <see cref="ResourceBuildingHarvester"/> as the per-building tick interval.
        /// </summary>
        public float HarvestInterval = 6f;

        /// <summary>
        /// YIELD SIZE multiplier (T-025): a curated bonus multiplier applied to
        /// <see cref="YieldPerTick"/> at harvest time. The "increase size" forge-tree
        /// axis — most levels leave this at 1.0 (size comes from YieldPerTick), but the
        /// top/arcane tiers bump it so a maxed building reads as a bigger haul. 1.0 = none.
        /// </summary>
        public float YieldSizeMultiplier = 1f;

        /// <summary>
        /// Resources spent to upgrade FROM this level to the next. Empty list
        /// means this is the max level (no further upgrade).
        /// </summary>
        public IReadOnlyList<ResourceCost> UpgradeCost;

        /// <summary>
        /// MAGIC tech-axis cost to upgrade FROM this level to the next (DEF-121).
        /// Magic is NOT a harvestable — it is the building-upgrade tech currency
        /// (GameState.Magic), spent ON TOP OF the harvestable cost to unlock a
        /// Magic-GATED tier. 0 = no Magic requirement (an ordinary tier).
        /// </summary>
        public int MagicCost;

        /// <summary>
        /// The tech-tree node id this upgrade UNLOCKS when bought (DEF-121). Null/empty
        /// for an ordinary tier. A Magic-gated tier sets this so reaching it lights up a
        /// node in the tech tree (consumed by <see cref="TechTree"/>).
        /// </summary>
        public string UnlocksTechNode;

        /// <summary>True when this tier is gated on the Magic tech axis (has a Magic cost).</summary>
        public bool IsMagicGated => MagicCost > 0;

        /// <summary>True when there is no further level to buy.</summary>
        public bool IsMaxLevel =>
            (UpgradeCost == null || UpgradeCost.Count == 0) && MagicCost <= 0;
    }

    /// <summary>
    /// One resource building's full leveling curve. Defined as a static C# table
    /// below — the single source of truth for Farm / Lumbermill / Forge costs and
    /// yields. CoC-style: yield steps up each level; cost ramps roughly 1.6x.
    /// </summary>
    public sealed class ResourceBuildingDef
    {
        /// <summary>Stable building id — <c>farm</c> / <c>lumbermill</c> / <c>forge</c>.</summary>
        public string BuildingId;

        /// <summary>Player-facing label (e.g. "Farm").</summary>
        public string DisplayName;

        /// <summary>The harvestable this building produces.</summary>
        public HarvestResource Yields;

        /// <summary>Per-level defs, index 0 = level 1. Length = MaxLevel.</summary>
        public ResourceLevelDef[] Levels;

        /// <summary>Highest reachable level.</summary>
        public int MaxLevel => Levels != null ? Levels.Length : 1;

        /// <summary>Clamps an arbitrary level into the valid 1..MaxLevel range.</summary>
        public int ClampLevel(int level) => Mathf.Clamp(level, 1, MaxLevel);

        /// <summary>The def for <paramref name="level"/> (clamped). Never null for a valid table.</summary>
        public ResourceLevelDef LevelDef(int level)
        {
            if (Levels == null || Levels.Length == 0) return null;
            return Levels[ClampLevel(level) - 1];
        }
    }

    /// <summary>
    /// Static catalog of the three resource buildings' leveling curves. Mirrors
    /// the BuildingCatalog / CraftingRecipeCatalog convention (a static lookup
    /// surface), but the numbers are authored in code here because they are a
    /// small balance table, not designer-facing content — kept SIMPLE per the
    /// "scope discipline — NOT an MMO" owner boundary (flat steps, CoC-style).
    /// </summary>
    public static class ResourceBuildingProgression
    {
        /// <summary>Canonical building ids for the three resource buildings.</summary>
        public const string FarmId = "farm";
        public const string LumbermillId = "lumbermill";
        public const string ForgeId = "forge";

        // Curated harvest-SPEED ladder (T-025). MUST be declared BEFORE _byId: static
        // field initializers run in textual order, and _byId = Build() reads this array
        // via IntervalForLevel during construction. If it sits after _byId it is still
        // null when Build() runs -> the type initializer throws NRE and poisons the whole
        // type (TypeInitializationException cascade across upgrade/dialogue/harvester).
        // Level 1 ticks every 8s dropping ~1.2s/level to 3.2s at level 5 (2.5x throughput).
        private static readonly float[] HarvestIntervalByLevel = { 8f, 6.8f, 5.6f, 4.4f, 3.2f };

        // LAZY + GUARDED catalog (WO-453). Previously `_byId = Build()` ran inside the
        // static .cctor. ANY exception thrown by Build() there raises a TypeInitialization
        // Exception that PERMANENTLY POISONS the whole `ResourceBuildingProgression` type —
        // every later member access (Find / IsResourceBuilding / All) then re-throws, which
        // cascaded into CmdStructureStatus/CmdStructureUpgrade, the harvester, and the dialogue
        // bridge ("structure upgrade broken / talk doesn't work / no stock"). Hardening one
        // inner helper (IntervalForLevel) only covered ONE throw path; the real fix is to never
        // build in the .cctor at all. We build LAZILY on first access, wrapped in Guard.Try, so
        // a failure logs once (via Guard -> Debug.LogError, [Flow:Progression]) and falls back to
        // an EMPTY catalog instead of poisoning the type. Consumers then degrade gracefully
        // (Find returns null) rather than throwing on every call.
        private static Dictionary<string, ResourceBuildingDef> _byId;

        private static Dictionary<string, ResourceBuildingDef> ById
        {
            get
            {
                if (_byId == null)
                {
                    _byId = Guard.Try(
                        "Progression", "build resource-building catalog",
                        Build,
                        fallback: new Dictionary<string, ResourceBuildingDef>());
                }
                return _byId;
            }
        }

        /// <summary>All three resource-building curves, in display order.</summary>
        public static IEnumerable<ResourceBuildingDef> All => ById.Values;

        /// <summary>The ordered building ids (Farm, Lumbermill, Forge).</summary>
        public static readonly string[] OrderedIds = { FarmId, LumbermillId, ForgeId };

        /// <summary>Looks up a curve by building id. Null when the id is not a resource building.</summary>
        public static ResourceBuildingDef Find(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return null;
            return ById.TryGetValue(buildingId, out var def) ? def : null;
        }

        /// <summary>True when <paramref name="buildingId"/> is one of the three resource buildings.</summary>
        public static bool IsResourceBuilding(string buildingId) => Find(buildingId) != null;

        // =====================================================================
        //  The balance table — the single source of truth for costs + yields.
        // =====================================================================

        private static Dictionary<string, ResourceBuildingDef> Build()
        {
            var dict = new Dictionary<string, ResourceBuildingDef>();

            // Farm — produces Food. Upgraded with Wood + Crystals (you spend the
            // other harvestables to grow your food output). 5 levels.
            dict[FarmId] = MakeBuilding(
                FarmId, "Farm", HarvestResource.Food,
                baseYield: 20, yieldStep: 12,
                costResources: new[] { HarvestResource.Wood, HarvestResource.Crystals },
                baseCost: 85, costStep: 1.9f);

            // Lumbermill — produces Wood. Upgraded with Food + Crystals. 5 levels.
            dict[LumbermillId] = MakeBuilding(
                LumbermillId, "Lumbermill", HarvestResource.Wood,
                baseYield: 15, yieldStep: 10,
                costResources: new[] { HarvestResource.Food, HarvestResource.Crystals },
                baseCost: 80, costStep: 1.9f);

            // Forge — produces Iron. Upgraded with Wood + Crystals. 5 harvestable
            // levels PLUS a 6th MAGIC-GATED tier (DEF-121): the Arcane Forge. Reaching
            // it costs Magic (the tech axis, NOT a harvestable) and UNLOCKS a tech-tree
            // node ("arcane_forge") — the one Magic-gated upgrade tier required by the
            // economy correction. The priciest curve.
            dict[ForgeId] = MakeBuilding(
                ForgeId, "Forge", HarvestResource.Iron,
                baseYield: 8, yieldStep: 6,
                costResources: new[] { HarvestResource.Wood, HarvestResource.Crystals },
                baseCost: 130, costStep: 2.0f,
                magicTier: new MagicTier(
                    magicCost: 3,
                    techNodeId: TechTree.ArcaneForgeNodeId,
                    // The arcane tier still spends some harvestables alongside the Magic.
                    harvestCost: new[]
                    {
                        new ResourceCost(HarvestResource.Iron, 120),
                        new ResourceCost(HarvestResource.Crystals, 60),
                    }));

            return dict;
        }

        /// <summary>
        /// Optional Magic-gated top tier for a building (DEF-121). When supplied,
        /// <see cref="MakeBuilding"/> appends ONE extra level above the harvestable
        /// curve that costs <see cref="MagicCost"/> Magic (+ optional harvestables) and
        /// unlocks <see cref="TechNodeId"/>.
        /// </summary>
        private readonly struct MagicTier
        {
            public readonly int MagicCost;
            public readonly string TechNodeId;
            public readonly ResourceCost[] HarvestCost;

            public MagicTier(int magicCost, string techNodeId, ResourceCost[] harvestCost)
            {
                MagicCost = magicCost;
                TechNodeId = techNodeId;
                HarvestCost = harvestCost ?? System.Array.Empty<ResourceCost>();
            }
        }

        private const int HarvestLevels = 5;

        // ── Curated harvest-SPEED + yield-SIZE ladders (T-025) ────────────────
        // The forge-tree dimensions beyond raw yield. Both are CURATED constants
        // (NOT free-form): each harvest level shortens the tick interval and the
        // arcane tier adds a size multiplier. Balanced off the existing yield ladder
        // so an upgrade is felt as "faster AND bigger", not just a bigger number.
        //
        // Speed ladder HarvestIntervalByLevel is declared near the top (before _byId)
        // so it is initialized before Build() reads it — see the comment there.
        // The arcane (Magic-gated) tier is the fastest tick in the game.
        private const float ArcaneHarvestInterval = 2.4f;
        // Size: harvestable tiers leave size at 1.0 (size = YieldPerTick); the
        // arcane tier multiplies the haul so a maxed forge reads as a big payout.
        private const float ArcaneYieldSizeMultiplier = 1.5f;

        /// <summary>The curated tick interval (seconds) for a 1-based <paramref name="level"/>.</summary>
        private static float IntervalForLevel(int level)
        {
            // HARDENING (dialogue-cluster RCA): this is called from MakeBuilding during the
            // static .cctor (Build() -> MakeBuilding -> IntervalForLevel). Although the speed
            // ladder is declared textually BEFORE _byId, the field has been observed NULL here
            // at runtime (TypeInitializationException -> NRE poisoning the WHOLE type, which then
            // cascaded into CmdStructureStatus/CmdStructureUpgrade and every other consumer —
            // "no stock / talk doesn't work"). The fallback ladder is an INLINE LOCAL literal —
            // not another static field — so it has ZERO dependence on static-field init order and
            // can never itself be null during cctor. Keeps the curve identical, never a flat 6f.
            float[] ladder = (HarvestIntervalByLevel != null && HarvestIntervalByLevel.Length > 0)
                ? HarvestIntervalByLevel
                : new[] { 8f, 6.8f, 5.6f, 4.4f, 3.2f };
            int i = Mathf.Clamp(level - 1, 0, ladder.Length - 1);
            return ladder[i];
        }

        /// <summary>
        /// Builds a 5-level harvestable curve from simple parameters: yield grows
        /// linearly (<paramref name="baseYield"/> + (level-1)*<paramref name="yieldStep"/>),
        /// cost grows geometrically (<paramref name="baseCost"/> * step^(level-1)),
        /// split evenly across <paramref name="costResources"/>. When
        /// <paramref name="magicTier"/> is supplied, ONE extra MAGIC-GATED level is
        /// appended above the harvestable curve (DEF-121): the prior top level now
        /// upgrades into it (Magic + optional harvestables) and unlocks a tech node;
        /// the appended level is the true max. Otherwise the top level is the max
        /// (empty cost).
        /// </summary>
        private static ResourceBuildingDef MakeBuilding(
            string id, string displayName, HarvestResource yields,
            int baseYield, int yieldStep,
            HarvestResource[] costResources, int baseCost, float costStep,
            MagicTier? magicTier = null)
        {
            bool hasMagic = magicTier.HasValue;
            int total = HarvestLevels + (hasMagic ? 1 : 0);
            var levels = new ResourceLevelDef[total];

            for (int i = 0; i < HarvestLevels; i++)
            {
                int level = i + 1;
                var def = new ResourceLevelDef
                {
                    Level = level,
                    Yields = yields,
                    YieldPerTick = baseYield + (level - 1) * yieldStep,
                    // T-025: each harvestable level also ticks FASTER (curated ladder).
                    HarvestInterval = IntervalForLevel(level),
                    YieldSizeMultiplier = 1f,
                };

                bool isTopHarvest = level == HarvestLevels;

                if (!isTopHarvest)
                {
                    // Ordinary harvestable upgrade to the next level.
                    int totalCost = Mathf.RoundToInt(baseCost * Mathf.Pow(costStep, level - 1));
                    var costs = new List<ResourceCost>(costResources.Length);
                    int per = Mathf.Max(1, totalCost / costResources.Length);
                    foreach (var res in costResources)
                        costs.Add(new ResourceCost(res, per));
                    def.UpgradeCost = costs;
                }
                else if (hasMagic)
                {
                    // Top harvestable level → the MAGIC-GATED arcane tier (DEF-121).
                    var mt = magicTier.Value;
                    def.UpgradeCost = mt.HarvestCost;
                    def.MagicCost = mt.MagicCost;
                    def.UnlocksTechNode = mt.TechNodeId;
                }
                else
                {
                    def.UpgradeCost = System.Array.Empty<ResourceCost>();
                }

                levels[i] = def;
            }

            // The appended arcane tier itself — the true max level (no further upgrade).
            if (hasMagic)
            {
                int level = HarvestLevels + 1;
                levels[HarvestLevels] = new ResourceLevelDef
                {
                    Level = level,
                    Yields = yields,
                    // A meaningful arcane yield bump over the previous top tier.
                    YieldPerTick = baseYield + (level - 1) * yieldStep + yieldStep,
                    // T-025: the arcane tier is the FASTEST tick AND multiplies the haul.
                    HarvestInterval = ArcaneHarvestInterval,
                    YieldSizeMultiplier = ArcaneYieldSizeMultiplier,
                    UpgradeCost = System.Array.Empty<ResourceCost>(),
                    MagicCost = 0,
                };
            }

            return new ResourceBuildingDef
            {
                BuildingId = id,
                DisplayName = displayName,
                Yields = yields,
                Levels = levels,
            };
        }

        /// <summary>Human label for a harvestable (for UI cost/yield lines).</summary>
        public static string LabelFor(HarvestResource r) => r switch
        {
            HarvestResource.Crystals => "Crystals",
            HarvestResource.Food => "Food",
            HarvestResource.Wood => "Wood",
            HarvestResource.Iron => "Iron",
            _ => r.ToString(),
        };
    }

    /// <summary>
    /// The single read/spend surface over the four harvestable balances in
    /// GameState. Village -> Core is a legal asmdef edge, so this resolves the
    /// live <see cref="GameStateService"/> directly (the same access BuildMenu /
    /// WaveManager / WallRepairController already use). Persists + raises
    /// <c>ResourcesChanged</c> exactly like BuildMenu.SpendCrystals so the HUD
    /// resource bar refreshes. Null-safe: with no service it reads/writes nothing
    /// and reports a zero balance (matches the standalone-test fallbacks elsewhere).
    /// </summary>
    public static class ResourceLedger
    {
        /// <summary>
        /// Reads the current MAGIC tech-axis balance from GameState (0 when no service).
        /// Magic is NOT a harvestable — it is the building-upgrade tech currency
        /// (GameState.Magic). DEF-121.
        /// </summary>
        public static int MagicBalance()
        {
            var s = GameStateService.Instance?.State;
            return s?.Magic ?? 0;
        }

        /// <summary>Reads the current balance of <paramref name="r"/> from GameState (0 when no service).</summary>
        public static int Balance(HarvestResource r)
        {
            var s = GameStateService.Instance?.State;
            if (s == null) return 0;
            return r switch
            {
                HarvestResource.Crystals => s.Resources.Crystals,
                HarvestResource.Food => s.Resources.Food,
                HarvestResource.Wood => s.Wood,
                HarvestResource.Iron => s.Iron,
                _ => 0,
            };
        }

        /// <summary>True when every cost line in <paramref name="costs"/> is affordable.</summary>
        public static bool CanAfford(IReadOnlyList<ResourceCost> costs)
        {
            if (costs == null) return true;
            foreach (var c in costs)
                if (Balance(c.Resource) < c.Amount) return false;
            return true;
        }

        /// <summary>
        /// Spends a full cost list atomically — checks affordability first, then
        /// deducts all lines, persists once, and raises <c>ResourcesChanged</c>.
        /// Returns false (spending nothing) when any line is unaffordable.
        /// </summary>
        public static bool TrySpend(IReadOnlyList<ResourceCost> costs)
        {
            if (costs == null || costs.Count == 0) return true;

            var service = GameStateService.Instance;
            var s = service?.State;
            if (s == null) return false;

            if (!CanAfford(costs)) return false;

            // Resources is a struct — read whole, mutate, write whole back.
            var bal = s.Resources;
            foreach (var c in costs)
            {
                switch (c.Resource)
                {
                    case HarvestResource.Crystals: bal.Crystals -= c.Amount; break;
                    case HarvestResource.Food: bal.Food -= c.Amount; break;
                    case HarvestResource.Wood: s.Wood -= c.Amount; break;
                    case HarvestResource.Iron: s.Iron -= c.Amount; break;
                }
            }
            s.Resources = bal;

            service.Save();
            service.ResourcesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Atomically spends a harvestable cost list PLUS a Magic tech-axis cost
        /// (DEF-121). Checks both are affordable first, then deducts harvestables
        /// (via the struct/field path) AND Magic (GameState.Magic), persists once,
        /// and raises <c>ResourcesChanged</c>. Returns false (spending nothing) when
        /// either the harvestables or the Magic are short. <paramref name="magicCost"/>
        /// of 0 makes this equivalent to <see cref="TrySpend"/>.
        /// </summary>
        public static bool TrySpendWithMagic(IReadOnlyList<ResourceCost> costs, int magicCost)
        {
            var service = GameStateService.Instance;
            var s = service?.State;
            if (s == null) return false;

            if (!CanAfford(costs)) return false;
            if (magicCost > 0 && MagicBalance() < magicCost) return false;

            // Deduct harvestables.
            var bal = s.Resources;
            if (costs != null)
            {
                foreach (var c in costs)
                {
                    switch (c.Resource)
                    {
                        case HarvestResource.Crystals: bal.Crystals -= c.Amount; break;
                        case HarvestResource.Food: bal.Food -= c.Amount; break;
                        case HarvestResource.Wood: s.Wood -= c.Amount; break;
                        case HarvestResource.Iron: s.Iron -= c.Amount; break;
                    }
                }
            }
            s.Resources = bal;

            // Deduct Magic (the tech axis).
            if (magicCost > 0)
                s.Magic = Mathf.Max(0, s.Magic - magicCost);

            service.Save();
            service.ResourcesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Credits <paramref name="amount"/> Magic tech-currency to GameState (boss /
        /// dungeon tech reward — NOT a harvest). Persists + raises <c>ResourcesChanged</c>.
        /// </summary>
        public static void CreditMagic(int amount)
        {
            if (amount <= 0) return;
            var service = GameStateService.Instance;
            var s = service?.State;
            if (s == null) return;
            s.Magic += amount;
            service.Save();
            service.ResourcesChanged?.Invoke();
        }

        /// <summary>
        /// Credits <paramref name="amount"/> of <paramref name="r"/> to GameState
        /// (used by a harvest tick / yield). Persists + raises <c>ResourcesChanged</c>.
        /// </summary>
        public static void Credit(HarvestResource r, int amount)
        {
            if (amount <= 0) return;
            var service = GameStateService.Instance;
            var s = service?.State;
            if (s == null) return;

            switch (r)
            {
                case HarvestResource.Crystals:
                {
                    var bal = s.Resources; bal.Crystals += amount; s.Resources = bal; break;
                }
                case HarvestResource.Food:
                {
                    var bal = s.Resources; bal.Food += amount; s.Resources = bal; break;
                }
                case HarvestResource.Wood: s.Wood += amount; break;
                case HarvestResource.Iron: s.Iron += amount; break;
            }

            service.Save();
            service.ResourcesChanged?.Invoke();
        }
    }
}
