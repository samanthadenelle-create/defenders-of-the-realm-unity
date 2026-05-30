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
                Debug.LogWarning("[CatalogRegistry] skipped a null / id-less entry.");
                return;
            }
            _byId[entry.id] = entry;
            if (!_byType.TryGetValue(entry.type, out var list))
            {
                list = new List<CatalogEntry>();
                _byType[entry.type] = list;
            }
            if (!list.Contains(entry)) list.Add(entry);
        }

        /// <summary>Look up a single entry by id; null if absent.</summary>
        public static CatalogEntry Get(string id) =>
            (id != null && _byId.TryGetValue(id, out var e)) ? e : null;

        /// <summary>All entries of a type (palette tab). Empty if none.</summary>
        public static IReadOnlyList<CatalogEntry> OfType(CatalogType type) =>
            _byType.TryGetValue(type, out var list)
                ? (IReadOnlyList<CatalogEntry>)list
                : System.Array.Empty<CatalogEntry>();

        public static int Count => _byId.Count;

        /// <summary>Wipe the registry (call before re-registering on startup / domain reload).</summary>
        public static void Clear()
        {
            _byId.Clear();
            _byType.Clear();
        }
    }
}
