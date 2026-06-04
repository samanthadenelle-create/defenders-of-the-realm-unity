// =============================================================================
// GearLoadout — Gear v1 equip model on the hero (auto-equip best eligible).
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

            WeaponMult   = EquippedWeapon != null ? Mathf.Max(0.1f, EquippedWeapon.damageMult) : 1f;
            ArmorDefense = EquippedArmor  != null ? Mathf.Clamp(EquippedArmor.defense, 0f, 0.9f) : 0f;
        }
    }
}
