// =============================================================================
// DynamicDifficulty -- the game-facing facade, plus a THIN MonoBehaviour host.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Adaptive
//
// SPLIT ON PURPOSE (the whole reason this system is testable at all):
//   DifficultyMath          pure static arithmetic, System.Math only
//   DynamicDifficultyState  pure mutable tracker, clock passed IN as a parameter
//   DynamicDifficulty       this file -- the ambient instance + the game-facing reads
//   DynamicDifficultyHost   a MonoBehaviour that owns LIFECYCLE AND TELEMETRY ONLY
//
// The reference sketch put everything in two DontDestroyOnLoad MonoBehaviour
// singletons. Nothing there can be driven from an EditMode test: no history can be
// injected, no clock can be advanced, so "deterministic / AutoPilot / regression
// friendly" would have been an unbacked claim. Here every one of the owner's feel
// rows is a headless assertion.
//
// THE HOST DOES NOT TICK THE SPIKE. Spike expiry is an absolute TIMESTAMP compared
// at read time (DifficultyMath.IsSpikeActive), so it is exact and can never be
// missed. The host's Update exists solely to notice spike EDGES and emit one
// FlowTrace line each, so the F8 break-capture harness can see a spike start and
// end in the trace -- a real job, and the only thing here that genuinely needs a
// frame. If that telemetry is ever unwanted, deleting the host changes NO
// gameplay behaviour whatsoever.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Adaptive
{
    /// <summary>
    /// Ambient dynamic-difficulty instance for the running game. Integration sites read
    /// the multiplier properties; the wave/raid/boss completion sites call
    /// <see cref="RecordEncounter"/>.
    /// </summary>
    public static class DynamicDifficulty
    {
        private static DynamicDifficultyState _state;

        /// <summary>
        /// The clock, injectable. Defaults to unscaled game time. Overriding it is how the
        /// oracle and EditMode tests advance to a spike expiry without waiting 45 real
        /// seconds -- and unscaled means a pause menu does not extend a spike.
        /// </summary>
        public static System.Func<double> Clock = DefaultClock;

        private static double DefaultClock()
        {
            return Time.unscaledTimeAsDouble;
        }

        /// <summary>The live tracker (created on first touch from the loaded profile).</summary>
        public static DynamicDifficultyState State
        {
            get
            {
                if (_state == null) _state = new DynamicDifficultyState(DifficultyProfileCatalog.Profile);
                return _state;
            }
        }

        /// <summary>Current clock reading through the injectable <see cref="Clock"/>.</summary>
        public static double Now
        {
            get
            {
                var c = Clock;
                return c != null ? c() : 0d;
            }
        }

        /// <summary>Rebuilds the tracker against the currently loaded profile and clears
        /// history. Call on a NEW GAME so one player's history never scales another's run.</summary>
        public static void ResetForNewGame()
        {
            _state = new DynamicDifficultyState(DifficultyProfileCatalog.Profile);
            FlowTrace.Step("Difficulty", "DynamicDifficulty reset for a new game (history cleared, multiplier 1.000).");
        }

        /// <summary>
        /// Records one finished encounter. Call from the wave-cleared / raid-ended /
        /// boss-defeated sites. Logs the resulting readout so the F8 harness can show WHY
        /// difficulty moved, rather than the player feeling an unexplained change.
        /// </summary>
        public static void RecordEncounter(EncounterSample sample)
        {
            double now = Now;
            bool spiked = State.Record(sample, now);
            FlowTrace.Step("Difficulty",
                "recorded " + sample + " -> " + State.Describe(now) + (spiked ? "  [SPIKE FIRED]" : ""));
        }

        // ---- Reads. Every one composes the spike at READ time. -----------------

        /// <summary>Base multiplier from history alone (never includes the spike).</summary>
        public static float BaseMultiplier { get { return State.BaseMultiplier; } }

        /// <summary>The live composed multiplier (base x active spike), clamped to the
        /// authored rails.</summary>
        public static float CurrentMultiplier { get { return State.CurrentMultiplier(Now); } }

        /// <summary>True while a pressure spike is live RIGHT NOW.</summary>
        public static bool IsSpikeActive { get { return State.IsSpikeActive(Now); } }

        /// <summary>Seconds of spike remaining (0 when none) -- for a HUD telegraph.</summary>
        public static double SpikeRemainingSeconds { get { return State.SpikeRemainingSeconds(Now); } }

        /// <summary>Current pressure 0..1 -- for a HUD telegraph.</summary>
        public static float Pressure { get { return State.Pressure; } }

        /// <summary>Enemy max-HP multiplier. Apply as <c>baseHp * mult</c>, NEVER <c>hp *= mult</c>.</summary>
        public static float EnemyHpMultiplier { get { return State.EnemyHpMultiplier(Now); } }

        /// <summary>Enemy damage multiplier. Apply as <c>baseDamage * mult</c>.</summary>
        public static float EnemyDamageMultiplier { get { return State.EnemyDamageMultiplier(Now); } }

        /// <summary>Wave enemy-count multiplier. Apply as <c>baseCount * mult</c>.</summary>
        public static float EnemyCountMultiplier { get { return State.EnemyCountMultiplier(Now); } }

        /// <summary>Boss max-HP multiplier (softer curve). Apply as <c>baseHp * mult</c>.</summary>
        public static float BossHpMultiplier { get { return State.BossHpMultiplier(Now); } }

        /// <summary>Boss damage multiplier (softer curve). Apply as <c>baseDamage * mult</c>.</summary>
        public static float BossDamageMultiplier { get { return State.BossDamageMultiplier(Now); } }

        /// <summary>One-line readout for FlowTrace / the F8 harness / a dev overlay.</summary>
        public static string Describe() { return State.Describe(Now); }
    }

    /// <summary>
    /// LIFECYCLE + TELEMETRY ONLY. Owns no arithmetic and no state that matters: deleting
    /// this component changes no gameplay behaviour, because spike expiry is a timestamp
    /// compared at read time rather than a countdown ticked here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DynamicDifficultyHost : MonoBehaviour
    {
        private static DynamicDifficultyHost s_instance;
        private bool _sawSpike;

        /// <summary>
        /// Creates the host once per process. Safe to call repeatedly.
        ///
        /// DELIBERATELY NOT [RuntimeInitializeOnLoadMethod]. Nothing calls
        /// <see cref="DynamicDifficulty.RecordEncounter"/> until the integration patch lands,
        /// so an auto-spawned DontDestroyOnLoad object with an Update() would add a
        /// per-frame no-op to every scene in a tree that currently has eleven lanes in
        /// flight. The integration site calls this explicitly when it wires up.
        /// </summary>
        public static void Bootstrap()
        {
            if (s_instance != null) return;
            var go = new GameObject("DynamicDifficultyHost");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<DynamicDifficultyHost>();
            FlowTrace.Step("Difficulty", "DynamicDifficultyHost online -- " + DynamicDifficulty.Describe());
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
            s_instance = this;
        }

        private void Update()
        {
            // EDGE DETECTION ONLY. This does NOT drive expiry -- IsSpikeActive is derived
            // from an absolute timestamp, so the spike is already over the instant it is
            // due whether or not this ever runs.
            bool live = DynamicDifficulty.IsSpikeActive;
            if (live == _sawSpike) return;
            _sawSpike = live;
            FlowTrace.Step("Difficulty", live
                ? "PRESSURE SPIKE started -- " + DynamicDifficulty.Describe()
                : "pressure spike ENDED -- " + DynamicDifficulty.Describe());
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
