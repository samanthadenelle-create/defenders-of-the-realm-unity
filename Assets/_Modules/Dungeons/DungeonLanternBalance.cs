// =============================================================================
// DungeonLanternBalance -- typed loader for the "lantern" block of
// Data/Canonical/dungeon-balance.json (WO-1112).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// WHY THIS EXISTS (owner ruling 2026-08-16, verbatim: "we should make the lanterns
// last triple that at minimum or option to dim to extend"): the lantern's burn was
// tuned NOWHERE. maxOil 100 and oilDrainPerSec 1.6 were [SerializeField] defaults
// inside Lantern.cs -- a HIDDEN CODE DEFAULT, the same shape as the silent 6x
// storage-repair issue found the same day. A knob nobody can see is a knob nobody
// re-tunes, and this one decided whether the player spent the run in the dark:
// 100 / 1.6 = 62.5s to empty, with Lantern.IsInDarkness latching at 12% oil, so any
// composed dungeon went permanently dark about 53 seconds in.
//
// The numbers now live in data. Mirrors EchoBalanceCatalog exactly -- reads through
// DeNelle.Core.CanonicalJson (Resources dual-copy wins, WebGL-safe, then
// StreamingAssets), caches, and is Guard-wrapped with SENSIBLE FALLBACKS so a
// missing or invalid file logs a [Flow:Dungeon] Warn and returns the built-in
// defaults rather than hard-failing a run.
//
// ⚠ THE CODE DEFAULTS BELOW MIRROR THE AUTHORED JSON ON PURPOSE. Keep them in step.
// If they drift back to 1.6, an absent file silently reinstates the 62.5s burn --
// which is precisely the failure mode this file was created to end.
// =============================================================================

using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Dungeons
{
    /// <summary>The "lantern" block of dungeon-balance.json. Field defaults ARE the fallback.</summary>
    [System.Serializable]
    public sealed class DungeonLanternBalanceData
    {
        /// <summary>A full flask, in arbitrary oil units. Every oil FRACTION threshold
        /// (low-oil 0.25, darkness 0.12, min-light 0.35) is expressed against this, and an
        /// oil stone tops the flask right back to it -- so this is the meter's 100%, not a
        /// duration knob. Tune <see cref="OilDrainPerSec"/> for duration.</summary>
        [JsonProperty("maxOil")] public float MaxOil = 100f;

        /// <summary>Oil burned per second while a run is active. THE duration knob:
        /// secondsToEmpty = maxOil / oilDrainPerSec. 0.5 gives 200s (3.2x the old 62.5s),
        /// with the darkness latch at 12% reached after ~176s.</summary>
        [JsonProperty("oilDrainPerSec")] public float OilDrainPerSec = 0.5f;
    }

    /// <summary>The parsed dungeon-balance.json root.</summary>
    [System.Serializable]
    public sealed class DungeonBalanceData
    {
        [JsonProperty("version")] public int Version = 1;
        [JsonProperty("lantern")] public DungeonLanternBalanceData Lantern = new DungeonLanternBalanceData();
    }

    /// <summary>Static surface over dungeon-balance.json -- load + cache + typed getters.</summary>
    public static class DungeonLanternBalance
    {
        private const string Sys = "Dungeon";
        private const string StreamingRelativePath = "Data/Canonical/dungeon-balance.json";
        private const int ExpectedVersion = 1;
        private static DungeonBalanceData _data;

        /// <summary>A full flask (>0). Never throws; falls back to the built-in default.</summary>
        public static float MaxOil { get { EnsureLoaded(); return Mathf.Max(1f, _data.Lantern.MaxOil); } }

        /// <summary>Oil burned per second while a run is active (>0).</summary>
        public static float OilDrainPerSec { get { EnsureLoaded(); return Mathf.Max(0.01f, _data.Lantern.OilDrainPerSec); } }

        /// <summary>Seconds a full flask lasts at the authored drain. Diagnostics + regression.</summary>
        public static float SecondsToEmpty => MaxOil / OilDrainPerSec;

        /// <summary>Force a re-read (test / hot-reload).</summary>
        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadData();
        }

        private static DungeonBalanceData LoadData()
        {
            var parsed = Guard.Try(Sys, "load dungeon-balance.json", () =>
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (string.IsNullOrEmpty(json))
                {
                    FlowTrace.Warn(Sys, "dungeon-balance.json not found (Resources or StreamingAssets) - using built-in default lantern balance.");
                    return (DungeonBalanceData)null;
                }
                var d = JsonConvert.DeserializeObject<DungeonBalanceData>(json);
                if (d == null)
                {
                    FlowTrace.Warn(Sys, "dungeon-balance.json parsed null - using built-in default lantern balance.");
                    return (DungeonBalanceData)null;
                }
                if (d.Lantern == null) d.Lantern = new DungeonLanternBalanceData();
                if (d.Version != ExpectedVersion)
                    FlowTrace.Warn(Sys, $"dungeon-balance.json version {d.Version} != expected {ExpectedVersion} - loading anyway (additive).");
                FlowTrace.Step(Sys,
                    $"DungeonLanternBalance loaded (version {d.Version}): maxOil={d.Lantern.MaxOil:F0} " +
                    $"drain={d.Lantern.OilDrainPerSec:F2}/s -> {(d.Lantern.OilDrainPerSec > 0f ? d.Lantern.MaxOil / d.Lantern.OilDrainPerSec : 0f):F0}s to empty.");
                return d;
            }, fallback: null);

            if (parsed != null) return parsed;
            FlowTrace.Warn(Sys, "DungeonLanternBalance falling back to built-in defaults (file missing/invalid).");
            return new DungeonBalanceData();
        }
    }
}
