// =============================================================================
// BuildPaletteVM — the PURE ViewModel behind the Build Mode palette (MVVM Silo C).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Strict-MVVM migration (UI_MVVM_MIGRATION_PLAN.md §1, Silo C): owns EVERY
// game-state read BuildPaletteUI used to do inline — the CatalogRegistry query,
// the BuildCategoryRegistry verb recipe (Configure), the freebie/affordability
// projection (per card, via StructureCardVM) and the live wallet subscription
// (EconomyService.OnChanged + GameState.ResourcesChanged). The View becomes a
// dumb skin: it renders <see cref="Cards"/> + <see cref="Crystals"/> and
// re-renders on <see cref="Changed"/>; it names no EconomyService/GameStateService/
// CatalogRegistry symbol.
//
// PURE C# + Village seams only; no GameObject/Image/RectTransform. The providers
// are injectable so §2c tests drive it over a fake catalog + fake IEconomy.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village
{
    /// <summary>
    /// ViewModel for the Build Mode structure palette. Projects the configured build verb's
    /// catalog entries into <see cref="StructureCardVM"/> cards (cost/affordability/targeting)
    /// and raises <see cref="Changed"/> on any wallet or verb change.
    /// </summary>
    public sealed class BuildPaletteVM : IPanelViewModel, IDisposable
    {
        private readonly IEconomy _economy;
        private readonly Func<BuildType, BuildCategory> _categoryProvider;
        private readonly Func<CatalogType[], IReadOnlyList<CatalogEntry>> _query;
        private readonly Func<CatalogEntry, bool> _freebieProvider;
        private readonly Func<int> _registryCount;
        private readonly Action _onClose;

        private readonly Action<ResourceSnapshot> _ecoHandler;
        private readonly UnityEngine.Events.UnityAction _stateHandler;
        private bool _disposed;

        private BuildType _activeType;
        private CatalogType[] _types = { CatalogType.Tower, CatalogType.Gate };
        private HashSet<string> _lockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<StructureCardVM> _cards = new List<StructureCardVM>();

        /// <summary>Resolves the live catalog/economy/state handles itself — the ONLY resolution
        /// site (audit §3.1). The View never names these services.</summary>
        public static BuildPaletteVM CreateDefault(BuildType initialType, Action onClose)
        {
            var vm = new BuildPaletteVM(
                EconomyService.Instance,
                BuildCategoryRegistry.Get,
                AggregateOfType,
                BuildModeController.FreeBuildAvailable,
                () => CatalogRegistry.Count,
                initialType,
                onClose);

            // Subscribe the live wallet feeds (both — TrySpend fires OnChanged but not
            // GameState.ResourcesChanged for a Wood/Iron-only spend, and a crystal grant
            // fires ResourcesChanged; the palette needs both to stay affordability-live).
            var gs = GameStateService.Instance;
            if (gs != null) gs.ResourcesChanged.AddListener(vm._stateHandler);
            if (vm._economy != null) vm._economy.OnChanged += vm._ecoHandler;
            return vm;
        }

        public BuildPaletteVM(
            IEconomy economy,
            Func<BuildType, BuildCategory> categoryProvider,
            Func<CatalogType[], IReadOnlyList<CatalogEntry>> query,
            Func<CatalogEntry, bool> freebieProvider,
            Func<int> registryCount,
            BuildType initialType,
            Action onClose)
        {
            _economy = economy;
            _categoryProvider = categoryProvider;
            _query = query;
            _freebieProvider = freebieProvider ?? (_ => false);
            _registryCount = registryCount;
            _onClose = onClose;

            _ecoHandler = _ => Raise();
            _stateHandler = Raise;

            Configure(initialType);
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;
        public string Title => "Build";
        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var gs = GameStateService.Instance;
            if (gs != null && _stateHandler != null) gs.ResourcesChanged.RemoveListener(_stateHandler);
            if (_economy != null && _ecoHandler != null) _economy.OnChanged -= _ecoHandler;
            Changed = null;
        }

        // ── Read-only data the View renders ───────────────────────────────────

        /// <summary>The projected cards for the active build verb. Never null.</summary>
        public IReadOnlyList<StructureCardVM> Cards => _cards;

        /// <summary>Live crystal balance (drives the header "Crystals: N" read-out).</summary>
        public int Crystals => _economy?.Crystals ?? 0;

        /// <summary>The active build verb (Town / Defense / Walls).</summary>
        public BuildType ActiveType => _activeType;

        /// <summary>Total catalog rows registered (diagnostic — the View logs this).</summary>
        public int RegistryCount => _registryCount != null ? _registryCount() : _cards.Count;

        // ── Commands ──────────────────────────────────────────────────────────

        /// <summary>Point the palette at a build verb: sources the catalog types + unlock-gated
        /// ids from the verb recipe, rebuilds the cards, and raises <see cref="Changed"/>.</summary>
        public void Configure(BuildType type)
        {
            _activeType = type;
            var cat = _categoryProvider != null ? _categoryProvider(type) : null;
            if (cat != null)
            {
                if (cat.Types != null && cat.Types.Length > 0) _types = cat.Types;
                _lockedIds = cat.LockedIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            Rebuild();
            Raise();
        }

        /// <summary>Force a re-projection of the cards (called by the View on show).</summary>
        public void Refresh() { Rebuild(); Raise(); }

        private void Rebuild()
        {
            _cards.Clear();
            var entries = _query != null ? _query(_types) : null;
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (e == null) continue;
                    if (e.id != null && _lockedIds.Contains(e.id)) continue;   // unlock-gated
                    _cards.Add(new StructureCardVM(e, _economy, _freebieProvider(e)));
                }
            }
            FlowTrace.Step("BuildPalette",
                $"catalog-count: registry={RegistryCount} cards={_cards.Count} (types={_types.Length})");
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }

        // ── Default catalog query (CreateDefault) ─────────────────────────────

        /// <summary>Aggregate every registered entry across the given catalog types (the
        /// SAME set StructureFactory builds from), in type order.</summary>
        private static IReadOnlyList<CatalogEntry> AggregateOfType(CatalogType[] types)
        {
            var all = new List<CatalogEntry>();
            if (types == null) return all;
            foreach (var type in types)
            {
                var entries = CatalogRegistry.OfType(type);
                if (entries == null) continue;
                foreach (var e in entries)
                    if (e != null) all.Add(e);
            }
            return all;
        }
    }
}
