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
// which owns the whole live reward schema (crystals/food/wisdom/random-item) in
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

            // ⛔ THE LIVE SEAM (quest audit 2026-08-21) — this is the subscription that
            // actually ticks. See HandleStructurePlaced for why the one below it is dead.
            BuildModeController.StructurePlaced += HandleStructurePlaced;
        }

        private void OnDestroy()
        {
            BuildModeController.StructurePlaced -= HandleStructurePlaced;
            UnsubscribePlacement();
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// The id prefix every defensive tower carries. structures-catalog.json classifies
        /// them as <c>"type": "Tower"</c> and all five such rows are named <c>tower_*</c>
        /// (tower_ground_archer, tower_ballista, tower_siege_tower, tower_catapult,
        /// tower_arcane_spire). <c>arcane-tower</c> is deliberately EXCLUDED — the catalog
        /// types it as a Resource building, not a defence.
        /// <para>⚠ No runtime code reads that catalog's <c>type</c> field, so this prefix is
        /// the cheap runtime stand-in for it. That is only safe while the naming holds, so
        /// <c>QuestCompletabilityRegression</c> PINS the invariant: a future row typed Tower
        /// without this prefix fails the gate instead of silently never counting toward the
        /// day-1 quest.</para>
        /// </summary>
        private const string TowerIdPrefix = "tower_";

        /// <summary>
        /// ⛔ THE FIX (quest audit 2026-08-21). <c>combat.build-towers</c> is
        /// <c>day1Guaranteed</c>, and DailyQuests force-returns it for the combat slot on
        /// EVERY roll while Day1QuestDone is false — a latch set only on completion. So a
        /// tick that never arrives does not cost one day, it pins every player's combat slot
        /// to an uncompletable quest FOREVER. Dailies are how a new player earns extra
        /// resources, so this was the on-ramp, broken for everyone.
        /// <para>Root cause: the only subscription was <c>TowerPlacementSystem.OnTowerPlaced</c>,
        /// which fires solely from <c>PlaceTower</c> &lt;- <c>StartPlacing</c> &lt;- <c>BuildMenu</c> —
        /// and BuildMenu's guid is in NO scene and NO prefab, and is never AddComponent'ed.
        /// BuildModeController's own header calls TowerPlacementSystem/BuildMenu the LEGACY
        /// path. The bridge was listening to a door nobody walks through.</para>
        /// <para>The legacy subscription is KEPT rather than deleted: it is harmless (the two
        /// paths cannot both fire for one placement) and it costs nothing, whereas removing
        /// it would be a second, unrelated change riding along in a defect fix.</para>
        /// </summary>
        private void HandleStructurePlaced(string structureId)
        {
            if (string.IsNullOrEmpty(structureId)) return;
            if (!structureId.StartsWith(TowerIdPrefix, System.StringComparison.OrdinalIgnoreCase)) return;

            DeNelle.Core.Diagnostics.FlowTrace.Step("DailyQuest",
                $"tower placed ('{structureId}') -> reporting 1 tick to '{DailyQuestService.Day1QuestTemplateId}'.");
            DailyQuestService.Instance?.Report(DailyQuestService.Day1QuestTemplateId, 1);
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
