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
                CheckCardRail(failures, log);
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
                // Owner 2026-08-01: the bar's Queues button is RETIRED — the right-column
                // Builders chip (QueueStatus band, above the resources dock) is the one
                // Queues entry. A live workQueueButton widget id reappearing = regression.
                if (kitSrc.IndexOf("Register(\"workQueueButton\"") >= 0)
                    failures.Add("HudKitController re-registers the retired workQueueButton (owner 2026-08-01: Queues entry = the Builders chip)");
                if (kitSrc.IndexOf("queueStatusChip") < 0)
                    failures.Add("HudKitController missing queueStatusChip (persistent Builders status, WO-778)");
                else
                    log.AppendLine("  HudKitController Builders chip -> ObsidianQueueGate.RequestToggle OK (bar button retired 2026-08-01)");
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
                if (j.IndexOf("workQueueButton") >= 0) failures.Add("hud-areas.json still carries the retired workQueueButton row (owner 2026-08-01: Queues entry = the Builders chip): " + p);
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

        // ── 9. WO-864 card rail — the four defects from the owner's 2026-08-03 capture ──
        // Her live Seeker screen read:
        //     Builders 1/2 / 3m 13s / > Tower Arcane Spire / 3m 13s
        // i.e. the SAME countdown twice, and builder slot 2 (idle) drawing nothing at all.
        private static void CheckCardRail(List<string> failures, StringBuilder log)
        {
            var railT = typeof(DeNelle.Core.UI.QueueRailView);

            // (a) THE REUSABLE COMPONENT — Build(mount, channel, options) is the one entry
            //     point a future host (the Manage screen under Bag) writes against, and
            //     HeightOf lets a host size its row BEFORE building.
            var build = railT.GetMethod("Build", new[]
            {
                typeof(RectTransform), typeof(ChannelId), typeof(DeNelle.Core.UI.QueueRailView.Options)
            });
            if (build == null)
                failures.Add("QueueRailView.Build(RectTransform,ChannelId,Options) missing — the rail is not host-agnostic");
            if (railT.GetMethod("HeightOf", BindingFlags.Public | BindingFlags.Static) == null)
                failures.Add("QueueRailView.HeightOf(Options) missing — hosts cannot size a rail row up front");

            // (b) NO DUPLICATED TIMER. The always-on chip must not print a countdown at all;
            //     exactly one surface (the card) owns it. Source oracle on the HUD seat.
            string kitPath = Path.Combine(Application.dataPath, "_Modules/HUD/Kit/HudKitController.cs");
            if (File.Exists(kitPath))
            {
                string src = File.ReadAllText(kitPath);
                int chipAt = src.IndexOf("private static string FormatQueueChip");
                if (chipAt < 0)
                    failures.Add("HudKitController.FormatQueueChip missing (the Builders chip summary)");
                else
                {
                    // Slice to the next member declaration (NOT to a literal close-brace —
                    // an unpaired brace in a string literal trips the CLAUDE.md §1 gate).
                    int end = src.IndexOf("\n        private", chipAt + 10);
                    string body = end > chipAt ? src.Substring(chipAt, end - chipAt) : src.Substring(chipAt);
                    if (body.IndexOf("SoonestRemainingSec") >= 0)
                        failures.Add("HudKitController.FormatQueueChip still prints SoonestRemainingSec — the chip header and the job card would BOTH show the same countdown (owner capture 2026-08-03: '3m 13s' twice)");
                }
                if (src.IndexOf("FormatQueueRows") >= 0)
                    failures.Add("HudKitController still carries FormatQueueRows — the WC3 text rows were replaced by QueueRailView; two queue visuals would disagree");
                if (src.IndexOf("QueueRailView.Build") < 0)
                    failures.Add("HudKitController does not host QueueRailView — the always-on Builders panel is the surface the owner actually sees");
            }
            else failures.Add("HudKitController.cs missing at " + kitPath);

            // (c) A FREE SLOT RENDERS A VISIBLE EMPTY-SLOT CARD. With 1 of 2 builders busy
            //     the model must be 2 cards, the second flagged Free with text-encoded state
            //     ("FREE" / "--") — never blank space, and never colour-only (owner is
            //     red/green colourblind).
            var st = new DeNelle.Core.UI.ObsidianQueueGate.WorkQueueStatus
            {
                Available = true,
                BuilderBusy = 1,
                BuilderSlots = 2,
                Entries = new[]
                {
                    new DeNelle.Core.UI.ObsidianQueueGate.QueueEntry
                    {
                        Label = "Arcane Spire", Verb = "BUILD", JobId = "tower_arcane_spire@15_7",
                        RemainingSec = 193, Queued = false, StackCount = 1,
                    },
                },
            };
            var model = InvokeCardModel(st, ChannelId.Builder, failures);
            if (model != null)
            {
                if (model.Length != 2)
                    failures.Add($"free-slot card missing: 1 of 2 builders busy produced {model.Length} card(s), expected 2 (the idle slot drew nothing on the owner's screen)");
                else
                {
                    if (!model[1].Free)
                        failures.Add("the second builder card is not flagged Free — an idle slot must READ as an idle slot");
                    string t = DeNelle.Core.UI.QueueRailView.TimerText(model[1]);
                    if (string.IsNullOrEmpty(t))
                        failures.Add("free-slot card has an EMPTY timer band — blank space is the bug, not the fix");
                    if (string.IsNullOrEmpty(model[1].Verb))
                        failures.Add("free-slot card has no verb — state must be text-encoded, never colour-only");
                }
                // The running card owns the ONE countdown, in ASCII.
                string active = DeNelle.Core.UI.QueueRailView.TimerText(model[0]);
                if (active != "3m 13s")
                    failures.Add("active card countdown formatted '" + active + "', expected '3m 13s' (193s)");
                foreach (var c in active ?? "")
                    if (c > '~') { failures.Add("card countdown carries a non-ASCII glyph ('" + active + "') — the SDF font tofus it"); break; }
            }

            // (d) A QUEUED job reads as QUEUED in TEXT, not by dimming alone.
            var queued = new DeNelle.Core.UI.ObsidianQueueGate.QueueEntry
            { Label = "Barracks", Verb = "UPGRADE", Queued = true, RemainingSec = -1, StackCount = 1 };
            if (DeNelle.Core.UI.QueueRailView.TimerText(queued) != "QUEUED")
                failures.Add("a queued card does not spell out QUEUED — colour-only state is banned");

            // (e) N IDENTICAL TROOP TRAINS COLLAPSE TO ONE CARD + xN BADGE. Three pending
            //     footmen must publish as ONE entry with StackCount 3, not three cards
            //     repeating the same word.
            var trains = PublishTrainEntries(3, failures);
            if (trains != null)
            {
                if (trains.Length != 1)
                    failures.Add($"3 identical queued footman trains published {trains.Length} card(s), expected 1 collapsed card with an xN badge");
                else if (trains[0].StackCount != 3)
                    failures.Add($"collapsed troop card carries StackCount {trains[0].StackCount}, expected 3 (the 'x3' badge would be wrong)");
                else if (trains[0].Label != null && trains[0].Label.EndsWith(" x1"))
                    failures.Add("collapsed troop card label still says 'x1' while the badge says xN — contradictory counts");
            }

            // (f) A MISSING PORTRAIT FALLS BACK TO THE VERB, NEVER A BLANK CARD. Measured
            //     coverage is ~76% of queueable jobs, so this is the COMMON case, not an
            //     edge case — the card is designed verb-first for exactly this reason.
            var noArt = new DeNelle.Core.UI.ObsidianQueueGate.QueueEntry
            { Label = "Stone Wall", Verb = "UPGRADE", JobId = "wall_stone@3_4", TargetTier = 2, StackCount = 1 };
            if (DeNelle.Core.UI.QueueIconResolver.Resolve(noArt) != null)
                log.AppendLine("  note: wall_stone resolved art (a portrait was added since the 2026-08-03 audit)");
            if (string.IsNullOrEmpty(noArt.Verb))
                failures.Add("a portrait-less card has no verb to fall back to — it would render blank");
            // The tower_arcane_spire case from the owner's screen: the category-token strip
            // is the ONLY step that reaches arcane-spire.png from that id.
            var spire = new DeNelle.Core.UI.ObsidianQueueGate.QueueEntry
            { Label = "Arcane Spire", Verb = "BUILD", JobId = "tower_arcane_spire@15_7", StackCount = 1 };
            if (DeNelle.Core.UI.QueueIconResolver.Resolve(spire) == null)
                failures.Add("QueueIconResolver did not reach Portraits/arcane-spire for 'tower_arcane_spire@15_7' — the leading category-token strip regressed");

            // ── WO-883 polish pins (owner capture 2026-08-04, QueueCardRail_2340x1080.png) ──

            // (g) THE NAME IS MEASURED, NOT LEFT TO AUTO-SIZE. The capture read "Arcane S..."
            //     at FULL SIZE in a card wide enough to seat the whole of "Arcane Spire" a
            //     point or two smaller. StretchLabel sets enableAutoSizing AND overflowMode =
            //     Ellipsis, and in THAT combination TMP truncates instead of shrinking — so
            //     the auto-size floor never fired horizontally and the WO-864 comment claiming
            //     it would was wrong. The card must measure the string with TMP's own metrics
            //     and set the size, keeping the ellipsis as the last resort only.
            string railPath = Path.Combine(Application.dataPath, "_Modules/Core/UI/QueueRailView.cs");
            if (!File.Exists(railPath))
            {
                failures.Add("QueueRailView.cs missing at " + railPath);
            }
            else
            {
                string railSrc = File.ReadAllText(railPath);
                if (railSrc.IndexOf("FitToWidth") < 0)
                    failures.Add("QueueRailView lost the card-name width fit (FitToWidth) — building names " +
                                 "truncate mid-word at full size again ('Arcane S...', WO-883)");
                if (railSrc.IndexOf("GetPreferredValues") < 0)
                    failures.Add("QueueRailView no longer MEASURES the card name (TMP GetPreferredValues) before " +
                                 "sizing it — auto-size + Ellipsis TRUNCATES rather than shrinks, which is exactly " +
                                 "the defect WO-883 fixed; a fit that does not measure is a guess");
                // NOTE: no blanket ASCII sweep on this file. Its comment banners are drawn with
                // box-drawing characters, so a source-wide check would fail forever; the ASCII
                // law that matters is on the RENDERED strings, and (c) above already asserts
                // that on the real countdown output.
                if (railSrc.IndexOf('\0') >= 0)
                    failures.Add("QueueRailView.cs contains an embedded NUL byte (mount-garble, CLAUDE.md Sec.0)");
            }

            // (h) THE CHANNEL HEADER CLEARS THE CARDS. AddStretchLabel seats the header text in
            //     0.05..0.95 of HeaderHeightPx and renders it at ElarionUi.FontBody, whose line
            //     box is ~1.2em. At 66px the line got 59.4px — a hair under its own box — so the
            //     descenders of "...busy" spilled below the row and the NEXT row's opaque card
            //     plate painted over them. Demand the box PLUS a visible gap, not just non-clip.
            float headerPx = PrivateConstFloat(typeof(ObsidianQueueHud), "HeaderHeightPx", failures);
            if (headerPx > 0f)
            {
                const float lineBoxMul = 1.2f;    // TMP line box for the shipped SDF face
                const float bandFrac = 0.90f;     // the label's 0.05..0.95 seat inside the row
                const float gapPx = 8f;           // the "few px of gap" WO-883 asks for
                float needed = (DeNelle.Core.UI.ElarionUi.FontBody * lineBoxMul + gapPx) / bandFrac;
                if (headerPx < needed)
                    failures.Add($"ObsidianQueueHud.HeaderHeightPx={headerPx} cannot seat a FontBody(" +
                                 DeNelle.Core.UI.ElarionUi.FontBody + $") line box plus a gap inside its " +
                                 $"0.05-0.95 label band (needs >= {needed:F0}) — the '...busy' descenders spill " +
                                 "under the row and the first card's plate paints over them (WO-883)");
            }

            log.AppendLine("  WO-864 card rail (reusable Build/HeightOf + no duplicate timer + free-slot card + " +
                           "queued text + xN collapse + verb fallback) + WO-883 polish (measured name fit + " +
                           "header clears the cards) OK-checked");
        }

        /// <summary>Read a private const float (a compile-time literal field) by reflection —
        /// the layout constants these oracles pin are deliberately not public API.</summary>
        private static float PrivateConstFloat(System.Type t, string name, List<string> failures)
        {
            var f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null)
            {
                failures.Add($"{t.Name}.{name} does not exist — the layout constant this oracle pins was renamed " +
                             "or removed; re-point it rather than dropping the guard");
                return 0f;
            }
            object v = f.GetValue(null);
            if (v is float fv) return fv;
            if (v is int iv) return iv;
            if (v is double dv) return (float)dv;
            failures.Add($"{t.Name}.{name} is not a numeric constant");
            return 0f;
        }

        // Reach QueueRailView's private card model (snapshot entries + the FREE slots it
        // derives from SlotCount) without spinning up a Canvas.
        private static DeNelle.Core.UI.ObsidianQueueGate.QueueEntry[] InvokeCardModel(
            DeNelle.Core.UI.ObsidianQueueGate.WorkQueueStatus st, ChannelId ch, List<string> failures)
        {
            var m = typeof(DeNelle.Core.UI.QueueRailView).GetMethod("BuildCardModel",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (m == null)
            {
                failures.Add("QueueRailView.BuildCardModel missing — free slots would not become cards");
                return null;
            }
            return (DeNelle.Core.UI.ObsidianQueueGate.QueueEntry[])m.Invoke(null, new object[] { st, ch });
        }

        // Drive the REAL publisher collapse path (BuildTimerService.BuildEntries) is
        // instance-bound, so mirror its stack key here against the REAL BarracksService
        // prefixes and assert the collapse contract the publisher implements.
        private static DeNelle.Core.UI.ObsidianQueueGate.QueueEntry[] PublishTrainEntries(
            int count, List<string> failures)
        {
            var svcT = typeof(DeNelle.Village.BuildTimerService);
            var stackKey = svcT.GetMethod("StackKey", BindingFlags.NonPublic | BindingFlags.Static);
            var makeEntry = svcT.GetMethod("MakeEntry", BindingFlags.NonPublic | BindingFlags.Static);
            if (stackKey == null || makeEntry == null)
            {
                failures.Add("BuildTimerService.StackKey/MakeEntry missing — troop trains cannot collapse to an xN card");
                return null;
            }

            var outp = new List<DeNelle.Core.UI.ObsidianQueueGate.QueueEntry>();
            for (int i = 0; i < count; i++)
            {
                var job = new BuildJobData
                {
                    StructureId = BarracksService.TrainPrefix + "troop-footman:uid" + i,
                    Kind = (int)JobKind.TrainTroop,
                    Channel = (int)ChannelId.Train,
                    StartMs = 0,
                    DurationMs = 90000,
                };
                var e = (DeNelle.Core.UI.ObsidianQueueGate.QueueEntry)makeEntry.Invoke(null, new object[] { job });
                e.Queued = true; e.RemainingSec = -1;

                string key = (string)stackKey.Invoke(null, new object[] { job.StructureId });
                int merged = -1;
                if (key != null)
                    for (int j = 0; j < outp.Count; j++)
                        if (string.Equals((string)stackKey.Invoke(null, new object[] { outp[j].JobId }), key,
                                          System.StringComparison.Ordinal))
                        { merged = j; break; }

                if (merged >= 0) { var m2 = outp[merged]; m2.StackCount++; outp[merged] = m2; }
                else outp.Add(e);
            }
            // Distinct troops must NOT collapse into each other.
            string kA = (string)stackKey.Invoke(null, new object[] { BarracksService.TrainPrefix + "troop-archer:z1" });
            string kF = (string)stackKey.Invoke(null, new object[] { BarracksService.TrainPrefix + "troop-footman:z1" });
            if (kA == kF) failures.Add("StackKey collapses DIFFERENT troops onto one card (archer would badge as a footman)");
            // A placed structure must never collapse — every one is real, distinct work.
            if ((string)stackKey.Invoke(null, new object[] { "forge@1_2" }) != null)
                failures.Add("StackKey collapses structure jobs — two different forges would hide behind one card");
            return outp.ToArray();
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
