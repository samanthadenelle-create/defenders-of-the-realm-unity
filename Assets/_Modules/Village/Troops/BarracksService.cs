// =============================================================================
// BarracksService — the LIVE facade for the WO-771.9 Barracks & troop-upgrade
// progression (integration half), riding the committed WO-773 Obsidian queue.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The player-facing API the BarracksPanel + deploy/training gates call. Reads the
// live GameState (BarracksLevel / TroopLevels — additive fields, no schema bump) +
// EconomyService (afford/spend) and ENQUEUES timed jobs on the common Obsidian
// queue (BuildTimerService), NEVER a private timer:
//   • Barracks upgrade  -> JobKind.BarracksUpgrade on the BUILDER channel.
//   • Troop track upgrade -> JobKind.TroopUpgrade   on the RESEARCH channel.
//   • Troop training      -> JobKind.TrainTroop     on the TRAIN channel.
// The COMPLETION effect for each is an IJobEffect (below), registered once at
// startup with the shared JobEffectRegistry. The pure decision + mutation logic
// lives in BarracksProgression (headlessly testable); this class only wires the
// live singletons to it.
//
// One job per structure-id per channel (BuildTimerService enforces), so exactly one
// barracks upgrade and one upgrade-per-troop can be in flight at a time (CoC parity);
// training mints a UNIQUE id per unit so many can queue in parallel on the Train channel.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Live facade over <see cref="BarracksProgression"/> + the Obsidian queue for the
    /// WO-771.9 progression: barracks-level upgrades, per-troop track upgrades and timed
    /// training, each enqueued on its channel and applied by a registered IJobEffect.
    /// </summary>
    public static class BarracksService
    {
        /// <summary>Obsidian job id of the (single) in-flight barracks-level upgrade.</summary>
        public const string BarracksJobId = "barracks-upgrade";
        /// <summary>Prefix for a per-troop upgrade job id: <c>barracks-troop-upgrade:&lt;troopId&gt;</c>.</summary>
        public const string TroopUpgradePrefix = "barracks-troop-upgrade:";
        /// <summary>Prefix for a training job id: <c>barracks-train:&lt;troopId&gt;:&lt;uid&gt;</c>.</summary>
        public const string TrainPrefix = "barracks-train:";

        /// <summary>Raised whenever a level changes (upgrade start OR a job completed) so the UI refreshes.</summary>
        public static event Action Changed;

        // ── Live state reads ──────────────────────────────────────────────────

        private static GameState State =>
            GameStateService.Instance != null ? GameStateService.Instance.State : null;

        /// <summary>The player's current barracks level (1 with no live state).</summary>
        public static int BarracksLevel => BarracksProgression.BarracksLevelOf(State);

        /// <summary>A troop's current upgrade level (1 = baseline; 1 with no live state).</summary>
        public static int TroopLevel(string troopId) => BarracksProgression.TroopLevelOf(State, troopId);

        /// <summary>True when <paramref name="troopId"/> is unlocked at the current barracks level.</summary>
        public static bool IsTroopUnlocked(string troopId) =>
            BarracksProgression.IsTroopUnlocked(troopId, BarracksLevel);

        /// <summary>True while a barracks-level upgrade is running/queued on the Builder channel.</summary>
        public static bool IsUpgradingBarracks =>
            IsInFlight(ChannelId.Builder, BarracksJobId);

        /// <summary>True while an upgrade for <paramref name="troopId"/> is running/queued on the Research channel.</summary>
        public static bool IsUpgradingTroop(string troopId) =>
            IsInFlight(ChannelId.Research, TroopUpgradePrefix + troopId);

        // ── Barracks upgrade ──────────────────────────────────────────────────

        /// <summary>
        /// True when the barracks can be upgraded right now: a higher level exists, one is not
        /// already in flight, and the player can afford the next level's cost. <paramref name="reason"/>
        /// carries the player-facing block reason otherwise.
        /// </summary>
        public static bool CanUpgradeBarracks(out string reason)
        {
            reason = null;
            if (State == null) { reason = "No game state."; return false; }

            int level = BarracksLevel;
            if (!BarracksProgression.HasNextBarracksLevel(level)) { reason = "Barracks is at max level."; return false; }
            if (IsUpgradingBarracks) { reason = "Barracks upgrade already in progress."; return false; }

            var cost = BarracksProgression.BarracksUpgradeCost(level);
            var eco = EconomyService.Instance;
            if (eco == null || !eco.CanAfford(cost)) { reason = "Not enough resources."; return false; }
            return true;
        }

        /// <summary>
        /// Spends the next barracks level's cost and ENQUEUES a BarracksUpgrade job on the Builder
        /// channel (the level applies at completion via <see cref="BarracksUpgradeEffect"/>). Returns
        /// false (no spend, no enqueue) when <see cref="CanUpgradeBarracks"/> refuses or the spend fails.
        /// </summary>
        public static bool UpgradeBarracks()
        {
            if (!CanUpgradeBarracks(out string reason))
            {
                FlowTrace.Warn("Barracks", "UpgradeBarracks refused: " + reason);
                return false;
            }

            int level = BarracksLevel;
            var cost = BarracksProgression.BarracksUpgradeCost(level);
            float seconds = BarracksProgression.BarracksUpgradeSeconds(level);

            var eco = EconomyService.Instance;
            if (eco == null || !eco.TrySpend(cost)) { FlowTrace.Warn("Barracks", "UpgradeBarracks spend failed."); return false; }

            var queue = BuildTimerService.Instance;
            if (queue == null) { FlowTrace.Warn("Barracks", "UpgradeBarracks: no BuildTimerService."); return false; }
            queue.Enqueue(JobKind.BarracksUpgrade, BarracksJobId, seconds, level + 1);

            FlowTrace.Step("Barracks", $"barracks upgrade L{level}->L{level + 1} enqueued (Builder, {seconds:0}s).");
            Changed?.Invoke();
            return true;
        }

        // ── Troop track upgrade ───────────────────────────────────────────────

        /// <summary>
        /// True when <paramref name="troopId"/> can be upgraded now: unlocked, below its max
        /// level, not already upgrading, and affordable. <paramref name="reason"/> carries the block.
        /// </summary>
        public static bool CanUpgradeTroop(string troopId, out string reason)
        {
            reason = null;
            if (State == null) { reason = "No game state."; return false; }
            if (string.IsNullOrEmpty(troopId)) { reason = "Unknown troop."; return false; }

            if (!IsTroopUnlocked(troopId)) { reason = "Unlocks at Barracks Level " + BarracksProgression.UnlockLevelFor(troopId) + "."; return false; }

            int level = TroopLevel(troopId);
            if (!BarracksProgression.HasNextTroopLevel(troopId, level)) { reason = "Troop is at max level."; return false; }
            if (IsUpgradingTroop(troopId)) { reason = "Upgrade already in progress."; return false; }

            var cost = BarracksProgression.TroopUpgradeCost(troopId, level + 1);
            var eco = EconomyService.Instance;
            if (eco == null || !eco.CanAfford(cost)) { reason = "Not enough resources."; return false; }
            return true;
        }

        /// <summary>
        /// Spends the next troop-level cost and ENQUEUES a TroopUpgrade job on the Research channel
        /// (the level applies at completion via <see cref="TroopUpgradeEffect"/>). Returns false
        /// (no spend/enqueue) when <see cref="CanUpgradeTroop"/> refuses or the spend fails.
        /// </summary>
        public static bool UpgradeTroop(string troopId)
        {
            if (!CanUpgradeTroop(troopId, out string reason))
            {
                FlowTrace.Warn("Barracks", "UpgradeTroop refused (" + troopId + "): " + reason);
                return false;
            }

            int level = TroopLevel(troopId);
            var cost = BarracksProgression.TroopUpgradeCost(troopId, level + 1);
            float seconds = BarracksProgression.TroopUpgradeSeconds(troopId, level + 1);

            var eco = EconomyService.Instance;
            if (eco == null || !eco.TrySpend(cost)) { FlowTrace.Warn("Barracks", "UpgradeTroop spend failed."); return false; }

            var queue = BuildTimerService.Instance;
            if (queue == null) { FlowTrace.Warn("Barracks", "UpgradeTroop: no BuildTimerService."); return false; }
            // JobKind.TroopUpgrade's default channel is Research (JobChannels) — the single-arg overload routes it there.
            queue.Enqueue(JobKind.TroopUpgrade, TroopUpgradePrefix + troopId, seconds, level + 1);

            FlowTrace.Step("Barracks", $"troop upgrade '{troopId}' L{level}->L{level + 1} enqueued (Research, {seconds:0}s).");
            Changed?.Invoke();
            return true;
        }

        // ── Timed training (Train channel; WO-771.9 §2 / WO-771.8) ─────────────

        /// <summary>
        /// COC-parity TIMED training: for each of <paramref name="qty"/> units, checks the troop is
        /// unlocked + there is army room + the resource cost is affordable, spends the cost, and
        /// ENQUEUES a TrainTroop job on the Train channel (the unit lands in the army at completion
        /// via <see cref="TrainTroopEffect"/> — NOT instantly, NO private timer). Stops early when a
        /// gate fails (that unit is neither spent nor enqueued). Returns the number enqueued.
        /// This is the sanctioned timed path; the existing instant TroopTrainingVM/DialogueCommands
        /// path is untouched (its felt-behaviour flip to this queue is WO-771.8 / PO-gated).
        /// </summary>
        public static int EnqueueTraining(string troopId, int qty)
        {
            if (string.IsNullOrEmpty(troopId) || qty <= 0) return 0;
            var state = State;
            if (state == null || state.Army == null) return 0;
            if (!IsTroopUnlocked(troopId)) return 0;

            var def = TroopCatalog.Find(troopId);
            if (def == null) return 0;

            var eco = EconomyService.Instance;
            var queue = BuildTimerService.Instance;
            if (eco == null || queue == null) return 0;

            // ResourceCost ctor order: (wood, food, iron, crystals, coins).
            var cost = new ResourceCost(def.CostWood, def.CostFood, def.CostIron);
            var army = state.Army;

            int enqueued = 0;
            for (int i = 0; i < qty; i++)
            {
                if (!army.CanTrain(troopId, TroopDialogueCommands.SlotOf)) break;   // cap full
                if (!eco.TrySpend(cost)) break;                                     // unaffordable
                string jobId = TrainPrefix + troopId + ":" + Guid.NewGuid().ToString("N").Substring(0, 8);
                queue.Enqueue(JobKind.TrainTroop, jobId, def.BuildSeconds);
                enqueued++;
            }

            if (enqueued > 0)
            {
                FlowTrace.Step("Barracks", $"training enqueued {enqueued}x '{troopId}' (Train, {def.BuildSeconds:0}s each).");
                Changed?.Invoke();
            }
            return enqueued;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private static bool IsInFlight(ChannelId channel, string id)
        {
            var queue = BuildTimerService.Instance;
            if (queue == null || string.IsNullOrEmpty(id)) return false;
            foreach (var j in queue.ActiveJobsOf(channel)) if (j.StructureId == id) return true;
            foreach (var j in queue.PendingJobsOf(channel)) if (j.StructureId == id) return true;
            return false;
        }

        private static GameState LiveState() => State;

        private static void PersistAndNotify()
        {
            GameStateService.Instance?.Save();
            ModifierService.Recompute();   // barracks unlock refresh (perk/tier listeners)
            Changed?.Invoke();
        }

        // ── Completion effects (registered once with the shared JobEffectRegistry) ──

        /// <summary>BarracksUpgrade job complete -> raise BarracksLevel by one, persist, notify.</summary>
        private sealed class BarracksUpgradeEffect : IJobEffect
        {
            public JobKind Kind => JobKind.BarracksUpgrade;
            public void Apply(BuildJobData job)
            {
                int level = BarracksProgression.ApplyBarracksUpgrade(LiveState());
                FlowTrace.Step("Barracks", "BarracksUpgrade job complete -> barracks L" + level + ".");
                PersistAndNotify();
            }
        }

        /// <summary>TroopUpgrade job complete -> raise that troop's upgrade level by one, persist, notify.</summary>
        private sealed class TroopUpgradeEffect : IJobEffect
        {
            public JobKind Kind => JobKind.TroopUpgrade;
            public void Apply(BuildJobData job)
            {
                string troopId = TroopIdFromUpgrade(job.StructureId);
                int level = BarracksProgression.ApplyTroopUpgrade(LiveState(), troopId);
                FlowTrace.Step("Barracks", "TroopUpgrade job complete -> '" + troopId + "' L" + level + ".");
                PersistAndNotify();
            }
        }

        /// <summary>TrainTroop job complete -> grant the trained troop into the army, persist, notify.</summary>
        private sealed class TrainTroopEffect : IJobEffect
        {
            public JobKind Kind => JobKind.TrainTroop;
            public void Apply(BuildJobData job)
            {
                string troopId = TroopIdFromTrain(job.StructureId);
                int count = BarracksProgression.GrantTrainedTroop(LiveState(), troopId);
                FlowTrace.Step("Barracks", "TrainTroop job complete -> +1 '" + troopId + "' (roster " + count + ").");
                PersistAndNotify();
            }
        }

        /// <summary>Recovers the troop id from a "barracks-troop-upgrade:&lt;troopId&gt;" job id.</summary>
        private static string TroopIdFromUpgrade(string jobId)
        {
            if (string.IsNullOrEmpty(jobId) || !jobId.StartsWith(TroopUpgradePrefix)) return jobId;
            return jobId.Substring(TroopUpgradePrefix.Length);
        }

        /// <summary>Recovers the troop id from a "barracks-train:&lt;troopId&gt;:&lt;uid&gt;" job id.</summary>
        private static string TroopIdFromTrain(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) return jobId;
            // Format: barracks-train:<troopId>:<uid>. Troop ids carry hyphens, never colons,
            // so split on ':' yields [prefix, troopId, uid].
            var parts = jobId.Split(':');
            return parts.Length >= 2 ? parts[1] : jobId;
        }

        // Register the three completion effects once at startup (idempotent; re-registered on domain reload).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterEffects()
        {
            JobEffectRegistry.Register(new BarracksUpgradeEffect());
            JobEffectRegistry.Register(new TroopUpgradeEffect());
            JobEffectRegistry.Register(new TrainTroopEffect());
            FlowTrace.Step("Barracks", "WO-771.9 job effects registered (BarracksUpgrade/TroopUpgrade/TrainTroop).");
        }
    }
}
