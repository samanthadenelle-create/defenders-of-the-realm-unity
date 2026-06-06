// =============================================================================
// RotationCorrectionRegistry — per-prefab-type persistent yaw correction for
// Build Preview Modal (WO-282).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village
//
// Stores a "natural orientation" yaw (degrees) per CatalogEntry.id (the stable
// itemId used in PlacedStructureData). Seeded into the modal preview so the 3D
// object appears already rotated correctly on first open. On confirm the final
// value is (re)saved so future placements of the same type auto-orient.
//
// Persistence: lightweight JSON blob in PlayerPrefs (mobile-safe, no file IO
// permissions issues, survives app restarts, works in editor). Uses JsonUtility
// + tiny serializable containers — zero extra packages.
//
// The correction is the *additive yaw the player chose in the viewer* so that
// 0-drag in the modal (or accept of the seeded value) produces a structure that
// "sits naturally" on the grid (correct facing, flat, no import-axis surprises
// from Quaternius/KayKit/Polyperfect/etc packs).
//
// Apply is implicit: the modal seeds its _currentYaw from here, the confirmed
// value flows through BuildModeController -> PlacedStructureData.yawOffset,
// and BaseLayoutLoader + StructureFactory already apply (yawSteps*90 + yawOffset).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Static registry for per-itemId yaw corrections discovered by the player in
    /// the Build Preview Modal. Corrections are remembered permanently (per device)
    /// and used to pre-orient the 3D preview on subsequent uses of the same structure
    /// type.
    /// </summary>
    public static class RotationCorrectionRegistry
    {
        private const string PREFS_KEY = "DeNelle.RotationYawCorrections.v1";

        [Serializable]
        private class CorrectionEntry
        {
            public string id;
            public float yaw;
        }

        [Serializable]
        private class CorrectionMap
        {
            public List<CorrectionEntry> entries = new List<CorrectionEntry>();
        }

        private static Dictionary<string, float> s_map;
        private static bool s_loaded;

        private static void EnsureLoaded()
        {
            if (s_loaded) return;

            s_map = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            string json = PlayerPrefs.GetString(PREFS_KEY, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var map = JsonUtility.FromJson<CorrectionMap>(json);
                    if (map != null && map.entries != null)
                    {
                        foreach (var e in map.entries)
                        {
                            if (!string.IsNullOrEmpty(e.id))
                            {
                                s_map[e.id] = e.yaw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RotationCorrectionRegistry] Failed to load corrections JSON: {ex.Message}");
                }
            }

            s_loaded = true;
        }

        private static void Save()
        {
            if (s_map == null) return;

            var map = new CorrectionMap();
            foreach (var kv in s_map)
            {
                map.entries.Add(new CorrectionEntry { id = kv.Key, yaw = kv.Value });
            }

            string json = JsonUtility.ToJson(map);
            PlayerPrefs.SetString(PREFS_KEY, json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Returns the saved yaw offset (degrees) for the given catalog itemId, or 0
        /// if this type has never been oriented by the player.
        /// </summary>
        public static float GetYawOffset(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0f;
            EnsureLoaded();
            return s_map.TryGetValue(itemId, out float v) ? v : 0f;
        }

        /// <summary>
        /// Stores (and immediately persists) a yaw correction for the given itemId.
        /// The value is normalized to [0, 360).
        /// </summary>
        public static void SetAndSave(string itemId, float yawDegrees)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            EnsureLoaded();
            s_map[itemId] = Mathf.Repeat(yawDegrees, 360f);
            Save();
            Debug.Log($"[RotationCorrectionRegistry] Saved yaw offset {s_map[itemId]:F1}° for structure type '{itemId}'");
        }

        /// <summary>
        /// Debug / future tooling: read-only view of the current in-memory map.
        /// </summary>
        public static IReadOnlyDictionary<string, float> GetAll()
        {
            EnsureLoaded();
            return s_map;
        }

        /// <summary>
        /// Testing / reset helper. Clears the in-memory map and the persisted blob.
        /// </summary>
        public static void ClearAllForTesting()
        {
            s_map?.Clear();
            PlayerPrefs.DeleteKey(PREFS_KEY);
            PlayerPrefs.Save();
            s_loaded = true; // still "loaded" (empty)
        }
    }
}
