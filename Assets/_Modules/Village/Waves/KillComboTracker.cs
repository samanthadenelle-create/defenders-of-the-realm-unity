// =============================================================================
// KillComboTracker — escalating kill-combo feedback (WO-83, extends WO-60).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Sits on top of CombatFeedbackManager.OnKillStreakChanged (the project's
// existing kill-streak system) to add the three escalation tiers defined in
// WO-83:
//
//   Tier 1 — 3 kills in 6 s → Combo_Tier1 VFX + Light shake + "COMBO!" text
//   Tier 2 — 5 kills in 6 s → Combo_Tier2 VFX + Medium shake + "RAMPAGE!" + 25 Gold
//   Tier 3 — 8+ kills in 6 s → Combo_Tier2 VFX + Heavy shake + "UNSTOPPABLE!" + 60 Gold
//
// CombatFeedbackManager owns the raw kill-streak counter and the 8-second decay
// window. KillComboTracker listens to its OnKillStreakChanged event and fires
// VFX / UI / reward at the Tier thresholds.
//
// Combo text: instantiates a world-space text prefab above the hero when
// assigned; falls back to an IMGUI toast when the prefab is null.
//
// Bootstrapped at runtime — no scene edit needed when a WaveManager is present.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;   // FlowTrace — the combo grant is traced, never silently swallowed (§12)

namespace DeNelle.Village
{
    /// <summary>
    /// Watches <see cref="CombatFeedbackManager.OnKillStreakChanged"/> and
    /// fires escalating feedback at Tier 1/2/3 kill-streak thresholds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillComboTracker : MonoBehaviour
    {
        public static KillComboTracker Instance { get; private set; }

        // ── Thresholds ────────────────────────────────────────────────────────

        private const int Tier1Threshold = 3;
        private const int Tier2Threshold = 5;
        private const int Tier3Threshold = 8;

        // ── Gold rewards (was Aether/Crystals - owner ruling 2026-08-04) ────────────────────────────────────────────────────

        // OWNER RULING 2026-08-04 — "make it gold". These were CRYSTAL grants, and measurement
        // (docs/ECONOMY_REWARD_MEASUREMENT_2026-08-04.md §5) found they paid ~1,435 crystals per
        // 20-wave run against the DESIGNED boss-drop rate of 3.6 — roughly 400x — making an
        // undocumented combo bonus ~70% of everything a player banked, and directly contradicting
        // the WO-830 monetization guard in echoes-balance.json ("crystals remain the slowest
        // faucet") that WO-855 was balanced around.
        //
        // The combo payoff is KEPT and the values are unchanged; only the CURRENCY moved. Gold is
        // the one currency the same measurement found correctly tuned (it tracks enemy HP at
        // ~0.084 coin/HP across the whole roster), and at gold weight this is ~1/17th the basket
        // value it was paying in crystals.
        private const int Tier2GoldReward = 25;
        private const int Tier3GoldReward = 60;

        // ── Combo text ────────────────────────────────────────────────────────

        [Header("Combo Text")]
        [Tooltip("World-space prefab with a TextMeshPro component. " +
                 "Falls back to IMGUI toast when null.")]
        [SerializeField] private GameObject _comboTextPrefab;

        [Tooltip("Seconds the combo text lives before being destroyed.")]
        [SerializeField, Min(0.2f)] private float _comboTextLifetime = 1.2f;

        // ── IMGUI fallback ────────────────────────────────────────────────────
        private string _toastText;
        private float  _toastTimer;

        // ── Hero Transform cache ──────────────────────────────────────────────
        private Transform _heroTransform;

        // ── Tier tracking (prevent re-firing the same tier twice per streak) ──
        private int _lastFiredTier;

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            // Subscribe to CombatFeedbackManager's kill-streak event.
            if (CombatFeedbackManager.Instance != null)
                CombatFeedbackManager.Instance.OnKillStreakChanged += OnKillStreakChanged;
        }

