// =============================================================================
// ObsidianQueueRegression — headless oracle for the common "Obsidian" multi-channel
// work queue (WO-773). Marker: OBSIDIAN_QUEUE_OK / OBSIDIAN_QUEUE_FAIL.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Wired into DataRegression.RunAll.
// Style/contract mirrors the other Run(out reason) oracles.
//
// Proves the queue's structure + behaviour with REAL types (no scene/play mode):
//   • the model exists — JobKind/ChannelId/ChannelState/ObsidianQueueState +
//     BuildJobData.Kind/Channel + GameState.ObsidianQueue + the HUD + the gate;
//   • the channel routing — JobChannels.DefaultChannel maps kinds to Builder/Train/
//     Research;
//   • the engine — a slot cap + FIFO queue + auto-pull cascade + channel independence
//     (train while a wall upgrades) run through the REAL ObsidianQueueEngine;
//   • the migration — a v34 save's in-flight BuildJobs fold into the v35 Builder
//     channel (Kind backfilled, legacy list cleared) via the REAL SaveMigrator;
//   • the service seam — BuildTimerService exposes Enqueue/SlotCount/ActiveJobsOf/
//     PendingJobsOf + the QueueChanged event (reflected, so no MonoBehaviour spin-up);
//   • WO-778 surface: KindLabel/JobTarget for BarracksUpgrade/TroopUpgrade/TrainTroop,
//     HUD toggle caller (OpenWorkQueue + HudKit source), layout.body list host.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class ObsidianQueueRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== ObsidianQueueRegression: common multi-channel work queue (WO-773) ===");

            try
            {
                CheckSchemaVersion(failures, log);
                CheckModelShape(failures, log);
                CheckChannelRouting(failures, log);
                CheckEngineSlotsAndFifo(failures, log);
                CheckChannelIndependence(failures, log);
                CheckServiceSeam(failures, log);
                CheckHudAndGate(failures, log);
                CheckLabelsAndTargets(failures, log);
                CheckReachabilityAndLayout(failures, log);
                CheckMigration(failures, log);
            }
            catch (System.Exception ex)
            {
                failures.Add($"ObsidianQueueRegression threw: {ex.GetType().Name}: {ex.Message}");
            }

            return Verdict(failures, log, out reason);
        }

        // ── 1. schema bumped to v35 ───────────────────────────────────────────
        private static void CheckSchemaVersion(List<string> failures, StringBuilder log)
        {
            if (SaveSchema.CurrentVersion < 35)
                failures.Add($"SaveSchema.CurrentVersion is {SaveSchema.CurrentVersion} — WO-773 requires >= 35");
            else
                log.AppendLine($"  schema version v{SaveSchema.CurrentVersion} OK");
        }

        // ── 2. model shape — BuildJobData kind/channel + GameState.ObsidianQueue +
        //      ChannelState/ObsidianQueueState members ─────────────────────────
        private static void CheckModelShape(List<string> failures, StringBuilder log)
        {
            var jobT = typeof(BuildJobData);
            if (jobT.GetField("Kind") == null) failures.Add("BuildJobData.Kind field missing (the ObsidianJob kind axis)");
            if (jobT.GetField("Channel") == null) failures.Add("BuildJobData.Channel field missing (the worker-pool channel)");

            if (typeof(GameState).GetField("ObsidianQueue") == null)
                failures.Add("GameState.ObsidianQueue field missing (the persisted multi-channel queue)");

            var chT = typeof(ChannelState);
            if (chT.GetField("ActiveJobs") == null) failures.Add("ChannelState.ActiveJobs missing");
            if (chT.GetField("PendingQueue") == null) failures.Add("ChannelState.PendingQueue missing (the FIFO pending queue)");
            if (chT.GetField("BoughtSlots") == null) failures.Add("ChannelState.BoughtSlots missing (purchased slots)");

            if (typeof(ObsidianQueueState).GetMethod("Channel") == null)
                failures.Add("ObsidianQueueState.Channel(id) accessor missing");

            // The three canonical channels exist on a fresh Empty() queue.
            var q = ObsidianQueueState.Empty();
            foreach (ChannelId id in new[] { ChannelId.Builder, ChannelId.Train, ChannelId.Research })
                if (q.Channel(id) == null) failures.Add($"ObsidianQueueState.Empty() missing the {id} channel");

            log.AppendLine("  model shape (BuildJobData kind/channel, GameState.ObsidianQueue, ChannelState, 3 channels) OK-checked");
        }

        // ── 3. channel routing ────────────────────────────────────────────────
        private static void CheckChannelRouting(List<string> failures, StringBuilder log)
        {
            Expect(JobChannels.DefaultChannel(JobKind.Build),      ChannelId.Builder,  "Build",      failures);
            Expect(JobChannels.DefaultChannel(JobKind.Repair),     ChannelId.Builder,  "Repair",     failures);
            Expect(JobChannels.DefaultChannel(JobKind.WallUpgrade), ChannelId.Builder, "WallUpgrade", failures);
            Expect(JobChannels.DefaultChannel(JobKind.TrainTroop), ChannelId.Train,    "TrainTroop", failures);
            Expect(JobChannels.DefaultChannel(JobKind.UnlockTier), ChannelId.Research, "UnlockTier", failures);
            Expect(JobChannels.DefaultChannel(JobKind.LearnMagic), ChannelId.Research, "LearnMagic", failures);
            log.AppendLine("  channel routing (Build/Repair/Wall→Builder, Train→Train, UnlockTier/LearnMagic→Research) OK");
        }

        private static void Expect(ChannelId got, ChannelId want, string kind, List<string> failures)
        {
            if (got != want) failures.Add($"JobChannels.DefaultChannel({kind}) = {got}, expected {want}");
        }

        // ── 4. engine — slot cap + FIFO + auto-pull cascade ───────────────────
        private static void CheckEngineSlotsAndFifo(List<string> failures, StringBuilder log)
        {
            var ch = new ChannelState();
            const int slots = 2;
            double now = 1000;
            for (int i = 0; i < 4; i++)
                ObsidianQueueEngine.Enqueue(ch, slots, MakeJob("j" + i, JobKind.Build, 100), now);

            if (ch.ActiveJobs.Count != 2) failures.Add($"engine slot cap broken: {ch.ActiveJobs.Count} active (expected 2)");
            if (ch.PendingQueue.Count != 2) failures.Add($"engine queue broken: {ch.PendingQueue.Count} pending (expected 2)");

            var order = new List<string>();
            ObsidianQueueEngine.Resolve(ch, slots, 1_000_000, j => order.Add(j.StructureId));
            if (order.Count != 4) failures.Add($"engine cascade broken: {order.Count} completed (expected 4)");
            else if (!(order[0] == "j0" && order[1] == "j1" && order[2] == "j2" && order[3] == "j3"))
                failures.Add("engine FIFO completion order broken: " + string.Join(",", order));
            if (ch.ActiveJobs.Count + ch.PendingQueue.Count != 0) failures.Add("engine did not drain the queue after a long offline gap");
            else log.AppendLine("  engine slot cap + FIFO + offline cascade OK");
        }

        // ── 5. channel independence — train while a wall upgrades ──────────────
        private static void CheckChannelIndependence(List<string> failures, StringBuilder log)
        {
            var builder = new ChannelState();
            var train = new ChannelState();
            double now = 1000;
            bool wall = ObsidianQueueEngine.Enqueue(builder, 1, MakeJob("wall", JobKind.WallUpgrade, 500, ChannelId.Builder), now);
            bool troop = ObsidianQueueEngine.Enqueue(train, 1, MakeJob("troop", JobKind.TrainTroop, 500, ChannelId.Train), now);
            if (!(wall && troop))
                failures.Add("channels share slots: a troop could not train while a wall upgraded (both single-slot channels should start immediately)");
            else if (builder.PendingQueue.Count != 0 || train.PendingQueue.Count != 0)
                failures.Add("channel independence broken: a job queued when its own channel had a free slot");
            else
                log.AppendLine("  channel independence (train while wall upgrades) OK");
        }

        // ── 6. service seam — BuildTimerService exposes the queue API ──────────
        private static void CheckServiceSeam(List<string> failures, StringBuilder log)
        {
            var t = typeof(DeNelle.Village.BuildTimerService);
            if (t.GetMethod("Enqueue", new[] { typeof(JobKind), typeof(string), typeof(double), typeof(int) }) == null)
                failures.Add("BuildTimerService.Enqueue(JobKind,string,double,int) missing (the generic enqueue seam)");
            if (t.GetMethod("SlotCount", new[] { typeof(ChannelId) }) == null)
                failures.Add("BuildTimerService.SlotCount(ChannelId) missing (dynamic per-channel slot count)");
            if (t.GetMethod("ActiveJobsOf") == null) failures.Add("BuildTimerService.ActiveJobsOf(ChannelId) missing");
            if (t.GetMethod("PendingJobsOf") == null) failures.Add("BuildTimerService.PendingJobsOf(ChannelId) missing");
            if (t.GetEvent("QueueChanged") == null) failures.Add("BuildTimerService.QueueChanged event missing (the HUD seam)");
            // WO-172 back-compat API still present.
            if (t.GetMethod("StartBuild") == null) failures.Add("BuildTimerService.StartBuild missing (build flow seam)");
            if (t.GetMethod("StartUpgrade") == null) failures.Add("BuildTimerService.StartUpgrade missing (upgrade flow seam)");
            log.AppendLine("  BuildTimerService queue seam (Enqueue/SlotCount/ActiveJobsOf/PendingJobsOf/QueueChanged + StartBuild/StartUpgrade) OK");
        }

        // ── 7. HUD + gate exist ───────────────────────────────────────────────
        private static void CheckHudAndGate(List<string> failures, StringBuilder log)
        {
            var hudType = typeof(ObsidianQueueHud);
            if (!typeof(MonoBehaviour).IsAssignableFrom(hudType))
                failures.Add("ObsidianQueueHud is not a MonoBehaviour view (the code-built queue view)");
            var gate = typeof(DeNelle.Core.UI.ObsidianQueueGate);
            if (gate.GetMethod("RequestToggle") == null)
                failures.Add("ObsidianQueueGate.RequestToggle missing (the HUD open seam)");
            if (hudType.GetMethod("OpenWorkQueue", BindingFlags.Public | BindingFlags.Static) == null)
                failures.Add("ObsidianQueueHud.OpenWorkQueue missing (public toggle entry for HUD/regression)");
            if (hudType.GetMethod("FormatKindLabel", BindingFlags.Public | BindingFlags.Static) == null)
                failures.Add("ObsidianQueueHud.FormatKindLabel missing (WO-778 label seam)");
            if (hudType.GetMethod("FormatJobLine", BindingFlags.Public | BindingFlags.Static) == null)
                failures.Add("ObsidianQueueHud.FormatJobLine missing (WO-778 job-line seam)");
            log.AppendLine("  queue HUD (ObsidianQueueHud) + gate (ObsidianQueueGate) + format helpers OK");
        }

        // ── 7b. WO-778 kind labels + target identity ──────────────────────────
        private static void CheckLabelsAndTargets(List<string> failures, StringBuilder log)
        {
            // Kind labels must never be raw enum names for known kinds.
            AssertLabel(JobKind.BarracksUpgrade, "Barracks upgrade", failures);
            AssertLabel(JobKind.TroopUpgrade, "Troop upgrade", failures);
            AssertLabel(JobKind.TrainTroop, "Train", failures);
            AssertLabel(JobKind.Build, "Build", failures);
            AssertLabel(JobKind.WallUpgrade, "Wall upgrade", failures);

            // Train job target: barracks-train:<troopId>:<uid> → "Footman x1" (or catalog DisplayName).
            var trainJob = new BuildJobData
            {
                StructureId = BarracksService.TrainPrefix + "footman:abc12345",
                Kind = (int)JobKind.TrainTroop,
                Channel = (int)ChannelId.Train,
                StartMs = 1000,
                DurationMs = 90000,
            };
            string trainTarget = ObsidianQueueHud.FormatJobTarget(trainJob);
            if (string.IsNullOrEmpty(trainTarget) || !trainTarget.Contains("x1"))
                failures.Add("FormatJobTarget(train) expected troop x1 identity, got '" + trainTarget + "'");
            if (!string.IsNullOrEmpty(trainTarget) && trainTarget.Equals("Train"))
                failures.Add("FormatJobTarget(train) is kind-only — needs troop identity");

            // Barracks upgrade target.
            var barracksJob = new BuildJobData
            {
                StructureId = BarracksService.BarracksJobId,
                Kind = (int)JobKind.BarracksUpgrade,
                Channel = (int)ChannelId.Builder,
                TargetTier = 2,
            };
            string barracksTarget = ObsidianQueueHud.FormatJobTarget(barracksJob);
            if (barracksTarget == null || !barracksTarget.Contains("Barracks") || !barracksTarget.Contains("L2"))
                failures.Add("FormatJobTarget(barracks-upgrade) expected 'Barracks -> L2', got '" + barracksTarget + "'");

            // Troop upgrade target.
            var troopUp = new BuildJobData
            {
                StructureId = BarracksService.TroopUpgradePrefix + "archer",
                Kind = (int)JobKind.TroopUpgrade,
                Channel = (int)ChannelId.Research,
                TargetTier = 3,
            };
            string troopUpTarget = ObsidianQueueHud.FormatJobTarget(troopUp);
            if (troopUpTarget == null || !troopUpTarget.Contains("L3"))
                failures.Add("FormatJobTarget(troop-upgrade) expected tier L3, got '" + troopUpTarget + "'");

            // Job line (queued) carries target, not kind alone.
            string queuedLine = ObsidianQueueHud.FormatJobLine(trainJob, 0, queued: true);
            if (queuedLine == null || !queuedLine.Contains("queued"))
                failures.Add("FormatJobLine(queued) missing '(queued)': '" + queuedLine + "'");
            if (queuedLine != null && queuedLine == "Train (queued)")
                failures.Add("FormatJobLine(queued) is kind-only — needs target identity");

            log.AppendLine("  WO-778 labels (BarracksUpgrade/TroopUpgrade/TrainTroop + job targets) OK");
        }

        private static void AssertLabel(JobKind kind, string expected, List<string> failures)
        {
            string got = ObsidianQueueHud.FormatKindLabel(kind);
            if (got != expected)
                failures.Add("FormatKindLabel(" + kind + ") = '" + got + "', expected '" + expected + "'");
            // Raw-enum leak: only fail when the label equals ToString() AND we expected a
            // different player-facing word (Build/Upgrade intentionally match their enum names).
            if (got == kind.ToString() && expected != kind.ToString())
                failures.Add("FormatKindLabel(" + kind + ") returned raw enum (player-facing leak)");
        }

        // ── 7c. WO-778 reachability (HUD caller) + layout.body list host ──────
        private static void CheckReachabilityAndLayout(List<string> failures, StringBuilder log)
        {
            // OpenWorkQueue is the public HUD/regression entry that calls RequestToggle.
            var open = typeof(ObsidianQueueHud).GetMethod("OpenWorkQueue",
                BindingFlags.Public | BindingFlags.Static);
            if (open == null)
                failures.Add("ObsidianQueueHud.OpenWorkQueue missing (reachability seam)");

            // HudKitController (DeNelle.HUD — not referenced by this asmdef) must call
            // ObsidianQueueGate.RequestToggle from the Work button (source oracle).
            string kitPath = Path.Combine(Application.dataPath, "_Modules/HUD/Kit/HudKitController.cs");
            if (!File.Exists(kitPath))
            {
                failures.Add("HudKitController.cs missing at " + kitPath);
            }
            else
            {
                string kitSrc = File.ReadAllText(kitPath);
                if (kitSrc.IndexOf("ObsidianQueueGate.RequestToggle") < 0)
                    failures.Add("HudKitController does not call ObsidianQueueGate.RequestToggle (queue still dark)");
                if (kitSrc.IndexOf("workQueueButton") < 0)
                    failures.Add("HudKitController missing workQueueButton widget id (WO-778)");
                else
                    log.AppendLine("  HudKitController Work button -> ObsidianQueueGate.RequestToggle OK");
                if (kitSrc.IndexOf("queueStatusChip") < 0)
                    failures.Add("HudKitController missing queueStatusChip (persistent Builders status, WO-778)");
            }

            // OCCUPANCY ORACLE (the Work-button-dark lesson, 2026-07-30): a widget that is
            // Register()'d in code but absent from hud-areas.json's posture rows NEVER
            // renders — code-present, behavior-absent. Assert both queue widgets have rows,
            // in BOTH dual copies, and that the copies have not diverged. Also assert the
            // parser knows the chip's area — an unknown area string is warn-skipped.
            string resJson = Path.Combine(Application.dataPath, "Resources/Data/Canonical/hud-areas.json");
            string samJson = Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical/hud-areas.json");
            foreach (var p in new[] { resJson, samJson })
            {
                if (!File.Exists(p)) { failures.Add("hud-areas.json missing: " + p); continue; }
                string j = File.ReadAllText(p);
                if (j.IndexOf("workQueueButton") < 0) failures.Add("hud-areas.json missing workQueueButton row: " + p);
                if (j.IndexOf("queueStatusChip") < 0) failures.Add("hud-areas.json missing queueStatusChip row: " + p);
            }
            if (File.Exists(resJson) && File.Exists(samJson) &&
                File.ReadAllText(resJson) != File.ReadAllText(samJson))
                failures.Add("hud-areas.json dual copies diverged (Resources vs StreamingAssets — CanonicalJson law)");
            string cfgPath = Path.Combine(Application.dataPath, "_Modules/HUD/Kit/HudAreasConfig.cs");
            if (File.Exists(cfgPath) && File.ReadAllText(cfgPath).IndexOf("queuestatus") < 0)
                failures.Add("HudAreasConfig.TryParseArea missing 'queuestatus' case — chip row would be warn-skipped");

            // List host prefers layout.body (source oracle on ObsidianQueueHud.Build).
            string hudPath = Path.Combine(Application.dataPath, "_Modules/Village/BuildMode/ObsidianQueueHud.cs");
            if (!File.Exists(hudPath))
            {
                failures.Add("ObsidianQueueHud.cs missing at " + hudPath);
            }
            else
            {
                string hudSrc = File.ReadAllText(hudPath);
                if (hudSrc.IndexOf("layout.body") < 0 && hudSrc.IndexOf("layout != null && built.chrome.layout.body") < 0)
                    failures.Add("ObsidianQueueHud.Build does not parent list to layout.body");
                if (hudSrc.IndexOf("MakeScrollZone") < 0)
                    failures.Add("ObsidianQueueHud.Build missing MakeScrollZone (overflow/clip risk)");
                else
                    log.AppendLine("  ObsidianQueueHud list host = layout.body + MakeScrollZone OK");
            }
        }

        // ── 8. migration — v34 buildJobs fold into the v35 Builder channel ─────
        private static void CheckMigration(List<string> failures, StringBuilder log)
        {
            var v34 = new SaveSchema.PersistedState
            {
                BuildJobs = new List<BuildJobData>
                {
                    new BuildJobData { StructureId = "forge@1_2", JobType = (int)BuildJobType.Build,   StartMs = 5000, DurationMs = 60000 },
                    new BuildJobData { StructureId = "tower@3_4", JobType = (int)BuildJobType.Upgrade, StartMs = 6000, DurationMs = 90000, TargetTier = 2 },
                },
            };
            var migrated = SaveMigrator.Migrate(v34, 34);
            if (migrated.ObsidianQueue == null)
            {
                failures.Add("v34→v35 migration did not build ObsidianQueue");
                return;
            }
            var builder = migrated.ObsidianQueue.Channel(ChannelId.Builder);
            if (builder.ActiveJobs.Count != 2)
                failures.Add($"v34→v35 folded {builder.ActiveJobs.Count} builder jobs (expected 2) — in-flight builds lost");
            if (migrated.BuildJobs != null && migrated.BuildJobs.Count != 0)
                failures.Add("v34→v35 left legacy buildJobs populated (single-source-of-truth broken)");
            var tower = builder.ActiveJobs.Find(j => j.StructureId == "tower@3_4");
            if (tower.JobKind != JobKind.Upgrade)
                failures.Add($"v34→v35 did not backfill Kind: tower job kind is {tower.JobKind} (expected Upgrade)");
            if (tower.TargetTier != 2)
                failures.Add("v34→v35 lost the upgrade target tier (in-progress upgrade would land the wrong level)");
            log.AppendLine("  v34→v35 migration (buildJobs → Builder channel, Kind backfilled, legacy cleared) OK");
        }

        private static BuildJobData MakeJob(string id, JobKind kind, double durationMs, ChannelId channel = ChannelId.Builder)
        {
            return new BuildJobData
            {
                StructureId = id,
                Kind = (int)kind,
                Channel = (int)channel,
                JobType = (kind == JobKind.Upgrade) ? (int)BuildJobType.Upgrade : (int)BuildJobType.Build,
                DurationMs = durationMs,
            };
        }

        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "OBSIDIAN QUEUE OK — multi-channel model + channel routing + engine slot-cap/FIFO/cascade " +
                         "+ channel independence + BuildTimerService seam + HUD/gate + labels/targets + " +
                         "reachability/layout + v34→v35 migration all hold";
                Debug.Log("OBSIDIAN_QUEUE_OK\n" + log);
                return true;
            }
            reason = $"OBSIDIAN QUEUE: {failures.Count} failure(s): " + string.Join(" | ", failures);
            Debug.LogError($"OBSIDIAN_QUEUE_FAIL: {failures.Count} failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }
    }
}
