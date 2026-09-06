// =============================================================================
// IEquipTarget — mockable model seam over a GearLoadout (WO-434 Phase A).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Generalizes the shop's old IShopEquipTarget (deleted with ShopVM.cs on 2026-09-06,
// WO-1430 - see the consolidation note below): a future EquipVM equips/unequips and reads
// the equipped loadout of a TARGET (the hero OR a companion) through this seam, never
// naming GearLoadout. PURE C#: no UnityEngine UI types, so a fake drives the VM in
// EditMode with no scene (ARCHITECTURE_PRINCIPLES.md §2 / §2c).
//
// CONSOLIDATION NOTE (WO-434, RESOLVED 2026-09-06 by WO-1430): IShopEquipTarget was LEFT
// UNTOUCHED here as a tighter, shop-only contract (equipped names + mults + EquipById),
// implemented by ShopPanel's private LoadoutEquipTarget adapter. ⛔ IT NO LONGER EXISTS —
// it was declared inside the legacy ShopPanel/ShopVM pair, which was DELETED as doorless
// (no production file opened that panel). Do not go looking for it. IEquipTarget is now the
// ONE equip contract, and PartyShopVM consumes it directly, which is the consolidation this
// note said "a later phase" would reach.
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

        /// <summary>WO-543: the wearer's level — gates accessory eligibility (req.level). 1 when unknown.</summary>
        int TargetLevel { get; }

        // ── Equipped state ─────────────────────────────────────────────────────────
        string EquippedWeaponName { get; }
        string EquippedArmorName  { get; }

        /// <summary>The equipped weapon def, or null when no weapon is equipped.</summary>
        WeaponDef EquippedWeapon { get; }

        /// <summary>The equipped armor def, or null when no armor is equipped.</summary>
        ArmorDef EquippedArmor { get; }

        /// <summary>The equipped OFF-HAND (shield) def, or null when none.</summary>
        WeaponDef EquippedOffHand { get; }

        /// <summary>WO-543: the equipped RING accessory, or null.</summary>
        AccessoryDef EquippedRing { get; }

        /// <summary>WO-543: the equipped AMULET accessory, or null.</summary>
        AccessoryDef EquippedAmulet { get; }

        /// <summary>Outgoing-damage multiplier from the equipped weapon (1f when none).</summary>
        float WeaponMult { get; }

        /// <summary>Fractional incoming-damage reduction from the equipped armor (0f when none).</summary>
        float ArmorDefense { get; }

        // ── Live vitals (WO-436) ───────────────────────────────────────────────────
        // The wearer's CURRENT/MAX HP + mana, read live off the hero's components so the
        // equip panel's HP/MP bars show real data (not placeholders). Pure numbers — the
        // VM never names HeroHealth / HeroAbilities. 0/0 when the live source is missing
        // (e.g. a companion with no health/mana component, or a fake in EditMode) so the
        // VM degrades to an empty bar rather than throwing.
        /// <summary>Current hit points of the wearer (0 when no live source).</summary>
        float CurrentHealth { get; }
        /// <summary>Maximum hit points of the wearer (0 when no live source).</summary>
        float MaxHealth { get; }
        /// <summary>Current mana of the wearer (0 when no live source).</summary>
        float CurrentMana { get; }
        /// <summary>Maximum mana of the wearer (0 when no live source).</summary>
        float MaxMana { get; }

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

        // ── Off-hand (shield) commands ────────────────────────────────────────────────
        /// <summary>Equip an off-hand (shield) item by id.</summary>
        void EquipOffHandById(string id);
        /// <summary>Clear the off-hand (shield) slot.</summary>
        void UnequipOffHand();

        // ── Accessory commands (WO-543) ───────────────────────────────────────────────
        /// <summary>Equip a ring/amulet accessory by id (routes by the def's slot).</summary>
        void EquipAccessoryById(string id);
        /// <summary>Clear the accessory in <paramref name="slot"/> ("ring"/"amulet").</summary>
        void UnequipAccessory(string slot);
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

        // WO-436: live vitals come off the SAME hero GameObject the GearLoadout is on
        // (HeroAbilities lazily adds GearLoadout to its own GO, and HeroHealth attaches
        // there too — see HeroHealthBootstrap). Resolved lazily + cached; every read is
        // null-guarded so a wearer missing either component reports 0 rather than NRE.
        private HeroHealth _health;
        private HeroAbilities _abilities;
        private HeroProgression _progression;

        private HeroHealth Health
        {
            get
            {
                if (_health == null && _loadout != null) _health = _loadout.GetComponent<HeroHealth>();
                return _health;
            }
        }

        private HeroAbilities Abilities
        {
            get
            {
                if (_abilities == null && _loadout != null) _abilities = _loadout.GetComponent<HeroAbilities>();
                return _abilities;
            }
        }

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

        public int TargetLevel
        {
            get
            {
                if (_progression == null && _loadout != null) _progression = _loadout.GetComponent<HeroProgression>();
                return _progression != null ? _progression.Level : 1;
            }
        }

        public string EquippedWeaponName => _loadout != null ? _loadout.EquippedWeapon?.name : null;
        public string EquippedArmorName  => _loadout != null ? _loadout.EquippedArmor?.name : null;

        public WeaponDef EquippedWeapon => _loadout != null ? _loadout.EquippedWeapon : null;
        public ArmorDef EquippedArmor   => _loadout != null ? _loadout.EquippedArmor : null;
        public WeaponDef EquippedOffHand => _loadout != null ? _loadout.EquippedOffHand : null;

        public AccessoryDef EquippedRing   => _loadout != null ? _loadout.EquippedRing : null;
        public AccessoryDef EquippedAmulet => _loadout != null ? _loadout.EquippedAmulet : null;

        public float WeaponMult   => _loadout != null ? _loadout.WeaponMult : 1f;
        public float ArmorDefense => _loadout != null ? _loadout.ArmorDefense : 0f;

        // WO-436: live HP/MP readouts — null-safe (0 when the component is absent).
        public float CurrentHealth => Health != null ? Health.Hp      : 0f;
        public float MaxHealth     => Health != null ? Health.MaxHp   : 0f;
        public float CurrentMana   => Abilities != null ? Abilities.Mana    : 0f;
        public float MaxMana       => Abilities != null ? Abilities.MaxMana : 0f;

        public void EquipWeaponById(string id) { if (_loadout != null) _loadout.EquipWeaponById(id); }
        public void EquipArmorById(string id)  { if (_loadout != null) _loadout.EquipArmorById(id); }
        public void UnequipWeapon()            { if (_loadout != null) _loadout.UnequipWeapon(); }
        public void UnequipArmor()             { if (_loadout != null) _loadout.UnequipArmor(); }

        public void EquipOffHandById(string id) { if (_loadout != null) _loadout.EquipOffHandById(id); }
        public void UnequipOffHand()            { if (_loadout != null) _loadout.UnequipOffHand(); }

        public void EquipAccessoryById(string id)  { if (_loadout != null) _loadout.EquipAccessoryById(id); }
        public void UnequipAccessory(string slot)  { if (_loadout != null) _loadout.UnequipAccessory(slot); }

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
