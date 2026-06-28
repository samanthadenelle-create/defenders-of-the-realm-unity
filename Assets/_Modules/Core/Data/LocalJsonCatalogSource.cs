// =============================================================================
// LocalJsonCatalogSource — the default ICatalogSource (local on-disk JSON).
// -----------------------------------------------------------------------------
// This is BYTE-IDENTICAL in behavior to the original CanonicalJson.Read: it
// loads Resources.Load<TextAsset> FIRST (synchronous on EVERY platform INCLUDING
// WebGL — WebGL has no filesystem) and falls back to a desktop StreamingAssets
// File.ReadAllText only when a Resources copy is absent. Resources wins.
//
// The canonical JSON therefore lives in BOTH:
//   - Assets/Resources/Data/Canonical/*.json    (WebGL-safe copy, Resources.Load)
//   - Assets/StreamingAssets/Data/Canonical/*.json (desktop fallback + source)
// Keep them in sync; Resources wins at load time.
// =============================================================================

using System.IO;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>Default <see cref="ICatalogSource"/>: local JSON, Resources first,
    /// StreamingAssets fallback. Identical precedence/behavior to the original
    /// CanonicalJson loader.</summary>
    public sealed class LocalJsonCatalogSource : ICatalogSource
    {
        /// <inheritdoc/>
        public string Read(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            // 1) Resources.Load<TextAsset> — works on ALL platforms incl. WebGL.
            //    Resources paths omit the file extension.
            string resPath = relativePath.EndsWith(".json")
                ? relativePath.Substring(0, relativePath.Length - 5)
                : relativePath;
            var ta = Resources.Load<TextAsset>(resPath);
            if (ta != null && !string.IsNullOrEmpty(ta.text))
            {
                FlowTrace.Step("Catalog", $"resolve '{relativePath}' <- Resources ({ta.text.Length} chars)");
                return ta.text;
            }

            // 2) Desktop / Editor fallback — real filesystem under StreamingAssets.
            //    On WebGL this is never needed (the Resources copy is the source of
            //    truth there); Guard keeps it safe even if it is reached and reports
            //    any real desktop read failure (locked/permission/corrupt file)
            //    instead of silently producing an empty catalog (§12 no-silent-failure).
            string text = Guard.Try("Catalog", $"StreamingAssets read of '{relativePath}'", () =>
            {
                string full = Path.Combine(Application.streamingAssetsPath, relativePath);
                return File.Exists(full) ? File.ReadAllText(full) : null;
            }, fallback: null);

            if (!string.IsNullOrEmpty(text))
            {
                FlowTrace.Step("Catalog", $"resolve '{relativePath}' <- StreamingAssets ({text.Length} chars)");
                return text;
            }

            FlowTrace.Warn("Catalog", $"resolve '{relativePath}' FAILED (no Resources copy, no StreamingAssets file)");
            return null;
        }
    }
}
