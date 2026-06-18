// =============================================================================
// PartyShopVM — the PARTY weapon/armor shop's pure ViewModel (docs/STORE_EQUIP_SPEC.md).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// THE COMPLETE store-to-spec ViewModel that replaces the broken ShopVM/ShopPanel
// experience (two sell bars, no party selection, blank icons). It composes the
// PROVEN MVVM seams already in the repo — IEconomy (wallet/spend), IInventoryStore
// (owned set + gear defs + fit-by-class), and IEquipTarget (per-member loadout +
// equip/unequip) — into ONE unified shop:
//
//   1. PARTY SELECTOR. Holds the party (hero + companions) as IEquipTarget members
//      (id/name/class/portrait). SelectMember(i) re-filters. Default = the active hero.
//   2. TAP → FILTER. The row list = gear the SELECTED member can equip: weapons by
//      `job` (WeaponFitsClass); armor by `ArmorFitsClass` (weight/class); both by
//      `req.level <= member level`; the store TYPE (weapon shop / armor shop) is the
//      VendorStockContract gate over the catalog.
//   3. ONE action per row, SINGLE TAP. BUY (spend + add to inventory + auto-equip to
//      the selected member), or EQUIP (already owned), or SELL (in the SAME screen —
//      a Sell tab; selling owned gear credits coins so the player can afford new gear
//      WITHOUT leaving). EXACTLY one buy button per row; NO duplicate buy/sell bars.
//   4. REAL ITEM IMAGE. Each row carries the iconPath (the rendered sprite key); the
//      View resolves the actual sprite (fallback to a category glyph if null).
//   5. DETAILS + BUFFS. Each row carries a stat line + the delta vs the member's
//      currently-equipped piece ("+X% def", "+Y reach", "= equipped") so the player
//      makes an informed decision.
//
// PURE: NO UnityEngine UI types (no GameObject/Image/Sprite/RectTransform/Color).
// Icons are carried as KEYS on ItemVM (IconRole/IconName) + a parallel detail map; the
// View resolves the real sprite. Math uses System.Math, never UnityEngine.Mathf, so the
// VM is unit-testable without a scene (ARCHITECTURE_PRINCIPLES.md §2 / §2c; ui-mvvm-binding-seam).
//
// Implements DeNelle.Core.UI.Mvvm.IPanelViewModel: the View binds it, re-renders on
// Changed, routes input back as commands, and never reads game state.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village.Crafting;   // VillageInventory (for the buy -> add path through the store seam)

namespace DeNelle.Village.Hero
{
    /// <summary>The two shop screens: BUY (catalog the member can equip) + SELL (owned gear, for coins).</summary>
    public enum PartyShopTab { Buy, Sell }

    /// <summary>
    /// Detail payload for one shop row — the stat line + the "why it's better" delta vs the
    /// selected member's currently-equipped piece, plus the icon KEYS the View resolves to art.
    /// Carried by the VM so the View renders the row purely from data (no state-pull).
    /// </summary>
    public readonly struct PartyShopDetail
    {
        /// <summary>Stat line, e.g. "+18% dmg   reach 2.5m" or "+12% def   +30 hp".</summary>
        public readonly string Stats;
        /// <summary>Delta vs the member's equipped piece, e.g. "+6% dmg vs equipped" / "= equipped" / "".</summary>
        public readonly string Delta;
        /// <summary>One-line description (rarity + class fit + flavour).</summary>
        public readonly string Description;
        /// <summary>iconPath (the rendered item sprite key), or null — the View falls back to a category glyph.</summary>
        public readonly string IconPath;
        /// <summary>Coarse icon role for the View's glyph fallback ("weapon" / "armor").</summary>
        public readonly string IconRole;
        /// <summary>Item id (the View resolves the catalog sprite from this when IconPath is null).</summary>
        public readonly string IconName;

        public PartyShopDetail(string stats, string delta, string description,
                               string iconPath, string iconRole, string iconName)
        {
            Stats = stats;
            Delta = delta;
            Description = description;
            IconPath = iconPath;
            IconRole = iconRole;
            IconName = iconName;
        }
    }

