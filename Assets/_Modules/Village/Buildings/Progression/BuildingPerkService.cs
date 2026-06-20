// =============================================================================
// BuildingPerkService — research (buy) a building perk with Gold (WO-432).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// The WC3 "research at the Blacksmith" pillar: numerical upgrades (damage/armor
// Lvl 1/2/3) + creative-owned ability unlocks, bought with GOLD (economy Coins),
// GATED by the building's tier AND the Village/Stronghold Tier. Pure static surface
// (mirrors BuildingUpgradeService) — the panel calls TryResearch; the VM reads
// IsOwned/CanResearch. On success records the perk in GameState.OwnedBuildingPerks,
// persists, and recomputes the active GameModifiers (ModifierService folds owned
// perks into towers/troops/raids).
// =============================================================================

using DeNelle.Core.State;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>Buy/own/query building research perks. The Gold-cost research layer over the tier ladder.</summary>
    public static class BuildingPerkService
    {
        /// <summary>The persisted owned-perks key for (building, perk).</summary>
        public static string Key(string buildingId, string perkId) => buildingId + ":" + perkId;

        /// <summary>True if the player has already researched this perk.</summary>
        public static bool IsOwned(string buildingId, string perkId)
        {
            var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            return s != null && s.OwnedBuildingPerks != null && s.OwnedBuildingPerks.Contains(Key(buildingId, perkId));
        }

        /// <summary>
        /// Whether this perk is researchable RIGHT NOW. Gates: it exists, isn't owned, the building has
        /// reached the perk's unlock tier, AND the Village/Stronghold Tier meets that tier (the WC3
        /// "Lvl N needs Tier N" rule). <paramref name="reason"/> returns a player-facing lock line for the View.
        /// </summary>
        public static bool CanResearch(string buildingId, string perkId, out string reason)
        {
            reason = null;
            var perk = BuildingTierCatalog.FindPerk(buildingId, perkId);
            if (perk == null) { reason = "Unknown research."; return false; }
            if (IsOwned(buildingId, perkId)) { reason = "Researched."; return false; }

            int unlock = BuildingTierCatalog.PerkUnlockTier(buildingId, perkId);
            if (ModifierService.TierOf(buildingId) < unlock) { reason = "Upgrade the building to Tier " + unlock + " first."; return false; }
            if (VillageTierService.Current < unlock) { reason = "Locked — needs Village Tier " + unlock + "."; return false; }
            return true;
        }

        /// <summary>
        /// Research (buy) the perk with Gold (economy Coins). No-op + false if it can't be researched
        /// (see <see cref="CanResearch"/>) or the spend fails. On success records it, persists, recomputes.
        /// </summary>
        public static bool TryResearch(string buildingId, string perkId)
        {
            if (!CanResearch(buildingId, perkId, out _)) return false;

            var perk = BuildingTierCatalog.FindPerk(buildingId, perkId);
            var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (perk == null || s == null) return false;
            if (s.OwnedBuildingPerks == null) s.OwnedBuildingPerks = new System.Collections.Generic.List<string>();

            if (perk.GoldCost > 0)
            {
                var econ = EconomyService.Instance;
                if (econ == null) return false;
                var cost = new DeNelle.Village.ResourceCost { Coins = perk.GoldCost };
                if (!econ.TrySpend(cost)) return false;   // can't afford -> no mutation
            }

            s.OwnedBuildingPerks.Add(Key(buildingId, perkId));
            GameStateService.Instance.Save();
            ModifierService.Recompute();
            return true;
        }
    }
}
