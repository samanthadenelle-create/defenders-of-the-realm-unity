// =============================================================================
// RealmMapVM — the PURE ViewModel behind RealmMapPanel (WO-826, strict MVVM).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// ALL Realm Map state projection lives here (the ClanChatVM / RumorBoardVM
// pattern): implements IPanelViewModel, carries NO UnityEngine UI types, and is
// unit-testable without a scene. The View (RealmMapPanel) renders vm.* only.
//
// Data in:
//   * RealmMapCatalog (Core) — the dual-copy realm-map.json typed loader
//     (Elarion home + 5 regions with mapPoint / gate / description).
//   * ISource — the save-progress seam (fake in tests; GameStateService live):
//     BestWave + the persisted RegionProgress discovered/cleared ledger.
//
// State derivation (mirrors the file's own _schemaNotes: RegionState is DERIVED
// at runtime from RegionProgress, never stored):
//   * home                       -> Home (always visible, never locked).
//   * ledger Cleared[id]         -> Cleared.
//   * ledger Discovered[id] OR gate satisfied -> Discovered. Gate satisfaction is
//     derived live: bestWave gate vs GameState.BestWave; regionCleared gate vs
//     the Cleared ledger. Nothing WRITES the Discovered ledger yet — that is the
//     WO-827 discovery/travel ledger; a FlowTrace.Once documents the stub. On a
//     fresh save (BestWave 0) every region therefore renders LOCKED fog, except
//     the Thornwood lights up once the village has held to wave 3 (its authored
//     trivial gate — documented WO-826 §2 choice).
//   * else                       -> Locked.
//
// Travel is a DISABLED stub until WO-827 (TravelEnabled always false; the CTA
// label carries "coming with discovery"). Colorblind law: every state is TEXT
// (StateLabel / DetailState), never colour alone. ASCII-only player strings;
// Elarion never Avalon (titles come verbatim from the catalog).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Core.World;

namespace DeNelle.Village.Hero
{
    /// <summary>
    /// Pure ViewModel for the Realm Map parchment panel. Projects the catalog +
    /// save ledger into node rows and a selection detail; holds the selection.
    /// </summary>
    public sealed class RealmMapVM : IPanelViewModel, IDisposable
    {
        // ── Save-progress seam (fake in tests; GameStateService live). ──────────
        public interface ISource
        {
            int BestWave { get; }                    // GameState.BestWave
            bool IsDiscovered(string regionId);      // RegionProgress.Discovered ledger
            bool IsCleared(string regionId);         // RegionProgress.Cleared ledger
        }

        /// <summary>Derived node state (the React RegionState minus the transient
        /// 'threatened' overlay — its weeklyRealmThreat flag is authored false).</summary>
        public enum NodeState { Home, Locked, Discovered, Cleared }

        /// <summary>One projected map node: id + title + mapPoint percents + derived state.
        /// XPercent rightward, YPercent DOWNWARD from the top-left of the map rect
        /// (the React realm-map-layout.ts convention the file was authored in).</summary>
        public readonly struct NodeRow
        {
            public readonly string Id;
            public readonly string Title;
            public readonly float XPercent;
            public readonly float YPercent;
            public readonly NodeState State;
            /// <summary>ASCII state word ("Home" / "Locked" / "Discovered" / "Cleared") —
            /// text carries the state, never colour alone.</summary>
            public readonly string StateLabel;
            public readonly bool IsHome;

            public NodeRow(string id, string title, float x, float y,
                           NodeState state, string stateLabel, bool isHome)
            {
                Id = id; Title = title; XPercent = x; YPercent = y;
                State = state; StateLabel = stateLabel; IsHome = isHome;
            }
        }

        private readonly ISource _source;
        private readonly HomeBaseDef _home;
        private readonly IReadOnlyList<RealmRegionDef> _regions;
        private readonly Action _onClose;
        private bool _disposed;

        private readonly List<NodeRow> _nodes = new List<NodeRow>();

        /// <summary>Live wiring: real catalog + the GameStateService-backed source.</summary>
        public static RealmMapVM CreateDefault(Action onClose)
            => new RealmMapVM(new StateSource(), RealmMapCatalog.Home, RealmMapCatalog.Regions, onClose);