    /// <summary>One party member chip — id/name/class for the selector, portrait key for the View.</summary>
    public readonly struct PartyMemberVM
    {
        public readonly string Name;
        public readonly string Class;        // knight/mage/ranger/cleric (lowercase)
        public readonly bool Selected;
        /// <summary>Portrait icon role (always "portrait"); the View maps Class -> a portrait/crest sprite.</summary>
        public readonly string IconRole;

        public PartyMemberVM(string name, string cls, bool selected)
        {
            Name = name;
            Class = cls ?? "";
            Selected = selected;
            IconRole = "portrait";
        }
    }

    public sealed class PartyShopVM : IPanelViewModel, IDisposable
    {
        // ── Icon role keys (ItemVM.IconRole) — the View maps these to the real sprite source ──
        public const string IconRoleWeapon = "weapon";
        public const string IconRoleArmor  = "armor";

        private readonly string _vendorContext;
        private readonly string _displayName;
        private readonly IEconomy _economy;
        private readonly IInventoryStore _store;
        private readonly IReadOnlyList<IEquipTarget> _members;   // hero + companions (never null)
        private readonly IReadOnlyList<int> _memberLevels;       // parallel to _members (member level for req gate)
        private readonly Action _onClose;

        private readonly Action<ResourceSnapshot> _ecoHandler;
        private readonly List<Action> _unsubscribers = new List<Action>();
        private bool _disposed;

        private int _selectedMember;                 // index into _members (default = active hero = 0)
        private PartyShopTab _tab = PartyShopTab.Buy;

        // The per-row action keyed by item id (armed on rebuild), plus the detail payload per id.
        private readonly Dictionary<string, Action> _rowActions = new Dictionary<string, Action>();
        private readonly Dictionary<string, PartyShopDetail> _rowDetails = new Dictionary<string, PartyShopDetail>();

        private readonly List<ItemVM> _items = new List<ItemVM>();
        private readonly List<(string id, GearKind kind)> _currentStock = new List<(string id, GearKind kind)>();

        // The store TYPE this vendor sells (weapon shop -> Weapon, armor shop -> Armor), from the contract.
        private readonly GearKind _storeKinds;

        public PartyShopVM(string vendorContext,
                           IEconomy economy,
                           IInventoryStore store,
                           IReadOnlyList<IEquipTarget> members,
                           IReadOnlyList<int> memberLevels,
                           string displayName = null,
                           Action onClose = null)
        {
            _vendorContext = vendorContext ?? "";
            _displayName = displayName;
            _economy = economy;
            _store = store;
            _members = members ?? Array.Empty<IEquipTarget>();
            _memberLevels = memberLevels ?? Array.Empty<int>();
            _onClose = onClose;

            _storeKinds = VendorStockContract.AllowedFor(_vendorContext.ToLowerInvariant());
            // A party gear shop only deals weapons/armor (potions are general-goods). If the
            // contract resolved to potions-only (or nothing useful), fall back to BOTH gear kinds
            // so a mis-tagged vendor still shows wearable gear rather than an empty screen.
            _storeKinds &= (GearKind.Weapon | GearKind.Armor);
            if (_storeKinds == GearKind.None) _storeKinds = GearKind.Weapon | GearKind.Armor;

            if (_economy != null)
            {
                _ecoHandler = _ => Raise();
                _economy.OnChanged += _ecoHandler;
            }
            if (_store != null)
            {
                Action h = OnModelChanged;
                _store.Changed += h;
                _unsubscribers.Add(() => _store.Changed -= h);
            }
            foreach (var m in _members)
            {
                if (m == null) continue;
                var mm = m;
                Action h = OnModelChanged;
                mm.EquipChanged += h;
                _unsubscribers.Add(() => mm.EquipChanged -= h);
            }

            Rebuild();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title => ResolveTitle();

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_economy != null && _ecoHandler != null) _economy.OnChanged -= _ecoHandler;
            foreach (var u in _unsubscribers) u?.Invoke();
            _unsubscribers.Clear();
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        public PartyShopTab Tab => _tab;

