// =============================================================================
// HeroTextureLoader — Tier-1 Addressables seam for the per-hero BASECOLOR / normal
// textures that the runtime binds by explicit path (WO-545, sibling of
// HeroAssetLoader).
// -----------------------------------------------------------------------------
// PROBLEM this closes: HeroAssetLoader routes the hero FBX + controller through
// Addressables, but the RENDERED look comes from a separate set of atlases in the
// plain folder Resources/Heroes/Textures/* that several systems load DIRECTLY via
// Resources.Load<Texture2D>("Heroes/Textures/<name>") and PAINT onto the body:
//   • HeroBodySwapper.ApplyExtractedTexture  (the playable hero — Knight basecolor+normal)
//   • StoryCompanionInjector.BindClassDiffuse (roster companions)
//   • TripoMaterialFixer                      (ATB hero + enemies, fallback atlas)
// If Heroes/Textures leaves Resources (so the ~84 MB stops shipping in WebGL.data)
// those bare Resources.Load calls return null → the hero renders flat/grey. This
// loader is the drop-in seam so those textures load from the Addressable bundle
// once grouped, and still fall back to Resources while they remain there.
//
// CONTRACT (identical shape to HeroAssetLoader, V1-SAFE): Addressables-FIRST,
// Resources-FALLBACK. The single argument is the Resources-relative path with NO
// extension (e.g. "Heroes/Textures/KnightArmored_basecolor"); it is used verbatim
// as BOTH the Addressable address AND the Resources.Load key (HeroAddressablesGrouper
// registers each texture at that exact address). Any path that is not registered
// (e.g. the enemy "Enemies/OrcTex/*" atlases, which this WO does NOT move) silently
// falls back to Resources.Load — so enemies and un-migrated content are unaffected.
//
// Synchronous surface (WaitForCompletion) so the existing sync call sites keep their
// shape. ⚠ WEBGL CAVEAT (see WO-545 RESULT): WaitForCompletion is NOT supported on
// WebGL for a bundle that still has to be downloaded — the hero's bundle must be
// warmed async (once resident, sync resolves) OR the call sites converted to async.
// This seam is correct for Editor/Standalone today; the WebGL sync-vs-async decision
// is called out in the WO result and gated on a build check.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;   // FlowTrace / Guard — §12 instrument the seam
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DeNelle.Core
{
    /// <summary>
    /// Addressables-first / Resources-fallback loader for a hero atlas texture.
    /// Drop-in for <c>Resources.Load&lt;Texture2D&gt;(path)</c> where <c>path</c> is the
    /// Resources-relative, extension-less key (e.g. "Heroes/Textures/KnightArmored_basecolor").
    /// V1-safe: an unregistered address silently falls back to the shipping Resources copy.
    /// </summary>
    public static class HeroTextureLoader
    {
        /// <summary>
        /// Load a hero atlas texture at <paramref name="resourcesRelativePath"/> (also the
        /// Addressable address). Addressables when that address is registered, else Resources.Load.
        /// Guarded — a throw at any step degrades to the Resources fallback so the caller never
        /// gets a hard exception; a total miss returns null (callers already handle a null atlas).
        /// </summary>
        /// <param name="optional">True when the caller treats a total miss as an EXPECTED,
        /// intentional state (e.g. the pet basecolor PNGs purged for size in 2774fb50 —
        /// the tint/extracted-material fallback is the design, not a failure). A miss then
        /// logs FlowTrace.Step instead of Fail, so it never lands in the break-log.</param>
        public static Texture2D Load(string resourcesRelativePath, bool optional = false)
        {
            if (string.IsNullOrEmpty(resourcesRelativePath)) return null;

            string address = resourcesRelativePath;
            Texture2D result = null;

            // ── Addressables FIRST (WO-1187) ────────────────────────────────────────
            // ⚠ Same inverted-order bug as HeroAssetLoader carried: this block used to sit
            // BELOW the Resources.Load call, so a grouped atlas was never consulted and the
            // 44 MB of Heroes/Textures kept shipping locally. Do not reorder.
            bool wasRegistered = false;
            Guard.Try("HeroAssets", $"Addressables resolve texture '{address}'", () =>
            {
                wasRegistered = AddressableRegistered(address);
                if (!wasRegistered) return; // non-hero / deliberately-local path — Resources below

                var handle = Addressables.LoadAssetAsync<Texture2D>(address);
                result = handle.WaitForCompletion();
                // Intentionally NOT released — parity with Resources.Load (never unloads); the atlas
                // must outlive the material it is painted onto. Tier-2 adds ref-counted release.
                if (result != null)
                    FlowTrace.Step("HeroAssets", $"Addressables HIT texture '{address}' -> '{result.name}'.");
            });
            if (result != null) return result;

            // ── Resources fallback (only for atlases that deliberately stay local) ──
            Guard.Try("HeroAssets", $"Resources.Load texture {address}", () =>
            {
                result = Resources.Load<Texture2D>(address);
            });

            if (wasRegistered)
                FlowTrace.Warn("HeroAssets",
                    $"Addressables texture '{address}' IS registered but resolved null — the bundle is likely " +
                    $"missing from the CDN (never pushed). Fell back to Resources.Load -> " +
                    $"{(result == null ? "ALSO NULL" : result.name)}.");
            else
                FlowTrace.Step("HeroAssets",
                    $"no Addressables entry for texture '{address}' (expected on a non-hero path) — using Resources.Load.");

            if (result == null)
            {
                if (optional)
                    // Owner F8 2026-07-02 (flame-pup): the pet basecolor PNGs were PURGED for size
                    // (2774fb50; flame-pup.png was a 16.4MB LFS asset) and the pets render from their
                    // extracted .fbm materials — a miss here is the intended state, not a break.
                    FlowTrace.Step("HeroAssets",
                        $"optional texture '{address}' absent (purged-for-size asset) — caller's tint fallback is intentional.");
                else
                    FlowTrace.Fail("HeroAssets",
                        $"hero texture '{address}' not found via Addressables OR Resources — caller falls back to tint.");
            }
            return result;
        }

        /// <summary>
        /// True when the Addressables catalog has at least one Texture2D location for
        /// <paramref name="address"/>. Silent (no error spam) on the common miss.
        /// </summary>
        private static bool AddressableRegistered(string address)
        {
            try
            {
                foreach (var locator in Addressables.ResourceLocators)
                {
                    if (locator.Locate(address, typeof(Texture2D), out IList<IResourceLocation> locations) &&
                        locations != null && locations.Count > 0)
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }
    }
}
