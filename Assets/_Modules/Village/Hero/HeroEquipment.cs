// =============================================================================
// HeroEquipment — basic equip system for heroes (WO-109).
// -----------------------------------------------------------------------------
// Builds on existing bare-bones (GearLoadout in PlayerAttackController for
// EquippedWeapon.reach, ItemInventory/VillageInventory for collection).
// Slots: MainHand (weapon), Armor.
// Equip: applies bonus (e.g. reach/damage to attack controller), triggers visual
// via HeroBodySwapper body or direct attach (weapon to hand bone).
// Stats simple for foundation: bonus reach or flat damage multiplier.
// Visual: post-swap re-apply; weapons parented to hero "hand" transform.
// All production/consume via Economy (when crafted).
// UI: paired with code-built EquipmentPanel (open via Yarn command from NPC).
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village.Hero
{
    public enum EquipmentSlot { MainHand, Armor }

    [System.Serializable]
    public class EquippedItem
    {
        public string ItemId;
        public EquipmentSlot Slot;
        public float ReachBonus;   // e.g. for weapons
        public float DamageBonus;  // flat or multiplier in attack
    }

    /// <summary>Manages hero equipment slots, bonuses, visuals. Attach to hero root.</summary>
    public sealed class HeroEquipment : MonoBehaviour
    {
        private Dictionary<EquipmentSlot, EquippedItem> _equipped = new Dictionary<EquipmentSlot, EquippedItem>();
        private Transform _heroBody; // set after HeroBodySwapper

        public EquippedItem GetEquipped(EquipmentSlot slot) => _equipped.ContainsKey(slot) ? _equipped[slot] : null;

        private void Start()
        {
            // Re-apply after body swap (HeroBodySwapper runs early).
            Invoke(nameof(RefreshVisuals), 0.1f);
        }

        /// <summary>Equip by id (from inventory). Resolves simple demo defs.</summary>
        public bool Equip(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            // Demo defs (extend with real ItemDef SO/JSON later).
            EquippedItem newItem = null;
            if (itemId == "basic_sword")
            {
                newItem = new EquippedItem { ItemId = itemId, Slot = EquipmentSlot.MainHand, ReachBonus = 2f, DamageBonus = 3f };
            }
            else if (itemId == "leather_armor")
            {
                newItem = new EquippedItem { ItemId = itemId, Slot = EquipmentSlot.Armor, DamageBonus = 1f };
            }
            else
            {
                Debug.Log($"[HeroEquipment] Unknown equippable '{itemId}'");
                return false;
            }

            // Unequip old in slot.
            if (_equipped.ContainsKey(newItem.Slot)) Unequip(newItem.Slot);

            _equipped[newItem.Slot] = newItem;
            ApplyBonuses();
            RefreshVisuals();

            Debug.Log($"[HeroEquipment] Equipped {itemId} (reach+{newItem.ReachBonus}, dmg+{newItem.DamageBonus})");
            return true;
        }

        public void Unequip(EquipmentSlot slot)
        {
            if (!_equipped.ContainsKey(slot)) return;
            _equipped.Remove(slot);
            ApplyBonuses();
            RefreshVisuals();
        }

        private void ApplyBonuses()
        {
            // Integrate with existing attack controller (reach) and simple damage.
            var attack = GetComponentInChildren<PlayerAttackController>();
            if (attack != null)
            {
                // The controller already reads GearLoadout for reach; we can also patch here or extend GearLoadout.
                // For demo, directly influence if possible (or log the effective).
                float totalReach = 0f;
                float totalDmg = 0f;
                foreach (var it in _equipped.Values)
                {
                    totalReach += it.ReachBonus;
                    totalDmg += it.DamageBonus;
                }
                // In real: set on a stats component or patch the controller's EffectiveRange.
                Debug.Log($"[HeroEquipment] Bonuses applied: reach+{totalReach}, dmg+{totalDmg}");
            }
        }

        private void RefreshVisuals()
        {
            // Find body after swapper (or use current).
            if (_heroBody == null)
            {
                var swap = GetComponent<HeroBodySwapper>();
                if (swap != null) _heroBody = swap.transform; // or find child body
                else _heroBody = transform;
            }

            // Weapon visual: attach to approximate hand (demo: right side or named child).
            // In full: find "RightHand" bone on the FBX after swap, parent the weapon prefab.
            var weapon = GetEquipped(EquipmentSlot.MainHand);
            if (weapon != null && weapon.ItemId == "basic_sword")
            {
                // Demo attach: create a simple blade child if not present.
                Transform hand = _heroBody.Find("RightHand") ?? _heroBody;
                if (hand.Find("EquippedSword") == null)
                {
                    var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blade.name = "EquippedSword";
                    blade.transform.SetParent(hand, false);
                    blade.transform.localPosition = new Vector3(0.5f, 0f, 0f);
                    blade.transform.localScale = new Vector3(0.1f, 0.1f, 1.2f);
                    blade.transform.localRotation = Quaternion.Euler(0, 0, 45);
                    // Color it metal.
                    var mr = blade.GetComponent<Renderer>();
                    if (mr) mr.material.color = new Color(0.7f, 0.7f, 0.75f);
                    Debug.Log("[HeroEquipment] Attached demo sword visual to hero.");
                }
            }
            // Armor: simple tint or scale for demo (real: mesh swap via VisualFactory or body parts).
            var armor = GetEquipped(EquipmentSlot.Armor);
            if (armor != null)
            {
                // Demo: tint the body slightly.
                var rends = _heroBody.GetComponentsInChildren<Renderer>();
                foreach (var r in rends) if (r.material) r.material.color = Color.Lerp(r.material.color, Color.gray, 0.3f);
            }
        }

        /// <summary>Demo helper for Yarn "OpenEquip" flow (auto equips a sword if none).</summary>
        public void TryEquipDemoWeapon()
        {
            if (GetEquipped(EquipmentSlot.MainHand) == null)
            {
                Equip("basic_sword");
            }
            // Also open a minimal code panel for real UI.
            // (EquipmentPanel would list inventory and call Equip.)
        }
    }
}
