// =============================================================================
// IEquipTarget — mockable model seam over a GearLoadout (WO-434 Phase A).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Generalizes the shop's IShopEquipTarget: a future EquipVM equips/unequips and reads
// the equipped loadout of a TARGET (the hero OR a companion) through this seam, never
// naming GearLoadout. PURE C#: no UnityEngine UI types, so a fake drives the VM in
// EditMode with no scene (ARCHITECTURE_PRINCIPLES.md §2 / §2c).
//
// CONSOLIDATION NOTE (WO-434): IShopEquipTarget (DeNelle.Village.Hero) is LEFT UNTOUCHED.
// It is a tighter, shop-only contract (equipped names + mults + EquipById) and ShopPanel's
// private LoadoutEquipTarget already implements it against the panel, not a GearLoadout.
// Folding it into IEquipTarget would force ShopPanel's adapter to grow Unequip + def +
// identity members it does not use — a behaviour risk this additive phase avoids. The two
// coexist; a later phase may retire IShopEquipTarget once EquipVM owns the equip surface.
// =============================================================================

using System;

namespace DeNelle.Village.Hero
{
    /// <summary>
    /// Mockable equip surface for one party member's <see cref="GearLoadout"/>: equipped
    /// names + defs, equip / unequip by id, stat readouts, and a target-identity label so the
    /// VM can show whose loadout it is (hero / companion name + class).
    /// </summary>
    public interface IEquipTarget
    {
        // ── Identity ─────────────────────────────────────────────────────────────
        /// <summary>Display name of the wearer (hero or companion).</summary>
        string TargetName { get; }

        /// <summary>Class id of the wearer (knight/mage/ranger/cleric) — for fit filtering + the label.</summary>
        string TargetClass { get; }

        // ── Equipped state ─────────────────────────────────────────────────────────
        string EquippedWeaponName { get; }
        string EquippedArmorName  { get; }

        /// <summary>The equipped weapon def, or null when no weapon is equipped.</summary>
        WeaponDef EquippedWeapon { get; }

        /// <summary>The equipped armor def, or null when no armor is equipped.</summary>
        ArmorDef EquippedArmor { get; }

        /// <summary>Outgoing-damage multiplier from the equipped weapon (1f when none).</summary>
        float WeaponMult { get; }

        /// <summary>Fractional incoming-damage reduction from the equipped armor (0f when none).</summary>
        float ArmorDefense { get; }

        // ── Change notification ──────────────────────────────────────────────────────
        /// <summary>
        /// Raised when this target's equipped loadout changes (mirrors GearLoadout.OnGearChanged).
        /// WO-434 Phase B: lets InventoryVM / EquipVM re-render equipped marks + stats without
        /// re-pulling the model. Additive; a fake may leave it unraised.
        /// </summary>
        event Action EquipChanged;

        // ── Commands ────────────────────────────────────────────────────────────────
        void EquipWeaponById(string id);
        void EquipArmorById(string id);
        void UnequipWeapon();
        void UnequipArmor();
    }

    /// <summary>
    /// Concrete adapter exposing a <see cref="GearLoadout"/> as an <see cref="IEquipTarget"/>.
    /// All reads null-guard the loadout so a missing wearer reports an empty loadout (no weapon /
    /// 1.0 mult / 0 defense) rather than throwing. Optionally carries an explicit name/class label
    /// (companions); falls back to the loadout's GameObject name when none is supplied.
    /// </summary>
    public sealed class GearLoadoutEquipTarget : IEquipTarget, IDisposable
    {
        private readonly GearLoadout _loadout;
        private readonly string _name;
        private readonly string _class;

        public GearLoadoutEquipTarget(GearLoadout loadout, string targetName = null, string targetClass = null)
        {
            _loadout = loadout;
            _name = targetName;
            _class = targetClass;
            if (_loadout != null) _loadout.OnGearChanged += RaiseEquipChanged;
        }

        public event Action EquipChanged;
        private void RaiseEquipChanged() => EquipChanged?.Invoke();

        public string TargetName =>
            !string.IsNullOrEmpty(_name) ? _name
                : (_loadout != null ? _loadout.gameObject.name : "");

        public string TargetClass => _class ?? "";

        public string EquippedWeaponName => _loadout != null ? _loadout.EquippedWeapon?.name : null;
        public string EquippedArmorName  => _loadout != null ? _loadout.EquippedArmor?.name : null;

        public WeaponDef EquippedWeapon => _loadout != null ? _loadout.EquippedWeapon : null;
        public ArmorDef EquippedArmor   => _loadout != null ? _loadout.EquippedArmor : null;

        public float WeaponMult   => _loadout != null ? _loadout.WeaponMult : 1f;
        public float ArmorDefense => _loadout != null ? _loadout.ArmorDefense : 0f;

        public void EquipWeaponById(string id) { if (_loadout != null) _loadout.EquipWeaponById(id); }
        public void EquipArmorById(string id)  { if (_loadout != null) _loadout.EquipArmorById(id); }
        public void UnequipWeapon()            { if (_loadout != null) _loadout.UnequipWeapon(); }
        public void UnequipArmor()             { if (_loadout != null) _loadout.UnequipArmor(); }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_loadout != null) _loadout.OnGearChanged -= RaiseEquipChanged;
            EquipChanged = null;
        }
    }
}
