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
using UnityEngine.AI;
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

        /// <summary>
        /// WO-389 (#3) — the ATTACK squad roster (ArenaDefenseDef ids) the player
        /// recruited for the NEXT attack raid. Set by ArenaAttackRecruitController on
        /// Launch; READ once at raid start to spawn the friendly squad hero-leashed to
        /// the Captain, then CLEARED so it never bleeds into a later defend raid. A null
        /// / empty squad = no attack-squad spawn (the seeded/defend paths are untouched).
        /// MVP store: a simple static (no SaveSchema round-trip — the squad is a
        /// per-raid choice, not persisted state). // TODO data-driven: persist if needed.
        /// </summary>
        public static List<string> AttackSquad;

        // The number of recruited attackers spawned this raid (for teardown / logging).
        private readonly List<GameObject> _spawnedSquad = new List<GameObject>();

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

            // WO-389 (#3) — ATTACK FLOW: if the player recruited a squad, spawn it now,
            // hero-leashed to the Captain (the player hero), so it follows + auto-fights
            // the garrison. Gracefully no-ops on the defend / seeded path (null squad).
            SpawnAttackSquad();

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

            // FIX (empty-Arena bug): listen for a FAILED garrison spawn so an outpost that
            // could NOT place its garrison on navmesh ends the raid as a NON-win (no purse)
            // instead of mis-firing the open-world auto-Clear() as an instant WIN.
            _outpost.OnArenaSpawnFailed += HandleArenaSpawnFailed;

            // EnemyOutpost.Start() (this same frame) realizes the fort + spawns the garrison.

            // The SEEDED castle-hub raid does NOT runtime-bake: FIX 1 (ResolveRaidAnchor's
            // NavMesh.SamplePosition snap) places the opponent ON the hub's PRE-baked floor,
            // and the fort's carving NavMeshObstacles cut the walls in real-time — so the
            // garrison + player path on the EXISTING mesh, no bake needed.
            // The runtime bake is a SYNCHRONOUS BuildNavMesh on the main thread; running it
            // every raid FROZE the game (it also needs read-enabled meshes for player builds).
            // So it stays restricted to the UsePlayerCastle path — the imported player castle
            // has NO pre-baked mesh, so it genuinely needs a local surface.
            if (UsePlayerCastle)
            {
                var baker = _outpostHost.AddComponent<ArenaNavMeshBaker>();
                baker.BakeForCastle(_outpostHost.transform);
            }
        }

        // WO-389 (#3) — ATTACK FLOW: spawn the recruited squad as FRIENDLY units
        // hero-leashed to the Captain (the player hero), fanned in a small ring around
        // them so they don't stack. Each troop id maps to a HeroClass via the catalog
        // (StoryCompanionInjector.SpawnAttacker reuses the companion body + follow +
        // combat — zero new combat code). STRUCTURES (e.g. Ballista) are skipped for
        // MVP (an attack squad is mobile units; a placed structure can't follow). The
        // squad is CLEARED after reading so it never re-spawns on a later defend raid.
        private void SpawnAttackSquad()
        {
            var squad = AttackSquad;
            AttackSquad = null;   // consume — a per-raid choice, never reused
            if (squad == null || squad.Count == 0) return;

            Transform captain = ResolveCaptain();
            if (captain == null)
            {
                Debug.LogWarning("[ArenaMode] Attack squad recruited but no Captain (Player) found - squad not spawned.");
                return;
            }

            _spawnedSquad.Clear();
            int i = 0;
            foreach (var id in squad)
            {
                var def = ArenaDefenseCatalog.Get(id);
                if (def == null) continue;

                if (def.Kind != DefenderKind.Unit || !def.UnitClass.HasValue)
                {
                    // MVP: skip structures (Ballista/Shrine) — they can't follow the
                    // Captain. A future bite spawns them via StructureFactory at the
                    // raid anchor. // TODO: structure attackers via StructureFactory.
                    Debug.Log($"[ArenaMode] Attack squad: skipping non-unit troop '{id}' (MVP).");
                    continue;
                }

                // Fan the units in a ring just behind/beside the Captain so they don't
                // stack on one point; SpawnAttacker snaps onto the NavMesh internally
                // via the shared BuildPlaceholder path.
                float angle = (i * 47f) * Mathf.Deg2Rad;   // golden-ish spread
                float radius = 2.0f + 0.6f * i;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Vector3 pos = captain.position - captain.forward * 1.5f + offset;

                var go = StoryCompanionInjector.SpawnAttacker(def.UnitClass.Value, pos, captain);
                if (go != null) _spawnedSquad.Add(go);
                i++;
            }

            Debug.Log($"[ArenaMode] Attack squad spawned: {_spawnedSquad.Count} friendly units leashed to the Captain.");
        }

        // Resolve the player Captain transform (the single hero leading the attack).
        // Mirrors ResolveRaidAnchor's hero lookup (the "Player" tag).
        private static Transform ResolveCaptain()
        {
            var hero = GameObject.FindWithTag("Player");
            return hero != null ? hero.transform : null;
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

        // A walk-to anchor in FRONT of the hero (mirrors RaidOutpostSystem's idea),
        // SNAPPED onto the baked NavMesh so the opponent fort + its garrison land on
        // walkable mesh the player can reach. In the castle hub the raw forward point
        // often lands OFF the baked mesh (the empty-Arena bug): the synchronous garrison
        // spawn then can't snap and the outpost silent-wins. We therefore try the full
        // forward offset first, then progressively closer offsets, and finally the hero's
        // own spot — using the FIRST one that NavMesh.SamplePosition resolves to walkable
        // mesh. Falls back to a fixed point only if no hero is found.
        private static Vector3 ResolveRaidAnchor()
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null)
            {
                // No hero: snap a fixed point onto the mesh if we can, else use it raw.
                Vector3 fixedPt = new Vector3(RaidAnchorDistance, 0f, 0f);
                return SnapAnchorToNav(fixedPt, out Vector3 snapped) ? snapped : fixedPt;
            }

            Vector3 origin = hero.transform.position;
            Vector3 fwd = hero.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();

            // Try the full forward distance first (fort in view, a real walk-to), then
            // shrink toward the hero so we never place the base off-mesh. The fort still
            // spawns in FRONT of the player at whichever distance first lands on mesh.
            float[] distances = { RaidAnchorDistance, 16f, 10f, 6f, 0f };
            foreach (float d in distances)
            {
                Vector3 candidate = origin + fwd * d;
                if (SnapAnchorToNav(candidate, out Vector3 snapped))
                    return snapped;
            }

            // No walkable mesh found anywhere along the ray — return the raw forward
            // point; the runtime ArenaNavMeshBaker then lays a local surface under it.
            return origin + fwd * RaidAnchorDistance;
        }

        // Snap a candidate anchor onto the baked NavMesh (typed SamplePosition — the
        // asmdef references Unity.AI.Navigation). Generous search radius so a point near
        // a path still resolves. Returns false (no walkable mesh nearby) so the caller
        // can fall back to a closer offset.
        private static bool SnapAnchorToNav(Vector3 candidate, out Vector3 result)
        {
            const float SampleRadius = 30f;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            result = candidate;
            return false;
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

        // -- FAILED SPAWN (empty-Arena bug guard) ----------------------------
        // The opponent garrison could NOT spawn (no walkable NavMesh under the raid
        // anchor). EnemyOutpost used to auto-Clear() here, which fired OnCleared -> an
        // instant WIN + purse the player never earned. We now end the raid as a NON-win
        // ABORT: no purse, no recorded win — and (since the stake was already debited and
        // the failure is the game's fault, not the player's) we REFUND the stake by
        // recording a zero-delta abort rather than a loss. The result reads as a
        // no-contest so the entry UI can re-offer the raid.
        private void HandleArenaSpawnFailed(EnemyOutpost o)
        {
            if (_resolved) return;
            _resolved = true;

            // Refund the staked wager — the raid never actually happened (spawn failure),
            // so the player should not forfeit. STUB wallet mirror of TryStartRaid's Debit.
            ArenaWalletService.Credit(_stakedWager);
            Debug.LogError($"[ArenaMode] Raid vs '{NameOf()}' ABORTED - opponent garrison failed to spawn " +
                           "(no NavMesh at the raid anchor). NOT a win; stake refunded.");
            // skrDelta 0 = no net change (stake refunded); result None = no-contest abort.
            EndRaid(ArenaResult.None, 0L);
        }

        // -- teardown --------------------------------------------------------
        private void EndRaid(ArenaResult result, long skrDelta)
        {
            if (_watcher != null) { StopCoroutine(_watcher); _watcher = null; }
            if (_outpost != null)
            {
                _outpost.OnCleared -= HandleOutpostCleared;
                _outpost.OnArenaSpawnFailed -= HandleArenaSpawnFailed;
            }

            var opponent = CurrentOpponent;
            RaidInProgress = false;

            // Despawn the opponent base now the raid is resolved (Arena owns its lifetime).
            if (_outpostHost != null) Destroy(_outpostHost);
            _outpostHost = null;
            _outpost = null;

            // WO-389 (#3) — despawn the recruited attack squad (Arena owns its lifetime,
            // same as the opponent base). Null-safe; some may have already fallen.
            foreach (var unit in _spawnedSquad)
                if (unit != null) Destroy(unit);
            _spawnedSquad.Clear();

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
