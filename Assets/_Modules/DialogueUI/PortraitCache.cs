// =============================================================================
// PortraitCache — a lazily-built, persistable collection of speaker portraits.
// -----------------------------------------------------------------------------
// Dialogue speaker portraits live at Resources/HeroPortraits/<Name>, but the art
// is imported as plain Textures (not Sprites), so Resources.Load<Sprite> returns
// null — we wrap the Texture2D in a runtime Sprite. Building a Sprite per line
// would churn allocations, so this is the shared registry: build once on first
// request, store it, and hand back the cached instance on every subsequent call
// (Sprite.Create is only invoked on a miss).
//
// The collection persists for the lifetime of the play session (static), and is
// shared by EVERY dialogue speaker — the companion today, and vendors / NPCs /
// lore once those route through the dialogue spine (the "dialogue is the
// interaction layer" decision). Misses are cached too, so a speaker with no
// portrait costs exactly one failed lookup, not one per line.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.DialogueUI
{
    /// <summary>
    /// Process-lifetime registry of speaker portrait <see cref="Sprite"/>s, built
    /// on demand from Resources and reused across all dialogue.
    /// </summary>
    public static class PortraitCache
    {
        private static readonly Dictionary<string, Sprite> _sprites = new();

        /// <summary>True if a portrait exists (and is now cached) for this path.</summary>
        public static bool Has(string resourcePath) => Get(resourcePath) != null;

        /// <summary>
        /// Returns the portrait Sprite for a Resources path (e.g.
        /// "HeroPortraits/Sylas"), building + caching it on first request. Returns
        /// null (cached) when no art exists at that path.
        /// </summary>
        public static Sprite Get(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                FlowTrace.Warn("Portrait", "Get called with <null/empty> path -> null");
                return null;
            }
            // Cache hit is the hot path (once per dialogue line) — guard the string-build.
            if (_sprites.TryGetValue(resourcePath, out var cached))
            {
                if (FlowTrace.Enabled)
                    FlowTrace.Step("Portrait", $"cache hit '{resourcePath}' -> {(cached != null ? "sprite" : "<null cached>")}");
                return cached;
            }

            FlowTrace.Step("Portrait", $"cache MISS '{resourcePath}' -> Build");
            Sprite sprite = Build(resourcePath);
            _sprites[resourcePath] = sprite;   // cache nulls too — one lookup per missing speaker
            if (sprite == null)
                // The bug the trace must prove: Resources/HeroPortraits folder is ABSENT, so
                // BOTH the Sprite and the Texture2D loads return null and this caches a null
                // silently (the speaker renders no portrait, no error). Warn, don't fix.
                FlowTrace.Warn("Portrait", $"NO art resolved for '{resourcePath}' -> caching null " +
                    "(folder likely absent, e.g. Resources/HeroPortraits/* — speaker shows no portrait)");
            return sprite;
        }

        // Load as a Sprite directly when possible; fall back to wrapping a Texture2D
        // (the portraits import as Default textures) in a runtime Sprite.
        private static Sprite Build(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                FlowTrace.Step("Portrait", $"Build '{path}' -> loaded as Sprite");
                return sprite;
            }

            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                FlowTrace.Warn("Portrait", $"Build '{path}' -> neither Sprite nor Texture2D at path (missing art)");
                return null;
            }

            FlowTrace.Step("Portrait", $"Build '{path}' -> wrapped Texture2D {tex.width}x{tex.height} in runtime Sprite");
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f));
        }
    }
}
