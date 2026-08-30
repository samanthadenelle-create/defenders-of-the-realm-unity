// =============================================================================
// StorefrontRegistry - how a non-Wallet assembly gets a handle on the storefront
// GameObject without naming PackStore (WO-1282).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Commerce   Namespace: DeNelle.Commerce   (STATIC)
//
// WHY A RESOLVER AND NOT AN INTERFACE. The one Village consumer
// (MarketplaceInteractor.Start) needs a GameObject, and it found one with
// FindAnyObjectByType<PackStore>(FindObjectsInactive.Include) - INACTIVE included,
// because the store host is disabled in the scene by design. That rules out both
// obvious alternatives:
//   * FindAnyObjectByType<T> constrains T : UnityEngine.Object, so an interface
//     cannot be the search key.
//   * A registry the storefront pushes itself into on Awake/OnEnable is EMPTY for a
//     host that is inactive - Awake does not run on a disabled GameObject. That is
//     the silent-failure shape, and it is the shape the current code deliberately
//     avoids by searching with FindObjectsInactive.Include.
// So Wallet registers a LAZY resolver at BeforeSceneLoad (PackStoreBootstrap) and the
// search still happens at call time, inactive objects included, exactly as before.
//
// ⛔ AN UNSET RESOLVER IS A LEGITIMATE STATE, AND IT IS NOT SILENT.
//    A Google Play artifact excludes DeNelle.Wallet, so nothing registers and
//    ResolveRoot() returns null - correct, because there is no Solana storefront in
//    that build. ResolveRoot FlowTrace.Warn's when it is asked with no resolver, so
//    the ONE case where that would be a defect (a Seeker build where the bootstrap
//    failed to run) says so in the trace instead of looking like "no store in scene".
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Commerce
{
    /// <summary>
    /// Resolves the storefront's scene host for assemblies that must not name the storefront type.
    /// </summary>
    public static class StorefrontRegistry
    {
        /// <summary>FlowTrace system tag for every line this seam emits. Matches PackStore's.</summary>
        private const string TraceSystem = "Store";

        /// <summary>
        /// Lazy resolver installed by the storefront's own bootstrap. Evaluated on every
        /// <see cref="ResolveRoot"/> call - never cached here, because the host is find-or-spawned
        /// and a cached null would outlive the reason it was null.
        /// </summary>
        private static Func<GameObject> _resolver;

        /// <summary>
        /// Installs the storefront resolver. Called once, at BeforeSceneLoad, by the assembly that
        /// owns the storefront. Idempotent - the last registration wins.
        /// </summary>
        public static void RegisterResolver(Func<GameObject> resolver)
        {
            _resolver = resolver;
            FlowTrace.Step(TraceSystem, "StorefrontRegistry: storefront resolver registered.");
        }

        /// <summary>True when a storefront implementation is compiled into this build.</summary>
        public static bool HasStorefront => _resolver != null;

        /// <summary>
        /// The storefront's scene host, or null when this build carries no storefront or none has
        /// been placed/spawned yet. Callers must treat null as ordinary and never as an error.
        /// </summary>
        public static GameObject ResolveRoot()
        {
            if (_resolver == null)
            {
                FlowTrace.Once(TraceSystem, "storefront-no-resolver",
                    "StorefrontRegistry.ResolveRoot: no storefront resolver is registered. EXPECTED on a " +
                    "build that excludes DeNelle.Wallet (Google Play). On a Seeker/dApp-Store build this " +
                    "means PackStoreBootstrap did not run - the Realm Store host cannot be found.");
                return null;
            }

            GameObject root = null;
            Guard.Try(TraceSystem, "resolve storefront root", () => { root = _resolver(); });
            return root;
        }
    }
}
