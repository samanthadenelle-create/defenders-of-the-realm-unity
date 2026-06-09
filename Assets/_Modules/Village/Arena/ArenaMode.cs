// =============================================================================
// ArenaMode — the async-PvP RAID flow controller (ARENA MVP, the PvP end-state).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// THE LOOP (first bite of async-PvP): enter Arena -> pick a SEEDED opponent
// (ArenaCatalog) -> STAKE a SKR wager (ArenaWalletService.Debit; blocked if you
// can't afford) -> GENERATE the opponent's base (REUSE EnemyOutpost pointed at the
// opponent's base-recipe via ConfigureArena -> OutpostFoundationGenerator.Realize +
// the existing garrison spawn) at a raid anchor near the hero -> RAID (FULL COMBAT
// REUSE: the garrison are real Enemy; the hero + party auto-fight via TargetManager
// -- ZERO new combat code) -> WIN (EnemyOutpost.OnCleared -> Credit 2x wager +
// record win + GrantArenaLoot) / LOSE (hero down OR timeout -> forfeit stake +
// record loss) -> RESULT readout.
//
// REUSE: EnemyOutpost (raid target + garrison + clear + OnCleared + loot),
// OutpostFoundationGenerator.Realize (opponent base, via EnemyOutpost), TargetManager
// combat (NO new combat code), the EnemyOutpost loot table (GrantArenaLoot).
//
// SCOPE (MVP): SEEDED opponents (not real matchmaking), client-stub SKR (not on-
// chain), simple W/L record (not ELO/leaderboard). Those are later bites.
//
// LIFETIME: a runtime singleton MonoBehaviour the entry UI drives. It owns the
// spawned opponent outpost + the lose-watcher coroutine and tears them down on
// result. ASCII-only strings; LogWarning, never error.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Audio;
using DeNelle.Core.State;
using DeNelle.Village.World.Camps;

namespace DeNelle.Village.Arena
{
    /// <summary>Outcome of a single Arena raid.</summary>
    public enum ArenaResult { None, Win, Loss }

    /// <summary>
    /// Drives one Arena raid at a time: stake -> spawn opponent -> raid -> win/lose
    /// -> result. The entry UI (ArenaPanel) calls <see cref="TryStartRaid"/> and
    /// listens to <see cref="OnRaidEnded"/>.
    /// </summary>
    public sealed class ArenaMode : MonoBehaviour
    {
        // -- Raid anchor + timing --------------------------------------------
        // Place the opponent base a short walk from the hero (same idea as
        // RaidOutpostSystem's walk-to anchor) so the existing locomotion + combat
        // carry the fight with no teleport API.
        private const float RaidAnchorDistance = 24f;
        // A raid that hasn't been won within this budget is a forfeit (a stuck/AFK
        // fight loses the stake). Generous so a real fight is never cut short.
        private const float RaidTimeoutSeconds = 180f;

        private static ArenaMode _instance;

        /// <summary>The live ArenaMode (creates a persistent host on first access).</summary>
        public static ArenaMode Instance
        {
            get
            {
                if (_instance == null)
                {
                    var host = new GameObject("ArenaMode");
                    DontDestroyOnLoad(host);
                    _instance = host.AddComponent<ArenaMode>();
                }
                return _instance;
            }
        }

        /// <summary>True while a raid is in progress (UI blocks a second start).</summary>
        public bool RaidInProgress { get; private set; }

        /// <summary>The opponent of the in-flight (or last) raid.</summary>
        public ArenaOpponentDef CurrentOpponent { get; private set; }

        /// <summary>
        /// WO-388 toggle (default OFF): when ON, an Arena raid fights the PLAYER'S OWN
        /// built castle (GameState.BaseLayout) as the defender base instead of the
        /// seeded ArenaCatalog fort, and runtime-bakes a local NavMesh for it. Default
        /// OFF keeps the verified seeded path identical. Bound to the ArenaPanel
        /// "Use My Castle" toggle.
        /// </summary>
        public bool UsePlayerCastle;

        /// <summary>
        /// Raised when a raid ends: (opponent, result, skrDelta). skrDelta is +purse
        /// on a win (net of the staked wager already debited) or -wager on a loss.
        /// </summary>
        public event Action<ArenaOpponentDef, ArenaResult, long> OnRaidEnded;

