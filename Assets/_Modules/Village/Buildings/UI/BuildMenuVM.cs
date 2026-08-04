// =============================================================================
// BuildMenuVM — pure ViewModel for the village build menu (MVVM Silo C).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Strict-MVVM migration (UI_MVVM_MIGRATION_PLAN.md §1, Silo C): the three
// game-state seams BuildMenu read inline move HERE so the View is a dumb skin:
//   * the crystal balance (was GameStateService.State.Resources.Crystals) →
//     <see cref="Crystals"/> (IEconomy.Crystals is the SAME GameState-backed store);
//   * the placed-tower poll (was FindObjectsByType<Tower>()) → the shared
//     <see cref="Towers"/> (PlacedTowerListVM — the sanctioned resolution site);
//   * the Repair-Wall REFLECTION (AppDomain scan + FindAnyObjectByType + MethodInfo
//     invoke — the architect flagged it as NOT a sanctioned seam) → the typed
//     <see cref="RepairNearestWall"/> command on the real WallRepairController API.
//
// Owns the GameState.ResourcesChanged subscription for the open-menu live refresh
// and raises <see cref="Changed"/>. PURE C# (no uGUI types); the injectable ctor
// lets §2c tests drive Crystals + the tower list over fakes.
//
// WO-861 (owner Tier 0, 2026-08-02) — THE WALLET RULE FINALLY FOLLOWED THE VIEW HERE.
// BuildMenu still ran a FAKE wallet after the crystal read migrated: a private
// GetMaterialCount(id) returned the literals wood=20 / stone=5, and those literals were
// (a) SHOWN to the player as an on-hand balance, (b) what the Build button's afford gate
// compared against, and (c) never deducted -- so every tower priced in wood/stone was
// FREE. The View also carried its own 4-row TowerVariantDef balance table (crystal/wood/
// stone cost + build time + upgrade cost + DPS + HP), a SECOND cost authority divergent
// from the catalog. Both are DELETED. The economy surface now lives here:
//   * balances        -> <see cref="Wood"/>/<see cref="Iron"/>/<see cref="Food"/>/
//                        <see cref="Crystals"/> + <see cref="MaterialCount"/>, all read
//                        straight off IEconomy (EconomyService == the ONE GameState-backed
//                        wallet since WO-842; there is no second material store --
//                        VillageInventory is the gear/consumable larder, not resources);
//   * priced towers   -> <see cref="TowerOptions"/>, sourced from CatalogRegistry rows via
//                        the REAL BuildModeController.CostFor (the same resolver Build Mode
//                        charges through), never a table in the View;
//   * affordability   -> <see cref="CanAfford"/> / <see cref="ShortfallFor"/>;
//   * the SPEND       -> <see cref="TrySpendBuild"/>, which HONOURS IEconomy.TrySpend's
//                        bool and returns false (with a reason) when the ledger declines.
//                        BuildMenu previously called BuildModeController.ChargeLedger, which
//                        DISCARDS TrySpend's return, then placed with prepaid:true -- a live
//                        free-tower path when the real ledger refused. Gone.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.State;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village.UI;
using UnityEngine;
using CoreCost = DeNelle.Core.Catalog.ResourceCost;
using CatalogRegistry = DeNelle.Core.Catalog.CatalogRegistry;
using CatalogType = DeNelle.Core.Catalog.CatalogType;

namespace DeNelle.Village
{
    /// <summary>
    /// ViewModel for the build menu: exposes the live crystal balance, the placed-tower list
    /// (for the Upgrade screen), and a typed Repair-Wall command. Created fresh on Open and
    /// disposed on Close (the panel-VM lifecycle).
    /// </summary>
    public sealed class BuildMenuVM : IPanelViewModel, IDisposable
    {
        /// <summary>
        /// One priced tower row the Build-Tower screen can offer. Every field is CATALOG
        /// data (id / displayName / repo stats) with the cost resolved through the REAL
        /// <see cref="BuildModeController.CostFor"/> — the View never authors a number.
        /// </summary>
        public readonly struct TowerBuildOption
        {
            public readonly string   Id;
            public readonly string   DisplayName;
            public readonly CoreCost Cost;
            public readonly float    Damage;
            public readonly float    Range;

            public TowerBuildOption(string id, string displayName, CoreCost cost, float damage, float range)
            {
                Id = id;
                DisplayName = displayName;
                Cost = cost;
                Damage = damage;
                Range = range;
            }

            /// <summary>True for the default/no-row struct (nothing selectable).</summary>
            public bool IsEmpty => string.IsNullOrEmpty(Id);

            /// <summary>Sum of every cost slot — the cheap-first ordering key.</summary>
            public int CostTotal => Cost.wood + Cost.food + Cost.iron + Cost.crystals;
        }

