// =============================================================================
// DataInjector — ONE generic wrapper for the "read a canonical JSON table and
// deserialize it to T" pattern that GearCatalog / WaveData / QuestCatalog / the
// monetization catalogs all hand-roll today (CanonicalJson.Read → JsonConvert.
// DeserializeObject<T>). Same WebGL-safe path (CanonicalJson loads the Resources
// dual-copy first, StreamingAssets fallback on desktop), now in one guarded place.
// -----------------------------------------------------------------------------
//   var cat = DataInjector.Inject<WeaponCatalogData>("Data/Canonical/weapons.json");
//   if (DataInjector.TryInject<WaveCatalog>("Data/Canonical/waves.json", out var w)) { ... }
//
// "table" = the canonical StreamingAssets-relative path (the .json extension is
// optional — CanonicalJson strips it for the Resources.Load lookup). A miss
// (file absent, empty, or malformed JSON) logs a clear warning and returns
// default(T) / false instead of throwing — a content gap never hard-faults the
// game, exactly like the existing catalog loaders.
//
// NOTE: the existing catalogs are NOT ripped out — they can adopt this at their
// own pace (`// TODO adopt DataInjector`). New tables should use it directly.
// =============================================================================

using System;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>Generic, WebGL-safe loader: canonical JSON table → deserialized T.</summary>
    public static class DataInjector
    {
        /// <summary>
        /// Reads + deserializes the canonical JSON <paramref name="table"/> to T.
        /// <paramref name="table"/> is the StreamingAssets-relative path, e.g.
        /// "Data/Canonical/weapons.json" (the .json suffix is optional). Returns
        /// default(T) and logs a warning if the table is missing/empty/malformed —
        /// never throws.
        /// </summary>
        public static T Inject<T>(string table)
        {
            TryInject(table, out T value);
            return value;
        }

        /// <summary>
        /// Try-pattern variant: <paramref name="value"/> receives the deserialized
        /// T on success, default(T) on any miss. Returns true only when a non-empty
        /// table parsed into a non-null object.
        /// </summary>
        public static bool TryInject<T>(string table, out T value)
        {
            value = default;
            DeNelle.Core.Diagnostics.FlowTrace.Step("DataInject", $"TryInject<{typeof(T).Name}> table='{table ?? "<null>"}'");

            if (string.IsNullOrEmpty(table))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("DataInject", "empty table path — skipped (returning default).");
                Debug.LogWarning("[DataInjector] Inject called with an empty table path — skipped.");
                return false;
            }

            string json;
            try
            {
                json = CanonicalJson.Read(table);
            }
            catch (Exception ex)
            {
                // Read threw (not just missing) — hard anomaly, surface it loud.
                DeNelle.Core.Diagnostics.FlowTrace.Fail("DataInject", $"CanonicalJson.Read('{table}') threw {ex.GetType().Name}: {ex.Message}");
                Debug.LogWarning($"[DataInjector] Failed reading table '{table}': {ex.Message}");
                return false;
            }

            if (string.IsNullOrEmpty(json))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("DataInject", $"table '{table}' not found/empty (data-empty, not a parse bug) — returning default.");
                Debug.LogWarning($"[DataInjector] Table '{table}' not found or empty " +
                                 "(no Resources dual-copy and no StreamingAssets file) — returning default.");
                return false;
            }

            try
            {
                value = JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("DataInject", $"table '{table}' did NOT parse to {typeof(T).Name}: {ex.GetType().Name}: {ex.Message}");
                Debug.LogWarning($"[DataInjector] Table '{table}' did not parse to {typeof(T).Name}: {ex.Message}");
                value = default;
                return false;
            }

            if (value == null)
                DeNelle.Core.Diagnostics.FlowTrace.Warn("DataInject", $"table '{table}' parsed to NULL {typeof(T).Name} (json present but mapped empty).");
            else
                DeNelle.Core.Diagnostics.FlowTrace.Step("DataInject", $"table '{table}' -> {typeof(T).Name} OK.");
            return value != null;
        }
    }
}
