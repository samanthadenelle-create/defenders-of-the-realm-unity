// =============================================================================
// CoreServices — tiny cross-assembly service registry (introduced by WO-43).
// -----------------------------------------------------------------------------
// WO-43 names this type as a dependency of "WO-41 (CoreServices registry)".
// WO-41 was never landed — there is no CoreServices.cs in the repo and the
// HUD / Audio modules use their own singleton + [RuntimeInitializeOnLoadMethod]
// bootstraps, NOT a central registry. Rather than block WO-43 on a missing
// dependency, this file introduces a MINIMAL registry that exposes only the
// one slot WO-43 actually needs: the Jupiter swap service.
//
// Pattern: a static registry living in DeNelle.Core (which every module already
// references) lets a module register an implementation of a Core-defined
// interface so other modules can reach it WITHOUT an assembly reference to the
// implementing module. The Jupiter slot follows exactly that pattern — the
// concrete JupiterSwapService lives in the non-autoReferenced DeNelle.Web3
// asmdef, registers itself here on Awake, and callers reach it through this
// Core-side surface.
//
// When/if a fuller WO-41 lands, additional slots (Hud / Audio / …) can be added
// here following the same Register/Unregister shape.
// =============================================================================

using DeNelle.Core.Web3;
using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>
    /// A minimal static service registry for cross-assembly access to a small
    /// set of game-wide services. Slots are populated at runtime by the
    /// implementing module and consumed through the interface, so consumers
    /// never need an assembly reference to the implementor.
    /// </summary>
    public static class CoreServices
    {
        // ── Jupiter swap service (WO-43) ─────────────────────────────────────
        // Populated by JupiterSwapService (DeNelle.Web3) on Awake. Null until a
        // JupiterSwapService exists in the loaded scene; callers MUST null-check
        // (e.g. CoreServices.Jupiter?.OpenSwapPanel()).
        /// <summary>
        /// The active Jupiter swap service, or null when no swap host is present
        /// in the loaded scenes. Always null-check before use.
        /// </summary>
        public static IJupiterService Jupiter { get; private set; }

        /// <summary>Registers the Jupiter swap service. Called by JupiterSwapService.Awake.</summary>
        public static void RegisterJupiter(IJupiterService svc)
        {
            if (Jupiter != null && Jupiter != svc)
                Debug.LogWarning("[CoreServices] Replacing existing IJupiterService registration.");
            Jupiter = svc;
        }

        /// <summary>Unregisters the Jupiter swap service. Called by JupiterSwapService.OnDestroy.</summary>
        public static void UnregisterJupiter(IJupiterService svc)
        {
            if (Jupiter == svc) Jupiter = null;
        }
    }
}