        /// <summary>The party-member chips (one per member; the selected one flagged). Never null.</summary>
        public IReadOnlyList<PartyMemberVM> Party
        {
            get
            {
                var list = new List<PartyMemberVM>(_members.Count);
                for (int i = 0; i < _members.Count; i++)
                {
                    var m = _members[i];
                    string name = m == null || string.IsNullOrEmpty(m.TargetName) ? "Hero" : m.TargetName;
                    string cls = m != null ? (m.TargetClass ?? "") : "";
                    list.Add(new PartyMemberVM(name, cls, i == _selectedMember));
                }
                return list;
            }
        }

        public int SelectedMemberIndex => _selectedMember;

        /// <summary>"Name — Class (Lv N)" for the selected member (the panel's sub-header).</summary>
        public string MemberLabel
        {
            get
            {
                var m = SelectedMember;
                if (m == null) return "No hero";
                string name = string.IsNullOrEmpty(m.TargetName) ? "Hero" : m.TargetName;
                string cls = string.IsNullOrEmpty(m.TargetClass) ? "" : Cap(m.TargetClass);
                string lv = " (Lv " + SelectedLevel + ")";
                return string.IsNullOrEmpty(cls) ? name + lv : name + " — " + cls + lv;
            }
        }

        /// <summary>The active tab's rows (affordability + owned/equipped already computed). Never null.</summary>
        public IReadOnlyList<ItemVM> Items => _items;

        public string SelectedId { get; private set; }

        /// <summary>The selected row's detail payload, or null when nothing is selected.</summary>
        public PartyShopDetail? Selected =>
            SelectedId != null && _rowDetails.TryGetValue(SelectedId, out var d) ? d : (PartyShopDetail?)null;

        /// <summary>Detail payload for any row id (View renders the per-row stats/delta from this). Null when absent.</summary>
        public PartyShopDetail? DetailFor(string id) =>
            id != null && _rowDetails.TryGetValue(id, out var d) ? d : (PartyShopDetail?)null;

        public string Status { get; private set; }

        /// <summary>Live wallet readout (the View rebuilds its "Gold: …" line from these).</summary>
        public int Coins    => _economy?.Coins ?? 0;
        public int Wood     => _economy?.Wood ?? 0;
        public int Iron     => _economy?.Iron ?? 0;
        public int Food     => _economy?.Food ?? 0;
        public int Crystals => _economy?.Crystals ?? 0;

        /// <summary>The store's ACTUAL built stock (id + category) — for an AutoPilot bot assertion.</summary>
        public IReadOnlyList<(string id, GearKind kind)> CurrentStock => _currentStock;

        public string VendorContext => _vendorContext;

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Select a party member by index → re-filter the list FOR that member.</summary>
        public void SelectMember(int index)
        {
            if (index < 0 || index >= _members.Count) return;
            if (index == _selectedMember) return;
            _selectedMember = index;
            SelectedId = null;
            Rebuild();
            Raise();
        }

        /// <summary>Switch BUY ↔ SELL (both live on the same screen — no leaving to sell).</summary>
        public void SetTab(PartyShopTab tab)
        {
            if (tab == _tab) return;
            _tab = tab;
            SelectedId = null;
            Rebuild();
            Raise();
        }

        /// <summary>Inspect a row (holds it as selected so the View can show its details).</summary>
        public void Select(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            SelectedId = id;
            Raise();
        }

        /// <summary>
        /// The single-tap action for a row: BUY (if not owned) / EQUIP (if owned) on the BUY tab,
        /// or SELL on the SELL tab. Selecting the row first arms it; this fires its armed action.
        /// </summary>
        public void Act(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            SelectedId = id;
            if (_rowActions.TryGetValue(id, out var act) && act != null) act();
            else Status = "Nothing to do for that item.";
            Raise();
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }

