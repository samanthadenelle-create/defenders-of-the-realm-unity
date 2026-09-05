// =============================================================================
// OfflineClaimCoordinator -- the ONE owner of the offline accrual clock (WO-1147).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE BUG THIS EXISTS TO KILL (sweep 2026-08-16): THREE consumers read the same
// persisted clock (GameState.LastHarvestClaimMs) from THREE independently deferred
// coroutines, and exactly ONE of them advanced it:
//   * OfflineHarvestService.Start -> 1 frame -> read delta AND WRITE clock = now.
//   * EchoService.Start           -> 1 frame -> read delta (SAME frame as the write;
//     component order is undefined, so the silo fill was a coin-flip: sometimes the
//     real away window, sometimes zero).
//   * EchoRepairService.Start     -> 2 frames -> read delta ALWAYS AFTER the write,
//     so the delta was always ~0 and offline repair NEVER accrued, for its whole life.
// Mobile made it worse: OfflineHarvestService re-claimed (and re-stamped) on every
// OnApplicationPause(false), while the two Echo consumers were guarded once-per-
// SESSION -- so the three disagreed about what a "session" even was.
//
// THE SHAPE OF THE FIX (owner directive: ONE authority, explicit fan-out -- never an
// ordering hack): this coordinator performs ONE read of the clock, computes ONE
// elapsed window, hands that SAME window to every registered consumer, and then
// advances + persists the clock EXACTLY ONCE. No consumer touches
// GameState.LastHarvestClaimMs any more; the only other write path is StampClock()
// (used by EchoService.DumpSilos, which is a deliberate come-back-RESET), and it
// routes through here so the field still has a single owner.
//
// PER-CONSUMER CAPS ARE PRESERVED, DELIBERATELY. The coordinator publishes the RAW
// elapsed window; each consumer clamps it with its OWN documented cap via
// OfflineClaimWindow.CappedSeconds(hours) -- OfflineHarvestService 10h (away-cap),
// EchoService the silo HOUR cap (4h), EchoRepairService OfflineCapHours (4h, further
// bounded by MaxBankedFractions). Unifying the delta is the fix; unifying the caps
// would have been an unrequested economy change.
//
// RESUME WINDOW (mobile): on a cold load the whole (now - lastClaim) delta is away
// time -- nothing was ticking. On an app RESUME it is not: the online loops
// (EchoService.Update silo tick, EchoRepairService.Tick) already counted the
// foreground stretch. So NotePaused(true) records when the app actually went to the
// background and a resume claim integrates only from THERE. That also removes a
// pre-existing over-grant in the node path, which used to credit foreground time on
// every resume. If no pause was recorded, we fall back to the full delta (never less
// income than before).
//
// FRESH SAVE (clock <= 0): seed the clock to now and fan out NOTHING -- the exact
// stance all three consumers already held individually (no giant retroactive first
// claim). BACKWARDS CLOCK (anti-tamper): elapsed clamps to 0 and the clock is still
// re-stamped to now, so a tampered window is never re-claimable.
//
// INSTRUMENTATION (CLAUDE.md section 12): every claim traces the ONE delta and EVERY
// consumer's share by name, so the next time this drifts a single capture names it.
// These traces are PERMANENT -- flag them off, never strip them.
// =============================================================================
using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// The single elapsed-away window produced by one read of the persisted clock and
    /// fanned out to every consumer of that claim. Immutable by construction: a consumer
    /// can clamp it, never redefine it.
    /// </summary>
    public readonly struct OfflineClaimWindow
    {
        /// <summary>Monotonically increasing id of the claim this window came from (1-based).</summary>
        public readonly int Sequence;
        /// <summary>Why the claim ran ("cold-load", "resume", an oracle name...). Trace text only.</summary>
        public readonly string Reason;
        /// <summary>Unix-ms "now" used for the WHOLE claim (one read, shared by all consumers).</summary>
        public readonly double NowUnixMs;
        /// <summary>Unix-ms start of the counted window (the persisted clock, or the pause moment on resume).</summary>
        public readonly double WindowStartUnixMs;
        /// <summary>The ONE elapsed away-window, in seconds, already clamped to &gt;= 0 (backwards-clock guard).</summary>
        public readonly double ElapsedSeconds;
        /// <summary>True when this claim seeded a fresh clock (<c>&lt;= 0</c>) and therefore accrues nothing.</summary>
        public readonly bool WasFreshClock;

        public OfflineClaimWindow(int sequence, string reason, double nowUnixMs,
                                  double windowStartUnixMs, double elapsedSeconds, bool wasFreshClock)
        {
            Sequence = sequence;
            Reason = reason;
            NowUnixMs = nowUnixMs;
            WindowStartUnixMs = windowStartUnixMs;
            ElapsedSeconds = elapsedSeconds;
            WasFreshClock = wasFreshClock;
        }

        /// <summary>This window clamped to a consumer's OWN cap in hours (the per-consumer
        /// fairness dial -- the coordinator never applies it for them).</summary>
        public double CappedSeconds(double capHours)
        {
            double capSec = Math.Max(0.0, capHours) * 3600.0;
            return Math.Min(Math.Max(0.0, ElapsedSeconds), capSec);
        }

        /// <summary>True when the raw window exceeded <paramref name="capHours"/> (the "capped" trace).</summary>
        public bool ExceedsCap(double capHours) => ElapsedSeconds > Math.Max(0.0, capHours) * 3600.0;
    }

    /// <summary>
    /// Implemented by every system that accrues over the away window. The coordinator
    /// calls <see cref="ApplyOfflineWindow"/> once per claim, with the SAME window for
    /// everyone, inside a Guard (one bad consumer never blocks the others or the clock).
    /// </summary>
    public interface IOfflineClaimConsumer
    {
        /// <summary>Short, stable name for traces ("harvest-nodes", "echo-silo", "echo-repair").</summary>
        string OfflineConsumerName { get; }

        /// <summary>Accrue this consumer's share of the window. MUST NOT touch
        /// <c>GameState.LastHarvestClaimMs</c> -- the coordinator owns it.</summary>
        void ApplyOfflineWindow(OfflineClaimWindow window);
    }

    /// <summary>
    /// The ONE authority over <c>GameState.LastHarvestClaimMs</c>: one read, one elapsed
    /// window, explicit fan-out to every registered consumer, one advance + save.
    /// See the file header for the ordering bug this replaced.
    /// </summary>
    public static class OfflineClaimCoordinator
    {
        private static readonly List<IOfflineClaimConsumer> _consumers = new List<IOfflineClaimConsumer>();

        /// <summary>Unix-ms at which the app last went to the background (0 = none recorded).</summary>
        private static double _pauseBeganUnixMs;

        /// <summary>Number of claims completed this process (also the last window's Sequence).</summary>
        public static int ClaimCount { get; private set; }

        /// <summary>The window produced by the most recent claim (default when none has run).</summary>
        public static OfflineClaimWindow LastWindow { get; private set; }

        /// <summary>Registered consumer count (diagnostic / regression readout).</summary>
        public static int ConsumerCount => _consumers.Count;

        /// <summary>
        /// Raised ONCE per claim, AFTER every consumer has applied its share and the clock
        /// has been advanced -- i.e. the first moment at which the whole away window is
        /// known.
        /// <para>
        /// WO-1231 added this for a specific reason: the while-you-were-away summary has to
        /// report what EVERY consumer did, including the Wood and Iron that passive Echo
        /// mending spent. It used to be raised from inside ONE consumer's
        /// <c>ApplyOfflineWindow</c>, so what it could see depended on <b>fan-out order</b>,
        /// which is registration order, which is bootstrap order -- undefined. That is the
        /// same class of ordering bug this whole file exists to kill (see the header), and
        /// "register the popup's owner last" would have been the ordering hack, not the fix.
        /// A completion event has no order to get wrong.
        /// </para>
        /// A subscriber must not accrue anything here: the clock has already advanced.
        /// Reporting only.
        /// </summary>
        public static event Action<OfflineClaimWindow> ClaimCompleted;

        // =====================================================================
        //  Registration -- idempotent, callable from Awake/Start AND from a
        //  headless oracle (editmode AddComponent never runs Awake).
        // =====================================================================

        /// <summary>Idempotently registers a consumer. Returns true if it was newly added.</summary>
        public static bool Register(IOfflineClaimConsumer consumer)
        {
            if (consumer == null) return false;
            for (int i = 0; i < _consumers.Count; i++)
                if (ReferenceEquals(_consumers[i], consumer)) return false;
            _consumers.Add(consumer);
            FlowTrace.Step("Offline", $"consumer registered: {consumer.OfflineConsumerName} (now {_consumers.Count}).");
            return true;
        }

        /// <summary>Removes a consumer (OnDestroy). Safe to call for an unregistered instance.</summary>
        public static void Unregister(IOfflineClaimConsumer consumer)
        {
            if (consumer == null) return;
            for (int i = _consumers.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_consumers[i], consumer)) _consumers.RemoveAt(i);
        }

        // =====================================================================
        //  Pause bookkeeping -- so a RESUME claim counts only the away stretch
        // =====================================================================

        /// <summary>
        /// Records the background/foreground edge. On <c>paused=true</c> we stamp the
        /// moment the online loops stopped ticking; a following claim integrates from
        /// there instead of from the persisted clock, so foreground time is never
        /// counted twice. We do NOT write the persisted clock here -- a consumer with no
        /// online loop (worker nodes) still needs that foreground stretch credited.
        /// </summary>
        public static void NotePaused(bool paused)
        {
            if (paused)
            {
                _pauseBeganUnixMs = TimeSource.NowUnixMs();
                FlowTrace.Step("Offline", $"app backgrounded at {_pauseBeganUnixMs:0} -- resume claim will count from here.");
            }
        }

        // =====================================================================
        //  WO-1414 -- NEW GAME: the LIVE half of the away clock
        // ---------------------------------------------------------------------
        //  ResetToNewGame zeroes the persisted stamp (GameStateService.cs:1232), which is
        //  the PERSISTED half and is enough for the arithmetic below: lastMs <= 0 takes the
        //  fresh-clock arm and the window is 0. What the reset had never heard of is the
        //  static state THIS class carries in memory across a New Game:
        //    * _pauseBeganUnixMs -- a background edge recorded under the PREVIOUS save.
        //      HYGIENE, NOT A LIVE DEFECT, and said precisely because a comment that
        //      overclaims is the next seat's wrong lead: the arithmetic ALREADY excludes it
        //      (the resume-edge branch is guarded by !freshClock, and once that claim stamps
        //      "now" any pre-reset edge is < lastMs and can never satisfy "> lastMs"). It is
        //      cleared so a capture can never show a new town's claim carrying an edge that
        //      belongs to the previous save.
        //    * NewGameThisProcess -- not behaviour, EVIDENCE: every window trace below
        //      names it, so one capture says whether the window it is about to fan out
        //      belongs to a game that was started this process.
        //  Static handler on a static event, installed BeforeSceneLoad, so it exists whether
        //  or not a claim has ever run -- the same shape ResourceCollector.InstallNewGameHook
        //  uses (WO-1371) and for the same reason.
        // =====================================================================

        /// <summary>True once <c>ResetToNewGame</c> has run in THIS process (trace field only).</summary>
        public static bool NewGameThisProcess { get; private set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallNewGameHook()
        {
            GameStateService.NewGameStarted -= OnNewGameStarted;
            GameStateService.NewGameStarted += OnNewGameStarted;
            FlowTrace.Step("Offline",
                "New Game hook installed -- a reset now drops the recorded background edge as well as " +
                "the persisted stamp (WO-1414).");
        }

        private static void OnNewGameStarted()
        {
            double droppedEdge = _pauseBeganUnixMs;
            _pauseBeganUnixMs = 0.0;
            NewGameThisProcess = true;
            FlowTrace.Step("Offline",
                $"New Game: dropped the recorded background edge ({droppedEdge:0}) - hygiene, so no capture " +
                "shows a new town's claim carrying the previous save's edge. The window itself is already " +
                "protected by the zeroed persisted stamp (the reset's own half) taking the fresh-clock arm.");
        }

        // =====================================================================
        //  THE CLAIM -- one read, one delta, explicit fan-out, one advance
        // =====================================================================

        /// <summary>
        /// Runs a full claim: reads the persisted clock ONCE, computes ONE elapsed
        /// window, hands it to every registered consumer (each Guard-wrapped), then
        /// advances + persists the clock EXACTLY ONCE. Returns the window that was
        /// fanned out (Sequence 0 / elapsed 0 when there is no GameState to read).
        /// </summary>
        public static OfflineClaimWindow Claim(string reason)
        {
            using var _t = FlowTrace.Enter("Offline", $"Claim({reason})");

            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null)
            {
                FlowTrace.Warn("Offline", $"Claim({reason}): no GameStateService/State -- claim skipped, clock untouched.");
                return default;
            }

            double nowMs = TimeSource.NowUnixMs();
            double lastMs = state.LastHarvestClaimMs;
            int seq = ClaimCount + 1;

            // -- WO-1414 sec.1: THE ANCHOR AND ITS PROVENANCE, named before anything is
            //    fanned out and named even when the window is ZERO. The window alone could
            //    never answer "where did that number come from"; the anchor plus its
            //    provenance can, and the newGame flag says whether this town was founded
            //    this process. One line per claim, Guard-wrapped (a trace must never be
            //    able to break a claim). --------------------------------------------------
            bool freshClock = lastMs <= 0;
            bool resumeEdge = !freshClock && _pauseBeganUnixMs > lastMs && _pauseBeganUnixMs <= nowMs;
            double anchorMs = freshClock ? nowMs : (resumeEdge ? _pauseBeganUnixMs : lastMs);
            double anchoredSec = freshClock ? 0.0 : Math.Max(0.0, (nowMs - anchorMs) / 1000.0);
            string provenance =
                freshClock ? "reseed" :
                (nowMs < anchorMs ? "zero" : (resumeEdge ? "resume-edge" : "state"));
            TraceWindowProvenance(seq, reason, anchoredSec, anchorMs, provenance);

            // -- Fresh save: seed the clock forward, accrue NOTHING this launch. -----
            if (freshClock)
            {
                var fresh = new OfflineClaimWindow(seq, reason, nowMs, nowMs, 0.0, true);
                FlowTrace.Step("Offline",
                    $"Claim #{seq} ({reason}): FRESH clock (LastHarvestClaimMs<=0) -- seed to now, zero fan-out " +
                    $"({_consumers.Count} consumer(s) get 0s).");
                AdvanceAndSave(state, svc, nowMs, reason, seq);
                ClaimCount = seq;
                LastWindow = fresh;
                // Raised here too so the invariant is "exactly one ClaimCompleted per
                // completed claim" with no exceptions a subscriber has to remember.
                RaiseCompleted(fresh);
                return fresh;
            }

            // -- Window start: the persisted clock, or the pause moment on a resume. --
            //    ONE decision, taken above with the provenance so the trace and the
            //    arithmetic can never name different anchors.
            double windowStartMs = anchorMs;
            if (resumeEdge)
            {
                FlowTrace.Step("Offline",
                    $"Claim #{seq} ({reason}): resume window -- counting from the background edge " +
                    $"({_pauseBeganUnixMs:0}), not the claim clock ({lastMs:0}); foreground time already ticked online.");
            }
            _pauseBeganUnixMs = 0.0;   // consumed: a second claim must not reuse this edge

            // Anti-tamper monotonic guard: a clock set FORWARD yields a negative delta
            // -> clamp to 0 (no accrual, no error). The clock is still re-stamped below,
            // so a tampered window is never re-claimable.
            double elapsedSec = Math.Max(0.0, (nowMs - windowStartMs) / 1000.0);
            if (nowMs < windowStartMs)
                FlowTrace.Warn("Offline",
                    $"Claim #{seq} ({reason}): clock ran BACKWARDS (now={nowMs:0} < start={windowStartMs:0}) -- elapsed clamped to 0.");

            var window = new OfflineClaimWindow(seq, reason, nowMs, windowStartMs, elapsedSec, false);

            // THE ONE DELTA, named before the fan-out: this is the line that proves every
            // consumer saw the same number.
            FlowTrace.Step("Offline",
                $"Claim #{seq} ({reason}): ONE delta = {elapsedSec:F0}s ({elapsedSec / 3600.0:F2}h) " +
                $"from {windowStartMs:0} to {nowMs:0}; fanning out to {_consumers.Count} consumer(s).");

            FanOut(window);

            AdvanceAndSave(state, svc, nowMs, reason, seq);
            ClaimCount = seq;
            LastWindow = window;
            RaiseCompleted(window);
            return window;
        }

        /// <summary>
        /// WO-1414 sec.1 -- the ONE line that names the window's ANCHOR and where that anchor
        /// came from, emitted on EVERY claim including the zero-window ones (a claim that
        /// accrues nothing is exactly the claim whose anchor is in dispute).
        /// <para>
        /// THE PROVENANCE VOCABULARY IS ONLY WHAT THIS CODE CAN DISTINGUISH, deliberately:
        /// <c>state</c> (the persisted stamp), <c>resume-edge</c> (the recorded background
        /// edge), <c>reseed</c> (a fresh/zero stamp -- New Game or first launch) and
        /// <c>zero</c> (a backwards clock, clamped). WO-1414 also listed <c>server</c> and
        /// <c>queued-payload</c> as candidates; NEITHER IS REACHABLE and inventing a label for
        /// them would be a fabricated provenance. Proof, read at source 2026-09-05:
        /// <c>GameStateService.LoadFromBackend</c> records the server anchor into
        /// <c>ServerLastSeenMs</c> and says out loud that it never writes the claim clock
        /// (GameStateService.cs:2068-2082), and its merge tail assigns only BestWave /
        /// Resources / Voidshards / Crystals / Stone / Iron / Wood (GameStateService.cs:2095-2136).
        /// The offline sync queue is documented as a retry MARKER that re-posts the CURRENT
        /// snapshot, never a body that is replayed into state (GameStateService.cs:2382-2396,
        /// and again at :2714).
        /// The only inbound writer is local deserialization of this save's own field.
        /// </para>
        /// Guard-wrapped: instrumentation may never break the claim it is instrumenting.
        /// </summary>
        private static void TraceWindowProvenance(int seq, string reason, double windowSec,
                                                  double anchorMs, string provenance)
        {
            Guard.Try("Offline", $"claim #{seq} window provenance", () =>
                FlowTrace.Step("Offline",
                    $"window={windowSec:F0}s anchor={anchorMs:0} provenance={provenance} " +
                    $"(newGame={NewGameThisProcess}) claim #{seq} ({reason})"));
        }

        /// <summary>Fires <see cref="ClaimCompleted"/> Guard-wrapped: a reporting subscriber
        /// that throws must never look like a failed claim (the accrual is already banked
        /// and the clock is already advanced by the time we get here).</summary>
        private static void RaiseCompleted(OfflineClaimWindow window)
        {
            var handler = ClaimCompleted;
            if (handler == null) return;
            Guard.Try("Offline", $"claim #{window.Sequence} completion report", () => handler(window));
        }

        /// <summary>Hands the SAME window to each consumer, Guard-wrapped so one thrower
        /// never starves the rest (and never blocks the clock advance).</summary>
        private static void FanOut(OfflineClaimWindow window)
        {
            // Snapshot: a consumer may register/unregister while being applied.
            var snapshot = _consumers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var c = snapshot[i];
                if (c == null) continue;
                string name = c.OfflineConsumerName;
                Guard.Try("Offline", $"offline share for '{name}' (claim #{window.Sequence})", () =>
                {
                    FlowTrace.Step("Offline",
                        $"claim #{window.Sequence}: share -> '{name}' gets the {window.ElapsedSeconds:F0}s window.");
                    c.ApplyOfflineWindow(window);
                });
            }
        }

        /// <summary>The ONE write to GameState.LastHarvestClaimMs (plus the atomic Save).</summary>
        private static void AdvanceAndSave(GameState state, GameStateService svc, double nowMs, string reason, int seq)
        {
            state.LastHarvestClaimMs = nowMs;
            svc?.Save();
            FlowTrace.Step("Offline", $"claim #{seq} ({reason}): clock advanced ONCE to {nowMs:0} and persisted.");
        }

        /// <summary>
        /// The come-back-RESET stamp (EchoService.DumpSilos): advance the clock to now
        /// WITHOUT accruing anything, so the next offline window starts from the dump.
        /// Routed through here so the persisted field keeps exactly one owner.
        /// </summary>
        public static void StampClock(string reason)
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null)
            {
                FlowTrace.Warn("Offline", $"StampClock({reason}): no GameStateService/State -- clock untouched.");
                return;
            }
            double nowMs = TimeSource.NowUnixMs();
            state.LastHarvestClaimMs = nowMs;
            svc?.Save();
            FlowTrace.Step("Offline", $"StampClock({reason}): clock re-stamped to {nowMs:0} (reset, no accrual).");
        }

        /// <summary>TEST SEAM: drops all registrations + pause state so an oracle starts clean.
        /// Never called by gameplay.</summary>
        public static void ResetForTests()
        {
            _consumers.Clear();
            _pauseBeganUnixMs = 0.0;
            ClaimCount = 0;
            LastWindow = default;
            NewGameThisProcess = false;   // WO-1414 -- the trace flag is process state; an oracle starts clean.
        }
    }
}
