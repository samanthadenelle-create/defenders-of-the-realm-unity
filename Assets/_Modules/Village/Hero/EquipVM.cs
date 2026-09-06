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
// mirrors PartyShopVM / InventoryVM).
//
// Implements DeNelle.Core.UI.Mvvm.IPanelViewModel.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village.Crafting;   // VillageInventory (resolved in CreateDefault, the sole site)

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

    public readonly struct EquipComparisonLine
    {
        public readonly string Label;
        public readonly string Candidate;
        public readonly float Delta;
        public EquipComparisonLine(string label, string candidate, float delta)
        { Label = label; Candidate = candidate; Delta = delta; }
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
        private string _selectedItemId;

        private readonly List<SlotVM> _equipSlots = new List<SlotVM>();
        private readonly List<ItemVM> _compatible = new List<ItemVM>();
        private readonly List<EquipStat> _stats = new List<EquipStat>();

        /// <summary>
        /// DI-in-Open factory (UI_MVVM_MIGRATION_PLAN §1 step 5): resolves the owned-store handle
        /// ITSELF (VillageInventory.Instance, UNIONed with the party targets — WO-578) so the View
        /// no longer names VillageInventory.Instance. The party targets stay View-supplied (they wrap
        /// live scene GameObjects the pure VM can't hold). Returns the built store via
        /// <paramref name="store"/> so the View keeps its handle to dispose.
        /// </summary>
        public static EquipVM CreateDefault(IReadOnlyList<IEquipTarget> targets, Action onClose, out InventoryStore store)
        {
            store = new InventoryStore(VillageInventory.Instance, targets);
            return new EquipVM(store, targets, onClose);
        }

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
                return string.IsNullOrEmpty(cls) ? name
                    : name + " - " + cls + " - Level " + t.TargetLevel;
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

        /// <summary>The inventory card selected for detail/action presentation. Selection is VM
        /// state so every Equipment view renders the same contextual action.</summary>
        public ItemVM? SelectedItem
        {
            get
            {
                foreach (var item in _compatible)
                    if (string.Equals(item.Id, _selectedItemId, StringComparison.OrdinalIgnoreCase))
                        return item;
                return null;
            }
        }

        /// <summary>Relevant candidate stats compared with the item in the same equipped slot.</summary>
        public IReadOnlyList<EquipComparisonLine> SelectedComparison
        {
            get
            {
                var lines = new List<EquipComparisonLine>();
                var selected = SelectedItem;
                if (!selected.HasValue) return lines;
                string id = selected.Value.Id;
                string equippedId = null;
                foreach (var slot in _equipSlots)
                    if (slot.SlotKey == _selectedSlotKey && slot.Content.HasValue)
                    { equippedId = slot.Content.Value.Id; break; }

                if (_selectedSlotKey == SlotMainhand)
                {
                    var candidate = GearCatalog.FindWeapon(id);
                    var equipped = GearCatalog.FindWeapon(equippedId);
                    if (candidate != null)
                    {
                        float value = (GearStatResolver.EffectiveDamageMult(candidate, GearLevel(id)) - 1f) * 100f;
                        float baseline = equipped != null
                            ? (GearStatResolver.EffectiveDamageMult(equipped, GearLevel(equippedId)) - 1f) * 100f : 0f;
                        lines.Add(new EquipComparisonLine("DAMAGE", "+" + RoundToInt(value) + "%", value - baseline));
                    }
                }
                else if (_selectedSlotKey == SlotOffHand)
                {
                    var candidate = GearCatalog.FindWeapon(id);
                    var equipped = GearCatalog.FindWeapon(equippedId);
                    if (candidate != null)
                    {
                        float value = GearStatResolver.EffectiveDefense(candidate, GearLevel(id)) * 100f;
                        float baseline = equipped != null
                            ? GearStatResolver.EffectiveDefense(equipped, GearLevel(equippedId)) * 100f : 0f;
                        lines.Add(new EquipComparisonLine("DEFENSE", RoundToInt(value) + "%", value - baseline));
                    }
                }
                else if (_selectedSlotKey == SlotChest)
                {
                    var candidate = GearCatalog.FindArmor(id);
                    var equipped = GearCatalog.FindArmor(equippedId);
                    if (candidate != null)
                    {
                        float value = GearStatResolver.EffectiveDefense(candidate, GearLevel(id)) * 100f;
                        float baseline = equipped != null
                            ? GearStatResolver.EffectiveDefense(equipped, GearLevel(equippedId)) * 100f : 0f;
                        lines.Add(new EquipComparisonLine("DEFENSE", RoundToInt(value) + "%", value - baseline));
                        lines.Add(new EquipComparisonLine("HEALTH", "+" + Fmt1(candidate.hpBonus),
                            candidate.hpBonus - (equipped != null ? equipped.hpBonus : 0f)));
                    }
                }
                return lines;
            }
        }

        /// <summary>The party-target chips (one per assignable member). Never null.</summary>
        public IReadOnlyList<string> TargetNames
        {
            get
            {
                var list = new List<string>(_targets.Count);
                foreach (var t in _targets)
                    list.Add(t == null ? "-" : (string.IsNullOrEmpty(t.TargetName) ? "Hero" : t.TargetName));
                return list;
            }
        }

        public int ActiveTargetIndex => _activeTargetIndex;

        public string Status { get; private set; }

        /// <summary>
        /// WO-1214 Ruling 2 - the player-facing sentence explaining why the LAST <see cref="Equip"/>
        /// call was refused, or null when it went through. ADDITIVE and separate from
        /// <see cref="Status"/> on purpose: Status carries every kind of transient line ("Equipped.",
        /// "Select an item first.") so a View cannot tell a refusal from a confirmation by reading
        /// it, and EquipmentPanel.DoEquip was doing exactly that - it toasted "Equipped &lt;item&gt;."
        /// unconditionally, so a Mage who tapped a shield was TOLD it had been equipped while the
        /// seam had refused it and changed nothing. A confident lie is worse than a silent failure:
        /// it sends the player away believing the slot changed.
        /// </summary>
        public string LastRefusal { get; private set; }

        /// <summary>
        /// The one-line GRANT a FILLED slot shows ("+25% dmg" / "+35% def  +12 hp"), keyed by slot.
        /// Moved out of EquipmentPanel.GrantLine (which read GearCatalog in the View) — the projection
        /// now lives here (banned symbols are legit inside a VM). Returns "" when the slot is empty or
        /// no def resolves (accessories have no stat def yet — no fake stats). Verbatim math.
        /// </summary>
        public string GrantLineFor(string slotKey)
        {
            string id = null;
            foreach (var s in _equipSlots)
                if (s.SlotKey == slotKey && s.Content.HasValue) { id = s.Content.Value.Id; break; }
            return GrantLineForItem(slotKey, id);
        }

        /// <summary>Authoritative one-line projection for any candidate item in a slot.</summary>
        public string GrantLineForItem(string slotKey, string id)
        {
            if (string.IsNullOrEmpty(id)) return "";

            if (slotKey == SlotMainhand)
            {
                var w = GearCatalog.FindWeapon(id);
                if (w == null) return "";
                // WO-808: the grant line reads the LIVE leveled power (level 1 == authored).
                int lvl = GearLevel(id);
                int dmgPct = RoundToInt((Max(0.1f, GearStatResolver.EffectiveDamageMult(w, lvl)) - 1f) * 100f);
                return (lvl > 1 ? "Lv " + lvl + "  " : "")
                    + "+" + dmgPct + "% dmg" + (w.reach > 0f ? "  reach " + Fmt1(w.reach) + "m" : "");
            }
            if (slotKey == SlotOffHand)
            {
                // OFF-HAND items are WeaponDef ROWS (shields live in weapons.json, not armor.json).
                // This branch used to share the SlotChest path and call GearCatalog.FindArmor(id),
                // which returns null for every shield - so the off-hand slot's stat line was
                // unconditionally EMPTY. Combined with the raw `.defense` read in ApplyStats, a
                // levelled shield showed nothing anywhere and did nothing in combat.
                var o = GearCatalog.FindWeapon(id);
                if (o == null) return "";
                int oLvl = GearLevel(id);
                int oDefPct = RoundToInt(GearStatResolver.EffectiveDefense(o, oLvl) * 100f);
                return (oLvl > 1 ? "Lv " + oLvl + "  " : "") + "+" + oDefPct + "% def";
            }
            if (slotKey == SlotChest)
            {
                var a = GearCatalog.FindArmor(id);
                if (a == null) return "";
                int lvl = GearLevel(id);
                int defPct = RoundToInt(GearStatResolver.EffectiveDefense(a, lvl) * 100f);
                return (lvl > 1 ? "Lv " + lvl + "  " : "")
                    + "+" + defPct + "% def" + (a.hpBonus > 0f ? "  +" + Fmt1(a.hpBonus) + " hp" : "");
            }
            return "";
        }

        /// <summary>WO-808: the owned instance's gear level (1 baseline) — VM-side state read.</summary>
        private static int GearLevel(string id) =>
            GearProgression.GearLevelOf(
                DeNelle.Core.State.GameStateService.Instance != null
                    ? DeNelle.Core.State.GameStateService.Instance.State : null, id);

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Select an equipment slot by index; rebuilds the compatible-items list.</summary>
        public void SelectSlot(int index)
        {
            if (index < 0 || index >= _equipSlots.Count) return;
            _selectedSlotKey = _equipSlots[index].SlotKey;
            RebuildCompatible();
            Raise();
        }

        public void SelectItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            foreach (var item in _compatible)
            {
                if (!string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase)) continue;
                _selectedItemId = item.Id;
                Raise();
                return;
            }
        }

        /// <summary>Execute the single contextual action required by the approved Equipment UI.</summary>
        public void ActivateSelected()
        {
            var selected = SelectedItem;
            if (!selected.HasValue) { Status = "Select an item first."; Raise(); return; }
            if (selected.Value.Equipped) Unequip();
            else Equip(selected.Value.Id);
        }

        /// <summary>Equip an owned item into the selected slot (routes by slot kind).</summary>
        public void Equip(string itemId)
        {
            LastRefusal = null;
            if (string.IsNullOrEmpty(itemId)) { Status = "Select an item first."; Raise(); return; }
            var t = Active;
            if (t == null) { Status = "No hero to equip."; Raise(); return; }

            // WO-1214 Ruling 2 - an item the wearer cannot use is HELD, not equipped, and the
            // refusal is shown as a SENTENCE naming why (never a greyed control, never colour
            // alone: the owner is red/green colourblind). The words come from GearCatalog, the
            // same authority GearLoadout's equip seam consults, so the UI and the seam can never
            // disagree about what is legal - which is exactly how the arena and outpost loot rolls
            // each grew their own copy of the class gate and drifted.
            if (!CanWearHere(t, itemId, out string refusal))
            {
                LastRefusal = refusal;
                Status = refusal;
                // §12: the refusal is CAPTURED as well as shown. A recurrence of "it says
                // equipped but nothing changed" is then one log read, not a repro hunt.
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Equip",
                    "EQUIP REFUSED (WO-1214) at the UI seam: item='" + itemId + "' slot='" + _selectedSlotKey +
                    "' -> shown to the player as: \"" + refusal + "\" | the item is NOT equipped, NOT destroyed, " +
                    "and stays in the bag as sellable stock (Ruling 2).");
                Raise();
                return;
            }

            if (_selectedSlotKey == SlotMainhand) t.EquipWeaponById(itemId);
            else if (_selectedSlotKey == SlotOffHand) t.EquipOffHandById(itemId);
            else if (_selectedSlotKey == SlotChest) t.EquipArmorById(itemId);
            else if (_selectedSlotKey == SlotRing || _selectedSlotKey == SlotAmulet) t.EquipAccessoryById(itemId);
            else t.EquipArmorById(itemId);

            Status = "Equipped.";
            Rebuild();
            Raise();
        }

        /// <summary>
        /// WO-1214 Ruling 2/3 - may <paramref name="t"/> equip <paramref name="itemId"/> into the
        /// selected slot right now? On false, <paramref name="refusal"/> is the player-facing
        /// sentence to show (never null, never empty).
        ///
        /// Accessories are exempt: rings/amulets carry job "any" and slot-match only, and the
        /// accessory list is already built per slot. An unknown/unresolvable id is ALLOWED through
        /// so the seam still owns the final word - a UI that silently swallowed unknown ids would
        /// hide the very "no def in catalog" Warn the seam exists to emit.
        /// </summary>
        private bool CanWearHere(IEquipTarget t, string itemId, out string refusal)
        {
            refusal = null;
            if (t == null || string.IsNullOrEmpty(itemId)) return true;
            if (_selectedSlotKey == SlotRing || _selectedSlotKey == SlotAmulet) return true;

            string job = t.TargetClass;
            int level = t.TargetLevel;

            var w = GearCatalog.FindWeapon(itemId);
            if (w != null)
            {
                if (GearCatalog.CanEquipWeaponNow(w, t.EquippedWeapon, job, level, out string words, out _))
                    return true;
                refusal = words;
                return false;
            }

            var a = GearCatalog.FindArmor(itemId);
            if (a != null)
            {
                if (GearCatalog.CanEquipArmorNow(a, job, level, out string words, out _)) return true;
                refusal = words;
                return false;
            }

            return true;   // not gear we can judge here - let the seam answer.
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

            // CAP: GearLoadout.MaxArmorDefense - the SAME symbol GearLoadout.ApplyStats clamps
            // the applied value to. A local literal here is what let the panel advertise a
            // number the damage chain never granted (display 0.90 vs applied 0.70).
            float def = t != null ? t.ArmorDefense : 0f;
            float defShown = Clamp(def, 0f, GearLoadout.MaxArmorDefense);
            int defPct = RoundToInt(defShown * 100f);
            _stats.Add(new EquipStat("Defense", new BarVM(defShown, "+" + defPct + "%", "def")));
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

                // WO-1061 §2 — THE MEASUREMENT LINE. A weapon drawer that lists nothing has three
                // causes and reading the code cannot tell them apart; this line can, in ONE read:
                //   owned=0                      -> the GRANT path (equipped != owned). Not a UI bug.
                //   owned>0, rejectedClass=owned -> the class gate (D): item job never equals this class.
                //   owned>0, rejectedHand=owned  -> the hand split (C): everything owned is/isn't a shield.
                // The View's existing trace only classifies "data-empty vs built-but-broken"; it stops
                // exactly where this begins, which is why the empty drawer stayed un-diagnosable.
                // PERMANENT (CLAUDE.md §12) — flag it off one day, never strip it.
                int ownedCount = 0, rejectedHand = 0, rejectedClass = 0;
                var perItem = new System.Text.StringBuilder();

                foreach (var (w, qty) in _store.OwnedWeapons())
                {
                    if (w == null) continue;
                    ownedCount++;
                    bool handOk = offhand == w.IsOffHandItem;
                    bool fits = string.IsNullOrEmpty(job) || _store.WeaponFitsClass(w, job);
                    if (perItem.Length > 0) perItem.Append("; ");
                    perItem.Append("id=").Append(w.id)
                           .Append(" job='").Append(w.job ?? "<null>")
                           .Append("' offHand=").Append(w.IsOffHandItem)
                           .Append(" fits=").Append(fits);

                    if (!handOk) { rejectedHand++; continue; }   // shields ⇄ off-hand only
                    if (!fits) { rejectedClass++; continue; }
                    bool equipped = !string.IsNullOrEmpty(equippedId) &&
                                    string.Equals(equippedId, w.id, StringComparison.OrdinalIgnoreCase);
                    string name = string.IsNullOrEmpty(w.name) ? w.id : w.name;
                    _compatible.Add(new ItemVM(w.id, name + (qty > 1 ? " x" + qty : ""),
                        IconRoleWeapon, w.id, 0, "gold", true, w.rarity, equipped: equipped));
                }

                string verdict = ownedCount == 0
                    ? "CAUSE=grant-path (owned=0: the store holds no weapons at all - an EQUIPPED item that is not OWNED never reaches this list)"
                    : _compatible.Count > 0 ? "ok"
                    : rejectedClass >= rejectedHand
                        ? "CAUSE=class-gate (every owned weapon's job fails this wearer's class - DATA authoring, do NOT loosen the gate)"
                        : "CAUSE=hand-split (every owned weapon sits in the other hand - may be genuinely correct)";
                DeNelle.Core.Diagnostics.FlowTrace.Step("Equip",
                    $"drawer slot={_selectedSlotKey} job='{job ?? "<null>"}' owned={ownedCount} " +
                    $"-> listed={_compatible.Count} rejectedHand={rejectedHand} rejectedClass={rejectedClass} " +
                    $"equippedHere='{equippedId ?? "<none>"}' {verdict} :: {(perItem.Length > 0 ? perItem.ToString() : "<no owned weapons>")}");
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

                // WO-1061 §2 — the armor drawer's twin of the weapon measurement above. Same three
                // causes, same one-read discrimination (armor has no hand split, so the only two
                // rejections are "nothing owned" and the WEIGHT-class gate). PERMANENT (§12).
                int ownedArmor = 0, rejectedWeight = 0;
                var perArmor = new System.Text.StringBuilder();

                foreach (var (a, qty) in _store.OwnedArmor())
                {
                    if (a == null) continue;
                    ownedArmor++;
                    bool fits = string.IsNullOrEmpty(job) || _store.ArmorFitsClass(a, job);
                    if (perArmor.Length > 0) perArmor.Append("; ");
                    perArmor.Append("id=").Append(a.id)
                            .Append(" job='").Append(a.job ?? "<null>")
                            .Append("' weight='").Append(a.weight ?? "<null>")
                            .Append("' fits=").Append(fits);

                    if (!fits) { rejectedWeight++; continue; }
                    bool equipped = !string.IsNullOrEmpty(equippedId) &&
                                    string.Equals(equippedId, a.id, StringComparison.OrdinalIgnoreCase);
                    string name = string.IsNullOrEmpty(a.name) ? a.id : a.name;
                    _compatible.Add(new ItemVM(a.id, name + (qty > 1 ? " x" + qty : ""),
                        IconRoleArmor, a.id, 0, "gold", true, a.rarity, equipped: equipped));
                }

                string armorVerdict = ownedArmor == 0
                    ? "CAUSE=grant-path (owned=0: no armor in the store)"
                    : _compatible.Count > 0 ? "ok"
                    : "CAUSE=weight-gate (every owned piece fails this class's weight - DATA authoring)";
                DeNelle.Core.Diagnostics.FlowTrace.Step("Equip",
                    $"drawer slot={_selectedSlotKey} job='{job ?? "<null>"}' owned={ownedArmor} " +
                    $"-> listed={_compatible.Count} rejectedWeight={rejectedWeight} " +
                    $"equippedHere='{equippedId ?? "<none>"}' {armorVerdict} :: {(perArmor.Length > 0 ? perArmor.ToString() : "<no owned armor>")}");
            }

            // Preserve a valid selection across model refreshes; otherwise choose the first card.
            // This keeps the detail pane deterministic without manufacturing a UI-side copy.
            bool selectedStillValid = false;
            foreach (var item in _compatible)
                if (string.Equals(item.Id, _selectedItemId, StringComparison.OrdinalIgnoreCase))
                { selectedStillValid = true; break; }
            if (!selectedStillValid)
                _selectedItemId = _compatible.Count > 0 ? _compatible[0].Id : null;
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
        private static float Max(float a, float b) => a > b ? a : b;
        private static string Fmt1(float v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
