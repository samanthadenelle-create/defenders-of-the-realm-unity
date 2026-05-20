// =============================================================================
// AudioBootstrap — guarantees an AudioService exists, with no scene wiring.
// -----------------------------------------------------------------------------
// missing-components.md §2.2 / Core row: "SceneRouter's own header says an
// 'Audio/Core director listens for scene loads' — that director does not
// exist." AudioBootstrap is what spawns it.
//
// It runs automatically via [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] —
// the same auto-run pattern as Core/SeekerBootstrap.cs — so an AudioService is
// alive before any scene controller calls PlayMusic, in EVERY scene, with no
// per-scene GameObject to forget to place.
//
// TWO bootstrap paths, preferred first:
//   1. PREFAB path — Resources.Load<GameObject>("DeNelleAudioService"). If the
//      integrator has authored a prefab at Assets/Audio/Resources/ with the
//      AudioService component + the AudioMixer + the six music clips wired in
//      the inspector, that prefab is instantiated. This is the full-fidelity
//      path (mixer routing + clips ready).
//   2. CODE path (fallback) — no prefab found: a bare GameObject with an
//      AudioService is created in code, and the mixer is pulled from
//      Resources.Load<AudioMixer>("Audio/GameAudioMixer") — the mixer ships at
//      Assets/Audio/Resources/Audio/GameAudioMixer.mixer, so the Resources
//      lookup resolves out of the box. With no mixer the service still runs
//      (no-mixer fallback, no music clips) so audio never blocks the game.
//
// Either way the AudioService is DontDestroyOnLoad and survives scene loads.
// =============================================================================

using UnityEngine;
using UnityEngine.Audio;

namespace DeNelle.Audio
{
    /// <summary>
    /// Auto-spawns the singleton <see cref="AudioService"/> at startup so the
    /// per-scene BGM director is always present. No scene wiring required.
    /// </summary>
    public static class AudioBootstrap
    {
        /// <summary>
        /// Resources path of the integrator-authored AudioService prefab
        /// (Assets/Audio/Resources/DeNelleAudioService.prefab). When present it
        /// carries the mixer + the six music clips wired in the inspector.
        /// </summary>
        public const string PrefabResourcePath = "DeNelleAudioService";

        /// <summary>
        /// Resources path of the canonical AudioMixer. The mixer ships at
        /// <c>Assets/Audio/Resources/Audio/GameAudioMixer.mixer</c>, so
        /// <c>Resources.Load&lt;AudioMixer&gt;("Audio/GameAudioMixer")</c>
        /// resolves it. This is the SAME path the parallel Settings module's
        /// <c>AudioMixerBridge</c> uses — one shared mixer, one shared contract.
        /// </summary>
        public const string MixerResourcePath = "Audio/GameAudioMixer";

        /// <summary>
        /// Auto-invoked by Unity after the first scene loads. Idempotent — if an
        /// <see cref="AudioService"/> already exists (e.g. one was placed in a
        /// scene by hand) this is a no-op.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureAudioService()
        {
            if (AudioService.Instance != null)
                return;

            // ── Path 1: the integrator-authored prefab (preferred) ───────────
            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab != null)
            {
                var go = Object.Instantiate(prefab);
                go.name = "DeNelleAudioService";
                // The prefab's own AudioService.Awake claims the singleton +
                // DontDestroyOnLoad; nothing else to do.
                return;
            }

            // ── Path 2: code-built fallback ──────────────────────────────────
            var host = new GameObject("DeNelleAudioService");
            var service = host.AddComponent<AudioService>();

            // Pull the mixer from Resources so SetVolume drives the exposed
            // parameters. The mixer ships under a Resources folder, so this
            // resolves out of the box; if it is somehow absent the service
            // logs and runs in the no-mixer fallback (music still plays;
            // SetVolume scales the sources directly).
            var mixer = Resources.Load<AudioMixer>(MixerResourcePath);
            if (mixer != null)
                service.SetMixer(mixer);
            else
                Debug.LogWarning(
                    "[AudioBootstrap] No AudioService prefab and no AudioMixer at " +
                    $"Resources/'{MixerResourcePath}' — running the code-built fallback " +
                    "without a mixer. For full mix control (and the six music clips " +
                    "pre-wired) author Assets/Audio/Resources/DeNelleAudioService.prefab. " +
                    "See docs/port-notes/audio-system.md.");

            // Music clips — Resources.Load each by short name (the MP3s ship
            // directly under Assets/Audio/Resources/). Missing ones stay null;
            // AudioService's "no clip" guard handles them silently.
            TryAssignClip(service, MusicTrack.Title,   "title");
            TryAssignClip(service, MusicTrack.Village, "village");
            TryAssignClip(service, MusicTrack.Battle,  "battle");
            TryAssignClip(service, MusicTrack.Victory, "victory");
            TryAssignClip(service, MusicTrack.Defeat,  "defeat");
            TryAssignClip(service, MusicTrack.Dungeon, "dungeon");

            // One-time mute-migration. Pre-2026-05-20 the schema default for
            // GameState.Muted was true, so saves persisted as muted even when
            // the player never opened Settings. Now that music actually exists,
            // flip Muted=false ONCE for each install, then never again.
            UnmuteOnce();
        }

        private const string UnmuteMigrationKey = "dotr-unmute-migration-v1";

        private static void UnmuteOnce()
        {
            if (PlayerPrefs.GetInt(UnmuteMigrationKey, 0) == 1) return;
            try
            {
                var t = System.Type.GetType("DeNelle.Core.State.GameStateService, DeNelle.Core");
                if (t == null) return;
                var instance = t.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (instance == null) return;
                var setMuted = t.GetMethod("SetMuted",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                setMuted?.Invoke(instance, new object[] { false });
            }
            catch { /* best-effort; not fatal */ }
            finally
            {
                PlayerPrefs.SetInt(UnmuteMigrationKey, 1);
                PlayerPrefs.Save();
            }
        }

        private static void TryAssignClip(AudioService service, MusicTrack track, string resourceName)
        {
            var clip = Resources.Load<AudioClip>(resourceName);
            if (clip != null) service.SetMusicClip(track, clip);
        }
    }
}
