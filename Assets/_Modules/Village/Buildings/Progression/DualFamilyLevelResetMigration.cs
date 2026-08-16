// =============================================================================
// DualFamilyLevelResetMigration -- one-shot cleanup of the RESOURCE-ladder levels
// that the inverted upgrade-family precedence wrote for a DUAL-FAMILY building.
// Owner ruling 2026-08-15: "reset to 1".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// THE DEFECT (fixed at the source in commit adfbec3c; this file cleans the residue)
//   farm / lumbermill / forge are DUAL-FAMILY: they appear in BOTH
//   building-tiers.json (the WO-430 CITY tier ladder) and ResourceBuildingProgression
//   (the legacy per-building PlayerPrefs level ladder). CompletedUpgradeApplier
//   resolved the family in the OPPOSITE order to the start side, so an upgrade the
//   player STARTED (and paid for) on the city ladder was APPLIED to the resource
//   ladder: ResourceBuildingState.ApplyCompletedUpgrade wrote the purchased CITY TIER
//   number into "dotr.resbuilding.level.<id>", clamped 1..5.
//
//   Consequence: those buildings' HARVEST YIELD and TICK SPEED silently rose (a
//   lumbermill at a bogus level 2 yields 13 per tick instead of 10, every 42.5s
//   instead of 50s -- about 1.53x wood/hour) while the tier ladder the player
//   actually paid into never moved.
//
// WHY A CLEANUP IS NEEDED AT ALL
//   UpgradeFamilyResolver now sends every dual-family id to the CITY ladder, which
//   stops NEW bogus writes -- but it also means NOTHING writes that PlayerPrefs key
//   for a dual-family id any more. The inflated value is therefore FROZEN FOREVER.
//   The owner ruled: reset those to 1.
//
// WHAT THIS DOES *NOT* TOUCH (both were verified at source before writing this)
//   * GameState.BuildingTiers -- the CITY ladder is the one the player actually paid
//     into and it is CORRECT. Clearing it would delete purchased progress. This file
//     never reads or writes it.
//   * TechTree -- ResourceBuildingState.ResetAll() would have been the one-liner, but
//     it (a) deletes EVERY resource building's key, including any that is NOT dual-
//     family and therefore holds a legitimate level, and (b) calls TechTree.ResetAll,
//     which revokes the Magic-gated 'arcane_forge' unlock. So it is NOT used; the
//     targeted ResourceBuildingState.ResetLevelToOne is.
//   * Resources -- NO make-good grant. The baskets/crystals spent on upgrades that
//     never landed are a SEPARATE owner ruling that has not been made. Deliberately
//     out of scope; do not add one here without it.
//
// THE DUAL-FAMILY SET IS DERIVED, NEVER HARDCODED
//   It is BuildingTierCatalog INTERSECT ResourceBuildingProgression, asked through
//   UpgradeFamilyResolver.IsDualFamily -- the same authority the precedence fix uses.
//   Today that is exactly {farm, lumbermill, forge}; if a catalog changes, this
//   follows it instead of going stale. A resource building that is NOT dual-family
//   holds a legitimate level and is explicitly LEFT ALONE (and said so in the trace).
//
// ONE-SHOT, AND WHY THE MARKER IS PLAYERPREFS (NO SCHEMA BUMP)
//   The corrupt data does not live in the save payload at all -- it lives in
//   PlayerPrefs, which is device-scoped and travels with neither a save export nor a
//   cloud save. A GameState field + SaveSchema bump would put the latch in a
//   DIFFERENT store from the data it guards: an imported save would carry a "already
//   migrated" flag for prefs it has never seen, and a fresh save on a dirty install
//   would carry "not migrated" for prefs already cleaned. Co-scoping the marker with
//   the data is the correct scope AND costs no schema bump -- the AudioBootstrap
//   "dotr-unmute-migration-v1" precedent, which guards a PlayerPrefs-scoped one-shot
//   the same way. A player who legitimately levels a resource building later is
//   therefore never reset again.
//
// FAILURE POSTURE (SS12): if the CITY catalog is not loaded when this runs, the
// derived intersection would be EMPTY and the migration would mark itself complete
// having reset nothing -- a silent no-op that can never be retried. So an empty city
// catalog DEFERS (Warn + marker NOT set) and the migration retries next launch.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// One-shot reset to level 1 of the legacy RESOURCE-ladder level for every
    /// DUAL-FAMILY building (owner ruling 2026-08-15). See the file header for the
    /// defect, the scope carve-outs, and why the latch is a PlayerPrefs marker.
    /// </summary>
    public static class DualFamilyLevelResetMigration
    {
        /// <summary>
        /// The one-shot latch. Lives in PlayerPrefs -- the SAME store as the data it
        /// guards (see the header). Versioned so a future, differently-scoped cleanup
        /// can mint its own key rather than re-using a burnt one.
        /// </summary>
        public const string MarkerKey = "dotr.migration.dualfamily-level-reset.v1";

        /// <summary>Result of one <see cref="RunIfNeeded"/> call.</summary>
        public enum Outcome
        {
            /// <summary>The marker was already burnt -- nothing was examined.</summary>
            AlreadyRun = 0,
            /// <summary>Ran; nothing needed resetting (marker burnt).</summary>
            NothingToReset = 1,
            /// <summary>Ran; at least one id was reset to 1 (marker burnt).</summary>
            Reset = 2,
            /// <summary>Could NOT run safely (city catalog unavailable) -- marker NOT burnt, retries next launch.</summary>
            Deferred = 3,
        }

        /// <summary>True once the one-shot latch has been burnt on this install.</summary>
        public static bool HasRun => PlayerPrefs.GetInt(MarkerKey, 0) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            // Guarded: a throw here must never block the boot chain, and must never be
            // silent -- Guard routes it through FlowTrace.Fail. The marker is NOT burnt on
            // a throw (it is set only on the success paths below), so a failed pass retries.
            // Braced body (not an expression lambda) so this binds unambiguously to the
            // Guard.Try(string,string,Action) overload rather than Guard.Try<T>(...,Func<T>).
            Guard.Try("Progression", "dual-family resource-level reset migration",
                () => { RunIfNeeded(out _); });
        }

        /// <summary>
        /// Run the one-shot reset if it has not run yet. Idempotent: a second call
        /// returns <see cref="Outcome.AlreadyRun"/> and touches nothing.
        /// </summary>
        /// <param name="resetCount">How many ids were reset by THIS call (0 on every path but <see cref="Outcome.Reset"/>).</param>
        public static Outcome RunIfNeeded(out int resetCount)
        {
            resetCount = 0;
            if (HasRun) return Outcome.AlreadyRun;

            // Guard the derivation: an unloaded city catalog would make the intersection
            // empty and burn the marker on a false "nothing to reset".
            var cityCatalog = BuildingTierCatalog.All;
            if (cityCatalog == null || cityCatalog.Count == 0)
            {
                FlowTrace.Warn("Progression",
                    "dual-family level reset DEFERRED: the city tier catalog (building-tiers.json) is empty or " +
                    "unloaded, so the dual-family set cannot be derived. Marker NOT burnt - retries next launch.");
                return Outcome.Deferred;
            }

            var dual = new List<string>();
            var skipped = new List<string>();
            foreach (var def in ResourceBuildingProgression.All)
            {
                string id = def?.BuildingId;
                if (string.IsNullOrEmpty(id)) continue;
                if (UpgradeFamilyResolver.IsDualFamily(id)) dual.Add(id);
                else skipped.Add(id);
            }
            dual.Sort(System.StringComparer.Ordinal);
            skipped.Sort(System.StringComparer.Ordinal);

            var log = new StringBuilder();
            log.Append("dual-family resource-level reset (owner ruling: reset to 1). Derived set = ")
               .Append("BuildingTierCatalog INTERSECT ResourceBuildingProgression via UpgradeFamilyResolver.IsDualFamily -> [")
               .Append(string.Join(", ", dual.ToArray()))
               .Append("]. NOT dual-family (legitimate levels, left alone) -> [")
               .Append(string.Join(", ", skipped.ToArray()))
               .Append("].");
            FlowTrace.Step("Progression", log.ToString());

            if (dual.Count == 0)
            {
                // The catalogs loaded but no id overlaps -- a real, reportable state (the
                // ladders diverged), not a failure. Nothing is corrupt, so burn the marker.
                FlowTrace.Warn("Progression",
                    "dual-family level reset: NO dual-family id exists any more (the two ladders no longer overlap). " +
                    "Nothing was reset; marker burnt.");
                BurnMarker();
                return Outcome.NothingToReset;
            }

            for (int i = 0; i < dual.Count; i++)
            {
                string id = dual[i];
                int raw = ResourceBuildingState.RawStoredLevel(id, missing: -1);
                if (raw < 0)
                {
                    FlowTrace.Step("Progression",
                        $"dual-family level reset: '{id}' has NO persisted resource level - already effectively 1, nothing to reset.");
                    continue;
                }
                if (raw <= 1)
                {
                    FlowTrace.Step("Progression",
                        $"dual-family level reset: '{id}' persisted level {raw} is already 1 - nothing to reset.");
                    continue;
                }

                bool removed = ResourceBuildingState.ResetLevelToOne(id);
                int after = ResourceBuildingState.GetLevel(id);
                if (removed && after == 1)
                {
                    resetCount++;
                    FlowTrace.Step("Progression",
                        $"dual-family level reset: '{id}' RESET - old level {raw} -> new level {after} " +
                        "(harvest yield + tick speed return to L1; the CITY tier ladder is untouched).");
                }
                else
                {
                    FlowTrace.Fail("Progression",
                        $"dual-family level reset: '{id}' did NOT reset (key removed={removed}, level now {after}, was {raw}) - " +
                        "the inflated harvest yield is still live. Marker NOT burnt; retries next launch.");
                    return Outcome.Deferred;
                }
            }

            BurnMarker();
            if (resetCount == 0)
            {
                FlowTrace.Step("Progression",
                    $"dual-family level reset COMPLETE: examined {dual.Count} dual-family id(s), NOTHING needed resetting " +
                    "(no inflated resource level on this install). Marker burnt - never runs again.");
                return Outcome.NothingToReset;
            }

            FlowTrace.Step("Progression",
                $"dual-family level reset COMPLETE: {resetCount} of {dual.Count} dual-family id(s) reset to level 1. " +
                "GameState.BuildingTiers (the purchased city ladder) and the tech tree were NOT touched, and no " +
                "resources were granted. Marker burnt - never runs again.");
            return Outcome.Reset;
        }

        private static void BurnMarker()
        {
            PlayerPrefs.SetInt(MarkerKey, 1);
            PlayerPrefs.Save();
        }
    }
}
