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

using System.IO;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>WebGL-safe reader for canonical catalog JSON (Resources first, StreamingAssets fallback).</summary>
    public static class CanonicalJson
    {
        /// <summary>Reads canonical JSON text. <paramref name="relativePath"/> is the
        /// StreamingAssets-relative path, e.g. "Data/Canonical/abilities.json".
        /// Returns null if neither a Resources copy nor a StreamingAssets file exists.</summary>
        public static string Read(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            // 1) Resources.Load<TextAsset> — works on ALL platforms incl. WebGL.
            //    Resources paths omit the file extension.
            string resPath = relativePath.EndsWith(".json")
                ? relativePath.Substring(0, relativePath.Length - 5)
                : relativePath;
            var ta = Resources.Load<TextAsset>(resPath);
            if (ta != null && !string.IsNullOrEmpty(ta.text))
                return ta.text;

            // 2) Desktop / Editor fallback — real filesystem under StreamingAssets.
            //    On WebGL this is never needed (the Resources copy is the source of
            //    truth there); the try/catch keeps it safe even if it is reached.
            try
            {
                string full = Path.Combine(Application.streamingAssetsPath, relativePath);
                if (File.Exists(full))
                    return File.ReadAllText(full);
            }
            catch (System.Exception ex)
            {
                // §12 TGVRU: was an EMPTY catch. On WebGL this is the expected no-filesystem
                // path (Resources is the only valid source there) — so it is a Warn, not a Fail.
                // But a REAL desktop read failure (locked/permission/corrupt file) used to be
                // swallowed here, producing the "catalog empty" class silently. Now it reports.
                FlowTrace.Warn("Catalog", $"StreamingAssets read of '{relativePath}' failed (expected on WebGL; a real desktop failure here = an empty catalog). {ex.GetType().Name}: {ex.Message}");
            }

            return null;
        }
    }
}
