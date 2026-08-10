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
    /// WO-1010 D15 — the owner's BINARY display grouping (ruling 2026-08-09, verbatim:
    /// "everything is either a structure (building) or a Defense (tower)").
    ///
    /// THIS IS PRESENTATION, NOT TAXONOMY. It renames no key and adds no row to
    /// build-categories.json or any catalog file: the two groups are COMPOSED at read time
    /// from the verb table that already exists. <see cref="Defenses"/> is the
    /// <c>CatalogType.Tower</c> rows; <see cref="Structures"/> is every OTHER placeable
    /// catalog type the verb table declares (Resource / Collector / Support / Wall / Gate).
    /// Derived from catalog TYPE, never from an id list — an id list would rot the first time
    /// a catalog row is added, a type rule will not.
    /// </summary>
    public enum BuildGroup
    {
        /// <summary>Buildings — every placeable that is NOT a tower (economy + castle fabric).</summary>
        Structures,
        /// <summary>Towers — the Tower catalog type.</summary>
        Defenses
    }

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
        private BuildGroup? _activeGroup;
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

        /// <summary>
        /// The active WO-1010 D15 display group, or null when the palette was pointed at a raw
        /// build verb by <see cref="Configure"/>. Lets the View underline the right quick-tab
        /// without re-deriving it.
        /// </summary>
        public BuildGroup? ActiveGroup => _activeGroup;

        /// <summary>Total catalog rows registered (diagnostic — the View logs this).</summary>
        public int RegistryCount => _registryCount != null ? _registryCount() : _cards.Count;

        // ── Commands ──────────────────────────────────────────────────────────

        /// <summary>Point the palette at a build verb: sources the catalog types + unlock-gated
        /// ids from the verb recipe, rebuilds the cards, and raises <see cref="Changed"/>.</summary>
        public void Configure(BuildType type)
        {
            _activeType = type;
            _activeGroup = null;   // a raw verb was selected; no D15 group is active
            var cat = _categoryProvider != null ? _categoryProvider(type) : null;
            if (cat != null)
            {
                if (cat.Types != null && cat.Types.Length > 0) _types = cat.Types;
                _lockedIds = cat.LockedIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            Rebuild();
            Raise();
        }

        /// <summary>
        /// WO-1010 D15 — point the palette at one of the owner's TWO display groups
        /// (<see cref="BuildGroup"/>), rebuild the cards and raise <see cref="Changed"/>.
        ///
        /// NO DATA CHANGE. The group's catalog types are COMPOSED from the verb rows the
        /// registry already serves: every catalog type any verb declares is sorted by the one
        /// rule the owner gave — <c>Tower</c> is a Defense, everything else placeable is a
        /// Structure. Because the split is by TYPE, a new catalog row lands in the right group
        /// automatically; nothing here needs editing when the catalog grows.
        ///
        /// GATING IS PRESERVED, NEVER LOOSENED: the locked-id filter is the UNION of every
        /// verb's <c>lockedIds</c>, so a row that is gated under ANY verb stays filtered out of
        /// whichever group it would land in. Merging groups must never be a way to surface a
        /// structure the unlock has not shipped.
        /// </summary>
        public void ConfigureGroup(BuildGroup group)
        {
            _activeGroup = group;
            // Keep ActiveType coherent so the View's underline/highlight still has a verb to
            // point at (Defenses -> the Defense verb, Structures -> the Town verb).
            _activeType = group == BuildGroup.Defenses ? BuildType.Defense : BuildType.Town;

            var types = new List<CatalogType>();
            var locked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BuildType verb in Enum.GetValues(typeof(BuildType)))
            {
                var cat = _categoryProvider != null ? _categoryProvider(verb) : null;
                if (cat == null) continue;

                if (cat.LockedIds != null)
                    foreach (var id in cat.LockedIds)
                        if (!string.IsNullOrEmpty(id)) locked.Add(id);

                if (cat.Types == null) continue;
                foreach (var t in cat.Types)
                {
                    bool isDefense = t == CatalogType.Tower;
                    if (isDefense != (group == BuildGroup.Defenses)) continue;
                    // Walls stay behind their existing feature flag. The owner's binary ruling
                    // puts the castle fabric in Structures, but ff.wallstab is OFF today and
                    // merging two tabs into one grouping must never be the thing that ships a
                    // gated surface: a display regrouping does not get to un-gate content. Flip
                    // the flag and the wall rows appear here with no further code change.
                    if (t == CatalogType.Wall && !DeNelle.Core.FeatureFlags.WallsTab) continue;
                    if (!types.Contains(t)) types.Add(t);
                }
            }

            // A registry that served nothing must not blank the palette — keep the previous
            // type set and say so, rather than rendering an empty shop with no explanation.
            if (types.Count > 0) _types = types.ToArray();
            else FlowTrace.Warn("BuildPalette",
                $"ConfigureGroup({group}): the verb table yielded NO catalog types for this group -- " +
                "keeping the previous type set so the palette does not blank.");

            _lockedIds = locked;
            FlowTrace.Step("BuildPalette",
                $"group-configure: group={group} types={string.Join(",", types)} lockedIds={locked.Count}");
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
