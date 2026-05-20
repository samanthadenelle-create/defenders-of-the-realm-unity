// =============================================================================
// HeroAbilitiesHudBridge — wires VillageHudController.AbilityRequested →
// HeroAbilities.TryCast. Same cross-asmdef reflection trick as WaveHudBridge
// + BuildMenuHudBridge.
// =============================================================================

using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeroAbilities))]
    public sealed class HeroAbilitiesHudBridge : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Object _hud;
        private HeroAbilities _abilities;
        private UnityEvent<int> _abilityRequestedEvent;
        private UnityAction<int> _onAbilityRequested;

        private void Awake()
        {
            _abilities = GetComponent<HeroAbilities>();
        }

        private void OnEnable()
        {
            if (_hud == null) return;
            var field = _hud.GetType().GetField("AbilityRequested",
                BindingFlags.Public | BindingFlags.Instance);
            _abilityRequestedEvent = field?.GetValue(_hud) as UnityEvent<int>;
            if (_abilityRequestedEvent == null)
            {
                Debug.LogWarning("[HeroAbilitiesHudBridge] VillageHudController.AbilityRequested " +
                                 "not found — HUD ability clicks will be silent.");
                return;
            }
            _onAbilityRequested = OnAbilityClicked;
            _abilityRequestedEvent.AddListener(_onAbilityRequested);
        }

        private void OnDisable()
        {
            if (_abilityRequestedEvent != null && _onAbilityRequested != null)
                _abilityRequestedEvent.RemoveListener(_onAbilityRequested);
        }

        private void OnAbilityClicked(int slotIndex)
        {
            if (_abilities == null) return;
            var slot = (AbilitySlot)Mathf.Clamp(slotIndex, 0, 3);
            _abilities.TryCast(slot);
        }
    }
}
