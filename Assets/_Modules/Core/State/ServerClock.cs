// =============================================================================
// ServerClock — server-anchored "now", immune to a wall-clock edit (WO-912 §7.2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// ⛔ WHY THIS EXISTS — the exploit it closes, stated plainly.
//
// The rewarded-ad skip allowance is a FIXED WINDOW anchored on first use: burn
// N watches, then a hard wall until the window expires. Before this file, the
// window start was stamped from the DEVICE clock (BuildTimerService wrote
// DateTime.UtcNow). Rolling the phone's clock forward past the window minted a
// fresh allowance, on demand, repeatedly.
//
// That is not merely free timer skips. Each minted allowance is a REAL rewarded
// ad shown against the owner's live ad account, which is FABRICATED IMPRESSIONS
// — the pattern ad networks ban accounts for. WO-912 §7.1 names this as the
// actual reason the window needs hardening, not the free skipping.
//
// ── HOW IT WORKS, and the one thing that makes it work ───────────────────────
// A plain server OFFSET does not defend against this. TimeSource.ServerOffsetMs
// is (serverNow - deviceNow) captured at a handshake; if the device clock later
// jumps +4h, deviceNow jumps too and the corrected value jumps right along with
// it. The offset corrects a WRONG clock; it does not resist a MOVING one.
//
// So the anchor here is a MONOTONIC one. At sync we record:
//     _anchorServerMs — server's unix-ms, from the server's own Date.now()
//     _anchorTicks    — a Stopwatch reading, which counts elapsed time and is
//                       NOT derived from the wall clock at all
// and thereafter report  _anchorServerMs + (elapsed since _anchorTicks).
// Changing the device clock moves neither term. Within a synced session the
// reported time is unforgeable by clock edits.
//
// ── WHAT THIS DELIBERATELY DOES NOT DO ───────────────────────────────────────
// Stopwatch resets when the process does, so after an app restart there is no
// anchor until the next successful handshake, and IsTrusted reads false. That
// gap is INTENTIONAL and is why WO-912 §7.2 recommends "server-stamped,
// RECONCILED ON SYNC" rather than blocking the watch on connectivity: a player
// on a plane must still be able to play. The server corrects the window on the
// next save/load round trip and refuses impossible histories. Callers must
// therefore treat IsTrusted as "can I rely on this right now", never as a
// permission gate that hard-fails offline play.
//
// ⛔ NEVER "simplify" this to DateTimeOffset.UtcNow + offset. That is precisely
// the shape this file replaces, and it reads as correct while defending nothing.
//
// Lives in DeNelle.Core (not DeNelle.Village) because the SETTER is the save
// round-trip in GameStateService (Core) while the READER is TimeSource
// (Village), and Core may not reference Village — CLAUDE.md §5.
// =============================================================================
using System;
using System.Diagnostics;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.State
{
    /// <summary>
    /// Server-anchored clock. <see cref="Sync"/> is called with the server's own
    /// unix-ms whenever a backend round trip returns one; <see cref="TryNowUnixMs"/>
    /// then reports server time advanced by a monotonic timer rather than by the
    /// device's wall clock.
    /// </summary>
    public static class ServerClock
    {
        private const string Sys = "ServerClock";

        // Monotonic elapsed-time source. Never reads the wall clock, so a user
        // changing the device date does not move it.
        private static readonly Stopwatch _mono = Stopwatch.StartNew();

        private static double _anchorServerMs;      // server unix-ms at the last sync
        private static double _anchorElapsedMs;     // _mono reading at that same instant
        private static bool   _synced;

        /// <summary>
        /// True when a server time has been received THIS PROCESS and the monotonic
        /// anchor is therefore still valid. False after a restart until the next
        /// handshake. This is a trust signal, not a permission gate — see the header.
        /// </summary>
        public static bool IsTrusted => _synced;

        /// <summary>Unix-ms of the last sync (0 when never synced). Diagnostics only.</summary>
        public static double LastSyncServerMs => _anchorServerMs;

        /// <summary>
        /// Records the server's authoritative unix-ms against a monotonic anchor.
        /// Safe to call on every round trip; each call re-anchors and so corrects drift.
        /// Non-positive values are refused (a missing/!malformed field must never be
        /// mistaken for "the epoch").
        /// </summary>
        public static void Sync(double serverNowUnixMs)
        {
            if (!(serverNowUnixMs > 0d) || double.IsNaN(serverNowUnixMs) || double.IsInfinity(serverNowUnixMs))
            {
                FlowTrace.Warn(Sys, $"Sync REFUSED: implausible serverNowUnixMs={serverNowUnixMs}. " +
                                    "Keeping the previous anchor; the clock stays as trustworthy as it was.");
                return;
            }

            double deviceMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            double skewMs   = serverNowUnixMs - deviceMs;

            _anchorServerMs  = serverNowUnixMs;
            _anchorElapsedMs = _mono.Elapsed.TotalMilliseconds;
            _synced          = true;

            // The skew is the interesting number in a capture: a large value means the
            // device clock is wrong (innocently or otherwise), and it is exactly what a
            // pre-WO-912 build would have silently trusted.
            FlowTrace.Step(Sys, $"synced — server anchor set; device skew {skewMs:F0} ms " +
                                $"({TimeSpan.FromMilliseconds(Math.Abs(skewMs)).TotalHours:F2} h). " +
                                "Window math now advances on a monotonic timer, not the wall clock.");
        }

        /// <summary>
        /// Server-anchored unix-ms. Returns false when this process has never synced,
        /// in which case the caller must fall back and let the server reconcile later.
        /// </summary>
        public static bool TryNowUnixMs(out double nowUnixMs)
        {
            if (!_synced) { nowUnixMs = 0d; return false; }
            nowUnixMs = _anchorServerMs + (_mono.Elapsed.TotalMilliseconds - _anchorElapsedMs);
            return true;
        }

        /// <summary>
        /// TEST/EDITOR ONLY: drops the anchor so a suite can exercise the untrusted path.
        /// Never call from gameplay — an attacker-visible reset would hand back the exploit.
        /// </summary>
        public static void ResetForTests()
        {
            _synced = false;
            _anchorServerMs = 0d;
            _anchorElapsedMs = 0d;
        }
    }
}
