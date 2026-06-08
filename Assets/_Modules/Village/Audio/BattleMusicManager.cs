// =============================================================================
// BattleMusicManager — WO-372: a wave-driven battle MUSIC STATE MACHINE.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT IS
//   A small state machine that scores the village wave loop with four cues and
//   crossfades between them (1.5 s):
//      • Combat   — the general battle loop  (Overworld_Battle_1, loops)
//      • Intense  — 5+ live enemies on the field, high-pressure (Overworld_Battle_2, loops)
//      • Victory  — a wave was cleared; a ONE-SHOT sting (Overworld_Victory, no loop),
//                   after which we hand the music back to the ambient/idle service.
//      • Boss     — a boss / apex wave is live (Overworld_Boss_Fight, loops)
//
// WHY ITS OWN AudioSources (and not AudioService.PlayMusic(Battle))
//   AudioService already owns the SCENE-music director (Title/Village/Battle/…)
//   and a battle POOL it rotates per scene-load; if we drove its PlayMusic we'd
//   (a) fight its scene-load short-circuit and (b) collapse our four distinct
//   wave states into its single "Battle" track. So we REUSE the audio service for
//   what it is good at — the shared mixer + the ambient/idle handoff — but own a
//   dedicated pair of crossfading AudioSources for the battle states, routed
//   THROUGH AudioService's Music mixer group so the player's music volume/mute
//   still applies. We do NOT reinvent the mixer, the volume model, or the idle
//   music: when the Victory sting ends we call AudioService.ReturnToAmbient() so
//   the town/overworld ambient track resumes exactly as before.
//
// HOW IT LISTENS (no WaveManager edits — listen only, per WO-372)
//   WaveManager (same DeNelle.Village assembly) exposes public UnityEvent fields.
//   We FIND the live WaveManager (FindFirstObjectByType) and AddListener to its
//   EXISTING events — exactly how HeroPoseController / TownHudBridge subscribe.
//   We add NO hook inside WaveManager. Mapping:
//      OnWaveStarted        → Combat   (general loop)        — unless it's a boss wave
//      OnApexBossSpawned    → Boss     (boss loop)
//      OnWaveCleared        → Victory  (one-shot, → ambient) — unless a boss is still up
//      OnDefeat             → stop battle music, hand back to ambient (the defeat
//                             screen owns its own sting via AudioService.Defeat)
//   Live-enemy pressure is polled (0.5 s): ≥ IntenseEnemyThreshold live enemies in
//   a non-boss combat state crossfades Combat ↔ Intense. The poll is also the
//   safety net that re-binds to a WaveManager that spawned after us / re-evaluates
//   from WaveManager.Phase + LiveEnemies if an event was missed.
//
// CLIPS  (Suno-generated; load by Resources path; missing = graceful no-op)
//   Loaded via Resources.Load<AudioClip> (WebGL-safe — no File I/O). Expected at:
//      Resources/Music/Battle/Overworld_Battle_1
//      Resources/Music/Battle/Overworld_Battle_2
//      Resources/Music/Battle/Overworld_Victory
//      Resources/Music/Battle/Overworld_Boss_Fight
//   i.e. dropped under Assets/Audio/Resources/Music/Battle/ (parallel to the other
//   music in Assets/Audio/Resources/). A few common name variants are also tried.
//   ANY missing clip → that STATE no-ops cleanly (no error, just a one-time warn);
//   the rest of the machine keeps working. See the FLAG at the bottom of this file
//   for the exact files that currently need importing/moving.
//
// SELF-BOOTSTRAP + SAFETY
//   RuntimeInitializeOnLoadMethod spawns a DDOL host (HeroPoseController/TownHud
//   pattern), idempotent. Every event read + audio touch is try/catch-guarded (an
//   uncaught throw halts the WebGL player). Music volume target ~0.7.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using DeNelle.Audio;            // AudioService (Village references DeNelle.Audio)
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Wave-driven battle music state machine (Combat / Intense / Victory / Boss)
    /// with a 1.5 s crossfade. Owns a dedicated pair of AudioSources routed through
    /// the shared <see cref="AudioService"/> Music mixer group, listens to the live
    /// <see cref="WaveManager"/>'s public events, and hands the music back to the
    /// ambient/idle service when battle ends. Self-bootstrapping, WebGL-safe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleMusicManager : MonoBehaviour
    {
        // ── The four battle music states ──────────────────────────────────────
        private enum BattleMusicState
        {
            /// <summary>No battle music — the ambient/idle service owns playback.</summary>
            None = 0,
            /// <summary>General battle loop (Overworld_Battle_1).</summary>
            Combat,
            /// <summary>High-pressure loop, 5+ live enemies (Overworld_Battle_2).</summary>
            Intense,
            /// <summary>Post-wave one-shot sting (Overworld_Victory) — then back to ambient.</summary>
            Victory,
            /// <summary>Boss / apex-wave loop (Overworld_Boss_Fight).</summary>
            Boss,
        }

        // ── Tunables ──────────────────────────────────────────────────────────
        private const float CrossfadeSeconds = 1.5f;     // WO-372 crossfade duration
        private const float MusicVolume      = 0.7f;      // WO-372 music level (pre-mixer)
        private const int   IntenseEnemyThreshold = 5;    // ≥5 live enemies → Intense
        private const float PollInterval     = 0.5f;      // re-bind / re-evaluate cadence

        // Resources paths (no extension, no Resources/ prefix — Resources.Load form).
        // Primary normalized names + fall-back variants for the as-imported files.
        private static readonly string[] CombatClipPaths =
        {
            "Music/Battle/Overworld_Battle_1",
            "Music/Battle/Overworld battle 1",
            "Music/Battle/Overworld_battle_1",
        };
        private static readonly string[] IntenseClipPaths =
        {
            "Music/Battle/Overworld_Battle_2",
            "Music/Battle/Overworld battle 2",
            "Music/Battle/Overworld_battle_2",
        };
        private static readonly string[] VictoryClipPaths =
        {
            "Music/Battle/Overworld_Victory",
            "Music/Battle/Overworld Victory",
        };
        private static readonly string[] BossClipPaths =
        {
            "Music/Battle/Overworld_Boss_Fight",
            "Music/Battle/Overworld Boss Fight",
        };

        // ── Singleton DDOL host (HeroPoseController pattern) ──────────────────
        private static BattleMusicManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("BattleMusicManager");
            DontDestroyOnLoad(go);
            go.AddComponent<BattleMusicManager>();
        }

        // ── Clips (resolved once from Resources) ──────────────────────────────
        private AudioClip _combatClip;
        private AudioClip _intenseClip;
        private AudioClip _victoryClip;
        private AudioClip _bossClip;
        private bool _clipsResolved;

        // ── Dedicated crossfading source pair (routed to the Music mixer group) ─
        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private AudioSource _activeSource;     // the one currently faded-in
        private Coroutine _fade;
        private int _fadeToken;                // supersede an in-flight fade

        // ── State ─────────────────────────────────────────────────────────────
        private BattleMusicState _state = BattleMusicState.None;
        private float _pollTimer;
        private Coroutine _victoryReturn;

        // WaveManager subscription (FOUND, not modified — listen only).
        private WaveManager _wave;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // Our own dedicated host — destroy only the duplicate component,
                // never the GameObject (singleton-dedup memory).
                Destroy(this);
                return;
            }
            _instance = this;
            BuildAudioSources();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeWave();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeWave();
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            ResolveClips();
            TryResolveAndSubscribeWave();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // A new scene's WaveManager replaces the old one — re-bind next poll.
            UnsubscribeWave();
            _wave = null;

            // Leaving a battle context: stop our battle music and let the scene's
            // ambient/idle music (driven by AudioService) take over.
            if (_state != BattleMusicState.None)
                TransitionTo(BattleMusicState.None);
        }

        private void Update()
        {
            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = PollInterval;

            try
            {
                TryResolveAndSubscribeWave();   // lazy-bind a WaveManager that appeared later
                ReevaluateFromState();          // pressure (Intense) + missed-event safety net
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleMusicManager] poll failed: " + e.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  WaveManager subscription (listen-only — no WaveManager edits)
        // ─────────────────────────────────────────────────────────────────────
        private void TryResolveAndSubscribeWave()
        {
            if (_wave != null) return;
            var wave = FindFirstObjectByType<WaveManager>();
            if (wave == null) return;

            _wave = wave;
            if (_wave.OnWaveStarted != null)     _wave.OnWaveStarted.AddListener(OnWaveStarted);
            if (_wave.OnWaveCleared != null)     _wave.OnWaveCleared.AddListener(OnWaveCleared);
            if (_wave.OnApexBossSpawned != null) _wave.OnApexBossSpawned.AddListener(OnBossSpawned);
            if (_wave.OnDefeat != null)          _wave.OnDefeat.AddListener(OnDefeat);
        }

        private void UnsubscribeWave()
        {
            if (_wave == null) return;
            if (_wave.OnWaveStarted != null)     _wave.OnWaveStarted.RemoveListener(OnWaveStarted);
            if (_wave.OnWaveCleared != null)     _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
            if (_wave.OnApexBossSpawned != null) _wave.OnApexBossSpawned.RemoveListener(OnBossSpawned);
            if (_wave.OnDefeat != null)          _wave.OnDefeat.RemoveListener(OnDefeat);
            _wave = null;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        // A wave began. Boss waves are scored by OnApexBossSpawned (fired alongside),
        // so only enter Combat here when this is NOT a boss wave — and never demote
        // a live Boss state down to Combat.
        private void OnWaveStarted(int waveId)
        {
            if (IsBossWaveLive()) { TransitionTo(BattleMusicState.Boss); return; }
            if (_state == BattleMusicState.Boss) return;
            TransitionTo(BattleMusicState.Combat);
        }

        private void OnBossSpawned(DragonBoss boss)
        {
            TransitionTo(BattleMusicState.Boss);
        }

        // A wave was cleared. If a boss is still aloft the fight isn't over — stay
        // in Boss. Otherwise play the Victory one-shot, then return to ambient.
        private void OnWaveCleared(int waveId)
        {
            if (IsBossWaveLive()) return;
            TransitionTo(BattleMusicState.Victory);
        }

        // Heart fell — battle's over. Stop our battle music; the defeat screen owns
        // its own sting (AudioService Defeat track). Hand back to ambient/idle.
        private void OnDefeat()
        {
            TransitionTo(BattleMusicState.None);
        }

        /// <summary>
        /// Poll-time re-evaluation: the missed-event safety net + the live-enemy
        /// PRESSURE swap (Combat ↔ Intense). Boss/Victory/None are event-owned and
        /// left alone here (we never auto-leave them on a poll).
        /// </summary>
        private void ReevaluateFromState()
        {
            if (_wave == null) return;

            // Boss is owned by events (OnApexBossSpawned / boss death handled via
            // the next wave's OnWaveStarted) — don't auto-change it on a poll.
            if (_state == BattleMusicState.Boss) return;
            // Victory is a one-shot that returns to ambient on its own coroutine.
            if (_state == BattleMusicState.Victory) return;

            // Safety net: if a wave is actually live (Active/Countdown) but we somehow
            // never entered a combat state, enter Combat.
            bool waveLive = _wave.Phase == WavePhase.Active || _wave.Phase == WavePhase.Countdown;
            if (waveLive && _state == BattleMusicState.None)
            {
                TransitionTo(IsBossWaveLive() ? BattleMusicState.Boss : BattleMusicState.Combat);
                return;
            }

            // Pressure swap only while we're already in a non-boss combat state.
            if (_state != BattleMusicState.Combat && _state != BattleMusicState.Intense)
                return;

            int live = _wave.LiveEnemies != null ? _wave.LiveEnemies.Count : 0;
            if (live >= IntenseEnemyThreshold && _state != BattleMusicState.Intense)
                TransitionTo(BattleMusicState.Intense);
            else if (live < IntenseEnemyThreshold && _state != BattleMusicState.Combat)
                TransitionTo(BattleMusicState.Combat);
        }

        private bool IsBossWaveLive()
        {
            return _wave != null && _wave.LiveApexBoss != null && !_wave.LiveApexBoss.IsDead;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  State machine → crossfade
        // ─────────────────────────────────────────────────────────────────────
        private void TransitionTo(BattleMusicState next)
        {
            if (next == _state) return;

            // Cancel a pending Victory→ambient handoff if the state changes first.
            if (_victoryReturn != null)
            {
                StopCoroutine(_victoryReturn);
                _victoryReturn = null;
            }

            _state = next;

            try
            {
                if (next == BattleMusicState.None)
                {
                    // End of battle music — fade ours out and let the ambient/idle
                    // service resume (reuses AudioService's ambient handoff).
                    FadeOutToAmbient();
                    return;
                }

                AudioClip clip = ClipForState(next);
                if (clip == null)
                {
                    // GRACEFUL no-op: this state has no imported clip. Warn once,
                    // keep the machine alive. (Don't touch playback — leave whatever
                    // is currently playing rather than cutting to silence.)
                    WarnMissingOnce(next);
                    return;
                }

                bool loop = next != BattleMusicState.Victory;   // Victory is a one-shot
                Crossfade(clip, loop);

                // Victory is a one-shot: when it ends, hand back to ambient/idle.
                if (next == BattleMusicState.Victory)
                {
                    float dur = clip.length > 0.01f ? clip.length : 3f;
                    _victoryReturn = StartCoroutine(VictoryReturnRoutine(dur));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleMusicManager] TransitionTo(" + next + ") failed: " + e.Message);
            }
        }

        private AudioClip ClipForState(BattleMusicState state)
        {
            ResolveClips();
            switch (state)
            {
                case BattleMusicState.Combat:  return _combatClip;
                case BattleMusicState.Intense: return _intenseClip;
                case BattleMusicState.Victory: return _victoryClip;
                case BattleMusicState.Boss:    return _bossClip;
                default:                       return null;
            }
        }

        // When the Victory sting finishes, drop back to None — which fades our
        // sources out and resumes the ambient/idle music via AudioService.
        private IEnumerator VictoryReturnRoutine(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _victoryReturn = null;
            if (_state == BattleMusicState.Victory)
                TransitionTo(BattleMusicState.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Audio sources + crossfade (own pair, Music-mixer-group routed)
        // ─────────────────────────────────────────────────────────────────────
        private void BuildAudioSources()
        {
            if (_sourceA != null) return;

            AudioMixerGroup musicGroup = ResolveMusicGroup();
            _sourceA = CreateSource("BattleMusic_A", musicGroup);
            _sourceB = CreateSource("BattleMusic_B", musicGroup);
            _activeSource = _sourceA;
        }

        private AudioSource CreateSource(string srcName, AudioMixerGroup group)
        {
            var go = new GameObject(srcName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;    // 2D — music is non-positional.
            src.volume = 0f;
            if (group != null) src.outputAudioMixerGroup = group;
            return src;
        }

        // Route through the SHARED Music mixer group so the player's music volume +
        // mute apply (reuse, not reinvent). The mixer ships at the same Resources
        // path AudioBootstrap uses; if it's somehow absent we run on the default
        // output (still audible).
        private static AudioMixerGroup ResolveMusicGroup()
        {
            try
            {
                var mixer = Resources.Load<AudioMixer>(AudioBootstrap.MixerResourcePath);
                if (mixer == null) return null;
                var groups = mixer.FindMatchingGroups("Music");
                return (groups != null && groups.Length > 0) ? groups[0] : null;
            }
            catch { return null; }
        }

        private void Crossfade(AudioClip clip, bool loop)
        {
            if (clip == null || _sourceA == null || _sourceB == null) return;

            AudioSource fadeIn  = (_activeSource == _sourceA) ? _sourceB : _sourceA;
            AudioSource fadeOut = _activeSource;
            _activeSource = fadeIn;

            fadeIn.clip = clip;
            fadeIn.loop = loop;
            fadeIn.volume = 0f;

            // Guard a clip object whose underlying file failed to load (FMOD
            // "file not found" otherwise crashes on Play).
            if (clip.loadState == AudioDataLoadState.Failed)
            {
                Debug.LogWarning($"[BattleMusicManager] Skipping '{clip.name}' — clip failed to load (file missing from build?).");
                return;
            }

            fadeIn.Play();

            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(CrossfadeRoutine(fadeIn, fadeOut, MusicVolume));
        }

        private IEnumerator CrossfadeRoutine(AudioSource fadeIn, AudioSource fadeOut, float target)
        {
            int token = ++_fadeToken;
            float startOut = fadeOut != null ? fadeOut.volume : 0f;
            float seconds = Mathf.Max(0.01f, CrossfadeSeconds);
            float t = 0f;

            while (t < seconds)
            {
                if (token != _fadeToken) yield break;   // superseded
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                if (fadeIn  != null) fadeIn.volume  = Mathf.Lerp(0f, target, k);
                if (fadeOut != null) fadeOut.volume = Mathf.Lerp(startOut, 0f, k);
                yield return null;
            }

            if (token != _fadeToken) yield break;
            if (fadeIn  != null) fadeIn.volume = target;
            if (fadeOut != null) { fadeOut.volume = 0f; fadeOut.Stop(); }
            _fade = null;
        }

        // Fade both of OUR sources out, then ask the shared AudioService to resume
        // the ambient/idle track for whatever context the player is in (reuse —
        // we do not own the ambient music).
        private void FadeOutToAmbient()
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeOutRoutine());

            try { AudioService.Instance?.ReturnToAmbient(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleMusicManager] ReturnToAmbient failed: " + e.Message);
            }
        }

        private IEnumerator FadeOutRoutine()
        {
            int token = ++_fadeToken;
            float seconds = Mathf.Max(0.01f, CrossfadeSeconds);
            float startA = _sourceA != null ? _sourceA.volume : 0f;
            float startB = _sourceB != null ? _sourceB.volume : 0f;
            float t = 0f;

            while (t < seconds)
            {
                if (token != _fadeToken) yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                if (_sourceA != null) _sourceA.volume = Mathf.Lerp(startA, 0f, k);
                if (_sourceB != null) _sourceB.volume = Mathf.Lerp(startB, 0f, k);
                yield return null;
            }

            if (token != _fadeToken) yield break;
            if (_sourceA != null) { _sourceA.volume = 0f; _sourceA.Stop(); }
            if (_sourceB != null) { _sourceB.volume = 0f; _sourceB.Stop(); }
            _fade = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Clip loading (Resources only — WebGL-safe, no File I/O)
        // ─────────────────────────────────────────────────────────────────────
        private void ResolveClips()
        {
            if (_clipsResolved) return;
            _clipsResolved = true;

            _combatClip  = LoadFirst(CombatClipPaths);
            _intenseClip = LoadFirst(IntenseClipPaths);
            _victoryClip = LoadFirst(VictoryClipPaths);
            _bossClip    = LoadFirst(BossClipPaths);

            // One consolidated FLAG so the integrator knows exactly what to import.
            var missing = new List<string>();
            if (_combatClip  == null) missing.Add("Combat → Resources/" + CombatClipPaths[0]);
            if (_intenseClip == null) missing.Add("Intense → Resources/" + IntenseClipPaths[0]);
            if (_victoryClip == null) missing.Add("Victory → Resources/" + VictoryClipPaths[0]);
            if (_bossClip    == null) missing.Add("Boss → Resources/" + BossClipPaths[0]);

            if (missing.Count > 0)
                Debug.LogWarning(
                    "[BattleMusicManager] " + missing.Count + " battle music clip(s) NOT found — " +
                    "those states play silent (no error). Drop the Suno tracks under " +
                    "Assets/Audio/Resources/Music/Battle/ with these names: " +
                    string.Join(" | ", missing));
        }

        private static AudioClip LoadFirst(string[] paths)
        {
            if (paths == null) return null;
            foreach (string p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                try
                {
                    AudioClip clip = Resources.Load<AudioClip>(p);
                    if (clip != null) return clip;
                }
                catch { /* WebGL-safe: never throw out of clip resolution */ }
            }
            return null;
        }

        // Warn at most once per state about a missing clip so a silent state never
        // spams the log every transition.
        private readonly HashSet<BattleMusicState> _warned = new HashSet<BattleMusicState>();
        private void WarnMissingOnce(BattleMusicState state)
        {
            if (_warned.Contains(state)) return;
            _warned.Add(state);
            Debug.LogWarning(
                "[BattleMusicManager] No clip for the " + state + " state — it plays silent. " +
                "Import the matching track under Assets/Audio/Resources/Music/Battle/.");
        }
    }
}

// =============================================================================
// FLAG — clip files that need importing/relocating for WO-372
// -----------------------------------------------------------------------------
// The four Suno tracks ARE in the project but live at:
//     Assets/Audio/Music/Battle/Overworld battle 1.mp3
//     Assets/Audio/Music/Battle/Overworld battle 2.mp3
//     Assets/Audio/Music/Battle/Overworld Victory.mp3
//     Assets/Audio/Music/Battle/Overworld Boss Fight.mp3
// That folder is NOT under a Resources/ folder, so Resources.Load CANNOT see them.
//
// CLI (asset owner): create Assets/Audio/Resources/Music/Battle/ and place the
// four clips there with normalized names so the PRIMARY Resources paths resolve:
//     Assets/Audio/Resources/Music/Battle/Overworld_Battle_1.mp3
//     Assets/Audio/Resources/Music/Battle/Overworld_Battle_2.mp3
//     Assets/Audio/Resources/Music/Battle/Overworld_Victory.mp3
//     Assets/Audio/Resources/Music/Battle/Overworld_Boss_Fight.mp3
// (The manager also tries the original spaced names as a fallback IF that whole
//  Battle folder is moved under Resources/, but the underscored names are canon.)
// Until then each state no-ops gracefully — no errors, just one warning listing
// exactly the above.
// =============================================================================
