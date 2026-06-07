// =============================================================================
// StartWaveHudBridge — wires the HUD "Defend!" button to WaveManager.
// -----------------------------------------------------------------------------
// OWNER ASK: "missing a button to start waves." The player previously had no
// way to begin the next wave on demand — waves only auto-started after the
// Prepare-Phase countdown, or via the owner-only AdminOverlay debug chord.
//
// This bridge gives the HUD's player-facing "DEFEND!" button a gameplay hook:
//   • Subscribes to VillageHudController.StartWaveRequested (UnityEvent) and
//     calls WaveManager.ForceBeginNextWave() — the SAME public entry the
//     AdminOverlay's "Trigger next wave" uses (skips the countdown / kicks the
//     loop from Idle / Complete).
//   • Pushes button availability each frame via SetStartWaveAvailable(bool):
//     VISIBLE between waves (Countdown / Idle / Complete), HIDDEN while a wave
//     is Active (or after Defeat) so the player can't double-trigger a live wave.
//
// CROSS-ASMDEF: DeNelle.Village must NOT reference DeNelle.HUD (HUD is Core-only).
// So — exactly like BuildMenuHudBridge — this resolves the HUD object via
// reflection. The HUD instance is obtained from CoreServices.Hud (IVillageHud,
// which lives in DeNelle.Core), and StartWaveRequested / SetStartWaveAvailable
// are reflected by name (they are HUD extras, not on the IVillageHud interface).
//
// SELF-WIRING: [RequireComponent(typeof(WaveManager))] + GetComponent — the
// DailyQuestCombatBridge pattern — so WaveSystemBridgeBootstrap can AddComponent
// this onto the WaveManager GO at runtime with no Village.unity re-save.
// =============================================================================

using System.Reflection;
using DeNelle.Core;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WaveManager))]
    public sealed class StartWaveHudBridge : MonoBehaviour
    {
        private WaveManager _wave;

        // The HUD object + reflected handles (resolved lazily once the HUD exists).
        private object _hud;
        private UnityEvent _startWaveRequestedEvent;
        private UnityAction _onStartWaveClicked;
        private MethodInfo _setAvailableMethod;

        private bool _lastPushedAvailable;
        private bool _everPushed;

        private void Awake()
        {
            _wave = GetComponent<WaveManager>();
        }

        private void OnDisable()
        {
            if (_startWaveRequestedEvent != null && _onStartWaveClicked != null)
                _startWaveRequestedEvent.RemoveListener(_onStartWaveClicked);
            _startWaveRequestedEvent = null;
            _hud = null;
            _setAvailableMethod = null;
            _everPushed = false;
        }

        private void Update()
        {
            // The HUD registers itself with CoreServices in Awake/Start; it may not
            // exist on this bridge's first frame, so resolve lazily + re-resolve if
            // the HUD instance changes (e.g. a scene reload across the breach loop).
            object hudNow = CoreServices.Hud as object;
            if (hudNow == null)
            {
                _hud = null;
                _startWaveRequestedEvent = null;
                _setAvailableMethod = null;
                _everPushed = false;
                return;
            }

            if (!ReferenceEquals(hudNow, _hud))
                Bind(hudNow);

            PushAvailability();
        }

        /// <summary>(Re)resolves the HUD's StartWaveRequested event + setter by name.</summary>
        private void Bind(object hud)
        {
            // Drop any old subscription before re-binding.
            if (_startWaveRequestedEvent != null && _onStartWaveClicked != null)
                _startWaveRequestedEvent.RemoveListener(_onStartWaveClicked);

            _hud = hud;
            _everPushed = false;

            var type = hud.GetType();

            var field = type.GetField("StartWaveRequested",
                BindingFlags.Public | BindingFlags.Instance);
            _startWaveRequestedEvent = field?.GetValue(hud) as UnityEvent;
            if (_startWaveRequestedEvent == null)
            {
                Debug.LogWarning("[StartWaveHudBridge] HUD.StartWaveRequested not found — " +
                                 "the Defend! button click will be silent.");
            }
            else
            {
                _onStartWaveClicked = OnStartWaveClicked;
                _startWaveRequestedEvent.AddListener(_onStartWaveClicked);
            }

            _setAvailableMethod = type.GetMethod("SetStartWaveAvailable",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(bool) }, null);
            if (_setAvailableMethod == null)
                Debug.LogWarning("[StartWaveHudBridge] HUD.SetStartWaveAvailable(bool) not found — " +
                                 "the Defend! button visibility won't be driven.");
        }

        private void OnStartWaveClicked()
        {
            if (_wave == null) return;
            _wave.ForceBeginNextWave();
        }

        /// <summary>
        /// Shows the button between waves (player can begin the next wave), hides it
        /// while a wave is live or the run has ended. Only pushes on change.
        /// </summary>
        private void PushAvailability()
        {
            if (_setAvailableMethod == null || _hud == null || _wave == null) return;

            bool available;
            switch (_wave.Phase)
            {
                case WavePhase.Countdown:
                case WavePhase.Idle:
                case WavePhase.Complete:
                    available = true;
                    break;
                default: // Active, Breached, Defeated
                    available = false;
                    break;
            }

            if (_everPushed && available == _lastPushedAvailable) return;
            _lastPushedAvailable = available;
            _everPushed = true;
            _setAvailableMethod.Invoke(_hud, new object[] { available });
        }
    }
}
