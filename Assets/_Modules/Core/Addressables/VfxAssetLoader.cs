// =============================================================================
// VfxAssetLoader — Tier-1 Addressables seam for VFX content.
// Sibling of HeroAssetLoader (WO-545) / EnemyAssetLoader; identical contract,
// VFX address space.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS: `Assets/Resources/VFX` is 81.1 MB and Unity FORCE-INCLUDES
// everything under a Resources/ folder in EVERY build, whether or not a single
// effect in it is ever played. Measured composition (verified, do not re-derive):
//   36.1 MB / 22 .tif   +   30.8 MB / 67 .prefab   +   13.3 MB / 44 .png
//   + small .mat/.shadergraph/.fbx.
// The largest files are the SHARED texture pool under `_Shared/Textures/`
// (LargeFlame02.tif 6.6 MB, Explosion.tif 2.9, EnergyEffect.tif 2.8,
// SmokePuff01.tif 2.7, ...) — one pool feeding many effects, which is exactly
// why the grouper files it as ONE shared bundle rather than per-effect
// (see VfxAddressablesGrouper's TOPOLOGY header).
//
// The fix is to move the VFX art into Addressable groups so it is pulled on
// demand — which requires a runtime SEAM the call sites can already be pointed
// at BEFORE the assets physically move. This is that seam.
//
// ── KEY CONVENTION (DECIDED — read this before adding a call site) ───────────
//   The key is the FULL, extension-less, RESOURCES-RELATIVE path, prefix
//   INCLUDED, used VERBATIM as BOTH the Addressable address AND the
//   Resources.Load key. e.g.
//       "VFX/VFXCatalog"        "VFX/HovlVfxCatalog"
//       "VFX/Portal/PortalCircleDarkStar"
//       "VFX/Status/Aura_acceleration"
//   NOT a bare name. Two reasons, both taken from the actual call sites:
//     1. VFX content is NESTED (Aura/ Boss/ Buffs/ Damage/ Death/ Env/ Harvest/
//        Markers/ Portal/ Projectiles/ Status/ UI/ Weapon/ _Shared/). A bare
//        name would need the loader to invent the sub-folder, and two effects in
//        different folders could share a leaf name — an address collision that is
//        an Addressables BUILD ERROR, not a runtime warning.
//     2. Every existing call site ALREADY holds the full key as a literal or a
//        const ("VFX/VFXCatalog", "VFX/Portal/PortalCircleDarkStar", the seven
//        AtbStatusVfx "VFX/Status/*" consts). Passing it through unchanged makes
//        the repoint a pure one-token edit with ZERO key rewriting — the cheapest
//        possible diff and nothing to get wrong.
//   The loader therefore does NOT auto-prefix. A key that does not start with
//   "VFX/" is a mis-shaped call site, and it is flagged Once (§12) rather than
//   silently "fixed" into a different address than the grouper registered.
//   VfxAddressablesGrouper.VfxAddrPrefix MUST equal VfxAddrPrefix here — the
//   grouper addresses assets at EXACTLY the key this loader queries.
//
// CONTRACT (V1-SAFE, NON-NEGOTIABLE): Addressables-FIRST, Resources-FALLBACK.
//   • If the key is REGISTERED in the Addressables catalog (type-filtered), load
//     it and use it.
//   • Otherwise (the V1 default — nothing grouped yet) fall straight back to the
//     EXISTING Resources.Load<T>(key) path.
//
// V1-SAFE because NOTHING under Assets/Resources/VFX is moved, deleted or
// re-imported by the code change that introduces this loader. The Resources copy
// remains the live path until the physical migration is run as a separate
// attended step, so every existing call site behaves EXACTLY as before — this
// only adds a silent probe in front of it. The day the assets move, the
// Addressables branch starts hitting with no further call-site edits.
//
// Synchronous surface (WaitForCompletion) so the existing sync call sites keep
// their shape (VFXManager.EnsureCatalog / EnsureHovlCatalog, HeroArmorRimLight).
//
// We deliberately check LoadResourceLocationsAsync FIRST rather than blindly
// calling LoadAssetAsync on a possibly-unregistered key: pre-migration NO VFX
// address is registered, and a blind LoadAssetAsync on a missing key spams a red
// Addressables error on EVERY call (VFX loads run per-cast). The locations probe
// is silent — so the clean pre-migration path stays clean.
//
// ⚠ WEBGL CAVEAT (inherited from WO-545): WaitForCompletion is not supported on
// WebGL for a bundle that still has to be downloaded — once the VFX assets are
// grouped, the VFX bundle must be warmed async before these sync calls resolve.
//
// NOTE on handle lifetime: like Resources.Load (which never unloads), we do NOT
// release the asset handle — the loaded catalog/prefab must outlive every pooled
// instance spawned from it. A future Tier-2 can add ref-counted release keyed by
// the pool.
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
    /// Addressables-first / Resources-fallback loader for VFX content (effect prefabs and
    /// the VFX catalog ScriptableObjects). Drop-in for <c>Resources.Load&lt;T&gt;("VFX/...")</c>.
    /// V1-safe: an unregistered address silently falls back to the shipping Resources copy.
    /// Keys are FULL Resources-relative paths — see the header's KEY CONVENTION block.
    /// </summary>
    public static class VfxAssetLoader
    {
        /// <summary>Resources sub-path prefix + Addressable address prefix (shared scheme).
        /// MUST match <c>DeNelle.Editor.VfxAddressablesGrouper.VfxAddrPrefix</c>.</summary>
        public const string VfxAddrPrefix = "VFX/";

        /// <summary>
        /// Load a VFX prefab by its FULL Resources-relative key, e.g.
        /// <c>LoadVfxPrefab("VFX/Portal/PortalCircleDarkStar")</c>.
        /// Addressables-first, Resources-fallback. Null when both paths miss (the caller
        /// keeps its existing graceful no-VFX behaviour).
        /// </summary>
        public static GameObject LoadVfxPrefab(string key) => Load<GameObject>(key);

        /// <summary>
        /// Load any VFX asset by its FULL Resources-relative key — catalogs, textures,
        /// materials — e.g. <c>LoadVfxAsset&lt;VFXCatalog&gt;("VFX/VFXCatalog")</c>.
        /// Addressables-first, Resources-fallback.
        /// </summary>
        public static T LoadVfxAsset<T>(string key) where T : Object => Load<T>(key);

        /// <summary>
        /// Try Addressables when <paramref name="key"/> (of type <typeparamref name="T"/>) is
        /// registered, else fall back to Resources.Load on the SAME key. Guarded — a throw at
        /// any step degrades to the Resources fallback so an effect is never left assetless.
        /// </summary>
        private static T Load<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key)) return null;

            // A key that is not Resources-relative from the VFX root cannot match what the
            // grouper registered. Flag it Once rather than auto-prefixing — silently rewriting
            // the key would query a DIFFERENT address than the one that was registered, and the
            // resulting miss would look like a grouping bug instead of a call-site bug.
            if (!key.StartsWith(VfxAddrPrefix, System.StringComparison.Ordinal))
                FlowTrace.Once("VfxAssets", "badkey:" + key,
                    $"VFX key '{key}' does not start with '{VfxAddrPrefix}' — the Addressables probe " +
                    "cannot match the grouper's address for it; only the Resources path can serve it. " +
                    "Pass the FULL Resources-relative key (see VfxAssetLoader KEY CONVENTION).");

            T result = null;

            Guard.Try("VfxAssets", $"Resources.Load {key} ({typeof(T).Name})", () =>
            {
                result = Resources.Load<T>(key);
            });
            if (result != null) return result;

            // ── Addressables-first (only when the address is actually registered) ──
            Guard.Try("VfxAssets", $"Addressables resolve '{key}' ({typeof(T).Name})", () =>
            {
                if (!AddressableRegistered<T>(key)) return; // expected pre-migration — handled by the Step below

                var handle = Addressables.LoadAssetAsync<T>(key);
                result = handle.WaitForCompletion();
                // Intentionally NOT released — the asset must outlive every pooled instance spawned
                // from it (parity with Resources.Load, which never unloads). Tier-2 adds ref-counted
                // release keyed by the VFX pool.
                if (result != null)
                    FlowTrace.Step("VfxAssets", $"Addressables HIT '{key}' -> '{result.name}' ({typeof(T).Name}).");
            });
            if (result != null) return result;

            // ── Resources fallback (the pre-migration path — Assets/Resources/VFX is NOT moved
            //    by the code change that introduces this seam) ──
            // Distinguish the two cases for §12 hygiene: a clean "no address registered yet"
            // (expected, Step) vs an address that WAS registered but failed to resolve (anomaly, Warn).
            bool wasRegistered = false;
            Guard.Try("VfxAssets", $"probe '{key}' registration", () => wasRegistered = AddressableRegistered<T>(key));
            if (wasRegistered)
                FlowTrace.Warn("VfxAssets",
                    $"Addressables '{key}' is registered but resolved null — falling back to Resources.Load(\"{key}\").");
            else
                FlowTrace.Step("VfxAssets",
                    $"no Addressables entry for '{key}' (expected pre-migration) — using Resources.Load(\"{key}\").");

            if (result == null)
                FlowTrace.Fail("VfxAssets",
                    $"VFX asset '{key}' ({typeof(T).Name}) not found via Addressables OR Resources — caller falls back.");
            return result;
        }

        /// <summary>
        /// True when the Addressables catalog has at least one location for <paramref name="address"/>
        /// providing type <typeparamref name="T"/>. Silent (no error spam) on the common
        /// pre-migration miss. Type-filtered so two locations sharing an address resolve apart.
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
