// =============================================================================
// InventoryVM — the inventory screen's pure ViewModel (WO-434 Phase B).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// ALL inventory STATE + LOGIC lives here, reproduced (pure + testable) from the two
// duplicate views (HeroInventoryController + EquipmentPanel) so a later Phase C can
// rebind them to ONE VM:
//   • OWNED items from the model (via the injected IInventoryStore seam) — CLOSES the
//     data gap: the panels listed class-eligible CATALOG gear; this lists what the
//     player actually OWNS (VillageInventory.Counts), projected by category.
//   • the four tabs Weapons / Armor / Outfits / Consumables (label + live count),
//   • a selected-item detail payload (name/desc/stats/stack + icon keys + rarity +
//     CanUse / CanEquip),
//   • the commands Select / SelectTab / Use / Drop / Equip / Close.
//
// WO-844: Use routes through an injected EFFECT seam (Func<string, InventoryUseResult>,
// default = BagConsumableUseEffect.Use -> ConsumableUseService.TryUse) so a Bag potion
// actually heals; the seam owns the decrement, the VM never consume-for-nothing.
//
// PURE: NO UnityEngine UI types (no GameObject/Image/Sprite/RectTransform/MonoBehaviour).
// Icons are carried as KEYS (IconRole = kind, IconName = id) — the View resolves the
// real Sprite. Rounding uses System.Math, never UnityEngine.Mathf, so the VM is unit-
// testable with a fake IInventoryStore / IEquipTarget and no scene
// (ARCHITECTURE_PRINCIPLES.md §2 / §2c; mirrors ShopVM).
//
// Implements DeNelle.Core.UI.Mvvm.IPanelViewModel: the View binds it, re-renders on
// Changed, routes input back as commands, and never reads game state.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;   // FlowTrace (WO-844: instrument the Use command)
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village.Crafting;   // VillageInventory (resolved in CreateDefault, the sole site)

namespace DeNelle.Village.Hero
{
    /// <summary>
    /// WO-844: result of routing a consumable through the effect authority (the seam the
    /// Bag's Use command calls). CONTRACT: when <see cref="Applied"/> is true the seam has
    /// BOTH applied the effect AND consumed the item from the underlying inventory (the
    /// way ConsumableUseService.TryUse does) - the VM never decrements on Use itself.
    /// When refused, nothing was consumed and <see cref="FailReason"/> carries the
    /// player-facing truth ("Already at full health." etc.).
    /// </summary>
    public readonly struct InventoryUseResult
    {
        public readonly bool Applied;
        public readonly string FailReason;

        private InventoryUseResult(bool applied, string failReason)
        {
            Applied = applied;
            FailReason = failReason;
        }

        public static InventoryUseResult Ok() => new InventoryUseResult(true, null);
        public static InventoryUseResult Refused(string reason) => new InventoryUseResult(false, reason);
    }

    /// <summary>The four inventory tabs (mirrors HeroInventoryController.Tab).</summary>
    public enum InventoryTabKind { Weapons, Armor, Outfits, Consumables }

    /// <summary>One tab's header chip: a label and the live owned-count in that category.</summary>
    public readonly struct InventoryTab
    {
        public readonly InventoryTabKind Kind;
        public readonly string Label;
        public readonly int Count;

        public InventoryTab(InventoryTabKind kind, string label, int count)
        {
            Kind = kind;
            Label = label;
            Count = count;
        }
    }

    /// <summary>
    /// Detail-pane payload for the selected owned item (icon keys + name/desc/stats/stack +
    /// rarity + what the player may do with it). Carried by the VM so the View renders the
    /// pane purely from data; the View resolves the Sprite from the icon keys, never re-pulling.
    /// </summary>
    public readonly struct InventoryDetail
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string Stats;
        public readonly int StackCount;
        public readonly string IconRole;     // "weapon" / "armor" / "potion"
        public readonly string IconName;     // item id (View resolves the Sprite)
        public readonly string Rarity;       // rarity key (frame escalation) or null
        public readonly bool CanUse;         // consumable -> Use is meaningful
        public readonly bool CanEquip;       // weapon/armor -> Equip is meaningful