        private void OnDisable()
        {
            if (CombatFeedbackManager.Instance != null)
                CombatFeedbackManager.Instance.OnKillStreakChanged -= OnKillStreakChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Streak event handler ──────────────────────────────────────────────

        private void OnKillStreakChanged(int streak)
        {
            // Streak reset to 0 — reset tier tracking for the next streak.
            if (streak == 0) { _lastFiredTier = 0; return; }

            Vector3 heroPos = GetHeroPosition();

            if (streak >= Tier3Threshold && _lastFiredTier < 3)
            {
                _lastFiredTier = 3;
                VFXManager.Play(VFXType.Combo_Tier2, heroPos + Vector3.up * 0.5f);
                CameraShakeBridge.Shake(0.48f, 0.32f);   // Heavy tier
                ShowComboText("UNSTOPPABLE!", heroPos);
                GrantComboGold(Tier3GoldReward);
                // AudioService.Instance?.PlaySfx(SfxId.Combo3);
            }
            else if (streak >= Tier2Threshold && _lastFiredTier < 2)
            {
                _lastFiredTier = 2;
                VFXManager.Play(VFXType.Combo_Tier2, heroPos);
                CameraShakeBridge.Shake(0.30f, 0.25f);   // Medium tier
                ShowComboText("RAMPAGE!", heroPos);
                GrantComboGold(Tier2GoldReward);
                // AudioService.Instance?.PlaySfx(SfxId.Combo2);
            }
            else if (streak >= Tier1Threshold && _lastFiredTier < 1)
            {
                _lastFiredTier = 1;
                VFXManager.Play(VFXType.Combo_Tier1, heroPos);
                CameraShakeBridge.Shake(0.14f, 0.18f);   // Light tier
                ShowComboText("COMBO!", heroPos);
                // AudioService.Instance?.PlaySfx(SfxId.Combo1);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Vector3 GetHeroPosition()
        {
            if (_heroTransform != null && _heroTransform.gameObject.activeInHierarchy)
                return _heroTransform.position;

            // WO-1513: the old second term read the "HeroTarget" tag, which
            // TagManager.asset has never declared — a permanently dead branch.
            // The hero definitively carries HeroLocomotion (CLAUDE.md §7).
            var heroGo = GameObject.FindWithTag("Player");
            if (heroGo == null)
            {
                var loco = FindFirstObjectByType<HeroLocomotion>();
                if (loco != null) heroGo = loco.gameObject;
            }
            if (heroGo != null) _heroTransform = heroGo.transform;

            return _heroTransform != null ? _heroTransform.position : Vector3.zero;
        }

        /// <summary>
        /// Spawns a short-lived combo label above the hero. Falls back to an
        /// IMGUI toast when the prefab is not assigned.
        /// </summary>
        private void ShowComboText(string text, Vector3 worldPos)
        {
            if (_comboTextPrefab != null)
            {
                Vector3 spawnPos = worldPos + Vector3.up * 2.2f;
                var obj = Instantiate(_comboTextPrefab, spawnPos, Quaternion.identity);

                // Set text via reflection so no hard TMPro assembly reference is needed.
                var comp = obj.GetComponent("TMP_Text")
                        ?? obj.GetComponent("TextMeshPro");
                if (comp != null)
                {
                    var textProp = comp.GetType().GetProperty("text");
                    textProp?.SetValue(comp, text);
                }
                else
                {
                    // Legacy UnityEngine.UI.Text fallback.
                    var uiText = obj.GetComponent<UnityEngine.UI.Text>();
                    if (uiText != null) uiText.text = text;
                }

                Destroy(obj, _comboTextLifetime);
            }
            else
            {
                // IMGUI toast.
                _toastText  = text;
                _toastTimer = _comboTextLifetime;
            }
        }

        /// <summary>
        /// Award the combo bonus in GOLD (owner ruling 2026-08-04 — see the reward constants above
        /// for why this is no longer a crystal grant). Gold is the shop/research wallet and is the
        /// currency the reward measurement found correctly tuned.
        /// </summary>
        private static void GrantComboGold(int amount)
        {
            var econ = EconomyService.Instance;
            if (econ == null)
            {
                FlowTrace.Warn("Economy", $"combo bonus of {amount} gold dropped - no EconomyService");
                return;
            }

            // No silent swallow (§12): a catch that hides a failure is forbidden, because a reward
            // that quietly stops paying is exactly the class of defect this file just came out of.
            try
            {
                econ.AddCoins(amount);
                FlowTrace.Once("Economy", "combo-gold", $"combo bonus paid {amount} gold");
            }
            catch (System.Exception e)
            {
                FlowTrace.Fail("Economy", $"combo bonus of {amount} gold threw: {e.Message}");
            }
        }

        // ── Update — toast timer ──────────────────────────────────────────────

        private void Update()
        {
            if (_toastTimer > 0f) _toastTimer -= Time.deltaTime;
        }

        // ── IMGUI toast fallback ──────────────────────────────────────────────

        private void OnGUI()
        {
            if (_toastTimer <= 0f || string.IsNullOrEmpty(_toastText)) return;

            float alpha = Mathf.Clamp01(_toastTimer * 2f);   // fade out in last 0.5 s
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(1f, 0.85f, 0.2f, alpha) }
            };

            GUI.Label(new Rect(0f, Screen.height * 0.25f, Screen.width, 44f),
                _toastText, style);
        }

        // ── Bootstrap ─────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => TryInstall();

        private static void TryInstall()
        {
            if (FindAnyObjectByType<WaveManager>() == null) return;
            if (Instance != null) return;

            var go = new GameObject("[KillComboTracker]");
            go.AddComponent<KillComboTracker>();
            Debug.Log("[KillComboTracker] Installed.");
        }
    }
}
