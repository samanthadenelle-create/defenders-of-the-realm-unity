// =============================================================================
// ArenaProgress — the Arena (async-PvP) win/loss ledger (ARENA MVP).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// The persisted scoreboard for the SKR-wager raid loop: how many opponent bases
// the player has raided to victory, lost, the current win streak, and the total
// SKR purse won across all wins. Pure serializable data (the GameState pattern —
// behaviour lives in DeNelle.Village.Arena, not here), so it round-trips through
// the save layer the same way ZoneState / TribeState do.
//
// SCOPE (MVP): a simple W/L record — NOT a full leaderboard / ELO / matchmaking
// rank (those are later bites). It only tallies this player's own raids.
//
// ASSEMBLY NOTE: lives in DeNelle.Core (not Village) because GameState.Arena is a
// Core field and Core may not reference Village. ASCII-only strings.
// =============================================================================

using System;

namespace DeNelle.Core.State
{
    /// <summary>
    /// The player's Arena raid record: wins, losses, current win streak, and the
    /// cumulative SKR purse won. Append-only struct — new fields go at the END so
    /// older saves stay loadable.
    /// </summary>
    [Serializable]
    public struct ArenaProgress
    {
        /// <summary>Opponent bases raided to victory (garrison cleared).</summary>
        public int Wins;

        /// <summary>Raids lost (hero down / forfeit / timeout — stake forfeited).</summary>
        public int Losses;

        /// <summary>Current consecutive-win streak. Resets to 0 on a loss.</summary>
        public int Streak;

        /// <summary>Cumulative SKR purse won across all victories (stat only).</summary>
        public long TotalPurse;

        /// <summary>A fresh, zeroed record (a player who has never entered the Arena).</summary>
        public static ArenaProgress Empty => new ArenaProgress { Wins = 0, Losses = 0, Streak = 0, TotalPurse = 0 };

        /// <summary>Total raids fought (wins + losses).</summary>
        public int TotalRaids => Wins + Losses;
    }
}
