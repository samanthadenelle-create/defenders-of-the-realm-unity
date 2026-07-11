// =============================================================================
// CompletedUpgradeApplier — F8-51: the ONE place a finished UPGRADE timer lands
// its level. Called by BuildTimerService.CompleteJob (the WO-612 completion seam)
// for every Upgrade job — live expiry, ad/instant skip, and the offline-fair
// load sweep all route through the same apply.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// Owner directive F8-51 (2026-07-11): "Should not be able to upgrade till build
// is complete, should not be able to upgrade instantly twice, each should have a
// build or upgrade timer." Costs are charged at COMMIT (unchanged); the level and
// visual apply HERE, at timer COMPLETION. The job key names the family:
//   * a resource building id (farm / lumbermill / forge)  -> ResourceBuildingState
//   * a WO-430 city-tier building id                       -> BuildingUpgradeService
//   * a placed-structure key "itemId@cellX_cellZ"          -> BaseLayout record +
//     the live PlacedStructure (BuildModeController.ApplyUpgradeLevel), if spawned.
//
// The persisted BaseLayout record is ALWAYS updated (even when the live object
// is not spawned yet — e.g. the offline sweep fires before BaseLayoutLoader
// spawns), so the eventual spawn reads the new level. No silent catch: every
// dead-end path traces (§12).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// Applies the deferred level of a completed UPGRADE job (F8-51). Static, stateless —
    /// invoked synchronously from <see cref="BuildTimerService.CompleteJob"/> so the apply
    /// can never be missed by event-subscription order.
    /// </summary>
    public static class CompletedUpgradeApplier
    {
        /// <summary>Route a completed Upgrade job to its family's level-apply.</summary>
        public static void Apply(BuildJobData job)
        {
            string id = job.StructureId;
            int level = job.TargetTier;
            if (string.IsNullOrEmpty(id) || level <= 0)
            {
                // Pre-F8-51 saves carry TargetTier 0 — nothing to apply (back-compat, traced).
                FlowTrace.Warn("BuildTimer",
                    $"upgrade '{id ?? "<null>"}' completed with no target tier ({level}) — nothing applied (old-save job?)");
                return;
            }

            // Placed-structure keys are the only ones carrying '@' (UnderConstructionVisual.KeyFor).
            if (id.IndexOf('@') >= 0) { ApplyPlacedStructure(id, level); return; }

            if (ResourceBuildingProgression.IsResourceBuilding(id))
            {
                ResourceBuildingState.ApplyCompletedUpgrade(id, level);
                FlowTrace.Step("BuildTimer", $"upgrade '{id}' completed -> level applied (level {level})");
                return;
            }

            if (BuildingTierCatalog.IsUpgradable(id))
            {
                BuildingUpgradeService.ApplyTier(id, level);
                FlowTrace.Step("BuildTimer", $"upgrade '{id}' completed -> level applied (tier {level})");
                return;
            }

            FlowTrace.Warn("BuildTimer",
                $"upgrade '{id}' completed but matches no upgrade family (not resource / city / placed key) — level {level} NOT applied");
        }

        // ── Placed structure ("itemId@cellX_cellZ", the WO-612 job-key shape) ─────
        private static void ApplyPlacedStructure(string key, int level)
        {
            int at = key.LastIndexOf('@');
            string itemId = key.Substring(0, at);
            string cellPart = key.Substring(at + 1);
            int us = cellPart.IndexOf('_');
            if (at <= 0 || us <= 0
                || !int.TryParse(cellPart.Substring(0, us), out int cx)
                || !int.TryParse(cellPart.Substring(us + 1), out int cz))
            {
                FlowTrace.Fail("BuildTimer",
                    $"upgrade '{key}' completed but the placed-structure key did not parse — level {level} NOT applied");
                return;
            }
            var cell = new Vector2Int(cx, cz);

            // 1) Persisted record FIRST — the level survives even if the live object is
            //    absent (offline sweep before spawn, or player away from the village).
            BuildModeController.UpdateLayoutLevel(itemId, cell, level);

            // 2) Live object, if spawned: full visual + stat apply (reskin / tier accent /
            //    tower range-damage / wall toughness) via the same path the instant upgrade uses.
            PlacedStructure live = null;
            var loader = BaseLayoutLoader.Instance;
            if (loader != null)
            {
                var loaded = loader.Loaded;
                for (int i = 0; i < loaded.Count; i++)
                {
                    var p = loaded[i];
                    if (p != null && p.itemId == itemId && p.gridCell == cell) { live = p; break; }
                }
            }
            if (live != null) BuildModeController.ApplyUpgradeLevel(live, level);
            else FlowTrace.Step("BuildTimer",
                $"upgrade '{key}' completed with no live structure in scene — persisted level {level}; visual applies on next spawn");

            GameStateService.Instance?.Save();
            FlowTrace.Step("BuildTimer", $"upgrade '{key}' completed -> level applied (tier {level})");
        }
    }
}