        private void OnModelChanged()
        {
            if (_disposed) return;
            Rebuild();
            Changed?.Invoke();
        }

        // ── Selected-member helpers ──────────────────────────────────────────────

        private IEquipTarget SelectedMember =>
            (_members.Count > 0 && _selectedMember >= 0 && _selectedMember < _members.Count)
                ? _members[_selectedMember] : null;

        private int SelectedLevel =>
            (_selectedMember >= 0 && _selectedMember < _memberLevels.Count) ? _memberLevels[_selectedMember] : 1;

        private string SelectedJob => SelectedMember != null ? SelectedMember.TargetClass : null;

        // ── List build (dispatch) ────────────────────────────────────────────────

        private void Rebuild()
        {
            _items.Clear();
            _rowActions.Clear();
            _rowDetails.Clear();
            _currentStock.Clear();

            if (_store == null)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("PartyShop", "no inventory store bound — list empty.");
                return;
            }

            if (_tab == PartyShopTab.Buy) BuildBuy();
            else BuildSell();
        }

        // ── BUY — the catalog gear the SELECTED member can equip, gated by store type ──────
        private void BuildBuy()
        {
            var member = SelectedMember;
            string job = SelectedJob;
            int level = SelectedLevel;

            // §12: guard the catalog read so a parse/IO failure logs + is skipped instead of throwing.
            DeNelle.Core.Diagnostics.Guard.Try("PartyShop", "reload gear catalog", () => GearCatalog.Reload());

            int weaponCount = 0, armorCount = 0;

            if ((_storeKinds & GearKind.Weapon) != 0)
            {
                foreach (var w in GearCatalog.AllWeapons())
                {
                    if (w == null) continue;
                    // Filter FOR the selected member: class fit + level requirement.
                    if (!string.IsNullOrEmpty(job) && !GearCatalog.WeaponFitsClass(w, job)) continue;
                    if (!MeetsLevel(w.req, level)) continue;

                    bool owned = _store.OwnedQuantity(w.id) > 0;
                    bool equipped = member != null && member.EquippedWeapon != null &&
                                    string.Equals(member.EquippedWeapon.id, w.id, StringComparison.OrdinalIgnoreCase);
                    var cost = GearCatalog.GetBuyCost(w);
                    bool affordable = owned || _economy == null || _economy.CanAfford(cost);

                    AddBuyWeaponRow(w, cost, owned, equipped, affordable);
                    _currentStock.Add((w.id, GearKind.Weapon));
                    weaponCount++;
                }
            }

            if ((_storeKinds & GearKind.Armor) != 0)
            {
                foreach (var a in GearCatalog.AllArmors())
                {
                    if (a == null) continue;
                    if (!GearCatalog.ArmorFitsClass(a, job)) continue;
                    if (!MeetsLevel(a.req, level)) continue;

                    bool owned = _store.OwnedQuantity(a.id) > 0;
                    bool equipped = member != null && member.EquippedArmor != null &&
                                    string.Equals(member.EquippedArmor.id, a.id, StringComparison.OrdinalIgnoreCase);
                    var cost = GearCatalog.GetBuyCost(a);
                    bool affordable = owned || _economy == null || _economy.CanAfford(cost);

                    AddBuyArmorRow(a, cost, owned, equipped, affordable);
                    _currentStock.Add((a.id, GearKind.Armor));
                    armorCount++;
                }
            }

            string who = member != null ? (string.IsNullOrEmpty(member.TargetName) ? "this hero" : member.TargetName) : "this hero";
            Status = _items.Count == 0
                ? "No gear here fits " + who + " yet."
                : "Tap a row to BUY (auto-equips) or EQUIP what you own.";

            // §12: no silent blank — record WHY the BUY list is empty (data vs filtered).
            if (_items.Count == 0)
            {
                bool catalogEmpty = GearCatalog.AllWeapons().Count == 0 && GearCatalog.AllArmors().Count == 0;
                if (catalogEmpty)
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("PartyShop",
                        $"BUY EMPTY for '{_vendorContext}': gear catalog loaded NO weapons/armor.");
                else
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("PartyShop",
                        $"BUY EMPTY for '{_vendorContext}' member job='{job}' lvl={level} " +
                        $"(store kinds {_storeKinds}) — every catalog item filtered out by class/level.");
            }
        }

