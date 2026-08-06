// =============================================================================
// HeroCanonNames - the ONE resolver for "what is this hero's NAME".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// WHY THIS EXISTS. The canon names live in Data/Canonical/en.json under
// hero.<job>.name (Grom / Sylas / Thrain / Elara), but the only reader of those
// keys was CanonStrings in DeNelle.Onboarding - reachable from the hero-select
// screen and nowhere else. Every other surface therefore showed the CLASS WORD
// instead: the HUD nameplate rendered "Ranger  Lv 1", so a player who picked the
// Ranger was never once told in-game that he is SYLAS.
//
// HUD -> Core only and Village -> Core only (assembly law), so the resolver has
// to live HERE for those layers to reach it. This is a lookup, not a second copy
// of the names: en.json stays the single authored source, and the class -> job
// key half of the mapping is still PlayableHeroes.JobKey.
//
// Loaded once, lazily, through CanonicalJson (Resources first, StreamingAssets
// fallback) so it is WebGL-safe. A missing file or key NEVER blanks a nameplate:
// it degrades to the capitalized class word, which is exactly what shipped
// before this type existed.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using Newtonsoft.Json;

namespace DeNelle.Core.State
{
    /// <summary>Canon display names for the hero classes, read from Data/Canonical/en.json.</summary>
    public static class HeroCanonNames
    {
        /// <summary>StreamingAssets-relative path to the English localizable strings.</summary>
        private const string LocaleRelativePath = "Data/Canonical/en.json";

        private static Dictionary<string, string> _locale;

        /// <summary>
        /// The canon display name for <paramref name="cls"/> ("Grom", "Sylas", "Thrain",
        /// "Elara"). Falls back to the capitalized class word when en.json is unavailable
        /// or the key is absent - never null, never empty, never a "[[missing:...]]" marker
        /// on a player-facing plate.
        /// </summary>
        public static string For(HeroClass cls) => ForJob(PlayableHeroes.JobKey(cls));

        /// <summary>
        /// The canon display name for a lowercase job key ("knight" / "ranger" / "mage" /
        /// "cleric") - the key the HUD models and the gear catalog already carry, so a
        /// caller holding only a job string does not have to reverse it into a HeroClass.
        /// "healer" is accepted as the legacy alias for "cleric".
        /// </summary>
        public static string ForJob(string jobKey)
        {
            string job = (jobKey ?? string.Empty).Trim().ToLowerInvariant();
            if (job.Length == 0) return "Hero";
            if (job == "healer") job = "cleric";   // legacy alias used by the gear/paperdoll layer

            EnsureLoaded();
            if (_locale != null && _locale.TryGetValue("hero." + job + ".name", out var name)
                && !string.IsNullOrWhiteSpace(name))
                return name;

            // No canon key for this job: show the class word rather than a debug marker.
            // Warn ONCE per job so an unauthored class is visible in the trace without spamming
            // a per-frame HUD refresh (no silent failure, but no firehose either).
            FlowTrace.Once("Canon", "hero-name-miss-" + job,
                "HeroCanonNames: no 'hero." + job + ".name' in " + LocaleRelativePath +
                " - falling back to the class word. Author the key or fix the job string.");
            return Capitalize(job);
        }

        private static void EnsureLoaded()
        {
            if (_locale != null) return;
            _locale = LoadMap(LocaleRelativePath);
        }

        // Mirrors CanonStrings.LoadMap: both canonical files are flat maps carrying some
        // leading "_" metadata keys, so deserialize loosely and keep only string entries.
        // Boot-path catalog - must never throw (a throw here would take the HUD down).
        private static Dictionary<string, string> LoadMap(string relativePath)
        {
            var map = new Dictionary<string, string>();
            try
            {
                string json = CanonicalJson.Read(relativePath);
                if (string.IsNullOrEmpty(json))
                {
                    FlowTrace.Warn("Canon",
                        "HeroCanonNames: " + relativePath + " not found (Resources or StreamingAssets) - " +
                        "hero names degrade to class words.");
                    return map;
                }

                var raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (raw != null)
                {
                    foreach (var kv in raw)
                        if (kv.Value is string s) map[kv.Key] = s;
                }
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Canon",
                    "HeroCanonNames: failed to read " + relativePath + " (" + ex.GetType().Name + ": " +
                    ex.Message + ") - hero names degrade to class words.");
            }
            return map;
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
