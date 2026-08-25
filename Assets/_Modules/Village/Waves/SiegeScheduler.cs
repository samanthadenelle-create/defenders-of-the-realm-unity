// =============================================================================
// SiegeScheduler — WHEN the town gets attacked (WO-1026). PvE. Not PvP.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE PROBLEM THIS CLOSES: the player's base was never attacked, so the CoC
// consequence loop was open — the town was a place you decorated, never a place you
// could lose. This adds the missing half: attacks ARRIVE on a cadence, they are
// RECORDED, and the record is RE-OPENABLE so the player can redesign against it.
//
// ⛔ IT SPAWNS NOTHING. There is no Instantiate in this file and there never will be.
//    WaveManager already owns "hostiles attack the player's town" and is the only
//    thing that does. The ENTIRE spawn integration is one call to the manager's own
//    public player-facing entry point:
//        WaveManager.Instance.ForceBeginNextWave()
//    No composition change, no roster change, no second attacker. Two systems that
//    both attack the town drift apart — SiegeSpawnAuthorityRegression fails the gate
//    if a spawn call ever appears here.
//
// THE OFFLINE PATH IS DELIBERATELY *NOT* A SIMULATION (design call — flagged to the
// owner in the WO-1026 result):
//    ApplyOfflineWindow does NOT resolve battles. It converts the away window into
//    siege PRESSURE, and the siege then happens LIVE, at the gate, with the player
//    watching. WHY: resolving in absentia under the interim would write a report whose
//    Rows AND ResourcesLost are both empty — a record that says nothing happened, which is
//    worse than no record because it teaches the player the system is noise. Making
//    the away time PRODUCE THE ATTACK YOU COME HOME TO is honest, needs no combat sim,
//    and keeps WaveManager the single spawn authority. DefenseResolution
//    .ResolvedInAbsentia and DefenderSnapshot.Garrison exist in the data model so
//    WO-430-F's fast-forward drops in AT THIS METHOD with no data change — and 430-F
//    cannot proceed before the stakes ruling anyway (its whole design IS a stake).
//
// ⛔ IT NEVER TOUCHES GameState.LastHarvestClaimMs. The OfflineClaimCoordinator owns
//    that clock. WO-1147 recorded what happens when a second system writes it: a
//    frame-order coin-flip in which offline Echo repair never accrued ONCE. The siege
//    has its OWN clock (GameState.LastSiegeUnixMs) and SiegeCadenceRegression asserts
//    the harvest clock is untouched across a window.
// =============================================================================

