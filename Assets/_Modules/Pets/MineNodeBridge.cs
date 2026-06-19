// =============================================================================
// MineNodeBridge — a reflection seam that lets a pet (DeNelle.Pets) discover and
// work the Village's MineNode resource nodes WITHOUT a DeNelle.Village asmdef
// reference (Pets references only Core + Data — CLAUDE.md §5/§9).
// -----------------------------------------------------------------------------
// WHY REFLECTION (the codebase's established cross-asmdef pattern):
//   DeNelle.Pets already reaches Village-side systems this exact way —
//   PetAttackVfxBridge (AbilityVfxKit), PetHeroLeash (HeroLocomotion), and the
//   PetDeployer cosmetic resolver (GlimmerCurrencyService) all name-match a
//   Village/Cosmetics type by reflection, cache the members, and best-effort
//   invoke. MineNodeBridge follows the same shape so the pet auto-harvest loop
//   adds NO new asmdef coupling.
//
// WHAT IT REUSES (no parallel economy, no new currency — CLAUDE.md §9):
//   MineNode (DeNelle.Village, WO-142/159) already owns the ONE banking path:
//   TryAutoExtract() banks one extract into GameState (Iron/Wood/Stone/
//   AetherCrystals) on the node's own cooldown — the SAME path Worker.cs and the
//   player's [F] tap use. The pet simply drives that existing API; harvested
//   yield lands in the existing wallet through MineNode.BankYield, untouched.
//   We also honour MineNode's worker-claim seam (SetWorkerClaim / IsClaimedByWorker)
//   so a pet and a dispatched Worker never double-work the same node.
//
// This bridge is a thin, allocation-light wrapper around a MonoBehaviour handle
// (the MineNode instance, held as UnityEngine.Object) plus cached reflection
// members. PetHarvester drives the state machine; this only exposes the node API.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Pets
{
    /// <summary>
    /// Reflection wrapper over one Village <c>MineNode</c>. Exposes the slice of
    /// MineNode's public API the pet auto-harvester needs (position, depleted /
    /// claimed flags, claim toggle, and the existing <c>TryAutoExtract</c> banking
    /// call) without DeNelle.Pets referencing DeNelle.Village. All members are
    /// resolved once and cached statically; every call is null-guarded and
    /// best-effort so a missing / changed MineNode never throws into pet logic.
    /// </summary>
    internal sealed class MineNodeHandle
    {
        // The live MineNode MonoBehaviour, held abstractly. Lets us null-check
        // against Unity's destroyed-object lifetime (`== null` works on Object).
        private readonly UnityEngine.Object _node;
        private readonly Component _component;

        private MineNodeHandle(Component node)
        {
            _component = node;
            _node = node;
        }

        /// <summary>True while the underlying MineNode still exists (not destroyed).</summary>
        public bool IsValid => _node != null && _component != null;

        /// <summary>World position of the node (the harvest destination).</summary>
        public Vector3 Position => IsValid ? _component.transform.position : Vector3.zero;

        /// <summary>Wraps a raw MineNode Component. Null in ⇒ null out.</summary>
        public static MineNodeHandle Wrap(Component node) =>
            node != null ? new MineNodeHandle(node) : null;

        /// <summary>MineNode.IsDepleted — node has run dry (no further yield).</summary>
        public bool IsDepleted
        {
            get
            {
                if (!IsValid || MineNodeBridge.IsDepletedProp == null) return true;
                try { return (bool)MineNodeBridge.IsDepletedProp.GetValue(_component); }
                catch (System.Exception e)
                {
                    FlowTrace.Warn("Pets", $"MineNode IsDepleted read failed: {e.GetType().Name}: {e.Message}");
                    return true;
                }
            }
        }

        /// <summary>MineNode.IsClaimedByWorker — another harvester is already on it.</summary>
        public bool IsClaimed
        {
            get
            {
                if (!IsValid || MineNodeBridge.IsClaimedProp == null) return false;
                try { return (bool)MineNodeBridge.IsClaimedProp.GetValue(_component); }
                catch (System.Exception e)
                {
                    FlowTrace.Warn("Pets", $"MineNode IsClaimed read failed: {e.GetType().Name}: {e.Message}");
                    return false;
                }
            }
        }

        /// <summary>MineNode.SetWorkerClaim — claim / release so a Worker and this pet
        /// never double-work one node (idempotent on the node side).</summary>
        public void SetClaim(bool claimed)
        {
            if (!IsValid || MineNodeBridge.SetClaimMethod == null) return;
            try { MineNodeBridge.SetClaimMethod.Invoke(_component, new object[] { claimed }); }
            catch { /* best-effort */ }
        }

        /// <summary>
        /// Drives MineNode.TryAutoExtract() — the EXISTING extract+bank path. Banks
        /// one extract into GameState (the node's own currency) if the node is ready
        /// (not depleted, cooldown elapsed) and returns the amount banked, else 0.
        /// This is the single deposit path; the pet adds no banking of its own.
        /// </summary>
        public int TryAutoExtract()
        {
            if (!IsValid || MineNodeBridge.TryAutoExtractMethod == null) return 0;
            try
            {
                object r = MineNodeBridge.TryAutoExtractMethod.Invoke(_component, null);
                return r is int i ? i : 0;
            }
            catch { return 0; }
        }

        /// <summary>Reference-equality on the underlying node (for claim bookkeeping).</summary>
        public bool Is(Component other) => ReferenceEquals(_component, other);
    }

    /// <summary>
    /// Static reflection cache + node discovery for <see cref="MineNodeHandle"/>.
    /// Resolves the MineNode <see cref="Type"/> and its public members once, then
    /// finds nearby live nodes via <c>FindObjectsOfType</c> filtered by distance.
    /// (MineNode exposes no central registry, so a throttled scene query is the
    /// reuse-without-new-infra path; PetHarvester throttles the scan so this never
    /// runs per-frame.)
    /// </summary>
    internal static class MineNodeBridge
    {
        private static bool s_resolved;
        private static Type s_mineNodeType;

        internal static PropertyInfo IsDepletedProp;
        internal static PropertyInfo IsClaimedProp;
        internal static MethodInfo SetClaimMethod;
        internal static MethodInfo TryAutoExtractMethod;

        /// <summary>True once the MineNode type + its members were found — i.e. the
        /// Village assembly is present and the harvest API is reachable.</summary>
        internal static bool Available
        {
            get { Resolve(); return s_mineNodeType != null && TryAutoExtractMethod != null; }
        }

        private static void Resolve()
        {
            if (s_resolved) return;
            s_resolved = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("DeNelle.Village.MineNode", false);
                if (t == null) continue;
                s_mineNodeType = t;
                IsDepletedProp       = t.GetProperty("IsDepleted",        BindingFlags.Public | BindingFlags.Instance);
                IsClaimedProp        = t.GetProperty("IsClaimedByWorker", BindingFlags.Public | BindingFlags.Instance);
                SetClaimMethod       = t.GetMethod("SetWorkerClaim",      BindingFlags.Public | BindingFlags.Instance);
                TryAutoExtractMethod = t.GetMethod("TryAutoExtract",      BindingFlags.Public | BindingFlags.Instance);
                break;
            }
        }

        // Reusable scratch list so the periodic scan doesn't churn GC.
        private static readonly List<MineNodeHandle> s_scratch = new List<MineNodeHandle>(32);

        /// <summary>
        /// Returns the nearest live, non-depleted, unclaimed MineNode within
        /// <paramref name="radius"/> of <paramref name="from"/>, or null. Discovery
        /// is a (throttled-by-caller) <c>FindObjectsOfType(MineNode)</c> filtered by
        /// distance — no per-frame allocation beyond the cached scratch list.
        /// </summary>
        internal static MineNodeHandle FindNearest(Vector3 from, float radius)
        {
            Resolve();
            if (s_mineNodeType == null) return null;

            UnityEngine.Object[] all;
            try { all = UnityEngine.Object.FindObjectsOfType(s_mineNodeType); }
            catch { return null; }
            if (all == null || all.Length == 0) return null;

            float bestSqr = radius * radius;
            MineNodeHandle best = null;
            for (int i = 0; i < all.Length; i++)
            {
                var comp = all[i] as Component;
                if (comp == null) continue;
                var handle = MineNodeHandle.Wrap(comp);
                if (handle == null || !handle.IsValid) continue;
                if (handle.IsDepleted || handle.IsClaimed) continue;

                float sqr = (comp.transform.position - from).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = handle;
                }
            }
            return best;
        }
    }
}