        private void AddBuyWeaponRow(WeaponDef w, ResourceCost cost, bool owned, bool equipped, bool affordable)
        {
            string id = w.id;
            string name = string.IsNullOrEmpty(w.name) ? w.id : w.name;

            _rowDetails[id] = new PartyShopDetail(
                WeaponStats(w), DeltaVsEquippedWeapon(w), DescribeGear(w.job, w.rarity),
                w.iconPath, IconRoleWeapon, id);

            // Single-tap action: EQUIP if already owned, else BUY (which auto-equips on success).
            if (owned) _rowActions[id] = () => EquipWeapon(w);
            else _rowActions[id] = () => BuyWeapon(w);

            // Price column shows the buy cost, or 0 (Owned) when held. Equipped/Owned flags drive the chip.
            _items.Add(new ItemVM(id, name, IconRoleWeapon, id, owned ? 0 : cost.Coins, "gold",
                affordable, w.rarity, equipped: equipped, locked: false));
        }

        private void AddBuyArmorRow(ArmorDef a, ResourceCost cost, bool owned, bool equipped, bool affordable)
        {
            string id = a.id;
            string name = string.IsNullOrEmpty(a.name) ? a.id : a.name;

            _rowDetails[id] = new PartyShopDetail(
                ArmorStats(a), DeltaVsEquippedArmor(a), DescribeGear(a.job, a.rarity),
                a.iconPath, IconRoleArmor, id);

            if (owned) _rowActions[id] = () => EquipArmor(a);
            else _rowActions[id] = () => BuyArmor(a);

            _items.Add(new ItemVM(id, name, IconRoleArmor, id, owned ? 0 : cost.Coins, "gold",
                affordable, a.rarity, equipped: equipped, locked: false));
        }

        // ── SELL — owned gear (any kind the shop type accepts), credits coins, same screen ──
        private void BuildSell()
        {
            var member = SelectedMember;

            foreach (var (w, qty) in _store.OwnedWeapons())
            {
                if (w == null || (_storeKinds & GearKind.Weapon) == 0) continue;
                bool equipped = member != null && member.EquippedWeapon != null &&
                                string.Equals(member.EquippedWeapon.id, w.id, StringComparison.OrdinalIgnoreCase);
                var refund = ScaleCost(GearCatalog.GetBuyCost(w), 0.50f);
                string id = w.id;
                string name = (string.IsNullOrEmpty(w.name) ? w.id : w.name) + " x" + qty;

                _rowDetails[id] = new PartyShopDetail(
                    WeaponStats(w), "", DescribeGear(w.job, w.rarity), w.iconPath, IconRoleWeapon, id);
                _rowActions[id] = () => SellGear(w.id, refund);
                _items.Add(new ItemVM(id, name, IconRoleWeapon, id, refund.Coins, "gold", true, w.rarity, equipped: equipped));
            }

            foreach (var (a, qty) in _store.OwnedArmor())
            {
                if (a == null || (_storeKinds & GearKind.Armor) == 0) continue;
                bool equipped = member != null && member.EquippedArmor != null &&
                                string.Equals(member.EquippedArmor.id, a.id, StringComparison.OrdinalIgnoreCase);
                var refund = ScaleCost(GearCatalog.GetBuyCost(a), 0.50f);
                string id = a.id;
                string name = (string.IsNullOrEmpty(a.name) ? a.id : a.name) + " x" + qty;

                _rowDetails[id] = new PartyShopDetail(
                    ArmorStats(a), "", DescribeGear(a.job, a.rarity), a.iconPath, IconRoleArmor, id);
                _rowActions[id] = () => SellGear(a.id, refund);
                _items.Add(new ItemVM(id, name, IconRoleArmor, id, refund.Coins, "gold", true, a.rarity, equipped: equipped));
            }

            Status = _items.Count == 0
                ? "You own no gear to sell here."
                : "Tap an item to SELL it for coins — buy without leaving.";
        }

