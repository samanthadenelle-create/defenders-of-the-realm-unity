// =============================================================================
// ObsidianQueueTests (EditMode) — the common "Obsidian" multi-channel work queue
// (WO-773). Behavioral engine tests + a v34→v35 save migration round-trip.
// -----------------------------------------------------------------------------
// Drives the PURE DeNelle.Core.Jobs.ObsidianQueueEngine with a simulated wall-clock
// (advancing nowMs = "ticking the TimeSource") so the slot cap, FIFO pull order,
// effect-on-completion, offline catch-up cascade AND channel independence are proven
// headlessly — no MonoBehaviour / GameStateService / real clock needed.
//
// Plus the highest-risk piece: a v34 save carrying in-progress BuildJobs migrates to
// the v35 ObsidianQueue (Builder channel) and round-trips through JSON with no loss.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class ObsidianQueueTests
    {
        private static BuildJobData Job(string id, JobKind kind, double durationMs, ChannelId channel = ChannelId.Builder)
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

        // =====================================================================
        //  Slot cap + FIFO queueing
        // =====================================================================
        [Test]
        public void enqueue_beyond_slots_caps_active_and_queues_the_rest()
        {
            var ch = new ChannelState();
            const int slots = 2;
            double now = 1000;

            Assert.That(ObsidianQueueEngine.Enqueue(ch, slots, Job("a", JobKind.Build, 100), now), Is.True, "1st starts");
            Assert.That(ObsidianQueueEngine.Enqueue(ch, slots, Job("b", JobKind.Build, 100), now), Is.True, "2nd starts");
            Assert.That(ObsidianQueueEngine.Enqueue(ch, slots, Job("c", JobKind.Build, 100), now), Is.False, "3rd queues");
            Assert.That(ObsidianQueueEngine.Enqueue(ch, slots, Job("d", JobKind.Build, 100), now), Is.False, "4th queues");

            Assert.That(ch.ActiveJobs.Count, Is.EqualTo(2), "active slot cap honored");
            Assert.That(ch.PendingQueue.Count, Is.EqualTo(2), "overflow queued FIFO");
            Assert.That(ch.PendingQueue[0].StructureId, Is.EqualTo("c"), "FIFO head is the 3rd enqueued");
            Assert.That(ch.ActiveJobs[0].StartMs, Is.EqualTo(now), "active job started at now");
            Assert.That(ch.PendingQueue[0].StartMs, Is.EqualTo(0), "pending job not started (StartMs 0)");
        }

        // =====================================================================
        //  Auto-pull + FIFO completion order + effect-on-completion
        // =====================================================================
        [Test]
        public void resolve_completes_in_order_autopulls_and_fires_effect()
        {
            var ch = new ChannelState();
            const int slots = 1;   // one slot forces strict serial FIFO
            double now = 1000;

            ObsidianQueueEngine.Enqueue(ch, slots, Job("a", JobKind.Build, 100), now);
            ObsidianQueueEngine.Enqueue(ch, slots, Job("b", JobKind.Build, 100), now);
            ObsidianQueueEngine.Enqueue(ch, slots, Job("c", JobKind.Build, 100), now);
            Assert.That(ch.ActiveJobs.Count, Is.EqualTo(1), "one slot");
            Assert.That(ch.PendingQueue.Count, Is.EqualTo(2), "two queued");

            var completed = new List<string>();

            // At now=1100 'a' finishes; 'b' auto-pulls (starts at 1100, finishes 1200) — not yet due.
            int n = ObsidianQueueEngine.Resolve(ch, slots, 1100, j => completed.Add(j.StructureId));
            Assert.That(n, Is.EqualTo(1), "only 'a' due at t=1100");
            Assert.That(completed, Is.EqualTo(new[] { "a" }));
            Assert.That(ch.ActiveJobs.Count, Is.EqualTo(1), "'b' auto-pulled into the freed slot");
            Assert.That(ch.ActiveJobs[0].StructureId, Is.EqualTo("b"));

            // Advance so 'b' (finish 1200) and its successor 'c' both resolve.
            n = ObsidianQueueEngine.Resolve(ch, slots, 5000, j => completed.Add(j.StructureId));
            Assert.That(n, Is.EqualTo(2), "'b' then 'c' complete");
            Assert.That(completed, Is.EqualTo(new[] { "a", "b", "c" }), "strict FIFO completion order");
            Assert.That(ch.ActiveJobs.Count, Is.EqualTo(0));
            Assert.That(ch.PendingQueue.Count, Is.EqualTo(0));
        }

        // =====================================================================
        //  Offline catch-up — a long gap resolves the whole chain in one Resolve
        // =====================================================================
        [Test]
        public void offline_gap_cascades_the_whole_queue_in_one_resolve()
        {
            var ch = new ChannelState();
            const int slots = 1;
            double now = 1000;   // a realistic wall-clock base (0 would collide with the StartMs=0 "pending" sentinel)
            for (int i = 0; i < 5; i++)
                ObsidianQueueEngine.Enqueue(ch, slots, Job("j" + i, JobKind.Build, 100), now);

            var completed = new List<string>();
            // Come back "online" far in the future: every job (serial, 100ms each) is long done.
            int n = ObsidianQueueEngine.Resolve(ch, slots, 1_000_000, j => completed.Add(j.StructureId));

            Assert.That(n, Is.EqualTo(5), "offline sweep completes the whole chain");
            Assert.That(completed, Is.EqualTo(new[] { "j0", "j1", "j2", "j3", "j4" }), "in FIFO order");
            Assert.That(ch.ActiveJobs.Count + ch.PendingQueue.Count, Is.EqualTo(0), "queue drained");
        }

        [Test]
        public void pending_jobs_never_complete_before_they_start()
        {
            var ch = new ChannelState();
            const int slots = 1;
            double now = 1000;
            ObsidianQueueEngine.Enqueue(ch, slots, Job("running", JobKind.Build, 10_000), now);   // long
            ObsidianQueueEngine.Enqueue(ch, slots, Job("waiting", JobKind.Build, 100), now);      // queued

            var completed = new List<string>();
            // now=2000: the running job (finish 11000) is NOT due; the queued one must NOT complete
            // despite its tiny duration — it hasn't started (StartMs 0).
            int n = ObsidianQueueEngine.Resolve(ch, slots, 2000, j => completed.Add(j.StructureId));
            Assert.That(n, Is.EqualTo(0), "nothing due");
            Assert.That(completed, Is.Empty, "a pending job never completes before it starts");
            Assert.That(ch.PendingQueue.Count, Is.EqualTo(1), "still queued");
        }

        // =====================================================================
        //  Channel independence — train a troop WHILE a wall upgrades (owner ask)
        // =====================================================================
        [Test]
        public void channels_do_not_share_slots_train_while_wall_upgrades()
        {
            // Two separate single-slot channels: Builder runs a wall upgrade, Train runs a troop.
            var builder = new ChannelState();
            var train = new ChannelState();
            double now = 1000;

            bool wallStarted = ObsidianQueueEngine.Enqueue(builder, 1,
                Job("wall_north", JobKind.WallUpgrade, 500, ChannelId.Builder), now);
            bool troopStarted = ObsidianQueueEngine.Enqueue(train, 1,
                Job("swordsman", JobKind.TrainTroop, 500, ChannelId.Train), now);

            // Both START immediately even though each channel has only ONE slot — they don't compete.
            Assert.That(wallStarted, Is.True, "wall upgrade runs on the Builder channel");
            Assert.That(troopStarted, Is.True, "troop trains CONCURRENTLY on the Train channel");
            Assert.That(builder.ActiveJobs.Count, Is.EqualTo(1));
            Assert.That(train.ActiveJobs.Count, Is.EqualTo(1));
            Assert.That(builder.PendingQueue.Count, Is.EqualTo(0), "nothing queued — channels are independent");
            Assert.That(train.PendingQueue.Count, Is.EqualTo(0));

            // Both resolve at their own finish time, independently.
            var done = new List<string>();
            ObsidianQueueEngine.Resolve(builder, 1, 2000, j => done.Add(j.StructureId));
            ObsidianQueueEngine.Resolve(train, 1, 2000, j => done.Add(j.StructureId));
            Assert.That(done, Does.Contain("wall_north"));
            Assert.That(done, Does.Contain("swordsman"));
        }

        [Test]
        public void job_effect_registry_dispatches_by_kind_and_no_ops_unregistered()
        {
            JobEffectRegistry.Clear();
            var applied = new List<string>();
            JobEffectRegistry.Register(new FakeEffect(JobKind.TrainTroop, j => applied.Add(j.StructureId)));

            JobEffectRegistry.Apply(Job("swordsman", JobKind.TrainTroop, 100, ChannelId.Train));
            JobEffectRegistry.Apply(Job("tower", JobKind.Build, 100));   // no handler → no-op

            Assert.That(applied, Is.EqualTo(new[] { "swordsman" }), "only the registered kind applies");
            JobEffectRegistry.Clear();
        }

        private sealed class FakeEffect : IJobEffect
        {
            private readonly System.Action<BuildJobData> _apply;
            public FakeEffect(JobKind kind, System.Action<BuildJobData> apply) { Kind = kind; _apply = apply; }
            public JobKind Kind { get; }
            public void Apply(BuildJobData job) => _apply(job);
        }

        // =====================================================================
        //  SAVE ROUND-TRIP — a v34 save with in-progress BuildJobs migrates to the
        //  v35 ObsidianQueue (Builder channel) and round-trips through JSON, no loss.
        // =====================================================================
        [Test]
        public void v34_buildjobs_migrate_to_v35_builder_channel_and_round_trip()
        {
            var v34 = new SaveSchema.PersistedState
            {
                BuildJobs = new List<BuildJobData>
                {
                    new BuildJobData { StructureId = "forge@1_2", JobType = (int)BuildJobType.Build,   StartMs = 5000, DurationMs = 60000, TargetTier = 0 },
                    new BuildJobData { StructureId = "tower@3_4", JobType = (int)BuildJobType.Upgrade, StartMs = 6000, DurationMs = 90000, TargetTier = 2 },
                },
            };

            var migrated = SaveMigrator.Migrate(v34, 34);

            Assert.That(migrated.ObsidianQueue, Is.Not.Null, "v34→v35 builds the ObsidianQueue");
            var builder = migrated.ObsidianQueue.Channel(ChannelId.Builder);
            Assert.That(builder.ActiveJobs.Count, Is.EqualTo(2), "both in-flight builds folded into the Builder channel");
            Assert.That(migrated.BuildJobs, Is.Empty, "legacy buildJobs cleared (single source of truth)");

            var forge = builder.ActiveJobs.Find(j => j.StructureId == "forge@1_2");
            var tower = builder.ActiveJobs.Find(j => j.StructureId == "tower@3_4");
            Assert.That(forge.JobKind, Is.EqualTo(JobKind.Build), "Build jobType backfilled to JobKind.Build");
            Assert.That(tower.JobKind, Is.EqualTo(JobKind.Upgrade), "Upgrade jobType backfilled to JobKind.Upgrade");
            Assert.That(tower.ChannelId, Is.EqualTo(ChannelId.Builder), "folded onto the Builder channel");
            Assert.That(tower.DurationMs, Is.EqualTo(90000), "no duration lost");
            Assert.That(tower.TargetTier, Is.EqualTo(2), "upgrade target tier preserved (no lost in-progress upgrade)");

            // Round-trip through JSON (the real save serializer settings) with no loss.
            string json = JsonConvert.SerializeObject(migrated, SaveSchema.JsonSettings);
            var back = JsonConvert.DeserializeObject<SaveSchema.PersistedState>(json, SaveSchema.JsonSettings);

            Assert.That(back.ObsidianQueue, Is.Not.Null, "queue survives serialization");
            var backBuilder = back.ObsidianQueue.Channel(ChannelId.Builder);
            Assert.That(backBuilder.ActiveJobs.Count, Is.EqualTo(2), "both jobs survive the round-trip");
            var backTower = backBuilder.ActiveJobs.Find(j => j.StructureId == "tower@3_4");
            Assert.That(backTower.JobKind, Is.EqualTo(JobKind.Upgrade), "kind survives round-trip");
            Assert.That(backTower.DurationMs, Is.EqualTo(90000), "duration survives round-trip");
            Assert.That(backTower.TargetTier, Is.EqualTo(2), "target tier survives round-trip");
        }
    }
}
