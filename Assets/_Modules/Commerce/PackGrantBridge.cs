// =============================================================================
// PackGrantBridge - the ONE rail-neutral door to the entitlement grant (WO-1282).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Commerce   Namespace: DeNelle.Commerce   (STATIC)
//
// WHY THIS EXISTS. The only local pack mutation in the game is
// PackStoreVM.ApplyPackContents, and PackStoreVM lives in DeNelle.Wallet - an
// assembly that carries `!GOOGLE_PLAY` and is therefore COMPILED OUT of a Google
// Play artifact. A Play-side settlement path cannot name it, and must never grow a
// second copy of it: a duplicated grant is a duplicated economy, and this repo has
// already paid for that once (RewardGrantWriter's "DUPLICATED MECHANISM, DECLARED"
// header). So the grant is reached through a registered delegate instead, exactly
// the way StorefrontRegistry reaches the storefront host.
//
// ⛔ AN UNREGISTERED BRIDGE IS A LEGITIMATE STATE ON A PLAY BUILD, AND IT IS NOT
//    SILENT. Two readings, and every call site names BOTH (CLAUDE.md §12):
//      * "no applier because DeNelle.Wallet is excluded from this artifact"
//        = CORRECT. The Play rail has no local grant yet, so settlement must fail
//        closed and the store must refuse to sell.
//      * "no applier but DeNelle.Wallet IS in this artifact"
//        = DEFECT. PackStoreBootstrap did not run and a paid pack would be lost.
//    Callers must treat a false return as "did not grant", NEVER as "granted".
//
// ⛔ NEVER MAKE THIS FAIL OPEN. Every accessor returns false/unknown when nothing
//    is registered. A convenience "assume granted" default here would hand a
//    charged player nothing while telling the Play order it was settled.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Commerce
{
    /// <summary>
    /// Rail-neutral seam onto the local pack entitlement grant. The owning assembly
    /// (DeNelle.Wallet, via PackStoreBootstrap) registers the real implementation at
    /// BeforeSceneLoad; assemblies that must not name PackStoreVM go through here.
    /// </summary>
    public static class PackGrantBridge
    {
        /// <summary>FlowTrace system tag for every line this seam emits. Matches PackStore's.</summary>
        private const string TraceSystem = "Store";

        /// <summary>Applies a pack SKU's contents and returns whether the entitlement took.</summary>
        private static Func<string, bool> _apply;

        /// <summary>Reports whether the pack SKU is already recorded as owned in the live save.</summary>
        private static Func<string, bool> _isOwned;

        /// <summary>
        /// Installs the real grant path. Called once, at BeforeSceneLoad, by the assembly that owns
        /// the entitlement writer. Idempotent - the last registration wins. Both delegates are
        /// required: an applier with no ownership probe cannot be verified, and an unverifiable
        /// grant is exactly the thing settlement must never confirm.
        /// </summary>
        public static void RegisterApplier(Func<string, bool> applyPackBySku, Func<string, bool> isPackOwned)
        {
            if (applyPackBySku == null || isPackOwned == null)
            {
                FlowTrace.Fail(TraceSystem,
                    "PackGrantBridge.RegisterApplier refused: both applyPackBySku and isPackOwned are " +
                    "required. The bridge stays UNREGISTERED, so settlement will fail closed.");
                return;
            }
            _apply = applyPackBySku;
            _isOwned = isPackOwned;
            FlowTrace.Step(TraceSystem, "PackGrantBridge: local pack grant applier registered.");
        }

        /// <summary>True when a local entitlement writer is compiled into this build AND registered.</summary>
        public static bool HasApplier => _apply != null && _isOwned != null;

        /// <summary>Test/teardown hook. Never called from a player build.</summary>
        public static void ResetForTests()
        {
            _apply = null;
            _isOwned = null;
        }

        /// <summary>
        /// Applies the pack SKU's contents through the ONE canonical entitlement writer.
        /// Returns true only when the writer confirmed the SKU is now owned.
        /// </summary>
        public static bool TryApply(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
            {
                FlowTrace.Fail(TraceSystem, "PackGrantBridge.TryApply refused: empty SKU.");
                return false;
            }
            if (!HasApplier)
            {
                WarnNoApplier("TryApply('" + sku + "')");
                return false;
            }

            bool granted = false;
            Guard.Try(TraceSystem, "apply pack grant '" + sku + "'", () => { granted = _apply(sku); });
            if (!granted)
                FlowTrace.Fail(TraceSystem,
                    "PackGrantBridge.TryApply: entitlement for '" + sku + "' did NOT take. If a payment " +
                    "has already confirmed, the player is CHARGED with NO entitlement.");
            return granted;
        }

        /// <summary>
        /// Ownership probe that distinguishes "not owned" from "cannot tell". A caller that needs to
        /// reason about a crash-interrupted grant MUST use this form: <c>false/false</c> (unknown) is
        /// not the same answer as <c>true/false</c> (known-not-owned).
        /// </summary>
        public static bool TryIsOwned(string sku, out bool owned)
        {
            owned = false;
            if (string.IsNullOrWhiteSpace(sku)) return false;
            if (!HasApplier)
            {
                WarnNoApplier("TryIsOwned('" + sku + "')");
                return false;
            }

            bool resolved = false;
            bool result = false;
            Guard.Try(TraceSystem, "probe pack ownership '" + sku + "'", () =>
            {
                result = _isOwned(sku);
                resolved = true;
            });
            owned = result;
            return resolved;
        }

        private static void WarnNoApplier(string call)
        {
            FlowTrace.Once(TraceSystem, "pack-grant-no-applier",
                "PackGrantBridge." + call + ": no local pack grant applier is registered. " +
                "EXPECTED on a build that excludes DeNelle.Wallet (Google Play) - that artifact has no " +
                "local entitlement writer yet, so settlement MUST fail closed here. On a Seeker/dApp-Store " +
                "or editor build this instead means PackStoreBootstrap did not run, which is a DEFECT: a " +
                "paid pack would be lost.");
        }
    }
}