        public InventoryDetail(string name, string description, string stats, int stackCount,
                               string iconRole, string iconName, string rarity,
                               bool canUse, bool canEquip)
        {
            Name = name;
            Description = description;
            Stats = stats;
            StackCount = stackCount;
            IconRole = iconRole;
            IconName = iconName;
            Rarity = rarity;
            CanUse = canUse;
            CanEquip = canEquip;
        }
    }

    public sealed class InventoryVM : IPanelViewModel, IDisposable
    {
        // ── Icon role keys (ItemVM.IconRole) — the View maps these to the real sprite source ──
        public const string IconRoleWeapon = "weapon";
        public const string IconRoleArmor  = "armor";
        public const string IconRolePotion = "potion";

        private readonly IInventoryStore _store;
        private readonly IEquipTarget _equip;        // may be null (no hero / tests)
        private readonly IEconomy _economy;          // wallet readout for the footer; may be null (tests)
        private readonly Action _onClose;            // View supplies how to dismiss; may be null

        // WO-844: the consumable EFFECT seam. The Bag's Use routes through THIS (the same
        // authority the battle potion slots use - ConsumableUseService.TryUse via the
        // default wiring in CreateDefault), never a bare store decrement. The seam owns
        // the item decrement on success (see InventoryUseResult contract), keeping the VM
        // fake-testable: tests inject a lambda, no scene / no static service.
        private readonly Func<string, InventoryUseResult> _useEffect;   // may be null (tests / no wiring)

        private readonly Action _storeHandler;
        private readonly Action _equipHandler;
        private bool _disposed;

        /// <summary>
        /// DI-in-Open factory (UI_MVVM_MIGRATION_PLAN §3): resolves the inventory model handles
        /// ITSELF — the live <see cref="VillageInventory"/> (wrapped in the returned store, so the
        /// View no longer reads VillageInventory.Instance) + the wallet economy (EconomyService.Instance,
        /// so the footer no longer reads GameStateService). The equip target stays View-supplied (it
        /// wraps the live hero loadout). Returns the built store via <paramref name="store"/> so the
        /// View keeps its handle to dispose.
        /// </summary>
        public static InventoryVM CreateDefault(IEquipTarget equip, Action onClose, out InventoryStore store)
        {
            // Mirror HeroInventoryController's WO-578 store build: UNION the active hero's equip
            // target with VillageInventory so OwnedWeapons/OwnedArmor agree with the Forge on "owned".
            store = new InventoryStore(VillageInventory.Instance,
                equip != null ? new List<IEquipTarget> { equip } : null);
            // WO-844: bind the REAL consumable effect authority (ConsumableUseService.TryUse
            // behind honest pre-gates) so drinking a potion from the Bag actually heals -
            // the same path the battle potion slots fire. Tests inject their own lambda.
            return new InventoryVM(store, equip, onClose, EconomyService.Instance,
                BagConsumableUseEffect.Use);
        }

        // Active list (owned items in the active tab) + per-id detail payload + kind.
        private readonly List<ItemVM> _slots = new List<ItemVM>();
        private readonly Dictionary<string, InventoryDetail> _details = new Dictionary<string, InventoryDetail>();
        private readonly Dictionary<string, InventoryTabKind> _slotKind = new Dictionary<string, InventoryTabKind>();

        private readonly List<InventoryTab> _tabs = new List<InventoryTab>();

        private InventoryTabKind _activeTab = InventoryTabKind.Weapons;

        public InventoryVM(IInventoryStore store,
                           IEquipTarget equip = null,
                           Action onClose = null,
                           IEconomy economy = null,
                           Func<string, InventoryUseResult> useEffect = null)
        {
            _store = store;
            _equip = equip;
            _economy = economy;
            _onClose = onClose;
            _useEffect = useEffect;

            if (_store != null)
            {
                _storeHandler = OnModelChanged;
                _store.Changed += _storeHandler;
            }
            if (_equip != null)
            {
                _equipHandler = OnModelChanged;
                _equip.EquipChanged += _equipHandler;
            }

            Rebuild();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title => "Inventory";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_store != null && _storeHandler != null) _store.Changed -= _storeHandler;
            if (_equip != null && _equipHandler != null) _equip.EquipChanged -= _equipHandler;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>Owned items in the active tab (never null). Equipped flag set from the equip target.</summary>
        public IReadOnlyList<ItemVM> Slots => _slots;

        /// <summary>The four tab chips (label + live owned count). Never null.</summary>
        public IReadOnlyList<InventoryTab> Tabs => _tabs;

        public int ActiveTabIndex => (int)_activeTab;

        public InventoryTabKind ActiveTab => _activeTab;

        public string SelectedId { get; private set; }

        /// <summary>Index of the selected slot within <see cref="Slots"/>, or -1 when none.</summary>
        public int SelectedSlotIndex
        {
            get
            {
                if (SelectedId == null) return -1;
                for (int i = 0; i < _slots.Count; i++)
                    if (_slots[i].Id == SelectedId) return i;
                return -1;
            }
        }

        /// <summary>The selected item's detail payload, or null when nothing is selected.</summary>
        public InventoryDetail? Selected =>
            SelectedId != null && _details.TryGetValue(SelectedId, out var d) ? d : (InventoryDetail?)null;

        public string Status { get; private set; }

        /// <summary>Live wallet readout for the footer (the View reads these instead of GameStateService).
        /// Sourced from the injected IEconomy, which reads GameState.Resources — the SAME values the
        /// footer showed before, so behaviour is unchanged. 0 when no economy is bound (tests).</summary>
        public int Coins    => _economy?.Coins ?? 0;
        public int Crystals => _economy?.Crystals ?? 0;

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Select the slot at <paramref name="index"/> (fills the detail pane).</summary>
        public void Select(int index)
        {
            // A fresh selection invalidates the previous command's result message (WO-585) so the
            // detail strip never shows a stale "Equipped X." against a newly-tapped item.
            Status = null;
            if (index < 0 || index >= _slots.Count) { SelectedId = null; Raise(); return; }
            SelectedId = _slots[index].Id;
            Raise();
        }

        /// <summary>Select by id (the View's per-cell tap); ignored when not in the active list.</summary>
        public void SelectById(string id)
        {
            if (string.IsNullOrEmpty(id) || !_details.ContainsKey(id)) return;
            Status = null;   // fresh selection clears the prior action's status (WO-585)
            SelectedId = id;
            Raise();
        }

        /// <summary>Switch tabs; rebuilds Slots and RESETS the selection (mirrors the panels' SelectTab).</summary>
        public void SelectTab(int index)
        {
            if (index < 0 || index > (int)InventoryTabKind.Consumables) return;
            var tab = (InventoryTabKind)index;
            if (tab == _activeTab) return;
            _activeTab = tab;
            SelectedId = null;
            Rebuild();
            Raise();
        }

        /// <summary>
        /// Use the selected consumable by routing it through the injected EFFECT seam - the
        /// same authority the battle potion slots fire (ConsumableUseService.TryUse via the
        /// CreateDefault wiring). WO-844: the old body called ONLY _store.TryRemove, so a
        /// potion vanished with ZERO effect applied. The seam OWNS the item decrement on
        /// success (TryUse consumes from the same VillageInventory this VM's store wraps),
        /// so this command must NOT also TryRemove - that would double-spend one drink.
        /// "Used X." is reported ONLY when the effect actually applied; a refusal keeps the
        /// item and surfaces the seam's truthful reason. No seam bound = no effect path =
        /// refuse honestly (never consume-for-nothing).
        /// </summary>
        public void Use()
        {
            var sel = Selected;
            if (sel == null) { Status = "Select an item first."; Raise(); return; }
            if (!sel.Value.CanUse) { Status = "That item cannot be used."; Raise(); return; }
            if (_store == null) { Status = "No inventory."; Raise(); return; }

            string id = SelectedId;
            if (_useEffect == null)
            {
                // Effect path missing: consuming an item with no effect IS the WO-844 bug.
                // Keep the item, tell the truth, and flag the wiring gap loudly.
                FlowTrace.Warn("Inventory", "Use '" + id + "' but NO effect seam bound - item kept, no effect applied.");
                Status = "Nothing happens.";
                Raise();
                return;
            }

            var result = _useEffect(id);
            FlowTrace.Step("Inventory", "Use '" + id + "' -> "
                + (result.Applied ? "APPLIED (seam consumed the item)" : "refused: " + (result.FailReason ?? "(no reason)")));

            if (result.Applied)
                Status = "Used " + sel.Value.Name + ".";
            else
                Status = string.IsNullOrEmpty(result.FailReason) ? "That had no effect." : result.FailReason;

            // On success the underlying inventory's Changed event rebuilds us; rebuild
            // defensively in case a fake seam does not raise it, then Raise either way.
            Rebuild();
            Raise();
        }

        /// <summary>Discard one of the selected owned item through the store.</summary>
        public void Drop()
        {
            var sel = Selected;
            if (sel == null) { Status = "Select an item first."; Raise(); return; }
            if (_store == null) { Status = "No inventory."; Raise(); return; }

            string name = sel.Value.Name;
            if (_store.TryRemove(SelectedId, 1))
                Status = "Dropped " + name + ".";
            else
                Status = "Nothing to drop.";
            Rebuild();
            Raise();
        }

        /// <summary>Route the selected weapon/armor to the equip target by its kind.</summary>
        public void Equip()
        {
            var sel = Selected;
            if (sel == null) { Status = "Select an item first."; Raise(); return; }
            if (!sel.Value.CanEquip) { Status = "That item cannot be equipped."; Raise(); return; }
            if (_equip == null) { Status = "No hero to equip."; Raise(); return; }

            if (_slotKind.TryGetValue(SelectedId, out var kind) && kind == InventoryTabKind.Weapons)
                _equip.EquipWeaponById(SelectedId);
            else
                _equip.EquipArmorById(SelectedId);

            Status = "Equipped " + sel.Value.Name + ".";
            Rebuild();   // re-mark equipped; equip target may also raise via INotifyEquipChanged
            Raise();
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }

        private void OnModelChanged()
        {
            if (_disposed) return;
            Rebuild();
            Changed?.Invoke();
        }

        // ── List + tab build ──────────────────────────────────────────────────────

        private void Rebuild()
        {
            _slots.Clear();
            _details.Clear();
            _slotKind.Clear();

            BuildTabs();

            if (_store == null) return;

            switch (_activeTab)
            {
                case InventoryTabKind.Weapons:     BuildWeapons();     break;
                case InventoryTabKind.Armor:       BuildArmor();       break;
                case InventoryTabKind.Outfits:     BuildOutfits();     break;
                case InventoryTabKind.Consumables: BuildConsumables(); break;
            }

            // If the previously-selected id is no longer present (used / dropped / tab change),
            // clear the selection so the detail pane never points at a vanished row.
            if (SelectedId != null && !_details.ContainsKey(SelectedId))
                SelectedId = null;
        }

        private void BuildTabs()
        {
            _tabs.Clear();
            int weapons = _store != null ? _store.OwnedWeapons().Count : 0;
            int armor   = _store != null ? _store.OwnedArmor().Count   : 0;
            int outfits = 0; // no per-player outfit model yet (cosmetics later) — mirror the empty tab.
            int cons    = _store != null ? _store.OwnedConsumables().Count : 0;
            _tabs.Add(new InventoryTab(InventoryTabKind.Weapons, "Weapons", weapons));
            _tabs.Add(new InventoryTab(InventoryTabKind.Armor, "Armor", armor));
            _tabs.Add(new InventoryTab(InventoryTabKind.Outfits, "Outfits", outfits));
            _tabs.Add(new InventoryTab(InventoryTabKind.Consumables, "Consumables", cons));
        }

        private void BuildWeapons()
        {
            string equippedId = _equip?.EquippedWeapon != null ? _equip.EquippedWeapon.id : null;
            foreach (var (w, qty) in _store.OwnedWeapons())
            {
                if (w == null) continue;
                bool equipped = !string.IsNullOrEmpty(equippedId) &&
                                string.Equals(equippedId, w.id, StringComparison.OrdinalIgnoreCase);
                // WO-808: stats read the LIVE leveled power (level 1 == authored, unchanged).
                int lvl = GearLevel(w.id);
                int dmgPct = RoundToInt((Max(0.1f, GearStatResolver.EffectiveDamageMult(w, lvl)) - 1f) * 100f);
                string stats = (lvl > 1 ? "Lv " + lvl + "   " : "")
                    + "+" + dmgPct + "% dmg" + (w.reach > 0f ? "   reach " + Fmt1(w.reach) + "m" : "");
                string name = string.IsNullOrEmpty(w.name) ? w.id : w.name;
                _details[w.id] = new InventoryDetail(name, DescribeGear(w.job, w.rarity), stats, qty,
                    IconRoleWeapon, w.id, w.rarity, canUse: false, canEquip: true);
                _slotKind[w.id] = InventoryTabKind.Weapons;
                _slots.Add(new ItemVM(w.id, name + (qty > 1 ? " x" + qty : ""), IconRoleWeapon, w.id,
                    0, "gold", true, w.rarity, equipped: equipped, level: lvl));
            }
        }

        private void BuildArmor()
        {
            string equippedId = _equip?.EquippedArmor != null ? _equip.EquippedArmor.id : null;
            foreach (var (a, qty) in _store.OwnedArmor())
            {
                if (a == null) continue;
                bool equipped = !string.IsNullOrEmpty(equippedId) &&
                                string.Equals(equippedId, a.id, StringComparison.OrdinalIgnoreCase);
                // WO-808: stats read the LIVE leveled power (level 1 == authored, unchanged).
                int lvl = GearLevel(a.id);
                int defPct = RoundToInt(GearStatResolver.EffectiveDefense(a, lvl) * 100f);
                string stats = (lvl > 1 ? "Lv " + lvl + "   " : "")
                    + "+" + defPct + "% def" + (a.hpBonus > 0f ? "   +" + Fmt1(a.hpBonus) + " hp" : "");
                string name = string.IsNullOrEmpty(a.name) ? a.id : a.name;
                _details[a.id] = new InventoryDetail(name, DescribeGear(a.job, a.rarity), stats, qty,
                    IconRoleArmor, a.id, a.rarity, canUse: false, canEquip: true);
                _slotKind[a.id] = InventoryTabKind.Armor;
                _slots.Add(new ItemVM(a.id, name + (qty > 1 ? " x" + qty : ""), IconRoleArmor, a.id,
                    0, "gold", true, a.rarity, equipped: equipped, level: lvl));
            }
        }

        /// <summary>WO-808: the owned instance's gear level (1 baseline) — VM-side state read.</summary>
        private static int GearLevel(string id) =>
            GearProgression.GearLevelOf(
                DeNelle.Core.State.GameStateService.Instance != null
                    ? DeNelle.Core.State.GameStateService.Instance.State : null, id);

        // No per-player OUTFIT/cosmetic ownership model exists yet (the panels showed none).
        // The tab is kept (label + zero count) so the surface matches; it lists nothing until
        // a cosmetics ownership list lands. This deliberately mirrors the views' empty Outfits.
        private void BuildOutfits()
        {
        }

        private void BuildConsumables()
        {
            foreach (var (id, qty) in _store.OwnedConsumables())
            {
                if (string.IsNullOrEmpty(id) || qty <= 0) continue;
                string name = id;
                _details[id] = new InventoryDetail(name, "Consumable you own.", "Consumable", qty,
                    IconRolePotion, id, null, canUse: true, canEquip: false);
                _slotKind[id] = InventoryTabKind.Consumables;
                _slots.Add(new ItemVM(id, name + (qty > 1 ? " x" + qty : ""), IconRolePotion, id,
                    0, "gold", true));
            }
        }

        // ── Description (mirrors ShopVM.DescribeGear) ─────────────────────────────
        private static string DescribeGear(string job, string rarity)
        {
            string r = string.IsNullOrEmpty(rarity) ? "" : char.ToUpper(rarity[0]) + (rarity.Length > 1 ? rarity.Substring(1) : "");
            string j = string.IsNullOrEmpty(job) || job == "any" ? "any class" : "the " + job;
            return (string.IsNullOrEmpty(r) ? "" : r + " gear. ") + "Suited to " + j + ".";
        }

        // ── Pure math (System.Math — no UnityEngine.Mathf, keeps the VM Unity-UI-free) ──
        private static int RoundToInt(float f) => (int)Math.Floor(f + 0.5f);
        private static float Max(float a, float b) => a > b ? a : b;
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        private static string Fmt1(float v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
