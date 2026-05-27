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
        [SerializeField] private int _baseWaveXp = 120;
        [SerializeField] private int _xpPerWave = 25;

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
