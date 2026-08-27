// =============================================================================
// AlertIntelSystem — Watchtower raid early-warning (DEF-199 / WO-241 / WO-1184).
// -----------------------------------------------------------------------------
// A lightweight, self-contained early-warning layer over the existing wave loop.
// Shortly BEFORE a wave/raid spawns it surfaces a friendly LOOKOUT NOTICE chip
// counting down, so the player has a beat to place towers / reposition. It
// clears the moment the wave starts.
//
// HOOK (no fork of the wave loop): this polls WaveManager's PUBLIC surface only —
//   WaveManager.Phase  (WavePhase.Countdown)
//   WaveManager.CountdownRemaining (seconds left)
//   WaveManager.CurrentWaveId
// When the prepare-phase countdown falls inside the alert window (≤ AlertLeadSeconds)
// it shows the chip; when the phase leaves Countdown (wave Active) it clears. No
// subscription to WaveManager internals, no schedule reach-in — so the wave loop is
// completely untouched and this no-ops silently if no WaveManager is present.
//
// DIRECTION: named from the live WaveSpawnPoints in the scene — if every spawn
// shares one cardinal direction we name it ("the north gate"); mixed/absent => a
// generic "the gates". Spawn-point routing per-wave lives in private schedule
// state, so this stays at the safe "which gates exist" granularity rather than
// forking the loop to learn the exact batch routing.
//
// UI: code-built uGUI chip via LookoutNoticeChip / ElarionUiKit (NO UIDocument,
// NO UXML — UXML does not render in player builds, CLAUDE.md §8 / WO-1182).
// Friendly tell, not a panic red bang. Words, not colour-only. ASCII-only.
// Never claims combat is happening offline. Never pairs a notice with a shield.
// ⛔ Presentation reads only. Does not write SiegeScheduler cadence (WO-1179
// is orthogonal and untouched).
//
// Assembly: DeNelle.Village (same as WaveManager) — direct refs, no reflection.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Surfaces a brief friendly lookout notice a few seconds before each
    /// wave spawns, by polling <see cref="WaveManager"/>'s public countdown state.
    /// Self-bootstrapping (no scene edit) and a silent no-op without a WaveManager.
    /// DEF-199 / WO-241 / WO-1184.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlertIntelSystem : MonoBehaviour
    {
        // ── Self-bootstrap (DEF-199) ──────────────────────────────────────────
        // Mirrors WaveSystemBridgeBootstrap: attach to the WaveManager GO at
        // runtime so the early-warning runs with no Village.unity re-save.

        private static bool s_hooked;

        // Domain-reload-off safety: statics persist across Play sessions, so reset
        // the guard each play start (runs before the AfterSceneLoad attach).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();   // the first scene is already loaded when this runs
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Attach();

        private static void Attach()
        {
            var managers = Object.FindObjectsByType<WaveManager>();
            if (managers == null || managers.Length == 0) return;   // not a wave scene

            var go = managers[0].gameObject;
            if (go.GetComponent<AlertIntelSystem>() == null)
                go.AddComponent<AlertIntelSystem>();
        }

        // ── Tuning ────────────────────────────────────────────────────────────

        [Tooltip("How many seconds before a wave spawns the lookout notice first appears.")]
        [SerializeField, Min(1f)] private float _alertLeadSeconds = 5f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private WaveManager _wave;
        private LookoutNoticeChip _chip;
        private bool _showing;
        // The wave id the current chip is warning about — so a notice shown for
        // wave N isn't reused/duplicated if the loop loops back through Countdown.
        private int _bannerWaveId = -1;

        private void Awake()
        {
            // Same GO carries the WaveManager (we were attached to it); fall back to
            // a scene search so a hand-placed AlertIntelSystem still finds the loop.
            _wave = GetComponent<WaveManager>();
            if (_wave == null) _wave = FindAnyObjectByType<WaveManager>();
        }

        private void OnDisable()
        {
            Clear();
            DisposeChip();
        }

        private void Update()
        {
            // No WaveManager => silent no-op (it may not exist outside a wave scene).
            if (_wave == null) { if (_showing) Clear(); return; }

            // Only the prepare-phase countdown is an "incoming horde" window.
            if (_wave.Phase != WavePhase.Countdown)
            {
                if (_showing) Clear();   // wave went Active / loop left countdown -> clear
                return;
            }

            float remaining = _wave.CountdownRemaining;

            // Outside the alert window (countdown still long, or already at zero):
            // keep the chip hidden until we enter the final lead-in seconds.
            if (remaining > _alertLeadSeconds || remaining <= 0f)
            {
                if (_showing) Clear();
                return;
            }

            // Earned intel: no lookout, no on-screen notice (matches the phone half).
            if (RoamingHordeNotifications.BestLookoutLevel() <= 0)
            {
                if (_showing) Clear();
                return;
            }

            ShowOrUpdate(_wave.CurrentWaveId, remaining);
        }

        // ── Chip content ──────────────────────────────────────────────────────

        private void ShowOrUpdate(int waveId, float remaining)
        {
            EnsureChip();
            if (_chip == null) return;

            if (!_showing || _bannerWaveId != waveId)
            {
                _bannerWaveId = waveId;
                _showing = true;
            }

            int secs = Mathf.Clamp(Mathf.CeilToInt(remaining), 1, 999);
            string where = DescribeApproach();
            int nextWave = Mathf.Max(1, waveId + 1);
            string size = RoamingHordeNotifications.BestLookoutLevel() >= 3
                ? RoamingHordeNotifications.DescribeForceSize(nextWave)
                : string.Empty;
            _chip.Show(LookoutNoticeChip.FormatLiveCopy(where, secs, size));
        }

        private void Clear()
        {
            if (_chip != null) _chip.Hide();
            _showing = false;
            _bannerWaveId = -1;
        }

        /// <summary>
        /// Names the threatened approach from the live spawn markers. When every
        /// WaveSpawnPoint shares one cardinal direction we name "the &lt;dir&gt; gate";
        /// when they're mixed or none exist we fall back to a generic phrase so the
        /// chip is always meaningful. (Per-wave routing is private schedule state,
        /// so we stay at gate-existence granularity rather than fork the wave loop.)
        /// </summary>
        private static string DescribeApproach()
        {
            var points = Object.FindObjectsByType<WaveSpawnPoint>();
            if (points == null || points.Length == 0)
                return "the gates";

            string dir = null;
            foreach (WaveSpawnPoint p in points)
            {
                if (p == null) continue;
                string d = p.Direction;
                if (string.IsNullOrEmpty(d)) continue;
                if (dir == null) dir = d;
                else if (!string.Equals(dir, d, System.StringComparison.OrdinalIgnoreCase))
                    return "all gates";   // multiple cardinal approaches in play
            }

            return string.IsNullOrEmpty(dir) ? "the gates" : "the " + dir + " gate";
        }

        private void EnsureChip()
        {
            if (_chip != null) return;
            _chip = LookoutNoticeChip.Create();
        }

        private void DisposeChip()
        {
            if (_chip == null) return;
            _chip.Dispose();
            _chip = null;
        }
    }
}
