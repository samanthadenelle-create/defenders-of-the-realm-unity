// =============================================================================
// AttackTimingBonus (DEF-47) — precision timing window for chained ability casts.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Rewards players who cast abilities in quick succession with an escalating
//   damage multiplier — creating a satisfying "combo rhythm" without changing
//   any base ability stats or data.
//
//   Each ability cast that lands within _chainWindowSeconds of the previous cast
//   increments the chain counter (max 3). The bonus multiplier scales with chain:
//     Chain 1 (first cast, or after timeout): 1.00× (baseline)
//     Chain 2 (cast within window):           1.15×
//     Chain 3 (cast within window again):     1.30×
//     Chain 4+ (maintained chain):            1.50× (capped)
//
//   If the player waits longer than _chainWindowSeconds between casts, the chain
//   resets to 1 and the next cast fires at baseline damage.
//
// INTEGRATION:
//   HeroAbilities.TryCast() calls AttackTimingBonus.NotifyCast() when a cast
//   lands. HeroAbilities.ResolveEffect() multiplies outgoing damage by
//   AttackTimingBonus.ConsumeBonus() — ConsumeBonus is a "read + reset" that
//   returns the accumulated bonus for THIS cast and prepares the window for the
//   next one.
//
// FEEDBACK:
//   OnChainChanged(int chain) fires on every chain change. HUD / VFX can
//   subscribe to show a "CHAIN x2!" indicator. DamageNumberSpawner pops a
//   coloured label above the hit point for chains ≥ 2.
//
// ARCHITECTURE:
//   * DontDestroyOnLoad singleton (BeforeSceneLoad bootstrap) — same pattern as
//     CombatFeedbackManager.
//   * Update() uses Time.unscaledDeltaTime to decay the window even during
//     hit-stop.
//   * Static API so HeroAbilities doesn't need a component reference.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Tracks quick-cast chains and provides a damage multiplier bonus for
    /// consecutive ability casts within a timing window.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackTimingBonus : MonoBehaviour
    {
        public static AttackTimingBonus Instance { get; private set; }

        // ── Tuning ────────────────────────────────────────────────────────────

        [Header("Chain window")]
        [Tooltip("Seconds after a cast during which the next cast extends the chain.")]
        [SerializeField, Range(0.3f, 3f)] private float _chainWindowSeconds = 1.2f;

        [Header("Bonus multipliers per chain depth")]
        [Tooltip("Chain 1 = baseline (always 1.0 — no bonus on first cast).")]
        [SerializeField] private float _chain1Bonus = 1.00f;
        [Tooltip("Chain 2 — second cast within the window.")]
        [SerializeField] private float _chain2Bonus = 1.15f;
        [Tooltip("Chain 3 — third consecutive cast.")]
        [SerializeField] private float _chain3Bonus = 1.30f;
        [Tooltip("Chain 4+ — sustained combo.")]
        [SerializeField] private float _chain4PlusBonus = 1.50f;

        // ── Runtime state ─────────────────────────────────────────────────────

        private int   _chain;         // current chain depth (1 = no bonus)
        private float _windowTimer;   // countdown to chain expiry
        private float _pendingBonus;  // bonus for the current cast (set by NotifyCast)

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fires when the chain depth changes. Arg = new chain count (1 = reset).</summary>
        public event System.Action<int> OnChainChanged;

        // ── Bootstrap ─────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[AttackTimingBonus]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AttackTimingBonus>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }   // remove only the redundant component, never the host GameObject (it may be the hero)
            Instance = this;
            _chain = 1;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Update — window decay ─────────────────────────────────────────────

        private void Update()
        {
            if (_chain <= 1) return;   // nothing to decay

            _windowTimer -= Time.unscaledDeltaTime;
            if (_windowTimer <= 0f)
            {
                int prev = _chain;
                _chain = 1;
                _pendingBonus = _chain1Bonus;
                if (prev != _chain)
                {
                    FlowTrace.Step("Chain", $"window expired — chain reset {prev}->1 (bonus back to baseline).");
                    OnChainChanged?.Invoke(_chain);
                }
            }
        }

        // ── Static API — called from HeroAbilities ────────────────────────────

        /// <summary>
        /// Call when an ability cast succeeds. Increments the chain if within the
        /// window; otherwise resets. Returns the bonus multiplier that should be
        /// applied to this cast's damage.
        /// </summary>
        public static float NotifyCast(Vector3 castWorldPos)
        {
            if (Instance == null)
            {
                FlowTrace.Warn("Chain", "NotifyCast: no AttackTimingBonus instance — returning baseline 1.0 (no chain bonus).");
                return 1f;
            }
            return Instance.RegisterCast(castWorldPos);
        }

        // ── Instance logic ────────────────────────────────────────────────────

        private float RegisterCast(Vector3 castWorldPos)
        {
            bool inWindow = _chain > 1 && _windowTimer > 0f;

            if (inWindow)
                _chain = Mathf.Min(_chain + 1, 4);
            else
                _chain = 1;

            _windowTimer  = _chainWindowSeconds;
            _pendingBonus = BonusForChain(_chain);

            FlowTrace.Step("Chain", $"RegisterCast inWindow={inWindow} -> chain={_chain} bonus={_pendingBonus:0.00}x");
            OnChainChanged?.Invoke(_chain);

            // Pop a chain label for chains ≥ 2 so the player gets feedback.
            if (_chain >= 2)
            {
                string label   = _chain >= 4 ? "MAX CHAIN!" : $"CHAIN ×{_chain}";
                Color  colour  = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.3f, 0.1f),
                                           (_chain - 2) / 2f);
                DamageNumberSpawner.SpawnLabel(label, castWorldPos + Vector3.up * 1.5f, colour, 1.1f);
            }

            return _pendingBonus;
        }

        private float BonusForChain(int chain)
        {
            return chain switch
            {
                1  => _chain1Bonus,
                2  => _chain2Bonus,
                3  => _chain3Bonus,
                _  => _chain4PlusBonus,
            };
        }

        // ── Properties ───────────────────────────────────────────────────────

        /// <summary>Current chain depth (1 = no active chain).</summary>
        public static int ChainDepth => Instance != null ? Instance._chain : 1;

        /// <summary>Seconds remaining in the current chain window (0 when no chain).</summary>
        public static float WindowRemaining => Instance != null ? Mathf.Max(0f, Instance._windowTimer) : 0f;
    }
}