using System;
using DeNelle.Core;
using DeNelle.Core.Defense;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Decides WHEN a siege arrives, opens the recorder, and files the report.</summary>
    [DisallowMultipleComponent]
    public sealed class SiegeScheduler : MonoBehaviour, IOfflineClaimConsumer
    {
        /// <summary>The live scheduler, or null outside the hub.</summary>
        public static SiegeScheduler Instance { get; private set; }

        [Header("Cadence (CONFIG, not save state)")]
        [Tooltip("Hours of play between sieges. Config, not persisted — the interval is a product " +
                 "decision, not player progress (mirrors BuildTimerConfig.queueDepthPerLine).")]
        [Min(0.05f)]
        [SerializeField] private float _siegeIntervalHours = 6f;

        [Tooltip("How many sieges can be BANKED from an away window. Deliberately small: coming " +
                 "home to a queue of five assaults is a punishment for playing, not a consequence.")]
        [Min(0)]
        [SerializeField] private int _maxPendingSieges = 1;

        [Tooltip("Away-window cap, in hours, for converting absence into siege pressure. A " +
                 "consumer applies its OWN cap — the coordinator never applies it for anyone.")]
        [Min(0f)]
        [SerializeField] private float _offlineCapHours = 24f;

        [Tooltip("Seconds between cadence evaluations. A cheap InvokeRepeating, never Update.")]
        [Min(1f)]
        [SerializeField] private float _evaluateEverySeconds = 15f;

        /// <summary>Sieges banked and waiting for a legal moment to fire. In-memory: it is derived
        /// from the persisted clock, so a long raid session still comes home to a siege.</summary>
        public int PendingSieges { get; private set; }

        /// <summary>Read-only cadence for the lookout presentation layer.</summary>
        public double SiegeIntervalMs => IntervalMs;

        /// <summary>The session currently being recorded, or null.</summary>
        private SiegeSession _session;
        private WaveManager _boundManager;

        private double IntervalMs => Math.Max(0.05, _siegeIntervalHours) * 3600.0 * 1000.0;

        string IOfflineClaimConsumer.OfflineConsumerName => "siege-pressure";

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            RoamingHordeNotifications.Attach(this);
            OfflineClaimCoordinator.Register(this);
            InvokeRepeating(nameof(Evaluate), 2f, Mathf.Max(1f, _evaluateEverySeconds));
            FlowTrace.Step("Siege",
                $"scheduler armed -- interval={_siegeIntervalHours}h maxPending={_maxPendingSieges} " +
                $"offlineCap={_offlineCapHours}h flag={FeatureFlags.Siege}");
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(Evaluate));
            OfflineClaimCoordinator.Unregister(this);
            UnbindManager();
            if (_session != null) { SiegeSession.Abandon("scheduler disabled"); _session = null; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        //  Offline — away time becomes PRESSURE, never a simulated battle
        // =====================================================================

        /// <summary>
        /// The coordinator's fan-out. Converts the away window into banked sieges.
        /// ⛔ MUST NOT touch GameState.LastHarvestClaimMs — the coordinator owns it.
        /// </summary>
        public void ApplyOfflineWindow(OfflineClaimWindow window)
        {
            var state = StateOrNull();
            if (state == null)
            {
                FlowTrace.Warn("Siege", "offline window ignored -- no GameStateService.State.");
                return;
            }

            // The cadence clock's arithmetic and its two self-healing cases (unseeded clock,
            // clock in the FUTURE) live in SiegeClock — see that file's header for why this
            // file may not touch the wall clock directly.
            double cappedSec = window.CappedSeconds(_offlineCapHours);
            int banked = SiegeClock.BankOfflinePressure(
                state, window.NowUnixMs, cappedSec, IntervalMs, out string note);

            if (note != null)
            {
                FlowTrace.Step("Siege", $"offline window: {note}.");
                return;
            }

            banked = Mathf.Clamp(banked, 0, Math.Max(0, _maxPendingSieges));

            PendingSieges = Mathf.Clamp(PendingSieges + banked, 0, Math.Max(0, _maxPendingSieges));

            FlowTrace.Step("Siege",
                $"offline window {cappedSec:F0}s -> pressure {PendingSieges} " +
                $"(banked {banked}, capped={window.ExceedsCap(_offlineCapHours)}, seq={window.Sequence}).");
        }

        // =====================================================================
        //  Cadence — the ONE place a siege is armed
        // =====================================================================

        /// <summary>
        /// Cheap periodic evaluation. Every path that does NOT arm a siege says WHY — a deferral
        /// that logs nothing recreates the exact bug this WO closes ("the base is never attacked",
        /// with no evidence for which gate refused).
        /// </summary>
        public void Evaluate()
        {
            if (!Defer(out string why))
            {
                Arm();
                return;
            }
            FlowTrace.Throttle("Siege", "deferred", 30f, $"deferred: {why}");
        }

        /// <summary>Returns true (with a reason) when a siege must NOT fire right now.</summary>
        private bool Defer(out string why)
        {
            why = null;

            if (!FeatureFlags.Siege)
            { why = "ff.siege OFF (turned off deliberately -- the loop is complete; see FeatureFlags.Siege)"; return true; }

            if (_session != null)
            { why = $"a siege for wave {_session.WaveId} is already in progress"; return true; }

            string scene = SceneManager.GetActiveScene().name;
            if (!HubScenes.IsHub(scene))
            { why = $"active scene '{scene}' is not the hub (a raid/dungeon/battle never fires one)"; return true; }

            var wm = WaveManager.Instance;
            if (wm == null)
            { why = "no WaveManager.Instance in the active scene"; return true; }

            if (wm.Phase != WavePhase.Idle && wm.Phase != WavePhase.Countdown)
            { why = $"WaveManager is busy (phase={wm.Phase})"; return true; }

            // THE ONBOARDING GATE. Memory `enemies-never-spawn-tutorial-onboarded-gate` records
            // this as a RECURRING bug whose real gate is !Onboarded (not the dead pausePressure
            // flag). We reuse the state field WaveManager's own BeginLoop guard reads rather than
            // writing a second predicate — a second predicate is how that bug returns in a new hat.
            var state = StateOrNull();
            if (state == null)
            { why = "no GameStateService.State (cannot check the onboarding gate)"; return true; }
            if (!state.Onboarded)
            { why = "onboarding not finished (!Onboarded) -- the FTUE owns the town until it is"; return true; }

            // Cadence: banked pressure fires immediately; otherwise the interval must have elapsed.
            if (PendingSieges > 0) return false;

            // ⛔ The wall-clock read lives in SiegeClock, one directory over, NOT here. This
            //    file is inside the combat firewall's swept tree (DevTimeSkipRegression case6):
            //    nothing under Village/Waves may touch the skippable clock, or the DEV queue
            //    time-skip would warp a live battle. Everything this class times DURING an
            //    assault is engine time. Read SiegeClock's header before changing this.
            return SiegeClock.CadenceDefer(state, IntervalMs, out why);
        }

        /// <summary>
        /// Opens the recorder, stamps the cadence clock, and asks WaveManager to begin.
        /// THIS IS THE WHOLE SPAWN INTEGRATION.
        /// </summary>
        private void Arm()
        {
            var wm = WaveManager.Instance;
            var state = StateOrNull();
            if (wm == null || state == null) return;

            Guard.Try("Siege", "arm siege", () =>
            {
                int waveId = wm.CurrentWaveId + 1;
                var attacker = DefenseReportBuilder.BuildPveAttacker(waveId);
                var defender = DefenseReportBuilder.CaptureDefender();

                _session = SiegeSession.Open(waveId, attacker, defender);

                SiegeClock.StampFired(state);
                if (PendingSieges > 0) PendingSieges--;

                BindManager(wm);

                FlowTrace.Step("Siege",
                    $"ARMED wave={waveId} attacker={attacker.DisplayName} source={attacker.Source} " +
                    $"pendingLeft={PendingSieges} layout={defender.LayoutHash}");

                // ── The entire spawn integration: the manager's own public entry point. ──
                wm.ForceBeginNextWave();
            });
        }

        // =====================================================================
        //  Outcome — Close, then file
        // =====================================================================

        private void BindManager(WaveManager wm)
        {
            if (ReferenceEquals(_boundManager, wm)) return;
            UnbindManager();
            _boundManager = wm;
            // WaveManager.cs's own contract note: the payout record is stamped BEFORE any
            // OnWaveCleared listener runs, so the order between this listener and
            // WaveCelebrationManager's does not matter for correctness. Do not reorder them.
            _boundManager.OnWaveCleared.AddListener(HandleWaveCleared);
            _boundManager.OnDefeat.AddListener(HandleDefeat);
        }

        private void UnbindManager()
        {
            if (_boundManager == null) return;
            _boundManager.OnWaveCleared.RemoveListener(HandleWaveCleared);
            _boundManager.OnDefeat.RemoveListener(HandleDefeat);
            _boundManager = null;
        }

        private void HandleWaveCleared(int waveId)
        {
            // Held is upgraded to Breached inside Close when any crossing was recorded.
            Settle(DefenseOutcome.Held);
        }

        private void HandleDefeat()
        {
            Settle(DefenseOutcome.Overrun);
        }

        private void Settle(DefenseOutcome outcome)
        {
            if (_session == null) return;   // a normal (non-siege) wave cleared — nothing to file
            var session = _session;
            _session = null;
            UnbindManager();

            var record = session.Close(outcome);

            // ⭐ THE RULED LOSS (WO-1139, ruling 2026-08-22): COLLECTOR LOOTING ONLY, NO BANK
            //    THEFT. Close SUMMED the broken collectors' own LastLootStolen into
            //    record.ResourcesLost; this SEALS that ledger (rule id, crystal backstop, the
            //    idempotence latch) so a re-filed report cannot re-count it.
            //
            //    ⛔ IT DEBITS NOTHING. The collector removed the resources from its own pending
            //    when it broke; a wallet debit here would charge the player twice for one siege.
            //
            //    ⛔ ORDER IS STILL LOAD-BEARING: seal BEFORE Append, so the persisted record is
            //    the sealed one and a crash cannot leave an unsealed report to be re-counted.
            //
            //    ⛔ OFFLINE SIEGES LOOT TOO, and they arrive HERE — an away window becomes siege
            //    PRESSURE (ApplyOfflineWindow), the siege then fires LIVE at the gate, and it
            //    settles through this exact path. There is no second, silent theft path that could
            //    shrink a number while the player was away: every theft in the game is attached to
            //    a report the player can open, which is the WO-1139 legibility constraint met by
            //    construction rather than by a notification.
            DefenseReportBuilder.ApplyStakes(record);

            DefenseReportLedger.Append(record);   // Read=false -> the unread badge is the tell
        }

        /// <summary>
        /// DEV / oracle entry point: fire a siege now, skipping the cadence but NOT the safety
        /// gates. Returns false (traced) when a gate refused. Exists so the loop can be verified
        /// headlessly without waiting out an interval.
        /// </summary>
        public bool ForceSiegeNow()
        {
            PendingSieges = Mathf.Max(1, PendingSieges);
            if (Defer(out string why))
            {
                FlowTrace.Warn("Siege", $"ForceSiegeNow refused: {why}");
                return false;
            }
            Arm();
            return _session != null;
        }

        private static GameState StateOrNull()
        {
            var svc = GameStateService.Instance;
            return svc != null ? svc.State : null;
        }
    }
}
