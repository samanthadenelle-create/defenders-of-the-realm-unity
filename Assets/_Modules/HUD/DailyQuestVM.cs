// =============================================================================
// DailyQuestVM — the PURE ViewModel behind DailyQuestHud (strict-MVVM Silo E).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// ALL daily-quest state + logic that used to live in the DailyQuestHud VIEW now
// lives here, view-agnostic (mirrors the gold-standard BuildingUpgradeVM):
//   * implements DeNelle.Core.UI.Mvvm.IPanelViewModel (Title / Changed / Close / Dispose)
//   * NO UnityEngine UI types — unit-testable without a scene (ARCH §2/§2c).
//   * projects DailyQuestService.Today into ItemVM tiles (Equipped = Completed) +
//     per-quest helpers (ProgressText / RewardFor / FlavorFor) so the View reads
//     purely from vm.* and never touches DailyQuestService / DailyQuestCatalog.
//   * owns the row SELECTION (which quest the detail card inspects) as VM state +
//     a Select(id) command; the View only renders vm.SelectedId.
//
// The reward AUTO-DISPENSES on completion (DEF-223 bridge) so there is NO claim
// command. A free-reroll command is exposed for completeness (the service caps it
// per day); the HUD does not currently surface a reroll button, so behaviour is
// preserved — the command is dormant until a View wires it.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Quests;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.HUD
{
    /// <summary>
    /// Pure ViewModel for the daily-quest panel. Exposes today's quests as
    /// <see cref="Quests"/> (one <see cref="ItemVM"/> each, Equipped = Completed) plus
    /// per-quest projections (progress / reward / flavor) and a tracked SELECTION.
    /// Raises <see cref="Changed"/> whenever the service reports a set change.
    /// </summary>
    public sealed class DailyQuestVM : IPanelViewModel, IDisposable
    {
        // ── Seam: everything the VM reads about daily quests, so tests inject a fake
        //    and the real path resolves the singleton + catalog in CreateDefault only. ──
        public interface ISource
        {
            event Action Changed;                                   // DailyQuestService.SetChanged
            IReadOnlyList<DailyQuestInstance> TodayQuests { get; }  // DailyQuestService.Today.Quests
            DailyQuestSlotReward RewardForSlot(string slot);        // DailyQuestCatalog.RewardFor(slot)
            DailyQuestInstance Reroll(string slot);                 // DailyQuestService.Reroll(slot)
        }

        /// <summary>Projected, UI-free reward readout for one quest slot (all zero when unrewarded).</summary>
        public readonly struct RewardInfo
        {
            public readonly int Crystals;
            public readonly int Food;
            public readonly int Glimmer;
            public readonly int Wisdom;
            public readonly bool RandomItem;
            public RewardInfo(int crystals, int food, int glimmer, int wisdom, bool randomItem)
            {
                Crystals = crystals; Food = food; Glimmer = glimmer;
                Wisdom = wisdom; RandomItem = randomItem;
            }
        }

        private readonly ISource _source;
        private readonly Action _onClose;
        private readonly Action _changedHandler;
        private bool _disposed;

        private readonly List<ItemVM> _quests = new List<ItemVM>();
        private readonly Dictionary<string, string> _progressById = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _flavorById   = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _celebById    = new Dictionary<string, string>();
        private readonly Dictionary<string, RewardInfo> _rewardById = new Dictionary<string, RewardInfo>();

        private string _selectedId;

        public static DailyQuestVM CreateDefault(Action onClose)
            => new DailyQuestVM(new ServiceSource(), onClose);

        public DailyQuestVM(ISource source, Action onClose)
        {
            _source = source;
            _onClose = onClose;
            if (_source != null)
            {
                _changedHandler = Rebuild;
                _source.Changed += _changedHandler;
            }
            Rebuild();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────
        public event Action Changed;
        public string Title => "Daily Quests";
        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_source != null && _changedHandler != null) _source.Changed -= _changedHandler;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>One tile per daily quest (Name = resolved label, Equipped = Completed). Never null.</summary>
        public IReadOnlyList<ItemVM> Quests => _quests;

        /// <summary>True when today's set is empty (View shows the "no daily quests" empty state).</summary>
        public bool IsEmpty => _quests.Count == 0;

        /// <summary>The quest the detail card inspects (defaults to the first quest).</summary>
        public string SelectedId => _selectedId;

        /// <summary>"2 / 5" progress-toward-target line for a quest id.</summary>
        public string ProgressText(string id) =>
            id != null && _progressById.TryGetValue(id, out var p) ? p : "";

        /// <summary>Readable slot-category flavor line for a quest id.</summary>
        public string FlavorFor(string id) =>
            id != null && _flavorById.TryGetValue(id, out var f) ? f : "";

        /// <summary>Projected reward readout for a quest id (all zero when unrewarded).</summary>
        public RewardInfo RewardFor(string id) =>
            id != null && _rewardById.TryGetValue(id, out var r) ? r : default;

        /// <summary>Stable per-quest key the View uses to fire a completion toast exactly once.</summary>
        public string CelebrationKeyFor(string id) =>
            id != null && _celebById.TryGetValue(id, out var k) ? k : id;

        /// <summary>True when the given quest is complete (View: toast + "+ Done" state).</summary>
        public bool CompletedFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var q in _quests) if (q.Id == id) return q.Equipped;
            return false;
        }

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Select which quest the detail card inspects. No-op for an unknown id.</summary>
        public void Select(string id)
        {
            if (string.IsNullOrEmpty(id) || id == _selectedId) return;
            bool known = false;
            foreach (var q in _quests) if (q.Id == id) { known = true; break; }
            if (!known) return;
            _selectedId = id;
            Raise();
        }

        /// <summary>Free-reroll the quest in a slot (service caps per day). Rebuilds + raises on success.</summary>
        public void Reroll(string slot)
        {
            if (_source == null || string.IsNullOrEmpty(slot)) return;
            var rolled = _source.Reroll(slot);
            if (rolled != null) Rebuild();   // (the service also fires Changed, but rebuild is idempotent)
        }

        // ── Build the projected tiles + selection (no Unity types) ──────────────

        private void Rebuild()
        {
            _quests.Clear();
            _progressById.Clear();
            _flavorById.Clear();
            _celebById.Clear();
            _rewardById.Clear();

            var today = _source?.TodayQuests;
            if (today != null)
            {
                foreach (var q in today)
                {
                    if (q == null) continue;
                    string id = KeyFor(q);
                    if (string.IsNullOrEmpty(id)) continue;

                    _quests.Add(new ItemVM(id, ResolveLabel(q), "quest", q.Slot, 0, "",
                                           affordable: true, rarity: null,
                                           equipped: q.Completed, locked: false));
                    _progressById[id] = q.Progress + " / " + q.Target;
                    _flavorById[id]   = SlotFlavor(q.Slot);
                    _celebById[id]    = q.TemplateId ?? q.Label ?? q.Slot ?? id;
                    _rewardById[id]   = BuildReward(q.Slot);
                }
            }

            // Keep the selection if still valid; else default to the first quest.
            if (string.IsNullOrEmpty(_selectedId) || !Contains(_selectedId))
                _selectedId = _quests.Count > 0 ? _quests[0].Id : null;

            Raise();
        }

        private RewardInfo BuildReward(string slot)
        {
            var r = _source?.RewardForSlot(slot);
            if (r == null) return default;
            return new RewardInfo(r.RewardCrystals, r.RewardFood, r.RewardGlimmer, r.RewardWisdom, r.RewardRandomItem);
        }

        private bool Contains(string id)
        {
            foreach (var q in _quests) if (q.Id == id) return true;
            return false;
        }

        // ── Pure helpers (moved verbatim from the View) ─────────────────────────

        private static string KeyFor(DailyQuestInstance q)
            => q.Id ?? q.TemplateId ?? q.Slot ?? "";

        // WO-810 follow-up: the substitution now lives on DailyQuestCatalog.ResolveLabel —
        // the ONE shared site — so the rumor board's daily rows resolve identically.
        private static string ResolveLabel(DailyQuestInstance q)
            => DailyQuestCatalog.ResolveLabel(q);

        private static string SlotFlavor(string slot)
        {
            switch (slot)
            {
                case "combat":      return "Combat objective - resets daily.";
                case "exploration": return "Exploration objective - resets daily.";
                case "wildcard":    return "Wildcard objective - resets daily.";
                default:            return "Daily objective - resets daily.";
            }
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }

        // ── Real seam: wraps the DailyQuestService singleton + DailyQuestCatalog ──
        //    (the SOLE resolution site for the live daily-quest handles). ─────────
        private sealed class ServiceSource : ISource
        {
            public event Action Changed
            {
                add    { if (DailyQuestService.Instance != null) DailyQuestService.Instance.SetChanged += value; }
                remove { if (DailyQuestService.Instance != null) DailyQuestService.Instance.SetChanged -= value; }
            }

            public IReadOnlyList<DailyQuestInstance> TodayQuests
            {
                get
                {
                    var today = DailyQuestService.Instance?.Today;
                    return today != null ? today.Quests : System.Array.Empty<DailyQuestInstance>();
                }
            }

            public DailyQuestSlotReward RewardForSlot(string slot) => DailyQuestCatalog.RewardFor(slot);

            public DailyQuestInstance Reroll(string slot) => DailyQuestService.Instance?.Reroll(slot);
        }
    }
}
