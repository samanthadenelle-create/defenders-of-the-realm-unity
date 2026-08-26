// =============================================================================
// ShopVM — the vendor shop's pure ViewModel (WO-431, first MVVM consumer).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// ALL shop STATE + LOGIC lives here, moved verbatim out of ShopPanel (the View):
//   • economy reads + affordability (via the injected IEconomy seam — no singleton)
//   • catalog -> row building, intersected with the VendorStockContract + Type filter
//   • the WO-406 never-empty fallback to general stock
//   • buy / sell / equip execution + the exact status strings
//   • vendor-gold pools (StartGoldFor / VendorKey) + the 50/50/30% sell rates
//   • the CurrentStock contract (id + GearKind) the AutoPilot bot asserts
//
// PURE: NO UnityEngine UI types (no GameObject/Image/Sprite/RectTransform/MonoBehaviour
// /Canvas/Color). Icons are carried as KEYS (IconRole/IconName on ItemVM) — the View
// resolves the actual Sprite. Rounding uses System.Math, not UnityEngine.Mathf, so the
// VM is unit-testable without a scene (ARCHITECTURE_PRINCIPLES.md §2 / §2c; WO-431).
//
// Implements DeNelle.Core.UI.Mvvm.IPanelViewModel: the View binds it, re-renders on
// Changed, and routes user input back as commands. The View never reads game state.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village.Crafting;   // VillageInventory

namespace DeNelle.Village.Hero
{
    /// <summary>The shop's three tabs. Mirrors the old ShowBuy/ShowEquip/ShowSell modes.</summary>
    public enum ShopMode { Buy, Equip, Sell }

    /// <summary>
    /// Equip target the View resolves (the active hero's <see cref="GearLoadout"/>, wrapped). Kept
    /// out of the VM's Unity surface: the VM only needs equipped names + equip-by-id, so this thin
    /// seam carries exactly that and stays mockable in tests. Null when no hero is present.
    /// </summary>
    public interface IShopEquipTarget
    {
        string EquippedWeaponName { get; }
        string EquippedArmorName { get; }
        float EquippedWeaponDamageMult { get; }   // 1f when no weapon
        float EquippedArmorDefense { get; }        // 0f when no armor
        void EquipWeaponById(string id);
        void EquipArmorById(string id);

        // ── WO-1214: the EQUIP tab must be able to ask WHY, not just to call ──────────
        // BuildEquip lists every OWNED gear id with no class or level filter, so an
        // ineligible drop appears there, is tapped, is refused by the (now fail-closed)
        // GearLoadout seam - and the old TryEquip printed "Equipped." over the refusal.
        // A screen that reports success for an action that did nothing is the same silent
        // failure this ticket exists to end, so the target now exposes the two facts the
        // eligibility question needs. They mirror IEquipTarget.TargetClass/TargetLevel.
        /// <summary>The wearer's class key ("mage"), as the equip seam resolves it. "" when unknown.</summary>
        string TargetClass { get; }
        /// <summary>The wearer's level, as the equip seam resolves it. 1 when unknown.</summary>
        int TargetLevel { get; }
    }

    /// <summary>
    /// Detail-pane payload for the selected row (icon keys + name/desc/stats/cost). Carried by the
    /// VM so the View renders the right pane purely from data; the View resolves the Sprite from the
    /// icon keys (presentation), never re-pulling state.
    /// </summary>
    public readonly struct ShopDetail
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string Stats;
        public readonly string CostString;   // already "Cost: …" / "Refund: +…"
        public readonly string IconRole;     // "weapon" / "armor" / "potion"
        public readonly string IconName;     // item id (View resolves the Sprite)

