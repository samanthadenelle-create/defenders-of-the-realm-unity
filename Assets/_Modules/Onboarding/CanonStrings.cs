// =============================================================================
// CanonStrings — read-only loader for the canonical onboarding text (Week 1)
// -----------------------------------------------------------------------------
// The Onboarding scenes must NEVER hardcode a canon string (the v2 port-spec
// Part 4 rule: "the Unity agent never types these inline"). The Localization
// package will own these strings long-term, but Week 1 ships before the string
// table is wired, so this tiny static loader reads the two canonical JSON files
// directly — exactly the StreamingAssets pattern Theme.cs already uses for
// themes.json (synchronous read; valid in the Editor and on the Week-1 desktop
// targets; an Android UnityWebRequest path is a later concern).
//
//   canon-strings.json — proper nouns: tagline, publisher, game title …
//   en.json            — localizable strings incl. the 3-line cold open.
//
// Both files are flat string→string maps, so a single Dictionary<string,string>
// per file is enough. Unknown keys return a visible "[[missing:key]]" marker so
// a typo is obvious on screen rather than silently blank.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Static read-only access to the canonical onboarding strings —
    /// <c>canon-strings.json</c> (proper nouns) and <c>en.json</c> (localizable
    /// copy). Loaded lazily from StreamingAssets on first access.
    /// </summary>
    public static class CanonStrings
    {
        /// <summary>StreamingAssets-relative path to the canon proper-noun file.</summary>
        private const string CanonRelativePath = "Data/Canonical/canon-strings.json";

        /// <summary>StreamingAssets-relative path to the English localizable strings.</summary>
        private const string LocaleRelativePath = "Data/Canonical/en.json";

        // ── Canon-strings.json keys used by the Onboarding module ────────────
        /// <summary>Key — the title-screen tagline ("Hold the last light.").</summary>
        public const string KeyTagline = "tagline";
        /// <summary>Key — the publisher / studio name ("DeNelle Studios").</summary>
        public const string KeyPublisher = "publisher";
        /// <summary>Key — the main game title ("Echoes of Elarion").</summary>
        public const string KeyGameTitle = "gameTitle";
        /// <summary>Key — the series / franchise label ("Defenders of the Realm").</summary>
        public const string KeyGameSubtitle = "gameSubtitle";
        /// <summary>Key — the Heart-Wing brand-dragon proper noun.</summary>
        public const string KeyHeartWing = "heartWing";

        // ── en.json keys for the three-line cold open (narrative-bible §7.1) ─
        /// <summary>Key — cold-open line 1.</summary>
        public const string KeyColdOpenLine1 = "intro.coldOpen.line1";
        /// <summary>Key — cold-open line 2.</summary>
        public const string KeyColdOpenLine2 = "intro.coldOpen.line2";
        /// <summary>Key — cold-open line 3.</summary>
        public const string KeyColdOpenLine3 = "intro.coldOpen.line3";

        private static Dictionary<string, string> _canon;
        private static Dictionary<string, string> _locale;

        /// <summary>
        /// Resolves a key from <c>canon-strings.json</c> (the proper nouns).
        /// Returns a visible <c>[[missing:key]]</c> marker for an unknown key.
        /// </summary>
        public static string Canon(string key)
        {
            EnsureLoaded();
            return Resolve(_canon, key);
        }

        /// <summary>
        /// Resolves a key from <c>en.json</c> (the localizable copy).
        /// Returns a visible <c>[[missing:key]]</c> marker for an unknown key.
        /// </summary>
        public static string Locale(string key)
        {
            EnsureLoaded();
            return Resolve(_locale, key);
        }

        /// <summary>The title-screen tagline — "Hold the last light."</summary>
        public static string Tagline => Canon(KeyTagline);

        /// <summary>The publisher name — "DeNelle Studios".</summary>
        public static string Publisher => Canon(KeyPublisher);

        /// <summary>The main game title — "Echoes of Elarion".</summary>
        public static string GameTitle => Canon(KeyGameTitle);

        /// <summary>The series / franchise label — "Defenders of the Realm" (this game is a chapter of that saga).</summary>
        public static string GameSubtitle => Canon(KeyGameSubtitle);

        /// <summary>The three cold-open lines, in order (narrative-bible §7.1).</summary>
        public static string[] ColdOpenLines()
        {
            EnsureLoaded();
            return new[]
            {
                Resolve(_locale, KeyColdOpenLine1),
                Resolve(_locale, KeyColdOpenLine2),
                Resolve(_locale, KeyColdOpenLine3),
            };
        }

        // =====================================================================
        //  Loading
        // =====================================================================

        private static void EnsureLoaded()
        {
            if (_canon == null) _canon = LoadMap(CanonRelativePath);
            if (_locale == null) _locale = LoadMap(LocaleRelativePath);
        }

        private static Dictionary<string, string> LoadMap(string relativePath)
        {
            // WebGL-safe load via CanonicalJson (Resources first, StreamingAssets fallback).
            // Boot-path catalog — must not throw in a browser (DEF-124 black screen).
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(relativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    // Both canonical files are flat maps with some leading "_" metadata
                    // keys; deserialize loosely, then keep only the string entries.
                    var raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    var map = new Dictionary<string, string>();
                    if (raw != null)
                    {
                        foreach (var kv in raw)
                        {
                            if (kv.Value is string s) map[kv.Key] = s;
                        }
                    }
                    return map;
                }
                Debug.LogError($"[CanonStrings] Canonical file not found (Resources or StreamingAssets): {relativePath}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CanonStrings] Failed to read {relativePath}: {ex.Message}");
            }
            return new Dictionary<string, string>();
        }

        private static string Resolve(Dictionary<string, string> map, string key)
        {
            if (map != null && key != null && map.TryGetValue(key, out var value) && value != null)
                return value;
            Debug.LogWarning($"[CanonStrings] Missing canonical key '{key}'.");
            return $"[[missing:{key}]]";
        }
    }
}
