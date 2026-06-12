// =============================================================================
// GearLoadout — Gear v1 equip model on the hero (auto-equip best + manual shop/equip).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Attaches to the hero. Reads the hero's class (HeroAbilities.HeroClass) + level
// (HeroProgression.Level) and auto-equips the BEST weapon + armor the hero
// currently qualifies for from GearCatalog. Re-evaluates on every level-up.
//
// No equip UI yet (that's the art-gated layer) — v1 auto-equips so the power
// curve works immediately: level up -> qualify for a stronger weapon -> it
// equips -> every ability hits harder. Manual equip + loot drops layer on later.
//
// Exposes:
//   WeaponMult    — multiply the hero's outgoing damage (HeroAbilities reads it).
//   ArmorDefense  — fractional incoming-damage reduction (HeroHealth reads it).
//
// GRACEFUL: if no catalog / no eligible item, WeaponMult stays 1.0 and
// ArmorDefense stays 0 — existing combat is unchanged. HeroAbilities lazily
// adds this component, so it works on every hero with no builder/scene change.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class GearLoadout : MonoBehaviour
    {
        /// <summary>Outgoing-damage multiplier from the equipped weapon (1.0 = none).</summary>
        public float WeaponMult { get; private set; } = 1f;

        /// <summary>Fractional incoming-damage reduction from equipped armor (0 = none).</summary>
        public float ArmorDefense { get; private set; } = 0f;

        /// <summary>The currently-equipped pieces (null when nothing qualifies). For UI/debug.</summary>
        public WeaponDef EquippedWeapon { get; private set; }
        public ArmorDef  EquippedArmor  { get; private set; }

        /// <summary>Fired after any manual or auto equip change (shop/equip UI can subscribe to refresh lists or HUD).</summary>
        public event System.Action OnGearChanged;

        // ── WO-295: Legendary "Aegis of Elarion" set bonus ───────────────────────
        // The full Aegis set = an Aegis WEAPON (per-class) + the Aegis ARMOR, both
        // carrying setId "aegis". A full set grants:
        //   • the "Oathweld" ward — a portion of damage the hero takes is refunded
        //     as HP to the Heart (mechanically ties survival to the win condition).
        //     Driven by the lazily-attached AegisSetEffect component.
        //   • a per-class weapon perk, expressed as an extra damage multiplier folded
        //     into WeaponMult (so it flows through the existing damage chain without
        //     forking combat), plus a small bonus armour defense.
        // All gated on AegisSetEffect.SetActive; ordinary gear is untouched.

        /// <summary>True when a full Aegis set (Aegis weapon + Aegis armor) is equipped.</summary>
        public bool AegisSetActive =>
            EquippedWeapon != null && EquippedWeapon.IsAegis &&
            EquippedArmor  != null && EquippedArmor.IsAegis;

        /// <summary>Fraction of damage taken that the Oathweld ward refunds to the Heart (0 when no set).</summary>
        public float WardRefundFraction => AegisSetActive ? 0.25f : 0f;

        // Per-class Aegis weapon perk as a flat extra outgoing-damage multiplier,
        // folded into WeaponMult when the full set is active. Knight (shock combo) and
        // Mage (cost-down → leans on raw power up close) get the larger bump; Archer
        // (pierce/mark) and Cleric (heal-also-wards) a steadier one. Data-tunable later.
        private static float AegisWeaponPerkMult(string job)
        {
            switch ((job ?? string.Empty).ToLowerInvariant())
            {
                case "knight": return 1.15f;  // Emberbrand — stored-aether shock finisher
                case "mage":   return 1.15f;  // Aetherstaff — spells hit harder up close
                case "ranger": return 1.10f;  // Heartwood Longbow — pierce + mark
                case "cleric": return 1.10f;  // Hallowed Censer — heal-also-wards
                default:       return 1.0f;
            }
        }

        // Extra flat defense the Oathweld plating adds on top of the armor's own value
        // when the full set is worn (clamped with the base in Refresh/Equip*).
        private const float AegisSetDefenseBonus = 0.05f;

        private HeroAbilities   _abilities;
        private HeroProgression _progression;

        private void Awake()
        {
            _abilities   = GetComponent<HeroAbilities>();
            _progression = GetComponent<HeroProgression>();
        }

        private void OnEnable()
        {
            if (_progression != null) _progression.OnLevelUp += OnLevelUp;
            Refresh();
        }

        private void OnDisable()
        {
            if (_progression != null) _progression.OnLevelUp -= OnLevelUp;
        }

        private void OnLevelUp(int newLevel) => Refresh();

        /// <summary>Re-resolve class + level and equip the best eligible weapon + armor.</summary>
        public void Refresh()
        {
            if (_abilities == null)   _abilities   = GetComponent<HeroAbilities>();
            if (_progression == null) _progression = GetComponent<HeroProgression>();

            string job = _abilities != null ? _abilities.HeroClass : AbilityCatalog.DefaultClass;
            int level  = _progression != null ? _progression.Level : 1;

            EquippedWeapon = GearCatalog.BestWeapon(job, level);
            EquippedArmor  = GearCatalog.BestArmor(job, level);

            ApplyStats(job);
            OnGearChanged?.Invoke();
        }

        /// <summary>
        /// Recomputes WeaponMult + ArmorDefense from the equipped pieces, folding in the
        /// WO-295 Aegis full-set bonus (per-class weapon perk multiplier + bonus defense)
        /// when both an Aegis weapon and Aegis armor are equipped. Also (de)activates the
        /// Oathweld ward via the lazily-attached AegisSetEffect. Ordinary gear is unchanged.
        /// </summary>
        private void ApplyStats(string job)
        {
            float weapon = EquippedWeapon != null ? Mathf.Max(0.1f, EquippedWeapon.damageMult) : 1f;
            float armor  = EquippedArmor  != null ? Mathf.Clamp(EquippedArmor.defense, 0f, 0.9f) : 0f;

            if (AegisSetActive)
            {
                weapon *= AegisWeaponPerkMult(job);
                armor   = Mathf.Clamp(armor + AegisSetDefenseBonus, 0f, 0.9f);
            }

            WeaponMult   = weapon;
            ArmorDefense = armor;

            // Drive the body's armor visual tier off the equipped piece. EquipmentController
            // owns the asset-attach layer (the canonical instance); GearLoadout simply tells
            // it WHICH tier is worn. NOTE: SetArmorTier is presently a recorded NO-OP on the
            // visual side (armor body art not yet authored) — this wire makes the tier flow
            // so the body-attach lights up automatically the moment that art lands, with no
            // further plumbing. Weapon mesh attach is already driven via OnGearChanged.
            PushArmorTierToBody();

            // Activate / refresh the Oathweld ward driver (lazily attached, self-guards).
            EnsureSetEffect().Refresh();
        }

        private EquipmentController _equipment;

        // Map the equipped armor to a coarse visual tier (0 = none) and hand it to the
        // EquipmentController. Tier is derived from the existing rarity ladder so it needs
        // no new data: common=1 … legendary=5. Graceful: no armor or no controller = tier 0
        // / no-op (combat + existing visuals unchanged).
        private void PushArmorTierToBody()
        {
            if (_equipment == null) _equipment = GetComponent<EquipmentController>();
            if (_equipment == null) return;   // controller not attached on this hero — skip
            _equipment.SetArmorTier(ArmorVisualTier(EquippedArmor));
        }

        private static int ArmorVisualTier(ArmorDef a)
        {
            if (a == null) return 0;
            switch ((a.rarity ?? "common").ToLowerInvariant())
            {
                case "uncommon":  return 2;
                case "rare":      return 3;
                case "epic":      return 4;
                case "legendary": return 5;
                default:          return 1;   // common starter (e.g. Wanderer's Cloth)
            }
        }

        private AegisSetEffect _setEffect;

        /// <summary>Lazily attaches the Oathweld-ward driver so every hero gets it with no builder change.</summary>
        private AegisSetEffect EnsureSetEffect()
        {
            if (_setEffect == null)
            {
                if (!TryGetComponent(out _setEffect)) _setEffect = gameObject.AddComponent<AegisSetEffect>();
            }
            return _setEffect;
        }

        /// <summary>
        /// Manual equip support for shop / EquipmentPanel flows. Forces a specific piece
        /// the player has purchased (or crafted). Updates stats immediately and triggers
        /// visuals re-apply (sword on hand, bow on back, armor accents, knight shield).
        /// </summary>
        public void EquipWeaponById(string id)
        {
            var w = GearCatalog.FindWeapon(id);
            if (w == null) return;
            EquippedWeapon = w;
            ApplyStats(CurrentJob());
            OnGearChanged?.Invoke();
            TryReapplyVisuals();
        }

        public void EquipArmorById(string id)
        {
            var a = GearCatalog.FindArmor(id);
            if (a == null) return;
            EquippedArmor = a;
            ApplyStats(CurrentJob());
            OnGearChanged?.Invoke();
            TryReapplyVisuals();
        }

        /// <summary>The hero's current class id (for the Aegis per-class weapon perk).</summary>
        private string CurrentJob()
        {
            if (_abilities == null) _abilities = GetComponent<HeroAbilities>();
            return _abilities != null ? _abilities.HeroClass : AbilityCatalog.DefaultClass;
        }

        private void TryReapplyVisuals()
        {
            var body = transform.Find("HeroBody");
            if (body != null)
                GearVisualApplier.Apply(body, this);
        }
    }
}
