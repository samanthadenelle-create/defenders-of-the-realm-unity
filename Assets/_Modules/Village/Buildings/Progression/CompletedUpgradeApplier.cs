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
// visual apply HERE, at timer COMPLETION. The job key names the family, resolved by
// the SHARED UpgradeFamilyResolver (city tiers WIN over the legacy resource ladder —
// the same precedence the START side uses; see UpgradeFamilyResolver's header):
//   * a placed-structure key "itemId@cellX_cellZ"          -> BaseLayout record +
//     the live PlacedStructure (BuildModeController.ApplyUpgradeLevel), if spawned.
//   * a WO-430 city-tier building id                       -> BuildingUpgradeService
//   * a legacy-only resource building id                   -> ResourceBuildingState
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

            // FAMILY PRECEDENCE — resolved by the SHARED UpgradeFamilyResolver, the SAME call the
            // START side (BuildingUpgradeVM) and DialogueCommandSink.structure_upgrade make. This
            // file used to check IsResourceBuilding FIRST and IsUpgradable SECOND — the OPPOSITE of
            // the start side's "city tiers win; else legacy" — so a dual-family building
            // (farm / lumbermill / forge, present in BOTH ladders) was STARTED on the city ladder
            // and APPLIED to the resource one: BuildingUpgradeService.ApplyTier (the only writer of
            // GameState.BuildingTiers / Save / Recompute / ApplyStructureHp) never ran and the
            // player's tier panel dead-ended after paying. Never re-derive the order here.
            var family = UpgradeFamilyResolver.Resolve(id);
            bool dual = UpgradeFamilyResolver.IsDualFamily(id);
            FlowTrace.Step("BuildTimer",
                $"upgrade '{id}' completed -> resolved {UpgradeFamilyResolver.LadderName(family)}"
                + (dual ? " [DUAL-FAMILY id: city precedence]" : ""));

            switch (family)
            {
                // Placed-structure keys are the only ones carrying '@' (UnderConstructionVisual.KeyFor).
                case UpgradeFamily.PlacedStructure:
                    ApplyPlaced(id, level);
                    return;

                case UpgradeFamily.City:
                {
                    BuildingUpgradeService.ApplyTier(id, level);
                    // READ-BACK: name the ladder AND the level that actually landed. A silent
                    // no-op inside ApplyTier (no GameStateService) can no longer read as success.
                    int landed = ModifierService.TierOf(id);
                    if (landed == level)
                        FlowTrace.Step("BuildTimer",
                            $"upgrade '{id}' applied to CITY-TIER ladder: GameState.BuildingTiers['{id}'] = {landed} (target {level})");
                    else
                        FlowTrace.Fail("BuildTimer",
                            $"upgrade '{id}' targeted CITY-TIER ladder tier {level} but GameState.BuildingTiers['{id}'] reads {landed} after apply -- the tier did NOT land (player paid, ladder did not move)");
                    return;
                }

                case UpgradeFamily.Resource:
                {
                    ResourceBuildingState.ApplyCompletedUpgrade(id, level);
                    // READ-BACK: ResourceBuildingState clamps to the def's MaxLevel, so a target
                    // above the ladder's top silently lands LOWER. Say which level actually landed.
                    int landedLevel = ResourceBuildingState.GetLevel(id);
                    if (landedLevel == level)
                        FlowTrace.Step("BuildTimer",
                            $"upgrade '{id}' applied to RESOURCE-LEVEL ladder: PlayerPrefs level = {landedLevel} (target {level})");
                    else
                        FlowTrace.Fail("BuildTimer",
                            $"upgrade '{id}' targeted RESOURCE-LEVEL ladder level {level} but the stored level reads {landedLevel} after apply (clamped or not written) -- the ladder did NOT reach the paid level");
                    return;
                }

                default:
                    FlowTrace.Warn("BuildTimer",
                        $"upgrade '{id}' completed but matches no upgrade family (not resource / city / placed key) — level {level} NOT applied");
                    return;
            }
        }

        // ── Placed structure ("itemId@cellX_cellZ", the WO-612 job-key shape) ─────
        /// <summary>
        /// Land <paramref name="level"/> on the placed structure named by <paramref name="key"/>:
        /// persisted BaseLayout record first, then the live object's visual + stats if it is
        /// spawned. PUBLIC because it is also the TIMERS-OFF apply path
        /// (<see cref="PlacedStructureUpgradeService"/>) — one apply, never a second copy, so
        /// the instant path and the timer path can never diverge.
        /// </summary>
        public static void ApplyPlaced(string key, int level)
        {
            if (!PlacedUpgradeKey.TryParse(key, out string itemId, out int cx, out int cz))
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
            PlacedStructure live = PlacedStructureUpgradeService.FindLive(itemId, cell);
            if (live != null) BuildModeController.ApplyUpgradeLevel(live, level);
            else FlowTrace.Step("BuildTimer",
                $"upgrade '{key}' completed with no live structure in scene — persisted level {level}; visual applies on next spawn");

            GameStateService.Instance?.Save();
            FlowTrace.Step("BuildTimer", $"upgrade '{key}' completed -> level applied (tier {level})");
        }
    }
}
