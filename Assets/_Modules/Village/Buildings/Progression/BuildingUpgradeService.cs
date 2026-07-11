// =============================================================================
// BuildingUpgradeService — the ONE shared execute path for a CITY building
// tier upgrade (WO: MVVM building-upgrade panel).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// EXTRACTED VERBATIM from DialogueCommandBridge.CmdTryUpgradeBuilding's execute
// body so BOTH the legacy Yarn command AND the new MVVM upgrade panel run the
// EXACT same model-side mechanics — no duplicated economy/save/recompute logic.
// The behaviour is byte-for-byte identical to the old inline body:
//   * read the catalog cost (Wood/Food/Crystal) for the target tier
//   * require targetTier == current + 1 and a real tier def
//   * spend atomically via EconomyService.TrySpend (false => no-op)
//   * write GameState.BuildingTiers[id] = tier, Save(), ModifierService.Recompute()
//
// This is the SOLE model-side touch of the MVVM slice: the Yarn command now CALLS
// this instead of carrying its own copy; the Yarn-var bookkeeping (the $<id>_Level
// gate var + $lastUpgradeOk) stays in the command (it is presentation-for-Yarn,
// not model mechanics). Village -> Core is a legal asmdef edge.
// =============================================================================

using DeNelle.Core.State;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// Shared, side-effecting execute for a single city-building tier upgrade. Pure
    /// static surface (no scene wiring) so the Yarn bridge and the MVVM panel both
    /// call ONE path. Returns true only when the spend succeeded and the tier was
    /// written/saved; false (no mutation) for an invalid tier or an unaffordable cost.
    /// </summary>
    public static class BuildingUpgradeService
    {
        /// <summary>
        /// Attempt to buy <paramref name="targetTier"/> of <paramref name="id"/>. Mirrors
        /// the old CmdTryUpgradeBuilding body EXACTLY: only the next tier (current+1) of a
        /// catalogued building is buyable; the cost is the catalog cost; the spend is atomic
        /// (EconomyService.TrySpend); on success it writes GameState.BuildingTiers, persists,
        /// and recomputes the active GameModifiers. No Yarn / UI side effects here.
        /// </summary>
        public static bool TryUpgrade(string id, int targetTier)
        {
            int current = ModifierService.TierOf(id);
            var def = BuildingTierCatalog.TierOf(id, targetTier);
            if (def != null && targetTier == current + 1)
            {
                // WO-432 TECH-GATE: a tier locked behind the Village/Stronghold Tier (Heart of Elarion)
                // can't be bought until the village reaches it — the WC3 "need a Keep for tier-2" rule.
                var gateState = GameStateService.Instance != null ? GameStateService.Instance.State : null;
                int villageTier = gateState != null ? gateState.VillageTier : 0;
                if (def.RequiresVillageTier > villageTier) return false;

                // ── F8-51 TIMER GATES (before the spend, so a rejection costs nothing) ──
                // A building with an ACTIVE build/upgrade timer is LOCKED; a full slot set
                // rejects. Flag OFF / no service = today's instant path. The VM mirrors this
                // check for an honest status line (this bool covers the Yarn path too).
                var timerSvc = DeNelle.Core.FeatureFlags.BuildTimers
                    ? DeNelle.Village.BuildTimerService.Instance : null;
                if (timerSvc != null)
                {
                    if (timerSvc.IsBuilding(id))
                    {
                        DeNelle.Core.Diagnostics.FlowTrace.Warn("BuildTimer",
                            $"upgrade '{id}' REJECTED (busy: {timerSvc.RemainingSeconds(id):0}s)");
                        return false;
                    }
                    if (!timerSvc.HasFreeSlot)
                    {
                        DeNelle.Core.Diagnostics.FlowTrace.Warn("BuildTimer",
                            $"upgrade '{id}' REJECTED (no free build slot: {timerSvc.ActiveJobs.Count} active)");
                        return false;
                    }
                }

                var cost = new DeNelle.Village.ResourceCost { Wood = def.CostWood, Food = def.CostFood, Crystals = def.CostCrystal };
                var econ = EconomyService.Instance;
                var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
                if (econ != null && state != null && econ.TrySpend(cost))
                {
                    // F8-51 — cost charged above; the TIER applies at timer COMPLETION
                    // (BuildTimerService.CompleteJob -> CompletedUpgradeApplier -> ApplyTier,
                    // offline-fair). A null job (raced) degrades to the instant apply so a
                    // paid charge is never lost.
                    if (timerSvc != null && timerSvc.StartUpgrade(id, targetTier) != null)
                        return true;

                    ApplyTier(id, targetTier);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Land a (charged) city-tier upgrade: write GameState.BuildingTiers, persist, and
        /// recompute the active GameModifiers. F8-51: shared by the instant path (flag OFF /
        /// no timer service) and the timer-completion path (CompletedUpgradeApplier), so both
        /// apply identically. Costs are NOT touched here — they were charged at commit.
        /// </summary>
        internal static void ApplyTier(string id, int targetTier)
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("BuildTimer",
                    $"ApplyTier '{id}' tier {targetTier}: no GameStateService — tier NOT applied");
                return;
            }
            if (state.BuildingTiers == null)
                state.BuildingTiers = new System.Collections.Generic.Dictionary<string, int>();
            state.BuildingTiers[id] = targetTier;
            svc.Save();
            ModifierService.Recompute();
        }
    }
}
