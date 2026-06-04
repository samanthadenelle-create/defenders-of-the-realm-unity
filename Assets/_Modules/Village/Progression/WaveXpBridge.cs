// =============================================================================
// WaveXpBridge — grants the hero a flat XP bonus when a wave is cleared.
// -----------------------------------------------------------------------------
// DEF-88: sits on the same GameObject as WaveManager and listens to
// OnWaveCleared. When the wave is won it hands the hero a lump of XP that
// scales with the wave number (_baseWaveXp + waveNumber * _xpPerWave), giving a
// meaningful reward for surviving a full wave on top of the per-kill XP.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    [RequireComponent(typeof(WaveManager))]
    public class WaveXpBridge : MonoBehaviour
    {
        [SerializeField] private WaveManager _wave;
        // DEF-133: halved from 120/25 — wave-clear lump was over-leveling the hero
        // before any base/structure investment ("crazy EXP"). Bake-coupled: this
        // value is auto-added (RequireComponent) onto WaveManager at build time, so
        // it takes effect on the next VillageSceneBuilder rebake. Trivially re-tunable.
        [SerializeField] private int _baseWaveXp = 60;
        [SerializeField] private int _xpPerWave = 12;

        private void Reset() => _wave = GetComponent<WaveManager>();

        private void OnEnable()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();
            _wave.OnWaveCleared.AddListener(OnWaveCleared);
        }

        private void OnDisable()
        {
            if (_wave != null) _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
        }

        private void OnWaveCleared(int waveNumber)
        {
            if (HeroProgression.Instance == null) return;
            HeroProgression.Instance.AddXp(_baseWaveXp + waveNumber * _xpPerWave);
        }
    }
}
