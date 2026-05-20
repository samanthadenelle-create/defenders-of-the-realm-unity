// =============================================================================
// WaveHudBridge — wires WaveManager events to VillageHudController.SetWave.
// -----------------------------------------------------------------------------
// DeNelle.Village ↔ DeNelle.HUD: the HUD module references DeNelle.Core only;
// DeNelle.Village cannot reference DeNelle.HUD without breaking that contract.
// Same pattern as WallRepairHudBridge — the bridge talks to the HUD by
// reflection at runtime, so the asmdef graph stays acyclic.
//
// Wiring: VillageSceneBuilder adds this component to the WaveManager
// GameObject and assigns _wave + _hud. At Start the bridge subscribes to
// OnCountdownTick / OnWaveStarted and forwards the values to HUD.SetWave.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Forwards WaveManager countdown / wave-start events to
    /// VillageHudController.SetWave(int, float) via reflection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveHudBridge : MonoBehaviour
    {
        [Tooltip("The scene's WaveManager. Bound by the village scene builder.")]
        [SerializeField] private WaveManager _wave;

        [Tooltip("The VillageHudController instance — held as a plain Object because " +
                 "DeNelle.Village cannot reference DeNelle.HUD. Bound by the scene builder.")]
        [SerializeField] private UnityEngine.Object _hud;

        private MethodInfo _setWave; // VillageHudController.SetWave(int, float)

        // Subscribe in OnEnable, not Start — WaveManager.BeginLoop is async; if
        // we wait for Start the first OnCountdownTick can fire before our
        // listener attaches, leaving the HUD stuck at "Wave 1 / 0".
        private void OnEnable()
        {
            if (_wave == null)
            {
                Debug.LogWarning("[WaveHudBridge] No WaveManager assigned — wave HUD will not update.");
                return;
            }
            if (_hud == null)
            {
                Debug.LogWarning("[WaveHudBridge] No VillageHudController assigned — wave HUD will not update.");
                return;
            }

            if (_setWave == null)
            {
                _setWave = _hud.GetType().GetMethod("SetWave",
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(int), typeof(float) },
                    modifiers: null);
            }
            if (_setWave == null)
            {
                Debug.LogWarning("[WaveHudBridge] VillageHudController.SetWave(int, float) " +
                                 "not found via reflection — wave HUD will not update.");
                return;
            }

            _wave.OnCountdownTick.AddListener(OnCountdown);
            _wave.OnWaveStarted.AddListener(OnWaveStart);
            // Push an initial value so the timer doesn't stick at the UXML default.
            Forward(_wave.CurrentWaveId, 0f);
        }

        private void OnDisable()
        {
            if (_wave == null) return;
            _wave.OnCountdownTick.RemoveListener(OnCountdown);
            _wave.OnWaveStarted.RemoveListener(OnWaveStart);
        }

        private void OnCountdown(float secondsRemaining)
        {
            if (_wave == null) return;
            // During countdown the HUD shows next-wave number + countdown timer.
            Forward(_wave.CurrentWaveId + 1, secondsRemaining);
        }

        private void OnWaveStart(int waveId)
        {
            // Wave is live now — clear the countdown timer.
            Forward(waveId, 0f);
        }

        private void Forward(int waveNumber, float countdown)
        {
            try { _setWave?.Invoke(_hud, new object[] { waveNumber, countdown }); }
            catch (Exception ex) { Debug.LogWarning("[WaveHudBridge] HUD update failed: " + ex.Message); }
        }
    }
}
