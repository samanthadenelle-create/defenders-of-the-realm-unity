// =============================================================================
// LeaderboardVM — the PURE ViewModel behind LeaderboardPanel (strict-MVVM Silo E).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// ALL leaderboard state + the async fetch that used to live in the Leaderboard-
// Panel VIEW now lives here:
//   * implements IPanelViewModel (Title / Changed / Close / Dispose).
//   * NO UnityEngine UI types — unit-testable without a scene.
//   * OWNS the async FetchTopAsync: SelectMetric / Refresh drive the fetch, the
//     result is projected into UI-free Row structs and Changed fires.
//   * projects GetLocalProfile + IsLocalStub/SourceLabel into ready-to-render
//     strings so the View reads vm.* only and never names LeaderboardService.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Services;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.HUD
{
    /// <summary>
    /// Pure ViewModel for the leaderboard modal. Holds the active metric, the local
    /// profile lines, the fetched ranked rows (projected), and the honest source
    /// footer. Raises <see cref="Changed"/> after a fetch completes / the source swaps.
    /// </summary>
    public sealed class LeaderboardVM : IPanelViewModel, IDisposable
    {
        // ── Seam over LeaderboardService (fake in tests; singleton live). ──
        public interface ISource
        {
            event Action Changed;                                          // LeaderboardService.Changed
            PlayerProfile GetLocalProfile();                               // LeaderboardService.GetLocalProfile()
            void FetchTopAsync(LeaderboardMetric metric, int limit,
                               Action<IReadOnlyList<LeaderboardEntry>> onResult);  // FetchTopAsync
            bool IsLocalStub { get; }                                      // LeaderboardService.IsLocalStub
            string SourceLabel { get; }                                    // LeaderboardService.SourceLabel
        }

        /// <summary>One projected leaderboard row (all strings ready to render, no LeaderboardEntry leak).</summary>
        public readonly struct Row
        {
            public readonly string Rank;
            public readonly string Name;
            public readonly string Score;
            public readonly bool IsLocal;
            public readonly int Index;     // zebra striping
            public Row(string rank, string name, string score, bool isLocal, int index)
            { Rank = rank; Name = name; Score = score; IsLocal = isLocal; Index = index; }
        }

        private const int FetchLimit = 20;

        private readonly ISource _source;
        private readonly Action _onClose;
        private readonly Action _changedHandler;
        private bool _disposed;

        private readonly List<Row> _rows = new List<Row>();

        public static LeaderboardVM CreateDefault(Action onClose)
            => new LeaderboardVM(new ServiceSource(), onClose);

        public LeaderboardVM(ISource source, Action onClose)
        {
            _source = source;
            _onClose = onClose;
            if (_source != null)
            {
                _changedHandler = OnSourceChanged;
                _source.Changed += _changedHandler;
            }
            Refresh();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────
        public event Action Changed;
        public string Title => "Leaderboard";
        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_source != null && _changedHandler != null) _source.Changed -= _changedHandler;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>The active ranked metric (Best Wave / Crystals / Arena Wins).</summary>
        public LeaderboardMetric Metric { get; private set; } = LeaderboardMetric.BestWave;

        /// <summary>Profile header line ("You - Ranger   #CODE").</summary>
        public string ProfileHeroLine { get; private set; } = "";

        /// <summary>Profile stats line ("Best Wave 12    Crystals 340    Magic 8    Arena 3-1").</summary>
        public string ProfileStatsLine { get; private set; } = "";

        /// <summary>Honest source footer (names the offline stub when applicable).</summary>
        public string FooterText { get; private set; } = "";

        /// <summary>Projected ranked rows (a single "No entries yet." row when empty). Never null.</summary>
        public IReadOnlyList<Row> Rows => _rows;

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Switch the ranked metric + re-fetch. No-op if already active.</summary>
        public void SelectMetric(LeaderboardMetric metric)
        {
            Metric = metric;
            Refresh();
        }

        /// <summary>Re-pull profile + footer + the ranked rows for the active metric.</summary>
        public void Refresh()
        {
            if (_source == null) { _rows.Clear(); Raise(); return; }

            RebuildProfile(_source.GetLocalProfile());

            FooterText = _source.IsLocalStub
                ? "Source: " + _source.SourceLabel + ". Scores are local; ranks shown are placeholder rivals until the online ladder is connected."
                : "Source: " + _source.SourceLabel + ".";

            // Owns the async fetch: the stub completes synchronously, a live source later.
            _source.FetchTopAsync(Metric, FetchLimit, RebuildRows);
        }

        // ── Projection (moved verbatim from the View) ───────────────────────────

        private void OnSourceChanged() => Refresh();

        private void RebuildProfile(PlayerProfile p)
        {
            if (p == null) { ProfileHeroLine = ""; ProfileStatsLine = ""; return; }

            var heroLine = string.IsNullOrEmpty(p.HeroClass) || p.HeroClass == "None"
                ? p.DisplayName
                : p.DisplayName + " - " + p.HeroClass;
            string code = string.IsNullOrEmpty(p.InviteCode) ? "" : "   #" + p.InviteCode;
            ProfileHeroLine = heroLine + code;
            ProfileStatsLine = "Best Wave " + p.BestWave + "    Crystals " + p.Crystals
                             + "    Magic " + p.Magic + "    Arena " + p.ArenaWins + "-" + p.ArenaLosses;
        }

        private void RebuildRows(IReadOnlyList<LeaderboardEntry> rows)
        {
            _rows.Clear();
            if (rows == null || rows.Count == 0)
            {
                _rows.Add(new Row("-", "No entries yet.", "", false, 0));
                Raise();
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                var e = rows[i];
                _rows.Add(new Row(e.Rank.ToString(), e.Name ?? "?", e.Score.ToString(), e.IsLocalPlayer, i));
            }
            Raise();
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }

        // ── Real seam: wraps LeaderboardService (SOLE live resolution site). ──
        private sealed class ServiceSource : ISource
        {
            public event Action Changed
            {
                add    { if (LeaderboardService.Instance != null) LeaderboardService.Instance.Changed += value; }
                remove { if (LeaderboardService.Instance != null) LeaderboardService.Instance.Changed -= value; }
            }

            public PlayerProfile GetLocalProfile() => LeaderboardService.Instance?.GetLocalProfile();

            public void FetchTopAsync(LeaderboardMetric metric, int limit,
                                      Action<IReadOnlyList<LeaderboardEntry>> onResult)
            {
                if (LeaderboardService.Instance != null)
                    LeaderboardService.Instance.FetchTopAsync(metric, limit, onResult);
                else
                    onResult?.Invoke(System.Array.Empty<LeaderboardEntry>());
            }

            public bool IsLocalStub => LeaderboardService.Instance == null || LeaderboardService.Instance.IsLocalStub;
            public string SourceLabel => LeaderboardService.Instance != null ? LeaderboardService.Instance.SourceLabel : "Local (offline)";
        }
    }
}
