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
using DeNelle.Village.Arena;   // BattleArena — gate home-scene combat feel during a staged battle

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
        /// DEF-178 (mobile safety): minimum REAL seconds between hit-stops. RegisterHit
        /// fires on EVERY enemy that takes damage; in a 20-enemy AoE wave (mage R, tower
        /// burst, multi-hit melee) that previously re-froze Time.timeScale every frame —
        /// a continuous stutter that reads as lag and is nauseating on mobile. With this
        /// cap a fresh freeze can only start once per window; rapid extra hits inside the
        /// window still register combo + shake + VFX, they just don't re-trigger the
        /// freeze. 0 = uncapped (legacy behaviour).
        /// </summary>
        [SerializeField, Range(0f, 0.5f)] private float _hitStopMinIntervalSeconds = 0.12f;

        // ── Kill slo-mo (the "death blow" beat) ───────────────────────────────
        /// <summary>Time scale during a kill slo-mo dip — deeper + longer than the hit-stop,
        /// so a finishing blow reads as a dramatic slow-time moment.</summary>
        [Header("Kill Slo-Mo (death blow)")]
        [SerializeField, Range(0.05f, 0.6f)] private float _killSloMoTimescale = 0.3f;
        /// <summary>Real-time seconds the kill slo-mo lasts.</summary>
        [SerializeField, Range(0.1f, 1f)] private float _killSloMoDurationSeconds = 0.45f;
        /// <summary>Minimum REAL seconds between kill slo-mos so it stays a SPECIAL beat (one
        /// per window) instead of a constant stutter when a swarm dies together. 0 = every kill.</summary>
        [SerializeField, Range(0f, 12f)] private float _killSloMoMinIntervalSeconds = 6f;
        /// <summary>Heavier camera kick on the slo-mo'd finisher.</summary>
        [SerializeField, Range(0f, 1f)] private float _killSloMoShake = 0.18f;
        [SerializeField, Range(0f, 0.6f)] private float _killSloMoShakeDuration = 0.35f;

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

        /// <summary>DEF-178: unscaled time the last hit-stop was allowed to start (rate cap).</summary>
        private float _lastHitStopTime = -999f;

        private Coroutine _killSloMoRoutine;
        private float _lastKillSloMoTime = -999f;

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

        // BATTLE ISOLATION helper: true when this impact is a HOME-scene bleed-through that must be
        // suppressed — i.e. a battle is staged AND the impact is NOT inside the far-offset arena.
        // A live arena hit (inside the arena) always passes; outside-arena hits only pass when no
        // battle is running. Guarded so a missing arena singleton never throws into combat.
        private static bool SuppressHomeBleed(Vector3 worldPos)
        {
            // Non-creating check: never spawn the arena singleton just to probe (BattleArena.Existing
            // is null until a fight is first staged).
            if (!BattleArena.AnyBattleInProgress) return false;
            return !BattleArena.IsArenaPosition(worldPos);
        }

        private void RegisterHit(Vector3 worldPos, float damage)
        {
            // BATTLE ISOLATION: while an additive arena battle is staged, the home scene must NOT
            // contribute combat feel (hit-stop / shake / rumble) — that bled into the battle as a
            // phantom rumble and double-simmed the feel (choppy). Gate by IMPACT POSITION so the
            // LIVE arena's own hits (staged ~7km away) still fire their feel; only home-scene
            // bleed-through (impacts back near the origin world) is suppressed during a battle.
            if (SuppressHomeBleed(worldPos)) return;

            // 1. Hit stop — rate-capped (DEF-178). A fresh freeze can only start once
            // per _hitStopMinIntervalSeconds so a multi-enemy AoE wave doesn't re-freeze
            // every frame. Extra hits inside the window still drive combo + shake + VFX
            // below; they just don't restart the time-freeze. Cap 0 = legacy uncapped.
            if (_killSloMoRoutine == null && Time.unscaledTime - _lastHitStopTime >= _hitStopMinIntervalSeconds)
            {
                _lastHitStopTime = Time.unscaledTime;
                if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
                _hitStopRoutine = StartCoroutine(HitStopRoutine());
            }

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
            // BATTLE ISOLATION (see RegisterHit): suppress home-scene kill feel (streak slow-mo /
            // shake / rumble) while an arena battle is staged so it can't bleed into the fight; the
            // live arena's own kills (far-offset) still fire.
            if (SuppressHomeBleed(worldPos)) return;

            _killStreak++;
            _killStreakTimer = _killStreakWindowSeconds;
            OnKillStreakChanged?.Invoke(_killStreak);

            // The "death blow" slo-mo beat — rate-capped so it lands as a SPECIAL moment
            // (one per window), not a stutter when a swarm dies at once. The killing hit
            // also fired a brief hit-stop; supersede it so the slow-time wins cleanly.
            if (_killSloMoTimescale < 1f
                && Time.unscaledTime - _lastKillSloMoTime >= _killSloMoMinIntervalSeconds)
            {
                _lastKillSloMoTime = Time.unscaledTime;
                StartSloMo();
            }
        }

        /// <summary>
        /// A perfect parry/deflect beat — ALWAYS gets the slow-time moment (skill-gated, so no
        /// rate-cap) + a heavier camera kick. Used by both the Knight's physical parry and the
        /// caster's magical deflect (PlayerAttackController.OnParrySuccess).
        /// </summary>
        public static void Parry(Vector3 worldPos)
        {
            if (Instance == null) return;
            Instance._lastKillSloMoTime = Time.unscaledTime; // so a kill right after doesn't double-dip
            Instance.StartSloMo();
        }

        // Shared slow-time starter: supersede any hit-stop so the slow-time wins cleanly.
        private void StartSloMo()
        {
            if (_hitStopRoutine != null) { StopCoroutine(_hitStopRoutine); _hitStopRoutine = null; }
            if (_killSloMoRoutine != null) StopCoroutine(_killSloMoRoutine);
            _killSloMoRoutine = StartCoroutine(KillSloMoRoutine());
            if (_killSloMoShake > 0f) CameraShakeBridge.Shake(_killSloMoShake, _killSloMoShakeDuration);
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

        // The deeper/longer slow-time dip for a finishing blow (see RegisterKill). Same
        // safe pattern as HitStopRoutine — timeScale always restored in the finally.
        private IEnumerator KillSloMoRoutine()
        {
            Time.timeScale = _killSloMoTimescale;
            try
            {
                yield return new WaitForSecondsRealtime(_killSloMoDurationSeconds);
            }
            finally
            {
                Time.timeScale = 1f;
                _killSloMoRoutine = null;
            }
        }
    }
}
