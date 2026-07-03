// =============================================================================
// BuildingUpgradeVM — the building ENHANCEMENT (perk-grid) panel's PURE ViewModel.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// Owner redo 2026-07-02: the panel is a Warcraft-3-style PERK GRID — every tier
// and research perk is a TILE the player taps to UNLOCK. VERBIAGE LAW: "Unlock
// perk" / "Enhancement" language only; the words "Upgrade Building" never appear.
//
// ALL grid STATE + LOGIC lives here, view-agnostic. Mirrors ShopVM:
//   * implements DeNelle.Core.UI.Mvvm.IPanelViewModel (Title / Changed / Close / Dispose)
//   * NO UnityEngine UI types; unit-testable without a scene (ARCHITECTURE_PRINCIPLES §2/§2c).
//   * the View binds it, re-renders on Changed, and routes taps back as commands;
//     the View NEVER reads game state (ui-mvvm-binding-seam rule).
//   * per-tile cost/effect strings are exposed via CostFor(id)/EffectFor(id); the
//     one-line concrete EFFECT comes from building-tiers.json (tier/perk "effect").
//
// TWO building families, decided EXACTLY like DialogueCommandBridge.CmdStructureStatus:
//   * CITY tier buildings  — BuildingTierCatalog.IsUpgradable(id): the WO-430 tier
//     ladder. Unlocking the next tier spends via BuildingUpgradeService.TryUpgrade.
//   * LEGACY resource buildings — ResourceBuildingProgression.IsResourceBuilding(id):
//     Farm/Lumbermill/Forge level curve via ResourceBuildingState.TryUpgrade(id).
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
    /// Pure ViewModel for the building enhancement (perk-grid) panel. Exposes every tier +
    /// research perk as <see cref="Perks"/> (one <see cref="ItemVM"/> tile each: owned=lit,
    /// next=gold affordance, locked=dim + requirement line) plus a status line. Raises
    /// <see cref="Changed"/> after each unlock and on any economy / modifier / level change.
    /// </summary>
    public sealed class BuildingUpgradeVM : IPanelViewModel, IDisposable
    {
        /// <summary>Tile id for the synthetic "Unlock Village Tier" affordance (the Heart-of-Elarion
        /// tech-gate). Injected at the TOP of every city/resource building's grid so the player has
        /// ONE place to raise the global Village/Stronghold Tier that unlocks the WO-432 tier-2+
        /// enhancements + research perks. Tapping it routes to VillageTierService.TryUpgrade().</summary>
        public const string VillageTierRowId = "villagetier";

        /// <summary>Icon role key on each tier tile (the View maps it to art; no game state).</summary>
        public const string IconRoleTier = "tier";
        /// <summary>Icon role key on each research-perk tile (the View maps it to the perk's
        /// Resources/HudIcons/BuildingUpgrades/&lt;iconId&gt; sprite). WO-432.</summary>
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

        private readonly List<ItemVM> _perks = new List<ItemVM>();
        // Per-tile cost/effect strings, keyed by the tile's ItemVM.Id — the View reads them
        // through CostFor(id)/EffectFor(id) so it renders purely from VM data (no catalog re-pull).
        private readonly Dictionary<string, string> _costById = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _effectById = new Dictionary<string, string>();

        /// <summary>
        /// The View-side entry point (audit §3.1): resolves the economy handle + the default
        /// building HERE so the View never touches EconomyService/BuildingTierCatalog itself.
        /// A null/empty buildingId falls back to the first catalog building (generic open).
        /// </summary>
        public static BuildingUpgradeVM CreateDefault(string buildingId, Action onClose)
        {
            if (string.IsNullOrEmpty(buildingId)) buildingId = DefaultBuildingId();
            return new BuildingUpgradeVM(buildingId, EconomyService.Instance, onClose);
        }

        private static string DefaultBuildingId()
        {
            var all = BuildingTierCatalog.All;
            if (all != null && all.Count > 0 && all[0] != null) return all[0].Id;
            return ResourceBuildingProgression.FarmId;
        }

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

            // §12 open trace: "[Flow:Upgrade] <building> grid: N perks, M owned, next=<id>".
            int owned = 0;
            string next = null;
            foreach (var p in _perks)
            {
                if (p.Equipped) owned++;
                else if (next == null && !p.Locked) next = p.Id;
            }
            FlowTrace.Step("Upgrade", _buildingId + " grid: " + _perks.Count + " perks, "
                + owned + " owned, next=" + (next ?? "none"));
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

        /// <summary>One TILE per tier + research perk (name + Affordable + owned/locked flags +
        /// LockReason requirement line). The View lays these out as the perk grid. Never null.</summary>
        public IReadOnlyList<ItemVM> Perks => _perks;

        /// <summary>Last action / hint line for the status row.</summary>
        public string Status { get; private set; }

        /// <summary>The cost string for a tile id (View renders it from here — no catalog re-pull).</summary>
        public string CostFor(string id) =>
            id != null && _costById.TryGetValue(id, out var c) ? c : "";

        /// <summary>The one-line concrete EFFECT for a tile id ("Farm +25% yield" / "offline bucket
        /// holds more") — sourced from building-tiers.json "effect" (or derived for legacy levels).</summary>
        public string EffectFor(string id) =>
            id != null && _effectById.TryGetValue(id, out var e) ? e : "";

        /// <summary>Live wallet readout (View rebuilds its "Wood … Food … Crystals" line from these).</summary>
        public int Wood     => _economy?.Wood ?? 0;
        public int Food     => _economy?.Food ?? 0;
        public int Iron     => _economy?.Iron ?? 0;
        public int Crystals => _economy?.Crystals ?? 0;
        public int Coins    => _economy?.Coins ?? 0;

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Unlock the NEXT tier/level (city -> BuildingUpgradeService, resource -> ResourceBuildingState).</summary>
        public void UpgradeNext()
        {
            if (_isCity)
            {
                int next = CurrentTier + 1;
                if (next > MaxTier) { Status = "Every enhancement here is already unlocked."; Raise(); return; }

                // WO-432 TIER GATE — mirror BuildingUpgradeService's gate so a tier-locked
                // unlock reports an HONEST reason, not the generic "can't afford" (the service
                // returns a bare false for BOTH cases). Resources are fine; she's village-tier-gated.
                var nextDef = BuildingTierCatalog.TierOf(_buildingId, next);
                int villageTier = GameStateService.Instance?.State?.VillageTier ?? 0;
                if (nextDef != null)
                {
                    FlowTrace.Step("Upgrade", "unlock " + _buildingId + " tier=" + next
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
                if (ok)
                {
                    Status = "Tier " + next + " unlocked.";
                    FlowTrace.Step("Upgrade", _buildingId + " unlocked tier-" + next);
                }
                else Status = "You can't afford that yet.";
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
                        Status = "Level " + ResourceBuildingState.GetLevel(_buildingId) + " unlocked.";
                        FlowTrace.Step("Upgrade", _buildingId + " unlocked level-"
                            + ResourceBuildingState.GetLevel(_buildingId));
                        break;
                    case UpgradeResult.Insufficient: Status = "You can't afford that yet."; break;
                    case UpgradeResult.MaxLevel:     Status = "Every enhancement here is already unlocked."; break;
                    case UpgradeResult.NeedMagic:    Status = "That enhancement needs Magic to unlock."; break;
                    default:                         Status = "Nothing to unlock here."; break;
                }
                Rebuild();
                Raise();
                return;
            }

            Status = "Nothing to unlock here.";
            Raise();
        }

        /// <summary>Perk-tile tap. The ladder unlocks the NEXT tier only, so tapping the next
        /// tier tile unlocks it; tapping any other tier just re-states the hint (no model change).</summary>
        public void Select(string tierId)
        {
            if (string.IsNullOrEmpty(tierId)) return;
            // WO-481 — the "Unlock Village Tier" tile (the Heart-of-Elarion tech-gate). Raise the
            // global Village/Stronghold Tier with Crystals via VillageTierService; on success the
            // newly-gated tier-2+ enhancements + research perks open immediately (Rebuild repaints
            // the now-unlocked tiles because the per-tier RequiresVillageTier gate now passes).
            if (tierId == VillageTierRowId)
            {
                if (VillageTierService.IsMax)
                {
                    Status = "Village Tier is already at its highest level.";
                    Raise();
                    return;
                }
                int crystals = _economy?.Crystals ?? 0;
                int cost = VillageTierService.NextCost();
                if (crystals < cost)
                {
                    Status = "Need " + cost + " Crystals to raise the Village Tier (you have " + crystals + ").";
                    Raise();
                    return;
                }
                if (VillageTierService.TryUpgrade())
                {
                    int n = VillageTierService.Current;
                    FlowTrace.Step("Upgrade", _buildingId + " unlocked villagetier-" + n
                        + " (unlocks tier-" + n + "+ enhancements).");
                    Status = "Village Tier raised to " + n + " — higher enhancements unlocked.";
                }
                else
                {
                    Status = "Couldn't raise the Village Tier right now.";
                }
                Rebuild();
                Raise();
                return;
            }
            // WO-432 — a research-perk tile ("perk:<perkId>"): unlock it with Gold (Coins).
            if (tierId.StartsWith("perk:", StringComparison.Ordinal))
            {
                string perkId = tierId.Substring("perk:".Length);
                if (BuildingPerkService.TryResearch(_buildingId, perkId))
                {
                    Status = "Perk unlocked.";
                    FlowTrace.Step("Upgrade", _buildingId + " unlocked perk " + perkId);
                }
                else
                {
                    BuildingPerkService.CanResearch(_buildingId, perkId, out string why);
                    Status = !string.IsNullOrEmpty(why) ? why : "Can't unlock that perk yet.";
                }
                Rebuild();
                Raise();
                return;
            }
            if (tierId == NextTierId()) { UpgradeNext(); return; }
            Status = "Tap the gold tile to unlock the next enhancement.";
            Raise();
        }

        // ── Build the tiles + title/status (no Unity types) ─────────────────────

        private void Rebuild()
        {
            _perks.Clear();
            _costById.Clear();
            _effectById.Clear();

            if (_isCity) BuildCity();
            else if (_isResource) BuildResource();
            else BuildUnknown();

            // WO-481 — surface the Heart-of-Elarion Village Tier control as the FIRST tile of every
            // building's grid (the enhancement panel is the surface the player already opens with F
            // at any upgrade building, so this needs zero new UI). This is the SOLE caller of
            // VillageTierService.TryUpgrade — without it GameState.VillageTier is stuck at 0 and
            // every RequiresVillageTier > 0 tier / research perk is permanently locked.
            PrependVillageTierRow();
        }

        /// <summary>
        /// Inserts the synthetic "Unlock Village Tier" tile at the top of <see cref="Perks"/>.
        /// Cost source = VillageTierService.NextCost() (Crystals, the premium progression currency;
        /// felt-tunable in VillageTierService). The View renders it like any tile; tapping it
        /// routes to <see cref="Select"/> which calls VillageTierService.TryUpgrade().
        /// </summary>
        private void PrependVillageTierRow()
        {
            int cur = VillageTierService.Current;
            bool maxed = VillageTierService.IsMax;
            int cost = VillageTierService.NextCost();
            int crystals = _economy?.Crystals ?? 0;
            bool affordable = !maxed && crystals >= cost;

            string name = maxed
                ? "Village Tier " + cur + " (Max)"
                : "Unlock Village Tier " + (cur + 1);
            _costById[VillageTierRowId] = maxed ? "Maxed" : (cost + " Crystals");
            _effectById[VillageTierRowId] = maxed
                ? "Every tier gate is open"
                : "Opens tier-" + (cur + 1) + " enhancements everywhere";

            // equipped=maxed -> renders as an owned (lit) tile; locked=false so a non-max tile is always tappable.
            _perks.Insert(0, new ItemVM(VillageTierRowId, name, IconRoleTier, VillageTierRowId, 0, "",
                                        affordable, rarity: null, equipped: maxed, locked: false));
        }

        private void BuildCity()
        {
            var def = BuildingTierCatalog.Find(_buildingId);
            Title = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : Titleize(_buildingId);
            CurrentTier = ModifierService.TierOf(_buildingId);
            MaxTier = BuildingTierCatalog.MaxTier(_buildingId);
            int villageTier = GameStateService.Instance?.State?.VillageTier ?? 0;

            if (def != null && def.Tiers != null)
            {
                foreach (var t in def.Tiers)
                {
                    if (t == null) continue;
                    int tier = t.Tier;
                    bool isCurrent = tier <= CurrentTier;
                    bool isNext = tier == CurrentTier + 1;
                    bool gated = isNext && t.RequiresVillageTier > villageTier;
                    bool locked = tier > CurrentTier + 1 || gated;
                    string lockReason = null;
                    if (gated) lockReason = "Requires Village Tier " + t.RequiresVillageTier;
                    else if (tier > CurrentTier + 1) lockReason = "Unlock Tier " + (tier - 1) + " first";

                    var cost = new EcoCost { Wood = t.CostWood, Food = t.CostFood, Crystals = t.CostCrystal };
                    bool affordable = isNext && !gated && (_economy == null || _economy.CanAfford(cost));
                    string costStr = CostString(cost);

                    string id = TierId(tier);
                    string name = "Tier " + tier + " — " + (!string.IsNullOrEmpty(t.Name) ? t.Name : ("Tier " + tier));
                    _costById[id] = isCurrent ? "Unlocked" : costStr;
                    _effectById[id] = t.Effect ?? "";
                    // Equipped flag carries "owned/lit"; Locked carries "not yet reachable" (+ reason).
                    _perks.Add(new ItemVM(id, name, IconRoleTier, id, 0, "", affordable,
                                          rarity: null, equipped: isCurrent, locked: locked,
                                          lockReason: lockReason));
                }
            }

            // WO-432 RESEARCH TILES — every perk unlocked at a REACHED tier shows as a Gold-cost tile
            // in the grid (owned = lit; gate-not-met = dimmed + requirement; else gold affordance).
            // A perk-tile tap routes to BuildingPerkService via Select("perk:<id>"). Tiles are
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
                        string why = null;
                        bool can = !owned && BuildingPerkService.CanResearch(_buildingId, p.Id, out why);
                        bool affordable = can && (_economy == null || _economy.Coins >= p.GoldCost);
                        string rid = "perk:" + p.Id;
                        // Signature marker is ASCII "*" not "★" (tofu fix 2026-07-02): U+2605 is
                        // in NO project SDF font (scanned — zero m_Unicode:9733 hits), so ★
                        // rendered as a box in builds. A VM emits strings (no procedural Image
                        // like StarRatingRow/EndStateView), so the font-safe ASCII star it is.
                        string pname = (p.IsSignature ? "* " : "") + (!string.IsNullOrEmpty(p.Name) ? p.Name : p.Id);
                        _costById[rid] = owned ? "Unlocked" : (p.GoldCost + " Gold");
                        _effectById[rid] = p.Effect ?? "";
                        string iconKey = string.IsNullOrEmpty(p.IconId) ? p.Id : p.IconId;
                        bool perkLocked = !owned && !can;
                        _perks.Add(new ItemVM(rid, pname, IconRolePerk, iconKey, 0, "", affordable,
                                              rarity: null, equipped: owned, locked: perkLocked,
                                              lockReason: perkLocked && !string.IsNullOrEmpty(why)
                                                  ? why : (perkLocked ? "Unlock Tier " + t.Tier + " first" : null)));
                    }
                }
            }

            if (string.IsNullOrEmpty(Status))
                Status = CurrentTier >= MaxTier
                    ? "Every enhancement here is unlocked."
                    : "Tap the gold tile to unlock the next enhancement.";
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

                    // A resource tile's cost is the cost FROM the previous level (the cost the
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
                    string name = "Level " + level;
                    _costById[id] = isCurrent ? "Unlocked" : costStr;
                    // Legacy levels have no authored effect string — derive the concrete yield line
                    // ("+6 Food per tick") from the level def, the owner's "Farm +25% yield" shape.
                    _effectById[id] = "+" + lvl.YieldPerTick + " "
                                      + ResourceBuildingProgression.LabelFor(lvl.Yields) + " per tick";
                    _perks.Add(new ItemVM(id, name, IconRoleTier, id, 0, "", affordable,
                                          rarity: null, equipped: isCurrent, locked: locked,
                                          lockReason: locked ? "Unlock Level " + (level - 1) + " first" : null));
                }
            }

            if (string.IsNullOrEmpty(Status))
                Status = ResourceBuildingState.IsMaxLevel(_buildingId)
                    ? "Every enhancement here is unlocked."
                    : "Tap the gold tile to unlock the next enhancement.";
        }

        private void BuildUnknown()
        {
            Title = Titleize(_buildingId);
            CurrentTier = 0;
            MaxTier = 0;
            if (string.IsNullOrEmpty(Status)) Status = "This building has no enhancements.";
        }

        // ── Helpers (pure) ───────────────────────────────────────────────────────

        private string NextTierId() => TierId(CurrentTier + 1);

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