        /// <summary>How many catalog tower rows the Build-Tower radio offers (layout fits four).</summary>
        public const int MaxTowerOptions = 4;

        /// <summary>The TowerData asset the menu-initiated placement actually raises (DEF-76 —
        /// TowerConstructionQueue times the raise off its <c>buildTime</c>). Resolved HERE so the
        /// View performs no Resources.Load of its own.</summary>
        public const string PlacedTowerResourcePath = "Towers/DevTower";

        private readonly IEconomy _economy;
        private readonly int _fallbackCrystals;
        private readonly Action _onClose;
        private readonly UnityEngine.Events.UnityAction _stateHandler;
        private readonly Action<ResourceSnapshot> _economyHandler;
        private WallRepairController _wallRepair;
        private bool _subscribed;
        private bool _disposed;

        private List<TowerBuildOption> _towerOptions;
        private DeNelle.Core.Data.TowerData _placedTowerData;
        private bool _placedTowerDataResolved;

        /// <summary>The shared placed-tower list VM (owns the FindObjectsByType&lt;Tower&gt; poll).</summary>
        public PlacedTowerListVM Towers { get; }

        /// <summary>Resolves EconomyService.Instance + WallRepairController + the tower list itself
        /// (the sole resolution site) and hooks the live ResourcesChanged feed.</summary>
        public static BuildMenuVM CreateDefault(Action onClose, int fallbackCrystals)
        {
            var vm = new BuildMenuVM(
                EconomyService.Instance,
                PlacedTowerListVM.CreateDefault(onClose),
                UnityEngine.Object.FindFirstObjectByType<WallRepairController>(),
                fallbackCrystals,
                onClose);
            vm.Subscribe();
            return vm;
        }

        public BuildMenuVM(IEconomy economy, PlacedTowerListVM towers,
            WallRepairController wallRepair, int fallbackCrystals, Action onClose)
        {
            _economy = economy;
            Towers = towers;
            _wallRepair = wallRepair;
            _fallbackCrystals = fallbackCrystals;
            _onClose = onClose;
            _stateHandler = Raise;
            _economyHandler = _ => Raise();
        }

        private void Subscribe()
        {
            if (_subscribed || _disposed) return;
            _subscribed = true;
            var gs = GameStateService.Instance;
            if (gs != null) gs.ResourcesChanged.AddListener(_stateHandler);
            // Also ride the economy's own change feed: a spend that lands on the
            // no-GameState FALLBACK pool (EditMode / headless boots) never raises
            // ResourcesChanged, and the open menu must still re-price.
            if (_economy != null) _economy.OnChanged += _economyHandler;
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────
        public event Action Changed;
        public string Title => "Build";
        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_subscribed)
            {
                var gs = GameStateService.Instance;
                if (gs != null && _stateHandler != null) gs.ResourcesChanged.RemoveListener(_stateHandler);
                if (_economy != null && _economyHandler != null) _economy.OnChanged -= _economyHandler;
            }
            Towers?.Dispose();
            Changed = null;
        }

        // ── Read-only data ────────────────────────────────────────────────────

        /// <summary>The live crystal balance the menu spends from (IEconomy.Crystals is the single
        /// GameState-backed store; falls back to the standalone-test value when no service).</summary>
        public int Crystals => _economy != null ? _economy.Crystals : _fallbackCrystals;

        /// <summary>Live Wood on hand (IEconomy — the ONE GameState-backed wallet, WO-842). 0 with no service.</summary>
        public int Wood => _economy != null ? _economy.Wood : 0;

        /// <summary>Live Iron on hand. The build menu labels this slot "Stone" historically —
        /// the catalog/ledger axis is IRON (the retired Stone axis became FOOD, DEF-121).</summary>
        public int Iron => _economy != null ? _economy.Iron : 0;

        /// <summary>Live Food on hand.</summary>
        public int Food => _economy != null ? _economy.Food : 0;

