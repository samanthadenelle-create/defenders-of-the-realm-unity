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
// =============================================================================
using System;

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

        /// <summary>Current unix-ms (device clock + <see cref="ServerOffsetMs"/>).</summary>
        public static double NowUnixMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ServerOffsetMs;
        }
    }
}
