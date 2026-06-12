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
        private HeroHealth _health; // same GameObject — drives the HUD hero-HP bar
        private UnityEvent<int> _abilityRequestedEvent;
        private UnityAction<int> _onAbilityRequested;

        // State-OUT push (cooldown sweep + mana bar). The bridge previously only
        // forwarded HUD clicks INTO TryCast; nothing pushed HeroAbilities state
        // back to the HUD, so the mana bar and ability cooldown sweeps never
        // updated on cast (WO-07). VillageHudController.SetMana / SetAbilityCooldown
        // are resolved by reflection — DeNelle.Village cannot reference DeNelle.HUD
        // (same asmdef-isolation seam as the AbilityRequested wiring above).
        private MethodInfo _setMana;        // SetMana(float current, float max)
        private MethodInfo _setHeroHp;      // SetHeroHp(float current, float max)
        private MethodInfo _setCooldown;    // SetAbilityCooldown(int slot, float remaining, float total)
        private MethodInfo _setSlot;        // SetAbilitySlot(int slot, string key, string glyph, string name, string description) — WO-36 visual
        private MethodInfo _setSlotColor;   // SetAbilitySlot(int,string,string,string,string,string accentHex) — DEF blank-buttons (rune-disc tint)
        private readonly object[] _manaArgs = new object[2];
        private readonly object[] _heroHpArgs = new object[2];
        private readonly object[] _cdArgs = new object[3];
        private readonly object[] _slotArgs = new object[5];
        private readonly object[] _slotColorArgs = new object[6];

        // WO-36 (visual half): the Q/W/E/R cells are built once showing the Mage
        // kit. Re-target them to the active hero's loadout whenever the class
        // changes (cheap to detect; cells never change mid-class). Cleared so the
        // first frame always pushes.
        private string _lastPushedClass = null;

        private void Awake()
        {
            _abilities = GetComponent<HeroAbilities>();
            _health = GetComponent<HeroHealth>(); // optional — HUD HP bar stays full if absent
        }

        // True once the HUD reflection methods + AbilityRequested event are bound to the
        // current _hud. Reset when _hud is lost so a new HUD (scene reload) rebinds.
        private bool _hudBound;

        private void OnEnable()
        {
            // Force a fresh per-class push on (re)enable — the HUD may have rebuilt its
            // cells (e.g. after a scene reload) back to the Mage defaults.
            _lastPushedClass = null;
            EnsureHud();
        }

        // WO-428 / WO-421 — runtime HUD resolution. The serialized _hud is wired ONLY by
        // the edit-time VillageSceneBuilder, so in the castle hub / OuterWorld (scenes it
        // never builds) _hud is null and this whole bridge no-ops: the hero HP bar never
        // moves and the mana / cooldown / ability-slot pushes never land. Mirror the
        // self-resolving pattern of WaveHudBridge / ComboHudBridge / HeartHudBridge — find
        // the HUD by component-type name (DeNelle.Village cannot reference DeNelle.HUD) and
        // bind once. Re-binds if the HUD is lost and a new one streams in.
        private void EnsureHud()
        {
            if (_hud == null)
            {
                _hudBound = false; // lost/never-resolved — must (re)bind once a HUD is found
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                {
                    if (mb != null && mb.GetType().Name == "VillageHudController") { _hud = mb; break; }
                }
                if (_hud == null) return;
            }
            if (_hudBound) return;
            BindHud();
        }

        private void BindHud()
        {
            // Resolve the state-out push methods first so they bind even if the
            // AbilityRequested click event is absent.
            _setMana = _hud.GetType().GetMethod("SetMana",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(float), typeof(float) }, null);
            _setHeroHp = _hud.GetType().GetMethod("SetHeroHp",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(float), typeof(float) }, null);
            _setCooldown = _hud.GetType().GetMethod("SetAbilityCooldown",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(float), typeof(float) }, null);
            _setSlot = _hud.GetType().GetMethod("SetAbilitySlot",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(string), typeof(string), typeof(string), typeof(string) }, null);
            // DEF blank-buttons: the 6-arg overload also pushes the ability's accent
            // colour so the HUD tints the code-built rune disc per ability. Optional —
            // a HUD without it (older build) still gets symbols via the 5-arg path.
            _setSlotColor = _hud.GetType().GetMethod("SetAbilitySlot",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) }, null);
            if (_setSlot == null)
                Debug.LogWarning("[HeroAbilitiesHudBridge] VillageHudController.SetAbilitySlot" +
                                 "(int,string,string,string,string) not found via reflection — " +
                                 "the ability bar will keep its default Mage glyphs/names.");

            var field = _hud.GetType().GetField("AbilityRequested",
                BindingFlags.Public | BindingFlags.Instance);
            _abilityRequestedEvent = field?.GetValue(_hud) as UnityEvent<int>;
            if (_abilityRequestedEvent == null)
            {
                Debug.LogWarning("[HeroAbilitiesHudBridge] VillageHudController.AbilityRequested " +
                                 "not found — HUD ability clicks will be silent.");
                _hudBound = true; // HUD + push methods resolved; only the click event is absent
                return;
            }
            _onAbilityRequested = OnAbilityClicked;
            _abilityRequestedEvent.AddListener(_onAbilityRequested);
            _hudBound = true;
        }

        private void OnDisable()
        {
            if (_abilityRequestedEvent != null && _onAbilityRequested != null)
                _abilityRequestedEvent.RemoveListener(_onAbilityRequested);
            _hudBound = false; // rebind on next enable (the HUD may rebuild across scenes)
        }

        // Pushes the live mana bar + per-slot cooldown sweep into the HUD every
        // frame (both animate continuously — regen + cooldown countdown). Cheap:
        // five cached-MethodInfo invokes/frame against the village HUD. Without
        // this the HUD mana/cooldown readouts stay frozen at their UXML defaults
        // even though HeroAbilities is tracking them correctly (WO-07 fix).
        private void Update()
        {
            // Resolve/re-resolve the HUD at runtime if the serialized ref was absent
            // (castle/OuterWorld) or the HUD streamed in after enable.
            if (_hud == null || !_hudBound) EnsureHud();
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

            if (_setHeroHp != null && _health != null)
            {
                _heroHpArgs[0] = _health.Hp;
                _heroHpArgs[1] = _health.MaxHp;
                _setHeroHp.Invoke(_hud, _heroHpArgs);
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

            // Build a verifiable one-time log line of the class + the 4 names we push,
            // so the player log proves the per-class bar is wired (FAIL #2 verify).
            var pushed = new System.Text.StringBuilder();

            for (int i = 0; i < 4; i++)
            {
                var slot = (AbilitySlot)i;
                var def = AbilityCatalog.Find(heroClass, slot);

                string key = def != null && !string.IsNullOrEmpty(def.Key) ? def.Key : DefaultKey(i);
                string glyph = GlyphFor(def);
                string name = def != null ? def.Name : null;
                string description = DescriptionFor(def);
                string accentHex = AccentFor(def, i);

                if (_setSlotColor != null)
                {
                    _slotColorArgs[0] = i;
                    _slotColorArgs[1] = key;
                    _slotColorArgs[2] = glyph;
                    _slotColorArgs[3] = name;
                    _slotColorArgs[4] = description;
                    _slotColorArgs[5] = accentHex;
                    _setSlotColor.Invoke(_hud, _slotColorArgs);
                }
                else
                {
                    _slotArgs[0] = i;
                    _slotArgs[1] = key;
                    _slotArgs[2] = glyph;
                    _slotArgs[3] = name;
                    _slotArgs[4] = description;
                    _setSlot.Invoke(_hud, _slotArgs);
                }

                if (i > 0) pushed.Append(", ");
                pushed.Append(string.IsNullOrEmpty(name) ? "(none)" : name);
            }

            Debug.Log($"[HeroAbilitiesHudBridge] Pushed ability bar for class '{heroClass}': {pushed}");
        }

        // Compose a concise 1-line effect blurb for the slot tooltip. Built from the
        // AbilityDef gameplay fields (damage / cooldown / effect) so it reflects what
        // the ability actually does + its feel — the canonical abilities.json also
        // carries a 'description' field, but AbilityDef does not surface it, so we
        // derive an equivalent line here without touching the catalog type.
        private static string DescriptionFor(AbilityDef def)
        {
            if (def == null) return null;

            string action;
            switch (def.EffectEnum)
            {
                case AbilityEffect.Strike: action = "Strikes the nearest foe"; break;
                case AbilityEffect.Snare:  action = "Snares foes at range"; break;
                case AbilityEffect.Aoe:    action = "Bursts an area"; break;
                case AbilityEffect.Cleave: action = "Cleaves foes in front"; break;
                case AbilityEffect.Heal:   action = "Heals the Heart"; break;
                case AbilityEffect.Meteor: action = "Calls down a meteor"; break;
                default:                   action = "Casts"; break;
            }

            string amount = def.EffectEnum == AbilityEffect.Heal
                ? $"+{Mathf.RoundToInt(def.Damage)} HP"
                : $"{Mathf.RoundToInt(def.Damage)} dmg";

            return $"{action} — {amount} ({def.Cooldown:0.##}s cd)";
        }

        // The ability's accent colour hex (abilities.json 'color', e.g. "#b388ff")
        // used by the HUD to tint the code-built rune disc so each spell button reads
        // as a distinct coloured symbol (DEF blank-buttons fix). Falls back to a
        // per-slot element colour (Q arcane / W frost / E heal / R fire) so a def
        // missing its colour still gets a sensible tint.
        private static string AccentFor(AbilityDef def, int slot)
        {
            if (def != null && !string.IsNullOrEmpty(def.Color))
                return def.Color;

            switch (slot)
            {
                case 0: return "#b388ff";  // arcane violet
                case 1: return "#7dd3fc";  // frost blue
                case 2: return "#ffd27a";  // heal gold
                default: return "#ff7043"; // fire orange
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
