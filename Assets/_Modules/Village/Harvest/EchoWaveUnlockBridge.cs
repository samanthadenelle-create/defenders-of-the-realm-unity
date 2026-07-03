// =============================================================================
// EchoWaveUnlockBridge -- routes WaveManager wave-clears into EchoService unlocks
// (ECHO_WORKFORCE_SPEC), mirroring WaveXpBridge's OnWaveCleared hook.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WaveXpBridge is BAKE-COUPLED to WaveManager (RequireComponent on the same baked
// GameObject). To stay scene-authoring-free (the Echo workforce self-bootstraps),
// this bridge instead lives on the persistent EchoService host and SUBSCRIBES to
// whatever WaveManager is live in the current scene -- re-subscribing when the scene
// (and its WaveManager) changes. Each wave clear drives EchoService.OnWaveCleared,
// which increments WavesCompleted and unlocks the next Echo every 5 waves (cap 4).
// =============================================================================
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Subscribes EchoService to the live WaveManager.OnWaveCleared, scene-robust.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoWaveUnlockBridge : MonoBehaviour
    {
        private WaveManager _subscribed;
        private float _nextScan;
        private const float ScanInterval = 1.0f;   // cheap periodic re-bind to the live WaveManager

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;

            // The current scene's WaveManager (null in non-wave scenes).
#if UNITY_2023_1_OR_NEWER
            var wm = Object.FindAnyObjectByType<WaveManager>();
#else
            var wm = Object.FindAnyObjectByType<WaveManager>();
#endif
            if (wm == _subscribed) return;   // already bound to this one (or both null)

            // Unbind the stale one (scene changed / destroyed).
            if (_subscribed != null)
                _subscribed.OnWaveCleared.RemoveListener(OnWaveCleared);

            _subscribed = wm;
            if (_subscribed != null)
            {
                _subscribed.OnWaveCleared.AddListener(OnWaveCleared);
                DeNelle.Core.Diagnostics.FlowTrace.Step("Echo",
                    "EchoWaveUnlockBridge: bound to the live WaveManager.OnWaveCleared.");
            }
        }

        private void OnWaveCleared(int waveNumber)
        {
            EchoService.Instance?.OnWaveCleared(waveNumber);
        }

        private void OnDestroy()
        {
            if (_subscribed != null)
                _subscribed.OnWaveCleared.RemoveListener(OnWaveCleared);
            _subscribed = null;
        }
    }
}
