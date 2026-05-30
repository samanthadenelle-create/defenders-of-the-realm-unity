// =============================================================================
// WaveHudBridge — wires WaveManager events to IVillageHud via CoreServices.
// -----------------------------------------------------------------------------
// WO-41 refactor: reflection removed. Previously held a plain UnityEngine.Object
// _hud field and resolved VillageHudController.SetWave(int, float) by reflection
// at runtime. Now uses CoreServices.Hud (IVillageHud) directly — no asmdef
// reference to DeNelle.HUD needed because IVillageHud lives in DeNelle.Core.
//
// The _hud serialised field is REMOVED. VillageSceneBuilder no longer needs to
// assign it (Step 6 removes that SetObjectField call). The _wave field is kept
// so the scene builder can still wire the WaveManager reference.
// =============================================================================

using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Forwards WaveManager countdown / wave-start events to
    /// <see cref="DeNelle.Core.HUD.IVillageHud"/> via <see cref="CoreServices.Hud"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveHudBridge : MonoBehaviour
    {
        [Tooltip("The scene's WaveManager. Bound by the village scene builder.")]
        [SerializeField] private WaveManager _wave;

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
            CoreServices.Hud?.SetWave(waveNumber);
            CoreServices.Hud?.SetCountdown(countdown);
        }
    }
}
