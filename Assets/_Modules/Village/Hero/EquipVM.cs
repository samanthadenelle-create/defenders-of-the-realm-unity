// =============================================================================
// EquipVM — the equipment / paperdoll screen's pure ViewModel (WO-434 Phase B).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// ALL equip-screen STATE + LOGIC lives here, reproduced (pure + testable) from
// EquipmentPanel so a later Phase C can rebind it to ONE VM:
//   • a portrait + character label (name / class / level) for the ACTIVE target,
//   • HP / MP / Damage / Defense stat bars from the equip target,
//   • the equipment slots the model supports today (mainhand weapon + chest armor),
//     each a SlotVM that may hold the equipped item,
//   • the OWNED items compatible with the selected slot (fit-by-class via the store),
//   • the party-target picker (hero + companions) preserved from EquipmentPanel,
//   • the commands SelectSlot / Equip / Unequip / Swap / SelectTarget / Close.
//
// PURE: NO UnityEngine UI types. Icons are KEYS (IconRole/IconName). Rounding uses
// System.Math, never UnityEngine.Mathf, so the VM is unit-testable with fake
// IInventoryStore / IEquipTarget and no scene (ARCHITECTURE_PRINCIPLES.md §2 / §2c;
// mirrors ShopVM / InventoryVM).
//
// Implements DeNelle.Core.UI.Mvvm.IPanelViewModel.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Hero
{
    /// <summary>One readable stat row: a label and its bar (HP / MP / Damage / Defense).</summary>
    public readonly struct EquipStat
    {
        public readonly string Label;
        public readonly BarVM Bar;

        public EquipStat(string label, BarVM bar)
        {
            Label = label;
            Bar = bar;
        }
    }

    public sealed class EquipVM : IPanelViewModel, IDisposable
    {
        // ── Slot keys (SlotVM.SlotKey) — weapon + off-hand + armor + WO-543 ring/amulet ──
        // We DELINEATE main-hand weapon (sword / 1H / 2H) from the OFF-HAND shield (owner
        // requirement): shields live only in the off-hand; the main-hand list excludes them.
        public const string SlotMainhand = "mainhand";
        public const string SlotOffHand  = "offhand";
        public const string SlotChest    = "chest";
        public const string SlotRing     = "ring";    // WO-543
        public const string SlotAmulet   = "amulet";  // WO-543

        // ── Icon role keys (mirror InventoryVM) ───────────────────────────────────────────
        public const string IconRoleWeapon = "weapon";
        public const string IconRoleArmor  = "armor";
        public const string IconRoleAccessory = "accessory";   // WO-543
        public const string IconRolePortrait = "portrait";

        private readonly IInventoryStore _store;
        private readonly IReadOnlyList<IEquipTarget> _targets;
        private readonly Action _onClose;

        private readonly List<Action> _unsubscribers = new List<Action>();
        private bool _disposed;

        private int _activeTargetIndex;
        private string _selectedSlotKey = SlotMainhand;

        private readonly List<SlotVM> _equipSlots = new List<SlotVM>();
        private readonly List<ItemVM> _compatible = new List<ItemVM>();
        private readonly List<EquipStat> _stats = new List<EquipStat>();

        public EquipVM(IInventoryStore store,
                       IReadOnlyList<IEquipTarget> targets,
                       Action onClose = null)
        {
            _store = store;
            _targets = targets ?? Array.Empty<IEquipTarget>();
            _onClose = onClose;

            if (_store != null)
            {
                Action h = OnModelChanged;
                _store.Changed += h;
                _unsubscribers.Add(() => _store.Changed -= h);
            }
            foreach (var t in _targets)
            {
                if (t == null) continue;
                var tt = t;
                Action h = OnModelChanged;
                tt.EquipChanged += h;
                _unsubscribers.Add(() => tt.EquipChanged -= h);
            }

            Rebuild();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title => "Equipment";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var u in _unsubscribers) u?.Invoke();
            _unsubscribers.Clear();
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        private IEquipTarget Active =>
            (_targets.Count > 0 && _activeTargetIndex >= 0 && _activeTargetIndex < _targets.Count)
                ? _targets[_activeTargetIndex] : null;

        /// <summary>Portrait icon keys for the active target (View resolves the Sprite).</summary>
        public (string IconRole, string IconName) Portrait =>
            (IconRolePortrait, Active != null ? (Active.TargetClass ?? "") : "");

        /// <summary>"Name — Class" for the active target (the panel's medallion label).</summary>
        public string CharacterLabel
        {
            get
            {
                var t = Active;
                if (t == null) return "No hero";
                string name = string.IsNullOrEmpty(t.TargetName) ? "Hero" : t.TargetName;
                string cls = string.IsNullOrEmpty(t.TargetClass) ? "" : Cap(t.TargetClass);
                return string.IsNullOrEmpty(cls) ? name : name + " — " + cls;
            }
        }

        /// <summary>HP / MP / Damage / Defense bars from the equip target. Never null.</summary>
        public IReadOnlyList<EquipStat> Stats => _stats;

        /// <summary>The equipment slots (mainhand / offhand / chest / ring / amulet), in order,
        /// each holding the equipped item or empty.</summary>
        public IReadOnlyList<SlotVM> EquipSlots => _equipSlots;

        /// <summary>Index of the selected slot within <see cref="EquipSlots"/>, or -1.</summary>
        public int SelectedSlotIndex
        {
            get
            {
                for (int i = 0; i < _equipSlots.Count; i++)
                    if (_equipSlots[i].SlotKey == _selectedSlotKey) return i;
                return -1;
            }
        }

        public string SelectedSlotKey => _selectedSlotKey;

        /// <summary>Owned items valid for the selected slot (fit by the active target's class). Never null.</summary>
        public IReadOnlyList<ItemVM> CompatibleItems => _compatible;

        /// <summary>The party-target chips (one per assignable member). Never null.</summary>
        public IReadOnlyList<string> TargetNames
        {
            get
            {
                var list = new List<string>(_targets.Count);
                foreach (var t in _targets)
                    list.Add(t == null ? "—" : (string.IsNullOrEmpty(t.TargetName) ? "Hero" : t.TargetName));
                return list;
            }
        }

        public int ActiveTargetIndex => _activeTargetIndex;

        public string Status { get; private set; }

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Select an equipment slot by index; rebuilds the compatible-items list.</summary>
        public void SelectSlot(int index)
        {
            if (index < 0 || index >= _equipSlots.Count) return;
            _selectedSlotKey = _equipSlots[index].SlotKey;
            RebuildCompatible();
            Raise();
        }

        /// <summary>Equip an owned item into the selected slot (routes by slot kind).</summary>
        public void Equip(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) { Status = "Select an item first."; Raise(); return; }
            var t = Active;
            if (t == null) { Status = "No hero to equip."; Raise(); return; }

            if (_selectedSlotKey == SlotMainhand) t.EquipWeaponById(itemId);
            else if (_selectedSlotKey == SlotOffHand) t.EquipOffHandById(itemId);
            else if (_selectedSlotKey == SlotChest) t.EquipArmorById(itemId);
            else if (_selectedSlotKey == SlotRing || _selectedSlotKey == SlotAmulet) t.EquipAccessoryById(itemId);
            else t.EquipArmorById(itemId);

            Status = "Equipped.";
            Rebuild();
            Raise();
        }

        /// <summary>Clear the selected slot on the active target.</summary>
        public void Unequip()
        {
            var t = Active;
            if (t == null) { Status = "No hero."; Raise(); return; }

            if (_selectedSlotKey == SlotMainhand) t.UnequipWeapon();
            else if (_selectedSlotKey == SlotOffHand) t.UnequipOffHand();
            else if (_selectedSlotKey == SlotChest) t.UnequipArmor();
            else if (_selectedSlotKey == SlotRing || _selectedSlotKey == SlotAmulet) t.UnequipAccessory(_selectedSlotKey);
            else t.UnequipArmor();

            Status = "Unequipped.";
            Rebuild();
            Raise();
        }

        /// <summary>Swap the selected slot to a different owned item (same routing as Equip).</summary>
        public void Swap(string itemId) => Equip(itemId);

        /// <summary>Switch the active party member; rebuilds slots + stats + compatible list.</summary>
        public void SelectTarget(int index)
        {
            if (index < 0 || index >= _targets.Count) return;
            if (index == _activeTargetIndex) return;
            _activeTargetIndex = index;
            Rebuild();
            Raise();
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }

        private void OnModelChanged()
        {
            if (_disposed) return;
            Rebuild();
            Changed?.Invoke();
        }

        // ── Build ─────────────────────────────────────────────────────────────────

        private void Rebuild()
        {
            BuildSlots();
            BuildStats();
            RebuildCompatible();
        }

        private void BuildSlots()
        {
            _equipSlots.Clear();
            var t = Active;

            ItemVM? weapon = null;
            if (t?.EquippedWeapon != null)
            {
                var w = t.EquippedWeapon;
                weapon = new ItemVM(w.id, string.IsNullOrEmpty(w.name) ? w.id : w.name,
                    IconRoleWeapon, w.id, 0, "gold", true, w.rarity, equipped: true);
            }
            _equipSlots.Add(new SlotVM(SlotMainhand, weapon, highlighted: _selectedSlotKey == SlotMainhand));

            // Off-hand (shield) — delineated from the main-hand weapon (owner requirement).
            ItemVM? offhand = null;
            if (t?.EquippedOffHand != null)
            {
                var o = t.EquippedOffHand;
                offhand = new ItemVM(o.id, string.IsNullOrEmpty(o.name) ? o.id : o.name,
                    IconRoleWeapon, o.id, 0, "gold", true, o.rarity, equipped: true);
            }
            _equipSlots.Add(new SlotVM(SlotOffHand, offhand, highlighted: _selectedSlotKey == SlotOffHand));

            ItemVM? armor = null;
            if (t?.EquippedArmor != null)
            {
                var a = t.EquippedArmor;
                armor = new ItemVM(a.id, string.IsNullOrEmpty(a.name) ? a.id : a.name,
                    IconRoleArmor, a.id, 0, "gold", true, a.rarity, equipped: true);
            }
            _equipSlots.Add(new SlotVM(SlotChest, armor, highlighted: _selectedSlotKey == SlotChest));

            // WO-543: ring + amulet accessory slots (below chest). Pure stat modifiers; the slot
            // renders the accessory's iconPath sprite (View resolves by id) or the emoji fallback.
            ItemVM? ring = null;
            if (t?.EquippedRing != null)
            {
                var r = t.EquippedRing;
                ring = new ItemVM(r.id, string.IsNullOrEmpty(r.name) ? r.id : r.name,
                    IconRoleAccessory, r.id, 0, "gold", true, r.rarity, equipped: true);
            }
            _equipSlots.Add(new SlotVM(SlotRing, ring, highlighted: _selectedSlotKey == SlotRing));

            ItemVM? amulet = null;
            if (t?.EquippedAmulet != null)
            {
                var m = t.EquippedAmulet;
                amulet = new ItemVM(m.id, string.IsNullOrEmpty(m.name) ? m.id : m.name,
                    IconRoleAccessory, m.id, 0, "gold", true, m.rarity, equipped: true);
            }
            _equipSlots.Add(new SlotVM(SlotAmulet, amulet, highlighted: _selectedSlotKey == SlotAmulet));
        }

        // Damage / Defense come straight from the loadout's applied stats (the EquipmentPanel
        // summary line). HP / MP are now LIVE (WO-436): the equip target exposes the wearer's
        // current/max HP + mana off its hero components, so the bars read real data and refresh
        // on equip / target-switch (the same Changed cadence — no per-frame poll). 0/0 from a
        // wearer with no live source degrades to an empty bar labelled "0 / 0".
        private void BuildStats()
        {
            _stats.Clear();
            var t = Active;

            float hp = t != null ? t.CurrentHealth : 0f;
            float hpMax = t != null ? t.MaxHealth : 0f;
            _stats.Add(new EquipStat("HP", new BarVM(SafeFill(hp, hpMax), Vital(hp, hpMax), "hp")));

            float mp = t != null ? t.CurrentMana : 0f;
            float mpMax = t != null ? t.MaxMana : 0f;
            _stats.Add(new EquipStat("MP", new BarVM(SafeFill(mp, mpMax), Vital(mp, mpMax), "mp")));

            float mult = t != null ? t.WeaponMult : 1f;
            int dmgPct = RoundToInt((mult - 1f) * 100f);
            // Normalize the damage bonus to a 0..1 bar across a +0..+100% range for display.
            float dmgFill = Clamp((mult - 1f), 0f, 1f);
            _stats.Add(new EquipStat("Damage", new BarVM(dmgFill, "+" + dmgPct + "%", "dmg")));

            float def = t != null ? t.ArmorDefense : 0f;
            int defPct = RoundToInt(Clamp(def, 0f, 0.9f) * 100f);
            _stats.Add(new EquipStat("Defense", new BarVM(Clamp(def, 0f, 0.9f), "+" + defPct + "%", "def")));
        }

        // Owned items valid for the selected slot, filtered by the active target's class — the
        // EquipmentPanel's per-target fit filter, but over OWNED gear (data gap closed). When the
        // store/target is missing the list is simply empty.
        private void RebuildCompatible()
        {
            _compatible.Clear();
            if (_store == null) return;
            var t = Active;
            string job = t != null ? t.TargetClass : null;

            if (_selectedSlotKey == SlotMainhand || _selectedSlotKey == SlotOffHand)
            {
                // Delineate hands: the OFF-HAND lists ONLY shields; the MAIN-HAND excludes
                // shields (sword / 1H / 2H only). The model's EnforceHandSlots still resolves
                // 2H↔off-hand conflicts on equip — this just keeps each list honest.
                bool offhand = _selectedSlotKey == SlotOffHand;
                string equippedId = offhand
                    ? (t?.EquippedOffHand != null ? t.EquippedOffHand.id : null)
                    : (t?.EquippedWeapon != null ? t.EquippedWeapon.id : null);
                foreach (var (w, qty) in _store.OwnedWeapons())
                {
                    if (w == null) continue;
                    if (offhand != w.IsOffHandItem) continue;   // shields ⇄ off-hand only
                    if (!string.IsNullOrEmpty(job) && !_store.WeaponFitsClass(w, job)) continue;
                    bool equipped = !string.IsNullOrEmpty(equippedId) &&
                                    string.Equals(equippedId, w.id, StringComparison.OrdinalIgnoreCase);
                    string name = string.IsNullOrEmpty(w.name) ? w.id : w.name;
                    _compatible.Add(new ItemVM(w.id, name + (qty > 1 ? " x" + qty : ""),
                        IconRoleWeapon, w.id, 0, "gold", true, w.rarity, equipped: equipped));
                }
            }
            else if (_selectedSlotKey == SlotRing || _selectedSlotKey == SlotAmulet)
            {
                // WO-543: accessory compatible list = catalog accessories whose slot matches and whose
                // req.level <= the wearer's level (job is "any" for v1 accessories). Catalog-sourced,
                // not owned-filtered, per the equip spec.
                int level = t != null ? t.TargetLevel : 1;
                string equippedId = _selectedSlotKey == SlotRing
                    ? (t?.EquippedRing != null ? t.EquippedRing.id : null)
                    : (t?.EquippedAmulet != null ? t.EquippedAmulet.id : null);
                foreach (var ac in _store.AccessoriesForSlot(_selectedSlotKey, level))
                {
                    if (ac == null) continue;
                    bool equipped = !string.IsNullOrEmpty(equippedId) &&
                                    string.Equals(equippedId, ac.id, StringComparison.OrdinalIgnoreCase);
                    string name = string.IsNullOrEmpty(ac.name) ? ac.id : ac.name;
                    _compatible.Add(new ItemVM(ac.id, name, IconRoleAccessory, ac.id, 0, "gold", true, ac.rarity, equipped: equipped));
                }
            }
            else
            {
                string equippedId = t?.EquippedArmor != null ? t.EquippedArmor.id : null;
                foreach (var (a, qty) in _store.OwnedArmor())
                {
                    if (a == null) continue;
                    if (!string.IsNullOrEmpty(job) && !_store.ArmorFitsClass(a, job)) continue;
                    bool equipped = !string.IsNullOrEmpty(equippedId) &&
                                    string.Equals(equippedId, a.id, StringComparison.OrdinalIgnoreCase);
                    string name = string.IsNullOrEmpty(a.name) ? a.id : a.name;
                    _compatible.Add(new ItemVM(a.id, name + (qty > 1 ? " x" + qty : ""),
                        IconRoleArmor, a.id, 0, "gold", true, a.rarity, equipped: equipped));
                }
            }
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        // ── Pure math (System.Math) ────────────────────────────────────────────────
        private static int RoundToInt(float f) => (int)Math.Floor(f + 0.5f);

        /// <summary>0..1 bar fill for a current/max pair, guarding divide-by-zero (max ≤ 0 → 0).</summary>
        private static float SafeFill(float cur, float max) =>
            max > 0f ? Clamp(cur / max, 0f, 1f) : 0f;

        /// <summary>Bar label like "120 / 200" for a vital pair (whole numbers).</summary>
        private static string Vital(float cur, float max) =>
            RoundToInt(cur) + " / " + RoundToInt(max);

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
