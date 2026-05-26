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
        private MethodInfo _setSlot;        // SetAbilitySlot(int slot, string key, string glyph, string name) — WO-36 visual
        private readonly object[] _manaArgs = new object[2];
        private readonly object[] _cdArgs = new object[3];
        private readonly object[] _slotArgs = new object[4];

        // WO-36 (visual half): the Q/W/E/R cells are built once showing the Mage
        // kit. Re-target them to the active hero's loadout whenever the class
        // changes (cheap to detect; cells never change mid-class). Cleared so the
        // first frame always pushes.
        private string _lastPushedClass = null;

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
            _setSlot = _hud.GetType().GetMethod("SetAbilitySlot",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(string), typeof(string), typeof(string) }, null);

            // Force a fresh per-class push on (re)enable — the HUD may have rebuilt
            // its cells (e.g. after a scene reload) back to the Mage defaults.
            _lastPushedClass = null;

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

            // WO-36 (visual half): re-target the Q/W/E/R cells to the active hero's
            // loadout whenever the class changes — the HUD builds them once showing
            // the Mage kit, so a Knight/Ranger would otherwise see Mage glyphs.
            PushClassLoadoutIfChanged();

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

        // WO-36 (visual half): push the active class's Q/W/E/R key + glyph + name
        // into the HUD cells, but only when the hero class actually changes (the
        // bar is otherwise static for the life of a class). Resolved via the same
        // AbilityCatalog the cooldown push already uses.
        private void PushClassLoadoutIfChanged()
        {
            if (_setSlot == null) return;

            string heroClass = _abilities.HeroClass;
            if (heroClass == _lastPushedClass) return;
            _lastPushedClass = heroClass;

            for (int i = 0; i < 4; i++)
            {
                var slot = (AbilitySlot)i;
                var def = AbilityCatalog.Find(heroClass, slot);

                string key = def != null && !string.IsNullOrEmpty(def.Key) ? def.Key : DefaultKey(i);
                string glyph = GlyphFor(def);
                string name = def != null ? def.Name : null;

                _slotArgs[0] = i;
                _slotArgs[1] = key;
                _slotArgs[2] = glyph;
                _slotArgs[3] = name;
                _setSlot.Invoke(_hud, _slotArgs);
            }
        }

        private static string DefaultKey(int slot)
        {
            switch (slot)
            {
                case 0: return "Q";
                case 1: return "W";
                case 2: return "E";
                default: return "R";
            }
        }

        // Pick a sensible per-slot glyph: prefer the catalog icon (abilities.json
        // supplies one per ability), else map by gameplay effect, else fall back to
        // the ability name's first letter so the bar is never blank.
        private static string GlyphFor(AbilityDef def)
        {
            if (def == null) return "?";
            if (!string.IsNullOrEmpty(def.Icon)) return def.Icon;

            switch (def.EffectEnum)
            {
                case AbilityEffect.Strike: return "⚔";
                case AbilityEffect.Snare: return "❄";
                case AbilityEffect.Aoe: return "✸";
                case AbilityEffect.Cleave: return "✦";
                case AbilityEffect.Heal: return "✚";
                case AbilityEffect.Meteor: return "☄";
                default: break;
            }

            return !string.IsNullOrEmpty(def.Name)
                ? def.Name.Substring(0, 1).ToUpperInvariant()
                : "?";
        }

        private void OnAbilityClicked(int slotIndex)
        {
            if (_abilities == null) return;
            var slot = (AbilitySlot)Mathf.Clamp(slotIndex, 0, 3);
            _abilities.TryCast(slot);
        }
    }
}
