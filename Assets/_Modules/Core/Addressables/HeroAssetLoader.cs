// =============================================================================
// HeroAssetLoader — Tier-1 Addressables seam for per-hero assets (WO-545).
// -----------------------------------------------------------------------------
// docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md, Tier-1: heroes currently ALL
// ship in Resources/Heroes (~138 MB always in the build). This helper is the
// drop-in seam so a hero can be pulled per-selection via Addressables instead.
//
// ⚠ HISTORY — WHY THE ORDER IS COMMENTED SO HARD (WO-1187, 2026-09-03):
// this header declared "Addressables-FIRST" from day one while the CODE called
// Resources.Load first. So the hero art could be grouped into Addressables and
// NOTHING would change: the local copy won every resolve, the 100 MB kept shipping,
// and the move would have looked like it simply did not work. The order is now
// correct and is PINNED by Assets/Editor/Regression/HeroRemoteContentRegression.cs.
// Never trust this header again without reading Load<T> — that is the lesson.
//
// CONTRACT (NON-NEGOTIABLE): Addressables-FIRST, Resources-FALLBACK.
//   • Build the per-hero address "Heroes/<slug>" (same scheme for the prefab and
//     the controller — the asset TYPE disambiguates the two locations sharing the
//     address; the loader queries type-filtered).
//   • If that address is REGISTERED in the Addressables catalog, load it and use it.
//   • Otherwise fall back to Resources.Load<T>("Heroes/" + slug). Post-WO-1187 that
//     fallback covers ONLY what deliberately stays local — Heroes/Props/*,
//     Heroes/Emotes/*, Heroes/SC_*.prefab. The hero BODIES (fbx + .fbm + controller)
//     and Heroes/Textures/* now live in Assets/HeroContent/ in REMOTE R2 bundles and
//     have NO local copy: for those, Addressables is the only path that can succeed.
//
// Synchronous surface (WaitForCompletion) so the existing sync call sites keep their
// shape (AtbCombatantSwapper, HeroBodySwapper legacy path, StoryCompanionInjector).
//
// We deliberately check LoadResourceLocationsAsync FIRST rather than blindly calling
// LoadAssetAsync on a possibly-unregistered key: in V1 NO hero address is registered,
// and a blind LoadAssetAsync on a missing key spams a red Addressables error on EVERY
// hero load. The locations probe is silent — so the clean V1 path stays clean.
//
// NOTE on handle lifetime: like Resources.Load (which never unloads), we do NOT release
// the asset handle — the loaded prefab/controller must outlive the instantiated hero.
// A future Tier-2 can add ref-counted release keyed by the spawned instance.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;   // FlowTrace / Guard — §12 instrument the seam (Step hit / Warn fallback)
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DeNelle.Core
{
    /// <summary>
    /// Addressables-first / Resources-fallback loader for per-hero prefab + animator controller.
    /// Drop-in for <c>Resources.Load&lt;T&gt;("Heroes/" + slug)</c>. V1-safe: an unregistered
    /// address silently falls back to the shipping Resources copy.
    /// </summary>
    public static class HeroAssetLoader
    {
        /// <summary>Resources sub-path prefix + Addressable address prefix (shared scheme).</summary>
        public const string HeroAddrPrefix = "Heroes/";

        /// <summary>Load the per-hero body prefab (Addressables-first, Resources-fallback).</summary>
        public static GameObject LoadHeroPrefab(string slug) => Load<GameObject>(slug);

        /// <summary>Load the per-hero animator controller (Addressables-first, Resources-fallback).</summary>
        public static RuntimeAnimatorController LoadHeroController(string slug) => Load<RuntimeAnimatorController>(slug);

        /// <summary>
        /// Build the address "Heroes/&lt;slug&gt;", try Addressables when that address (of type
        /// <typeparamref name="T"/>) is registered, else fall back to Resources.Load. Guarded — a
        /// throw at any step degrades to the Resources fallback so the hero is never left assetless.
        /// </summary>
        private static T Load<T>(string slug) where T : Object
        {
            if (string.IsNullOrEmpty(slug)) return null;

            string address = HeroAddrPrefix + slug;
            T result = null;

            // ── Addressables FIRST (WO-1187) ────────────────────────────────────────
            // ⚠ ORDER IS THE WHOLE POINT OF THIS METHOD. Until WO-1187 this block sat
            // BELOW the Resources.Load call, so the header's "Addressables-FIRST" contract
            // was a lie in code: the local Resources copy always won and the remote bundle
            // was never consulted. That made grouping the heroes into Addressables a
            // NO-OP — the 100 MB kept shipping and the CDN copy was dead weight.
            // Do not reorder these two blocks.
            bool wasRegistered = false;
            Guard.Try("HeroAssets", $"Addressables resolve '{address}' ({typeof(T).Name})", () =>
            {
                wasRegistered = AddressableRegistered<T>(address);
                if (!wasRegistered) return; // un-grouped asset (Props/, Emotes/, SC_*) — Resources below

                // Safe because HeroContentPrewarmer has already DOWNLOADED this hero's bundle on
                // the post-class-select load screen. WaitForCompletion on an UNCACHED remote
                // bundle would stall the main thread for the length of the download, which is
                // exactly why the prewarm gate blocks entry into the world instead.
                var handle = Addressables.LoadAssetAsync<T>(address);
                result = handle.WaitForCompletion();
                // Intentionally NOT released — the asset must outlive the spawned hero (parity with
                // Resources.Load, which never unloads). Tier-2 adds ref-counted release.
                if (result != null)
                    FlowTrace.Step("HeroAssets", $"Addressables HIT '{address}' -> '{result.name}' ({typeof(T).Name}).");
            });
            if (result != null) return result;

            // ── Resources fallback (only what DELIBERATELY stays local: Props/, Emotes/, SC_*) ──
            Guard.Try("HeroAssets", $"Resources.Load {address} ({typeof(T).Name})", () =>
            {
                result = Resources.Load<T>(address);
            });

            // §12 hygiene: separate a clean "never grouped, lives in Resources by design" (Step)
            // from "the catalog HAS this address but it did not resolve" (Warn — a real anomaly,
            // and on a remote group it almost always means the bundle was never pushed to R2).
            if (wasRegistered)
                FlowTrace.Warn("HeroAssets",
                    $"Addressables '{address}' IS registered but resolved null — the bundle is likely missing from " +
                    $"the CDN (never pushed). Fell back to Resources.Load(\"{HeroAddrPrefix}{slug}\") -> " +
                    $"{(result == null ? "ALSO NULL" : result.name)}.");
            else
                FlowTrace.Step("HeroAssets",
                    $"no Addressables entry for '{address}' (expected for the deliberately-local Props/Emotes/SC_ " +
                    $"assets) — using Resources.Load(\"{HeroAddrPrefix}{slug}\").");

            if (result == null)
                FlowTrace.Fail("HeroAssets",
                    $"hero asset '{HeroAddrPrefix}{slug}' ({typeof(T).Name}) not found via Addressables OR Resources — caller falls back.");
            return result;
        }

        /// <summary>
        /// True when the Addressables catalog has at least one location for <paramref name="address"/>
        /// providing type <typeparamref name="T"/>. Silent (no error spam) on the common V1 miss.
        /// Type-filtered so the prefab vs controller locations sharing the same address resolve apart.
        /// </summary>
        private static bool AddressableRegistered<T>(string address) where T : Object
        {
            try
            {
                foreach (var locator in Addressables.ResourceLocators)
                {
                    if (locator.Locate(address, typeof(T), out IList<IResourceLocation> locations) &&
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
