using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Village
{
    [RequireComponent(typeof(WaveManager))]
    public class WaveAccessLock : MonoBehaviour
    {
        [SerializeField] private WaveManager _wave;
        [SerializeField] private GameObject[] _lockedDuringWave;

        private void Reset() => _wave = GetComponent<WaveManager>();

        private void OnEnable()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();
            _wave.OnWaveStarted.AddListener(OnWaveStarted);
            _wave.OnWaveCleared.AddListener(OnWaveCleared);
        }

        private void OnDisable()
        {
            if (_wave == null) return;
            _wave.OnWaveStarted.RemoveListener(OnWaveStarted);
            _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
        }

        private void OnWaveStarted(int waveNumber)
        {
            foreach (var go in _lockedDuringWave)
                if (go != null) go.SetActive(false);
        }

        private void OnWaveCleared(int waveNumber)
        {
            foreach (var go in _lockedDuringWave)
                if (go != null) go.SetActive(true);
        }
    }
}
