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
using DeNelle.Core.Jobs;
using DeNelle.Core.State;
using DeNelle.Core.UI.Mvvm;
// Disambiguate the two ResourceCost types in scope: the build-economy cost (Wood/Food/
// Crystals — what IEconomy spends) vs. the legacy harvest cost (Resource/Amount). The
// unqualified `ResourceCost` in this Progression namespace is the harvest one.
using EcoCost = DeNelle.Village.ResourceCost;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// WO-895 — the ONE action button's state on the building upgrade panel. The View is a pure
    /// reflection of this; it never invents a second state. Resolved from the SAME authorities the
    /// build/queue system uses (BuildTimerService active/pending jobs + the tier catalog gates +
    /// the GameState wallet), so the button can never disagree with the Obsidian queue.
    /// </summary>
    public enum UpgradeActionState
    {
        /// <summary>Affordable + not gated: tapping starts (or queues) the upgrade.</summary>
        Ready = 0,
        /// <summary>The wallet is short of the next tier's cost.</summary>
        MissingResources = 1,
        /// <summary>The job is in the Builder channel's PENDING queue, waiting for a free crew.</summary>
        Queued = 2,
        /// <summary>A crew is actively working this building's upgrade (live countdown).</summary>
        InProgress = 3,
        /// <summary>The next tier is behind the global Village Tier gate — the button raises THAT.</summary>
        VillageGated = 4,
        /// <summary>Every tier/level here is already owned.</summary>
        Maxed = 5,
        /// <summary>No ladder for this building (nothing to show).</summary>
        Unavailable = 6,
    }

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
        // WO-680 — per-tile "this is the building-upgrade KEY" sub-line ("UPGRADES FORGE TO
        // TIER 2") + which gate blocks the tile right now. Both composed HERE (data-driven,
        // never hardcoded in the View) so the View stays a dumb skin.
        private readonly Dictionary<string, string> _keyLineById = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _gateById = new Dictionary<string, string>();

        /// <summary>WO-680 gate names for <see cref="GateFor"/> (the View seats the village
        /// Unlock CTA ONLY on a village-gated band; text composes from LockReason as ever).</summary>
        public const string GateVillage = "village";
        public const string GateBuildingTier = "building-tier";
        public const string GateCost = "cost";

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
            // COLLECTOR ID RESOLUTION (collector bug fix): a placed collector opens this panel under
            // its catalog id ("collector_lumbermill"/"collector_farm"), but its tier/level ladder is
            // keyed on the bare collectorBuildingId ("lumbermill"/"farm"). Normalize HERE so EVERY
            // open path (BuildMode, CastleVendorNpc, HudKit) resolves to the right ladder -- without
            // it a collector id classified as neither city nor resource and rendered an empty grid.
            // Unchanged for every non-collector id (ResolveUpgradeId is a pass-through there).
            _buildingId = DeNelle.Core.Catalog.CatalogRegistry.ResolveUpgradeId(buildingId ?? "");
            _economy = economy;
            _onClose = onClose;

            // Decide the family EXACTLY like CmdStructureStatus (city tiers win; else legacy).
            _isCity = BuildingTierCatalog.IsUpgradable(_buildingId);
            _isResource = !_isCity && ResourceBuildingProgression.IsResourceBuilding(_buildingId);

            // WO-842 (F8 2026-08-02, arcane-tower "TryUpgrade FALSE" with a full wallet):
            // these handlers used to call Raise() ONLY — the View re-rendered STALE tiles.
            // When a build timer completed while the panel sat open (ModifierService.
            // Recompute -> Changed), CurrentTier stayed stale, the owned tier still showed
            // as the gold "next" tile, and tapping it sent targetTier == the ALREADY-OWNED
            // tier into BuildingUpgradeService.TryUpgrade, whose next-tier guard silently
            // returned false — misreported as "can't afford" against 985k wood. Every
            // change now REBUILDS the grid (fresh CurrentTier + affordability) before Raise.
            if (_economy != null)
            {
                _ecoHandler = _ => { Rebuild(); Raise(); };
                _economy.OnChanged += _ecoHandler;
            }
            _modHandler = () => { Rebuild(); Raise(); };
            ModifierService.Changed += _modHandler;
            _levelHandler = _ => { Rebuild(); Raise(); };
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

        /// <summary>The catalog building id this VM drives — lets the panel's CTA read the
        /// live timer gates (busy / no free crew) for a pre-tap reason (F8 2026-07-30).</summary>
        public string BuildingId => _buildingId;

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

        /// <summary>WO-680 — the "this tile IS the building upgrade" sub-line for a tier/level tile
        /// ("UPGRADES FORGE TO TIER 2"), composed from the building's display name. Empty for perks.</summary>
        public string KeyLineFor(string id) =>
            id != null && _keyLineById.TryGetValue(id, out var k) ? k : "";

        /// <summary>WO-680 — which gate blocks a tile right now: <see cref="GateVillage"/> (Village
        /// Tier too low), <see cref="GateBuildingTier"/> (a lower tier tile is unowned),
        /// <see cref="GateCost"/> (next but unaffordable), or "" (owned / open). The View uses it to
        /// seat the village Unlock CTA ONLY where the VILLAGE gate is the blocker.</summary>
        public string GateFor(string id) =>
            id != null && _gateById.TryGetValue(id, out var g) ? g : "";

        // ── WO-841 live-countdown feed ──────────────────────────────────────────
        // The View's per-second "Under construction" tick reads THESE (MVVM: the VM
        // owns the queue-snapshot read; the View only renders). Same BuildTimerService
        // seam the tap-path mirrors above (F8-51) — pure reads, no state mutated, and
        // both return the idle shape (false/0) when the BuildTimers flag is off.

        /// <summary>True while a build/upgrade job is in flight for this building (WO-841).</summary>
        public bool UnderConstruction
        {
            get
            {
                var t = DeNelle.Core.FeatureFlags.BuildTimers ? BuildTimerService.Instance : null;
                return t != null && t.IsBuilding(_buildingId);
            }
        }

        /// <summary>Whole seconds left on this building's in-flight job; 0 when idle (WO-841).</summary>
        public int UnderConstructionSeconds
        {
            get
            {
                var t = DeNelle.Core.FeatureFlags.BuildTimers ? BuildTimerService.Instance : null;
                if (t == null || !t.IsBuilding(_buildingId)) return 0;
                return (int)t.RemainingSeconds(_buildingId);
            }
        }

        // ── WO-895 NEXT-UPGRADE surface (the "next only" card) ──────────────────
        // The panel no longer renders a 6-tier rail — it renders WHERE YOU ARE plus ONE
        // next-upgrade card. Everything that card needs is composed HERE (View stays a dumb
        // skin): the next tier's name, a plain description, its bonuses as SEPARATE lines,
        // its cost as STRUCTURED lines (so the short resource is flagged as TEXT, never a
        // red tint — colourblind law), and the ONE action button's state.

        /// <summary>One structured cost line for the next upgrade — amount, wallet balance, and
        /// whether the wallet is SHORT. The View renders the shortfall as words + a glyph; the
        /// owner is red/green colourblind, so a colour tint may never be the only signal.</summary>
        public struct UpgradeCostLine
        {
            /// <summary>ASCII resource name ("Wood", "Food", "Crystals", "Iron", "Magic").</summary>
            public string Label;
            /// <summary>Amount the upgrade charges.</summary>
            public int Amount;
            /// <summary>Wallet balance for this resource right now.</summary>
            public int Have;
            /// <summary>True when <see cref="Have"/> &lt; <see cref="Amount"/>.</summary>
            public bool Short;
            /// <summary>How many more units are needed (0 when not short).</summary>
            public int Missing => Short ? Amount - Have : 0;
        }

        private string _currentTierName = "";
        private string _nextTierName = "";
        private string _nextDescription = "";
        private readonly List<string> _nextBonuses = new List<string>();
        private readonly List<UpgradeCostLine> _nextCostLines = new List<UpgradeCostLine>();
        private bool _nextAffordable;
        private int _nextRequiresVillageTier;   // 0 = no village gate on the next tier
        private int _villageTierNow;

        /// <summary>"Tier" for a city building, "Level" for a legacy resource building. The card's
        /// copy is composed from this so ONE card serves both families (owner: a drill-in from the
        /// Manage screen lands here and must stand alone).</summary>
        public string TierWord => _isResource ? "Level" : "Tier";

        /// <summary>True when there is a next tier/level to show (i.e. not maxed and a ladder exists).</summary>
        public bool HasNextUpgrade => MaxTier > 0 && CurrentTier < MaxTier;

        /// <summary>The tier/level number the next upgrade lands on (CurrentTier + 1).</summary>
        public int NextTier => CurrentTier + 1;

        /// <summary>Display name of the tier/level the player is on now ("" before the first tier).</summary>
        public string CurrentTierName => _currentTierName;

        /// <summary>Display name of the NEXT tier ("Drill Yard"). "" when maxed.</summary>
        public string NextTierName => _nextTierName;

        /// <summary>Plain sentence describing what the next upgrade does structurally
        /// ("Raises Barracks to Tier 3 of 6."). Never truncated by the View.</summary>
        public string NextDescription => _nextDescription;

        /// <summary>The next tier's bonuses as SEPARATE lines — each renders on its own row, so
        /// nothing is jammed into one clipped run-on string (WO-895 §1).</summary>
        public IReadOnlyList<string> NextBonuses => _nextBonuses;

        /// <summary>The next tier's cost, one structured line per resource.</summary>
        public IReadOnlyList<UpgradeCostLine> NextCostLines => _nextCostLines;

        /// <summary>True when the whole next-upgrade cost is affordable from the wallet the tap charges.</summary>
        public bool NextAffordable => _nextAffordable;

        /// <summary>Village Tier required by the next tier (0 = ungated).</summary>
        public int NextRequiresVillageTier => _nextRequiresVillageTier;

        /// <summary>The Village Tier the player holds right now (the gate copy reads from this).</summary>
        public int VillageTierNow => _villageTierNow;

        /// <summary>
        /// WO-895 — the ONE action button's live state. Read from the REAL authorities in the same
        /// order the tap path checks them, so the button always states the true blocker:
        /// maxed -&gt; queue (pending = Queued, active = InProgress) -&gt; village gate -&gt; cost -&gt; Ready.
        /// A full crew set no longer blocks Ready: the tap QUEUES the job (Obsidian Builder channel).
        /// </summary>
        public UpgradeActionState ActionState
        {
            get
            {
                if (!_isCity && !_isResource) return UpgradeActionState.Unavailable;
                if (!HasNextUpgrade) return UpgradeActionState.Maxed;

                var t = DeNelle.Core.FeatureFlags.BuildTimers ? BuildTimerService.Instance : null;
                if (t != null && t.IsBuilding(_buildingId))
                    return IsPendingInBuilderQueue(t) ? UpgradeActionState.Queued : UpgradeActionState.InProgress;

                if (_nextRequiresVillageTier > _villageTierNow) return UpgradeActionState.VillageGated;
                if (!_nextAffordable) return UpgradeActionState.MissingResources;
                return UpgradeActionState.Ready;
            }
        }

        /// <summary>Whole seconds left on this building's in-flight upgrade (0 when idle).
        /// While QUEUED this is the job's FULL duration — what it will take once a crew frees.</summary>
        public int ActionRemainingSeconds => UnderConstructionSeconds;

        /// <summary>0..1 progress of the in-flight job (0 while queued) — the button's fill bar
        /// reads this, so "in progress" is distinguishable by SHAPE, never by colour alone.</summary>
        public float ActionProgress
        {
            get
            {
                var t = DeNelle.Core.FeatureFlags.BuildTimers ? BuildTimerService.Instance : null;
                if (t == null || !t.IsBuilding(_buildingId)) return 0f;
                return t.Progress(_buildingId);
            }
        }

        /// <summary>True when this building's Builder job is waiting in the PENDING queue rather
        /// than running. The Obsidian queue is the sole authority — no second state is invented.</summary>
        private bool IsPendingInBuilderQueue(BuildTimerService t)
        {
            var pending = t.PendingJobsOf(ChannelId.Builder);
            if (pending == null) return false;
            for (int i = 0; i < pending.Count; i++)
                if (string.Equals(pending[i].StructureId, _buildingId, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// WO-895 — the ONE command behind the panel's one true button. Starts (or queues) the next
        /// tier/level. Routes to the SAME <see cref="UpgradeNext"/> execute path the old rail used,
        /// so there is exactly one spend/queue mechanism.
        /// </summary>
        public void StartNextUpgrade()
        {
            FlowTrace.Step("Upgrade", _buildingId + " StartNextUpgrade tapped: state=" + ActionState
                + " next=" + TierWord.ToLowerInvariant() + "-" + NextTier
                + " affordable=" + _nextAffordable);
            UpgradeNext();
        }

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
                // WO-842 stale-grid guard: if the LIVE tier moved since the last Rebuild
                // (timer completion / another surface upgraded), resync instead of sending
                // a mismatched targetTier into the service (whose next-tier guard returns a
                // bare false that read as "can't afford" — the captured F8). Honest + traced.
                int liveTier = ModifierService.TierOf(_buildingId);
                if (liveTier != CurrentTier)
                {
                    FlowTrace.Warn("Upgrade", _buildingId + " grid was STALE (vm tier=" + CurrentTier
                        + " live tier=" + liveTier + ") - resynced instead of a mismatched unlock (WO-842)");
                    Status = liveTier > CurrentTier
                        ? "Tier " + liveTier + " is already unlocked - grid refreshed."
                        : "Grid refreshed.";
                    Rebuild();
                    Raise();
                    return;
                }

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

                // F8-51 — mirror the service's timer gate for an HONEST status (the service
                // returns a bare false for busy AND unaffordable). Same one-timer service.
                var timerSvc = DeNelle.Core.FeatureFlags.BuildTimers ? BuildTimerService.Instance : null;
                if (timerSvc != null && timerSvc.IsBuilding(_buildingId))
                {
                    Status = "Under construction — " + (int)timerSvc.RemainingSeconds(_buildingId)
                             + "s until work here finishes.";
                    // F8 2026-07-30: these mirror gates emitted NO trace, so a busy refusal was
                    // invisible in the log while the resources-shaped Fail below got the blame.
                    FlowTrace.Step("Upgrade", _buildingId + " tier-" + next + " refused: under construction ("
                        + (int)timerSvc.RemainingSeconds(_buildingId) + "s left).");
                    Raise();
                    return;
                }
                // WO-895: a FULL crew set no longer REFUSES the tap — the Obsidian Builder
                // channel QUEUES the job and pulls it the moment a crew frees (the engine has
                // always done this; only these mirror gates rejected). The button then shows
                // "Queued" until the pull. No second state, no dead click.

                bool ok = BuildingUpgradeService.TryUpgrade(_buildingId, next);
                if (ok)
                {
                    // F8-51 — with timers on, a successful buy STARTS the work; the tier
                    // lands at completion (the tile lights up then).
                    if (timerSvc != null && timerSvc.IsBuilding(_buildingId))
                    {
                        Status = IsPendingInBuilderQueue(timerSvc)
                            ? "Tier " + next + " queued - it starts when a builder frees up."
                            : "Tier " + next + " under construction - "
                              + (int)timerSvc.RemainingSeconds(_buildingId) + "s.";
                        FlowTrace.Step("Upgrade", _buildingId + " tier-" + next + " timer started");
                    }
                    else
                    {
                        Status = "Tier " + next + " unlocked.";
                        FlowTrace.Step("Upgrade", _buildingId + " unlocked tier-" + next);
                    }
                }
                else
                {
                    Status = "You can't afford that yet.";
                    // CLAUDE.md 12 - never a silent no-op again: the service logs wallet-vs-cost;
                    // mirror an honest [Flow:Upgrade] Fail here naming the tier cost that was short.
                    if (nextDef != null)
                        FlowTrace.Fail("Upgrade", _buildingId + " tier-" + next
                            + " UpgradeNext -> TryUpgrade FALSE (needed W" + nextDef.CostWood
                            + "/F" + nextDef.CostFood + "/C" + nextDef.CostCrystal
                            + ", have W" + ResourceLedger.Balance(HarvestResource.Wood)
                            + "/F" + ResourceLedger.Balance(HarvestResource.Food)
                            + "/C" + ResourceLedger.Balance(HarvestResource.Crystals) + ")");
                }
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
                    // F8-51 — timer states: work already running here (locked), or the buy
                    // just STARTED the work (level lands when the timer completes).
                    case UpgradeResult.InProgress:
                        Status = "Under construction — finish the current work first.";
                        break;
                    case UpgradeResult.Started:
                    {
                        var t = BuildTimerService.Instance;
                        int rem = t != null ? (int)t.RemainingSeconds(_buildingId) : 0;
                        // WO-895 — a full crew set QUEUES the job now (it no longer refuses),
                        // so say which of the two actually happened.
                        Status = t != null && IsPendingInBuilderQueue(t)
                            ? "Upgrade queued - it starts when a builder frees up."
                            : "Upgrade under construction - " + rem + "s.";
                        FlowTrace.Step("Upgrade", _buildingId + " level timer started");
                        break;
                    }
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
                    Status = "Need " + DeNelle.Core.UI.ElarionUi.CompactNumber(cost)
                           + " Crystals to raise the Village Tier (you have "
                           + DeNelle.Core.UI.ElarionUi.CompactNumber(crystals) + ").";   // WO-697
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
            _keyLineById.Clear();
            _gateById.Clear();
            _nextBonuses.Clear();
            _nextCostLines.Clear();
            _currentTierName = "";
            _nextTierName = "";
            _nextDescription = "";
            _nextAffordable = false;
            _nextRequiresVillageTier = 0;
            _villageTierNow = GameStateService.Instance?.State?.VillageTier ?? 0;

            if (_isCity) BuildCity();
            else if (_isResource) BuildResource();
            else BuildUnknown();

            // WO-895 — compose the NEXT-upgrade card's content from whichever ladder just built.
            ComposeNextUpgrade();

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

            // WO-680 — at max Village Tier the village gate is OPEN: emit NO control at all.
            // The old "(Max)" tile fed the band-header CTA a "Maxed" cost string, and the View
            // composed the dead "Unlock Maxed" button from it. No tile -> no button, ever.
            if (VillageTierService.IsMax)
            {
                FlowTrace.Step("Upgrade", _buildingId + " villagetier gate=open (maxed at "
                    + cur + ") -> no unlock control emitted");
                return;
            }

            int cost = VillageTierService.NextCost();
            int crystals = _economy?.Crystals ?? 0;
            bool affordable = crystals >= cost;

            string name = "Unlock Village Tier " + (cur + 1);
            _costById[VillageTierRowId] = DeNelle.Core.UI.ElarionUi.CompactNumber(cost) + " Crystals";   // WO-697
            _effectById[VillageTierRowId] = "Opens tier-" + (cur + 1) + " enhancements everywhere";
            _gateById[VillageTierRowId] = affordable ? "" : GateCost;

            // locked=false so the control is always tappable (Select reports affordability honestly).
            _perks.Insert(0, new ItemVM(VillageTierRowId, name, IconRoleTier, VillageTierRowId, 0, "",
                                        affordable, rarity: null, equipped: false, locked: false));
        }

        private void BuildCity()
        {
            var def = BuildingTierCatalog.Find(_buildingId);
            Title = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : Titleize(_buildingId);
            CurrentTier = ModifierService.TierOf(_buildingId);
            MaxTier = BuildingTierCatalog.MaxTier(_buildingId);
            int villageTier = GameStateService.Instance?.State?.VillageTier ?? 0;

            // WO-680 §12 step-in: band-state resolution — the OUT line names WHICH gate blocks
            // the next tier (village vs building-tier vs cost) so "why is this locked" is one read.
            FlowTrace.Step("Upgrade", _buildingId + " band-state IN: curTier=" + CurrentTier
                + "/" + MaxTier + " villageTier=" + villageTier);
            string nextGate = "none";

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

                    var cost = new EcoCost { Wood = t.CostWood, Food = t.CostFood, Crystals = t.CostCrystal };
                    // Affordability reads the GameState-backed wallet the tap actually charges
                    // (building-upgrade blocker fix) -- NOT EconomyService's divergent in-session
                    // Wood/Iron pool. Mirrors BuildResource's ResourceLedger check below so the
                    // tile's gold affordance matches what BuildingUpgradeService.TryUpgrade will do.
                    bool affordable = isNext && !gated && BuildingUpgradeService.CanAffordTier(t);
                    string costStr = CostString(cost);

                    string lockReason = null;
                    if (gated) lockReason = "Requires Village Tier " + t.RequiresVillageTier;
                    // WO-680 gate copy names the ACTION: point at the PREVIOUS tier tile by its
                    // display name ("Unlock 'Ignite the Forge' to open Tier 2") so the thing to
                    // tap is unambiguous — composed from catalog data, never hardcoded.
                    else if (tier > CurrentTier + 1)
                        lockReason = "Unlock '" + TierDisplayName(def, tier - 1) + "' to open Tier " + tier;

                    string id = TierId(tier);
                    string name = "Tier " + tier + " — " + (!string.IsNullOrEmpty(t.Name) ? t.Name : ("Tier " + tier));
                    _costById[id] = isCurrent ? "Unlocked" : costStr;
                    _effectById[id] = t.Effect ?? "";
                    // WO-680 — mark the tier tile as THE building-upgrade key (View sub-line).
                    _keyLineById[id] = "UPGRADES " + Title.ToUpperInvariant() + " TO TIER " + tier;
                    _gateById[id] = isCurrent ? ""
                        : gated ? GateVillage
                        : tier > CurrentTier + 1 ? GateBuildingTier
                        : !affordable ? GateCost : "";
                    if (isNext) nextGate = string.IsNullOrEmpty(_gateById[id]) ? "open" : _gateById[id];
                    // Equipped flag carries "owned/lit"; Locked carries "not yet reachable" (+ reason).
                    _perks.Add(new ItemVM(id, name, IconRoleTier, id, 0, "", affordable,
                                          rarity: null, equipped: isCurrent, locked: locked,
                                          lockReason: lockReason));
                }
            }

            // WO-680 §12 step-out: the resolved blocker for the next reachable tier.
            FlowTrace.Step("Upgrade", _buildingId + " band-state OUT: next=tier-"
                + (CurrentTier + 1) + " gate=" + nextGate);

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
                        _costById[rid] = owned ? "Unlocked"
                            : (DeNelle.Core.UI.ElarionUi.CompactNumber(p.GoldCost) + " Gold");   // WO-697
                        _effectById[rid] = p.Effect ?? "";
                        string iconKey = string.IsNullOrEmpty(p.IconId) ? p.Id : p.IconId;
                        bool perkLocked = !owned && !can;
                        // WO-680 — the fallback gate copy names the tier TILE, not a bare number.
                        _perks.Add(new ItemVM(rid, pname, IconRolePerk, iconKey, 0, "", affordable,
                                              rarity: null, equipped: owned, locked: perkLocked,
                                              lockReason: perkLocked && !string.IsNullOrEmpty(why)
                                                  ? why : (perkLocked
                                                      ? "Unlock '" + TierDisplayName(def, t.Tier) + "' first" : null)));
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

            // WO-680 §12 step-in/out — mirror BuildCity's band-state trace for the level ladder
            // (no village gate here; the blockers are building-tier order and cost only).
            FlowTrace.Step("Upgrade", _buildingId + " band-state IN: curLevel=" + CurrentTier
                + "/" + MaxTier);
            string nextGate = "none";

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
                    // WO-680 — the level tile is the building-upgrade key too (level 1 is owned
                    // by default, so no key line there); gate mirrors BuildCity minus the village gate.
                    if (level > 1)
                        _keyLineById[id] = "UPGRADES " + Title.ToUpperInvariant() + " TO LEVEL " + level;
                    _gateById[id] = isCurrent ? ""
                        : locked ? GateBuildingTier
                        : !affordable ? GateCost : "";
                    if (isNext) nextGate = string.IsNullOrEmpty(_gateById[id]) ? "open" : _gateById[id];
                    _perks.Add(new ItemVM(id, name, IconRoleTier, id, 0, "", affordable,
                                          rarity: null, equipped: isCurrent, locked: locked,
                                          lockReason: locked
                                              ? "Unlock 'Level " + (level - 1) + "' to open Level " + level : null));
                }
            }

            // WO-680 §12 step-out: the resolved blocker for the next reachable level.
            FlowTrace.Step("Upgrade", _buildingId + " band-state OUT: next=level-"
                + (CurrentTier + 1) + " gate=" + nextGate);

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

        // ── WO-895 — compose the NEXT-upgrade card content ───────────────────────
        // Runs at the tail of every Rebuild, so the card is always in step with the ladder
        // that just built. Pure composition from catalog + wallet reads; no mutation.

        private void ComposeNextUpgrade()
        {
            if (_isCity) ComposeNextCity();
            else if (_isResource) ComposeNextResource();

            FlowTrace.Step("Upgrade", _buildingId + " next-card: has=" + HasNextUpgrade
                + " " + TierWord.ToLowerInvariant() + "=" + CurrentTier + "/" + MaxTier
                + " name='" + _nextTierName + "' bonuses=" + _nextBonuses.Count
                + " costLines=" + _nextCostLines.Count + " affordable=" + _nextAffordable
                + " villageGate=" + _nextRequiresVillageTier + "/" + _villageTierNow);
        }

        private void ComposeNextCity()
        {
            var def = BuildingTierCatalog.Find(_buildingId);
            if (def == null) return;

            if (CurrentTier >= 1) _currentTierName = Ascii(TierDisplayName(def, CurrentTier));
            if (!HasNextUpgrade) return;

            int next = NextTier;
            var nextDef = BuildingTierCatalog.TierOf(_buildingId, next);
            if (nextDef == null) return;

            _nextTierName = Ascii(!string.IsNullOrEmpty(nextDef.Name) ? nextDef.Name : ("Tier " + next));
            _nextDescription = "Raises " + Ascii(Title) + " to Tier " + next + " of " + MaxTier + ".";
            _nextRequiresVillageTier = nextDef.RequiresVillageTier;

            AppendEffectClauses(nextDef.Effect, _nextBonuses);
            if (nextDef.Perks != null)
                foreach (var p in nextDef.Perks)
                {
                    if (p == null) continue;
                    string pn = !string.IsNullOrEmpty(p.Name) ? p.Name : p.Id;
                    if (!string.IsNullOrEmpty(pn)) _nextBonuses.Add("Opens research: " + Ascii(pn));
                }

            AddCostLine(HarvestResource.Wood, nextDef.CostWood);
            AddCostLine(HarvestResource.Food, nextDef.CostFood);
            AddCostLine(HarvestResource.Crystals, nextDef.CostCrystal);
            _nextAffordable = BuildingUpgradeService.CanAffordTier(nextDef);
        }

        private void ComposeNextResource()
        {
            var def = ResourceBuildingProgression.Find(_buildingId);
            if (def == null) return;

            _currentTierName = "Level " + CurrentTier;
            if (!HasNextUpgrade) return;

            int next = NextTier;
            var cur = def.LevelDef(CurrentTier);      // the cost to LEAVE the current level
            var nextLvl = def.LevelDef(next);
            _nextTierName = "Level " + next;
            _nextDescription = "Raises " + Ascii(Title) + " to Level " + next + " of " + MaxTier + ".";

            if (nextLvl != null)
            {
                _nextBonuses.Add("+" + nextLvl.YieldPerTick + " "
                    + ResourceBuildingProgression.LabelFor(nextLvl.Yields) + " per harvest");
                if (cur != null && nextLvl.HarvestInterval < cur.HarvestInterval - 0.01f)
                    _nextBonuses.Add("Harvests every " + nextLvl.HarvestInterval.ToString("0.#")
                        + "s (was " + cur.HarvestInterval.ToString("0.#") + "s)");
                if (nextLvl.YieldSizeMultiplier > 1.01f)
                    _nextBonuses.Add("Haul size x" + nextLvl.YieldSizeMultiplier.ToString("0.##"));
            }

            if (cur != null)
            {
                if (cur.UpgradeCost != null)
                    foreach (var c in cur.UpgradeCost) AddCostLine(c.Resource, c.Amount);
                if (cur.MagicCost > 0)
                    _nextCostLines.Add(new UpgradeCostLine
                    {
                        Label = "Magic",
                        Amount = cur.MagicCost,
                        Have = ResourceLedger.MagicBalance(),
                        Short = ResourceLedger.MagicBalance() < cur.MagicCost,
                    });
                _nextAffordable = ResourceLedger.CanAfford(cur.UpgradeCost)
                                  && (cur.MagicCost <= 0 || ResourceLedger.MagicBalance() >= cur.MagicCost);
            }
        }

        private void AddCostLine(HarvestResource r, int amount)
        {
            if (amount <= 0) return;
            int have = ResourceLedger.Balance(r);
            _nextCostLines.Add(new UpgradeCostLine
            {
                Label = ResourceBuildingProgression.LabelFor(r),
                Amount = amount,
                Have = have,
                Short = have < amount,
            });
        }

        /// <summary>Split an authored one-line effect ("Unlocks Spearman. Troop health +8%. Structure
        /// HP +45%") into SEPARATE bonus lines. Sentence boundaries only — commas inside a clause
        /// ("damage +15%, health +10%") belong together and are never split.</summary>
        private static void AppendEffectClauses(string effect, List<string> into)
        {
            if (string.IsNullOrEmpty(effect) || into == null) return;
            var parts = effect.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in parts)
            {
                string s = Ascii(raw).Trim().TrimEnd('.').Trim();
                if (s.Length > 0) into.Add(s);
            }
        }

        /// <summary>ASCII-fold the punctuation authored data carries (em/en dash, middle dot,
        /// ellipsis, curly quotes). TMP renders non-ASCII as tofu boxes (CLAUDE.md §7 law), and
        /// building-tiers.json does contain em dashes — so every string the card shows is folded
        /// HERE rather than trusting the data.</summary>
        private static string Ascii(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            var sb = new System.Text.StringBuilder(s.Length + 4);
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '—': case '–': case '−': sb.Append('-'); break;   // em/en dash, minus
                    case '·': case '•': sb.Append('-'); break;                  // middle dot, bullet
                    case '…': sb.Append("..."); break;                               // ellipsis
                    case '‘': case '’': sb.Append('\''); break;                 // curly single quotes
                    case '“': case '”': sb.Append('"'); break;                  // curly double quotes
                    case ' ': sb.Append(' '); break;                                 // nbsp
                    default: sb.Append(ch <= 127 ? ch : '?'); break;
                }
            }
            return sb.ToString();
        }

        // ── Helpers (pure) ───────────────────────────────────────────────────────

        private string NextTierId() => TierId(CurrentTier + 1);

        private static string TierId(int tier) => "tier-" + tier;

        /// <summary>WO-680 — a tier's player-facing display name from the (already fetched) catalog
        /// def ("Ignite the Forge"); falls back to "Tier N" when unauthored. Pure data read.</summary>
        private static string TierDisplayName(BuildingUpgradeDef def, int tier)
        {
            if (def != null && def.Tiers != null)
                foreach (var t in def.Tiers)
                    if (t != null && t.Tier == tier)
                        return !string.IsNullOrEmpty(t.Name) ? t.Name : ("Tier " + tier);
            return "Tier " + tier;
        }

        // WO-697: cost numbers render through the ONE kit formatter (ElarionUi.CompactNumber
        // — verbatim below 10k, "98.6k"/"1.2m" above); the currency KEYWORDS stay intact so
        // the panel's DeriveSpendableCurrencies keyword scan keeps working.
        private string CostString(EcoCost c)
        {
            var parts = new List<string>();
            if (c.Wood > 0) parts.Add(DeNelle.Core.UI.ElarionUi.CompactNumber(c.Wood) + " Wood");
            if (c.Food > 0) parts.Add(DeNelle.Core.UI.ElarionUi.CompactNumber(c.Food) + " Food");
            if (c.Iron > 0) parts.Add(DeNelle.Core.UI.ElarionUi.CompactNumber(c.Iron) + " Iron");
            if (c.Crystals > 0) parts.Add(DeNelle.Core.UI.ElarionUi.CompactNumber(c.Crystals) + " Crystals");
            if (c.Coins > 0) parts.Add(DeNelle.Core.UI.ElarionUi.CompactNumber(c.Coins) + " Gold");
            return parts.Count == 0 ? "Free" : string.Join(" · ", parts);
        }

        private static string ResourceCostString(IReadOnlyList<ResourceCost> costs, int magic)
        {
            var parts = new List<string>();
            if (costs != null)
                foreach (var c in costs)
                    parts.Add(DeNelle.Core.UI.ElarionUi.CompactNumber(c.Amount) + " "
                              + ResourceBuildingProgression.LabelFor(c.Resource));
            if (magic > 0) parts.Add(DeNelle.Core.UI.ElarionUi.CompactNumber(magic) + " Magic");
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
