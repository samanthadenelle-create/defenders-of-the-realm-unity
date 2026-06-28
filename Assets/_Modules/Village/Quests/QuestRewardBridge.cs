// =============================================================================
// QuestRewardBridge — Village-side listener that DISPENSES story-quest rewards.
// -----------------------------------------------------------------------------
// QuestService (Core) raises RewardEarned(QuestReward) when a stage's reward is
// earned, but Core cannot reference the wallet (EconomyService). This bridge
// closes that gap: it subscribes to RewardEarned and grants crystals / food /
// magic / items through the live Village economy. Mirrors the DailyQuest reward
// pattern (QuestCompleted → grant) and the DailyQuestGateBridge lifecycle.
//
// Self-bootstraps via RuntimeInitializeOnLoadMethod into a DontDestroyOnLoad
// object. Village → Core only; all cross-calls are null-conditional.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.Quests;
using DeNelle.Village.Crafting;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class QuestRewardBridge : MonoBehaviour
    {
        private static QuestRewardBridge _instance;
        private bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("QuestRewardBridge");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<QuestRewardBridge>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            TrySubscribe();
        }

        private void OnEnable() => TrySubscribe();

        private void Update()
        {
            // QuestService self-bootstraps too; if it wasn't up yet at Awake, retry
            // cheaply until the subscription lands.
            if (!_subscribed) TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            var svc = QuestService.Instance;
            if (svc == null) return;
            svc.RewardEarned += OnRewardEarned;
            _subscribed = true;
        }

        private void OnDestroy()
        {
            if (QuestService.Instance != null) QuestService.Instance.RewardEarned -= OnRewardEarned;
            if (_instance == this) _instance = null;
        }

        private void OnRewardEarned(QuestReward reward)
        {
            if (reward == null) return;

            // Crystals + food route through the single economy wallet.
            if (reward.Crystals > 0 || reward.Food > 0)
                EconomyService.Instance?.Grant(crystals: reward.Crystals, food: reward.Food);

            // Magic is a building-upgrade tech axis (GameState.Magic top-level field,
            // no EconomyService bucket / no Add helper). Write it directly + persist.
            if (reward.Magic > 0)
            {
                var svc = DeNelle.Core.State.GameStateService.Instance;
                if (svc != null && svc.State != null)
                {
                    svc.State.Magic += reward.Magic;
                    svc.Save();
                }
            }

            // Item grants (WO-564): grant into the persisted larder / gear store
            // (VillageInventory.Add -> GameState.GearInventory, survives reload +
            // Neon-syncs) — the same store the shop/crafting use. Was log-only before.
            if (!string.IsNullOrEmpty(reward.GrantItemId))
            {
                var inv = VillageInventory.Instance;
                if (inv != null)
                {
                    inv.Add(reward.GrantItemId, 1);
                    FlowTrace.Step("Economy", $"Story quest granted item '{reward.GrantItemId}' x1");
                }
                else
                {
                    FlowTrace.Warn("Economy", $"Story quest item '{reward.GrantItemId}' lost — VillageInventory not ready");
                }
            }
        }
    }
}
