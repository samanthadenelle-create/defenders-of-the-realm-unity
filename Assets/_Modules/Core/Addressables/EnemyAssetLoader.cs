// =============================================================================
// EnemyAssetLoader — Tier-1 Addressables seam for per-enemy assets.
// Sibling of HeroAssetLoader (WO-545); identical contract, enemy address space.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS: `Assets/Resources/Enemies` is ~539 MB and Unity FORCE-INCLUDES
// everything under a Resources/ folder in EVERY build, whether or not a single
// enemy in it is ever spawned. That is the largest single line item in the player
// payload (and it lands whole in WebGL.data / the APK). The fix is to move the
// enemy art into an Addressable group so it is pulled per-encounter — which
// requires a runtime SEAM the call sites can already be pointed at BEFORE the
// assets physically move. This is that seam.
//
// CONTRACT (V1-SAFE, NON-NEGOTIABLE): Addressables-FIRST, Resources-FALLBACK.
//   • The address is the extension-less, Resources-relative key used VERBATIM as
//     BOTH the Addressable address AND the Resources.Load key — e.g.
//     "Enemies/Skeleton_Warrior", "Enemies/OrcHumanoid", "Enemies/Boss_Dragon".
//     Do NOT invent a second address scheme; the grouper registers these exact
//     strings (same rule as HeroAssetLoader / HeroTextureLoader).
//     The asset TYPE disambiguates the prefab vs the controller when two locations
//     share an address — the loader queries type-filtered.
//   • If that address is REGISTERED in the Addressables catalog, load it and use it.
//   • Otherwise (the V1 default — no enemy address grouped yet) fall straight back to
//     the EXISTING Resources.Load<T>("Enemies/...") path.
//
// V1-SAFE because NOTHING under Assets/Resources/Enemies is moved, deleted or
// re-imported by the code change that introduces this loader. The Resources copy
// remains the live path until the physical migration is done as a separate attended
// step, so every existing call site behaves EXACTLY as before — this only adds a
// probe in front of it. The day the assets move, the Addressables branch starts
// hitting with no further call-site edits.
//
// Synchronous surface (WaitForCompletion) so the existing sync call sites keep their
// shape (AtbCombatantSwapper, EnemyAnimatorFactory, WaveManager, EnemyOutpostBuilder).
//
// We deliberately check LoadResourceLocationsAsync FIRST rather than blindly calling
// LoadAssetAsync on a possibly-unregistered key: in V1 NO enemy address is registered,
// and a blind LoadAssetAsync on a missing key spams a red Addressables error on EVERY
// enemy spawn (a wave spawns dozens). The locations probe is silent — so the clean V1
// path stays clean.
//
// ⚠ WEBGL CAVEAT (inherited from WO-545): WaitForCompletion is not supported on WebGL
// for a bundle that still has to be downloaded — once the enemy assets are grouped,
// the encounter's bundle must be warmed async before these sync calls resolve.
//
// NOTE on handle lifetime: like Resources.Load (which never unloads), we do NOT release
// the asset handle — the loaded prefab/controller must outlive the instantiated enemy.
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
    /// Addressables-first / Resources-fallback loader for per-enemy prefabs, animator
    /// controllers and prefab-borne components. Drop-in for
    /// <c>Resources.Load&lt;T&gt;("Enemies/" + key)</c>. V1-safe: an unregistered address
    /// silently falls back to the shipping Resources copy.
    /// </summary>
    public static class EnemyAssetLoader
    {
        /// <summary>Resources sub-path prefix + Addressable address prefix (shared scheme).</summary>
        public const string EnemyAddrPrefix = "Enemies/";

        /// <summary>
        /// Load an enemy body prefab by slug (Addressables-first, Resources-fallback).
        /// <paramref name="slug"/> is the bare file name, e.g. "Skeleton_Warrior".
        /// </summary>
        public static GameObject LoadEnemyPrefab(string slug) => LoadPrefixed<GameObject>(slug);

        /// <summary>
        /// Load a shared enemy animator controller by name (Addressables-first, Resources-fallback).
        /// <paramref name="name"/> is the bare controller name, e.g. "OrcHumanoid".
        /// </summary>
        public static RuntimeAnimatorController LoadEnemyController(string name)
            => LoadPrefixed<RuntimeAnimatorController>(name);

        /// <summary>
        /// Generic escape hatch for enemy assets addressed by a FULL Resources-relative key
        /// (prefix included), e.g. <c>LoadEnemyAsset&lt;DragonBoss&gt;("Enemies/Boss_Dragon")</c>.
        /// Use this for prefab-borne component types (DragonBoss etc.) and any enemy key that
        /// is not a bare slug under the prefix.
        /// </summary>
        public static T LoadEnemyAsset<T>(string key) where T : Object => Load<T>(key);

        /// <summary>Prefix a bare slug/name with <see cref="EnemyAddrPrefix"/>, then load.</summary>
        private static T LoadPrefixed<T>(string slug) where T : Object
        {
            if (string.IsNullOrEmpty(slug)) return null;
            return Load<T>(EnemyAddrPrefix + slug);
        }

        /// <summary>
        /// Try Addressables when <paramref name="address"/> (of type <typeparamref name="T"/>) is
        /// registered, else fall back to Resources.Load on the SAME key. Guarded — a throw at any
        /// step degrades to the Resources fallback so the enemy is never left assetless.
        /// </summary>
        private static T Load<T>(string address) where T : Object
        {
            if (string.IsNullOrEmpty(address)) return null;

            T result = null;

            // ── Addressables-first (only when the address is actually registered) ──
            Guard.Try("EnemyAssets", $"Addressables resolve '{address}' ({typeof(T).Name})", () =>
            {
                if (!AddressableRegistered<T>(address)) return; // expected in V1 — handled by the Step below

                var handle = Addressables.LoadAssetAsync<T>(address);
                result = handle.WaitForCompletion();
                // Intentionally NOT released — the asset must outlive the spawned enemy (parity with
                // Resources.Load, which never unloads). Tier-2 adds ref-counted release.
                if (result != null)
                    FlowTrace.Step("EnemyAssets", $"Addressables HIT '{address}' -> '{result.name}' ({typeof(T).Name}).");
            });
            if (result != null) return result;

            // ── Resources fallback (the V1 path — Assets/Resources/Enemies is NOT moved by this WO) ──
            // Distinguish the two cases for §12 hygiene: a clean "no address registered yet"
            // (expected, Step) vs an address that WAS registered but failed to resolve (anomaly, Warn).
            bool wasRegistered = false;
            Guard.Try("EnemyAssets", $"probe '{address}' registration", () => wasRegistered = AddressableRegistered<T>(address));
            if (wasRegistered)
                FlowTrace.Warn("EnemyAssets",
                    $"Addressables '{address}' is registered but resolved null — falling back to Resources.Load(\"{address}\").");
            else
                FlowTrace.Step("EnemyAssets",
                    $"no Addressables entry for '{address}' (expected pre-migration) — using Resources.Load(\"{address}\").");

            Guard.Try("EnemyAssets", $"Resources.Load {address} ({typeof(T).Name})", () =>
            {
                result = Resources.Load<T>(address);
            });
            if (result == null)
                FlowTrace.Fail("EnemyAssets",
                    $"enemy asset '{address}' ({typeof(T).Name}) not found via Addressables OR Resources — caller falls back.");
            return result;
        }

        /// <summary>
        /// True when the Addressables catalog has at least one location for <paramref name="address"/>
        /// providing type <typeparamref name="T"/>. Silent (no error spam) on the common V1 miss.
        /// Type-filtered so the prefab vs controller locations sharing the same address resolve apart.
        /// </summary>
        private static bool AddressableRegistered<T>(string address) where T : Object
        {
            AsyncOperationHandle<IList<IResourceLocation>> locHandle = default;
            bool found = false;
            try
            {
                locHandle = Addressables.LoadResourceLocationsAsync(address, typeof(T));
                IList<IResourceLocation> locs = locHandle.WaitForCompletion();
                found = locs != null && locs.Count > 0;
            }
            catch
            {
                found = false; // no catalog / not initialised / bad key — treat as unregistered
            }
            finally
            {
                if (locHandle.IsValid()) Addressables.Release(locHandle);
            }
            return found;
        }
    }
}
