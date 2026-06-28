// =============================================================================
// HeroSkillTreeVM — the Knight skill-tree panel's PURE ViewModel (MVVM slice).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Talents
//
// ALL skill-tree STATE + LOGIC lives here, view-agnostic. Mirrors BuildingUpgradeVM:
//   * implements DeNelle.Core.UI.Mvvm.IPanelViewModel (Title / Changed / Close / Dispose)
//   * NO UnityEngine UI types (no GameObject/Image/Sprite/RectTransform/Color); the
//     View resolves all presentation. The VM is unit-testable without a scene
//     (ARCHITECTURE_PRINCIPLES §2 / §2c).
//   * the View binds it, re-renders on Changed, and routes user input back as
//     commands; the View NEVER reads game state (ui-mvvm-binding-seam rule).
//
// V1 = solo Knight (combat-pivot north star). The tree is the "knight" HeroTalentTreeDef
// (HeroTalentCatalog), laid out column-per-branch (Ranged / Heal-Sustain / Control),
// tier rows top-down. Owned/CanUnlock/LockReason come from WisdomCurrencyService +
// HeroTalentCatalog.CanUnlock. Unlock(nodeId) spends through WisdomCurrencyService.Unlock.
//
// NODE KIND (parallel data slice): HeroTalentNodeDef carries an optional `kind`
// (Skill | Stat) + `abilityId`. A Skill-kind node grants an equippable ability the
// loadout panel can slot; a Stat node is a passive stat bump. We read both off the
// def reflectively-free (the fields exist in the combined tree at gate time).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Talents
{
    /// <summary>Whether a skill-tree node grants an equippable ability or a passive stat.</summary>
    public enum SkillNodeKind { Skill, Stat }

    /// <summary>
    /// One skill-tree node's view-agnostic payload: identity + placement (branch column,
    /// tier row), prerequisite ids (for the View's parent->child lines), unlock state
    /// (Owned / CanUnlock / LockReason), Wisdom cost, kind, and whether the granted ability
    /// is currently equipped in a loadout slot. A readonly struct (no per-row allocation).
    /// </summary>
    public readonly struct SkillNodeVM
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Tier;            // 1..4 (parsed from "tier1".."tier4"); 0 for shared pool nodes
        public readonly int Column;          // 0-based grid column == Slot-1 (v2: slot 1..5)
        public readonly string Branch;       // legacy branch label (unused by the v2 slot-grid view)
        public readonly IReadOnlyList<string> Prereqs;
        public readonly SkillNodeKind Kind;
        public readonly string AbilityId;    // non-empty for Skill-kind nodes; "" for Stat
        public readonly string IconPath;     // Resources sprite path (Talents/<hero>/<hero>_NN), may be ""
        public readonly bool IsCapstone;     // tier-4 capstone (distinct frame)
        public readonly bool IsShared;       // a Shared Universal pool node
        public readonly bool Owned;
        public readonly bool CanUnlock;
        public readonly string LockReason;   // why it's locked (shown instead of bare "LOCKED")
        public readonly int WisdomCost;
        public readonly bool IsEquipped;
        public readonly bool IsPending;      // staged in the current plan (not yet committed)
        public readonly float X;             // node-graph canvas position (0..1; -1 = unset/auto)
        public readonly float Y;             // 0=top, 1=bottom

        public SkillNodeVM(string id, string name, int tier, int column, string branch,
                           IReadOnlyList<string> prereqs, SkillNodeKind kind, string abilityId,
                           string iconPath, bool isCapstone, bool isShared,
                           bool owned, bool canUnlock, string lockReason, int wisdomCost, bool isEquipped,
                           bool isPending, float x, float y)
        {
            Id = id;
            Name = name;
            Tier = tier;
            Column = column;
            Branch = branch;
            Prereqs = prereqs ?? Array.Empty<string>();
            Kind = kind;
            AbilityId = abilityId ?? "";
            IconPath = iconPath ?? "";
            IsCapstone = isCapstone;
            IsShared = isShared;
            Owned = owned;
            CanUnlock = canUnlock;
            LockReason = lockReason ?? "";
            WisdomCost = wisdomCost;
            IsEquipped = isEquipped;
            IsPending = isPending;
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Pure ViewModel for the Knight skill tree. Exposes <see cref="Nodes"/> (one
    /// <see cref="SkillNodeVM"/> per authored node) + the wallet header (RemainingWisdom,
    /// RemainingSkillPoints) + the column/branch labels. Raises <see cref="Changed"/>
    /// after each unlock and on any WisdomCurrencyService change.
    /// </summary>
    public sealed class HeroSkillTreeVM : IPanelViewModel, IDisposable
    {
        // V1 hero slug — solo Knight north star. Swap to a ctor arg when multi-hero lands.
        public const string HeroSlug = "knight";

        private readonly string _heroSlug;
        private readonly Action _onClose;
        private readonly Action _wisdomHandler;
        private bool _disposed;

        private readonly List<SkillNodeVM> _nodes = new List<SkillNodeVM>();
        private readonly List<SkillNodeVM> _shared = new List<SkillNodeVM>();
        // Ordered, de-duped branch column labels (index == SkillNodeVM.Column).
        private readonly List<string> _branches = new List<string>();
        // Plan→CONFIRM: nodes staged this session but not yet committed/spent.
        private readonly HashSet<string> _pending = new HashSet<string>(StringComparer.Ordinal);

        // Single-screen folds (owner 2026-06-28): the currently SELECTED node (drives the
        // detail/description panel) + a mirror of the player's QUICK-SWAP bar (slots 1..4)
        // so a player can read a perk AND assign an owned skill without a second screen.
        private string _selectedId = "";
        private readonly List<LoadoutSlotVM> _quickSlots = new List<LoadoutSlotVM>(AssignableSkillBar.SlotCount);
        private Action _barHandler;
        private AssignableSkillBar _barSub;

        public HeroSkillTreeVM(Action onClose, string heroSlug = HeroSlug)
        {
            _heroSlug = string.IsNullOrEmpty(heroSlug) ? HeroSlug : heroSlug;
            _onClose = onClose;

            var svc = WisdomCurrencyService.Instance;
            if (svc != null)
            {
                _wisdomHandler = Raise;
                svc.Changed += _wisdomHandler;
            }
            SubscribeBar();

            Rebuild();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title { get; private set; } = "Skills";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var svc = WisdomCurrencyService.Instance;
            if (svc != null && _wisdomHandler != null) svc.Changed -= _wisdomHandler;
            UnsubscribeBar();
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>Every hero-tree node (slot column + tier row carried on each). Never null.</summary>
        public IReadOnlyList<SkillNodeVM> Nodes => _nodes;

        /// <summary>The 8 Shared Universal pool nodes (v2 strip). Never null.</summary>
        public IReadOnlyList<SkillNodeVM> Shared => _shared;

        /// <summary>Branch column display names, left-to-right (index == node.Column). Never null.</summary>
        public IReadOnlyList<string> Branches => _branches;

        /// <summary>Current Wisdom balance (the talent-unlock currency).</summary>
        public int RemainingWisdom
        {
            get { var svc = WisdomCurrencyService.Instance; return svc != null ? svc.Wisdom : 0; }
        }

        /// <summary>Unspent hero skill points (display companion to Wisdom).</summary>
        public int RemainingSkillPoints
        {
            get
            {
                var sk = DeNelle.Core.Progression.SkillSystem.Instance;
                return sk != null ? sk.AvailablePoints : 0;
            }
        }

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Unlock a node IMMEDIATELY (legacy path): spends Wisdom + validates via the service.
        /// The node-graph View uses the plan→CONFIRM flow (Stage/Commit) instead.</summary>
        public void Unlock(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            var svc = WisdomCurrencyService.Instance;
            if (svc != null) svc.Unlock(nodeId);
            _pending.Remove(nodeId);
            Rebuild();
            Raise();
        }

        // ── Plan → CONFIRM flow (node-graph) ─────────────────────────────────────

        /// <summary>Total Wisdom the current staged plan would spend.</summary>
        public int PendingCost
        {
            get
            {
                int sum = 0;
                foreach (var id in _pending)
                {
                    var n = HeroTalentCatalog.FindNode(id);
                    if (n != null) sum += n.Cost;
                }
                return sum;
            }
        }

        /// <summary>Count of nodes staged in the current plan.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>True when there is a non-empty, affordable plan to commit.</summary>
        public bool CanCommit
        {
            get
            {
                var svc = WisdomCurrencyService.Instance;
                int wisdom = svc != null ? svc.Wisdom : 0;
                return _pending.Count > 0 && PendingCost <= wisdom;
            }
        }

        /// <summary>Stage a node into the plan if reachable + affordable within the remaining budget
        /// (treats already-staged nodes as tentatively owned so a chain can be planned in one pass).</summary>
        public void Stage(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || _pending.Contains(nodeId)) return;
            var svc = WisdomCurrencyService.Instance;
            if (svc == null) return;
            var node = HeroTalentCatalog.FindNode(nodeId);
            if (node == null) return;

            var owned = BuildUnlockedSet(svc);
            if (owned.Contains(nodeId)) return;                 // already owned
            var effective = Effective(owned);                   // owned ∪ pending
            int budget = svc.Wisdom - PendingCost;              // Wisdom left after the staged plan
            if (!HeroTalentCatalog.CanUnlock(nodeId, budget, effective)) return;
            if (node.Cost > budget) return;

            _pending.Add(nodeId);
            Rebuild();
            Raise();
        }

        /// <summary>Remove a node from the plan, and cascade-drop any staged node that depended on it.</summary>
        public void Unstage(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || !_pending.Remove(nodeId)) return;
            PrunePending();
            Rebuild();
            Raise();
        }

        /// <summary>Commit the staged plan: unlock every pending node (tier order) via the service, then clear.</summary>
        public void Commit()
        {
            var svc = WisdomCurrencyService.Instance;
            if (svc == null || _pending.Count == 0) return;

            // Dependency-safe order: shared (no prereqs) + by tier ascending, so a parent
            // is always committed before its child.
            var ordered = new List<string>(_pending);
            ordered.Sort((a, b) => TierOf(a).CompareTo(TierOf(b)));
            FlowTrace.Step("SkillTree", "commit plan: " + ordered.Count + " node(s), -" + PendingCost + " wisdom");
            foreach (var id in ordered) svc.Unlock(id);   // each re-validates prereq+cost+capstone

            _pending.Clear();
            Rebuild();
            Raise();
        }

        /// <summary>Discard the staged plan without spending.</summary>
        public void CancelPlan()
        {
            if (_pending.Count == 0) return;
            _pending.Clear();
            Rebuild();
            Raise();
        }

        // owned ∪ pending — the "tentatively owned" set used for staging validation + node state.
        private HashSet<string> Effective(HashSet<string> owned)
        {
            var set = new HashSet<string>(owned, StringComparer.Ordinal);
            foreach (var id in _pending) set.Add(id);
            return set;
        }

        // Drop staged nodes whose prerequisites are no longer satisfied by owned ∪ remaining-pending
        // (iterate to a fixpoint so a whole staged chain collapses when its root is unstaged).
        private void PrunePending()
        {
            var svc = WisdomCurrencyService.Instance;
            if (svc == null) { _pending.Clear(); return; }
            var owned = BuildUnlockedSet(svc);
            bool changed = true;
            while (changed)
            {
                changed = false;
                var effective = Effective(owned);
                foreach (var id in new List<string>(_pending))
                {
                    var n = HeroTalentCatalog.FindNode(id);
                    if (n == null) { _pending.Remove(id); changed = true; continue; }
                    if (n.Prerequisites == null) continue;
                    foreach (var pr in n.Prerequisites)
                    {
                        if (string.IsNullOrEmpty(pr)) continue;
                        if (!effective.Contains(pr)) { _pending.Remove(id); changed = true; break; }
                    }
                }
            }
        }

        private static int TierOf(string nodeId)
        {
            var n = HeroTalentCatalog.FindNode(nodeId);
            return n != null ? TierIndex(n.Tier) : 0;   // shared (tier "shared") -> 1 via default
        }

        // ── Build the node rows (no Unity types) ─────────────────────────────────

        private void Rebuild()
        {
            _nodes.Clear();
            _shared.Clear();
            _branches.Clear();

            var tree = HeroTalentCatalog.GetTree(_heroSlug);
            Title = tree != null && !string.IsNullOrEmpty(tree.DisplayName)
                ? tree.DisplayName + " Skills"
                : "Skills";

            var svc = WisdomCurrencyService.Instance;
            int wisdom = svc != null ? svc.Wisdom : 0;
            var owned = BuildUnlockedSet(svc);
            _pending.RemoveWhere(owned.Contains);     // drop anything committed/owned externally
            var effective = Effective(owned);          // owned ∪ pending = tentatively owned
            int budget = wisdom - PendingCost;          // Wisdom left after the staged plan

            // ── Hero tree: v2 slot-grid (column == slot-1, row == tier 1..4). ───────
            if (tree != null && tree.Nodes != null)
            {
                foreach (var n in tree.Nodes)
                {
                    if (n == null || string.IsNullOrEmpty(n.Id)) continue;
                    // Column from the explicit v2 slot (1..5); fall back to legacy branch column.
                    int col = n.Slot > 0 ? n.Slot - 1 : LegacyColumn(n);
                    _nodes.Add(BuildNode(n, col, isShared: false, budget, owned, effective));
                }
            }

            // ── Shared Universal pool (8 free-standing nodes any hero may draw). ────
            var shared = HeroTalentCatalog.SharedNodes;
            if (shared != null)
            {
                for (int i = 0; i < shared.Count; i++)
                {
                    var n = shared[i];
                    if (n == null || string.IsNullOrEmpty(n.Id)) continue;
                    int col = n.Slot > 0 ? n.Slot - 1 : i;
                    _shared.Add(BuildNode(n, col, isShared: true, budget, owned, effective));
                }
            }

            BuildQuickSlots();
        }

        private SkillNodeVM BuildNode(HeroTalentNodeDef n, int col, bool isShared, int budget,
                                      HashSet<string> owned, HashSet<string> effective)
        {
            bool isOwned = owned.Contains(n.Id);
            bool isPending = _pending.Contains(n.Id);
            // "CanUnlock" for the View = can be STAGED now: not owned/pending, reachable
            // (prereqs in owned∪pending), capstone-legal, affordable within the remaining budget.
            bool canStage = !isOwned && !isPending
                            && HeroTalentCatalog.CanUnlock(n.Id, budget, effective)
                            && n.Cost <= budget;
            string reason = (isOwned || isPending) ? "" : LockReasonFor(n, budget, effective);

            SkillNodeKind kind = KindOf(n);
            string abilityId = AbilityIdOf(n);
            bool equipped = kind == SkillNodeKind.Skill
                            && !string.IsNullOrEmpty(abilityId)
                            && AssignableSkillBarAccess.IsAssigned(abilityId);

            int tier = isShared ? 0 : TierIndex(n.Tier);

            return new SkillNodeVM(
                n.Id,
                string.IsNullOrEmpty(n.Name) ? n.Id : n.Name,
                tier,
                col,
                isShared ? "Shared" : BranchLabel(BranchKey(n)),
                n.Prerequisites != null ? new List<string>(n.Prerequisites) : null,
                kind,
                abilityId,
                n.IconPath,
                !isShared && tier >= 4,   // capstone
                isShared,
                isOwned,
                canStage,
                reason,
                n.Cost,
                equipped,
                isPending,
                n.X,
                n.Y);
        }

        // Legacy fallback when a node has no v2 slot: derive a column from its branch.
        private int LegacyColumn(HeroTalentNodeDef n)
        {
            string branch = BranchKey(n);
            int idx = _branches.IndexOf(BranchLabel(branch));
            if (idx >= 0) return idx;
            _branches.Add(BranchLabel(branch));
            return _branches.Count - 1;
        }

        // ── Lock-reason (the specific "why", not bare LOCKED) ─────────────────────

        private static string LockReasonFor(HeroTalentNodeDef n, int wisdom, HashSet<string> unlocked)
        {
            if (n == null) return "Locked";
            // v2 capstone exclusivity — once one Tier-4 is taken, the others read this
            // (dominant reason so the panel dims every other capstone clearly). A hero
            // respec clears the unlocked set and re-frees the choice.
            if (HeroTalentCatalog.IsCapstone(n) && HeroTalentCatalog.AnotherCapstoneUnlocked(n.Id, unlocked))
                return "One capstone per hero";
            // Prereq gate first (the structural blocker).
            if (n.Prerequisites != null)
            {
                foreach (var pr in n.Prerequisites)
                {
                    if (string.IsNullOrEmpty(pr)) continue;
                    if (unlocked == null || !unlocked.Contains(pr))
                    {
                        var prNode = HeroTalentCatalog.FindNode(pr);
                        string prName = prNode != null && !string.IsNullOrEmpty(prNode.Name) ? prNode.Name : pr;
                        return "Requires " + prName;
                    }
                }
            }
            // Then affordability.
            if (wisdom < n.Cost)
                return "Needs " + n.Cost + " Wisdom (have " + wisdom + ")";
            return "Locked";
        }

        // ── Node kind / ability id readers (data slice fields; safe if absent) ────
        // The parallel data slice adds `kind` ("skill"|"stat") + `abilityId` to
        // HeroTalentNodeDef. Until both ship in the combined tree these readers degrade
        // gracefully: a node with no abilityId is treated as a Stat node.

        private static SkillNodeKind KindOf(HeroTalentNodeDef n)
        {
            if (n == null) return SkillNodeKind.Stat;
            string k = ReadStringField(n, "Kind");
            if (!string.IsNullOrEmpty(k))
                return k.Trim().ToLowerInvariant() == "skill" ? SkillNodeKind.Skill : SkillNodeKind.Stat;
            // No explicit kind — a node that names an ability is a Skill, else a Stat.
            return string.IsNullOrEmpty(AbilityIdOf(n)) ? SkillNodeKind.Stat : SkillNodeKind.Skill;
        }

        private static string AbilityIdOf(HeroTalentNodeDef n)
        {
            if (n == null) return "";
            return ReadStringField(n, "AbilityId") ?? "";
        }

        // Field/property read tolerant of the data slice landing slightly differently
        // (field vs property, present or not) — no NRE, no hard compile coupling.
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

        // ── Branch derivation (column-per-branch) ────────────────────────────────
        // The catalog node carries an optional `branch` ("ranged"/"heal"/"control").
        // Until that ships we fall back to the node's Column letter so the panel still
        // lays out column-per-branch (a/b/c -> Ranged/Heal-Sustain/Control).

        private static string BranchKey(HeroTalentNodeDef n)
        {
            string b = ReadStringField(n, "Branch");
            if (!string.IsNullOrEmpty(b)) return b.Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(n?.Column) ? "a" : n.Column.Trim().ToLowerInvariant();
        }

        private static string BranchLabel(string key)
        {
            switch ((key ?? "").ToLowerInvariant())
            {
                case "ranged": case "a": return "Ranged";
                case "heal": case "heal-sustain": case "sustain": case "b": return "Heal-Sustain";
                case "control": case "c": return "Control";
                default: return char.ToUpper(key[0]) + (key.Length > 1 ? key.Substring(1) : "");
            }
        }

        private static int TierIndex(string tier)
        {
            switch ((tier ?? "tier1").Trim().ToLowerInvariant())
            {
                case "tier4": return 4;
                case "tier3": return 3;
                case "tier2": return 2;
                default: return 1;
            }
        }

        private static HashSet<string> BuildUnlockedSet(WisdomCurrencyService svc)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (svc != null && svc.Unlocked != null)
                foreach (var id in svc.Unlocked)
                    if (!string.IsNullOrEmpty(id)) set.Add(id);
            return set;
        }

        // ── Selection + detail panel (read what a perk does BEFORE confirming) ───
        // The View calls Select(id) on every node tap. Selection drives the detail
        // strip (name + description). For an ACTIONABLE node we also fold the plan
        // toggle in here (stage/unstage), so one tap both reads AND plans it; a locked
        // node just updates the detail so the player can read why it's gated.

        /// <summary>Select a node (updates the detail strip) and, if it's actionable, stage/unstage it.</summary>
        public void Select(string nodeId)
        {
            _selectedId = nodeId ?? "";
            FlowTrace.Step("SkillTree", "select node " + _selectedId);
            if (string.IsNullOrEmpty(_selectedId)) { Raise(); return; }
            if (_pending.Contains(_selectedId)) { Unstage(_selectedId); return; }  // Unstage raises (selection kept)
            int before = _pending.Count;
            Stage(_selectedId);                                                     // Stage raises if it took
            if (_pending.Count == before) Raise();                                  // locked/owned — refresh detail only
        }

        /// <summary>True when a real node is selected (the detail strip has content).</summary>
        public bool HasSelection => HeroTalentCatalog.FindNode(_selectedId) != null;

        /// <summary>Display name of the selected node, or "" when none.</summary>
        public string SelectedNodeName
        {
            get { var n = HeroTalentCatalog.FindNode(_selectedId); return n == null ? "" : (string.IsNullOrEmpty(n.Name) ? n.Id : n.Name); }
        }

        /// <summary>What the selected perk does — the node's authored description, with a
        /// graceful fallback to the unlocked ability / effect summary when none is authored.</summary>
        public string SelectedNodeDescription
        {
            get
            {
                var n = HeroTalentCatalog.FindNode(_selectedId);
                if (n == null) return "";
                return string.IsNullOrEmpty(n.Description) ? DescribeFallback(n) : n.Description;
            }
        }

        /// <summary>Owned / planned / cost / lock-reason line for the selected node.</summary>
        public string SelectedNodeStateLine
        {
            get
            {
                var n = HeroTalentCatalog.FindNode(_selectedId);
                if (n == null) return "";
                var svc = WisdomCurrencyService.Instance;
                var owned = BuildUnlockedSet(svc);
                if (owned.Contains(n.Id)) return "Owned";
                if (_pending.Contains(n.Id)) return "Planned  ·  -" + n.Cost + " Wisdom";
                int budget = (svc != null ? svc.Wisdom : 0) - PendingCost;
                var effective = Effective(owned);
                if (HeroTalentCatalog.CanUnlock(n.Id, budget, effective) && n.Cost <= budget)
                    return "Costs " + n.Cost + " Wisdom  ·  tap the node to plan it";
                return LockReasonFor(n, budget, effective);
            }
        }

        /// <summary>The ability id the selected node grants IF it is an OWNED, assignable skill — else "".
        /// Non-empty means the quick-swap row can drop this skill into a slot 1..4.</summary>
        public string SelectedAssignAbilityId
        {
            get
            {
                var n = HeroTalentCatalog.FindNode(_selectedId);
                if (n == null) return "";
                string abilityId = AbilityIdOf(n);
                if (string.IsNullOrEmpty(abilityId)) return "";
                if (!BuildUnlockedSet(WisdomCurrencyService.Instance).Contains(n.Id)) return ""; // owned only
                return AbilityCatalog.FindById(abilityId) != null ? abilityId : "";
            }
        }

        /// <summary>True when the selected node is an owned skill that can be assigned to the quick-swap bar.</summary>
        public bool SelectedIsAssignable => !string.IsNullOrEmpty(SelectedAssignAbilityId);

        // Best-available text when a node carries no authored description string.
        private static string DescribeFallback(HeroTalentNodeDef n)
        {
            if (n == null) return "";
            string ability = AbilityIdOf(n);
            if (!string.IsNullOrEmpty(ability))
            {
                var def = AbilityCatalog.FindById(ability);
                if (def != null)
                    return (string.IsNullOrEmpty(def.Name) ? ability : def.Name)
                         + (string.IsNullOrEmpty(def.Effect) ? "" : " — " + def.Effect);
                return "Unlocks ability: " + ability;
            }
            if (n.Effect != null && !string.IsNullOrEmpty(n.Effect.Type))
                return n.Effect.Type + (n.Effect.Value != 0f ? " " + n.Effect.Value : "");
            return "Passive talent.";
        }

        // ── Quick-swap bar (folds in the loadout screen) ─────────────────────────

        /// <summary>The player's quick-swap slots 1..4 (mirror of AssignableSkillBar). Never null.</summary>
        public IReadOnlyList<LoadoutSlotVM> QuickSlots => _quickSlots;

        /// <summary>Last quick-swap action / hint line.</summary>
        public string QuickSwapStatus { get; private set; } = "Select an owned skill, then tap a slot (1-4).";

        /// <summary>Assign the SELECTED owned skill into quick-swap <paramref name="slotIndex"/>; with no
        /// assignable selection, tapping a filled slot clears it. Battle-locked + persisted via the bar.</summary>
        public void AssignSelectedToSlot(int slotIndex)
        {
            string id = SelectedAssignAbilityId;
            if (string.IsNullOrEmpty(id))
            {
                if (AssignableSkillBarAccess.EditsLocked) { QuickSwapStatus = "Can't change skills during battle."; Raise(); return; }
                bool cleared = AssignableSkillBarAccess.Clear(slotIndex);
                QuickSwapStatus = cleared ? "Slot " + (slotIndex + 1) + " cleared."
                                          : "Select an owned skill, then tap a slot (1-4).";
                FlowTrace.Step("SkillTree", "quickswap clear slot " + slotIndex + " => " + cleared);
                Rebuild(); Raise();
                return;
            }
            if (AssignableSkillBarAccess.EditsLocked) { QuickSwapStatus = "Can't change skills during battle."; Raise(); return; }
            bool ok = AssignableSkillBarAccess.Assign(slotIndex, id);
            QuickSwapStatus = ok ? SelectedNodeName + " → quick-swap " + (slotIndex + 1) + "."
                                 : "That skill is already on the bar.";
            FlowTrace.Step("SkillTree", "quickswap assign " + id + " -> slot " + slotIndex + " => " + ok);
            Rebuild(); Raise();
        }

        private void BuildQuickSlots()
        {
            _quickSlots.Clear();
            var bar = AssignableSkillBarAccess.Current;
            for (int i = 0; i < AssignableSkillBar.SlotCount; i++)
            {
                string id = bar != null ? bar.AbilityIdForSlot(i) : null;
                string name = "";
                if (!string.IsNullOrEmpty(id))
                {
                    var def = AbilityCatalog.FindById(id);
                    name = def != null && !string.IsNullOrEmpty(def.Name) ? def.Name : id;
                }
                _quickSlots.Add(new LoadoutSlotVM(i, (i + 1).ToString(), id ?? "", name));
            }
        }

        private void SubscribeBar()
        {
            var bar = AssignableSkillBarAccess.Current;
            if (bar == null) return;
            _barHandler = OnBarChanged;
            bar.Changed += _barHandler;
            _barSub = bar;
        }

        private void UnsubscribeBar()
        {
            if (_barSub != null && _barHandler != null) _barSub.Changed -= _barHandler;
            _barSub = null;
            _barHandler = null;
        }

        private void OnBarChanged()
        {
            if (_disposed) return;
            Rebuild();
            Raise();
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