        private EnemyOutpost _outpost;
        private GameObject _outpostHost;
        private Coroutine _watcher;
        private long _stakedWager;
        private bool _resolved;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        /// <summary>
        /// Stake the wager and start the raid against <paramref name="opponent"/>.
        /// Returns false (no charge, no spawn) if a raid is already running, the
        /// opponent is null, or the SKR balance can't cover the stake.
        /// </summary>
        public bool TryStartRaid(ArenaOpponentDef opponent)
        {
            if (RaidInProgress)
            {
                Debug.LogWarning("[ArenaMode] a raid is already in progress - ignored.");
                return false;
            }
            if (opponent == null)
            {
                Debug.LogWarning("[ArenaMode] null opponent - ignored.");
                return false;
            }

            // STAKE: debit the SKR wager up front. Blocked if insufficient.
            // STUB — replace with WalletService (CurrencyKind.Skr) backend escrow when deployed.
            if (!ArenaWalletService.Debit(opponent.Wager))
            {
                Debug.LogWarning($"[ArenaMode] cannot afford {opponent.Wager} SKR wager - raid blocked.");
                return false;
            }

            CurrentOpponent = opponent;
            _stakedWager = opponent.Wager;
            _resolved = false;
            RaidInProgress = true;

            SpawnOpponentBase(opponent);

            // WIN watch: the outpost fires OnCleared the instant its last defender dies.
            if (_outpost != null) _outpost.OnCleared += HandleOutpostCleared;

            // LOSE watch: hero down OR the raid times out.
            _watcher = StartCoroutine(WatchForLoss());

            // ARENA BGM: crossfade to "Echo's theme" (soft, looping) for the raid.
            // arena BGM — soft background. The Audio assembly owns the soft mix
            // volume + loop (MusicTrackRegistry.Arena); Village reaches it only via
            // the Core seam. ReturnToAmbient on EndRaid restores the explore music.
            CoreServices.Audio?.PlayMusic(MusicTrack.Arena);

            Debug.Log($"[ArenaMode] Raid started vs '{opponent.DisplayName}' (tier {opponent.Tier}, stake {opponent.Wager} SKR).");
            return true;
        }

        // GENERATE the opponent's base: REUSE EnemyOutpost pointed at the seeded
        // base-recipe (ConfigureArena -> Realize the layout + spawn the garrison).
        // Spawned at a raid anchor a short walk from the hero so the EXISTING combat
        // pulls the fight together (no new combat / targeting / teleport code).
        private void SpawnOpponentBase(ArenaOpponentDef opponent)
        {
            Vector3 anchor = ResolveRaidAnchor();

            _outpostHost = new GameObject($"ArenaOutpost_{opponent.Id}");
            _outpostHost.transform.position = anchor;

            _outpost = _outpostHost.AddComponent<EnemyOutpost>();
            // Region default is fine; threat + recipe + garrison come from the opponent.
            // WO-388: the defender recipe is the player's OWN castle when "Use My Castle"
            // is ON (and a base exists), else the seeded opponent fort.
            _outpost.ConfigureArena(opponent.Id, opponent.Threat, GetDefenderRecipe(opponent), opponent.GuardCount);
            // EnemyOutpost.Start() (this same frame) realizes the fort + spawns the garrison.

            // WO-388: a player castle has NO pre-baked NavMesh + no ground at the raid
            // anchor, so bake a local walkable surface at runtime (toggle-gated). The
            // baker's coroutine waits for the fort to realize before baking. The seeded
            // path (toggle OFF) keeps relying on the existing scene mesh - untouched.
            if (UsePlayerCastle)
            {
                var baker = _outpostHost.AddComponent<ArenaNavMeshBaker>();
                baker.BakeForCastle(_outpostHost.transform);
            }
        }

        // WO-388: resolve the defender base recipe. When "Use My Castle" is ON and the
        // player has a non-empty built base, fight that castle (GameState.BaseLayout);
        // otherwise the seeded opponent fort. An EMPTY player base returns the seeded
        // recipe (NOT an empty list) so EnemyOutpost's `_arenaRecipe ?? generated`
        // fallback fires correctly. Default OFF = the verified seeded path.
        private List<PlacedStructureData> GetDefenderRecipe(ArenaOpponentDef opponent)
        {
            if (UsePlayerCastle)
            {
                var state = GameStateService.Instance?.State;
                if (state?.BaseLayout != null && state.BaseLayout.Count > 0)
                {
                    Debug.Log($"[ArenaMode] Defender = player's castle ({state.BaseLayout.Count} structures).");
                    return state.BaseLayout;
                }
                Debug.LogWarning("[ArenaMode] Use-My-Castle on but no player base - falling back to seeded fort.");
            }
            return opponent.BaseRecipe;   // seeded (BuildFortification's ?? also covers null)
        }

