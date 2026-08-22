// =============================================================================
// OfflineHarvestResult — the per-resource haul accrued while the player was away
// (WO-115). A plain value carrier raised to the welcome-back popup; it never
// touches GameState itself (the service banks; this only reports what it banked).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Resource buckets map 1:1 to MineNode.MineResource / the GameState wallet fields
// (Iron / Wood / Food / AetherCrystals) so the popup can render +N rows without
// re-deriving anything. Pet harvest (WO-111 Phase 4) folds into the same buckets
// when it lands — no shape change needed.
// =============================================================================

namespace DeNelle.Village
{
    /// <summary>
    /// The result of an offline-accrual claim: how much of each resource was
    /// banked, how long the player was away, and whether the cap clipped it.
    /// </summary>
    public sealed class OfflineHarvestResult
    {
        /// <summary>Iron banked this claim.</summary>
        public int Iron;
        /// <summary>Wood banked this claim.</summary>
        public int Wood;
        /// <summary>Food banked this claim (the retired "Stone" axis, repurposed — DEF-121).</summary>
        public int Food;
        /// <summary>Aether Crystals banked this claim.</summary>
        public int AetherCrystals;

        /// <summary>Real seconds since the last claim (BEFORE the cap clamp) — for the away-time line.</summary>
        public double AwaySeconds;
        /// <summary>True when <see cref="AwaySeconds"/> exceeded the offline cap (the gentle nudge).</summary>
        public bool WasCapped;

        // =====================================================================
        //  WO-1128 — WHICH CLOCK PRODUCED THIS WINDOW (the reconciliation half)
        // ---------------------------------------------------------------------
        //  The device clock cannot be verified and we do not try (WO-1128 §1).
        //  What we CAN do is record, per window, whether "now" came from the
        //  monotonic server anchor (ServerClock, unforgeable by a wall-clock edit)
        //  or from the raw device clock, and carry the window's own endpoints so
        //  the server can compare the client's DECLARED window against its OWN
        //  elapsed time on the next round trip (api/game/save.js §RECONCILE).
        //
        //  These fields are DIAGNOSTIC + DECLARATIVE, never punitive. Nothing in
        //  the client reduces a haul because the clock was unanchored — a cold
        //  launch is ALWAYS unanchored (Stopwatch dies with the process), and a
        //  player on a plane is not a cheater. Refuse server-side, never punish
        //  client-side.
        // =====================================================================

        /// <summary>
        /// True when <c>TimeSource.NowUnixMs()</c> was server-anchored for THIS claim
        /// (<see cref="DeNelle.Core.State.ServerClock.IsTrusted"/>). False on any cold
        /// launch before the first backend round trip — an expected, honest state.
        /// </summary>
        public bool ServerAnchored;

        /// <summary>Unix-ms this window started at (the persisted claim clock, or the pause edge).</summary>
        public double WindowStartUnixMs;

        /// <summary>Unix-ms "now" that closed this window — the value the clock advanced to.</summary>
        public double NowUnixMs;

        /// <summary>
        /// True when this haul rests on an unverifiable device clock and is therefore
        /// subject to server reconciliation on the next sync. Display/telemetry only.
        /// </summary>
        public bool IsProvisional => !ServerAnchored;

        /// <summary>Short trace/telemetry name of the clock this window trusted.</summary>
        public string ClockSource => ServerAnchored ? "server-anchored" : "device";

        /// <summary>Total units banked across every resource (popup-trigger gate: show only when &gt; 0).</summary>
        public int Total => Iron + Wood + Food + AetherCrystals;

        /// <summary>A zero haul — nothing accrued, no popup.</summary>
        public static OfflineHarvestResult None => new OfflineHarvestResult();

        /// <summary>Add an integer amount to the bucket for <paramref name="resource"/>.</summary>
        public void Add(MineResource resource, int amount)
        {
            if (amount <= 0) return;
            switch (resource)
            {
                case MineResource.Iron:          Iron += amount;           break;
                case MineResource.Wood:          Wood += amount;           break;
                case MineResource.Food:          Food += amount;          break;
                case MineResource.AetherCrystal: AetherCrystals += amount; break;
            }
        }
    }
}
