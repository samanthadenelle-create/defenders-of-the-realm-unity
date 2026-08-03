// =============================================================================
// DifficultyProfileCatalog -- typed loader for difficulty-profile.json.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Adaptive
//
// Reads Data/Canonical/difficulty-profile.json through DeNelle.Core.CanonicalJson
// (Resources dual-copy first -- WebGL-safe -- then a StreamingAssets fallback),
// exactly like EchoBalanceCatalog / BuildingTierCatalog. NO SECOND CONFIG
// MECHANISM IS INTRODUCED: the reference sketch reached for
// ScriptableObject.CreateInstance on a plain [Serializable] class, which cannot
// compile, and inventing a ScriptableObject asset for this would have put the
// tuning somewhere the JSON dual-copy oracle cannot see.
//
// NOTE ON THE READ CALL: a parallel lane is adding CanonicalJson.ReadCatalog<T>.
// It is NOT on disk at the time of writing, so this uses the CURRENT per-catalog
// idiom -- CanonicalJson.Read(relativePath) + JsonConvert.DeserializeObject<T> --
// which is what every shipped catalog does today. When ReadCatalog<T> lands this
// is a one-line swap inside LoadProfile() and nothing else changes.
//
// Guard-wrapped with a SENSIBLE FALLBACK: a missing or invalid file logs a
// [Flow:Difficulty] Warn and returns the built-in defaults, so difficulty
// degrades to the shipped-tested table rather than to zeros. No silent failures.
// =============================================================================

using Newtonsoft.Json;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Adaptive
{
    /// <summary>Static surface over difficulty-profile.json -- load + cache + reload.</summary>
    public static class DifficultyProfileCatalog
    {
        /// <summary>StreamingAssets-relative path. The Resources dual-copy of the same file
        /// wins at load time (WebGL has no filesystem).</summary>
        public const string StreamingRelativePath = "Data/Canonical/difficulty-profile.json";

        private const int ExpectedVersion = 1;
        private static DifficultyProfile _profile;

        /// <summary>The parsed profile. Never null -- built-in defaults if the file is absent.</summary>
        public static DifficultyProfile Profile
        {
            get
            {
                if (_profile == null) _profile = LoadProfile();
                return _profile;
            }
        }

        /// <summary>Force a re-read (test / hot-reload / the oracle re-checking a retuned file).</summary>
        public static void Reload()
        {
            _profile = null;
            _profile = LoadProfile();
        }

        /// <summary>Overrides the cached profile. EDITOR/TEST ONLY -- lets an EditMode test or
        /// the headless oracle drive the real code path against a synthetic profile without
        /// touching the shipped JSON.</summary>
        public static void OverrideForTests(DifficultyProfile profile)
        {
            _profile = profile != null ? profile.Validate() : null;
        }

        private static DifficultyProfile LoadProfile()
        {
            var parsed = Guard.Try("Difficulty", "load difficulty-profile.json", () =>
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (string.IsNullOrEmpty(json))
                {
                    FlowTrace.Warn("Difficulty",
                        "difficulty-profile.json not found (Resources or StreamingAssets) -- using built-in defaults.");
                    return (DifficultyProfile)null;
                }

                var d = JsonConvert.DeserializeObject<DifficultyProfile>(json);
                if (d == null)
                {
                    FlowTrace.Warn("Difficulty", "difficulty-profile.json parsed null -- using built-in defaults.");
                    return (DifficultyProfile)null;
                }

                if (d.Version != ExpectedVersion)
                    FlowTrace.Warn("Difficulty",
                        "difficulty-profile.json version " + d.Version + " != expected " + ExpectedVersion +
                        " -- loading anyway (additive).");

                d.Validate();
                FlowTrace.Step("Difficulty",
                    "DifficultyProfileCatalog loaded (version " + d.Version +
                    ", window " + d.SampleWindow + ", gate " + d.MinSamples +
                    ", rails " + d.MinMultiplier.ToString("0.##") + ".." + d.MaxMultiplier.ToString("0.##") +
                    " / spike ceiling " + d.MaxMultiplierWithSpike.ToString("0.##") + ").");
                return d;
            }, fallback: null);

            if (parsed != null) return parsed;
            FlowTrace.Warn("Difficulty", "DifficultyProfileCatalog falling back to built-in defaults (file missing/invalid).");
            return new DifficultyProfile().Validate();
        }
    }
}
