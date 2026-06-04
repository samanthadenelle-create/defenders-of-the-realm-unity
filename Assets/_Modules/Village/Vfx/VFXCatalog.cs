// =============================================================================
// VFXCatalog — ScriptableObject mapping every VFXType to a prefab + pool config.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Create via:  Assets → Create → Defenders / VFX Catalog
// Assign the resulting asset to VFXManager._catalog in the Inspector.
//
// WORKFLOW:
//   1. Add a row in the Entries array in the Inspector.
//   2. Set Type to the VFXType enum value.
//   3. Drag the Mirza Beig / Lana Studio / Spells Pack prefab into Prefab.
//   4. Set PoolSize (pre-warmed count) and IsLoop.
//   5. Set MinQuality: 0 = always play, 1 = skip on Low, 2 = skip on Low+Medium.
//
// If Prefab is null the VFXManager falls back to procedural (AbilityVfxKit).
// That keeps the game playable fresh-clone before any art assets are wired.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    [CreateAssetMenu(
        menuName  = "Defenders/VFX Catalog",
        fileName  = "VFXCatalog",
        order     = 55)]
    public sealed class VFXCatalog : ScriptableObject
    {
        // ── Entry ─────────────────────────────────────────────────────────────

        [System.Serializable]
        public struct Entry
        {
            [Tooltip("The VFXType enum value this row covers.")]
            public VFXType Type;

            [Tooltip("Prefab to pool and play. Null = procedural fallback via AbilityVfxKit.")]
            public GameObject Prefab;

            [Tooltip("How many instances to pre-warm in Awake (0 = lazy instantiate on first use).")]
            [Min(0)] public int PoolSize;

            [Tooltip("True for looping auras and environment effects that need a VFXHandle to stop.")]
            public bool IsLoop;

            [Tooltip("Minimum quality level required to play this effect.\n" +
                     "0 = always  1 = skip on Low  2 = High only")]
            [Range(0, 2)] public int MinQuality;

            [Tooltip("Override lifetime in seconds before the pool auto-reclaims a oneshot. " +
                     "0 = auto-detect from particle system duration + startLifetime.")]
            [Min(0f)] public float LifetimeOverride;
        }

        // ── Inspector array ───────────────────────────────────────────────────

        [Tooltip("One row per VFXType. Rows with null Prefab use the procedural fallback.")]
        public Entry[] Entries = System.Array.Empty<Entry>();

        // ── Runtime lookup (built on first use) ───────────────────────────────

        private Dictionary<VFXType, Entry> _map;

        /// <summary>Build / rebuild the fast-lookup dictionary from the Entries array.</summary>
        public void BuildLookup()
        {
            _map = new Dictionary<VFXType, Entry>(Entries.Length);
            foreach (var e in Entries)
            {
                if (e.Type == VFXType.None) continue;
                _map[e.Type] = e;   // last entry wins on duplicate types
            }
        }

        /// <summary>
        /// Try to get the catalog entry for a given type.
        /// Returns false if the type is not in the catalog (use procedural fallback).
        /// </summary>
        public bool TryGet(VFXType type, out Entry entry)
        {
            if (_map == null) BuildLookup();
            return _map.TryGetValue(type, out entry);
        }

        private void OnEnable() => BuildLookup();
        private void OnValidate() => BuildLookup();
    }
}
