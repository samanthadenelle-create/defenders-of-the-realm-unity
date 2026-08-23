// =============================================================================
// StructureRoles — resolve ANY role to the row that claims it. Indexed, not coded.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Catalog   (WO-1161)
//
// Owner, 2026-08-23: "we want building.newtype" · "the idea is staying fluid" ·
// "if we add a building we do not want to have to manually code it" · "you could
// even point to a db table to settle them".
//
// So this is a TABLE YOU INDEX, not a switch you extend:
//
//     StructureRoles.By[StructureRole.Armorer].DisplayName   // named, compile-checked
//     StructureRoles.By["newtype"].DisplayName               // brand new role, ZERO code
//
// Both lines are the same call. The second one is the whole point: author a row in
// structures-catalog.json with "role": "newtype" and it resolves immediately — no
// enum member, no case label, no registration, nothing recompiled. Adding a
// building is a DATA edit.
//
// ⛔ THERE IS NO ROLE -> ID MAP IN THIS FILE, AND THERE MUST NEVER BE ONE.
// The TABLE settles it — each catalog row declares its own `role` and this class
// only INDEXES what the data claims. The moment a `case "armorer": return "armorer";`
// appears here, one fact is written in two places and they begin to drift. That
// exact shape has already produced the stale WO-number block (CLAUDE.md sec.2), the
// retired assembly table (sec.5), the hardcoded repo root (sec.0), the drifted R2
// push (sec.16) and WO-1137's 3-of-28 fallback catalog. To move a role onto a
// different building, edit the CATALOG. Never this file.
//
// ⛔ AND IT REFUSES AMBIGUITY LOUDLY. Two rows claiming one role would otherwise
// resolve by catalog order — silently, and differently after the next regenerate.
// The index keeps the FIRST and reports the collision through FlowTrace.Fail so it
// lands in the flight recorder instead of becoming a coin flip nobody can see.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Catalog
{
    /// <summary>
    /// One role's answer. A struct so an unresolved role costs no allocation and can
    /// never be a null-reference at a call site — ask <see cref="Exists"/> instead.
    /// </summary>
    public readonly struct RoleView
    {
        /// <summary>The catalog row, or null when nothing claims this role.</summary>
        public readonly CatalogEntry Entry;
        /// <summary>The role string this view was resolved for.</summary>
        public readonly string Role;

        public RoleView(string role, CatalogEntry entry) { Role = role; Entry = entry; }

        /// <summary>True when some catalog row claims this role.</summary>
        public bool Exists => Entry != null;

        /// <summary>The opaque SAVE-KEY id, or null. Never rendered to a player.</summary>
        public string Id => Entry != null ? Entry.id : null;

        /// <summary>
        /// The player-facing word, straight off the row that claims the role — the
        /// `building.role.displayname` the owner asked for.
        /// <para>⛔ Null when unresolved, and callers must NOT substitute a literal.
        /// A wrong-but-present word is precisely what produced "Iron - NEEDS: Forge"
        /// against a Forge the player had already built. Say nothing rather than say
        /// something false.</para>
        /// </summary>
        public string DisplayName =>
            Entry != null && !string.IsNullOrEmpty(Entry.displayName) ? Entry.displayName : null;
    }

    /// <summary>Indexable role table. Obtained via <see cref="StructureRoles.By"/>.</summary>
    public sealed class RoleTable
    {
        internal RoleTable() { }

        /// <summary>`StructureRoles.By[role]` — the one lookup, named or ad-hoc.</summary>
        public RoleView this[string role] => StructureRoles.Resolve(role);
    }

    /// <summary>
    /// The one place code asks "which building is the Armorer?" — answered by the
    /// catalog table, never by a literal.
    /// </summary>
    public static class StructureRoles
    {
        /// <summary>The indexable table: <c>StructureRoles.By["anything"].DisplayName</c>.</summary>
        public static readonly RoleTable By = new RoleTable();

        // Built lazily and invalidated with the catalog, so a hot-reload or a
        // late-loading catalog can never leave a stale index behind.
        private static Dictionary<string, CatalogEntry> _byRole;
        private static int _builtFromCount = -1;

        /// <summary>Drop the index. Call when the catalog is reloaded.</summary>
        public static void Invalidate()
        {
            _byRole = null;
            _builtFromCount = -1;
        }

        /// <summary>Resolve a role to its row view. Case-insensitive; unknown roles are legal (they just do not exist yet).</summary>
        public static RoleView Resolve(string role)
        {
            if (string.IsNullOrEmpty(role)) return new RoleView(role, null);
            var map = Index();
            if (map != null && map.TryGetValue(role, out var e)) return new RoleView(role, e);
            return new RoleView(role, null);
        }

        /// <summary>Convenience: the word for a role, or null.</summary>
        public static string DisplayName(string role) => Resolve(role).DisplayName;

        /// <summary>Convenience: the save-key id for a role, or null.</summary>
        public static string Id(string role) => Resolve(role).Id;

        /// <summary>True when some row claims this role.</summary>
        public static bool IsAuthored(string role) => Resolve(role).Exists;

        /// <summary>The role a given catalog id claims, or <see cref="StructureRole.None"/>.</summary>
        public static string RoleOf(string catalogId)
        {
            if (string.IsNullOrEmpty(catalogId)) return StructureRole.None;
            var e = CatalogRegistry.Get(catalogId);
            return e != null && !string.IsNullOrEmpty(e.role) ? e.role : StructureRole.None;
        }

        /// <summary>Every role the catalog currently authors. Diagnostics + the oracle read this.</summary>
        public static IEnumerable<string> AuthoredRoles()
        {
            var map = Index();
            return map != null ? (IEnumerable<string>)map.Keys : Array.Empty<string>();
        }

        // ---------------------------------------------------------------------

        private static Dictionary<string, CatalogEntry> Index()
        {
            var all = CatalogRegistry.All();
            if (all == null) return null;

            // Cheap staleness check: the row count changing means the catalog was
            // reloaded or extended. Guarded so a broken read degrades to "no roles
            // authored" (every caller already handles that) instead of throwing on a UI path.
            int count = 0;
            foreach (var _ in all) count++;
            if (_byRole != null && _builtFromCount == count) return _byRole;

            // Explicit generic + explicit fallback type: C# 9 (this project's language
            // version) cannot infer a delegate type when the fallback is a bare null.
            var built = Guard.Try<Dictionary<string, CatalogEntry>>("Catalog", "index structure roles", () =>
            {
                var map = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in all)
                {
                    if (e == null || string.IsNullOrEmpty(e.role)) continue;
                    if (map.TryGetValue(e.role, out var first))
                    {
                        // Two rows, one role. Never silently pick — say which two.
                        FlowTrace.Fail("Catalog",
                            $"structure role COLLISION: '{e.role}' is claimed by BOTH '{first.id}' " +
                            $"and '{e.id}'. Keeping '{first.id}'. A role must identify exactly ONE " +
                            "row - fix the catalog, not this index.");
                        continue;
                    }
                    map[e.role] = e;
                }
                return map;
            }, fallback: (Dictionary<string, CatalogEntry>)null);

            if (built == null) return null;
            _byRole = built;
            _builtFromCount = count;
            return _byRole;
        }
    }
}
