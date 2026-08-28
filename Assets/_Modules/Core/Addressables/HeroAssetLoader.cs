// =============================================================================
// HeroAssetLoader — Tier-1 Addressables seam for per-hero assets (WO-545).
// -----------------------------------------------------------------------------
// docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md, Tier-1: heroes currently ALL
// ship in Resources/Heroes (~138 MB always in the build). This helper is the
// drop-in seam so a hero can be pulled per-selection via Addressables instead.
//
// CONTRACT (V1-SAFE, NON-NEGOTIABLE): Addressables-FIRST, Resources-FALLBACK.
//   • Build the per-hero address "Heroes/<slug>" (same scheme for the prefab and
//     the controller — the asset TYPE disambiguates the two locations sharing the
//     address; the loader queries type-filtered).
//   • If that address is REGISTERED in the Addressables catalog, load it and use it.
//   • Otherwise (the V1 default — no hero address grouped yet) fall straight back to
//     the EXISTING Resources.Load<T>("Heroes/" + slug) path. Nothing in Resources is
//     moved/deleted by this WO, so the fallback is always available and V1 cannot break.
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

            Guard.Try("HeroAssets", $"Resources.Load {address} ({typeof(T).Name})", () =>
            {
                result = Resources.Load<T>(address);
            });
            if (result != null) return result;

            // ── Addressables-first (only when the address is actually registered) ──
            Guard.Try("HeroAssets", $"Addressables resolve '{address}' ({typeof(T).Name})", () =>
            {
                if (!AddressableRegistered<T>(address)) return; // expected in V1 — handled by the Step below

                var handle = Addressables.LoadAssetAsync<T>(address);
                result = handle.WaitForCompletion();
                // Intentionally NOT released — the asset must outlive the spawned hero (parity with
                // Resources.Load, which never unloads). Tier-2 adds ref-counted release.
                if (result != null)
                    FlowTrace.Step("HeroAssets", $"Addressables HIT '{address}' -> '{result.name}' ({typeof(T).Name}).");
            });
            if (result != null) return result;

            // ── Resources fallback (the V1 path — always available, never deleted by WO-545) ──
            // Distinguish the two cases for §12 hygiene: a clean "no address registered yet"
            // (expected, Step) vs an address that WAS registered but failed to resolve (anomaly, Warn).
            bool wasRegistered = false;
            Guard.Try("HeroAssets", $"probe '{address}' registration", () => wasRegistered = AddressableRegistered<T>(address));
            if (wasRegistered)
                FlowTrace.Warn("HeroAssets",
                    $"Addressables '{address}' is registered but resolved null — falling back to Resources.Load(\"{HeroAddrPrefix}{slug}\").");
            else
                FlowTrace.Step("HeroAssets",
                    $"no Addressables entry for '{address}' (expected in V1) — using Resources.Load(\"{HeroAddrPrefix}{slug}\").");

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
