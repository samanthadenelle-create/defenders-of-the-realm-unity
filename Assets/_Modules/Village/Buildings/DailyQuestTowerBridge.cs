// =============================================================================
// DailyQuestTowerBridge — DEF-223. TOWER-PLACEMENT HOOK only (additive; no
// scene-builder / VillageSceneBuilder edits, mirrors the DailyQuestCombatBridge
// pattern):
//
//   subscribes to TowerPlacementSystem.OnTowerPlaced and reports one tick to the
//   DailyQuestService for the Day-1 "Build {target} defensive towers" quest
//   (template id "combat.build-towers", tracked 0/4 → 4/4). TowerPlacementSystem
//   is a scene singleton that may not exist until the player first opens build
//   mode, so the bridge watches TowerPlacementSystem.Instance and (re)subscribes
//   whenever it changes — surviving scene reloads without a hard scene-builder
//   dependency.
//
// REWARD DISPENSE MOVED OUT (WO-564): paying out a completed daily quest's slot
// reward now lives in the dedicated DailyQuestRewardBridge (DeNelle.Village.Quests),
// which owns the WHOLE reward schema (crystals/food/glimmer/wisdom/random-item) in
// one place. Keeping reward logic here meant food + random-item were silently
// dropped and two unrelated responsibilities shared a class. Exactly ONE listener
// now subscribes to DailyQuestService.QuestCompleted, so the ClaimedAtUnix latch
// is never raced. This bridge does only the tower-placement tick.
//
// Self-bootstraps via RuntimeInitializeOnLoadMethod into a DontDestroyOnLoad
// object, the same lifecycle the DailyQuestService / WisdomCurrencyService use.
// =============================================================================

using DeNelle.Core.Quests;
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

        private void OnDestroy()
        {
            UnsubscribePlacement();
            if (_instance == this) _instance = null;
        }

        // ── Tower-placement subscription (re-checked each frame) ────────────────

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
    }
}