        /// <summary>
        /// On-hand count of one resource axis, BY ID, straight off the live ledger. This
        /// replaces the deleted BuildMenu.GetMaterialCount stub, which returned the literals
        /// wood=20 / stone=5 and made every wood/stone-priced tower free (WO-861). There is
        /// no literal balance in this method and there must never be one again: an unknown id
        /// returns 0 (nothing is affordable) rather than a made-up number.
        /// </summary>
        public int MaterialCount(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return 0;
            switch (resourceId.ToLowerInvariant())
            {
                case "wood":     return Wood;
                case "iron":
                case "stone":    return Iron;   // legacy UI label -> the real Iron axis
                case "food":     return Food;
                case "crystal":
                case "crystals": return Crystals;
                default:         return 0;
            }
        }

        /// <summary>
        /// The tower rows this menu can price + build: every <see cref="CatalogType.Tower"/> entry
        /// in the registry, cheapest first, capped at <see cref="MaxTowerOptions"/>. Cost comes from
        /// the REAL <see cref="BuildModeController.CostFor"/> — the same resolver the Build-Mode
        /// commit charges through — so the menu can never show a price the ledger disagrees with.
        /// Empty (not fabricated) when the catalog has not been bootstrapped.
        /// </summary>
        public IReadOnlyList<TowerBuildOption> TowerOptions
        {
            get
            {
                if (_towerOptions == null) RefreshTowerOptions();
                return _towerOptions;
            }
        }

