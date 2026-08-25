// =============================================================================
// SiegeClock — the siege system's WALL-CLOCK seam (WO-1026).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ⛔ WHY THIS FILE EXISTS AT ALL, AND WHY IT IS NOT IN Village/Waves/ ⛔
//
// The siege system has TWO CLASSES OF TIME and they must never be the same clock:
//
//   1. CADENCE / RECORD time — "when is the next assault due", "when did this one
//      start and end". This is wall-clock, server-anchored, and MUST survive the
//      app being closed (otherwise closing the app stalls the siege cadence, and
//      the consequence loop becomes opt-out). That is QUEUE-class time and it
//      belongs on TimeSource, exactly like the build/train/research queues.
//
//   2. SESSION time — anything that ticks DURING an assault: hold time, path
//      sampling cadence, session duration. That is COMBAT-class time and it runs
//      on engine time (Time.realtimeSinceStartup / Time.deltaTime / Time.time),
//      because the DEV queue time-skip (DevClock) fast-forwards TimeSource and
//      must never warp a live battle. The owner ruled that out explicitly.
//
// DevTimeSkipRegression case6 enforces class 2 as a FILE-LEVEL source lint: no file
// anywhere under Assets/_Modules/Village/Waves (or BattleATB / Village/Enemies /
// Dungeons) may reference TimeSource *at all*. That is deliberately blunt and it is
// the right blunt: it needs no dataflow analysis, it cannot be argued with in review,
// and it cannot rot. SiegeScheduler and SiegeSession live in that swept tree and
// legitimately need class-1 time — so the class-1 reads move HERE, one directory
// over, where they are allowed, instead of the assertion being narrowed to let them
// through. Weakening the lint to exempt two files would have re-opened it for every
// file added after them.
//
// ⛔ DO NOT MOVE THIS FILE INTO Village/Waves/, and do not inline these calls back
//    into SiegeScheduler/SiegeSession — that re-breaches the firewall on the next
//    gate run. If a THIRD kind of siege wall-clock read appears, add it here.
//
// This file holds NO session/combat timing. There is no Time.deltaTime, no
// Time.time, no per-frame anything: it is only the cadence arithmetic and the two
// report stamps.
// =============================================================================

using System;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The ONLY place the siege system reads the persisted wall clock. See the file header:
    /// cadence + report stamps live here so that <c>SiegeScheduler</c> / <c>SiegeSession</c>
    /// stay engine-time-only for the combat firewall.
    /// </summary>
    public static class SiegeClock
    {
        /// <summary>
        /// The house wall clock, in unix ms. Used for the cadence and for the assault's
        /// start/end stamps in the ledger — never for anything that paces a live fight.
        /// </summary>
        public static double NowUnixMs() => TimeSource.NowUnixMs();

        /// <summary>Remaining cadence time for presentation/scheduling. Returns false until
        /// a real cadence anchor exists; callers must never turn a fresh save into an alert.</summary>
        public static bool TryGetDueIn(GameState state, double intervalMs, out TimeSpan dueIn)
        {
            dueIn = default;
            if (state == null || state.LastSiegeUnixMs <= 0 || intervalMs <= 0) return false;

            double now = NowUnixMs();
            if (state.LastSiegeUnixMs > now) return false;

            dueIn = TimeSpan.FromMilliseconds(Math.Max(0, intervalMs - (now - state.LastSiegeUnixMs)));
            return true;
        }

        /// <summary>
        /// Stamps the cadence clock as "a siege just fired now". Called once, from
        /// <c>SiegeScheduler.Arm</c>.
        /// </summary>
        /// <remarks>⛔ Never writes <c>GameState.LastHarvestClaimMs</c> — the
        /// OfflineClaimCoordinator owns that clock (WO-1147).</remarks>
        public static void StampFired(GameState state)
        {
            if (state == null) return;
            state.LastSiegeUnixMs = NowUnixMs();
        }

        /// <summary>
        /// The cadence half of <c>SiegeScheduler.Defer</c>: returns true (with a reason)
        /// when the interval has NOT elapsed yet, false when a siege is due.
        /// <para>Also owns the two clock-hygiene cases, both of which must SELF-HEAL rather
        /// than latch: an unseeded clock (a fresh save reads as 1970 = an infinite elapsed
        /// window = a retroactive assault as the player's first act), and a clock in the
        /// FUTURE (device clock moved, or a save restored from ahead), which without a
        /// monotonic re-stamp would stall the cadence forever.</para>
        /// </summary>
        /// <param name="state">Live game state. Null is treated as "defer".</param>
        /// <param name="intervalMs">Hours-between-sieges, already in ms.</param>
        /// <param name="why">Set on defer; null when a siege is due.</param>
        public static bool CadenceDefer(GameState state, double intervalMs, out string why)
        {
            why = null;

            if (state == null)
            {
                why = "no GameStateService.State (cannot read the cadence clock)";
                return true;
            }

            if (state.LastSiegeUnixMs <= 0)
            {
                // Seed forward: a fresh save gets a full interval of peace, not an instant siege.
                state.LastSiegeUnixMs = NowUnixMs();
                why = "cadence clock seeded forward (first evaluation -- no retroactive siege)";
                return true;
            }

            double now = NowUnixMs();
            if (state.LastSiegeUnixMs > now)
            {
                state.LastSiegeUnixMs = now;   // monotonic guard: never stall forever on a bad clock
                why = "cadence clock was in the FUTURE -- re-stamped to now";
                return true;
            }

            double dueInMs = intervalMs - (now - state.LastSiegeUnixMs);
            if (dueInMs > 0)
            {
                why = $"not due for {(dueInMs / 60000.0):F1} more minutes";
                return true;
            }

            return false;
        }

        /// <summary>
        /// The offline half: converts an away window into BANKED siege pressure, and owns the
        /// same two clock-hygiene cases as <see cref="CadenceDefer"/> (seed-forward, future
        /// clock). Returns the number of sieges banked; <paramref name="note"/> always carries
        /// a traceable reason, because a deferral that logs nothing recreates the very bug
        /// WO-1026 closes ("the base is never attacked", with no evidence which gate refused).
        /// </summary>
        /// <remarks>This does NOT resolve a battle. Away time becomes PRESSURE; the siege then
        /// happens LIVE, at the gate, with the player watching — see SiegeScheduler's header.</remarks>
        public static int BankOfflinePressure(GameState state, double nowUnixMs, double cappedSeconds,
                                              double intervalMs, out string note)
        {
            note = null;

            if (state == null)
            {
                note = "no GameStateService.State";
                return 0;
            }

            if (state.LastSiegeUnixMs <= 0)
            {
                state.LastSiegeUnixMs = nowUnixMs;
                note = "fresh cadence clock seeded, pressure 0 (no retroactive siege)";
                return 0;
            }

            if (state.LastSiegeUnixMs > nowUnixMs)
            {
                note = $"cadence clock is in the FUTURE ({state.LastSiegeUnixMs:F0} > {nowUnixMs:F0}) " +
                       "-- pressure 0, clock re-stamped to now";
                state.LastSiegeUnixMs = nowUnixMs;
                return 0;
            }

            double intervalSec = intervalMs / 1000.0;
            if (intervalSec <= 0) return 0;

            int banked = (int)Math.Floor(cappedSeconds / intervalSec);
            return Mathf.Max(0, banked);
        }
    }
}