        public ShopDetail(string name, string description, string stats, string costString,
                          string iconRole, string iconName)
        {
            Name = name;
            Description = description;
            Stats = stats;
            CostString = costString;
            IconRole = iconRole;
            IconName = iconName;
        }
    }

    public sealed class ShopVM : IPanelViewModel, IDisposable
    {
        // ── Icon role keys (ItemVM.IconRole) — the View maps these to the real sprite source ──
        public const string IconRoleWeapon = "weapon";
        public const string IconRoleArmor  = "armor";
        public const string IconRolePotion = "potion";
        public const string IconRoleAccessory = "accessory";   // WO-543 (rings + amulets)

        private readonly string _vendorContext;
        private readonly string _displayName;
        private readonly IEconomy _economy;
        private readonly IShopEquipTarget _equip;   // may be null (no hero / tests)
        private readonly Action _onClose;           // View supplies how to dismiss; may be null
        private readonly Action<ShopVM> _onEquipRefreshHero; // View re-resolves the hero loadout; may be null

        private readonly Action<ResourceSnapshot> _ecoHandler;
        private bool _disposed;

        private readonly List<string> _potionIds = new List<string> { "minor-heal-potion", "minor-mana-potion" };

        // The per-row buy/sell/equip action of the active list, keyed by item id (armed on Select).
        private readonly Dictionary<string, Action> _rowActions = new Dictionary<string, Action>();
        // The detail payload per id, so Select can fill the detail pane purely from VM data.
        private readonly Dictionary<string, ShopDetail> _rowDetails = new Dictionary<string, ShopDetail>();

        private readonly List<ItemVM> _items = new List<ItemVM>();
        private readonly List<(string id, GearKind kind)> _currentStock = new List<(string id, GearKind kind)>();

        // Type filter for the SELL list (Buy is vendor-locked). Default = All.
        private GearKind _buyFilter = GearKind.Weapon | GearKind.Armor | GearKind.Potion | GearKind.Accessory;

        /// <summary>
        /// DI-in-Open factory (UI_MVVM_MIGRATION_PLAN §1 step 5): resolves the IEconomy handle
        /// (EconomyService.Instance) ITSELF so the View never names the economy singleton — the
        /// sole resolution site. The equip target + Close/refresh callbacks stay View-supplied
        /// (they wrap live scene objects the pure VM can't hold). Mirrors BuildingUpgradeVM.CreateDefault.
        /// </summary>
        public static ShopVM CreateDefault(string vendorContext,
                                           string displayName = null,
                                           IShopEquipTarget equip = null,
                                           Action onClose = null,
                                           Action<ShopVM> onEquipRefreshHero = null)
        {
            return new ShopVM(vendorContext, EconomyService.Instance, displayName, equip, onClose, onEquipRefreshHero);
        }

        public ShopVM(string vendorContext, IEconomy economy,
                      string displayName = null,
                      IShopEquipTarget equip = null,
                      Action onClose = null,
                      Action<ShopVM> onEquipRefreshHero = null)
        {
            _vendorContext = vendorContext ?? "";
            _displayName = displayName;
            _economy = economy;
            _equip = equip;
            _onClose = onClose;
            _onEquipRefreshHero = onEquipRefreshHero;

            if (_economy != null)
            {
                _ecoHandler = _ => Raise();
                _economy.OnChanged += _ecoHandler;
            }

            Mode = ShopMode.Buy;
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
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        public ShopMode Mode { get; private set; }

        /// <summary>The active mode's rows (affordability already computed). Never null.</summary>
        public IReadOnlyList<ItemVM> Items => _items;

        public string SelectedId { get; private set; }

        /// <summary>The selected row's detail payload, or null when nothing is selected.</summary>
        public ShopDetail? Selected =>
            SelectedId != null && _rowDetails.TryGetValue(SelectedId, out var d) ? d : (ShopDetail?)null;

        public string Status { get; private set; }

        /// <summary>Bottom action button label ("Purchase" on Buy, "Sell" on Sell, "" on Equip).</summary>
        public string ActionLabel { get; private set; }

        /// <summary>Live wallet readout (the old "Gold: … Wood: …" line is rebuilt from these in the View).</summary>
        public int Coins    => _economy?.Coins ?? 0;
        public int Wood     => _economy?.Wood ?? 0;
        public int Iron     => _economy?.Iron ?? 0;
        public int Food     => _economy?.Food ?? 0;
        public int Crystals => _economy?.Crystals ?? 0;

        public WalletVM Wallet =>
            new WalletVM(new[] { new WalletVM.Entry("gold", "icons", "gold", Coins) });

        /// <summary>The active Type filter (SELL list). View uses it to highlight the filter row.</summary>
        public GearKind BuyFilter => _buyFilter;

        /// <summary>Whether the View should show the Type-filter row (SELL only).</summary>
        public bool FilterBarVisible => Mode == ShopMode.Sell;

        /// <summary>The store's ACTUAL built stock (id + category) for the AutoPilot bot assertion.</summary>
        public IReadOnlyList<(string id, GearKind kind)> CurrentStock => _currentStock;

        public string VendorContext => _vendorContext;

        // ── Commands ────────────────────────────────────────────────────────────

        public void SetMode(ShopMode mode)
        {
            Mode = mode;
            if (mode == ShopMode.Buy)
                _buyFilter = GearKind.Weapon | GearKind.Armor | GearKind.Potion | GearKind.Accessory;   // buy is vendor-locked
            SelectedId = null;
            Rebuild();
            Raise();
        }

        /// <summary>Set the SELL Type filter (All / Weapons / Armor / Potions), then rebuild the list.</summary>
        public void SetFilter(GearKind kind)
        {
            _buyFilter = kind;
            // The filter row drives the SELL list (BUY is vendor-locked).
            Mode = ShopMode.Sell;
            SelectedId = null;
            Rebuild();
            Raise();
        }

        public void Select(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            SelectedId = id;
            Raise();
        }

        /// <summary>Fire the selected row's buy action (or set the "select first" status).</summary>
        public void Buy() => InvokeSelectedAction();

        /// <summary>Fire the selected row's sell action (same arming as Buy; label differs).</summary>
        public void Sell() => InvokeSelectedAction();

        private void InvokeSelectedAction()
        {
            if (SelectedId != null && _rowActions.TryGetValue(SelectedId, out var act) && act != null)
                act();
            else
                Status = "Select an item first.";
            Raise();
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }

        // ── Title ────────────────────────────────────────────────────────────────
        //
        // ⛔ ONE IMPLEMENTATION, and it is NOT here. This method used to invent the shop
        // header from substrings of the vendor context — "Armorer's Shop", "The Forge",
        // "Market Stalls", "Jeweler's Bench", "Lumbermill Stores" — and PartyShopVM
        // carried a second copy of the same list, annotated "mirrors ShopVM.ResolveTitle".
        // Two copies of one fact, and the copies had already diverged: the catalog says
        // "Lumber Mill", this said "Lumbermill Stores". The name now comes from the
        // catalog row that claims the vendor's ROLE (StructureRoles), which is the single
        // naming authority, via the one shared resolver both VMs call.

        private string ResolveTitle() =>
            VendorStockResolver.TitleFor(_vendorContext, _displayName);

        // ── List build (dispatch) ────────────────────────────────────────────────

        private void Rebuild()
        {
            _items.Clear();
            _rowActions.Clear();
            _rowDetails.Clear();
            _currentStock.Clear();

            switch (Mode)
            {
                case ShopMode.Buy:   BuildBuy();   break;
                case ShopMode.Sell:  BuildSell();  break;
                case ShopMode.Equip: BuildEquip(); break;
            }
        }

        // ── BUY (moved from ShopPanel.ShowBuy) ────────────────────────────────────

        private void BuildBuy()
        {
            _buyFilter = GearKind.Weapon | GearKind.Armor | GearKind.Potion | GearKind.Accessory;   // buy is vendor-locked
            ActionLabel = "Purchase";
            Status = "Buy gear: tap a row to view it, then Purchase.";

            // §12: guard the catalog read so a parse/IO failure logs + is skipped (and the
            // never-empty fallback below still runs from whatever loaded) instead of throwing
            // out of BuildBuy and silently blanking the BUY tab (the WO-412 surface).
            DeNelle.Core.Diagnostics.Guard.Try("Shop", "reload gear catalog", () => GearCatalog.Reload());

            string ctx = _vendorContext.ToLowerInvariant();
            GearKind allowed = VendorStockContract.AllowedFor(ctx);
            allowed &= _buyFilter;

            // WO-429: OFFLINE-FIRST stock. Read BUY stock through StoreStockService, not
            // GearCatalog directly: it returns the LOCAL catalog as the authoritative
            // default (always available offline) and MERGES any optional remote refresh on
            // top. With no remote provider registered (the shipping default) this is exactly
            // the local catalog — so the BUY tab is never blocked/emptied by network state.
            var allWeapons = new List<WeaponDef>();
            var allArmors  = new List<ArmorDef>();
            foreach (var w in StoreStockService.Weapons()) if (w != null) allWeapons.Add(w);
            foreach (var a in StoreStockService.Armors())  if (a != null) allArmors.Add(a);

            // WO-543: accessories (rings + amulets) come from the local catalog (offline-first by
            // nature — no remote stock). The Jeweler is the only vendor whose contract allows them.
            var allAccessories = new List<AccessoryDef>();
            foreach (var ac in GearCatalog.Accessories) if (ac != null) allAccessories.Add(ac);

            var weapons = new List<WeaponDef>();
            var armors  = new List<ArmorDef>();
            var accessories = new List<AccessoryDef>();
            if ((allowed & GearKind.Weapon)    != 0) weapons.AddRange(allWeapons);
            if ((allowed & GearKind.Armor)     != 0) armors.AddRange(allArmors);
            if ((allowed & GearKind.Accessory) != 0) accessories.AddRange(allAccessories);
            bool potionsAllowed = (allowed & GearKind.Potion) != 0;

            bool wouldBeEmpty = weapons.Count == 0 && armors.Count == 0 && accessories.Count == 0 &&
                                (!potionsAllowed || _potionIds.Count == 0);
            bool filterIsAll = _buyFilter == (GearKind.Weapon | GearKind.Armor | GearKind.Potion | GearKind.Accessory);
            if (wouldBeEmpty && filterIsAll && (allWeapons.Count + allArmors.Count) > 0)
            {
                weapons.Clear(); weapons.AddRange(allWeapons);
                armors.Clear();  armors.AddRange(allArmors);
                potionsAllowed = true;
            }

            foreach (var w in weapons)
            {
                if (w == null) continue;
                var wCopy = w;
                var cost = GearCatalog.GetBuyCost(w);
                bool affordable = _economy == null || _economy.CanAfford(cost);
                int wDmgPct = RoundToInt((Max(0.1f, w.damageMult) - 1f) * 100f);
                string wStats = "+" + wDmgPct + "% dmg" + (w.reach > 0f ? "   reach " + Fmt1(w.reach) + "m" : "") + DeltaVsEquipped(w);
                string name = string.IsNullOrEmpty(w.name) ? w.id : w.name;
                _rowDetails[w.id] = new ShopDetail(name, DescribeGear(w.job, w.rarity), wStats,
                    "Cost: " + CostString(cost), IconRoleWeapon, w.id);
                _rowActions[w.id] = () => TryBuyWeapon(wCopy);
                _items.Add(new ItemVM(w.id, BuyLabel(w.name, GearAppraisal.Appraise(w)),
                    IconRoleWeapon, w.id, cost.Coins, "gold", affordable, w.rarity));
                _currentStock.Add((w.id, GearKind.Weapon));
            }
            foreach (var a in armors)
            {
                if (a == null) continue;
                var aCopy = a;
                var cost = GearCatalog.GetBuyCost(a);
                bool affordable = _economy == null || _economy.CanAfford(cost);
                int aDefPct = RoundToInt(Clamp(a.defense, 0f, GearLoadout.MaxArmorDefense) * 100f);
                string aStats = "+" + aDefPct + "% def" + (a.hpBonus > 0f ? "   +" + Fmt1(a.hpBonus) + " hp" : "") + DeltaVsEquipped(a);
                string name = string.IsNullOrEmpty(a.name) ? a.id : a.name;
                _rowDetails[a.id] = new ShopDetail(name, DescribeGear(a.job, a.rarity), aStats,
                    "Cost: " + CostString(cost), IconRoleArmor, a.id);
                _rowActions[a.id] = () => TryBuyArmor(aCopy);
                _items.Add(new ItemVM(a.id, BuyLabel(a.name, GearAppraisal.Appraise(a)),
                    IconRoleArmor, a.id, cost.Coins, "gold", affordable, a.rarity));
                _currentStock.Add((a.id, GearKind.Armor));
            }
            foreach (var ac in accessories)
            {
                if (ac == null) continue;
                var acCopy = ac;
                var cost = GearCatalog.GetBuyCost(ac);
                bool affordable = _economy == null || _economy.CanAfford(cost);
                int acDmgPct = RoundToInt(Max(0f, ac.damageMult) * 100f);
                int acDefPct = RoundToInt(Max(0f, ac.defense) * 100f);
                var bits = new List<string>();
                if (acDmgPct > 0) bits.Add("+" + acDmgPct + "% dmg");
                if (acDefPct > 0) bits.Add("+" + acDefPct + "% def");
                if (ac.hpBonus > 0) bits.Add("+" + ac.hpBonus + " hp");
                string acStats = bits.Count > 0 ? string.Join("   ", bits) : "Accessory";
                string name = string.IsNullOrEmpty(ac.name) ? ac.id : ac.name;
                _rowDetails[ac.id] = new ShopDetail(name, DescribeGear(ac.job, ac.rarity), acStats,
                    "Cost: " + CostString(cost), IconRoleAccessory, ac.id);
                _rowActions[ac.id] = () => TryBuyAccessory(acCopy);
                _items.Add(new ItemVM(ac.id, BuyLabel(ac.name, GearAppraisal.Appraise(ac)),
                    IconRoleAccessory, ac.id, cost.Coins, "gold", affordable, ac.rarity));
                _currentStock.Add((ac.id, GearKind.Accessory));
            }
            if (potionsAllowed)
            foreach (var pid in _potionIds)
            {
                var cost = new ResourceCost(coins: pid.Contains("mana") ? 12 : 8);
                bool affordable = _economy == null || _economy.CanAfford(cost);
                string pidCopy = pid; var costCopy = cost;
                string potionDesc = pid.Contains("mana") ? "Restores mana in a pinch." : "Restores health in a pinch.";
                _rowDetails[pid] = new ShopDetail(pid, potionDesc, "Consumable",
                    "Cost: " + CostString(cost), IconRolePotion, pid);
                _rowActions[pid] = () => TryBuyPotion(pidCopy, costCopy);
                _items.Add(new ItemVM(pid, pid, IconRolePotion, pid, cost.Coins, "gold", affordable));
                _currentStock.Add((pid, GearKind.Potion));
            }

            // §12: no silent blank. If the BUY list built nothing, log WHICH case it is so the
            // trace pinpoints the dead step — data-empty (catalog has no wares) vs filtered-out
            // (the vendor contract allowed nothing the catalog could satisfy). The View renders
            // whatever _items holds; this records why it is empty instead of a blank tab.
            if (_items.Count == 0)
            {
                bool catalogEmpty = allWeapons.Count == 0 && allArmors.Count == 0 && allAccessories.Count == 0;
                if (catalogEmpty)
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Shop",
                        $"BUY tab EMPTY for vendor '{_vendorContext}': gear catalog loaded NO weapons/armor/accessories " +
                        "(weapons.json/armor.json/accessories.json missing or failed to parse).");
                else
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Shop",
                        $"BUY tab EMPTY for vendor '{_vendorContext}' (allowed {allowed}) though the catalog " +
                        $"has {allWeapons.Count} weapon(s) + {allArmors.Count} armor(s) + {allAccessories.Count} accessory(ies) — filter excluded all stock.");
            }
        }

        private void TryBuyWeapon(WeaponDef w)
        {
            if (w == null) return;
            var cost = GearCatalog.GetBuyCost(w);
            if (_economy == null) { Status = "Economy unavailable."; return; }
            if (_economy.TrySpend(cost))
            {
                if (VillageInventory.Instance != null) VillageInventory.Instance.Add(w.id, 1);
                var ap = GearAppraisal.Appraise(w);
                Status = ap != null && ap.isElarionMarked
                    ? "Purchased " + w.name + "! " + ap.Summary() + " (added to inventory - see EQUIP)"
                    : "Purchased " + w.name + "! Added to inventory - see EQUIP.";
                PushHudResources();
                Rebuild();
            }
            else
            {
                Status = "Not enough resources for " + w.name + " - needs " + CostString(cost) + ".";
            }
        }

        private void TryBuyArmor(ArmorDef a)
        {
            if (a == null) return;
            var cost = GearCatalog.GetBuyCost(a);
            if (_economy == null) { Status = "Economy unavailable."; return; }
            if (_economy.TrySpend(cost))
            {
                if (VillageInventory.Instance != null) VillageInventory.Instance.Add(a.id, 1);
                var ap = GearAppraisal.Appraise(a);
                Status = ap != null && ap.isElarionMarked
                    ? "Purchased " + a.name + "! " + ap.Summary() + " (added to inventory - see EQUIP)"
                    : "Purchased " + a.name + "! Added to inventory - see EQUIP.";
                PushHudResources();
                Rebuild();
            }
            else
            {
                Status = "Not enough resources for " + a.name + " - needs " + CostString(cost) + ".";
            }
        }

        private void TryBuyAccessory(AccessoryDef ac)
        {
            if (ac == null) return;
            var cost = GearCatalog.GetBuyCost(ac);
            if (_economy == null) { Status = "Economy unavailable."; return; }
            if (_economy.TrySpend(cost))
            {
                if (VillageInventory.Instance != null) VillageInventory.Instance.Add(ac.id, 1);
                var ap = GearAppraisal.Appraise(ac);
                Status = ap != null && ap.isElarionMarked
                    ? "Purchased " + ac.name + "! " + ap.Summary() + " (added to inventory - see EQUIP)"
                    : "Purchased " + ac.name + "! Added to inventory - see EQUIP.";
                PushHudResources();
                Rebuild();
            }
            else
            {
                Status = "Not enough resources for " + ac.name + " - needs " + CostString(cost) + ".";
            }
        }

        private void TryBuyPotion(string id, ResourceCost cost)
        {
            if (_economy == null) { Status = "Economy unavailable."; return; }
            if (_economy.TrySpend(cost))
            {
                if (VillageInventory.Instance != null) VillageInventory.Instance.Add(id, 1);
                Status = "Purchased " + id + "!";
                PushHudResources();
                Rebuild();
            }
            else Status = "Not enough resources for " + id + " - needs " + CostString(cost) + ".";
        }

        // Mirror ShopPanel.RefreshEco's town-HUD push (owner: "sync on subtract"). Pure data call
        // through CoreServices.Hud — no Unity UI types involved.
        private void PushHudResources()
        {
            if (_economy == null) return;
            DeNelle.Core.CoreServices.Hud?.SetResources(_economy.Wood, _economy.Iron, _economy.Food, _economy.Crystals);
        }

        // ── SELL (moved from ShopPanel.ShowSell) ──────────────────────────────────

        private void BuildSell()
        {
            ActionLabel = "Sell";
            Status = "Sell for gold: tap a row, then Sell.  Vendor gold: " + VendorGold() + ".";

            var inv = VillageInventory.Instance;
            if (inv == null) { Status = "No inventory."; return; }

            var sellable = new List<string>();
            foreach (var kv in inv.Counts)
            {
                if (kv.Value <= 0) continue;
                string id = kv.Key;
                bool isPotion = _potionIds.Contains(id);
                bool isWeapon = GearCatalog.FindWeapon(id) != null;
                bool isArmor  = GearCatalog.FindArmor(id) != null;
                bool isAccessory = GearCatalog.FindAccessory(id) != null;
                if (!isWeapon && !isArmor && !isAccessory && !isPotion) continue;
                GearKind k = isWeapon ? GearKind.Weapon : isArmor ? GearKind.Armor
                           : isAccessory ? GearKind.Accessory : GearKind.Potion;
                if ((_buyFilter & k) == 0) continue;
                sellable.Add(id);
            }

            foreach (var id in sellable)
            {
                int owned = inv.Get(id);
                WeaponDef w = GearCatalog.FindWeapon(id);
                ArmorDef a = GearCatalog.FindArmor(id);
                AccessoryDef ac = (w == null && a == null) ? GearCatalog.FindAccessory(id) : null;

                string display = (w != null ? w.name : a != null ? a.name : ac != null ? ac.name : id) + " x" + owned;
                ResourceCost refund = w != null ? ScaleCost(GearCatalog.GetBuyCost(w), 0.50f) :
                                    a != null ? ScaleCost(GearCatalog.GetBuyCost(a), 0.50f) :
                                    ac != null ? ScaleCost(GearCatalog.GetBuyCost(ac), 0.50f) :
                                    ScaleCost(PotionBuyCost(id), 0.30f);

                string idCopy = id; var refundCopy = refund;
                bool isPotionSell = _potionIds.Contains(id);
                string sellDesc = w != null ? DescribeGear(w.job, w.rarity)
                                : a != null ? DescribeGear(a.job, a.rarity)
                                : ac != null ? DescribeGear(ac.job, ac.rarity)
                                : (isPotionSell ? "Consumable you own." : "Owned item.");
                string sellStats = w != null ? "+" + RoundToInt((Max(0.1f, w.damageMult) - 1f) * 100f) + "% dmg"
                                 : a != null ? "+" + RoundToInt(Clamp(a.defense, 0f, GearLoadout.MaxArmorDefense) * 100f) + "% def"
                                 : ac != null ? AccessorySellStats(ac)
                                 : "Consumable";
                string sellName = w != null ? w.name : a != null ? a.name : ac != null ? ac.name : id;
                string iconRole = w != null ? IconRoleWeapon : a != null ? IconRoleArmor
                                : ac != null ? IconRoleAccessory : IconRolePotion;

                _rowDetails[id] = new ShopDetail(sellName, sellDesc, sellStats,
                    "Refund: +" + CostString(refundCopy), iconRole, id);
                _rowActions[id] = () => TrySell(idCopy, refundCopy);
                // Sell rows show the refund as the price; always "affordable" (a sale credits the player).
                _items.Add(new ItemVM(id, display, iconRole, id, refundCopy.Coins, "gold", true));
            }
        }

        private void TrySell(string id, ResourceCost refund)
        {
            var inv = VillageInventory.Instance;
            if (inv == null || inv.Get(id) <= 0) return;
            int price = refund.Coins;
            if (VendorGold() < price)
            {
                Status = "I don't have enough coin for that right now.";
                return;
            }
            inv.TryConsume(id, 1);
            SpendVendorGold(price);
            if (_economy != null) _economy.Grant(refund);
            Status = "Sold for +" + CostString(refund) + ".  (Vendor gold: " + VendorGold() + ")";
            PushHudResources();
            SelectedId = null;
            Rebuild();
        }

        // ── EQUIP (moved from ShopPanel.ShowEquip / TryEquip) ──────────────────────

        private void BuildEquip()
        {
            ActionLabel = "";
            Status = "Equip owned gear to the active hero (updates visuals + stats).";

            _onEquipRefreshHero?.Invoke(this);   // View re-resolves the active hero loadout

            var inv = VillageInventory.Instance;
            if (inv == null) return;

            foreach (var kv in inv.Counts)
            {
                if (kv.Value <= 0) continue;
                string id = kv.Key;
                var w = GearCatalog.FindWeapon(id);
                var a = w == null ? GearCatalog.FindArmor(id) : null;
                if (w == null && a == null) continue;

                int owned = inv.Get(id);
                bool isWeapon = w != null;
                string name = isWeapon ? w.name : a.name;
                string label = name + " (owned " + owned + ")";
                string idCopy = id; bool isWeaponCopy = isWeapon;
                string iconRole = isWeapon ? IconRoleWeapon : IconRoleArmor;

                _rowActions[id] = () => TryEquip(idCopy, isWeaponCopy);
                _rowDetails[id] = new ShopDetail(name,
                    isWeapon ? DescribeGear(w.job, w.rarity) : DescribeGear(a.job, a.rarity),
                    isWeapon ? "+" + RoundToInt((Max(0.1f, w.damageMult) - 1f) * 100f) + "% dmg"
                             : "+" + RoundToInt(Clamp(a.defense, 0f, GearLoadout.MaxArmorDefense) * 100f) + "% def",
                    "", iconRole, id);
                _items.Add(new ItemVM(id, label, iconRole, id, 0, "gold", true,
                    isWeapon ? w.rarity : a.rarity));
            }
        }

        /// <summary>The "Current: weapon / armor" line for the equip pane (View renders it as a header).</summary>
        public string EquipCurrentLine()
        {
            string current = "Current: ";
            if (_equip != null)
            {
                current += (!string.IsNullOrEmpty(_equip.EquippedWeaponName) ? _equip.EquippedWeaponName : "no weapon") + " / ";
                current += (!string.IsNullOrEmpty(_equip.EquippedArmorName) ? _equip.EquippedArmorName : "no armor");
            }
            else current += "none";
            return current;
        }

        private void TryEquip(string id, bool isWeapon)
        {
            _onEquipRefreshHero?.Invoke(this);
            if (_equip == null) { Status = "No hero to equip."; return; }

            // WO-1214 Rulings 2 + 3 - ask the ONE authority (GearCatalog) BEFORE claiming success.
            // The EQUIP list above is built from the raw owned set with no class or level filter,
            // so an ineligible drop reaches here; the seam refuses it, and this line used to print
            // "Equipped." anyway. The item is KEPT and stays sellable - the refusal explains why.
            string job = _equip.TargetClass;
            int level = _equip.TargetLevel;
            if (isWeapon)
            {
                var w = GearCatalog.FindWeapon(id);
                var currentMain = GearCatalog.FindWeapon(_equipCurrentMainHandId());
                if (w != null && !GearCatalog.CanEquipWeaponNow(w, currentMain, job, level,
                                                               out string refusal, out _))
                {
                    Status = refusal;
                    Rebuild();
                    return;
                }
                _equip.EquipWeaponById(id);
            }
            else
            {
                var a = GearCatalog.FindArmor(id);
                if (a != null && !GearCatalog.CanEquipArmorNow(a, job, level, out string refusal, out _))
                {
                    Status = refusal;
                    Rebuild();
                    return;
                }
                _equip.EquipArmorById(id);
            }

            Status = "Equipped. Visuals + stats updated.";
            Rebuild();
        }

        /// <summary>
        /// WO-1214 - the id of the wearer's CURRENT main hand, for the armed-hero (Ruling 4)
        /// question. IShopEquipTarget only exposes the display NAME, so it is resolved back to a
        /// row by name; a miss simply yields null, which makes the Ruling 4 branch inert rather
        /// than wrong (the seam still fails closed either way - this only decides whether the
        /// SHOP can explain the refusal in advance).
        /// </summary>
        private string _equipCurrentMainHandId()
        {
            string name = _equip != null ? _equip.EquippedWeaponName : null;
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var w in GearCatalog.AllWeapons())
                if (w != null && string.Equals(w.name, name, StringComparison.OrdinalIgnoreCase)) return w.id;
            return null;
        }

        // ── Vendor gold pools ─────────────────────────────────────────────────────
        //
        // NOT PERSISTED, and that is what makes re-keying them safe. `_vendorGold` is a
        // private static in-memory dictionary; it appears in no save schema, no
        // PlayerPrefs, no server payload, and nothing outside this file reads it (checked
        // 2026-08-23 before the change). The keys never outlive the process, so no saved
        // pool can be stranded by renaming one.
        //
        // The key is now the vendor's catalog ROLE. It used to be a substring test that
        // returned "blacksmith" — a word that is neither a catalog id nor a role, and
        // which lumped the ARMOURER in with anything containing "armor". The pool sizes
        // are unchanged; only the identity that selects them is honest now.

        private static readonly Dictionary<string, int> _vendorGold = new Dictionary<string, int>();

        // The arcane tower has no catalog row and therefore no role, so it keeps a named
        // residual key. When it earns a row with a role, delete this and add the role case.
        private const string ArcaneKey = "arcane";
        private const string GeneralKey = "general";

        private string VendorKey()
        {
            string role = VendorStockContract.RoleFor(_vendorContext);
            if (role != DeNelle.Core.Catalog.StructureRole.None) return role;

            string vc = (_vendorContext ?? string.Empty).ToLowerInvariant();
            if (vc.Contains(ArcaneKey) || vc.Contains("tower") || vc.Contains("magic")) return ArcaneKey;
            return GeneralKey;
        }

        private static int StartGoldFor(string key)
        {
            switch (key)
            {
                case DeNelle.Core.Catalog.StructureRole.Weaponsmith: return 8000;   // was "forge"
                case DeNelle.Core.Catalog.StructureRole.Armorer:     return 9000;   // was "blacksmith"
                case ArcaneKey:                                      return 7000;
                default:                                             return 5000;
            }
        }

        private int VendorGold()
        {
            string k = VendorKey();
            if (!_vendorGold.ContainsKey(k)) _vendorGold[k] = StartGoldFor(k);
            return _vendorGold[k];
        }

        private void SpendVendorGold(int amt)
        {
            _vendorGold[VendorKey()] = Math.Max(0, VendorGold() - amt);
        }

        private string AccessorySellStats(AccessoryDef ac)
        {
            if (ac == null) return "Accessory";
            var bits = new List<string>();
            int dmg = RoundToInt(Max(0f, ac.damageMult) * 100f);
            int def = RoundToInt(Max(0f, ac.defense) * 100f);
            if (dmg > 0) bits.Add("+" + dmg + "% dmg");
            if (def > 0) bits.Add("+" + def + "% def");
            if (ac.hpBonus > 0) bits.Add("+" + ac.hpBonus + " hp");
            return bits.Count > 0 ? string.Join("   ", bits) : "Accessory";
        }

        private ResourceCost PotionBuyCost(string id)
        {
            return new ResourceCost(coins: (id != null && id.Contains("mana")) ? 12 : 8);
        }

        // ── Cost / label / delta helpers (moved verbatim) ─────────────────────────

        private ResourceCost ScaleCost(ResourceCost c, float f)
        {
            return new ResourceCost(
                RoundToInt(c.Wood * f),
                RoundToInt(c.Food * f),
                RoundToInt(c.Iron * f),
                RoundToInt(c.Crystals * f),
                RoundToInt(c.Coins * f));
        }

        private string CostString(ResourceCost c)
        {
            // WO-697: cost numbers through the ONE kit formatter (compact >= 10k).
            var parts = DeNelle.Core.UI.CostFormat.Parts(new[] { ("gold", "Gold", c.Coins), ("wood", "Wood", c.Wood), ("iron", "Iron", c.Iron), ("stone", "Stone", c.Food), ("crystal", "Crystals", c.Crystals) });
            return parts.Count == 0 ? "Free" : DeNelle.Core.UI.CostFormat.Words(parts);
        }

        private string BuyLabel(string baseName, GearAppraisalResult appraisal)
        {
            if (appraisal == null || !appraisal.isElarionMarked) return baseName;
            return baseName + "  [" + appraisal.makersMark + "]";
        }

        private static string DescribeGear(string job, string rarity)
        {
            string r = string.IsNullOrEmpty(rarity) ? "" : char.ToUpper(rarity[0]) + (rarity.Length > 1 ? rarity.Substring(1) : "");
            string j = string.IsNullOrEmpty(job) || job == "any" ? "any class" : "the " + job;
            return (string.IsNullOrEmpty(r) ? "" : r + " gear. ") + "Suited to " + j + ".";
        }

        private string DeltaVsEquipped(WeaponDef w)
        {
            if (w == null || _equip == null || _equip.EquippedWeaponName == null) return "";
            int cur = RoundToInt((Max(0.1f, _equip.EquippedWeaponDamageMult) - 1f) * 100f);
            int nw  = RoundToInt((Max(0.1f, w.damageMult) - 1f) * 100f);
            int d = nw - cur;
            return d == 0 ? "\n(= equipped)" : (d > 0 ? "\n(+" + d + "% dmg vs equipped)" : "\n(" + d + "% dmg vs equipped)");
        }

        private string DeltaVsEquipped(ArmorDef a)
        {
            if (a == null || _equip == null || _equip.EquippedArmorName == null) return "";
            int cur = RoundToInt(Clamp(_equip.EquippedArmorDefense, 0f, GearLoadout.MaxArmorDefense) * 100f);
            int nw  = RoundToInt(Clamp(a.defense, 0f, GearLoadout.MaxArmorDefense) * 100f);
            int d = nw - cur;
            return d == 0 ? "\n(= equipped)" : (d > 0 ? "\n(+" + d + "% def vs equipped)" : "\n(" + d + "% def vs equipped)");
        }

        // ── Pure math (System.Math — no UnityEngine.Mathf, keeps the VM Unity-UI-free) ──
        // NOTE on the defense ceiling: every Clamp above bounds at GearLoadout.MaxArmorDefense,
        // the ONE owner-locked cap the engine (GearLoadout.ApplyStats) applies. It is a `const
        // float`, so the compiler inlines it and this VM gains NO runtime dependency on a
        // MonoBehaviour - the purity note above still holds. What it buys is that the shop can
        // never again advertise a mitigation number the damage chain refuses to grant.
        private static int RoundToInt(float f) => (int)Math.Floor(f + 0.5f);
        private static float Max(float a, float b) => a > b ? a : b;
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        private static string Fmt1(float v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
