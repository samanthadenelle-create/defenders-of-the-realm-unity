// =============================================================================
// HovlVfxCatalog — ScriptableObject mapping a STRING KEY to a Hovl Studio prefab
// + pool/override config. WO-VFX-002.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Create via:  Assets → Create → Defenders / Hovl VFX Catalog
// Loaded at runtime from Resources/VFX/HovlVfxCatalog by VFXManager (see
// VFXManager.Hovl.cs → EnsureHovlCatalog). The Hovl prefabs are NOT under
// Resources/, so each row holds a SERIALIZED prefab reference authored in-editor;
// only this .asset lives in Resources/ (build-size guard — no whole pack dumped).
//
// AUTHORING (two ways):
//   • One-click: run  Defenders/VFX/Generate Hovl VFX Catalog
//     (DeNelle.Editor.HovlVfxCatalogGenerator) — authors the .asset from the
//     curated key→path table (mirrors VFXCatalogGenerator).
//   • By hand: add a row, type the Key, drag the Hovl prefab into Prefab, set
//     PoolSize / DefaultScale / DefaultLifetime / Recolorable / IsLoop.
//
// The 8–10 shortlist keys + their EXACT Hovl paths (from Docs/VFX/
// HovlStudio_Inventory.md §5) are documented in VFXManager.Hovl.cs and the
// generator's table. Any key not in the catalog simply no-ops (logged, throttled).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    [CreateAssetMenu(
        menuName = "Defenders/Hovl VFX Catalog",
        fileName = "HovlVfxCatalog",
        order    = 56)]
    public sealed class HovlVfxCatalog : ScriptableObject
    {
        // ── Row ───────────────────────────────────────────────────────────────

        [System.Serializable]
        public struct Row
        {
            [Tooltip("String key callers pass to VFXManager.PlayKey(\"...\").\n" +
                     "e.g. Fireball_Projectile, Arcane_Impact, Collector_Full, Raid_Explosion.")]
            public string Key;

            [Tooltip("The Hovl Studio prefab to pool + play. Null = the key no-ops (logged).")]
            public GameObject Prefab;

            [Tooltip("How many instances to pre-warm in Awake (0 = lazy instantiate on first use).")]
            [Min(0)] public int PoolSize;

            [Tooltip("Uniform scale applied when a caller passes scale <= 0. 0 or 1 = native size.")]
            public float DefaultScale;

            [Tooltip("Lifetime (s) before a ONESHOT auto-returns when a caller passes lifetime <= 0. " +
                     "0 = auto-detect from the particle systems. Ignored for loops.")]
            [Min(0f)] public float DefaultLifetime;

            [Tooltip("True if this effect can be HDR-recoloured at runtime (StartColor tint). " +
                     "Hovl HS_Blend_CG effects are recolourable — one base serves many elements.")]
            public bool Recolorable;

            [Tooltip("True for looping auras/trails/shields — PlayKey returns a VFXHandle so the " +
                     "caller can Stop() it. False = oneshot that auto-returns to the pool.")]
            public bool IsLoop;
        }

        // ── Inspector array ─────────────────────────────────────────────────────

        [Tooltip("One row per key. Rows with a null Prefab no-op at call time.")]
        public Row[] Rows = System.Array.Empty<Row>();

        // ── Runtime lookup (built on first use) ─────────────────────────────────

        private Dictionary<string, Row> _map;

        /// <summary>Build / rebuild the fast key→row dictionary from the Rows array.</summary>
        public void BuildLookup()
        {
            _map = new Dictionary<string, Row>(Rows.Length);
            foreach (var r in Rows)
            {
                if (string.IsNullOrEmpty(r.Key)) continue;
                _map[r.Key] = r;   // last row wins on duplicate keys
            }
        }

        /// <summary>
        /// Try to get the row for a given key. Returns false when the key is not in the
        /// catalog (caller no-ops).
        /// </summary>
        public bool TryGet(string key, out Row row)
        {
            if (_map == null) BuildLookup();
            return _map.TryGetValue(key, out row);
        }

        private void OnEnable() => BuildLookup();
        private void OnValidate() => BuildLookup();
    }
}