        public RealmMapVM(ISource source, HomeBaseDef home,
                          IReadOnlyList<RealmRegionDef> regions, Action onClose)
        {
            _source = source;
            _home = home;
            _regions = regions ?? Array.Empty<RealmRegionDef>();
            _onClose = onClose;

            // WO-826 §2: the DISCOVERY ledger has no writer yet — derivation below reads
            // the persisted RegionProgress + live gates; writes land with WO-827.
            FlowTrace.Once("RealmMap", "progress-stub",
                "region discovery derivation is gate+ledger READ-ONLY (no writer until WO-827 travel/discovery)");

            Rebuild();
            // Open with the home base selected so the detail pane is never blank.
            SelectedId = _home != null ? _home.Id : (_nodes.Count > 0 ? _nodes[0].Id : null);
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────
        public event Action Changed;
        public string Title => "REALM MAP";
        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Changed = null;
        }

        // ── Read-only data the View renders ──────────────────────────────────

        /// <summary>All map nodes (home first, then regions in mapOrder). Never null.</summary>
        public IReadOnlyList<NodeRow> Nodes => _nodes;

        /// <summary>The selected node id (home by default; never null while any node exists).</summary>
        public string SelectedId { get; private set; }

        /// <summary>Detail: gilt header title for the selection.</summary>
        public string DetailTitle
        {
            get
            {
                var n = FindNode(SelectedId);
                return n.HasValue ? n.Value.Title : "The Realm";
            }
        }

        /// <summary>Detail: the state line, TEXT-encoded (colorblind law). E.g.
        /// "Home Base - you are here" / "Region - Locked".</summary>
        public string DetailState
        {
            get
            {
                var n = FindNode(SelectedId);
                if (!n.HasValue) return "";
                if (n.Value.IsHome) return "Home Base - you are here";
                return "Region - " + n.Value.StateLabel;
            }
        }

        /// <summary>Detail: WHY a locked region is locked (gate reason), or a short
        /// standing line for other states. Never null.</summary>
        public string DetailGate
        {
            get
            {
                var n = FindNode(SelectedId);
                if (!n.HasValue || n.Value.IsHome) return "";
                var region = FindRegion(n.Value.Id);
                if (region == null) return "";
                switch (n.Value.State)
                {
                    case NodeState.Cleared:    return "Cleared - this land stands safe.";
                    case NodeState.Discovered: return "Discovered - travel opens with a coming update.";
                    default:                   return "Gate: " + GateReason(region.Gate);
                }
            }
        }

        /// <summary>Detail: the authored region/home description (scrolls in the View when long).</summary>
        public string DetailBody
        {
            get
            {
                var n = FindNode(SelectedId);
                if (!n.HasValue) return "";
                if (n.Value.IsHome)
                    return _home != null ? (_home.Description ?? "") : "";
                var region = FindRegion(n.Value.Id);
                return region != null ? (region.Description ?? "") : "";
            }
        }

        /// <summary>True when the selection is a region (the Travel CTA slot renders).
        /// Home shows no CTA — the player is already here.</summary>
        public bool ShowTravel
        {
            get
            {
                var n = FindNode(SelectedId);
                return n.HasValue && !n.Value.IsHome;
            }
        }

        /// <summary>WO-826 stub: travel is DISABLED until the WO-827 discovery/travel ledger.</summary>
        public bool TravelEnabled => false;

        /// <summary>Disabled-CTA label — the word carries the state (colorblind law).</summary>
        public string TravelLabel => "Travel - coming with discovery";

        // ── Commands ──────────────────────────────────────────────────────────

        /// <summary>Select a node by id (no-op on unknown/no-change). Raises Changed.</summary>
        public void Select(string id)
        {
            if (string.IsNullOrEmpty(id) || id == SelectedId) return;
            if (!FindNode(id).HasValue) return;
            SelectedId = id;
            FlowTrace.Step("RealmMap", "select '" + id + "' -> state " +
                (FindNode(id).HasValue ? FindNode(id).Value.StateLabel : "?"));
            Raise();
        }

