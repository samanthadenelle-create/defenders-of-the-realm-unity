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

        // State-OUT push (cooldown sweep + mana bar). The bridge previously only
        // forwarded HUD clicks INTO TryCast; nothing pushed HeroAbilities state
        // back to the HUD, so the mana bar and ability cooldown sweeps never
        // updated on cast (WO-07). VillageHudController.SetMana / SetAbilityCooldown
        // are resolved by reflection — DeNelle.Village cannot reference DeNelle.HUD
        // (same asmdef-isolation seam as the AbilityRequested wiring above).
        private MethodInfo _setMana;        // SetMana(float current, float max)
        private MethodInfo _setCooldown;    // SetAbilityCooldown(int slot, float remaining, float total)
        private readonly object[] _manaArgs = new object[2];
        private readonly object[] _cdArgs = new object[3];

        private void Awake()
        {
            _abilities = GetComponent<HeroAbilities>();
        }

        private void OnEnable()
        {
            if (_hud == null) return;

            // Resolve the state-out push methods first so they bind even if the
            // AbilityRequested click event is absent.
            _setMana = _hud.GetType().GetMethod("SetMana",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(float), typeof(float) }, null);
            _setCooldown = _hud.GetType().GetMethod("SetAbilityCooldown",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(float), typeof(float) }, null);

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

        // Pushes the live mana bar + per-slot cooldown sweep into the HUD every
        // frame (both animate continuously — regen + cooldown countdown). Cheap:
        // five cached-MethodInfo invokes/frame against the village HUD. Without
        // this the HUD mana/cooldown readouts stay frozen at their UXML defaults
        // even though HeroAbilities is tracking them correctly (WO-07 fix).
        private void Update()
        {
            if (_abilities == null || _hud == null) return;

            if (_setMana != null)
            {
                _manaArgs[0] = _abilities.Mana;
                _manaArgs[1] = _abilities.MaxMana;
                _setMana.Invoke(_hud, _manaArgs);
            }

            if (_setCooldown != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var slot = (AbilitySlot)i;
                    var def = AbilityCatalog.Find(_abilities.HeroClass, slot);
                    _cdArgs[0] = i;
                    _cdArgs[1] = _abilities.CooldownRemaining(slot);
                    _cdArgs[2] = def != null ? def.Cooldown : 0f;
                    _setCooldown.Invoke(_hud, _cdArgs);
                }
            }
        }

        private void OnAbilityClicked(int slotIndex)
        {
            if (_abilities == null) return;
            var slot = (AbilitySlot)Mathf.Clamp(slotIndex, 0, 3);
            _abilities.TryCast(slot);
        }
    }
}