        // ── Transactions ─────────────────────────────────────────────────────────

        private void BuyWeapon(WeaponDef w)
        {
            if (w == null) return;
            if (_economy == null) { Status = "Economy unavailable."; return; }
            var cost = GearCatalog.GetBuyCost(w);
            if (!_economy.TrySpend(cost))
            {
                Status = "Not enough gold for " + Display(w.name, w.id) + " — needs " + CostString(cost) + ".";
                return;
            }
            VillageInventory.Instance?.Add(w.id, 1);
            EquipWeapon(w);   // auto-equip to the selected member on purchase (spec point 3)
            PushHud();
            Status = "Bought + equipped " + Display(w.name, w.id) + " to " + MemberName() + ".";
            Rebuild();
        }

        private void BuyArmor(ArmorDef a)
        {
            if (a == null) return;
            if (_economy == null) { Status = "Economy unavailable."; return; }
            var cost = GearCatalog.GetBuyCost(a);
            if (!_economy.TrySpend(cost))
            {
                Status = "Not enough gold for " + Display(a.name, a.id) + " — needs " + CostString(cost) + ".";
                return;
            }
            VillageInventory.Instance?.Add(a.id, 1);
            EquipArmor(a);
            PushHud();
            Status = "Bought + equipped " + Display(a.name, a.id) + " to " + MemberName() + ".";
            Rebuild();
        }

        private void EquipWeapon(WeaponDef w)
        {
            var member = SelectedMember;
            if (member == null) { Status = "No hero selected to equip."; return; }
            if (w == null) return;
            member.EquipWeaponById(w.id);   // GearLoadout auto-routes shields to off-hand + enforces hand rules
            Status = "Equipped " + Display(w.name, w.id) + " to " + MemberName() + ".";
            Rebuild();
        }

        private void EquipArmor(ArmorDef a)
        {
            var member = SelectedMember;
            if (member == null) { Status = "No hero selected to equip."; return; }
            if (a == null) return;
            member.EquipArmorById(a.id);
            Status = "Equipped " + Display(a.name, a.id) + " to " + MemberName() + ".";
            Rebuild();
        }

