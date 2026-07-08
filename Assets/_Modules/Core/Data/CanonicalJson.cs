// =============================================================================
// CanonicalJson — single WebGL-safe loader for the canonical JSON catalogs.
// -----------------------------------------------------------------------------
// WebGL has NO filesystem, so File.ReadAllText(Application.streamingAssetsPath)
// THROWS in a browser build — which is why the abilities/gear/etc. catalogs came
// up empty in WebGL ("the build loads but combat won't play": no abilities ->
// can't cast, no gear -> can't equip).
//
// Every catalog routes its read through here. It loads Resources.Load<TextAsset>
// FIRST (synchronous on EVERY platform INCLUDING WebGL) and falls back to a
// desktop StreamingAssets File.ReadAllText only when a Resources copy is absent.
//
// The canonical JSON therefore lives in BOTH:
//   - Assets/Resources/Data/Canonical/*.json    (WebGL-safe copy, Resources.Load)
//   - Assets/StreamingAssets/Data/Canonical/*.json (desktop fallback + source)
// Keep them in sync; Resources wins at load time. Small text files are exactly
// what Resources is good for — the old "no Resources.Load" rule targeted large
// assets (models/textures), not a few KB of catalog JSON.
// =============================================================================

namespace DeNelle.Core
{
    /// <summary>WebGL-safe reader for canonical catalog JSON (Resources first, StreamingAssets fallback).
    ///
    /// Source-agnostic seam (Tier-0 of docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md): the actual
    /// read is delegated to a swappable <see cref="ICatalogSource"/> (<see cref="Source"/>), which
    /// DEFAULTS to <see cref="LocalJsonCatalogSource"/> — exactly the original local-JSON behavior
    /// (Resources first, StreamingAssets fallback). A future remote/DB source is a one-line swap:
    ///   <c>CanonicalJson.Source = new MyRemoteCatalogSource();</c>
    /// No call site changes — every caller still calls <see cref="Read"/> with the same signature.</summary>
    public static class CanonicalJson
    {
        /// <summary>The active catalog source. Defaults to local JSON (Resources first,
        /// StreamingAssets fallback). Assign a different <see cref="ICatalogSource"/> to back the
        /// same catalogs with a remote/DB source without touching any call site.</summary>
        public static ICatalogSource Source { get; set; } = new LocalJsonCatalogSource();

        /// <summary>Reads canonical JSON text. <paramref name="relativePath"/> is the
        /// StreamingAssets-relative path, e.g. "Data/Canonical/abilities.json".
        /// Returns null if the active <see cref="Source"/> cannot resolve it.</summary>
        public static string Read(string relativePath)
        {
            // Defensive: a caller could null out Source; fall back to a fresh local source
            // so catalog loads never silently break (no silent failure, §12).
            var src = Source;
            if (src == null)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("CanonJson",
                    $"Source was null — rebuilt LocalJsonCatalogSource for '{relativePath}'.");
                src = Source = new LocalJsonCatalogSource();
            }
            var text = src.Read(relativePath);
            if (string.IsNullOrEmpty(text))
                DeNelle.Core.Diagnostics.FlowTrace.Warn("CanonJson",
                    $"Read('{relativePath}') via {src.GetType().Name} returned EMPTY (no Resources dual-copy + no StreamingAssets file).");
            else
                DeNelle.Core.Diagnostics.FlowTrace.Step("CanonJson",
                    $"Read('{relativePath}') via {src.GetType().Name} -> {text.Length} chars.");
            return text;
        }
    }
}
