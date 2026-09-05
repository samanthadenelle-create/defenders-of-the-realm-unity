// =============================================================================
// BarracksProgression — the PURE, side-effect-free decision + mutation core for
// the WO-771.9 Barracks & troop-upgrade progression (integration half).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// This is the headlessly UNIT-TESTABLE brain the live BarracksService (singletons)
// and the three IJobEffect handlers delegate to. It reads the committed catalogs
// (BarracksCatalog / TroopUpgradeCatalog / TroopCatalog) and mutates a passed
// GameState directly — NO GameStateService / EconomyService / BuildTimerService
// reference, so a test drives it with a `ScriptableObject.CreateInstance<GameState>()`
// and a FakeEconomy (mirrors ObsidianQueueEngine / TroopTrainingVM purity).
//
// GATING RECONCILE (WO-771.9 §3): a troop is unlocked when EITHER encoding says so —
//   • barracks.json  : the troop id appears in unlocksTroopIds for some level <= L, OR
//   • troops.json    : the troop's UnlockBarracksTier <= L.
// The two agree by construction (barracks level N unlocks the troop whose
// UnlockBarracksTier == N); the union is defensive against a one-sided authoring gap.
//
// TROOP UPGRADE COST/TIME: troop-upgrades.json carries stat CURVES + abilities only,
// NOT per-level cost/time. The troop-upgrade TIME is DERIVED from the troop's base
// BuildSeconds scaled by the target level (placeholder curve; WO-771.14 owns balance).
//
// WO-1387 (owner ruling 2026-09-04 23:16): a troop upgrade has NO resource/gold cost -
// TroopUpgradeCost returns an EMPTY basket and TroopUpgradeSeconds is the ONLY price.
// "we agreed earlier training free ... just time ... and gold is to hire mercenaries if
// they dont want to wait". The previous CostGold*level curve (commit 281902df0) mis-read
// her; do not restore it. Gold is spent ONLY through BuildTimerService.TryInstantFinish
// (FinishPaysGold = TrainTroop) to skip the clock.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Pure decision + mutation helpers for the Barracks / troop-upgrade progression.
    /// Every method is side-effect-free except the explicit <c>Apply*</c> / <c>Grant*</c>
    /// mutators, which mutate ONLY the <see cref="GameState"/> they are handed.
    /// </summary>
    public static class BarracksProgression
    {
        /// <summary>Day-one barracks level (unlocks the starter roster).</summary>
        public const int DefaultBarracksLevel = 1;
        /// <summary>Baseline troop upgrade level (pure baseline — no curve applied).</summary>
        public const int DefaultTroopLevel = 1;

        // ── GameState reads (default-on-read; null-safe) ──────────────────────

        /// <summary>The barracks level held on <paramref name="state"/>, floored to 1 (1 with no state).</summary>
        public static int BarracksLevelOf(GameState state)
        {
            if (state == null) return DefaultBarracksLevel;
            return state.BarracksLevel < DefaultBarracksLevel ? DefaultBarracksLevel : state.BarracksLevel;
        }

        /// <summary>A troop's upgrade level on <paramref name="state"/>, floored to 1 (baseline when absent).</summary>
        public static int TroopLevelOf(GameState state, string troopId)
        {
            if (state == null || state.TroopLevels == null || string.IsNullOrEmpty(troopId)) return DefaultTroopLevel;
            if (state.TroopLevels.TryGetValue(troopId, out int lvl))
                return lvl < DefaultTroopLevel ? DefaultTroopLevel : lvl;
            return DefaultTroopLevel;
        }

        // ── Barracks ladder ───────────────────────────────────────────────────

        /// <summary>The highest authored barracks level (0 when the catalog failed to load).</summary>
        public static int MaxBarracksLevel => BarracksCatalog.MaxLevel;

        /// <summary>The barracks def for the NEXT level above <paramref name="currentLevel"/>, or null at max.</summary>
        public static BarracksDef NextBarracksDef(int currentLevel) => BarracksCatalog.Find(currentLevel + 1);

        /// <summary>True when a higher barracks level is authored above <paramref name="currentLevel"/>.</summary>
        public static bool HasNextBarracksLevel(int currentLevel) => NextBarracksDef(currentLevel) != null;

        /// <summary>Resource cost to reach the next barracks level (zero when none/at max).</summary>
        public static ResourceCost BarracksUpgradeCost(int currentLevel)
        {
            var def = NextBarracksDef(currentLevel);
            return def != null ? def.Cost : new ResourceCost();
        }

        /// <summary>Wall-clock seconds the next barracks upgrade takes (0 when none/at max).</summary>
        public static float BarracksUpgradeSeconds(int currentLevel)
        {
            var def = NextBarracksDef(currentLevel);
            return def != null ? Mathf.Max(0f, def.BuildTimeSeconds) : 0f;
        }

        // ── Troop unlock gating (reconcile barracks.json ↔ troops.json) ────────

        /// <summary>
        /// True when <paramref name="troopId"/> is unlocked at barracks level
        /// <paramref name="barracksLevel"/> — by EITHER the troops.json UnlockBarracksTier OR the
        /// barracks.json unlocksTroopIds union (see the class header). Unknown id → false.
        /// </summary>
        public static bool IsTroopUnlocked(string troopId, int barracksLevel)
        {
            if (string.IsNullOrEmpty(troopId)) return false;

            // troops.json encoding.
            var def = TroopCatalog.Find(troopId);
            if (def != null && def.UnlockBarracksTier <= barracksLevel) return true;

            // barracks.json encoding — is it listed at any level we have reached?
            foreach (var lvl in BarracksCatalog.All)
            {
                if (lvl == null || lvl.Level > barracksLevel || lvl.UnlocksTroopIds == null) continue;
                foreach (var id in lvl.UnlocksTroopIds)
                    if (id == troopId) return true;
            }
            return false;
        }

        /// <summary>
        /// The barracks level that unlocks <paramref name="troopId"/> — troops.json
        /// UnlockBarracksTier first, else the lowest barracks.json level listing it, else 1.
        /// </summary>
        public static int UnlockLevelFor(string troopId)
        {
            var def = TroopCatalog.Find(troopId);
            if (def != null && def.UnlockBarracksTier > 0) return def.UnlockBarracksTier;

            int best = int.MaxValue;
            foreach (var lvl in BarracksCatalog.All)
            {
                if (lvl == null || lvl.UnlocksTroopIds == null) continue;
                foreach (var id in lvl.UnlocksTroopIds)
                    if (id == troopId && lvl.Level < best) best = lvl.Level;
            }
            return best == int.MaxValue ? 1 : best;
        }

        // ── Per-troop upgrade ladder ───────────────────────────────────────────

        /// <summary>The highest upgrade level a troop's curves define (1 when it has no upgrade row).</summary>
        public static int MaxTroopLevel(string troopId)
        {
            var upg = TroopUpgradeCatalog.Find(troopId);
            if (upg == null) return 1;
            int reachLen = upg.Reach != null && upg.Reach.Values != null ? upg.Reach.Values.Length : 0;
            int strLen = upg.Strength != null && upg.Strength.Values != null ? upg.Strength.Values.Length : 0;
            int max = reachLen > strLen ? reachLen : strLen;
            return max < 1 ? 1 : max;
        }

        /// <summary>True when <paramref name="troopId"/> can climb above <paramref name="currentLevel"/>.</summary>
        public static bool HasNextTroopLevel(string troopId, int currentLevel) =>
            currentLevel < MaxTroopLevel(troopId);

        /// <summary>
        /// The resource cost to upgrade <paramref name="troopId"/> TO <paramref name="targetLevel"/>:
        /// ALWAYS EMPTY. WO-1387, owner 2026-09-04 23:16, verbatim: "we agreed earlier training
        /// free" / "just time" / "and gold is to hire mercenaries if they dont want to wait".
        /// The only price of a troop upgrade is <see cref="TroopUpgradeSeconds"/>; the only gold
        /// spend is the instant-finish skip in BuildTimerService. This used to return
        /// <c>coins: CostGold * targetLevel</c> (commit 281902df0, "the last CLI did bad changes") -
        /// TroopDef.CostGold STAYS on the row as the raid-reward anchor / mercenary-hire basis and
        /// is never charged here. Pinned by TrainingCostsTimeOnlyRegression.
        /// </summary>
        public static ResourceCost TroopUpgradeCost(string troopId, int targetLevel)
        {
            return new ResourceCost();
        }

        /// <summary>
        /// DERIVED wall-clock seconds to upgrade <paramref name="troopId"/> to
        /// <paramref name="targetLevel"/> — base train time × target level × 2 (placeholder;
        /// WO-771.14 tunes). Floored to a small minimum so a card never shows an instant upgrade.
        /// </summary>
        public static float TroopUpgradeSeconds(string troopId, int targetLevel)
        {
            var def = TroopCatalog.Find(troopId);
            float baseSec = def != null && def.BuildSeconds > 0f ? def.BuildSeconds : 30f;
            int m = targetLevel < 1 ? 1 : targetLevel;
            return Mathf.Max(15f, baseSec * m * 2f);
        }

        /// <summary>
        /// The NEXT special ability <paramref name="troopId"/> will unlock above
        /// <paramref name="currentLevel"/> (lowest threshold strictly greater), or null when none remain.
        /// </summary>
        public static AbilityUnlock NextAbility(string troopId, int currentLevel)
        {
            var upg = TroopUpgradeCatalog.Find(troopId);
            if (upg == null || upg.SpecialAbilities == null) return null;
            AbilityUnlock best = null;
            foreach (var a in upg.SpecialAbilities)
            {
                if (a == null || a.LevelThreshold <= currentLevel) continue;
                if (best == null || a.LevelThreshold < best.LevelThreshold) best = a;
            }
            return best;
        }

        // ── Mutators (mutate ONLY the passed GameState — the IJobEffect completion seams) ──

        /// <summary>
        /// Raise the barracks level on <paramref name="state"/> by one (clamped to the authored
        /// max). The completion effect of a BarracksUpgrade job. Returns the new level (0 no state).
        /// </summary>
        public static int ApplyBarracksUpgrade(GameState state)
        {
            if (state == null) return 0;
            int max = MaxBarracksLevel;
            int next = BarracksLevelOf(state) + 1;
            if (max > 0 && next > max) next = max;
            state.BarracksLevel = next;
            return next;
        }

        /// <summary>
        /// Raise <paramref name="troopId"/>'s upgrade level on <paramref name="state"/> by one
        /// (clamped to the troop's max). The completion effect of a TroopUpgrade job. Returns the
        /// new level (0 on a null state/id).
        /// </summary>
        public static int ApplyTroopUpgrade(GameState state, string troopId)
        {
            if (state == null || string.IsNullOrEmpty(troopId)) return 0;
            if (state.TroopLevels == null) state.TroopLevels = new Dictionary<string, int>();
            int max = MaxTroopLevel(troopId);
            int next = TroopLevelOf(state, troopId) + 1;
            if (next > max) next = max;
            state.TroopLevels[troopId] = next;
            return next;
        }

        /// <summary>
        /// Grant a freshly-trained troop into <paramref name="state"/>'s army (unconditionally —
        /// cost/cap were checked at enqueue). The completion effect of a TrainTroop job. Returns
        /// the new roster count (0 on a null state).
        ///
        /// <para>WO-1374 — this is the SINGLE OWNER of "a troop joins the roster", and so it is
        /// where funnel step 2 ("army trained") is emitted. Both real paths pass through here:
        /// the timed Train job's completion effect, and the free starter squad granted on
        /// Barracks completion. Instrumenting the two call sites separately would mean the step
        /// could be reached by one and missed by the other, which is how a funnel starts
        /// reporting a conversion nobody can reproduce.</para>
        /// </summary>
        /// <param name="source">Which seam granted it - trace/property only, never behaviour.</param>
        public static int GrantTrainedTroop(GameState state, string troopId, string source = "train-job")
        {
            if (state == null || string.IsNullOrEmpty(troopId)) return 0;
            if (state.Army == null) state.Army = new ArmyStorage();
            state.Army.GrantTrained(troopId);
            int count = state.Army.Owned != null ? state.Army.Owned.Count : 0;
            // Guarded: analytics must never be able to lose a troop the player paid for.
            DeNelle.Core.Diagnostics.Guard.Try("Funnel", "army trained",
                () => DeNelle.Core.Analytics.RaidFunnel.ArmyTrained(troopId, count, source));
            return count;
        }
    }
}