        /// <summary>Re-read the tower rows from the catalog (call after a late CatalogBootstrap).</summary>
        public void RefreshTowerOptions()
        {
            var built = new List<TowerBuildOption>(MaxTowerOptions);
            var rows = CatalogRegistry.OfType(CatalogType.Tower);
            if (rows != null)
            {
                var all = new List<TowerBuildOption>(rows.Count);
                foreach (var e in rows)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    CoreCost cost = BuildModeController.CostFor(e);
                    string label = !string.IsNullOrEmpty(e.displayName) ? e.displayName : e.id;
                    float dmg = e.repo != null ? e.repo.damage : 0f;
                    float rng = e.repo != null ? e.repo.range  : 0f;
                    all.Add(new TowerBuildOption(e.id, label, cost, dmg, rng));
                }
                all.Sort((a, b) =>
                {
                    int byCost = a.CostTotal.CompareTo(b.CostTotal);
                    return byCost != 0 ? byCost : string.CompareOrdinal(a.Id, b.Id);
                });
                for (int i = 0; i < all.Count && built.Count < MaxTowerOptions; i++)
                    built.Add(all[i]);
            }
            _towerOptions = built;
        }

        /// <summary>The option matching <paramref name="id"/>, else the first (cheapest) row,
        /// else an empty option when the catalog offered nothing.</summary>
        public TowerBuildOption TowerOptionFor(string id)
        {
            var list = TowerOptions;
            if (list == null || list.Count == 0) return default;
            if (!string.IsNullOrEmpty(id))
                for (int i = 0; i < list.Count; i++)
                    if (string.Equals(list[i].Id, id, StringComparison.OrdinalIgnoreCase)) return list[i];
            return list[0];
        }

        /// <summary>The TowerData asset a menu-initiated placement raises. Resolved once; null when
        /// the asset is missing (the View reports that instead of charging).</summary>
        public DeNelle.Core.Data.TowerData PlacedTowerData
        {
            get
            {
                if (!_placedTowerDataResolved)
                {
                    _placedTowerDataResolved = true;
                    _placedTowerData = Resources.Load<DeNelle.Core.Data.TowerData>(PlacedTowerResourcePath);
                }
                return _placedTowerData;
            }
        }

        /// <summary>Seconds the placed tower body takes to raise (TowerConstructionQueue reads the
        /// SAME field). 0 when the asset is missing — never a made-up duration.</summary>
        public int BuildSeconds
        {
            get
            {
                var d = PlacedTowerData;
                return d != null ? Mathf.Max(0, Mathf.RoundToInt(d.buildTime)) : 0;
            }
        }

        /// <summary>
        /// True when the LIVE ledger covers <paramref name="cost"/> on every axis. Routes through
        /// IEconomy.CanAfford (the same check the spend re-runs atomically); with no economy service
        /// it compares against this VM's own balances (crystals fall back, everything else is 0), so
        /// a service-less menu refuses a material cost instead of inventing stock.
        /// </summary>
        public bool CanAfford(CoreCost cost)
        {
            if (cost.IsZero) return true;
            if (_economy != null) return _economy.CanAfford(BuildModeController.ToEconomy(cost));
            return Crystals >= cost.crystals && Wood >= cost.wood
                   && Iron >= cost.iron && Food >= cost.food;
        }

        /// <summary>The concrete "Not enough &lt;Resource&gt; (N)" line for an unaffordable cost
        /// (the ONE shortfall message authority, shared with Build Mode).</summary>
        public string ShortfallFor(CoreCost cost) => BuildModeController.ShortfallMessage(cost);

        // ── Commands ──────────────────────────────────────────────────────────

        /// <summary>
        /// THE build spend. Returns TRUE ONLY when the ledger actually deducted
        /// <paramref name="cost"/>; on false <paramref name="failure"/> carries the player-facing
        /// reason and NOTHING was mutated. The caller must not place on false.
        ///
        /// WO-861: BuildMenu used to call BuildModeController.ChargeLedger, which throws away
        /// IEconomy.TrySpend's bool, and then placed with prepaid:true regardless — so a DECLINED
        /// spend still produced a tower. Here TrySpend's return is the decision. A missing economy
        /// service REFUSES (it cannot prove a deduction); it never falls through to a silent yes.
        /// </summary>
        public bool TrySpendBuild(CoreCost cost, out string failure)
        {
            failure = null;
            if (cost.IsZero) return true;                      // catalog row with no price
            if (_economy == null)
            {
                failure = "Economy unavailable - the build was not charged, so nothing was placed.";
                Debug.LogWarning("[BuildMenuVM] TrySpendBuild refused: no IEconomy - a spend that cannot be proven is a spend we do not make.");
                return false;
            }
            var price = BuildModeController.ToEconomy(cost);
            if (!_economy.CanAfford(price)) { failure = ShortfallFor(cost); return false; }
            if (!_economy.TrySpend(price))                      // <- the bool is HONOURED
            {
                failure = ShortfallFor(cost);
                Debug.LogWarning("[BuildMenuVM] TrySpendBuild: the ledger DECLINED the spend (balance raced) - placement blocked.");
                return false;
            }
            Raise();
            return true;
        }

        /// <summary>
        /// The REAL price of the selected placed tower's next level: Tower.TryUpgrade charges
        /// <c>NextUpgradeCost</c> of wood AND iron AND crystals (Tower.cs), so the menu shows exactly
        /// that instead of the deleted variant table's invented upgrade numbers. Zero when the tower
        /// is null / maxed / free / has no authored cost — callers that need to tell those apart
        /// must use <see cref="UpgradeQuoteFor"/>, which is the authority this delegates to.
        /// </summary>
        public CoreCost UpgradePriceFor(Tower tower) => UpgradeQuoteFor(tower).Cost;

        /// <summary>
        /// Why a placed tower can or cannot be upgraded right now. These are FIVE distinct states
        /// that a bare "price == 0" check collapsed into one, which is how a tower whose next level
        /// is authored at zero cost (it upgrades, for free) reported itself to the player as an
        /// un-authored tower.
        /// </summary>
        public enum UpgradeAvailability
        {
            /// <summary>Still being raised — it has no TowerData, so it has no level, stats or price.</summary>
            NotBuilt,
            /// <summary>Already at the last level.</summary>
            Maxed,
            /// <summary>The next level is authored, and authored at zero cost.</summary>
            Free,
            /// <summary>The next level is authored with a real price.</summary>
            Priced,
            /// <summary>The next level has no authored row at all — Tower.TryUpgrade refuses it.</summary>
            Unpriced,
        }

        /// <summary>Everything the upgrade screen needs about one placed tower's next level.</summary>
        public readonly struct UpgradeQuote
        {
            public readonly UpgradeAvailability Availability;
            public readonly int      Level;
            public readonly int      MaxLevel;
            public readonly CoreCost Cost;
            public readonly float    NowDamage;
            public readonly float    NowRange;
            public readonly float    NextDamage;
            public readonly float    NextRange;
            /// <summary>True when the live ledger covers <see cref="Cost"/> (always true when free).</summary>
            public readonly bool     Affordable;

            public UpgradeQuote(UpgradeAvailability availability, int level, int maxLevel, CoreCost cost,
                float nowDamage, float nowRange, float nextDamage, float nextRange, bool affordable)
            {
                Availability = availability;
                Level        = level;
                MaxLevel     = maxLevel;
                Cost         = cost;
                NowDamage    = nowDamage;
                NowRange     = nowRange;
                NextDamage   = nextDamage;
                NextRange    = nextRange;
                Affordable   = affordable;
            }

            /// <summary>True when a next level exists to advertise (so its stats can be shown).</summary>
            public bool HasNextLevel =>
                Availability == UpgradeAvailability.Priced || Availability == UpgradeAvailability.Free;

            /// <summary>True when tapping Upgrade would actually succeed.</summary>
            public bool CanUpgradeNow => HasNextLevel && Affordable;

            /// <summary>True when the tower is built, so its live stats are real numbers.</summary>
            public bool HasStats => Availability != UpgradeAvailability.NotBuilt;
        }

        /// <summary>
        /// Quote the selected tower's next level. Mirrors <see cref="Tower.TryUpgrade"/> exactly:
        /// the price is <c>upgrades[currentLevel].upgradeCost</c> charged on wood AND iron AND
        /// crystals, an un-authored row is refused, and a zero cost is a real (free) upgrade.
        ///
        /// The next level's stats are PROJECTED from the authored row rather than re-derived, so
        /// they carry the same village research + hero-talent modifiers Tower already folded into
        /// its live values. Tower.CurrentDamage is purely multiplicative in those modifiers, so the
        /// perk RATIO transfers exactly; Tower.CurrentRange adds a flat talent bonus after the
        /// multiplier, so the scaled perk DELTA transfers exactly.
        /// </summary>
        public UpgradeQuote UpgradeQuoteFor(Tower tower)
        {
            int max = Tower.MaxLevel;
            var data = tower != null ? tower.Data : null;
            if (data == null)
                return new UpgradeQuote(UpgradeAvailability.NotBuilt, 0, max, default, 0f, 0f, 0f, 0f, false);

            int level = tower.CurrentLevel;
            float nowDamage = tower.CurrentDamage;
            float nowRange  = tower.CurrentRange;

            if (level >= max)
                return new UpgradeQuote(UpgradeAvailability.Maxed, level, max, default,
                    nowDamage, nowRange, nowDamage, nowRange, false);

            var rows = data.upgrades;
            var nowRow  = RowAt(rows, level - 1);   // upgrades[level-1] == the level the tower is on
            var nextRow = RowAt(rows, level);       // upgrades[level]   == the level it would reach
            if (nextRow == null)
                return new UpgradeQuote(UpgradeAvailability.Unpriced, level, max, default,
                    nowDamage, nowRange, nowDamage, nowRange, false);

            int nowTier  = tower.EffectiveTier;
            int nextTier = level + 1;
            float perkDamageNow  = nowRow != null ? TowerPerkTable.EffectiveDamage(nowRow.damage, nowTier) : 0f;
            float perkDamageNext = TowerPerkTable.EffectiveDamage(nextRow.damage, nextTier);
            float nextDamage = perkDamageNow > 0.0001f
                ? nowDamage * (perkDamageNext / perkDamageNow)
                : perkDamageNext * ModifierService.Active.TowerDamageMult;

            float rangeMult     = ModifierService.Active.TowerRangeMult;
            float perkRangeNow  = nowRow != null ? TowerPerkTable.EffectiveRange(nowRow.range, nowTier) : 0f;
            float perkRangeNext = TowerPerkTable.EffectiveRange(nextRow.range, nextTier);
            float nextRange = nowRange + (perkRangeNext - perkRangeNow) * rangeMult;

            int each = nextRow.upgradeCost;
            if (each < 0 || each == int.MaxValue)
                return new UpgradeQuote(UpgradeAvailability.Unpriced, level, max, default,
                    nowDamage, nowRange, nextDamage, nextRange, false);
            if (each == 0)
                return new UpgradeQuote(UpgradeAvailability.Free, level, max, default,
                    nowDamage, nowRange, nextDamage, nextRange, true);

            var cost = new CoreCost { wood = each, iron = each, crystals = each };
            return new UpgradeQuote(UpgradeAvailability.Priced, level, max, cost,
                nowDamage, nowRange, nextDamage, nextRange, CanAfford(cost));
        }

        private static DeNelle.Core.Data.TowerUpgrade RowAt(DeNelle.Core.Data.TowerUpgrade[] rows, int index)
            => (rows != null && index >= 0 && index < rows.Length) ? rows[index] : null;

        /// <summary>Repair the most-damaged wall/structure through the sanctioned WallRepairController
        /// API (replaces the removed reflection seam). Surfaces the worst-damaged structure's repair
        /// prompt; no-op (warned) when the controller is absent.</summary>
        public void RepairNearestWall()
        {
            if (_wallRepair == null)
                _wallRepair = UnityEngine.Object.FindFirstObjectByType<WallRepairController>();
            if (_wallRepair != null) _wallRepair.SurfaceWorstRepair();
            else Debug.LogWarning("[BuildMenuVM] WallRepairController not in scene — Repair Wall no-op.");
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
