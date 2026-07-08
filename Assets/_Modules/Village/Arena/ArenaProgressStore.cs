// =============================================================================
// ArenaProgressStore — read/record the Arena W/L ledger (GameState + PlayerPrefs).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena   (STATIC)
//
// GameState.Arena (DeNelle.Core.State.ArenaProgress) is the in-memory source of
// truth, but — like Zones/Tribes/BaseLayout — it is NOT yet threaded through
// SaveSchema's round-trip. So this store mirrors it to PlayerPrefs (the same
// pattern EnemyOutpost / ClaimableCamp use for raid-clear state) so the W/L record
// survives a restart until the save owner wires ArenaProgress into SaveSchema.
//
// RecordWin / RecordLoss mutate BOTH the live GameState struct (so HUD/UI read it
// immediately) AND the PlayerPrefs mirror (so it persists). On first access we
// hydrate the live struct FROM PlayerPrefs if a save hasn't already populated it.
//
// ASCII-only strings. Null-safe: a missing GameStateService still records to prefs.
// =============================================================================

using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Arena
{
    /// <summary>Reads + records the Arena win/loss ledger, mirrored to PlayerPrefs.</summary>
    public static class ArenaProgressStore
    {
        private const string KeyWins   = "dotr-arena-wins";
        private const string KeyLosses = "dotr-arena-losses";
        private const string KeyStreak = "dotr-arena-streak";
        private const string KeyPurse  = "dotr-arena-purse";   // stored as string (long range)

        private static bool _hydrated;

        /// <summary>The current Arena record (hydrated from prefs on first read).</summary>
        public static ArenaProgress Current
        {
            get { Hydrate(); return GetLive(); }
        }

        /// <summary>Record a victory: +1 win, +1 streak, add the purse, persist.</summary>
        public static void RecordWin(long purseWon)
        {
            Hydrate();
            var a = GetLive();
            a.Wins += 1;
            a.Streak += 1;
            a.TotalPurse += System.Math.Max(0, purseWon);
            SetLive(a);
            Persist(a);
            FlowTrace.Step("ArenaProgress", $"RecordWin purse={purseWon} -> {a.Wins}W/{a.Losses}L streak={a.Streak}");
            Debug.Log($"[ArenaProgressStore] WIN recorded - {a.Wins}W/{a.Losses}L, streak {a.Streak}, purse {a.TotalPurse}.");
        }

        /// <summary>Record a loss: +1 loss, streak reset to 0, persist.</summary>
        public static void RecordLoss()
        {
            Hydrate();
            var a = GetLive();
            a.Losses += 1;
            a.Streak = 0;
            SetLive(a);
            Persist(a);
            FlowTrace.Step("ArenaProgress", $"RecordLoss -> {a.Wins}W/{a.Losses}L streak reset");
            Debug.Log($"[ArenaProgressStore] LOSS recorded - {a.Wins}W/{a.Losses}L, streak reset.");
        }

        // --- live GameState.Arena helpers (null-safe) ------------------------
        private static ArenaProgress GetLive()
        {
            var state = GameStateService.Instance?.State;
            return state != null ? state.Arena : _prefsFallback;
        }

        private static void SetLive(ArenaProgress a)
        {
            var state = GameStateService.Instance?.State;
            if (state != null) state.Arena = a;
            _prefsFallback = a;
        }

        // Used only when there is no live GameState (so the record still functions).
        private static ArenaProgress _prefsFallback = ArenaProgress.Empty;

        // --- persistence (PlayerPrefs mirror) --------------------------------
        private static void Hydrate()
        {
            if (_hydrated) return;
            _hydrated = true;

            // If the save layer already populated GameState.Arena with a non-empty
            // record, trust it; otherwise pull the PlayerPrefs mirror into the live
            // struct so the W/L record survives restarts (the unwired-schema bridge).
            var live = GetLive();
            if (live.TotalRaids == 0 && PlayerPrefs.HasKey(KeyWins))
            {
                var a = new ArenaProgress
                {
                    Wins = PlayerPrefs.GetInt(KeyWins, 0),
                    Losses = PlayerPrefs.GetInt(KeyLosses, 0),
                    Streak = PlayerPrefs.GetInt(KeyStreak, 0),
                };
                long.TryParse(PlayerPrefs.GetString(KeyPurse, "0"), out a.TotalPurse);
                SetLive(a);
            }
        }

        private static void Persist(ArenaProgress a)
        {
            PlayerPrefs.SetInt(KeyWins, a.Wins);
            PlayerPrefs.SetInt(KeyLosses, a.Losses);
            PlayerPrefs.SetInt(KeyStreak, a.Streak);
            PlayerPrefs.SetString(KeyPurse, a.TotalPurse.ToString());
            PlayerPrefs.Save();
            // Also nudge the GameState save (best-effort) so an in-flight save snapshot
            // captures the live struct; harmless if the schema doesn't round-trip it yet.
            GameStateService.Instance?.Save();
        }
    }
}
