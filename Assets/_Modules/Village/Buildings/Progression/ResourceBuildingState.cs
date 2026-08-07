// =============================================================================
// ResourceBuildingState — the runtime level + upgrade behaviour for one resource
// building (Farm / Lumbermill / Forge). WO-151 / DEF-121 (WO-230).
// -----------------------------------------------------------------------------
// A small registry of per-building levels keyed by building id, plus the
// Upgrade() operation that spends from the economy (via ResourceLedger) and
// bumps the level. The UI panel (BuildingUpgradePanel) renders against this and
// calls TryUpgrade().
//
// LEVEL PERSISTENCE — deliberate scoping decision (flagged for the gatekeeper):
//   GameState/SaveSchema has NO generic building-level field. Per CLAUDE.md
//   ("note a save-schema gap rather than inventing a schema") and the memory
//   note "save-schema collections not wired", this class persists levels in
//   PlayerPrefs under a namespaced key. This is self-contained and survives a
//   session; folding it into GameState + the v-bump SaveSchema is the documented
//   FOLLOW-UP (see the final report). Reads/writes are cheap and one-per-upgrade.
//
// This is a static registry (not a MonoBehaviour) so any caller — the UI panel,
// a future harvest tick, a save-owner pass — sees one consistent level per
// building without scene wiring. Village -> Core only.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>Outcome of an upgrade attempt — for UI feedback.</summary>
    public enum UpgradeResult
    {
        /// <summary>Level was raised and resources spent.</summary>
        Upgraded = 0,
        /// <summary>Already at the building's max level.</summary>
        MaxLevel = 1,
        /// <summary>Not enough resources to pay the next-level cost.</summary>
        Insufficient = 2,
        /// <summary>The id is not a known resource building.</summary>
        Unknown = 3,
        /// <summary>The next tier is Magic-gated and the player lacks the Magic (DEF-121).</summary>
        NeedMagic = 4,
        /// <summary>F8-51: a build/upgrade timer already runs on this building — locked until it completes.</summary>
        InProgress = 5,
        /// <summary>F8-51: the cost was charged and an UPGRADE TIMER started — the level applies at completion.</summary>
        Started = 6,
    }

    /// <summary>
    /// Static registry of resource-building levels + the upgrade operation.
    /// Levels start at 1 and persist in PlayerPrefs (see file header note).
    /// </summary>
    public static class ResourceBuildingState
    {
        private const string PrefsPrefix = "dotr.resbuilding.level.";

        /// <summary>Raised after any building's level changes. Arg = building id.</summary>
        public static event Action<string> LevelChanged;

        /// <summary>
        /// The current level (1..MaxLevel) of <paramref name="buildingId"/>.
        /// Returns 1 for an unknown id (defensive — never below 1).
        /// </summary>
        public static int GetLevel(string buildingId)
        {
            var def = ResourceBuildingProgression.Find(buildingId);
            if (def == null) return 1;
            int stored = PlayerPrefs.GetInt(Key(buildingId), 1);
            return def.ClampLevel(stored);
        }

        /// <summary>
        /// The current per-level def for a building (yield + next-level cost).
        /// Null for an unknown id.
        /// </summary>
        public static ResourceLevelDef CurrentDef(string buildingId)
        {
            var def = ResourceBuildingProgression.Find(buildingId);
            return def?.LevelDef(GetLevel(buildingId));
        }

        /// <summary>The base yield this building produces at its current level (0 when unknown).</summary>
        public static int CurrentYield(string buildingId)
        {
            var lvl = CurrentDef(buildingId);
            return lvl != null ? lvl.YieldPerTick : 0;
        }

        /// <summary>
        /// T-025: the EFFECTIVE yield at the current level — base YieldPerTick scaled
        /// by the level's size multiplier (rounded). This is the amount actually
        /// credited per harvest tick. Equals <see cref="CurrentYield"/> while the
        /// size multiplier is 1.0 (all harvestable tiers); larger at the arcane tier.
        /// </summary>
        public static int CurrentEffectiveYield(string buildingId)
        {
            var lvl = CurrentDef(buildingId);
            if (lvl == null) return 0;
            // WO-430 — fold in the city-upgrade production perk (lumbermill→wood, windmill→food,
            // forge→efficiency). 1.0 for any building outside the upgrade set, so other yields are
            // unchanged. (These WO-430 buildings upgrade via BuildingTiers, not the legacy level, so
            // no double-dip once the StructureMenu routes them to the tier tree.)
            float wo430 = DeNelle.Core.State.ModifierService.ProductionMultFor(buildingId);
            return Mathf.Max(0, Mathf.RoundToInt(lvl.YieldPerTick * Mathf.Max(0f, lvl.YieldSizeMultiplier) * wo430));
        }

        /// <summary>
        /// T-025: the harvest-SPEED (seconds between ticks) at the current level.
        /// Smaller = faster. Consumed by <see cref="ResourceBuildingHarvester"/>.
        /// Falls back to 6s for an unknown id.
        /// </summary>
        public static float CurrentHarvestInterval(string buildingId)
        {
            var lvl = CurrentDef(buildingId);
            return lvl != null ? Mathf.Max(0.5f, lvl.HarvestInterval) : 6f;
        }

        /// <summary>True when the building is at its top level.</summary>
        public static bool IsMaxLevel(string buildingId)
        {
            var lvl = CurrentDef(buildingId);
            return lvl == null || lvl.IsMaxLevel;
        }

        /// <summary>
        /// Attempts to upgrade <paramref name="buildingId"/> one level: validates
        /// the id, checks it is not maxed, spends the next-level cost from the
        /// economy (atomic — ResourceLedger.TrySpend), then bumps + persists the
        /// level and raises <see cref="LevelChanged"/>. Returns a result code for
        /// the UI to surface.
        /// </summary>
        public static UpgradeResult TryUpgrade(string buildingId)
        {
            FlowTrace.Step("Upgrade", $"TryUpgrade id='{buildingId ?? "<null>"}'");
            var def = ResourceBuildingProgression.Find(buildingId);
            if (def == null) { FlowTrace.Warn("Upgrade", $"id='{buildingId ?? "<null>"}' is not a resource building -> Unknown"); return UpgradeResult.Unknown; }

            int level = GetLevel(buildingId);
            var lvlDef = def.LevelDef(level);
            if (lvlDef == null || lvlDef.IsMaxLevel) { FlowTrace.Warn("Upgrade", $"'{buildingId}' at level {level} is max/no-def -> MaxLevel"); return UpgradeResult.MaxLevel; }

            if (!ResourceLedger.CanAfford(lvlDef.UpgradeCost))
            { FlowTrace.Warn("Upgrade", $"'{buildingId}' lvl {level}->{level + 1} unaffordable (harvestables) -> Insufficient"); return UpgradeResult.Insufficient; }

            // DEF-121 — a Magic-gated tier additionally requires the Magic tech axis.
            if (lvlDef.IsMagicGated && ResourceLedger.MagicBalance() < lvlDef.MagicCost)
            { FlowTrace.Warn("Upgrade", $"'{buildingId}' magic-gated tier short on Magic (have {ResourceLedger.MagicBalance()}, need {lvlDef.MagicCost}) -> NeedMagic"); return UpgradeResult.NeedMagic; }

            // ── F8-51 TIMER GATES (before the spend, so a rejection costs nothing) ──
            // A building with an ACTIVE build/upgrade timer is LOCKED; a full slot set
            // rejects. Flag OFF (ff.buildtimers=0) or no service = today's instant path.
            var timerSvc = DeNelle.Core.FeatureFlags.BuildTimers ? BuildTimerService.Instance : null;
            if (timerSvc != null)
            {
                if (timerSvc.IsBuilding(buildingId))
                {
                    FlowTrace.Warn("BuildTimer",
                        $"upgrade '{buildingId}' REJECTED (busy: {timerSvc.RemainingSeconds(buildingId):0}s)");
                    return UpgradeResult.InProgress;
                }
                // WO-895: a FULL crew set no longer rejects — the Obsidian Builder channel
                // QUEUES the job and pulls it when a crew frees, so the panel's one true
                // button can honestly show "Queued" instead of a dead refusal.
                if (!timerSvc.HasFreeSlot)
                    FlowTrace.Step("BuildTimer",
                        $"upgrade '{buildingId}' will QUEUE (no free build slot: {timerSvc.ActiveJobs.Count} active)");

                // WO-911 (M1, ruling Q4) — a full LINE refuses (a full SLOT set only queues).
                // Checked BEFORE the atomic spend below so the refusal costs the player nothing.
                if (timerSvc.IsLineFull(DeNelle.Core.Jobs.ChannelId.Builder))
                {
                    FlowTrace.Warn("BuildTimer",
                        $"upgrade '{buildingId}' REFUSED — Builders queue is full " +
                        $"({timerSvc.QueueDepth(DeNelle.Core.Jobs.ChannelId.Builder)}/" +
                        $"{timerSvc.QueueDepthLimit(DeNelle.Core.Jobs.ChannelId.Builder)}). Nothing charged.");
                    return UpgradeResult.Insufficient;
                }
            }

            // Atomic spend: harvestables + (optional) Magic in one transaction.
            if (!ResourceLedger.TrySpendWithMagic(lvlDef.UpgradeCost, lvlDef.MagicCost))
            { FlowTrace.Fail("Upgrade", $"'{buildingId}' TrySpendWithMagic FAILED after affordability check passed (raced / no GameStateService) — spend rolled back"); return lvlDef.IsMagicGated ? UpgradeResult.NeedMagic : UpgradeResult.Insufficient; } // raced / no service

            int next = def.ClampLevel(level + 1);

            // F8-51 — cost charged above; the LEVEL applies at timer COMPLETION
            // (BuildTimerService.CompleteJob -> CompletedUpgradeApplier -> ApplyCompletedUpgrade,
            // offline-fair). A null job here (raced) degrades to the instant apply below so a
            // paid charge is never lost.
            // WO-911 (M2): the harvestable lines AND the magic charged above ride the job so a
            // cancel refunds 100% of both (ruling Q1) — magic included, since TrySpendWithMagic
            // debited it in the same atomic transaction.
            if (timerSvc != null &&
                timerSvc.StartUpgrade(buildingId, next,
                    BuildTimerService.ToJobCost(lvlDef.UpgradeCost, lvlDef.MagicCost)) != null)
            {
                // F8 (owner 2026-07-17): CoC-style on-building countdown for a resource-building
                // upgrade too (reuses the WO-612 scaffold + world countdown; Guard-wrapped inside,
                // a no-match is a traced no-op and never blocks the buy).
                DeNelle.Village.UnderConstructionVisual.AttachToBuildingId(buildingId);
                return UpgradeResult.Started;
            }

            ApplyCompletedUpgrade(buildingId, next);
            return UpgradeResult.Upgraded;
        }

        /// <summary>
        /// Land a (charged) upgrade's level: persist it, unlock any tech node the bought tier
        /// grants, and raise <see cref="LevelChanged"/>. F8-51: shared by the instant path
        /// (flag OFF / no timer service) and the timer-completion path (CompletedUpgradeApplier),
        /// so both apply identically. Costs are NOT touched here — they were charged at commit.
        /// </summary>
        internal static void ApplyCompletedUpgrade(string buildingId, int targetLevel)
        {
            var def = ResourceBuildingProgression.Find(buildingId);
            if (def == null)
            { FlowTrace.Warn("Upgrade", $"ApplyCompletedUpgrade: '{buildingId ?? "<null>"}' is not a resource building — level {targetLevel} NOT applied"); return; }

            int next = def.ClampLevel(targetLevel);
            PlayerPrefs.SetInt(Key(buildingId), next);
            PlayerPrefs.Save();

            // Buying a Magic-gated tier lights up the tech-tree node it unlocks. The node is
            // authored on the level def the player upgraded FROM (next - 1).
            var fromDef = def.LevelDef(next - 1);
            if (fromDef != null && !string.IsNullOrEmpty(fromDef.UnlocksTechNode))
            { FlowTrace.Step("Upgrade", $"'{buildingId}' unlocks tech node '{fromDef.UnlocksTechNode}'"); TechTree.Unlock(fromDef.UnlocksTechNode); }

            FlowTrace.Step("Upgrade", $"'{buildingId}' upgraded to level {next}");
            LevelChanged?.Invoke(buildingId);
        }

        /// <summary>
        /// Resets all resource-building levels to 1 (used by a New Game / dev
        /// reset). Mirrors the carve-out semantics elsewhere; cheap.
        /// </summary>
        public static void ResetAll()
        {
            foreach (var id in ResourceBuildingProgression.OrderedIds)
            {
                PlayerPrefs.DeleteKey(Key(id));
                LevelChanged?.Invoke(id);
            }
            TechTree.ResetAll();   // DEF-121 — Magic-gated unlocks reset with the levels.
            PlayerPrefs.Save();
        }

        private static string Key(string buildingId) => PrefsPrefix + buildingId;
    }
}
