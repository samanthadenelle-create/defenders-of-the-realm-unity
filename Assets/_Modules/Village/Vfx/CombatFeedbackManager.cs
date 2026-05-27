// =============================================================================
// CombatFeedbackManager (DEF-44 / DEF-45) — hit stop, combo counter, kill streak.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Three interlocking combat-feel systems triggered by a single static API:
//
//   HIT STOP  (DEF-45)
//     On CombatFeedbackManager.Hit() a very brief Time.timeScale dip (default
//     0.05 s at 0.05× speed) punches the impact of every successful strike into
//     the player's hand. WaitForSecondsRealtime keeps the coroutine alive during
//     the pause. Successive hits within the dip restart it — no stacking.
//
//   COMBO COUNTER  (DEF-45)
//     Each Hit() increments ComboCount and resets a decay window (default 2.5 s).
//     If no hit lands before the window expires the count resets to zero.
//     OnComboChanged(int) fires on every change (increment AND reset to zero).
//     Callers: HUD can subscribe and show a "3× COMBO!" badge.
//
//   KILL STREAK  (DEF-45)
//     Each Kill() increments KillStreak and resets a longer decay window
//     (default 8 s). OnKillStreakChanged(int) fires on change. A "DOUBLE KILL!"
//     type UI subscribes here. Resets to zero on window expiry, not on wave end
//     (wave end is a natural pause — let the streak survive it).
//
// FLOATING DAMAGE NUMBERS (DEF-44)
//   DamageNumberSpawner already handles this in Enemy.TakeDamage() — this
//   manager does NOT duplicate that call.
//
// SCREEN SHAKE  (DEF-44)
//   CameraShakeBridge.Shake() is already called in Enemy.Die() for the death
//   burst. This manager adds a lighter per-hit shake for the survival path via
//   CombatFeedbackManager.Hit() so the camera responds to every landing blow.
//
// WIRING:
//   Enemy.cs calls:
//     CombatFeedbackManager.Hit(position, amount)   — survival hit branch
//     CombatFeedbackManager.Kill(position)           — killed == true branch
//
// ARCHITECTURE (non-negotiable):
//   * DontDestroyOnLoad singleton — bootstrapped BeforeSceneLoad so it's ready
//     for the first enemy spawn.
//   * Update() uses Time.unscaledDeltaTime so timers survive the hit-stop dip.
//   * No per-frame Find / allocation. Static caches only.
//   * Time.timeScale is always restored in a finally block so a script exception
//     cannot permanently freeze the game.
// =============================================================================

