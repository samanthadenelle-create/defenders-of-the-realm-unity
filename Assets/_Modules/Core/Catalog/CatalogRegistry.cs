// =============================================================================
// CatalogRegistry — the static registry of catalog defs. Content (Village's
// DefensiveCatalog, etc.) registers entries at startup; build-mode + factories
// look them up by id or by type. Pure data (DeNelle.Core). Clear() guards
// against stale entries surviving a domain reload.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.Catalog
{
    public static class CatalogRegistry
    {
        private static readonly Dictionary<string, CatalogEntry> _byId =
            new Dictionary<string, CatalogEntry>();
        private static readonly Dictionary<CatalogType, List<CatalogEntry>> _byType =
            new Dictionary<CatalogType, List<CatalogEntry>>();

        /// <summary>Register (or replace) an entry. Idless/null entries are skipped with a warning.</summary>
        public static void Register(CatalogEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Catalog", "skipped a null / id-less entry (never registered).");
                Debug.LogWarning("[CatalogRegistry] skipped a null / id-less entry.");
                return;
            }
            bool replaced = _byId.ContainsKey(entry.id);
            _byId[entry.id] = entry;
            if (!_byType.TryGetValue(entry.type, out var list))
            {
                list = new List<CatalogEntry>();
                _byType[entry.type] = list;
            }
            if (!list.Contains(entry)) list.Add(entry);
            if (replaced)
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Catalog", $"REPLACED existing entry id='{entry.id}' (type={entry.type}).");
            else
                DeNelle.Core.Diagnostics.FlowTrace.Step("Catalog", $"registered id='{entry.id}' (type={entry.type}); total={_byId.Count}.");
        }

        /// <summary>Look up a single entry by id; null if absent.</summary>
        public static CatalogEntry Get(string id) =>
            (id != null && _byId.TryGetValue(id, out var e)) ? e : null;

        /// <summary>
        /// Resolve a placed structure's catalog id to the id its UPGRADE ladder is keyed on.
        /// A resource COLLECTOR is registered under its catalog id (e.g. "collector_lumbermill")
        /// but its tier/level data lives under the bare <c>collectorBuildingId</c> ("lumbermill",
        /// per building-tiers.json + ResourceBuildingProgression). Every non-collector id (and any
        /// unknown id) returns UNCHANGED. Mirrors the existing
        /// <c>repo.collectorBuildingId ?? entry.id</c> resolution in StructureFactory /
        /// WallRepairController so the upgrade path agrees with placement.
        /// </summary>
        public static string ResolveUpgradeId(string id)
        {
            var e = Get(id);
            var cbid = (e != null && e.repo != null) ? e.repo.collectorBuildingId : null;
            return !string.IsNullOrEmpty(cbid) ? cbid : id;
        }

        /// <summary>All entries of a type (palette tab). Empty if none.</summary>
        public static IReadOnlyList<CatalogEntry> OfType(CatalogType type) =>
            _byType.TryGetValue(type, out var list)
                ? (IReadOnlyList<CatalogEntry>)list
                : System.Array.Empty<CatalogEntry>();

        public static int Count => _byId.Count;

        /// <summary>Every registered entry (snapshot copy — safe to enumerate while registering).
        /// Added for the StructureSingleton EnforceAll sweep (owner only-ever-one ruling) and
        /// any future whole-catalog audit.</summary>
        public static IReadOnlyList<CatalogEntry> All()
        {
            var list = new List<CatalogEntry>(_byId.Count);
            foreach (var kv in _byId) list.Add(kv.Value);
            return list;
        }

        /// <summary>Wipe the registry (call before re-registering on startup / domain reload).</summary>
        public static void Clear()
        {
            _byId.Clear();
            _byType.Clear();
        }
    }
}
