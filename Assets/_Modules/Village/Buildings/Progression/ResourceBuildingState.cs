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

        /// <summary>The yield this building produces at its current level (0 when unknown).</summary>
        public static int CurrentYield(string buildingId)
        {
            var lvl = CurrentDef(buildingId);
            return lvl != null ? lvl.YieldPerTick : 0;
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
            var def = ResourceBuildingProgression.Find(buildingId);
            if (def == null) return UpgradeResult.Unknown;

            int level = GetLevel(buildingId);
            var lvlDef = def.LevelDef(level);
            if (lvlDef == null || lvlDef.IsMaxLevel) return UpgradeResult.MaxLevel;

            if (!ResourceLedger.CanAfford(lvlDef.UpgradeCost))
                return UpgradeResult.Insufficient;

            // DEF-121 — a Magic-gated tier additionally requires the Magic tech axis.
            if (lvlDef.IsMagicGated && ResourceLedger.MagicBalance() < lvlDef.MagicCost)
                return UpgradeResult.NeedMagic;

            // Atomic spend: harvestables + (optional) Magic in one transaction.
            if (!ResourceLedger.TrySpendWithMagic(lvlDef.UpgradeCost, lvlDef.MagicCost))
                return lvlDef.IsMagicGated ? UpgradeResult.NeedMagic : UpgradeResult.Insufficient; // raced / no service

            int next = def.ClampLevel(level + 1);
            PlayerPrefs.SetInt(Key(buildingId), next);
            PlayerPrefs.Save();

            // Buying a Magic-gated tier lights up the tech-tree node it unlocks.
            if (!string.IsNullOrEmpty(lvlDef.UnlocksTechNode))
                TechTree.Unlock(lvlDef.UnlocksTechNode);

            LevelChanged?.Invoke(buildingId);
            return UpgradeResult.Upgraded;
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
