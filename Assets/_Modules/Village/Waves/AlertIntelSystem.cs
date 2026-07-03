// =============================================================================
// AlertIntelSystem — Watchtower raid early-warning (DEF-199 / WO-241).
// -----------------------------------------------------------------------------
// A lightweight, self-contained early-warning layer over the existing wave loop.
// Shortly BEFORE a wave/raid spawns it surfaces a clear, themed on-screen banner
// ("⚠ Raid incoming — <gate> in 5…") counting down, so the player has a beat to
// place towers / reposition. It clears the moment the wave starts.
//
// HOOK (no fork of the wave loop): this polls WaveManager's PUBLIC surface only —
//   WaveManager.Phase  (WavePhase.Countdown)        — WaveManager.cs:206
//   WaveManager.CountdownRemaining (seconds left)    — WaveManager.cs:212
//   WaveManager.CurrentWaveId                        — WaveManager.cs:209
// When the prepare-phase countdown falls inside the alert window (≤ AlertLeadSeconds)
// it shows the banner; when the phase leaves Countdown (wave Active) it clears. No
// subscription to WaveManager internals, no schedule reach-in — so the wave loop is
// completely untouched and this no-ops silently if no WaveManager is present.
//
// DIRECTION: named from the live WaveSpawnPoints in the scene — if every spawn
// shares one cardinal direction we name it ("the north gate"); mixed/absent ⇒ a
// generic "Raid incoming." Spawn-point routing per-wave lives in private schedule
// state, so this stays at the safe "which gates exist" granularity rather than
// forking the loop to learn the exact batch routing.
//
// UI: code-built UIToolkit banner (NO UXML — UXML does not render in player builds,
// per CLAUDE.md §8), themed plum/gold to match WaveCountdownUI. Self-bootstraps via
// RuntimeInitializeOnLoadMethod (mirrors WaveSystemBridgeBootstrap) — it attaches
// itself to the WaveManager GameObject at runtime, so NO scene edit is required.
//
// Assembly: DeNelle.Village (same as WaveManager) — direct refs, no reflection.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    /// <summary>
    /// Surfaces a brief themed "Raid incoming" banner a few seconds before each
    /// wave spawns, by polling <see cref="WaveManager"/>'s public countdown state.
    /// Self-bootstrapping (no scene edit) and a silent no-op without a WaveManager.
    /// DEF-199 / WO-241.
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

        [Tooltip("How many seconds before a wave spawns the warning banner first appears.")]
        [SerializeField, Min(1f)] private float _alertLeadSeconds = 5f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private WaveManager _wave;
        private Label _banner;
        private bool _showing;
        // The wave id the current banner is warning about — so a banner shown for
        // wave N isn't reused/duplicated if the loop loops back through Countdown.
        private int _bannerWaveId = -1;

        private void Awake()
        {
            // Same GO carries the WaveManager (we were attached to it); fall back to
            // a scene search so a hand-placed AlertIntelSystem still finds the loop.
            _wave = GetComponent<WaveManager>();
            if (_wave == null) _wave = FindAnyObjectByType<WaveManager>();
            BuildBanner();
        }

        private void OnDisable() => Clear();

        private void Update()
        {
            // No WaveManager ⇒ silent no-op (it may not exist outside a wave scene).
            if (_wave == null) { if (_showing) Clear(); return; }

            // Only the prepare-phase countdown is an "incoming raid" window.
            if (_wave.Phase != WavePhase.Countdown)
            {
                if (_showing) Clear();   // wave went Active / loop left countdown → clear
                return;
            }

            float remaining = _wave.CountdownRemaining;

            // Outside the alert window (countdown still long, or already at zero):
            // keep the banner hidden until we enter the final lead-in seconds.
            if (remaining > _alertLeadSeconds || remaining <= 0f)
            {
                if (_showing) Clear();
                return;
            }

            // Inside the window — show / refresh the banner with the live count.
            ShowOrUpdate(_wave.CurrentWaveId, remaining);
        }

        // ── Banner content ────────────────────────────────────────────────────

        private void ShowOrUpdate(int waveId, float remaining)
        {
            if (_banner == null) return;

            if (!_showing || _bannerWaveId != waveId)
            {
                _bannerWaveId = waveId;
                _banner.style.display = DisplayStyle.Flex;
                _showing = true;
            }

            int secs = Mathf.Clamp(Mathf.CeilToInt(remaining), 1, 999);
            string where = DescribeApproach();
            _banner.text = $"⚠ Raid incoming — {where} in {secs}…";
        }

        private void Clear()
        {
            if (_banner != null) _banner.style.display = DisplayStyle.None;
            _showing = false;
            _bannerWaveId = -1;
        }

        /// <summary>
        /// Names the threatened approach from the live spawn markers. When every
        /// WaveSpawnPoint shares one cardinal direction we name "the &lt;dir&gt; gate";
        /// when they're mixed or none exist we fall back to a generic phrase so the
        /// banner is always meaningful. (Per-wave routing is private schedule state,
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

            return string.IsNullOrEmpty(dir) ? "the gates" : $"the {dir} gate";
        }

        // ── Code-built UIToolkit banner (no UXML) ─────────────────────────────

        private void BuildBanner()
        {
            // Host the banner on the first available UIDocument (the same approach
            // WaveCountdownUI uses). No UIDocument yet ⇒ no banner, but Update still
            // no-ops safely; the banner simply isn't shown this scene.
            UIDocument doc = FindAnyObjectByType<UIDocument>();
            if (doc == null || doc.rootVisualElement == null) return;

            _banner = new Label { pickingMode = PickingMode.Ignore };

            // Themed plum/gold to match the village HUD (cf. WaveCountdownUI).
            _banner.style.position = Position.Absolute;
            _banner.style.top = 56;                     // below the wave-countdown label
            _banner.style.left = Length.Percent(50);
            _banner.style.translate = new StyleTranslate(new Translate(-50f, 0));
            _banner.style.color = new Color(0.97f, 0.86f, 0.45f);          // gold
            _banner.style.fontSize = 24;
            _banner.style.unityFontStyleAndWeight = FontStyle.Bold;
            _banner.style.unityTextAlign = TextAnchor.MiddleCenter;
            _banner.style.backgroundColor = new Color(0.18f, 0.06f, 0.20f, 0.86f); // plum
            _banner.style.paddingLeft = 16;
            _banner.style.paddingRight = 16;
            _banner.style.paddingTop = 6;
            _banner.style.paddingBottom = 6;
            _banner.style.borderTopLeftRadius = 10;
            _banner.style.borderTopRightRadius = 10;
            _banner.style.borderBottomLeftRadius = 10;
            _banner.style.borderBottomRightRadius = 10;

            // A thin gold edge so the plum banner reads as a deliberate alert chip.
            Color edge = new Color(0.85f, 0.70f, 0.32f, 0.9f);
            _banner.style.borderLeftColor = edge;
            _banner.style.borderRightColor = edge;
            _banner.style.borderTopColor = edge;
            _banner.style.borderBottomColor = edge;
            _banner.style.borderLeftWidth = 1.5f;
            _banner.style.borderRightWidth = 1.5f;
            _banner.style.borderTopWidth = 1.5f;
            _banner.style.borderBottomWidth = 1.5f;

            _banner.style.display = DisplayStyle.None;
            doc.rootVisualElement.Add(_banner);
        }
    }
}
