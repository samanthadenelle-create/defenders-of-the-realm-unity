// =============================================================================
// DailyQuestCombatBridge — pipes WaveManager.OnWaveCleared into the
// DailyQuestService so combat-slot quest progress actually ticks during play.
// -----------------------------------------------------------------------------
// Lives on the WaveManager GameObject (added by VillageSceneBuilder, same
// place as WaveHudBridge). Same pattern: subscribe in OnEnable so we don't
// race the wave loop, unsubscribe in OnDisable so we don't double-fire after
// a scene reload.
// =============================================================================

using DeNelle.Core.Quests;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WaveManager))]
    public sealed class DailyQuestCombatBridge : MonoBehaviour
    {
        [SerializeField] private WaveManager _wave;

        private void Reset() => _wave = GetComponent<WaveManager>();

        private void OnEnable()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();
            if (_wave != null)
                _wave.OnWaveCleared.AddListener(HandleWaveCleared);
        }

        private void OnDisable()
        {
            if (_wave != null)
                _wave.OnWaveCleared.RemoveListener(HandleWaveCleared);
        }

        private void HandleWaveCleared(int waveNumber)
        {
            // The "clear N waves" template uses dot-prefix matching, so the
            // service ticks every combat quest whose template id starts with
            // "combat.clear-waves" — additional combat templates (frost-nova,
            // sword-strike, hawks-eye) will use their own event ids when the
            // corresponding ability hooks land.
            DailyQuestService.Instance?.Report("combat.clear-waves", 1);
        }
    }
}
