// =============================================================================
// CatalogEntry — one def in the catalog. visualPrefabPath = LOOK (a real
// polyperfect prefab path), repo = BEHAVIOR. A Composite is a pre-snapped set
// of cell placements. This is the unit the build palette lists and the
// dispatcher builds. Pure data (DeNelle.Core).
// =============================================================================
using UnityEngine;

namespace DeNelle.Core.Catalog
{
    /// <summary>A cell inside a Composite: which cell-entry, at what relative offset + rotation.</summary>
    [System.Serializable]
    public sealed class CellPlacement
    {
        public string  cellEntryId;
        public Vector3 offset;
        public float   yRotation;   // 90° steps

        public CellPlacement() { }
        public CellPlacement(string cellEntryId, Vector3 offset, float yRotation)
        {
            this.cellEntryId = cellEntryId;
            this.offset = offset;
            this.yRotation = yRotation;
        }
    }

    [System.Serializable]
    public sealed class CatalogEntry
    {
        public string      id;
        public string      displayName;
        public CatalogType type;
        public EntryKind   kind = EntryKind.Cell;

        /// <summary>LOOK — Resources/polyperfect-style prefab path. Resolved to a model at build time.</summary>
        public string      visualPrefabPath;

        /// <summary>BEHAVIOR — stats, nav, placement, behaviour id.</summary>
        public RepoProps   repo = new RepoProps();

        /// <summary>Composites only: the cell set to drop as a bundle. Null for cells.</summary>
        public CellPlacement[] composite = null;

        /// <summary>
        /// Orientation correction (CatalogOrientationBaker / the future Orientation
        /// Inspector). Auto-baked entries are ADVISORY (manual=false) and are NOT
        /// applied — a bounds heuristic can't tell an authored-flat-but-skinned-upright
        /// model (e.g. tower2: Y=0.37, Z=1.00) from a genuinely tipped one, so auto-
        /// rotating would tip good assets. Only HUMAN-verified (manual=true) corrections
        /// are applied by StructureFactory.
        /// </summary>
        public OrientationFix orientation = null;
    }

    /// <summary>A stored keep-vertical correction for a catalog entry's visual.</summary>
    [System.Serializable]
    public sealed class OrientationFix
    {
        public bool    corrected;
        public bool    manual;        // true = human-verified (Inspector) → applied; false = advisory
        public float[] euler;         // [x,y,z] degrees
        public float[] offset;        // [x,y,z] metres
        public float   scale = 1f;
        public string  note;

        public Vector3 Euler  => euler  != null && euler.Length  == 3 ? new Vector3(euler[0],  euler[1],  euler[2])  : Vector3.zero;
        public Vector3 Offset => offset != null && offset.Length == 3 ? new Vector3(offset[0], offset[1], offset[2]) : Vector3.zero;
    }
}
