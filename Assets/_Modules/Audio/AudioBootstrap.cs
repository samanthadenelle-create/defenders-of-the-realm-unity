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
using DeNelle.Core.Diagnostics;

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
            // Town/hub theme (owner Suno track 2026-06-29): "Whispering Pines" is the PRIMARY town
            // theme; the prior "village" clip is kept as a pool variant (non-destructive).
            TryAssignClip(service, MusicTrack.Village, "whispering_pines");
            TryAddClip(service,    MusicTrack.Village, "village");
            TryAssignClip(service, MusicTrack.Victory, "victory");
            // Dungeon theme (owner slot mapping 2026-08-04): "Echoes Beneath Elarion" is the
            // PRIMARY dungeon track, displacing the prior "whispering_depths" pick. Dungeon is
            // NOT a pooled track - AudioService.AddMusicClip pools only Battle and Overworld and
            // routes everything else to SetMusicClip, which REPLACES rather than appends - so
            // whispering_depths cannot be kept here as a rotation variant. It stays on disk,
            // unreferenced, pending a Dungeon pool or a re-slot.
            TryAssignClip(service, MusicTrack.Dungeon, "Music/echoes_beneath_elarion");

            // Game-over music (owner slot mapping 2026-08-04): "Heartwood Collapse" is the
            // PRIMARY defeat track, displacing Resources/Audio/Music/GameOver.mp3. Load the
            // legacy 'defeat' sting first as a fallback, then the owner track - the second
            // assign wins when present (SetMusicClip overwrites). Defeat is NOT a pooled track
            // (AddMusicClip pools only Battle/Overworld), so GameOver.mp3 cannot be kept as a
            // variant; it stays on disk, unreferenced.
            // Playback shape is unchanged: the Defeat slot is non-looping and plays on the
            // time-frozen game-over modal, which the player dismisses - the displaced
            // GameOver.mp3 was 5:19, so this 2:53 track is SHORTER than what it replaces.
            TryAssignClip(service, MusicTrack.Defeat,  "defeat");
            TryAssignClip(service, MusicTrack.Defeat,  "Music/heartwood_collapse");

            // Pooled tracks (WO-171) — the owner's NEW themes (2026-06-02), cycled by
            // NextFromPool. Battle: 3 themes. Overworld: 2 themes ("a loop of either is fine"
            // — owner). Assign the default, append the rest to the rotation pool.
            // Battle/invasion theme (owner Suno track 2026-06-29): "Siege of the Iron Bastion" is the
            // PRIMARY battle theme; the prior battle themes stay as pool variants (cycled by NextFromPool).
            TryAssignClip(service, MusicTrack.Battle,    "siege_iron_bastion");
            TryAddClip(service,    MusicTrack.Battle,    "battle_theme_NEW");
            TryAddClip(service,    MusicTrack.Battle,    "battle_theme2_NEW");
            TryAddClip(service,    MusicTrack.Battle,    "battle_theme3_NEW");
            TryAssignClip(service, MusicTrack.Overworld, "mainworld1_NEW");
            TryAddClip(service,    MusicTrack.Overworld, "world_theme_NEW");

            // Arena raid BGM — "Echo's theme" (owner-supplied). Ships at
            // Assets/Audio/Resources/Music/echo_theme.mp3, so the Resources short
            // name is "Music/echo_theme". Soft + looping (MusicTrackRegistry.Arena).
            TryAssignClip(service, MusicTrack.Arena, "Music/echo_theme");
            // WO-453 offensive-raid BGM — Assets/Audio/Resources/Music/Raid/brass-rampart.mp3.
            TryAssignClip(service, MusicTrack.Raid,  "Music/Raid/brass-rampart");

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
            catch (System.Exception ex)
            {
                // TGVRU U: no silent catch. The unmute migration is best-effort (not fatal), but a
                // throw here means the install stayed muted — surface it instead of swallowing.
                FlowTrace.Warn("Audio", $"UnmuteOnce: SetMuted(false) migration threw {ex.GetType().Name}: {ex.Message} " +
                    "(best-effort; install may stay muted).");
            }
            finally
            {
                PlayerPrefs.SetInt(UnmuteMigrationKey, 1);
                PlayerPrefs.Save();
            }
        }

        private static void TryAssignClip(AudioService service, MusicTrack track, string resourceName)
        {
            var clip = Resources.Load<AudioClip>(resourceName);
            // TGVRU V: a clip-resolve MISS was COMPLETELY SILENT — all ~14 music clips could fail to
            // wire with zero signal (the "silent clip" class). Warn on a null so a run self-reports
            // WHICH track has no audio, instead of just playing silence.
            if (clip == null)
            {
                FlowTrace.Warn("Audio", $"TryAssignClip: Resources.Load<AudioClip>('{resourceName}') returned null " +
                    $"for track '{track}' — that track will be SILENT (clip missing/misnamed under Resources).");
                return;
            }
            service.SetMusicClip(track, clip);
        }

        /// <summary>
        /// Appends a clip to a pooled track's rotation (WO-171), if the Resource
        /// exists. Missing extras are silently skipped — the pool just rotates
        /// over whatever landed.
        /// </summary>
        private static void TryAddClip(AudioService service, MusicTrack track, string resourceName)
        {
            var clip = Resources.Load<AudioClip>(resourceName);
            // TGVRU V: an extra pooled clip going missing was silent. These are optional rotation
            // extras (the pool still works on whatever landed), so this is a Warn, not a Fail —
            // but it must self-report so a thin/missing rotation is visible, not invisible.
            if (clip == null)
            {
                FlowTrace.Warn("Audio", $"TryAddClip: Resources.Load<AudioClip>('{resourceName}') returned null " +
                    $"for pooled track '{track}' — extra skipped (rotation runs on remaining clips).");
                return;
            }
            service.AddMusicClip(track, clip);
        }
    }
}
