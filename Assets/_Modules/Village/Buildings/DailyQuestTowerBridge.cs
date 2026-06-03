// =============================================================================
// DailyQuestTowerBridge — DEF-223. Two jobs, both additive (no scene-builder /
// VillageSceneBuilder edits, mirrors the DailyQuestCombatBridge pattern):
//
//   1) TOWER-PLACEMENT HOOK — subscribes to TowerPlacementSystem.OnTowerPlaced
//      and reports one tick to the DailyQuestService for the Day-1
//      "Build {target} defensive towers" quest (template id "combat.build-towers",
//      tracked 0/4 → 4/4). TowerPlacementSystem is a scene singleton that may not
//      exist until the player first opens build mode, so the bridge watches
//      TowerPlacementSystem.Instance and (re)subscribes whenever it changes —
//      surviving scene reloads without a hard scene-builder dependency.
//
//   2) REWARD DISPENSE — subscribes to DailyQuestService.QuestCompleted and
//      grants the completed quest's slot reward. DeNelle.Core (where the quest
//      service lives) cannot reference the Village wallet, so the grant happens
//      here on the Village side: crystals → GameState.Resources.Crystals
//      (the single spend/earn store), wisdom → WisdomCurrencyService.
//
// Self-bootstraps via RuntimeInitializeOnLoadMethod into a DontDestroyOnLoad
// object, the same lifecycle the DailyQuestService / WisdomCurrencyService use.
// =============================================================================

using DeNelle.Core.Quests;
using DeNelle.Core.State;
using DeNelle.Cosmetics;
using DeNelle.Village.Talents;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class DailyQuestTowerBridge : MonoBehaviour
    {
        private static DailyQuestTowerBridge _instance;

        // The placement singleton we are currently subscribed to (may be null
        // until the player opens build mode, and changes across scene reloads).
        private TowerPlacementSystem _subscribedPlacement;
        private bool _rewardsHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("DailyQuestTowerBridge");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<DailyQuestTowerBridge>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnEnable()  => HookReward();
        private void OnDisable() => UnhookReward();

        private void OnDestroy()
        {
            UnsubscribePlacement();
            UnhookReward();
            if (_instance == this) _instance = null;
        }

        // ── 1) Tower-placement subscription (re-checked each frame) ────────────

        private void Update()
        {
            // Late-bind: TowerPlacementSystem is a scene object created lazily.
            var live = TowerPlacementSystem.Instance;
            if (live == _subscribedPlacement) return;

            UnsubscribePlacement();
            if (live != null)
            {
                live.OnTowerPlaced += HandleTowerPlaced;
                _subscribedPlacement = live;
            }
            // The reward hook can also race the quest service's bootstrap, so
            // make sure it's wired once the service exists.
            if (!_rewardsHooked) HookReward();
        }

        private void UnsubscribePlacement()
        {
            if (_subscribedPlacement != null)
                _subscribedPlacement.OnTowerPlaced -= HandleTowerPlaced;
            _subscribedPlacement = null;
        }

        private void HandleTowerPlaced(DeNelle.Core.Data.TowerData _)
        {
            // One tick per tower placed → advances "combat.build-towers" 0/4 → 4/4.
            DailyQuestService.Instance?.Report(DailyQuestService.Day1QuestTemplateId, 1);
        }

        // ── 2) Reward dispense on quest completion ────────────────────────────

        private void HookReward()
        {
            if (_rewardsHooked) return;
            var svc = DailyQuestService.Instance;
            if (svc == null) return;
            svc.QuestCompleted += HandleQuestCompleted;
            _rewardsHooked = true;
        }

        private void UnhookReward()
        {
            if (!_rewardsHooked) return;
            var svc = DailyQuestService.Instance;
            if (svc != null) svc.QuestCompleted -= HandleQuestCompleted;
            _rewardsHooked = false;
        }

        private void HandleQuestCompleted(DailyQuestInstance q)
        {
            if (q == null || q.ClaimedAtUnix != 0) return; // already paid out
            var reward = DailyQuestCatalog.RewardFor(q.Slot);
            if (reward == null) return;

            // Crystals → the canonical resource wallet (the same store the build
            // menu reads/spends). AddCrystals clamps, persists, and raises
            // ResourcesChanged so the HUD updates — the single earn/spend site.
            if (reward.RewardCrystals > 0)
                GameStateService.Instance?.AddCrystals(reward.RewardCrystals);

            // Wisdom → the talent currency (self-installing singleton).
            if (reward.RewardWisdom > 0)
                WisdomCurrencyService.Instance?.Grant(reward.RewardWisdom);

            // Glimmer → the cosmetic-shop currency (DEF-29). daily-quests.json
            // already specifies rewardGlimmer per slot, but the dispenser dropped
            // it (no branch existed) so quest Glimmer never actually entered the
            // wallet. Honour the data-driven amount — a steady, non-grindy trickle
            // toward the 80-Glimmer cosmetic over normal daily play.
            if (reward.RewardGlimmer > 0)
                GlimmerCurrencyService.Instance?.TryAddGlimmer(reward.RewardGlimmer);

            // Mark claimed so a re-fire (e.g. a later Repaint) can't double-grant.
            q.ClaimedAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Debug.Log($"[DailyQuestTowerBridge] Quest '{q.TemplateId}' complete — " +
                      $"granted {reward.RewardCrystals} crystals, {reward.RewardWisdom} wisdom, " +
                      $"{reward.RewardGlimmer} glimmer.");
        }
    }
}
