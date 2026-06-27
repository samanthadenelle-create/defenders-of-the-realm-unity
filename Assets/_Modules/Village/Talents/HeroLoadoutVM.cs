// =============================================================================
// HeroLoadoutVM — the loadout-chooser's PURE ViewModel (MVVM slice).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Talents
//
// ALL loadout STATE + LOGIC lives here, view-agnostic. Mirrors BuildingUpgradeVM:
//   * implements DeNelle.Core.UI.Mvvm.IPanelViewModel (Title / Changed / Close / Dispose)
//   * NO UnityEngine UI types; the View resolves all presentation. Unit-testable
//     without a scene (ARCHITECTURE_PRINCIPLES §2 / §2c).
//   * the View binds it, re-renders on Changed, routes user input back as commands.
//
// The chooser fills the hero's W/E/R ability slots from the SKILL-kind nodes the
// player has unlocked in the skill tree (HeroSkillTreeVM). Q is the LOCKED basic
// attack (never equippable). Equip(slot, abilityId) routes to HeroLoadout.Equip
// via HeroLoadoutAccess (rejects Q + duplicate ids). State sources:
//   * unlocked SKILL ids  = WisdomCurrencyService.Unlocked ∩ Skill-kind talent nodes,
//                            each resolving to an AbilityDef via AbilityCatalog.FindById,
//   * equipped slot map   = HeroLoadout (live hero, via HeroLoadoutAccess).
// Raises Changed on HeroLoadout.Changed + WisdomCurrencyService.Changed.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Talents
{
    /// <summary>One of the four ability slots (Q/W/E/R) in the loadout strip.</summary>
    public readonly struct LoadoutSlotVM
    {
        public readonly AbilitySlot Slot;
        public readonly string SlotKey;       // "Q"/"W"/"E"/"R"
        public readonly bool IsLocked;        // Q only (basic attack)
        public readonly string AbilityId;     // equipped id, or "" when empty
        public readonly string AbilityName;   // display name of the equipped ability, or "" when empty

        public LoadoutSlotVM(AbilitySlot slot, string slotKey, bool isLocked, string abilityId, string abilityName)
        {
            Slot = slot;
            SlotKey = slotKey;
            IsLocked = isLocked;
            AbilityId = abilityId ?? "";
            AbilityName = abilityName ?? "";
        }

        public bool IsEmpty => string.IsNullOrEmpty(AbilityId);
    }

    /// <summary>One unlocked, equippable skill the chooser can drop into a W/E/R slot.</summary>
    public readonly struct SkillChoiceVM
    {
        public readonly string AbilityId;
        public readonly string Name;
        public readonly bool IsEquipped;      // already slotted somewhere

        public SkillChoiceVM(string abilityId, string name, bool isEquipped)
        {
            AbilityId = abilityId ?? "";
            Name = name ?? "";
            IsEquipped = isEquipped;
        }
    }

    /// <summary>
    /// Pure ViewModel for the loadout chooser. Exposes the four <see cref="Slots"/>
    /// (Q locked; W/E/R fillable), the grid of unlocked <see cref="UnlockedSkills"/>,
    /// a current selection, and the Equip command. Raises <see cref="Changed"/> on any
    /// loadout / unlock change.
    /// </summary>
    public sealed class HeroLoadoutVM : IPanelViewModel, IDisposable
    {
        private readonly Action _onClose;
        private readonly Action _wisdomHandler;
        private Action _loadoutHandler;
        private HeroLoadout _loadoutSub;   // the instance we attached _loadoutHandler to (for clean detach)
        private bool _disposed;

        private readonly List<LoadoutSlotVM> _slots = new List<LoadoutSlotVM>(4);
        private readonly List<SkillChoiceVM> _choices = new List<SkillChoiceVM>();

        public HeroLoadoutVM(Action onClose)
        {
            _onClose = onClose;

            var wisdom = WisdomCurrencyService.Instance;
            if (wisdom != null)
            {
                _wisdomHandler = OnModelChanged;
                wisdom.Changed += _wisdomHandler;
            }
            SubscribeLoadout();

            Rebuild();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title => "Equip Skills";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var wisdom = WisdomCurrencyService.Instance;
            if (wisdom != null && _wisdomHandler != null) wisdom.Changed -= _wisdomHandler;
            UnsubscribeLoadout();
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>The four ability slots, ordered Q,W,E,R. Q is locked (basic attack). Never null.</summary>
        public IReadOnlyList<LoadoutSlotVM> Slots => _slots;

        /// <summary>Unlocked, equippable skills (Skill-kind talent nodes that resolve to an ability). Never null.</summary>
        public IReadOnlyList<SkillChoiceVM> UnlockedSkills => _choices;

        /// <summary>The currently picked skill id (the View's "tap a skill, then a slot" flow), or "" when none.</summary>
        public string SelectedAbilityId { get; private set; } = "";

        /// <summary>Last action / hint line for the status row.</summary>
        public string Status { get; private set; } = "Tap a skill, then a W/E/R slot to equip.";

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Pick a skill from the grid (highlights it; the next slot tap equips it).</summary>
        public void SelectSkill(string abilityId)
        {
            SelectedAbilityId = abilityId ?? "";
            Status = string.IsNullOrEmpty(SelectedAbilityId)
                ? "Tap a skill, then a W/E/R slot to equip."
                : "Now tap a W/E/R slot.";
            Raise();
        }

        /// <summary>
        /// Equip the picked skill (or an explicit <paramref name="abilityId"/>) into
        /// <paramref name="slot"/>. Rejects Q (locked) and duplicates via HeroLoadout.Equip.
        /// </summary>
        public void Equip(AbilitySlot slot, string abilityId = null)
        {
            string id = string.IsNullOrEmpty(abilityId) ? SelectedAbilityId : abilityId;
            if (string.IsNullOrEmpty(id)) { Status = "Pick a skill first."; Raise(); return; }
            if (slot == AbilitySlot.Q) { Status = "Q is the basic attack — it can't be changed."; Raise(); return; }

            if (HeroLoadoutAccess.Current == null)
            {
                Status = "No hero to equip.";
                Raise();
                return;
            }
            if (HeroLoadoutAccess.EditsLocked)
            {
                Status = "Can't change skills during battle.";
                Raise();
                return;
            }

            bool ok = HeroLoadoutAccess.Equip(slot, id);
            if (ok)
            {
                Status = "Equipped to " + SlotKeyOf(slot) + ".";
                SelectedAbilityId = "";
            }
            else
            {
                // Equip returns false for a duplicate id or a redundant equip.
                Status = "That skill is already equipped.";
            }
            // HeroLoadout.Changed fires Rebuild via the subscription; rebuild defensively too.
            Rebuild();
            Raise();
        }

        /// <summary>Slot-tap entry the View uses (equips the selected skill into this slot).</summary>
        public void OnSlotTapped(AbilitySlot slot) => Equip(slot);

        /// <summary>
        /// Skill-Tree "add to battle bar" one-tap: auto-assign the selected skill (or an explicit
        /// <paramref name="abilityId"/>) into the first free slot of the player-ASSIGNABLE EXTRA
        /// bar (the bottom-middle HUD row), via <see cref="AssignableSkillBarAccess.TryAdd"/>. This
        /// is SEPARATE from the W/E/R default loadout (which <see cref="Equip"/> fills) — the extras
        /// bar holds skill-tree-added EXTRA skills. Battle-locked + persisted; surfaces on Status.
        /// </summary>
        public void TryAdd(string abilityId = null)
        {
            string id = string.IsNullOrEmpty(abilityId) ? SelectedAbilityId : abilityId;
            if (string.IsNullOrEmpty(id)) { Status = "Pick a skill first."; Raise(); return; }
            if (AssignableSkillBarAccess.Current == null) { Status = "No hero to equip."; Raise(); return; }
            if (AssignableSkillBarAccess.EditsLocked) { Status = "Can't change skills during battle."; Raise(); return; }

            bool ok = AssignableSkillBarAccess.TryAdd(id);
            Status = ok ? "Added to your battle bar." : "No free slot (or already on the bar).";
            if (ok) SelectedAbilityId = "";
            Rebuild();
            Raise();
        }

        // ── Build (no Unity UI types) ────────────────────────────────────────────

        private void Rebuild()
        {
            BuildSlots();
            BuildChoices();
            // Drop a stale selection (the picked skill got equipped / cleared).
            if (!string.IsNullOrEmpty(SelectedAbilityId))
            {
                bool stillChoosable = false;
                foreach (var c in _choices)
                    if (string.Equals(c.AbilityId, SelectedAbilityId, StringComparison.OrdinalIgnoreCase) && !c.IsEquipped)
                    { stillChoosable = true; break; }
                if (!stillChoosable) SelectedAbilityId = "";
            }
        }

        private void BuildSlots()
        {
            _slots.Clear();
            var lo = HeroLoadoutAccess.Current;
            foreach (var slot in new[] { AbilitySlot.Q, AbilitySlot.W, AbilitySlot.E, AbilitySlot.R })
            {
                bool locked = slot == AbilitySlot.Q;
                string id = locked || lo == null ? null : lo.AbilityIdForSlot(slot);
                string name = "";
                if (locked)
                {
                    name = "Basic Attack";
                }
                else if (!string.IsNullOrEmpty(id))
                {
                    var def = AbilityCatalog.FindById(id);
                    name = def != null && !string.IsNullOrEmpty(def.Name) ? def.Name : id;
                }
                _slots.Add(new LoadoutSlotVM(slot, SlotKeyOf(slot), locked, id ?? "", name));
            }
        }

        private void BuildChoices()
        {
            _choices.Clear();
            var wisdom = WisdomCurrencyService.Instance;
            if (wisdom == null || wisdom.Unlocked == null) return;

            // Unlocked talent nodes -> Skill-kind -> abilityId -> AbilityDef. Skip dupes.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var nodeId in wisdom.Unlocked)
            {
                if (string.IsNullOrEmpty(nodeId)) continue;
                var node = HeroTalentCatalog.FindNode(nodeId);
                if (node == null) continue;

                string abilityId = AbilityIdOf(node);
                if (string.IsNullOrEmpty(abilityId)) continue;          // Stat nodes have none — skip
                if (!IsSkillKind(node, abilityId)) continue;
                if (!seen.Add(abilityId)) continue;

                var def = AbilityCatalog.FindById(abilityId);
                if (def == null) continue;                              // not a real equippable ability
                string name = !string.IsNullOrEmpty(def.Name) ? def.Name : abilityId;
                _choices.Add(new SkillChoiceVM(abilityId, name, HeroLoadoutAccess.IsEquipped(abilityId)));
            }
        }

        // ── Node kind / ability id readers (data slice fields; safe if absent) ────

        private static bool IsSkillKind(HeroTalentNodeDef n, string abilityId)
        {
            string k = ReadStringField(n, "Kind");
            if (!string.IsNullOrEmpty(k)) return k.Trim().ToLowerInvariant() == "skill";
            // No explicit kind — a node that names an ability is a Skill.
            return !string.IsNullOrEmpty(abilityId);
        }

        private static string AbilityIdOf(HeroTalentNodeDef n)
        {
            if (n == null) return "";
            return ReadStringField(n, "AbilityId") ?? "";
        }

        private static string ReadStringField(object obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var f = t.GetField(name);
            if (f != null) return f.GetValue(obj) as string;
            var p = t.GetProperty(name);
            if (p != null) return p.GetValue(obj, null) as string;
            return null;
        }

        private static string SlotKeyOf(AbilitySlot slot)
        {
            switch (slot)
            {
                case AbilitySlot.Q: return "Q";
                case AbilitySlot.W: return "W";
                case AbilitySlot.E: return "E";
                case AbilitySlot.R: return "R";
                default: return "?";
            }
        }

        // ── Subscriptions ────────────────────────────────────────────────────────

        private void SubscribeLoadout()
        {
            var lo = HeroLoadoutAccess.Current;
            if (lo == null) return;
            _loadoutHandler = OnModelChanged;
            lo.Changed += _loadoutHandler;
            _loadoutSub = lo;
        }

        private void UnsubscribeLoadout()
        {
            if (_loadoutSub != null && _loadoutHandler != null)
                _loadoutSub.Changed -= _loadoutHandler;
            _loadoutSub = null;
            _loadoutHandler = null;
        }

        private void OnModelChanged()
        {
            if (_disposed) return;
            Rebuild();
            Changed?.Invoke();
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
