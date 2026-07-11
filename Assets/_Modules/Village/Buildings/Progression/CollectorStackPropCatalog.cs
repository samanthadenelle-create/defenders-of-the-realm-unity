// =============================================================================
// CollectorStackPropCatalog — per-resource stack-prop prefab map (WO-665a).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// A ScriptableObject that maps a HarvestResource to the diegetic prop a collector
// stacks as it fills (Wood = log, Iron = ingot/ore, Food = grain sack, Crystals =
// shard). Mirrors the VFXCatalog load-from-Resources pattern (VFXManager.
// EnsureCatalog): CollectorStackView.TryLoad() does Resources.Load on
// "Collectors/CollectorStackPropCatalog"; if the asset is absent OR a resource row
// has no prefab, the view falls back to a NodeFillIndicator-style abstract fill bar
// so a fresh clone (pack prefabs are gitignored) is never blank.
//
// Create via:  Assets -> Create -> Defenders / Collector Stack Prop Catalog
// Place the resulting asset at:  Assets/Resources/Collectors/CollectorStackPropCatalog
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.Buildings.Progression
{
    [CreateAssetMenu(
        menuName = "Defenders/Collector Stack Prop Catalog",
        fileName = "CollectorStackPropCatalog",
        order    = 56)]
    public sealed class CollectorStackPropCatalog : ScriptableObject
    {
        /// <summary>Resource-standard Resources path the view loads from (no extension).</summary>
        public const string ResourcesPath = "Collectors/CollectorStackPropCatalog";

        [System.Serializable]
        public struct Entry
        {
            [Tooltip("Which resource this prop represents.")]
            public HarvestResource Resource;

            [Tooltip("Prop instanced once per fill step (log / ingot / sack / shard). " +
                     "Null = this resource uses the abstract fill-bar fallback.")]
            public GameObject Prop;

            [Tooltip("Uniform scale applied to each instanced prop.")]
            public float PropScale;

            [Tooltip("World-space size of one 'slot' the props are stacked into (x,y,z). " +
                     "Props tile bottom-up within this footprint as steps fill.")]
            public Vector3 SlotSize;
        }

        [Tooltip("One row per HarvestResource. Rows with a null Prop fall back to the bar.")]
        public Entry[] Entries = System.Array.Empty<Entry>();

        /// <summary>Find the prop entry for a resource. Returns false when unmapped/prefab-less.</summary>
        public bool TryGet(HarvestResource resource, out Entry entry)
        {
            entry = default;
            if (Entries == null) return false;
            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Resource == resource)
                {
                    entry = Entries[i];
                    return entry.Prop != null;
                }
            }
            return false;
        }
    }
}
