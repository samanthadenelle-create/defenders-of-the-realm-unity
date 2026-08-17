// =============================================================================
// StructurePreviewSource — resolves the 3D MODEL a building/structure should show
// in the upgrade panel's preview band ("you should go to the modeled page to
// manage all towers", owner ruling).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// WHY IT IS ITS OWN FILE
//   The panel is a DUMB SKIN (UiMvvmConformanceRegression): it may not go catalog-
//   spelunking. It hands this resolver an id + a level and gets back a prefab it can
//   drop into a TowerPreviewCamera rig, or false — in which case the panel keeps its
//   existing 2D BuildingArt portrait. No new widget vocabulary either way.
//
// THE ID TRAP THIS HANDLES (verified against structures-catalog.json 2026-08-16)
//   The panel is opened with THREE different id shapes:
//     * a placed job key   "tower_ballista@4_7"  -> the item id always has a row.
//     * a placed catalog id "collector_farm"     -> has a row.
//     * a LADDER id        "farm"                -> HAS NO ROW AT ALL. Only
//       "collector_farm" exists; "farm" is the repo.collectorBuildingId that
//       BuildingUpgradeVM normalises to. "lumbermill" and "forge" DO have rows, so
//       the gap is not uniform and cannot be assumed away.
//   So a miss walks BACK across the same authored mapping CatalogRegistry.ResolveUpgradeId
//   walks forward (repo.collectorBuildingId == id), never a hardcoded translation table
//   (owner 2026-08-08 forbade inventing one). If nothing resolves we return FALSE and say
//   so in the trace — the band is never left as an empty black box.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>Resolves the preview prefab + upright correction for a building/structure id.</summary>
    public static class StructurePreviewSource
    {
        /// <summary>
        /// The catalog row that OWNS the visual for <paramref name="id"/>. Accepts a placed
        /// job key, a placed catalog id, or an upgrade-ladder id; returns null when no row
        /// anywhere claims it.
        /// </summary>
        public static CatalogEntry ResolveEntry(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            // A job key names its item id directly.
            if (PlacedUpgradeKey.TryParse(id, out string placedId, out _, out _)) id = placedId;

            var direct = CatalogRegistry.Get(id);
            if (direct != null) return direct;

            // A LADDER id with no row of its own: find the placed row whose AUTHORED
            // repo.collectorBuildingId points back at it (the reverse of ResolveUpgradeId).
            var all = CatalogRegistry.All();
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var e = all[i];
                    if (e == null || e.repo == null) continue;
                    if (string.Equals(e.repo.collectorBuildingId, id, System.StringComparison.OrdinalIgnoreCase))
                        return e;
                }
            }
            return null;
        }

        /// <summary>
        /// Try to resolve the model for <paramref name="id"/> at <paramref name="level"/>
        /// (the SAME per-level ladder placement uses, StructureFactory.VisualPathForLevel).
        /// Returns false — with a traced reason — when there is no row or no loadable prefab,
        /// which is the caller's cue to fall back to the 2D portrait.
        /// </summary>
        public static bool TryResolve(string id, int level, out GameObject prefab, out OrientationFix orientation)
        {
            prefab = null;
            orientation = null;

            var entry = ResolveEntry(id);
            if (entry == null)
            {
                FlowTrace.Throttle("UpgradeUI", "preview-no-entry-" + (id ?? "null"), 30f,
                    "preview: no catalog row resolves '" + (id ?? "<null>") + "' - falling back to the 2D portrait");
                return false;
            }

            string path = StructureFactory.VisualPathForLevel(entry, Mathf.Max(1, level));
            if (string.IsNullOrEmpty(path))
            {
                FlowTrace.Throttle("UpgradeUI", "preview-no-path-" + entry.id, 30f,
                    "preview: '" + entry.id + "' authors no visual path at level " + level
                    + " - falling back to the 2D portrait");
                return false;
            }

            // Addressables-first via StructureAssetLoader (2026-08-17); identical behaviour while
            // the art remains in Resources, and the precondition for moving it out of the build.
            prefab = DeNelle.Core.StructureAssetLoader.LoadStructurePrefab(path);
            if (prefab == null)
            {
                FlowTrace.Throttle("UpgradeUI", "preview-load-null-" + path, 30f,
                    "preview: Resources.Load('" + path + "') returned NULL for '" + entry.id
                    + "' - falling back to the 2D portrait");
                return false;
            }

            orientation = entry.orientation;
            FlowTrace.Step("UpgradeUI", "preview model resolved: id='" + (id ?? "") + "' -> row '"
                + entry.id + "' level " + level + " prefab '" + path + "'");
            return true;
        }
    }
}
