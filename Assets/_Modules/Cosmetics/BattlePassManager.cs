// =============================================================================
// BattlePassManager — WO-73 battle pass runtime (reconciled).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Cosmetics  Namespace: DeNelle.Cosmetics
//
// Reconciliation vs WO-73 spec:
//   • WO-73 called this BattlePassSystem and referenced MonetizationManager
//     (which doesn't exist) and CosmeticData (which doesn't exist). Reconciled
//     to use GlimmerCurrencyService (the actual soft-currency service) and
//     BattlePassReward/BattlePassData (the actual Core Data types already built).
//   • WO-73 called LevelUpVFXController.Instance?.PlayLevelUp(transform, level, false)
//     — actual signature is PlayLevelUp(Vector3, int). Fixed to call
//     LevelUpVFXController via reflection (cross-asmdef: Cosmetics → Village is
//     NOT allowed). PlayLevelUp is optional; a LogWarning fires if missing.
//   • Persistence uses the same PlayerPrefs approach as WO-73 spec.
//   • Premium pass costs 2 400 Glimmer (using the branch's Glimmer soft currency
//     rather than "AetherShards" which don't exist on the branch).
//   • BattlePassReward.kind == Crystals → AddCrystals on GameState via
//     CoreServices; kind == Cosmetic → GlimmerCurrencyService.GrantAchievement.
//
// Authored BattlePassData SO must be assigned to `battlePassData` in the Inspector.
// Without it the manager logs a warning and is a no-op until an asset is created.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;
using DeNelle.Core.Data;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Cosmetics
{
    /// <summary>
    /// DontDestroyOnLoad singleton. Persists XP and level in PlayerPrefs.
    /// Grants free rewards to all players; premium rewards only when
    /// <see cref="HasPremium"/> is true.
    ///
    /// Add this component to your persistent manager GameObject alongside
    /// <see cref="GlimmerCurrencyService"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattlePassManager : MonoBehaviour
    {
        public static BattlePassManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Season")]
        [Tooltip("Authored BattlePassData ScriptableObject for this season. Required.")]
        public BattlePassData battlePassData;

        [Tooltip("Display name of the current season.")]
        public string seasonName = "Season 1 - Shadow Realms";

        [Header("Progress")]
        [Tooltip("Current tier (1-based). Driven by AddXP; also readable by UI.")]
        public int currentLevel = 1;

        [Tooltip("XP accumulated within the current tier.")]
        public int currentXP   = 0;

        [Tooltip("XP required to advance one tier.")]
        public int xpPerLevel  = 800;

        [Header("Premium Pass")]
        [Tooltip("Glimmer cost for the premium track. Default 2 400.")]
        public int premiumCostGlimmer = 2400;

        // ── Runtime state ─────────────────────────────────────────────────────
        private bool _hasPremium;

        // Reflection cache for LevelUpVFXController.PlayLevelUp(Vector3, int)
        // (DeNelle.Village — Cosmetics cannot reference Village directly).
        private static Type   s_lvupType;
        private static object s_lvupInstance;
        private static MethodInfo s_playMethod;
        private static bool   s_lvupResolved;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress();

            if (battlePassData == null)
                Debug.LogWarning("[BattlePassManager] battlePassData is not assigned. " +
                    "Rewards will be a no-op until a BattlePassData asset is wired.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Award XP and advance tiers automatically when the threshold is crossed.
        /// Fires <see cref="GrantReward"/> for each tier gained.
        /// </summary>
        public void AddXP(int amount)
        {
            if (amount <= 0) { FlowTrace.Warn("BattlePass", $"AddXP no-op: non-positive amount ({amount})"); return; }
            FlowTrace.Step("BattlePass", $"AddXP +{amount} (level={currentLevel} xp={currentXP}/{xpPerLevel})");
            currentXP += amount;

            while (currentXP >= xpPerLevel)
            {
                currentXP -= xpPerLevel;
                currentLevel++;
                FlowTrace.Step("BattlePass", $"tier up -> Lv{currentLevel} (carry xp={currentXP})");
                GrantReward(currentLevel);
            }

            SaveProgress();
        }

        /// <summary>
        /// Purchase the premium track for <see cref="premiumCostGlimmer"/> Glimmer.
        /// Returns true on success. If already owned, returns true immediately.
        /// On purchase, back-dates all premium rewards up to the current tier.
        /// </summary>
        public bool PurchasePremiumPass()
        {
            FlowTrace.Step("BattlePass", $"PurchasePremiumPass (cost={premiumCostGlimmer} glimmer, level={currentLevel})");
            if (_hasPremium) { FlowTrace.Step("BattlePass", "already premium — no charge"); return true; }

            var svc = GlimmerCurrencyService.Instance;
            if (svc == null)
            {
                FlowTrace.Fail("BattlePass", "premium purchase aborted: GlimmerCurrencyService.Instance is null (no debit, no grant)");
                Debug.LogWarning("[BattlePassManager] GlimmerCurrencyService not available.");
                return false;
            }

            // Deduct Glimmer via SpendGlimmer (balance-checked, same assembly).
            if (!svc.SpendGlimmer(premiumCostGlimmer))
            {
                FlowTrace.Warn("BattlePass", $"premium purchase rejected: SpendGlimmer failed (have {svc.Glimmer}, need {premiumCostGlimmer}) — no debit taken");
                Debug.LogWarning($"[BattlePassManager] Not enough Glimmer for premium pass " +
                    $"(have {svc.Glimmer}, need {premiumCostGlimmer}).");
                return false;
            }

            // DEBIT LANDED — the matching grant (premium flag + back-dated rewards) MUST follow.
            FlowTrace.Step("BattlePass", $"DEBITED {premiumCostGlimmer} glimmer — now granting premium + back-dating rewards to Lv{currentLevel}");
            _hasPremium = true;
            SaveProgress();
            Debug.Log("[BattlePassManager] Premium pass unlocked!");

            // Back-date all premium rewards up to the current tier.
            for (int i = 1; i <= currentLevel; i++)
                GrantReward(i);

            FlowTrace.Step("BattlePass", $"premium purchase COMMITTED (back-dated {currentLevel} tier(s))");
            return true;
        }

        /// <summary>True when the player owns the premium battle pass track.</summary>
        public bool HasPremium => _hasPremium;

        // ── Reward granting ───────────────────────────────────────────────────

        private void GrantReward(int level)
        {
            if (battlePassData == null) return;

            int idx = level - 1; // 0-based array index

            // Free track.
            if (battlePassData.freeTrack != null &&
                idx >= 0 && idx < battlePassData.freeTrack.Length)
            {
                ApplyReward(battlePassData.freeTrack[idx], level, premium: false);
            }

            // Premium track.
            if (_hasPremium &&
                battlePassData.premiumTrack != null &&
                idx >= 0 && idx < battlePassData.premiumTrack.Length)
            {
                ApplyReward(battlePassData.premiumTrack[idx], level, premium: true);
            }

            TryPlayLevelUpVfx(level);
        }

        private void ApplyReward(BattlePassReward reward, int level, bool premium)
        {
            string track = premium ? "Premium" : "Free";

            switch (reward.kind)
            {
                case BattlePassRewardKind.Crystals:
                    if (reward.amount > 0)
                    {
                        // Add crystals via GameStateService — unified onto Resources.Crystals
                        // (clamps >= 0, persists, raises ResourcesChanged).
                        var gs = GameStateService.Instance;
                        if (gs != null)
                        {
                            gs.AddCrystals(reward.amount);
                            FlowTrace.Step("BattlePass", $"{track} reward Lv{level}: +{reward.amount} Crystals");
                            Debug.Log($"[BattlePassManager] {track} reward Lv{level}: +{reward.amount} Crystals.");
                        }
                        else FlowTrace.Warn("BattlePass", $"{track} reward Lv{level}: {reward.amount} Crystals NOT granted — GameStateService.Instance null");
                    }
                    break;

                case BattlePassRewardKind.Cosmetic:
                    if (!string.IsNullOrEmpty(reward.rewardId))
                    {
                        var svc = GlimmerCurrencyService.Instance;
                        if (svc != null && svc.GrantAchievement(reward.rewardId))
                            FlowTrace.Step("BattlePass", $"{track} reward Lv{level}: cosmetic '{reward.rewardId}' unlocked");
                        else if (svc == null)
                            FlowTrace.Warn("BattlePass", $"{track} reward Lv{level}: cosmetic '{reward.rewardId}' NOT granted — GlimmerCurrencyService null");
                    }
                    break;

                case BattlePassRewardKind.Resource:
                    // Generic resource: rewardId is the resource key, amount is quantity.
                    // Hook into your resource system here when it exists.
                    Debug.Log($"[BattlePassManager] {track} reward Lv{level}: resource '{reward.rewardId}' x{reward.amount} (hook pending).");
                    break;

                default:
                    FlowTrace.Warn("BattlePass", $"unknown reward kind {reward.kind} at Lv{level} — nothing granted");
                    Debug.LogWarning($"[BattlePassManager] Unknown reward kind {reward.kind} at Lv{level}.");
                    break;
            }
        }

        // ── LevelUpVFX via reflection (Cosmetics → Village cross-asmdef guard) ──

        private void TryPlayLevelUpVfx(int level)
        {
            if (!s_lvupResolved)
                ResolveLevelUpVfx();

            if (s_lvupInstance == null || s_playMethod == null) return;

            try
            {
                s_playMethod.Invoke(s_lvupInstance, new object[] { transform.position, level });
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("BattlePass", $"LevelUpVFX invoke failed (cosmetic-only, reward already granted): {ex.GetType().Name}: {ex.Message}");
                Debug.LogWarning("[BattlePassManager] LevelUpVFX invoke failed: " + ex.Message);
            }
        }

        private static void ResolveLevelUpVfx()
        {
            s_lvupResolved = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    s_lvupType = asm.GetType("DeNelle.Village.LevelUpVFXController", false);
                    if (s_lvupType != null) break;
                }
                if (s_lvupType == null) return;

                var instanceProp = s_lvupType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                s_lvupInstance = instanceProp?.GetValue(null);

                s_playMethod = s_lvupType.GetMethod("PlayLevelUp",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(Vector3), typeof(int) },
                    null);
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("BattlePass", $"LevelUpVFX reflection resolve failed (VFX will no-op): {ex.GetType().Name}: {ex.Message}");
                Debug.LogWarning("[BattlePassManager] LevelUpVFX reflection failed: " + ex.Message);
            }
        }

        // ── Persistence ────────────────────────────────────────────────────────

        private const string PrefKeyLevel     = "BP_Level";
        private const string PrefKeyXP        = "BP_XP";
        private const string PrefKeyPremium   = "BP_HasPremium";

        private void SaveProgress()
        {
            PlayerPrefs.SetInt(PrefKeyLevel,   currentLevel);
            PlayerPrefs.SetInt(PrefKeyXP,      currentXP);
            PlayerPrefs.SetInt(PrefKeyPremium, _hasPremium ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void LoadProgress()
        {
            currentLevel = PlayerPrefs.GetInt(PrefKeyLevel,   1);
            currentXP    = PlayerPrefs.GetInt(PrefKeyXP,      0);
            _hasPremium  = PlayerPrefs.GetInt(PrefKeyPremium, 0) == 1;
        }
    }
}
