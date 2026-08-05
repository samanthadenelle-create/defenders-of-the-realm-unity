// =============================================================================
// StorageCapsCatalog -- typed loader for storage-caps.json (WO-857 / WO-901 Phase F).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Economy
//
// The TUNABLE half of the town bank cap. The STRUCTURE (which resources can be
// capped at all, the absolute floor under baseCap, the fill/drain ordering law)
// is code in TownBankCapacity and is NOT data-overridable -- a bad JSON edit must
// never be able to soft-lock a save. Everything an owner re-tunes in playtest --
// the per-resource baseCap, the per-level capacity multipliers, the overflow warn
// cooldown -- lives here so a tune needs no recompile.
//
// Mirrors EchoBalanceCatalog exactly: reads Data/Canonical/storage-caps.json via
// DeNelle.Core.CanonicalJson (Resources dual-copy wins -- WebGL-safe -- then
// StreamingAssets) and caches a typed def. Guard-wrapped with SENSIBLE FALLBACKS:
// a missing/invalid file logs a [Flow:Bank] Warn and returns the built-in defaults
// so nothing hard-fails and no cap can ever resolve to zero.
//
// SAFETY (the fresh-save soft-lock guard, WO-901 Sec.5): the field defaults below ARE
// the built-in fallback, and TownBankCapacity.BaseCapOf additionally floors every
// answer at TownBankCapacity.AbsoluteMinBaseCap. A deleted file, a truncated file,
// a "baseCap": {} object, or an authored 0 all still yield a playable wallet.
// =============================================================================

using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Economy
{
    /// <summary>The parsed storage-caps.json root. Field defaults ARE the built-in fallback.</summary>
    [System.Serializable]
    public sealed class StorageCapsData
    {
        [JsonProperty("version")] public int Version = 1;

        /// <summary>
        /// Per-resource baseline wallet cap before any storage container is built, keyed by the
        /// same lowercase resource word structures-catalog uses in <c>repo.storageResource</c>
        /// ("wood" / "iron" / "food"). A "crystals" or "coins" key here is IGNORED -- those are
        /// UNCAPPED by design (owner ruling 2026-08-04) and the exemption is enforced in code.
        /// </summary>
        [JsonProperty("baseCap")] public Dictionary<string, int> BaseCap = new Dictionary<string, int>();

        /// <summary>
        /// Container capacity multiplier by level: index 0 = level 1, index 1 = level 2, ...
        /// A level beyond the array clamps to the LAST entry (never 0 -- a missing row must not
        /// silently delete a built container's capacity). Default [1, 2, 3] = a level-3 pallet
        /// holds 3x a level-1 pallet, matching the maxLevel:3 the three container rows author.
        /// </summary>
        [JsonProperty("levelCapacityMultipliers")] public List<float> LevelCapacityMultipliers = new List<float> { 1f, 2f, 3f };

        /// <summary>Seconds between player-facing overflow warnings for the SAME resource. The
        /// FlowTrace warn is never throttled (Sec.12 -- the data line must always exist); only the
        /// on-screen toast is, so a hot income loop cannot spam the screen.</summary>
        [JsonProperty("overflowWarnCooldownSeconds")] public float OverflowWarnCooldownSeconds = 8f;
    }

    /// <summary>Static surface over storage-caps.json -- load + cache + typed getters.</summary>
    public static class StorageCapsCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/storage-caps.json";
        private const int ExpectedVersion = 1;
        private static StorageCapsData _data;

        /// <summary>The full parsed data (never null -- built-in defaults if the file is absent).</summary>
        public static StorageCapsData Data { get { EnsureLoaded(); return _data; } }

        /// <summary>
        /// Authored baseline cap for a lowercase resource word, or 0 when the key is absent.
        /// Callers MUST route through <see cref="TownBankCapacity.BaseCapOf"/>, which applies the
        /// absolute floor -- this raw getter is deliberately unfloored so the regression can see
        /// exactly what the data says.
        /// </summary>
        public static int RawBaseCap(string resourceWord)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(resourceWord) || _data.BaseCap == null) return 0;
            return _data.BaseCap.TryGetValue(resourceWord.ToLowerInvariant(), out var v) ? v : 0;
        }

        /// <summary>Capacity multiplier for a 1-based container level. Clamps into the authored
        /// array and never returns a value below 1 (a container can never shrink below its L1 size).</summary>
        public static float LevelMultiplier(int level)
        {
            EnsureLoaded();
            var list = _data.LevelCapacityMultipliers;
            if (list == null || list.Count == 0) return 1f;
            int idx = Mathf.Clamp(level - 1, 0, list.Count - 1);
            return Mathf.Max(1f, list[idx]);
        }

        /// <summary>Seconds between on-screen overflow toasts for the same resource (>= 0).</summary>
        public static float OverflowWarnCooldownSeconds
        {
            get { EnsureLoaded(); return Mathf.Max(0f, _data.OverflowWarnCooldownSeconds); }
        }

        /// <summary>Force a re-read (test / hot-reload).</summary>
        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadData();
        }

        private static StorageCapsData LoadData()
        {
            var parsed = Guard.Try("Bank", "load storage-caps.json", () =>
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (string.IsNullOrEmpty(json))
                {
                    FlowTrace.Warn("Bank", "storage-caps.json not found (Resources or StreamingAssets) -- using built-in default caps.");
                    return (StorageCapsData)null;
                }
                var d = JsonConvert.DeserializeObject<StorageCapsData>(json);
                if (d == null)
                {
                    FlowTrace.Warn("Bank", "storage-caps.json parsed null -- using built-in default caps.");
                    return (StorageCapsData)null;
                }
                if (d.Version != ExpectedVersion)
                    FlowTrace.Warn("Bank", $"storage-caps.json version {d.Version} != expected {ExpectedVersion} -- loading anyway (additive).");
                if (d.BaseCap == null) d.BaseCap = new Dictionary<string, int>();
                if (d.LevelCapacityMultipliers == null || d.LevelCapacityMultipliers.Count == 0)
                {
                    FlowTrace.Warn("Bank", "storage-caps.json authored no levelCapacityMultipliers -- defaulting to [1,2,3].");
                    d.LevelCapacityMultipliers = new List<float> { 1f, 2f, 3f };
                }
                FlowTrace.Step("Bank", $"StorageCapsCatalog loaded (version {d.Version}, {d.BaseCap.Count} baseCap rows, {d.LevelCapacityMultipliers.Count} level multipliers).");
                return d;
            }, fallback: null);

            if (parsed != null) return parsed;
            FlowTrace.Warn("Bank", "StorageCapsCatalog falling back to built-in default caps (file missing/invalid) -- baseCap floors at TownBankCapacity.AbsoluteMinBaseCap, so no save can soft-lock.");
            return new StorageCapsData();
        }
    }
}
