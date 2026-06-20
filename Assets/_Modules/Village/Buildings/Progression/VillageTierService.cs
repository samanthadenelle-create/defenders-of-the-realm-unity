// =============================================================================
// VillageTierService — the global Village/Stronghold Tier (WO-432 tech-gate).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// The WC3 Town-Hall -> Keep -> Castle anchor, owner-decided to live at the HEART
// OF ELARION (the town center). Raising it OPENS higher building tiers + research
// levels (BuildingUpgradeService gates the tier; BuildingPerkService gates the
// research). Pure static surface over GameState.VillageTier — the Heart's upgrade
// UI calls TryUpgrade(); everything else reads Current. Persists + recomputes the
// active GameModifiers on change. Village -> Core is a legal asmdef edge.
// =============================================================================

using DeNelle.Core.State;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// The global Village/Stronghold Tier — the tech-gate raised at the Heart of Elarion.
    /// Gates building tier upgrades + research perks. Bought with Crystals (the premium
    /// progression currency). v1 cost ladder is a simple scaling formula (tunable later).
    /// </summary>
    public static class VillageTierService
    {
        /// <summary>Highest Village/Stronghold Tier (matches the 3-tier building ladder).</summary>
        public const int MaxTier = 3;

        /// <summary>The player's current Village/Stronghold Tier (0 = fresh village).</summary>
        public static int Current
        {
            get
            {
                var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
                return s != null ? s.VillageTier : 0;
            }
        }

        /// <summary>True once the village is fully advanced (no further tier to buy).</summary>
        public static bool IsMax => Current >= MaxTier;

        /// <summary>Crystal cost to raise the village tier from its current level (0 at max). Epic + scaling.</summary>
        public static int NextCost()
        {
            int next = Current + 1;
            if (next > MaxTier) return 0;
            return 250 * next;   // 250 / 500 / 750 crystals (tunable in v2).
        }

        /// <summary>
        /// Raise the Village/Stronghold Tier by one (the Heart-of-Elarion upgrade). Spends Crystals
        /// atomically via EconomyService. Returns false at max tier or when unaffordable. On success it
        /// persists + recomputes the active modifiers so the newly-gated tiers/research open immediately.
        /// </summary>
        public static bool TryUpgrade()
        {
            if (IsMax) return false;
            var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (s == null) return false;

            int cost = NextCost();
            if (cost > 0)
            {
                var econ = EconomyService.Instance;
                if (econ == null) return false;
                var c = new DeNelle.Village.ResourceCost { Crystals = cost };
                if (!econ.TrySpend(c)) return false;
            }

            s.VillageTier = Current + 1;
            GameStateService.Instance.Save();
            ModifierService.Recompute();
            return true;
        }
    }
}