        // A walk-to anchor in front of the hero (mirrors RaidOutpostSystem's idea).
        // Falls back to a fixed point if no hero is found.
        private static Vector3 ResolveRaidAnchor()
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero != null)
            {
                Vector3 fwd = hero.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                return hero.transform.position + fwd.normalized * RaidAnchorDistance;
            }
            return new Vector3(RaidAnchorDistance, 0f, 0f);
        }

        // -- WIN -------------------------------------------------------------
        private void HandleOutpostCleared(EnemyOutpost o)
        {
            if (_resolved) return;
            _resolved = true;

            long opponentWager = CurrentOpponent != null ? CurrentOpponent.Wager : _stakedWager;
            long purse = opponentWager * 2L;   // your stake back + theirs

            // CREDIT the won purse. STUB — replace with WalletService payout on deploy.
            ArenaWalletService.Credit(purse);
            // RECORD the win (W/L ledger + total purse).
            ArenaProgressStore.RecordWin(purse);
            // LOOT: the victor still gets the standard threat-scaled clear drop ON TOP
            // of the purse (full reuse of EnemyOutpost's loot table).
            if (_outpost != null) _outpost.GrantArenaLoot();

            Debug.Log($"[ArenaMode] WIN vs '{NameOf()}' - purse {purse} SKR credited.");
            // skrDelta = net SKR gained = purse - staked wager (the +profit on the screen).
            EndRaid(ArenaResult.Win, purse - _stakedWager);
        }

        // -- LOSE ------------------------------------------------------------
        private IEnumerator WatchForLoss()
        {
            float deadline = Time.time + RaidTimeoutSeconds;
            while (!_resolved && RaidInProgress)
            {
                // Hero down -> forfeit (the hero respawns elsewhere, but the raid is
                // lost: HeroHealth is the live hero's HP; null/zero = down).
                if (HeroIsDown())
                {
                    HandleLoss("hero down");
                    yield break;
                }
                // Timeout -> forfeit the stake.
                if (Time.time >= deadline)
                {
                    HandleLoss("timeout");
                    yield break;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        // The hero is "down" when its HeroHealth exists and is no longer alive.
        private static bool HeroIsDown()
        {
            var hh = HeroHealth.Instance;
            return hh != null && !hh.IsAlive;
        }

        private void HandleLoss(string reason)
        {
            if (_resolved) return;
            _resolved = true;

            // FORFEIT: the stake was already debited at start, so a loss just records
            // it (no refund). STUB — backend escrow keeps the real stake on deploy.
            ArenaProgressStore.RecordLoss();
            Debug.Log($"[ArenaMode] LOSS vs '{NameOf()}' ({reason}) - forfeited {_stakedWager} SKR.");
            EndRaid(ArenaResult.Loss, -_stakedWager);
        }

        // -- teardown --------------------------------------------------------
        private void EndRaid(ArenaResult result, long skrDelta)
        {
            if (_watcher != null) { StopCoroutine(_watcher); _watcher = null; }
            if (_outpost != null) _outpost.OnCleared -= HandleOutpostCleared;

            var opponent = CurrentOpponent;
            RaidInProgress = false;

            // Despawn the opponent base now the raid is resolved (Arena owns its lifetime).
            if (_outpostHost != null) Destroy(_outpostHost);
            _outpostHost = null;
            _outpost = null;

            // Restore the prior (explore) music so "Echo's theme" doesn't bleed past
            // the raid. The raid plays in the open world by the hero, so return to the
            // Overworld ambient — the bridge routes this through PlayAmbientContext,
            // which restores the player's chosen explore track (WIN / LOSS / timeout
            // all funnel through EndRaid).
            CoreServices.Audio?.PlayMusic(MusicTrack.Overworld);

            OnRaidEnded?.Invoke(opponent, result, skrDelta);
        }

        private string NameOf() => CurrentOpponent != null ? CurrentOpponent.DisplayName : "opponent";
    }
}
