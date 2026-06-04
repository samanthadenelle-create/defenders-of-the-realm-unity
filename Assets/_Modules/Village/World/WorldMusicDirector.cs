// =============================================================================
// WorldMusicDirector — swaps Village ↔ Overworld music as the hero crosses the
// wall footprint into the open world (owner 2026-06-02: "there are 2 overworlds
// that can be cycled").
// -----------------------------------------------------------------------------
// The Village and OuterWorld scenes load ADDITIVELY (one contiguous walkable
// space), so scene-based music (AudioService.HandleSceneMusic) can't tell "in
// the village" from "out in the world" — the active scene stays Village the whole
// time. This director closes that gap with a position read:
//
//   ZoneManager.GetZone(heroPos) == Village  → Village music
//   anything else (Goldfields/…/Ashwood)     → Overworld music (cycled pool)
//
// It fires PlayMusic ONLY on a boundary CROSSING (tracked via _lastInWorld), never
// per-poll — re-asserting Overworld every frame would re-roll NextFromPool and
// thrash the track. Each village→world crossing advances the 2-theme overworld
// rotation, which is the cadence the owner asked for ("a loop of either is fine").
//
// Self-bootstrapping DDOL (same pattern as GameOverScreen / AudioBootstrap), so
// there's no scene object to place. Idle (no-op) on Title/Battle/Dungeon where no
// hero exists. DeNelle.Village → Core only (ZoneManager + CoreServices.Audio).
// =============================================================================

using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.World;

namespace DeNelle.Village
{
    /// <summary>Position-driven Village↔Overworld music switch for the additive open world.</summary>
    public sealed class WorldMusicDirector : MonoBehaviour
    {
        public static WorldMusicDirector Instance { get; private set; }

        private const float PollInterval = 0.5f;   // seconds between position reads
        private Transform _hero;
        private bool _lastInWorld;
        private bool _initialised;
        private float _nextPoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("WorldMusicDirector").AddComponent<WorldMusicDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollInterval;

            var hero = ResolveHero();
            if (hero == null) { _initialised = false; return; }

            bool inWorld = ZoneManager.GetZone(hero.position) != RegionId.Village;

            // Seed the state on first acquisition WITHOUT forcing a switch — the
            // hero spawns in the village and HandleSceneMusic has already started
            // village music; we only want to react to subsequent crossings.
            if (!_initialised)
            {
                _initialised = true;
                _lastInWorld = inWorld;
                if (inWorld) Apply(true);   // spawned already out in the world: assert it once
                return;
            }

            if (inWorld != _lastInWorld)
            {
                _lastInWorld = inWorld;
                Apply(inWorld);
            }
        }

        private static void Apply(bool inWorld)
        {
            var track = inWorld
                ? DeNelle.Core.Audio.MusicTrack.Overworld
                : DeNelle.Core.Audio.MusicTrack.Village;
            CoreServices.Audio?.PlayMusic(track);
        }

        // Hero is tagged "Player" for locomotion (CLAUDE.md §7). Fall back to a
        // HeroHealth lookup so the director still resolves if the tag is missing.
        private Transform ResolveHero()
        {
            if (_hero != null) return _hero;
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) { _hero = tagged.transform; return _hero; }
            var hh = HeroHealth.Instance ?? FindFirstObjectByType<HeroHealth>();
            if (hh != null) { _hero = hh.transform; return _hero; }
            return null;
        }
    }
}
