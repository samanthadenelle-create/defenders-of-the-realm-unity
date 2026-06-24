// =============================================================================
// BuildingUpgradeVM — the building-upgrade panel's PURE ViewModel (MVVM slice).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// ALL upgrade-panel STATE + LOGIC lives here, view-agnostic. Mirrors ShopVM:
//   * implements DeNelle.Core.UI.Mvvm.IPanelViewModel (Title / Changed / Close / Dispose)
//   * NO UnityEngine UI types (no GameObject/Image/Sprite/RectTransform/Color); the
//     View resolves all presentation. Math uses System.Math, not UnityEngine.Mathf,
//     so the VM is unit-testable without a scene (ARCHITECTURE_PRINCIPLES §2 / §2c).
//   * the View binds it, re-renders on Changed, and routes user input back as
//     commands; the View NEVER reads game state (ui-mvvm-binding-seam rule).
//
// TWO building families, decided EXACTLY like DialogueCommandBridge.CmdStructureStatus:
//   * CITY tier buildings  — BuildingTierCatalog.IsUpgradable(id): the WO-430 tier
//     ladder. UpgradeNext() spends via BuildingUpgradeService.TryUpgrade(id, tier+1).
//   * LEGACY resource buildings — ResourceBuildingProgression.IsResourceBuilding(id):
//     Farm/Lumbermill/Forge level curve. UpgradeNext() calls ResourceBuildingState.TryUpgrade(id).
//
// The model-side execute math is UNCHANGED — this VM only orchestrates + formats.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI.Mvvm;
// Disambiguate the two ResourceCost types in scope: the build-economy cost (Wood/Food/
// Crystals — what IEconomy spends) vs. the legacy harvest cost (Resource/Amount). The
// unqualified `ResourceCost` in this Progression namespace is the harvest one.
using EcoCost = DeNelle.Village.ResourceCost;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// Pure ViewModel for the building-upgrade panel. Exposes the tier ladder as
    /// <see cref="Upgrades"/> (one <see cref="ItemVM"/> per authored tier) plus a big
    /// main-button (label + enabled) and a status line. Raises <see cref="Changed"/>
    /// after each upgrade and on any economy / modifier / level change.
    /// </summary>
    public sealed class BuildingUpgradeVM : IPanelViewModel, IDisposable
    {
        /// <summary>Icon role key on each tier's ItemVM (the View maps it to art; no game state).</summary>
        public const string IconRoleTier = "tier";
        /// <summary>Icon role key on each research-perk row (the View maps it to the perk's
        /// Resources/HudItems/BuildingUpgrades/&lt;iconId&gt; sprite). WO-432.</summary>
        public const string IconRolePerk = "perk";

        private readonly string _buildingId;
        private readonly IEconomy _economy;
        private readonly Action _onClose;

        private readonly bool _isCity;
        private readonly bool _isResource;

        private readonly Action<ResourceSnapshot> _ecoHandler;
        private readonly Action _modHandler;
        private readonly Action<string> _levelHandler;
        private bool _disposed;

        private readonly List<ItemVM> _upgrades = new List<ItemVM>();
        // Per-tier cost string, keyed by the tier's ItemVM.Id — the View reads it through
        // CostFor(id) so it renders cost text purely from VM data (no catalog re-pull).
        private readonly Dictionary<string, string> _costById = new Dictionary<string, string>();

        public BuildingUpgradeVM(string buildingId, IEconomy economy, Action onClose)
        {
            _buildingId = buildingId ?? "";
            _economy = economy;
            _onClose = onClose;

            // Decide the family EXACTLY like CmdStructureStatus (city tiers win; else legacy).
            _isCity = BuildingTierCatalog.IsUpgradable(_buildingId);
            _isResource = !_isCity && ResourceBuildingProgression.IsResourceBuilding(_buildingId);

            if (_economy != null)
            {
                _ecoHandler = _ => Raise();
                _economy.OnChanged += _ecoHandler;
            }
            _modHandler = Raise;
            ModifierService.Changed += _modHandler;
            _levelHandler = _ => Raise();
            ResourceBuildingState.LevelChanged += _levelHandler;

            Rebuild();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title { get; private set; }

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_economy != null && _ecoHandler != null) _economy.OnChanged -= _ecoHandler;
            if (_modHandler != null) ModifierService.Changed -= _modHandler;
            if (_levelHandler != null) ResourceBuildingState.LevelChanged -= _levelHandler;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>Current tier/level (0 = locked for a city building; 1.. for a resource building).</summary>
        public int CurrentTier { get; private set; }

        /// <summary>Highest authored tier/level.</summary>
        public int MaxTier { get; private set; }

        /// <summary>One row per authored tier (name + Affordable + current/next/locked flags). Never null.</summary>
        public IReadOnlyList<ItemVM> Upgrades => _upgrades;

        /// <summary>The big main button's label ("Upgrade Building" / "Maxed" / "Locked").</summary>
        public string MainButtonLabel { get; private set; }

        /// <summary>Whether the main "Upgrade Building" button is enabled (affordable next tier exists).</summary>
        public bool MainButtonEnabled { get; private set; }

        /// <summary>Last action / hint line for the status row.</summary>
        public string Status { get; private set; }

        /// <summary>The cost string for a tier row id (View renders it from here — no catalog re-pull).</summary>
        public string CostFor(string id) =>
            id != null && _costById.TryGetValue(id, out var c) ? c : "";

        /// <summary>Live wallet readout (View rebuilds its "Wood … Food … Crystals" line from these).</summary>
        public int Wood     => _economy?.Wood ?? 0;
        public int Food     => _economy?.Food ?? 0;
        public int Iron     => _economy?.Iron ?? 0;
        public int Crystals => _economy?.Crystals ?? 0;
        public int Coins    => _economy?.Coins ?? 0;

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Buy the NEXT tier/level (city -> BuildingUpgradeService, resource -> ResourceBuildingState).</summary>
        public void UpgradeNext()
        {
            if (_isCity)
            {
                int next = CurrentTier + 1;
                if (next > MaxTier) { Status = "This is already at its highest level."; Raise(); return; }

                // WO-432 TIER GATE — mirror BuildingUpgradeService's gate so a tier-locked
                // upgrade reports an HONEST reason, not the generic "can't afford" (the service
                // returns a bare false for BOTH cases). Resources are fine; she's village-tier-gated.
                var nextDef = BuildingTierCatalog.TierOf(_buildingId, next);
                int villageTier = GameStateService.Instance?.State?.VillageTier ?? 0;
                if (nextDef != null)
                {
                    FlowTrace.Step("BuildingUpgrade", "UpgradeNext " + _buildingId + " tier=" + next
                        + " requiresVillageTier=" + nextDef.RequiresVillageTier
                        + " villageTier=" + villageTier
                        + " gated=" + (nextDef.RequiresVillageTier > villageTier));
                    if (nextDef.RequiresVillageTier > villageTier)
                    {
                        Status = "Requires Village Tier " + nextDef.RequiresVillageTier
                                 + " (you have " + villageTier + ").";
                        Raise();
                        return;
                    }
                }

                bool ok = BuildingUpgradeService.TryUpgrade(_buildingId, next);
                Status = ok
                    ? "Upgraded to Tier " + next + "."
                    : "You can't afford that yet.";
                Rebuild();
                Raise();
                return;
            }

            if (_isResource)
            {
                var result = ResourceBuildingState.TryUpgrade(_buildingId);
                switch (result)
                {
                    case UpgradeResult.Upgraded:
                        Status = "Upgraded to Level " + ResourceBuildingState.GetLevel(_buildingId) + ".";
                        break;
                    case UpgradeResult.Insufficient: Status = "You can't afford that yet."; break;
                    case UpgradeResult.MaxLevel:     Status = "This is already at its highest level."; break;
                    case UpgradeResult.NeedMagic:    Status = "That tier needs Magic to unlock."; break;
                    default:                         Status = "Nothing to upgrade here."; break;
                }
                Rebuild();
                Raise();
                return;
            }

            Status = "Nothing to upgrade here.";
            Raise();
        }

        /// <summary>Tier-card tap. The ladder buys the NEXT tier only, so tapping the next
        /// tier upgrades; tapping any other tier just re-states the hint (no model change).</summary>
        public void Select(string tierId)
        {
            if (string.IsNullOrEmpty(tierId)) return;
            // WO-432 — a research-perk row ("perk:<perkId>"): buy it with Gold (Coins).
            if (tierId.StartsWith("perk:", StringComparison.Ordinal))
            {
                string perkId = tierId.Substring("perk:".Length);
                if (BuildingPerkService.TryResearch(_buildingId, perkId))
                    Status = "Researched.";
                else
                {
                    BuildingPerkService.CanResearch(_buildingId, perkId, out string why);
                    Status = !string.IsNullOrEmpty(why) ? why : "Can't research that yet.";
                }
                Rebuild();
                Raise();
                return;
            }
            if (tierId == NextTierId()) { UpgradeNext(); return; }
            Status = "Tap the next tier to upgrade.";
            Raise();
        }

        // ── Build the rows + button/title/status (no Unity types) ────────────────

        private void Rebuild()
        {
            _upgrades.Clear();
            _costById.Clear();

            if (_isCity) BuildCity();
            else if (_isResource) BuildResource();
            else BuildUnknown();
        }

        private void BuildCity()
        {
            var def = BuildingTierCatalog.Find(_buildingId);
            Title = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : Titleize(_buildingId);
            CurrentTier = ModifierService.TierOf(_buildingId);
            MaxTier = BuildingTierCatalog.MaxTier(_buildingId);

            if (def != null && def.Tiers != null)
            {
                foreach (var t in def.Tiers)
                {
                    if (t == null) continue;
                    int tier = t.Tier;
                    bool isCurrent = tier <= CurrentTier;
                    bool isNext = tier == CurrentTier + 1;
                    bool locked = tier > CurrentTier + 1;

                    var cost = new EcoCost { Wood = t.CostWood, Food = t.CostFood, Crystals = t.CostCrystal };
                    bool affordable = isNext && (_economy == null || _economy.CanAfford(cost));
                    string costStr = CostString(cost);

                    string id = TierId(tier);
                    string name = (!string.IsNullOrEmpty(t.Name) ? t.Name : ("Tier " + tier));
                    _costById[id] = isCurrent ? "Owned" : costStr;
                    // Equipped flag carries "current/owned"; Locked carries "not yet reachable".
                    _upgrades.Add(new ItemVM(id, name, IconRoleTier, id, 0, "", affordable,
                                             rarity: null, equipped: isCurrent, locked: locked));
                }
            }

            // WO-432 RESEARCH ROWS — every perk unlocked at a REACHED tier shows as a Gold-cost row
            // under the tier ladder (owned = OWNED chip; gate-not-met = LOCKED; else NEXT + affordability
            // colour). A perk-row tap routes to BuildingPerkService via Select("perk:<id>"). Rows are
            // View-agnostic (a future Blink prefab View binds the same data — WO-435).
            if (def != null && def.Tiers != null)
            {
                foreach (var t in def.Tiers)
                {
                    if (t == null || t.Perks == null) continue;
                    foreach (var p in t.Perks)
                    {
                        if (p == null || string.IsNullOrEmpty(p.Id)) continue;
                        bool owned = BuildingPerkService.IsOwned(_buildingId, p.Id);
                        bool can = !owned && BuildingPerkService.CanResearch(_buildingId, p.Id, out _);
                        bool affordable = can && (_economy == null || _economy.Coins >= p.GoldCost);
                        string rid = "perk:" + p.Id;
                        string pname = (p.IsSignature ? "★ " : "") + (!string.IsNullOrEmpty(p.Name) ? p.Name : p.Id);
                        _costById[rid] = owned ? "Researched" : (p.GoldCost + " Gold");
                        string iconKey = string.IsNullOrEmpty(p.IconId) ? p.Id : p.IconId;
                        _upgrades.Add(new ItemVM(rid, pname, IconRolePerk, iconKey, 0, "", affordable,
                                                 rarity: null, equipped: owned, locked: !owned && !can));
                    }
                }
            }

            bool maxed = CurrentTier >= MaxTier;
            if (maxed)
            {
                MainButtonLabel = "Maxed";
                MainButtonEnabled = false;
            }
            else
            {
                MainButtonLabel = "Upgrade Building";
                var nextCost = NextCity();
                MainButtonEnabled = _economy == null || _economy.CanAfford(nextCost);
            }
            if (string.IsNullOrEmpty(Status))
                Status = maxed ? "Fully upgraded." : "Tap Upgrade Building to advance a tier.";
        }

        private void BuildResource()
        {
            var def = ResourceBuildingProgression.Find(_buildingId);
            Title = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : Titleize(_buildingId);
            CurrentTier = ResourceBuildingState.GetLevel(_buildingId);
            MaxTier = def != null ? def.MaxLevel : CurrentTier;

            if (def != null && def.Levels != null)
            {
                for (int i = 0; i < def.Levels.Length; i++)
                {
                    var lvl = def.Levels[i];
                    if (lvl == null) continue;
                    int level = lvl.Level;
                    bool isCurrent = level <= CurrentTier;
                    bool isNext = level == CurrentTier + 1;
                    bool locked = level > CurrentTier + 1;

                    // A resource row's cost is the cost FROM the previous level (the cost the
                    // player pays to reach THIS level). Level 1 is owned by default (no cost).
                    string costStr;
                    bool affordable = false;
                    if (level <= 1)
                    {
                        costStr = "Base";
                    }
                    else
                    {
                        var prev = def.LevelDef(level - 1);
                        costStr = prev != null ? ResourceCostString(prev.UpgradeCost, prev.MagicCost) : "";
                        if (isNext && prev != null)
                            affordable = ResourceLedger.CanAfford(prev.UpgradeCost)
                                         && (prev.MagicCost <= 0 || ResourceLedger.MagicBalance() >= prev.MagicCost);
                    }

                    string id = TierId(level);
                    string name = "Level " + level + "  (+" + lvl.YieldPerTick + " "
                                  + ResourceBuildingProgression.LabelFor(lvl.Yields) + ")";
                    _costById[id] = isCurrent ? "Owned" : costStr;
                    _upgrades.Add(new ItemVM(id, name, IconRoleTier, id, 0, "", affordable,
                                             rarity: null, equipped: isCurrent, locked: locked));
                }
            }

            bool maxed = ResourceBuildingState.IsMaxLevel(_buildingId);
            if (maxed)
            {
                MainButtonLabel = "Maxed";
                MainButtonEnabled = false;
            }
            else
            {
                MainButtonLabel = "Upgrade Building";
                var cur = ResourceBuildingState.CurrentDef(_buildingId);
                MainButtonEnabled = cur != null
                    && ResourceLedger.CanAfford(cur.UpgradeCost)
                    && (cur.MagicCost <= 0 || ResourceLedger.MagicBalance() >= cur.MagicCost);
            }
            if (string.IsNullOrEmpty(Status))
                Status = maxed ? "Fully upgraded." : "Tap Upgrade Building to advance a level.";
        }

        private void BuildUnknown()
        {
            Title = Titleize(_buildingId);
            CurrentTier = 0;
            MaxTier = 0;
            MainButtonLabel = "Nothing to upgrade";
            MainButtonEnabled = false;
            if (string.IsNullOrEmpty(Status)) Status = "This building has no upgrades.";
        }

        // ── Helpers (pure) ───────────────────────────────────────────────────────

        private string NextTierId() => TierId(CurrentTier + 1);

        private EcoCost NextCity()
        {
            var def = BuildingTierCatalog.TierOf(_buildingId, CurrentTier + 1);
            return def != null
                ? new EcoCost { Wood = def.CostWood, Food = def.CostFood, Crystals = def.CostCrystal }
                : new EcoCost();
        }

        private static string TierId(int tier) => "tier-" + tier;

        private string CostString(EcoCost c)
        {
            var parts = new List<string>();
            if (c.Wood > 0) parts.Add(c.Wood + " Wood");
            if (c.Food > 0) parts.Add(c.Food + " Food");
            if (c.Iron > 0) parts.Add(c.Iron + " Iron");
            if (c.Crystals > 0) parts.Add(c.Crystals + " Crystals");
            if (c.Coins > 0) parts.Add(c.Coins + " Gold");
            return parts.Count == 0 ? "Free" : string.Join(" · ", parts);
        }

        private static string ResourceCostString(IReadOnlyList<ResourceCost> costs, int magic)
        {
            var parts = new List<string>();
            if (costs != null)
                foreach (var c in costs)
                    parts.Add(c.Amount + " " + ResourceBuildingProgression.LabelFor(c.Resource));
            if (magic > 0) parts.Add(magic + " Magic");
            return parts.Count == 0 ? "Free" : string.Join(" · ", parts);
        }

        private static string Titleize(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Building";
            id = id.Replace('-', ' ').Replace('_', ' ');
            return char.ToUpper(id[0]) + (id.Length > 1 ? id.Substring(1) : "");
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
