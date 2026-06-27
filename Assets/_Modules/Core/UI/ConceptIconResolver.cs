// =============================================================================
// ConceptIconResolver — DATA-DRIVEN concept->icon map for the dumb HUD skin.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// THE PROBLEM IT FILLS: the assignable skill bar (and any id-keyed widget) had
// NO abilityId->sprite map, so it rendered letter-glyphs. This resolver lets the
// skin ask "what sprite for this conceptId?" and get a real Obsidian RPG sprite,
// with the caller keeping its glyph fallback when nothing maps / the art is absent.
//
// DATA, NOT CODE: the single source of the mapping is the canonical JSON
//   Assets/Resources/Data/Canonical/concept-icons.json   (WebGL-safe, Resources)
//   Assets/StreamingAssets/Data/Canonical/concept-icons.json (desktop source)
// keyed by a lower-cased conceptId (abilityId / skillId / effect / itemId / owner
// concept token) -> { role, name }. The sprite itself is resolved through
// RpgUiCatalog.Get(role, name) — so this class chooses NO icon names; the table does.
//
// NULL-SAFE CONTRACT (mirrors RpgUiCatalog EXACTLY): every Resolve* returns a
// sprite when the concept is mapped AND the pack art is present, else NULL. EVERY
// caller keeps its existing procedural/glyph fallback for the null case. This class
// NEVER throws to a caller (all IO/parse is wrapped) and caches the parsed map so
// repeated lookups are free. The HUD stays dumb: it passes ids, the data decides.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core;                // CanonicalJson (WebGL-safe loader)
using DeNelle.Core.Diagnostics;    // FlowTrace (§12 instrument-first)

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Resolves a conceptId (abilityId / skillId / effect / itemId / concept token) to a
    /// real RpgUiCatalog sprite via the canonical concept-icons.json table. Sprite-first
    /// with a NULL return when the concept is unmapped or the art is absent — callers keep
    /// their glyph fallback. WebGL-safe (CanonicalJson -> Resources first), cached, never throws.
    /// </summary>
    public static class ConceptIconResolver
    {
        private const string CanonRelativePath = "Data/Canonical/concept-icons.json";

        /// <summary>One mapped icon: a RpgUiCatalog (role, name) address.</summary>
        [Serializable]
        private sealed class IconRef
        {
            [JsonProperty("role")] public string Role;
            [JsonProperty("name")] public string Name;
            /// <summary>
            /// OPT-IN: when true, this concept FORCES its Obsidian icon over a caller's own
            /// richer art (used by the defaults bar to let the owner override class art per-concept,
            /// pure data). Absent/false = the icon is only a gap-filler fallback. Newtonsoft leaves
            /// this false when the field is missing.
            /// </summary>
            [JsonProperty("override")] public bool Override;
        }

        /// <summary>The parsed concept-icons.json root.</summary>
        [Serializable]
        private sealed class ConceptIconData
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("default")] public IconRef Default;
            [JsonProperty("map")] public Dictionary<string, IconRef> Map
                = new Dictionary<string, IconRef>(StringComparer.OrdinalIgnoreCase);
        }

        // conceptId (lower-cased) -> icon address; plus the catch-all default. Built once.
        private static Dictionary<string, IconRef> _map;
        private static IconRef _default;
        private static bool _loaded;

        /// <summary>
        /// The sprite mapped to <paramref name="conceptId"/>, or NULL when the concept is
        /// unmapped OR the pack art for it is absent. Caller keeps its glyph fallback on null.
        /// Lookup is case-insensitive on the trimmed id.
        /// </summary>
        public static Sprite Resolve(string conceptId)
        {
            if (string.IsNullOrEmpty(conceptId)) return null;
            EnsureLoaded();
            if (_map == null) return null;

            string key = conceptId.Trim().ToLowerInvariant();
            if (!_map.TryGetValue(key, out var icon) || icon == null)
                return null; // unmapped -> caller fallback (no spam: misses are expected/normal)

            var sprite = RpgUiCatalog.Get(icon.Role, icon.Name);
            if (sprite == null)
                FlowTrace.Throttle("Icon", "miss-art:" + key, 5f,
                    "concept '" + key + "' maps to " + icon.Role + "/" + icon.Name +
                    " but the pack art is absent — caller keeps glyph fallback");
            return sprite;
        }

        /// <summary>
        /// First non-null <see cref="Resolve"/> across the ordered candidate ids — lets a caller
        /// pass a def's own fields (id, effect, name token) and let the DATA decide which resolves,
        /// with NO icon choice in C#. Returns null when none map / no art — caller keeps its fallback.
        /// </summary>
        public static Sprite ResolveAny(params string[] conceptIds)
        {
            if (conceptIds == null) return null;
            for (int i = 0; i < conceptIds.Length; i++)
            {
                var s = Resolve(conceptIds[i]);
                if (s != null) return s;
            }
            return null;
        }

        /// <summary>
        /// Like <see cref="Resolve"/> but returns the sprite ONLY when the matched entry is flagged
        /// <c>override:true</c> in the table (else null) — the OPT-IN path that lets a concept force its
        /// Obsidian icon over a caller's own richer art. Null when unmapped, not-overridden, or art absent.
        /// </summary>
        public static Sprite ResolveOverride(string conceptId)
        {
            if (string.IsNullOrEmpty(conceptId)) return null;
            EnsureLoaded();
            if (_map == null) return null;

            string key = conceptId.Trim().ToLowerInvariant();
            if (!_map.TryGetValue(key, out var icon) || icon == null || !icon.Override)
                return null; // unmapped or not opted-in -> caller keeps its own art

            var sprite = RpgUiCatalog.Get(icon.Role, icon.Name);
            if (sprite == null)
                FlowTrace.Throttle("Icon", "miss-art-ovr:" + key, 5f,
                    "OVERRIDE concept '" + key + "' maps to " + icon.Role + "/" + icon.Name +
                    " but the pack art is absent — caller keeps its own art");
            return sprite;
        }

        /// <summary>
        /// First non-null <see cref="ResolveOverride"/> across the ordered candidate ids — the opt-in
        /// override path for a caller passing a def's own fields. Returns null when none are flagged
        /// override / map / have art, so the caller keeps its own art.
        /// </summary>
        public static Sprite ResolveAnyOverride(params string[] conceptIds)
        {
            if (conceptIds == null) return null;
            for (int i = 0; i < conceptIds.Length; i++)
            {
                var s = ResolveOverride(conceptIds[i]);
                if (s != null) return s;
            }
            return null;
        }

        /// <summary>
        /// The table's catch-all default sprite (role/name in concept-icons.json "default"), or null
        /// when absent. For callers (e.g. a glyph-only bar) that prefer a generic icon over a letter
        /// glyph; callers that own richer per-slot art should NOT use this (keep their own fallback).
        /// </summary>
        public static Sprite DefaultSprite()
        {
            EnsureLoaded();
            return _default != null ? RpgUiCatalog.Get(_default.Role, _default.Name) : null;
        }

        // ── Lazy load (WebGL-safe via CanonicalJson; cached; never throws to the caller) ──
        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true; // set first so a parse failure does not retry every call
            _map = new Dictionary<string, IconRef>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string json = CanonicalJson.Read(CanonRelativePath);
                if (string.IsNullOrEmpty(json))
                {
                    FlowTrace.Warn("Icon", "concept-icons.json not found (Resources or StreamingAssets) — " +
                        "every concept falls back to its glyph.");
                    return;
                }

                var data = JsonConvert.DeserializeObject<ConceptIconData>(json);
                if (data == null)
                {
                    FlowTrace.Warn("Icon", "concept-icons.json parsed empty — glyph fallback everywhere.");
                    return;
                }

                _default = data.Default;
                if (data.Map != null)
                {
                    foreach (var kv in data.Map)
                    {
                        if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                        _map[kv.Key.Trim().ToLowerInvariant()] = kv.Value;
                    }
                }
                FlowTrace.Once("Icon", "loaded",
                    "concept-icons.json loaded — " + _map.Count + " concept(s) mapped" +
                    (_default != null ? " (+default " + _default.Role + "/" + _default.Name + ")" : ""));
            }
            catch (Exception ex)
            {
                // §12 no silent failure: a bad table logs and leaves an empty map (all glyph fallback).
                FlowTrace.Warn("Icon", "concept-icons.json load/parse failed — glyph fallback everywhere. " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: drop the cache so a JSON re-edit is picked up without a domain reload.</summary>
        public static void ClearCache()
        {
            _map = null;
            _default = null;
            _loaded = false;
        }
#endif
    }
}
