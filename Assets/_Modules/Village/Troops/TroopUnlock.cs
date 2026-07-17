// =============================================================================
// TroopUnlock — the SINGLE unlock-gate authority for the Barracks troop ladder
// (WO-733). Every train entry point (the training panel, the Yarn <<StartTraining>>
// verb, TroopDialogueCommands.Train) asks THESE queries whether a troop is
// trainable — no copy-pasted magic tier numbers anywhere else.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The rule (BINDING, WO-733): a troop is trainable when its authored
// TroopDef.UnlockBarracksTier is <= the player's EFFECTIVE Barracks tier, where
// the effective tier is ModifierService.TierOf("barracks") FLOORED to 1 (a barracks
// that exists but was never explicitly upgraded still trains the day-one Footman +
// Archer). Presentation NEVER invents this — it only projects IsTrainable /
// LockedReason. Cap / wounded / afford checks apply AFTER unlock passes.
// =============================================================================

using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// The one place the Barracks-tier troop gate is decided (WO-733). All train
    /// paths call <see cref="IsTrainable"/>; the UI calls <see cref="LockedReason"/>
    /// for the "why locked" copy. No other code compares a troop's unlock tier.
    /// </summary>
    public static class TroopUnlock
    {
        /// <summary>
        /// The player's effective Barracks tier for unlock math: the persisted tier
        /// (<see cref="ModifierService.TierOf"/>) floored to 1. A barracks that exists
        /// but was never written a tier (TierOf → 0) still counts as tier 1, so the
        /// day-one troops train the moment the Barracks is usable.
        /// </summary>
        public static int EffectiveBarracksTier()
        {
            int tier = ModifierService.TierOf("barracks");
            return tier < 1 ? 1 : tier;
        }

        /// <summary>
        /// True when <paramref name="def"/> may be trained at the current Barracks tier
        /// (its <c>UnlockBarracksTier</c> &lt;= the effective tier). Null def → false.
        /// This is the ONLY tier comparison used for troops.
        /// </summary>
        public static bool IsTrainable(TroopDef def)
        {
            if (def == null) return false;
            return def.UnlockBarracksTier <= EffectiveBarracksTier();
        }

        /// <summary>
        /// The player-facing "why locked" line, e.g. "Unlocks at Barracks Tier 3 - War College".
        /// The tier NAME is pulled from <see cref="BuildingTierCatalog"/> when the barracks
        /// ladder is authored there; when it is not, the reason degrades cleanly to just the
        /// tier number ("Unlocks at Barracks Tier 3"). ASCII-only (no em-dash; device tofu risk).
        /// </summary>
        public static string LockedReason(TroopDef def)
        {
            if (def == null) return "";
            int need = def.UnlockBarracksTier;
            string tierName = TierName(need);
            return string.IsNullOrEmpty(tierName)
                ? "Unlocks at Barracks Tier " + need
                : "Unlocks at Barracks Tier " + need + " - " + tierName;
        }

        /// <summary>
        /// The authored name of Barracks tier <paramref name="tier"/> from
        /// <see cref="BuildingTierCatalog"/>, or null when the barracks/tier is not in the
        /// building-tiers catalog (the barracks may not be an upgradable building yet).
        /// </summary>
        public static string TierName(int tier)
        {
            var def = BuildingTierCatalog.TierOf("barracks", tier);
            return def != null ? def.Name : null;
        }
    }
}
