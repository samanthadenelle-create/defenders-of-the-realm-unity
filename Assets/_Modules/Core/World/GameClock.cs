// =============================================================================
// GameClock — a single read for "what game-day is it" (WO-159 razed-site lockout).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// WO-159 needs a game-day count for the 3-game-day razed-site lockout, and is
// explicit: "Reuse the existing game-day clock — do NOT add a parallel time
// system; read whatever GameState already uses for the day count."
//
// FINDING (flagged in the WO result): the branch has NO existing day-count system.
// The only time seam is WorkerManager's wall-clock LastActive PlayerPrefs stamp
// (WO-115 offline). So rather than invent a parallel SIMULATED-time system, this
// helper derives a real-world day index from a single persisted epoch (the moment
// the save first asked the clock). 1 game-day == 1 real day by default. This is the
// minimal, non-parallel anchor; when WO-115's richer offline/day service lands it
// SUPERSEDES CurrentDay() (callers keep the same API). Headless-safe (no engine
// types beyond PlayerPrefs, which the project uses for the save anyway).
// =============================================================================
using System;
using UnityEngine;

namespace DeNelle.Core.World
{
    /// <summary>
    /// The single game-day read. <see cref="CurrentDay"/> returns whole game-days
    /// elapsed since the clock epoch (persisted once in PlayerPrefs). Reused by the
    /// WO-159 razed-site lockout. Designed to be replaced by WO-115's day service
    /// without changing callers.
    /// </summary>
    public static class GameClock
    {
        /// <summary>PlayerPrefs key holding the clock epoch (UTC ticks). Set once.</summary>
        public const string EpochKey = "dotr-gameclock-epoch";

        /// <summary>Real seconds that make up one game-day. Default 1 real day.
        /// (A future day service can compress this; the lockout math is unaffected.)</summary>
        public const double SecondsPerGameDay = 24.0 * 3600.0;

        /// <summary>
        /// Whole game-days elapsed since the clock epoch. The epoch is lazily stamped
        /// on the first call, so day 0 is "the first time the world asked". Monotonic
        /// and non-negative. Used as <c>currentGameDay</c> for the WO-159 lockout.
        /// </summary>
        public static int CurrentDay()
        {
            long epochTicks;
            string raw = PlayerPrefs.GetString(EpochKey, string.Empty);
            if (string.IsNullOrEmpty(raw) || !long.TryParse(raw, out epochTicks))
            {
                epochTicks = DateTime.UtcNow.Ticks;
                PlayerPrefs.SetString(EpochKey, epochTicks.ToString());
                PlayerPrefs.Save();
            }

            var epoch = new DateTime(epochTicks, DateTimeKind.Utc);
            double seconds = (DateTime.UtcNow - epoch).TotalSeconds;
            if (seconds <= 0) return 0;
            return (int)(seconds / SecondsPerGameDay);
        }
    }
}
