// =============================================================================
// BuildMenuHudBridge — wires VillageHudController.BuildRequested → BuildMenu.Open.
// Same cross-asmdef reflection pattern as WaveHudBridge — DeNelle.Village
// cannot reference DeNelle.HUD without breaking the HUD's "Core-only" rule,
// so the bridge subscribes to the HUD event via reflection.
// =============================================================================

using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuildMenu))]
    public sealed class BuildMenuHudBridge : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Object _hud;
        private BuildMenu _menu;
        private UnityEvent _buildRequestedEvent;
        private UnityAction _onBuildClicked;

        private void Awake()
        {
            _menu = GetComponent<BuildMenu>();
        }

        private void OnEnable()
        {
            if (_hud == null) return;
            var field = _hud.GetType().GetField("BuildRequested",
                BindingFlags.Public | BindingFlags.Instance);
            _buildRequestedEvent = field?.GetValue(_hud) as UnityEvent;
            if (_buildRequestedEvent == null)
            {
                Debug.LogWarning("[BuildMenuHudBridge] VillageHudController.BuildRequested " +
                                 "not found — Build button click will be silent.");
                return;
            }
            _onBuildClicked = OnBuildClicked;
            _buildRequestedEvent.AddListener(_onBuildClicked);
        }

        private void OnDisable()
        {
            if (_buildRequestedEvent != null && _onBuildClicked != null)
                _buildRequestedEvent.RemoveListener(_onBuildClicked);
        }

        private void OnBuildClicked()
        {
            if (_menu == null) return;
            _menu.Open();
        }
    }
}