        // ── Projection ────────────────────────────────────────────────────────

        private void Rebuild()
        {
            _nodes.Clear();

            if (_home != null)
            {
                float hx = _home.MapPoint != null ? _home.MapPoint.X : 50f;
                float hy = _home.MapPoint != null ? _home.MapPoint.Y : 50f;
                _nodes.Add(new NodeRow(_home.Id,
                    string.IsNullOrEmpty(_home.Title) ? "Elarion" : _home.Title,
                    hx, hy, NodeState.Home, "Home", isHome: true));
            }

            foreach (var r in _regions)
            {
                if (r == null || string.IsNullOrEmpty(r.Id)) continue;
                var state = StateFor(r);
                float x = r.MapPoint != null ? r.MapPoint.X : 50f;
                float y = r.MapPoint != null ? r.MapPoint.Y : 50f;
                _nodes.Add(new NodeRow(r.Id, r.Title ?? r.Id, x, y,
                    state, StateWord(state), isHome: false));
            }
        }

        private NodeState StateFor(RealmRegionDef region)
        {
            if (_source != null && _source.IsCleared(region.Id)) return NodeState.Cleared;
            if (_source != null && _source.IsDiscovered(region.Id)) return NodeState.Discovered;
            if (GateSatisfied(region.Gate)) return NodeState.Discovered;
            return NodeState.Locked;
        }

        private bool GateSatisfied(RealmRegionGate gate)
        {
            if (gate == null || _source == null) return false;
            switch (gate.Kind)
            {
                case RealmRegionGate.KindBestWave:
                    return _source.BestWave >= gate.Value;
                case RealmRegionGate.KindRegionCleared:
                    return !string.IsNullOrEmpty(gate.RegionId) && _source.IsCleared(gate.RegionId);
                default:
                    FlowTrace.Warn("RealmMap", "unknown gate kind '" + (gate.Kind ?? "<null>") + "' — treated as locked");
                    return false;
            }
        }

        private string GateReason(RealmRegionGate gate)
        {
            if (gate == null) return "Unknown.";
            switch (gate.Kind)
            {
                case RealmRegionGate.KindBestWave:
                    int best = _source != null ? _source.BestWave : 0;
                    return "Hold the village to wave " + gate.Value + " (best so far: " + best + ").";
                case RealmRegionGate.KindRegionCleared:
                    return "Clear " + RealmMapCatalog.TitleFor(gate.RegionId) + " first.";
                default:
                    return "Sealed by forces unknown.";
            }
        }

        private static string StateWord(NodeState s)
        {
            switch (s)
            {
                case NodeState.Home:       return "Home";
                case NodeState.Cleared:    return "Cleared";
                case NodeState.Discovered: return "Discovered";
                default:                   return "Locked";
            }
        }

        private NodeRow? FindNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var n in _nodes)
                if (n.Id == id) return n;
            return null;
        }

        private RealmRegionDef FindRegion(string id)
        {
            foreach (var r in _regions)
                if (r != null && r.Id == id) return r;
            return null;
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }

        // ── Real seam: GameStateService-backed progress (SOLE live resolution site). ──
        private sealed class StateSource : ISource
        {
            public int BestWave
            {
                get
                {
                    var svc = GameStateService.Instance;
                    return svc != null && svc.State != null ? svc.State.BestWave : 0;
                }
            }

            public bool IsDiscovered(string regionId)
            {
                var ledger = Ledger();
                return ledger != null && ledger.Discovered != null &&
                       !string.IsNullOrEmpty(regionId) &&
                       ledger.Discovered.TryGetValue(regionId, out var d) && d;
            }

            public bool IsCleared(string regionId)
            {
                var ledger = Ledger();
                return ledger != null && ledger.Cleared != null &&
                       !string.IsNullOrEmpty(regionId) &&
                       ledger.Cleared.TryGetValue(regionId, out var c) && c;
            }

            private static RegionProgress Ledger()
            {
                var svc = GameStateService.Instance;
                return svc != null && svc.State != null ? svc.State.Regions : null;
            }
        }
    }
}