using System.Collections;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Singleton combat-feel hub: hit stop, combo counter, kill streak.
    /// Call the static <see cref="Hit"/> / <see cref="Kill"/> entry points from
    /// <see cref="Enemy"/>; subscribe to <see cref="OnComboChanged"/> /
    /// <see cref="OnKillStreakChanged"/> from HUD components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatFeedbackManager : MonoBehaviour
    {
        public static CombatFeedbackManager Instance { get; private set; }

        // ── Hit stop ─────────────────────────────────────────────────────────

        /// <summary>Time scale applied during the hit-stop dip (0–1).</summary>
        [Header("Hit Stop")]
        [SerializeField, Range(0.01f, 0.3f)] private float _hitStopTimescale = 0.05f;

        /// <summary>Real-time seconds the hit-stop dip lasts.</summary>
        [SerializeField, Range(0.02f, 0.2f)] private float _hitStopDurationSeconds = 0.05f;

        /// <summary>
        /// Lighter camera shake intensity on a surviving hit (landing-blow feel).
        /// Set to 0 to disable per-hit shake — death kills already shake harder via
        /// Enemy.Die().
        /// </summary>
        [SerializeField, Range(0f, 1f)] private float _hitShakeIntensity = 0.06f;

        /// <summary>Duration of the per-hit camera shake.</summary>
        [SerializeField, Range(0f, 0.5f)] private float _hitShakeDuration = 0.12f;

        // ── Combo ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Seconds with no hits before the combo count resets to zero.
        /// </summary>
        [Header("Combo Counter")]
        [SerializeField, Min(0.5f)] private float _comboWindowSeconds = 2.5f;

        // ── Kill streak ───────────────────────────────────────────────────────

        /// <summary>
        /// Seconds with no kills before the kill streak resets to zero.
        /// </summary>
        [Header("Kill Streak")]
        [SerializeField, Min(1f)] private float _killStreakWindowSeconds = 8f;

        // ── Runtime state ─────────────────────────────────────────────────────

        private int _comboCount;
        private float _comboTimer;

        private int _killStreak;
        private float _killStreakTimer;

        private Coroutine _hitStopRoutine;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires every time the combo count changes — including the reset to zero.
        /// Argument: new combo count.
        /// </summary>
        public event System.Action<int> OnComboChanged;

        /// <summary>
        /// Fires every time the kill streak changes — including the reset to zero.
        /// Argument: new kill streak.
        /// </summary>
        public event System.Action<int> OnKillStreakChanged;

        // ── Bootstrap ─────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[CombatFeedbackManager]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CombatFeedbackManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            // Guarantee timeScale is sane if this object is torn down mid-dip.
            Time.timeScale = 1f;
            if (Instance == this) Instance = null;
        }

        // ── Update — decay timers (unscaled so they work during hit stop) ─────

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_comboCount > 0)
            {
                _comboTimer -= dt;
                if (_comboTimer <= 0f)
                {
                    _comboCount = 0;
                    OnComboChanged?.Invoke(0);
                }
            }

            if (_killStreak > 0)
            {
                _killStreakTimer -= dt;
                if (_killStreakTimer <= 0f)
                {
                    _killStreak = 0;
                    OnKillStreakChanged?.Invoke(0);
                }
            }
        }

        // ── Public static API — called from Enemy.cs ──────────────────────────

        /// <summary>
        /// Call on every successful hit that the enemy survives. Triggers hit stop,
        /// increments combo, and fires a per-hit camera shake.
        /// </summary>
        /// <param name="worldPos">World-space impact position (used for future per-hit VFX).</param>
        /// <param name="damage">Raw damage amount landed this hit.</param>
        public static void Hit(Vector3 worldPos, float damage)
        {
            Instance?.RegisterHit(worldPos, damage);
        }

        /// <summary>
        /// Call when an enemy is killed (hp reached zero). Increments the kill streak.
        /// </summary>
        /// <param name="worldPos">World-space death position.</param>
        public static void Kill(Vector3 worldPos)
        {
            Instance?.RegisterKill(worldPos);
        }

        // ── Properties — read by HUD / UI ─────────────────────────────────────

        /// <summary>Current consecutive-hit combo count.</summary>
        public static int ComboCount => Instance != null ? Instance._comboCount : 0;

        /// <summary>Current kill streak count.</summary>
        public static int KillStreak => Instance != null ? Instance._killStreak : 0;

        // ── Instance logic ────────────────────────────────────────────────────

        private void RegisterHit(Vector3 worldPos, float damage)
        {
            // 1. Hit stop — restart if already running (successive hits refresh, never stack).
            if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
            _hitStopRoutine = StartCoroutine(HitStopRoutine());

            // 2. Per-hit camera shake (lighter than kill shake).
            if (_hitShakeIntensity > 0f)
                CameraShakeBridge.Shake(_hitShakeIntensity, _hitShakeDuration);

            // 3. Combo counter — refresh window and increment.
            _comboCount++;
            _comboTimer = _comboWindowSeconds;
            OnComboChanged?.Invoke(_comboCount);
        }

        private void RegisterKill(Vector3 worldPos)
        {
            _killStreak++;
            _killStreakTimer = _killStreakWindowSeconds;
            OnKillStreakChanged?.Invoke(_killStreak);
        }

        private IEnumerator HitStopRoutine()
        {
            Time.timeScale = _hitStopTimescale;
            try
            {
                yield return new WaitForSecondsRealtime(_hitStopDurationSeconds);
            }
            finally
            {
                // Restores timeScale even if the coroutine is interrupted (StopCoroutine,
                // MonoBehaviour destruction, scene unload).
                Time.timeScale = 1f;
                _hitStopRoutine = null;
            }
        }
    }
}
