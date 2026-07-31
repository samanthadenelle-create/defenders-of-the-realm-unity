// =============================================================================
// RumorBoardVM — the pure ViewModel behind RumorBoardPanel (Brom's rumor board).
// Strict-MVVM migration Silo D.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Owns ALL the quest state + logic the board used to read inline: the QuestCatalog
// browse list, the active/available bucketing, the per-tab (All/Story/Daily/Gear/
// Endgame) filtering, the DailyQuestService projection, the tracked-quest flag, and
// the StartQuest / SetTracked writes. The View (RumorBoardPanel) binds this,
// re-renders on Changed, and routes taps to Accept/Track/SetTab — it never reads
// QuestService / QuestCatalog / DailyQuestService itself.
//
// PURE C#: no UnityEngine UI types; unit-testable over a fake IRumorBoardBackend (§2c).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Quests;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Hero
{
    /// <summary>
    /// The seam the RumorBoardVM resolves quest state through. The live implementation
    /// (<see cref="RumorBoardLiveBackend"/>) wires QuestService / QuestCatalog /
    /// DailyQuestService; tests supply a fake.
    /// </summary>
    public interface IRumorBoardBackend
    {
        IReadOnlyList<QuestDef> Catalog { get; }
        bool Ready { get; }
        bool IsActive(string id);
        bool IsCompleted(string id);
        /// <summary>The current active stage's objective text, or null.</summary>
        string ObjectiveFor(string id);
        string TrackedId { get; }
        void StartQuest(string id);
        void SetTracked(string id);
        IReadOnlyList<RumorBoardVM.DailyRow> DailyToday { get; }
        event Action Changed;
    }

    /// <summary>Pure ViewModel for the rumor board.</summary>
    public sealed class RumorBoardVM : IPanelViewModel, IDisposable
    {
        /// <summary>One projected daily-quest row (View-agnostic — no DailyQuestInstance leak).</summary>
        public readonly struct DailyRow
        {
            public readonly string Id;
            public readonly string Title;
            public readonly int Progress;
            public readonly int Target;
            public readonly bool Completed;
            public DailyRow(string id, string title, int progress, int target, bool completed)
            {
                Id = id;
                Title = title;
                Progress = progress;
                Target = target;
                Completed = completed;
            }
        }

        /// <summary>Board tab keys (drive the filter). "all" = the ungrouped catalog view.</summary>
        public static readonly string[] TabKeys = { "all", "story", "daily", "gear", "endgame" };
        /// <summary>Board tab labels (View chrome).</summary>
        public static readonly string[] TabLabels = { "All", "Story", "Daily", "Gear", "Endgame" };

        private readonly IRumorBoardBackend _backend;
        private readonly Action _onClose;
        private readonly Action _changedHandler;

        private readonly List<ItemVM> _active = new List<ItemVM>();
        private readonly List<ItemVM> _available = new List<ItemVM>();
        private readonly Dictionary<string, QuestDef> _byId = new Dictionary<string, QuestDef>();
        private bool _disposed;

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title => "Brom's Rumor Board";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_backend != null && _changedHandler != null) _backend.Changed -= _changedHandler;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        public string ActiveTab { get; private set; } = "all";
        public bool IsDailyTab => ActiveTab == "daily";

        /// <summary>Active quests under the current tab (Equipped = tracked/pinned). Never null.</summary>
        public IReadOnlyList<ItemVM> ActiveQuests => _active;

        /// <summary>Available quests under the current tab (not active, not completed). Never null.</summary>
        public IReadOnlyList<ItemVM> AvailableQuests => _available;

        /// <summary>Today's daily quests (only meaningful under the Daily tab). Never null.</summary>
        public IReadOnlyList<DailyRow> DailyQuests =>
            _backend != null ? _backend.DailyToday : System.Array.Empty<DailyRow>();

        /// <summary>Status line (the board's transient message).</summary>
        public string Status { get; private set; } = "The talk of Elarion. Accept what calls to you.";

        /// <summary>The active stage objective for an active quest ("…" when unknown).</summary>
        public string ObjectiveFor(string id)
        {
            string o = _backend != null ? _backend.ObjectiveFor(id) : null;
            return string.IsNullOrEmpty(o) ? "..." : o;
        }

        /// <summary>The hook line for an available quest (its stage-1 objective, else a default).</summary>
        public string HookFor(string id)
        {
            if (id != null && _byId.TryGetValue(id, out var def) && def != null
                && def.Stages != null && def.Stages.Count > 0 && def.Stages[0] != null
                && !string.IsNullOrEmpty(def.Stages[0].ObjectiveText))
                return def.Stages[0].ObjectiveText;
            return "A new thread waits to be picked up.";
        }

        /// <summary>WO-810 detail tag: the quest's display type ("Story" / "Gear" / "Endgame").
        /// Same normalization as the tab filter, capitalized for the tag row.</summary>
        public string TypeFor(string id)
        {
            var def = FindDef(id);
            string ty = NormalizedType(def);
            if (ty == "gear") return "Gear";
            if (ty == "endgame") return "Endgame";
            return "Story";
        }

        /// <summary>WO-810 detail rewards row: the quest's TOTAL authored rewards across all
        /// stages, formatted ASCII ("Crystals 20 | Food 10 | Item: xyz"). "" when unrewarded —
        /// the View hides the row rather than rendering an empty line.</summary>
        public string RewardFor(string id)
        {
            var def = FindDef(id);
            if (def == null || def.Stages == null) return "";
            int crystals = 0, food = 0, magic = 0;
            var items = new List<string>();
            foreach (var st in def.Stages)
            {
                if (st == null || st.Reward == null) continue;
                crystals += st.Reward.Crystals;
                food += st.Reward.Food;
                magic += st.Reward.Magic;
                if (!string.IsNullOrEmpty(st.Reward.GrantItemId)) items.Add(st.Reward.GrantItemId);
            }
            var parts = new List<string>();
            if (crystals > 0) parts.Add("Crystals " + crystals);
            if (food > 0) parts.Add("Food " + food);
            if (magic > 0) parts.Add("Magic " + magic);
            foreach (var it in items) parts.Add("Item: " + it);
            return string.Join(" | ", parts);
        }

        // ── Commands ────────────────────────────────────────────────────────────

        public void SetTab(string tab)
        {
            if (string.IsNullOrEmpty(tab) || ActiveTab == tab) return;
            ActiveTab = tab;
            Rebuild();
            Raise();
        }

        /// <summary>Accept an available quest (StartQuest). Moves it Available -> Active.</summary>
        public void Accept(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_backend == null || !_backend.Ready) { Status = "Quests aren't ready yet."; Raise(); return; }
            _backend.StartQuest(id);
            var def = FindDef(id);
            string name = def != null && !string.IsNullOrEmpty(def.Title) ? def.Title : id;
            Status = "Accepted: " + name + ".";
            // The backend raises Changed on a successful start (-> Rebuild); rebuild defensively
            // if it did not become active (service wasn't up to fire the event).
            if (!_backend.IsActive(id)) Rebuild();
            Raise();
        }

        /// <summary>Pin an active quest to the HUD tracker, then close the board.</summary>
        public void Track(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_backend == null || !_backend.Ready) { Status = "Quests aren't ready yet."; Raise(); return; }
            _backend.SetTracked(id);
            Close();
        }

        // ── Construction / resolution ───────────────────────────────────────────

        /// <summary>The ONLY resolution site: wires the live quest services/catalog.</summary>
        public static RumorBoardVM CreateDefault(Action onClose = null) =>
            new RumorBoardVM(new RumorBoardLiveBackend(), onClose);

        public RumorBoardVM(IRumorBoardBackend backend, Action onClose)
        {
            _backend = backend;
            _onClose = onClose;
            if (_backend != null)
            {
                _changedHandler = OnBackendChanged;
                _backend.Changed += _changedHandler;
            }
            Rebuild();
        }

        private void OnBackendChanged() { Rebuild(); Raise(); }

        private QuestDef FindDef(string id) =>
            id != null && _byId.TryGetValue(id, out var d) ? d : null;

        private void Rebuild()
        {
            _active.Clear();
            _available.Clear();
            _byId.Clear();

            if (IsDailyTab) return;   // the Daily tab renders from DailyQuests, not the catalog

            var catalog = _backend != null ? _backend.Catalog : null;
            if (catalog == null) return;

            string tracked = _backend != null ? _backend.TrackedId : null;
            foreach (var def in catalog)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (!MatchesTab(def, ActiveTab)) continue;
                _byId[def.Id] = def;

                string title = !string.IsNullOrEmpty(def.Title) ? def.Title : def.Id;
                if (_backend.IsActive(def.Id))
                {
                    bool isTracked = tracked == def.Id;
                    _active.Add(new ItemVM(def.Id, title, "quest", def.Id, 0, "", true,
                                           rarity: null, equipped: isTracked, locked: false));
                    continue;
                }
                if (_backend.IsCompleted(def.Id)) continue;   // done — off the board
                _available.Add(new ItemVM(def.Id, title, "quest", def.Id, 0, "", true));
            }
        }

        // Normalize a quest's free-string Type -> a lowercase bucket; empty/null = "story".
        private static string NormalizedType(QuestDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Type)) return "story";
            return def.Type.Trim().ToLowerInvariant();
        }

        // Does this quest belong under the given tab? "all" shows everything; "story" also
        // catches main/side/unknown; gear/endgame are exact.
        private static bool MatchesTab(QuestDef def, string tab)
        {
            if (tab == "all") return true;
            string ty = NormalizedType(def);
            switch (tab)
            {
                case "gear": return ty == "gear";
                case "endgame": return ty == "endgame";
                case "story": return ty != "gear" && ty != "endgame";
                default: return true;
            }
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }

    /// <summary>
    /// Live <see cref="IRumorBoardBackend"/> — the sole binding to QuestService /
    /// QuestCatalog / DailyQuestService. Kept out of the View so RumorBoardPanel stays
    /// a dumb skin.
    /// </summary>
    public sealed class RumorBoardLiveBackend : IRumorBoardBackend
    {
        public IReadOnlyList<QuestDef> Catalog => QuestCatalog.Quests;
        public bool Ready => QuestService.Instance != null;

        public bool IsActive(string id) => QuestService.Instance != null && QuestService.Instance.IsActive(id);
        public bool IsCompleted(string id) => QuestService.Instance != null && QuestService.Instance.IsCompleted(id);

        public string ObjectiveFor(string id)
        {
            var svc = QuestService.Instance;
            var stage = svc != null ? svc.GetStage(id) : null;
            return stage != null ? stage.ObjectiveText : null;
        }

        public string TrackedId => QuestService.Instance != null ? QuestService.Instance.TrackedId : null;

        public void StartQuest(string id) { if (QuestService.Instance != null) QuestService.Instance.StartQuest(id); }
        public void SetTracked(string id) { if (QuestService.Instance != null) QuestService.Instance.SetTracked(id); }

        public IReadOnlyList<RumorBoardVM.DailyRow> DailyToday
        {
            get
            {
                var rows = new List<RumorBoardVM.DailyRow>();
                var dq = DailyQuestService.Instance;
                var set = dq != null ? dq.Today : null;
                if (set == null || set.Quests == null) return rows;
                foreach (var q in set.Quests)
                {
                    if (q == null) continue;
                    string title = q.Label ?? q.TemplateId ?? q.Slot;
                    rows.Add(new RumorBoardVM.DailyRow(q.Id, title, q.Progress, q.Target, q.Completed));
                }
                return rows;
            }
        }

        public event Action Changed
        {
            add { if (QuestService.Instance != null) QuestService.Instance.QuestChanged += value; }
            remove { if (QuestService.Instance != null) QuestService.Instance.QuestChanged -= value; }
        }
    }
}
