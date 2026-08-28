// =============================================================================
// LeaderboardService — client-side leaderboard + player-profile data provider.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Services
//
// SCOPE: CLIENT-SIDE, STUB-OR-LIVE. The React/Vercel backend was NEVER deployed
// (the client services have real Vercel URLs but are unauthed / stubbed — see
// ServerConfig + the "backend never connected" project note). So this ships
// resilient: it reads from an injectable ILeaderboardSource. The default source
// is LocalStubLeaderboardSource, which returns:
//   • the LOCAL player's REAL profile (pulled from GameState — best wave, crystals,
//     magic, arena wins, invite code), and
//   • a handful of clearly-labelled SAMPLE rivals so the board renders meaningfully.
// It does NOT fake a network round-trip and does NOT claim to be "live" — the
// source advertises IsLocalStub = true so the UI can show a "local / offline"
// badge honestly.
//
// DROP-IN-LATER: when the backend deploys, implement a RemoteLeaderboardSource
// (an ILeaderboardSource that GETs e.g. {ServerConfig host}/api/leaderboard/top
// and POSTs the local profile to submit a score) and call
// LeaderboardService.Instance.SetSource(new RemoteLeaderboardSource(...)). The UI
// and the rest of the game never change — only the source swaps. The async shape
// (FetchTopAsync / SubmitLocalAsync with a callback) is already network-ready, so
// the stub path simply completes synchronously.
//
// PATTERN MODEL: ClanService.cs — singleton MonoBehaviour, RuntimeInitializeOnLoad
// bootstrap, a Changed event for UI repaint, local-stub-now / network-bridge-later.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using DeNelle.Core.State;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Core.Services
{
    /// <summary>
    /// Which metric a leaderboard is ranked by. Highest-first for all of these.
    /// </summary>
    public enum LeaderboardMetric
    {
        BestWave,
        Crystals,
        ArenaWins,
    }

    /// <summary>
    /// One ranked row. Pure data so a future network source can deserialise it
    /// straight from JSON. <see cref="IsLocalPlayer"/> lets the UI highlight the
    /// player's own row. ASCII-only fields.
    /// </summary>
    [Serializable]
    public sealed class LeaderboardEntry
    {
        /// <summary>1-based rank within the returned page.</summary>
        public int Rank;
        /// <summary>Display name (local player = "You"; samples are flavour names).</summary>
        public string Name;
        /// <summary>The metric value this row is ranked by (wave / crystals / wins).</summary>
        public long Score;
        /// <summary>True for the local player's own row, so the UI can highlight it.</summary>
        public bool IsLocalPlayer;
    }

    /// <summary>
    /// The local player's profile card — their identity + headline stats. Sourced
    /// from GameState by the stub; a live source would merge server-authoritative
    /// totals later.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public string DisplayName;
        public string InviteCode;   // GameState.MyInviteCode (may be empty until generated)
        public int BestWave;
        public int Crystals;
        public int Magic;
        public int ArenaWins;
        public int ArenaLosses;
        public string HeroClass;    // "None" when unchosen
    }

    /// <summary>
    /// The pluggable data source. The stub returns local+sample data synchronously;
    /// a real backend client implements the SAME interface (drop-in). Callbacks (not
    /// raw return values) so the network implementation can complete asynchronously
    /// without changing callers.
    /// </summary>
    public interface ILeaderboardSource
    {
        /// <summary>
        /// True when this source serves local/offline placeholder data (NOT a live
        /// server). The UI shows an honest "local" badge when this is true.
        /// </summary>
        bool IsLocalStub { get; }

        /// <summary>Human-readable source label for the UI footer, e.g. "Local (offline)".</summary>
        string SourceLabel { get; }

        /// <summary>
        /// Fetch the top entries for a metric. <paramref name="onResult"/> is invoked
        /// with the ranked rows (local player's row included + flagged). Never null.
        /// </summary>
        void FetchTopAsync(LeaderboardMetric metric, int limit, Action<IReadOnlyList<LeaderboardEntry>> onResult);

        /// <summary>The local player's profile card.</summary>
        PlayerProfile GetLocalProfile();

        /// <summary>
        /// Submit the local player's current scores. The stub no-ops (scores are read
        /// live from GameState anyway); a live source POSTs them. <paramref name="onDone"/>
        /// reports success/failure honestly.
        /// </summary>
        void SubmitLocalAsync(Action<bool> onDone);
    }

    // =========================================================================
    // LocalStubLeaderboardSource — the DEFAULT, offline source.
    // =========================================================================
    /// <summary>
    /// Reads the local player's real stats from GameState and pads the board with a
    /// few clearly-fictional sample rivals so the screen renders. Honest about being
    /// local: <see cref="IsLocalStub"/> = true. No network, no faked success.
    /// </summary>
    public sealed class LocalStubLeaderboardSource : ILeaderboardSource
    {
        public bool IsLocalStub => true;
        public string SourceLabel => "Local (offline) — sample ladder";

        // Fictional rivals. Scores are deliberately spread so the local player can
        // land anywhere in the table depending on their real progress.
        private static readonly (string Name, int Wave, int Crystals, int Wins)[] Samples =
        {
            ("Aldric the Bold",  28, 540, 17),
            ("Mira Stormcaller", 22, 410, 12),
            ("Thane Ironwood",   19, 360,  9),
            ("Sable Nightwind",  15, 280,  6),
            ("Bram Hollowfist",  11, 190,  3),
            ("Yara Dawnseeker",   8, 120,  1),
        };

        public PlayerProfile GetLocalProfile()
        {
            var s = SafeState();
            if (s == null)
            {
                return new PlayerProfile
                {
                    DisplayName = "You",
                    InviteCode  = string.Empty,
                    BestWave    = 0,
                    Crystals    = 0,
                    Magic       = 0,
                    ArenaWins   = 0,
                    ArenaLosses = 0,
                    HeroClass   = "None",
                };
            }

            return new PlayerProfile
            {
                DisplayName = "You",
                InviteCode  = s.MyInviteCode ?? string.Empty,
                BestWave    = Mathf.Max(0, s.BestWave),
                Crystals    = Mathf.Max(0, s.Resources.Crystals),
                Magic       = Mathf.Max(0, s.Magic),
                ArenaWins   = Mathf.Max(0, s.Arena.Wins),
                ArenaLosses = Mathf.Max(0, s.Arena.Losses),
                HeroClass   = s.HeroClass.ToString(),
            };
        }

        public void FetchTopAsync(LeaderboardMetric metric, int limit,
                                  Action<IReadOnlyList<LeaderboardEntry>> onResult)
        {
            var rows = BuildBoard(metric);
            if (limit > 0 && rows.Count > limit)
                rows = rows.GetRange(0, limit);
            onResult?.Invoke(rows);
        }

        public void SubmitLocalAsync(Action<bool> onDone)
        {
            // Local stub: nothing to submit — the board reads GameState live. Report
            // false for "did not reach a server" so the UI never lies about syncing.
            onDone?.Invoke(false);
        }

        private List<LeaderboardEntry> BuildBoard(LeaderboardMetric metric)
        {
            var profile = GetLocalProfile();
            int localScore = ScoreFor(metric, profile.BestWave, profile.Crystals, profile.ArenaWins);

            var raw = new List<(string Name, long Score, bool Local)>();
            foreach (var samp in Samples)
                raw.Add((samp.Name, ScoreFor(metric, samp.Wave, samp.Crystals, samp.Wins), false));
            raw.Add((profile.DisplayName, localScore, true));

            // Highest-first; on a tie the local player sorts ahead so "You" is visible.
            raw.Sort((a, b) =>
            {
                int cmp = b.Score.CompareTo(a.Score);
                if (cmp != 0) return cmp;
                return b.Local.CompareTo(a.Local);
            });

            var rows = new List<LeaderboardEntry>(raw.Count);
            for (int i = 0; i < raw.Count; i++)
            {
                rows.Add(new LeaderboardEntry
                {
                    Rank          = i + 1,
                    Name          = raw[i].Name,
                    Score         = raw[i].Score,
                    IsLocalPlayer = raw[i].Local,
                });
            }
            return rows;
        }

        private static int ScoreFor(LeaderboardMetric metric, int wave, int crystals, int wins)
        {
            switch (metric)
            {
                case LeaderboardMetric.Crystals:  return crystals;
                case LeaderboardMetric.ArenaWins: return wins;
                case LeaderboardMetric.BestWave:
                default:                          return wave;
            }
        }

        private static GameState SafeState()
        {
            var svc = GameStateService.Instance;
            return svc != null ? svc.State : null;
        }
    }

    /// <summary>Public, read-only production leaderboard source. Failures return an empty board.</summary>
    public sealed class RemoteLeaderboardSource : ILeaderboardSource
    {
        private const string TopUrl =
            "https://defenders-of-the-realm-v2.vercel.app/api/leaderboard/get" +
            "?metric=highest_wave&period=alltime&limit=";
        private readonly MonoBehaviour _host;

        public RemoteLeaderboardSource(MonoBehaviour host) => _host = host;
        public bool IsLocalStub => false;
        public string SourceLabel => "Live - all-time";

        public void FetchTopAsync(LeaderboardMetric metric, int limit,
                                  Action<IReadOnlyList<LeaderboardEntry>> onResult)
        {
            if (_host == null || metric != LeaderboardMetric.BestWave)
            {
                onResult?.Invoke(Array.Empty<LeaderboardEntry>());
                return;
            }
            _host.StartCoroutine(Fetch(Mathf.Clamp(limit, 1, 100), onResult));
        }

        public PlayerProfile GetLocalProfile() => new LocalStubLeaderboardSource().GetLocalProfile();
        public void SubmitLocalAsync(Action<bool> onDone) => onDone?.Invoke(false);

        private IEnumerator Fetch(int limit, Action<IReadOnlyList<LeaderboardEntry>> onResult)
        {
            using var req = UnityWebRequest.Get(TopUrl + limit);
            req.timeout = 8;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onResult?.Invoke(Array.Empty<LeaderboardEntry>());
                yield break;
            }
            try
            {
                var reply = JsonConvert.DeserializeObject<RemoteReply>(req.downloadHandler.text);
                var rows = new List<LeaderboardEntry>();
                if (reply?.success == true && reply.top != null)
                {
                    foreach (var row in reply.top)
                    {
                        if (row == null || row.rank <= 0) continue;
                        rows.Add(new LeaderboardEntry
                        {
                            Rank = row.rank,
                            Name = DisplayName(row.username, row.wallet),
                            Score = Math.Max(0L, row.score),
                            IsLocalPlayer = false,
                        });
                    }
                }
                onResult?.Invoke(rows);
            }
            catch
            {
                onResult?.Invoke(Array.Empty<LeaderboardEntry>());
            }
        }

        private static string DisplayName(string username, string wallet)
        {
            if (!string.IsNullOrWhiteSpace(username)) return username.Trim();
            if (string.IsNullOrWhiteSpace(wallet)) return "Watchkeeper";
            wallet = wallet.Trim();
            return wallet.Length <= 10 ? wallet : wallet.Substring(0, 4) + "..." + wallet.Substring(wallet.Length - 4);
        }

        private sealed class RemoteReply
        {
            public bool success;
            public List<RemoteRow> top;
        }

        private sealed class RemoteRow
        {
            public int rank;
            public string wallet;
            public string username;
            public long score;
        }
    }

    // =========================================================================
    // LeaderboardService — singleton front door over the active source.
    // =========================================================================
    /// <summary>
    /// Singleton MonoBehaviour that holds the active <see cref="ILeaderboardSource"/>
    /// (default: <see cref="LocalStubLeaderboardSource"/>) and forwards queries to it.
    /// Swap the source with <see cref="SetSource"/> when the backend lands — nothing
    /// else changes. Mirrors ClanService's bootstrap + Changed-event shape.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardService : MonoBehaviour
    {
        public static LeaderboardService Instance { get; private set; }

        /// <summary>Raised when the active source changes (so open panels repaint).</summary>
        public event Action Changed;

        private ILeaderboardSource _source;

        /// <summary>The active source. Never null (defaults to the local stub).</summary>
        public ILeaderboardSource Source
        {
            get
            {
                if (_source == null) _source = new RemoteLeaderboardSource(this);
                return _source;
            }
        }

        /// <summary>True when the active source is serving local/offline data.</summary>
        public bool IsLocalStub => Source.IsLocalStub;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("LeaderboardService");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<LeaderboardService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_source == null) _source = new RemoteLeaderboardSource(this);
        }

        /// <summary>
        /// Replace the data source (e.g. a RemoteLeaderboardSource once the backend
        /// deploys). Null restores the local stub. Fires <see cref="Changed"/>.
        /// </summary>
        public void SetSource(ILeaderboardSource source)
        {
            _source = source ?? new LocalStubLeaderboardSource();
            Changed?.Invoke();
        }

        // ── Forwarding API (what the UI calls) ───────────────────────────────

        public void FetchTopAsync(LeaderboardMetric metric, int limit,
                                  Action<IReadOnlyList<LeaderboardEntry>> onResult)
            => Source.FetchTopAsync(metric, limit, onResult);

        public PlayerProfile GetLocalProfile() => Source.GetLocalProfile();

        public void SubmitLocalAsync(Action<bool> onDone) => Source.SubmitLocalAsync(onDone);

        public string SourceLabel => Source.SourceLabel;
    }
}
