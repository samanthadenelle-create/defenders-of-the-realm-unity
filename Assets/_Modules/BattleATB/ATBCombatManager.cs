// =============================================================================
// ATBCombatManager — turn-timer singleton for the ATB battle scene (WO-68).
// -----------------------------------------------------------------------------
// Tracks whether an ATB combat session is active and drives a per-turn timer
// that fires an enemy auto-attack when the player idles too long (WO-93).
//
// WIRING
//   BattleController.Start() calls ATBCombatManager.Instance?.StartCombat()
//   after _runtimeState.StartBattle(). The timer is then driven by Update()
//   while _combatActive is true and it is the player's turn (_playerTurn).
//
//   When the player acts (Attack / Skills / Item), BattleController calls
//   ATBCombatManager.Instance?.OnPlayerActed() to reset the turn timer.
//
//   When the battle ends, BattleController calls
//   ATBCombatManager.Instance?.StopCombat() so the timer halts cleanly.
//
// TurnProgress (0..1) is read by the BattleHUD ATB fill bar for the
// "idle pressure" visual (WO-93 requirement).
// =============================================================================

using DeNelle.Core.Combat;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace DeNelle.BattleATB
{
    /// <summary>
    /// Turn-timer singleton for the ATB battle scene. Tracks active-combat state
    /// and exposes a per-turn countdown that auto-fires an enemy attack when the
    /// player idles past <see cref="maxTurnTime"/> seconds (WO-68 / WO-93).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ATBCombatManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        /// <summary>The live scene instance, or null when the ATB scene is not loaded.</summary>
        public static ATBCombatManager Instance { get; private set; }

        // ── Self-bootstrap ────────────────────────────────────────────────────
        // ATBBattle.unity has NO authored ATBCombatManager, so Instance was always
        // null → the idle-turn-timeout safety net (HandleIdleTimeout, built to stop
        // stalls) never ran → battles could hang forever ("ATB endless loop",
        // break-log 2026-06-14). Bootstrap on EVERY ATB scene load (not once at
        // startup — the battle is entered later from the village), so the timeout
        // always advances the fight.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallBootstrap()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            OnAnySceneLoaded(SceneManager.GetActiveScene(), default);
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == null ||
                scene.name.IndexOf("ATBBattle", System.StringComparison.OrdinalIgnoreCase) < 0)
                return;
            if (Instance == null)
                new GameObject("ATBCombatManager (auto)").AddComponent<ATBCombatManager>();
        }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Turn Settings")]
        [Tooltip("Seconds the player has to act before the enemy auto-attacks.")]
        public float maxTurnTime = 8f;

        [Header("Events")]
        [Tooltip("Fires when the player's turn begins (or resets after they act).")]
        public UnityEvent onPlayerTurnStart = new UnityEvent();

        [Tooltip("Fires when the idle timer expires and the enemy auto-attacks.")]
        public UnityEvent onEnemyAutoAttack = new UnityEvent();

        // ── Runtime state ─────────────────────────────────────────────────────

        private float _currentTime;
        private bool  _playerTurn   = true;
        private bool  _combatActive = false;

        /// <summary>
        /// How far through the current player turn the idle timer is (0 = just
        /// started, 1 = timed out). Drives the HUD "idle pressure" bar.
        /// </summary>
        public float TurnProgress => Mathf.Clamp01(_currentTime / Mathf.Max(0.001f, maxTurnTime));

        /// <summary>True while a combat session is running.</summary>
        public bool IsActive => _combatActive;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        // WO-437: probe the battle-active predicate so the single source of truth
        // (DeNelle.Core.Combat.BattleLock) is TRUE while an ATB combat session runs.
        // The gate (PanelManager, hotkeys, Yarn verbs) reads BattleLock.IsInBattle().
        private System.Func<bool> _battleProbe;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _battleProbe = () => _combatActive;
            BattleLock.RegisterProbe(_battleProbe);
        }

        private void OnDestroy()
        {
            BattleLock.UnregisterProbe(_battleProbe);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_combatActive || !_playerTurn) return;

            _currentTime += Time.deltaTime;
            if (_currentTime >= maxTurnTime)
            {
                _playerTurn  = false;
                _currentTime = 0f;
                onEnemyAutoAttack?.Invoke();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Begin a combat session. Resets the turn timer and fires
        /// <see cref="onPlayerTurnStart"/>. Called by <see cref="BattleController"/>
        /// right after <c>ATBRuntimeState.StartBattle</c> (WO-68).
        /// </summary>
        public void StartCombat()
        {
            _combatActive = true;
            _currentTime  = 0f;
            _playerTurn   = true;
            onPlayerTurnStart?.Invoke();
        }

        /// <summary>
        /// The player submitted an action — reset the idle timer and signal the
        /// start of a fresh player turn. Called by <see cref="BattleController"/>
        /// on every Attack / Skills / Item click (WO-93).
        /// </summary>
        public void OnPlayerActed()
        {
            if (!_combatActive) return;
            _currentTime = 0f;
            _playerTurn  = true;
            onPlayerTurnStart?.Invoke();
        }

        /// <summary>
        /// End the combat session cleanly (called on battle outcome / flee).
        /// </summary>
        public void StopCombat()
        {
            _combatActive = false;
            _currentTime  = 0f;
            _playerTurn   = false;
        }
    }
}
