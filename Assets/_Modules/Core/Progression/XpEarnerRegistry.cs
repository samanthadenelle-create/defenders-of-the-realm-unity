// =============================================================================
// XpEarnerRegistry — process-wide map of earner-id -> live IXpEarner.
// -----------------------------------------------------------------------------
// Earners register on enable and unregister on disable. As of WO-993 there is
// exactly ONE impl: HeroProgression (DeNelle.Village). PetProgression was retired
// with the physical pet stack (Echoes are a faucet, not a levelling companion),
// so no pet registers here. The seam itself is KEPT — it is what lets the Village
// ProgressionManager grant XP without referencing another gameplay module, and it
// is the join point for any future earner. The Village ProgressionManager
// drains the per-enemy damage ledger on a kill and resolves each contributor's
// id to its IXpEarner here, so it can grant shared XP across the asmdef seam
// without referencing the other gameplay module. Pure C#, no Unity coupling.
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Core.Progression
{
    /// <summary>Resolves a damage-attribution id to the live <see cref="IXpEarner"/>.</summary>
    public static class XpEarnerRegistry
    {
        private static readonly Dictionary<string, IXpEarner> s_byId =
            new Dictionary<string, IXpEarner>();

        /// <summary>Registers (or replaces) the earner under its <see cref="IXpEarner.EarnerId"/>.</summary>
        public static void Register(IXpEarner earner)
        {
            if (earner == null || string.IsNullOrEmpty(earner.EarnerId)) return;
            s_byId[earner.EarnerId] = earner;
        }

        /// <summary>Removes the earner — only if it is still the registered instance for its id.</summary>
        public static void Unregister(IXpEarner earner)
        {
            if (earner == null || string.IsNullOrEmpty(earner.EarnerId)) return;
            if (s_byId.TryGetValue(earner.EarnerId, out var cur) && ReferenceEquals(cur, earner))
                s_byId.Remove(earner.EarnerId);
        }

        /// <summary>The live earner for <paramref name="id"/>, or null if none is registered.</summary>
        public static IXpEarner TryGet(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return s_byId.TryGetValue(id, out var e) ? e : null;
        }
    }
}
