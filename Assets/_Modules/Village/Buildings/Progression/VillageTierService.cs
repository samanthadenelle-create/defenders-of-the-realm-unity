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
//
// ⚠ WO-2004 (2026-09-06): THE CEILING AND THE COST LADDER ARE NO LONGER IN THIS
// FILE. `MaxTier` was `const int = 3` and `NextCost()` was `250 * next`; both now
// project HeartProgressionCatalog / heart-progression.json, which is their single
// authority. Values are unchanged (3, and 250/500/750). Do not re-inline either —
// a balance number copied back into code is exactly the duplicated state CLAUDE.md
// §2/§5/§16 keep paying for.
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
        /// <summary>
        /// Highest Village/Stronghold Tier — the player-facing HEART LEVEL ceiling.
        /// <para>⛔ READ FROM DATA, NEVER RE-HARDCODED (WO-2004). This was
        /// <c>public const int MaxTier = 3;</c> until 2026-09-06. The ceiling now lives in
        /// <c>heart-progression.json</c> and <see cref="HeartProgressionCatalog.MaxLevel"/> is its
        /// only reader; this property projects it so every existing call site
        /// (ProgressionReachabilityRegression, HeartSurfaceRegression, HeartProgression.MaxLevel)
        /// keeps compiling unchanged. Verified 2026-09-06: no call site required a compile-time
        /// constant — no attribute argument, no <c>case</c> label, no <c>const</c> initializer
        /// named it — so const → property is source-compatible here.</para>
        /// <para>⚠ A DIFFERENT AXIS FROM <c>RepoProps.MaxStructureLevel</c> (6, the per-structure
        /// ladder ceiling). Two integer scales with similar names; conflating them was the WO-1423
        /// dead end. Neither may be re-hardcoded, and neither is the other.</para>
        /// <para>Returns 0 if the catalog failed to load — a LOUD state (the Heart reports Max and
        /// HeartProgressionCatalog has already emitted FlowTrace.Fail), never a silent 3.</para>
        /// </summary>
        public static int MaxTier => HeartProgressionCatalog.MaxLevel;

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

        /// <summary>
        /// Crystal cost to raise the village tier from its current level (0 at max).
        /// <para>⛔ READ FROM DATA, NEVER RE-HARDCODED (WO-2004). This was the literal
        /// <c>return 250 * next;</c> until 2026-09-06 — a balance curve for the gate that opens
        /// nearly all content, buried where the owner could not tune it. The ladder now lives in
        /// <c>heart-progression.json</c> (250 / 500 / 750, the SAME numbers the formula produced —
        /// this was a de-hardcoding, not a re-balance) and ⛔ the owner rules on those values.</para>
        /// </summary>
        public static int NextCost()
        {
            int next = Current + 1;
            if (next > MaxTier) return 0;
            return HeartProgressionCatalog.CostToReach(next);
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
