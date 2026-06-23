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
        public readonly int Tier;            // 1..3 (parsed from "tier1".."tier3")
        public readonly int Column;          // 0-based branch column
        public readonly string Branch;       // branch display name (Ranged / Heal-Sustain / Control / ...)
        public readonly IReadOnlyList<string> Prereqs;
        public readonly SkillNodeKind Kind;
        public readonly string AbilityId;    // non-empty for Skill-kind nodes; "" for Stat
        public readonly bool Owned;
        public readonly bool CanUnlock;
        public readonly string LockReason;   // why it's locked (shown instead of bare "LOCKED")
        public readonly int WisdomCost;
        public readonly bool IsEquipped;

        public SkillNodeVM(string id, string name, int tier, int column, string branch,
                           IReadOnlyList<string> prereqs, SkillNodeKind kind, string abilityId,
                           bool owned, bool canUnlock, string lockReason, int wisdomCost, bool isEquipped)
        {
            Id = id;
            Name = name;
            Tier = tier;
            Column = column;
            Branch = branch;
            Prereqs = prereqs ?? Array.Empty<string>();
            Kind = kind;
            AbilityId = abilityId ?? "";
            Owned = owned;
            CanUnlock = canUnlock;
            LockReason = lockReason ?? "";
            WisdomCost = wisdomCost;
            IsEquipped = isEquipped;
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
        // Ordered, de-duped branch column labels (index == SkillNodeVM.Column).
        private readonly List<string> _branches = new List<string>();

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
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>Every node in the tree (branch column + tier row carried on each). Never null.</summary>
        public IReadOnlyList<SkillNodeVM> Nodes => _nodes;

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

        /// <summary>Unlock a node: spends Wisdom + validates prereqs via WisdomCurrencyService.Unlock.</summary>
        public void Unlock(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            var svc = WisdomCurrencyService.Instance;
            if (svc != null) svc.Unlock(nodeId);
            Rebuild();
            Raise();
        }

        // ── Build the node rows (no Unity types) ─────────────────────────────────

        private void Rebuild()
        {
            _nodes.Clear();
            _branches.Clear();

            var tree = HeroTalentCatalog.GetTree(_heroSlug);
            Title = tree != null && !string.IsNullOrEmpty(tree.DisplayName)
                ? tree.DisplayName + " Skills"
                : "Skills";
            if (tree == null || tree.Nodes == null) return;

            var svc = WisdomCurrencyService.Instance;
            int wisdom = svc != null ? svc.Wisdom : 0;
            var unlocked = BuildUnlockedSet(svc);

            // Resolve branch columns first (stable left-to-right order from first appearance).
            var columnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var n in tree.Nodes)
            {
                if (n == null) continue;
                string branch = BranchKey(n);
                if (!columnIndex.ContainsKey(branch))
                {
                    columnIndex[branch] = _branches.Count;
                    _branches.Add(BranchLabel(branch));
                }
            }

            foreach (var n in tree.Nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.Id)) continue;

                bool owned = unlocked.Contains(n.Id);
                bool canUnlock = !owned && HeroTalentCatalog.CanUnlock(n.Id, wisdom, unlocked);
                string reason = owned ? "" : LockReasonFor(n, wisdom, unlocked);

                string branch = BranchKey(n);
                int col = columnIndex.TryGetValue(branch, out var ci) ? ci : 0;

                SkillNodeKind kind = KindOf(n);
                string abilityId = AbilityIdOf(n);
                bool equipped = kind == SkillNodeKind.Skill
                                && !string.IsNullOrEmpty(abilityId)
                                && HeroLoadoutAccess.IsEquipped(abilityId);

                _nodes.Add(new SkillNodeVM(
                    n.Id,
                    string.IsNullOrEmpty(n.Name) ? n.Id : n.Name,
                    TierIndex(n.Tier),
                    col,
                    BranchLabel(branch),
                    n.Prerequisites != null ? new List<string>(n.Prerequisites) : null,
                    kind,
                    abilityId,
                    owned,
                    canUnlock,
                    reason,
                    n.Cost,
                    equipped));
            }
        }

        // ── Lock-reason (the specific "why", not bare LOCKED) ─────────────────────

        private static string LockReasonFor(HeroTalentNodeDef n, int wisdom, HashSet<string> unlocked)
        {
            if (n == null) return "Locked";
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

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
