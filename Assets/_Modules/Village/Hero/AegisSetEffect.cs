// =============================================================================
// AegisSetEffect — WO-295 "Oathweld" ward, the Legendary Aegis-of-Elarion set bonus.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The legendary set (an Aegis per-class WEAPON + the Aegis ARMOR, both carrying
// setId "aegis" in weapons.json / armor.json) grants the "Oathweld" ward — the
// emotional + mechanical payoff of the Forgemasters' Saga: when the bearer of the
// full set takes damage, a portion of that damage is REFUNDED as HP to the Heart
// of Elarion. This ties the player's own survival to the win condition (the Heart
// is the lose condition; the Aegis bleeds the hero's pain back into the Tree).
//
// DESIGN / RECONCILE (no combat fork):
//   • The per-class WEAPON perk + bonus DEFENSE are folded into GearLoadout's
//     existing WeaponMult / ArmorDefense (the established damage / mitigation chain)
//     — this component does NOT duplicate the gear stat system.
//   • The ward itself is the one piece that needs a runtime hook: it listens to
//     HeroHealth.OnHealthChanged and, on any HP DECREASE while the set is active,
//     heals the Heart by (lost HP x ward fraction). HeroHealth already fires that
//     event, so no HeroHealth surgery is needed.
//
// GRACEFUL: self-attaches via GearLoadout.EnsureSetEffect(); when the set is not
// equipped the ward is inert (the OnHealthChanged handler early-outs), so a hero
// without the Aegis is completely unaffected. Null-guards every cross-ref.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>The Oathweld ward: refunds a portion of the hero's damage taken to the Heart
    /// while the full Aegis of Elarion set is equipped. Driven by GearLoadout.</summary>
    [DisallowMultipleComponent]
    public sealed class AegisSetEffect : MonoBehaviour
    {
        private GearLoadout   _gear;
        private HeroHealth    _health;
        private HeartController _heart;

        // Last observed hero HP — used to derive the per-event "damage taken" delta
        // (HeroHealth's OnHealthChanged reports current/max, not the hit amount).
        private float _lastHp = -1f;

        // True while wired so we don't double-subscribe across enable cycles.
        private bool _subscribed;

        /// <summary>True when the full Aegis set is equipped (ward active). For HUD / debug.</summary>
        public bool WardActive => _gear != null && _gear.AegisSetActive;

        private void Awake()
        {
            _gear   = GetComponent<GearLoadout>();
            _health = GetComponent<HeroHealth>();
        }

        private void OnEnable()  => TrySubscribe();
        private void OnDisable() => Unsubscribe();

        /// <summary>Called by GearLoadout after any equip change — (re)wires the health hook.</summary>
        public void Refresh() => TrySubscribe();

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (_health == null) _health = GetComponent<HeroHealth>();
            // HeroHealth is attached lazily by HeroHealthBootstrap a frame or two after
            // the hero spawns; if it isn't here yet, a later Refresh()/OnEnable retries.
            if (_health == null) return;
            _lastHp = _health.Hp;
            _health.OnHealthChanged += OnHeroHealthChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _health == null) return;
            _health.OnHealthChanged -= OnHeroHealthChanged;
            _subscribed = false;
        }

        private void OnHeroHealthChanged(float current, float max)
        {
            float prev = _lastHp;
            _lastHp = current;

            // Only react to a real DECREASE (damage). Heals / respawns raise HP and
            // must not pull from the Heart.
            float lost = prev - current;
            if (lost <= 0f) return;

            // Ward only while the full Aegis set is worn.
            if (_gear == null || !_gear.AegisSetActive) return;

            float refund = lost * Mathf.Max(0f, _gear.WardRefundFraction);
            if (refund <= 0f) return;

            var heart = ResolveHeart();
            if (heart == null) return;

            // Heal the Heart (SetHp-clamped internally; a no-op if the Heart is full).
            heart.Heal(refund);

            // A soft ward pulse on the Heart so the protection is VISIBLE (acceptance
            // criterion). VFXManager.Play is static + null-safe — absent manager = no-op.
            VFXManager.Play(VFXType.Impact_Heal, heart.transform.position + Vector3.up * 1.5f);
        }

        private HeartController ResolveHeart()
        {
            if (_heart != null) return _heart;
            _heart = FindAnyObjectByType<HeartController>();
            return _heart;
        }
    }
}
