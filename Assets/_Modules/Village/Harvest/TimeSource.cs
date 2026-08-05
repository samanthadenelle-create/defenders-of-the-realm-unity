// =============================================================================
// TimeSource — the single clock seam the offline-accrual layer reads (WO-115 §3).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// v1: device clock. NowUnixMs() returns DateTimeOffset.UtcNow as unix-ms — the
// SAME unit GameState.LastInboxSyncAt / LastHarvestClaimMs use, so the accrual
// math is a plain (now - lastClaim) subtraction.
//
// WHY A SEAM (not an inline DateTimeOffset call): the accrual window is only as
// trustworthy as the clock it reads. Server-authoritative time is the hardening
// path (WO-107/WO-120 backend lane). By routing every "now" through this one
// method, v2 can swap the device read for a server read WITHOUT touching the
// accrual math — set ServerOffsetMs once after a backend handshake and every
// reader is corrected. v1 leaves the offset at 0 (pure device clock).
//
// DEV TIME-SKIP (owner ask 2026-08-04) — the clock now assembles THREE terms:
//     NowUnixMs() = deviceUtcMs + ServerOffsetMs + DevSkipMs
// DevSkipMs is a SEPARATE dev-only field (see DeNelle.Core.Diagnostics.DevClock),
// deliberately NOT folded into ServerOffsetMs: that one belongs to the WO-120
// backend lane, and conflating them would make a QA skip indistinguishable from a
// server correction and impossible to clear independently. DevClock compiles to a
// constant 0 in shipped player builds, so this term vanishes there.
// Combat/waves/battle timers do NOT read this seam (they use Time.deltaTime), so a
// dev skip advances the build queues + offline accrual WITHOUT warping battle —
// pinned by DevTimeSkipRegression. Full consumer list + save-safety assessment
// lives in the DevClock.cs header. READ IT before trusting a jumped wallet.
// =============================================================================
using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The one clock the offline-accrual layer reads. Device clock for v1;
    /// swappable to server time later via <see cref="ServerOffsetMs"/> without
    /// changing any accrual caller.
    /// </summary>
    public static class TimeSource
    {
        /// <summary>
        /// Correction added to the device clock to approximate server "now"
        /// (serverNowMs - deviceNowMs at the last handshake). 0 = pure device
        /// clock (v1). The WO-120 backend lane sets this once after a time sync;
        /// no accrual caller changes.
        /// </summary>
        public static double ServerOffsetMs;

        /// <summary>
        /// DEV-ONLY forward skip added on top of the device clock, so QA can jump the
        /// Obsidian build/train/research queues without waiting out a 2h timer. Kept in
        /// <see cref="DevClock"/> (DeNelle.Core) rather than as a field here so the live
        /// dev panel — DeNelle.HUD.AdminOverlay, which may reference Core but NEVER
        /// DeNelle.Village (CLAUDE.md §5) — can drive it without System.Reflection.
        /// <para>
        /// READ-ONLY here on purpose: mutate through <see cref="AddDevSkipMs"/> /
        /// <see cref="ResetDevSkip"/> so every change is FlowTrace'd and the
        /// forward-only rule is enforced in one place. ALWAYS 0 in a shipped build.
        /// </para>
        /// </summary>
        public static double DevSkipMs => DevClock.SkipMs;

        /// <summary>
        /// Current unix-ms — <c>device clock + <see cref="ServerOffsetMs"/> +
        /// <see cref="DevSkipMs"/></c>. The three terms are independent: a dev skip
        /// never perturbs the server offset and vice versa.
        /// </summary>
        public static double NowUnixMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ServerOffsetMs + DevClock.SkipMs;
        }

        /// <summary>
        /// DEV: pushes the queue clock forward by <paramref name="deltaMs"/> (additive —
        /// repeated calls stack) and returns the new accumulated skip. Negative deltas are
        /// refused; <see cref="ResetDevSkip"/> is the only way back. No-op in a shipped
        /// build. Every call is FlowTrace'd so a capture shows the clock was moved and
        /// by how much.
        /// </summary>
        public static double AddDevSkipMs(double deltaMs) => DevClock.Add(deltaMs);

        /// <summary>
        /// DEV: clears the accumulated skip (back to <see cref="ServerOffsetMs"/> only) and
        /// returns how much was cleared. Accrual stamps written while skipped self-heal via
        /// their existing monotonic guards; a queue job ENQUEUED while skipped keeps its
        /// skewed FinishMs — see the DevClock.cs header, §SAVE SAFETY (b).
        /// </summary>
        public static double ResetDevSkip() => DevClock.Reset();
    }
}