        private void SellGear(string id, ResourceCost refund)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_store == null || _store.OwnedQuantity(id) <= 0) { Status = "You don't own that."; return; }
            if (!_store.TryRemove(id, 1)) { Status = "Couldn't sell that."; return; }
            _economy?.Grant(refund);
            PushHud();
            Status = "Sold for +" + CostString(refund) + ".";
            SelectedId = null;
            Rebuild();
        }

        private void PushHud()
        {
            if (_economy == null) return;
            DeNelle.Core.CoreServices.Hud?.SetResources(_economy.Wood, _economy.Iron, _economy.Food, _economy.Crystals);
        }

        // ── Title (mirrors ShopVM.ResolveTitle) ──────────────────────────────────

        private string ResolveTitle()
        {
            if (!string.IsNullOrEmpty(_displayName)) return _displayName;
            string vc = _vendorContext.ToLowerInvariant();
            if (vc.Contains("armor") || vc.Contains("blacksmith")) return "Armorer's Shop";
            if (vc.Contains("forge") || vc.Contains("smith")) return "The Forge";
            if (string.IsNullOrEmpty(_vendorContext)) return "Gear Shop";
            return TitleizeVendor(_vendorContext) + " Wares";
        }

        private static string TitleizeVendor(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Vendor";
            id = id.Replace('-', ' ').Replace('_', ' ').Trim();
            if (id.Length == 0) return "Vendor";
            return char.ToUpper(id[0]) + (id.Length > 1 ? id.Substring(1) : "");
        }

        // ── Stat / delta / cost formatting (pure; System.Math) ────────────────────

        private string MemberName()
        {
            var m = SelectedMember;
            return m == null || string.IsNullOrEmpty(m.TargetName) ? "this hero" : m.TargetName;
        }

        private static string Display(string name, string id) => string.IsNullOrEmpty(name) ? id : name;

        private static string WeaponStats(WeaponDef w)
        {
            if (w == null) return "";
            int dmgPct = RoundToInt((Max(0.1f, w.damageMult) - 1f) * 100f);
            string s = "+" + dmgPct + "% dmg";
            if (w.reach > 0f) s += "   reach " + Fmt1(w.reach) + "m";
            if (!string.IsNullOrEmpty(w.damageType)) s += "   " + w.damageType;
            if (!string.IsNullOrEmpty(w.hand)) s += "   " + w.hand;
            return s;
        }

        private static string ArmorStats(ArmorDef a)
        {
            if (a == null) return "";
            int defPct = RoundToInt(Clamp(a.defense, 0f, 0.9f) * 100f);
            string s = "+" + defPct + "% def";
            if (a.hpBonus > 0f) s += "   +" + Fmt1(a.hpBonus) + " hp";
            if (!string.IsNullOrEmpty(a.weight)) s += "   " + a.weight;
            return s;
        }

        private string DeltaVsEquippedWeapon(WeaponDef w)
        {
            var m = SelectedMember;
            if (w == null || m == null || m.EquippedWeapon == null) return "";
            int cur = RoundToInt((Max(0.1f, m.EquippedWeapon.damageMult) - 1f) * 100f);
            int nw  = RoundToInt((Max(0.1f, w.damageMult) - 1f) * 100f);
            int d = nw - cur;
            return d == 0 ? "= equipped" : (d > 0 ? "+" + d + "% dmg vs equipped" : d + "% dmg vs equipped");
        }

        private string DeltaVsEquippedArmor(ArmorDef a)
        {
            var m = SelectedMember;
            if (a == null || m == null || m.EquippedArmor == null) return "";
            int cur = RoundToInt(Clamp(m.EquippedArmor.defense, 0f, 0.9f) * 100f);
            int nw  = RoundToInt(Clamp(a.defense, 0f, 0.9f) * 100f);
            int d = nw - cur;
            return d == 0 ? "= equipped" : (d > 0 ? "+" + d + "% def vs equipped" : d + "% def vs equipped");
        }

        private static string DescribeGear(string job, string rarity)
        {
            string r = string.IsNullOrEmpty(rarity) ? "" : char.ToUpper(rarity[0]) + (rarity.Length > 1 ? rarity.Substring(1) : "");
            string j = string.IsNullOrEmpty(job) || job == "any" ? "any class" : "the " + job;
            return (string.IsNullOrEmpty(r) ? "" : r + " gear. ") + "Suited to " + j + ".";
        }

        private ResourceCost ScaleCost(ResourceCost c, float f) =>
            new ResourceCost(
                RoundToInt(c.Wood * f), RoundToInt(c.Food * f), RoundToInt(c.Iron * f),
                RoundToInt(c.Crystals * f), RoundToInt(c.Coins * f));

        private static string CostString(ResourceCost c)
        {
            var parts = new List<string>();
            if (c.Coins > 0) parts.Add(c.Coins + " Gold");
            if (c.Wood > 0) parts.Add(c.Wood + "W");
            if (c.Iron > 0) parts.Add(c.Iron + "I");
            if (c.Food > 0) parts.Add(c.Food + "F");
            if (c.Crystals > 0) parts.Add(c.Crystals + "C");
            return parts.Count == 0 ? "Free" : string.Join(" ", parts);
        }

        private static bool MeetsLevel(GearReq req, int level) => req == null || level >= req.level;

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        // ── Pure math (System.Math — keeps the VM Unity-UI-free) ──────────────────
        private static int RoundToInt(float f) => (int)Math.Floor(f + 0.5f);
        private static float Max(float a, float b) => a > b ? a : b;
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        private static string Fmt1(float v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
