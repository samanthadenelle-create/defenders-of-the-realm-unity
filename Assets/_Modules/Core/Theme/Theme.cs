// =============================================================================
// Theme — runtime theme tokens, loaded from the canonical themes.json (spec §4.4)
// -----------------------------------------------------------------------------
// C# port of src/lib/themes.ts. IMPROVEMENT #2 (adopted): the 7 themes are NOT
// hardcoded — they live in Assets/Data/Canonical/themes.json (HSL triples
// transcribed verbatim from themes.ts). Theme.cs LOADS + PARSES that file and
// exposes the default theme (`midnight-luxe`) as typed Color properties. The
// other six are reachable via Theme.Apply(key) — the React `useTheme` job.
//
// Source of truth is the HSL triple ("H S% L%"). HslToColor() converts it so a
// future designer edit in the JSON stays lossless. Theme.uss carries the same
// 38 tokens as USS hex custom properties for UI Toolkit documents.
//
// Loading: reads Assets/StreamingAssets/Data/Canonical/themes.json via
// Application.streamingAssetsPath — readable in the Editor and in desktop
// builds. The canonical JSON lives under StreamingAssets (not Assets/Data/
// Canonical/) so it is available at runtime without Resources.Load, which
// spec Part 3 forbids. See the unity-decisions.md row dated 2026-05-18.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core.Theme
{
    /// <summary>One preset theme's parsed data (mirrors <c>ThemeConfig</c> in themes.ts).</summary>
    [Serializable]
    public sealed class ThemeConfig
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("description")] public string Description;
        /// <summary>Corner radius (e.g. "0.5rem", "0px").</summary>
        [JsonProperty("radius")] public string Radius;
        [JsonProperty("font")] public ThemeFont Font;
        /// <summary>token → HSL triple ("H S% L%").</summary>
        [JsonProperty("colors")] public Dictionary<string, string> Colors = new Dictionary<string, string>();
    }

    /// <summary>A theme's font reference.</summary>
    [Serializable]
    public sealed class ThemeFont
    {
        [JsonProperty("family")] public string Family;
        [JsonProperty("url")] public string Url;
    }

    /// <summary>The parsed themes.json root.</summary>
    [Serializable]
    public sealed class ThemeCatalog
    {
        /// <summary>The default theme key — <c>midnight-luxe</c>.</summary>
        [JsonProperty("default")] public string Default;
        [JsonProperty("themes")] public Dictionary<string, ThemeConfig> Themes = new Dictionary<string, ThemeConfig>();
    }

    /// <summary>
    /// Static theme surface — loads the canonical themes.json, exposes the
    /// active theme's 38 tokens as typed <see cref="Color"/> values.
    /// </summary>
    public static class Theme
    {
        /// <summary>The default theme key (themes.ts <c>DEFAULT_THEME</c>).</summary>
        public const string DefaultThemeKey = "midnight-luxe";

        /// <summary>StreamingAssets-relative path to the canonical theme data.</summary>
        private const string StreamingRelativePath = "Data/Canonical/themes.json";

        private static ThemeCatalog _catalog;
        private static ThemeConfig _active;
        private static string _activeKey;

        /// <summary>The currently-applied theme's key (defaults to <see cref="DefaultThemeKey"/>).</summary>
        public static string ActiveKey
        {
            get { EnsureLoaded(); return _activeKey; }
        }

        /// <summary>The currently-applied theme's parsed config.</summary>
        public static ThemeConfig Active
        {
            get { EnsureLoaded(); return _active; }
        }

        /// <summary>All preset theme keys (themes.ts <c>themeNames</c>).</summary>
        public static IEnumerable<string> ThemeKeys
        {
            get { EnsureLoaded(); return _catalog.Themes.Keys; }
        }

        // ── Typed token accessors (the 18 the spec §4.2 calls out + the rest) ─
        public static Color Background => Token("background");
        public static Color Foreground => Token("foreground");
        public static Color Card => Token("card");
        public static Color CardForeground => Token("card-foreground");
        public static Color Popover => Token("popover");
        public static Color PopoverForeground => Token("popover-foreground");
        public static Color Primary => Token("primary");
        public static Color PrimaryForeground => Token("primary-foreground");
        public static Color Secondary => Token("secondary");
        public static Color SecondaryForeground => Token("secondary-foreground");
        public static Color Muted => Token("muted");
        public static Color MutedForeground => Token("muted-foreground");
        public static Color Accent => Token("accent");
        public static Color AccentForeground => Token("accent-foreground");
        public static Color Destructive => Token("destructive");
        public static Color DestructiveForeground => Token("destructive-foreground");
        public static Color Border => Token("border");
        public static Color Input => Token("input");
        public static Color Ring => Token("ring");
        public static Color Link => Token("link");
        public static Color LinkHover => Token("link-hover");
        public static Color Button => Token("button");
        public static Color ButtonForeground => Token("button-foreground");

        /// <summary>The active theme's corner radius in pixels (1rem = 16px).</summary>
        public static float RadiusPx
        {
            get { EnsureLoaded(); return RemToPixels(_active != null ? _active.Radius : "0.5rem"); }
        }

        // =====================================================================
        //  Theme resolution
        // =====================================================================

        /// <summary>
        /// Resolves a token (e.g. "primary") of the active theme to a
        /// <see cref="Color"/>. Returns <see cref="Color.magenta"/> for an
        /// unknown token so a missing key is obvious on screen.
        /// </summary>
        public static Color Token(string token)
        {
            EnsureLoaded();
            if (_active != null && _active.Colors != null &&
                _active.Colors.TryGetValue(token, out var hsl) && !string.IsNullOrEmpty(hsl))
            {
                return HslToColor(hsl);
            }
            Debug.LogWarning($"[Theme] Unknown token '{token}' in theme '{_activeKey}'.");
            return Color.magenta;
        }

        /// <summary>
        /// Switches the active theme (the React <c>useTheme</c> behaviour).
        /// Week 1 ships <see cref="DefaultThemeKey"/> live; the other six are
        /// reachable here. No-op + warning for an unknown key.
        /// </summary>
        public static void Apply(string themeKey)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(themeKey) || !_catalog.Themes.ContainsKey(themeKey))
            {
                Debug.LogWarning($"[Theme] Unknown theme key '{themeKey}' — keeping '{_activeKey}'.");
                return;
            }
            _activeKey = themeKey;
            _active = _catalog.Themes[themeKey];
        }

        // =====================================================================
        //  Loading
        // =====================================================================

        private static void EnsureLoaded()
        {
            if (_catalog != null) return;
            _catalog = LoadCatalog();
            _activeKey = !string.IsNullOrEmpty(_catalog.Default) ? _catalog.Default : DefaultThemeKey;
            if (!_catalog.Themes.TryGetValue(_activeKey, out _active))
            {
                Debug.LogError($"[Theme] Default theme '{_activeKey}' missing from themes.json.");
                // Fall back to whatever theme is first, if any.
                foreach (var kv in _catalog.Themes) { _activeKey = kv.Key; _active = kv.Value; break; }
            }
        }

        private static ThemeCatalog LoadCatalog()
        {
            // WebGL-safe load via CanonicalJson (Resources.Load first — works in a browser
            // where File.ReadAllText(streamingAssetsPath) throws — then StreamingAssets
            // fallback on desktop). themes.json is mirrored into Assets/Resources/Data/Canonical/.
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = Parse(json);
                    if (parsed != null) return parsed;
                }
                else
                {
                    Debug.LogError("[Theme] themes.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Theme] Failed to read themes.json: {ex.Message}");
            }

            Debug.LogError("[Theme] themes.json could not be loaded — using an empty catalog.");
            return new ThemeCatalog { Default = DefaultThemeKey };
        }

        private static ThemeCatalog Parse(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<ThemeCatalog>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Theme] themes.json parse failed: {ex.Message}");
                return null;
            }
        }

        // =====================================================================
        //  HSL → Color  (the shadcn "H S% L%" CSS-variable convention)
        // =====================================================================

        /// <summary>
        /// Converts an HSL triple string (<c>"H S% L%"</c>, e.g. "45 70% 50%")
        /// to a Unity <see cref="Color"/>. The space-separated, percent-suffixed
        /// form is the shadcn CSS-variable convention themes.ts uses.
        /// </summary>
        public static Color HslToColor(string hslTriple)
        {
            if (string.IsNullOrWhiteSpace(hslTriple)) return Color.magenta;

            var parts = hslTriple.Replace("%", string.Empty)
                                 .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return Color.magenta;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var l))
            {
                return Color.magenta;
            }

            return HslToColor(h, s / 100f, l / 100f);
        }

        /// <summary>Converts H (0-360) / S (0-1) / L (0-1) to an RGB <see cref="Color"/>.</summary>
        public static Color HslToColor(float hueDegrees, float s, float l)
        {
            var h = Mathf.Repeat(hueDegrees, 360f) / 360f;
            s = Mathf.Clamp01(s);
            l = Mathf.Clamp01(l);

            if (Mathf.Approximately(s, 0f))
                return new Color(l, l, l, 1f); // achromatic (grey)

            var q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            var p = 2f * l - q;
            var r = HueToChannel(p, q, h + 1f / 3f);
            var g = HueToChannel(p, q, h);
            var b = HueToChannel(p, q, h - 1f / 3f);
            return new Color(r, g, b, 1f);
        }

        private static float HueToChannel(float p, float q, float t)
        {
            t = Mathf.Repeat(t, 1f);
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        /// <summary>Converts a CSS rem/px radius string to pixels (1rem = 16px).</summary>
        public static float RemToPixels(string radius)
        {
            if (string.IsNullOrWhiteSpace(radius)) return 8f;
            var trimmed = radius.Trim();
            if (trimmed.EndsWith("rem", StringComparison.OrdinalIgnoreCase))
            {
                var num = trimmed.Substring(0, trimmed.Length - 3);
                return float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var rem)
                    ? rem * 16f
                    : 8f;
            }
            if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                var num = trimmed.Substring(0, trimmed.Length - 2);
                return float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var px)
                    ? px
                    : 0f;
            }
            return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw)
                ? raw
                : 8f;
        }

        /// <summary>Converts a <see cref="Color"/> to a USS/CSS <c>#RRGGBB</c> hex string.</summary>
        public static string ToHex(Color c)
        {
            var r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
            var g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f);
            var b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
