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
// Also owns the PREREQUISITE gate: a quest whose QuestDef.RequiresQuestId names a quest the
// player has not completed is kept out of Available and refused by Accept, which is what makes
// the Forgemasters act chain (act1 -> act2 -> act3 -> act4) an order instead of a suggestion.
//
// PURE C#: no UnityEngine UI types; unit-testable over a fake IRumorBoardBackend (§2c).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Quests;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village.Items;

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
        /// stages as READY-TO-DRAW parts, one per chip ("Crystals 20", "Food 10", "Iron
        /// Longsword"). Empty when unrewarded — the View hides the row rather than rendering
        /// an empty line. The View NEVER parses this back out of a joined string, and an item
        /// part is ALWAYS a resolved display name (see <see cref="ItemDisplayName"/>).</summary>
        public IReadOnlyList<string> RewardPartsFor(string id)
        {
            var parts = new List<string>();
            var def = FindDef(id);
            if (def == null || def.Stages == null) return parts;
            int xp = 0, crystals = 0, wood = 0, iron = 0, food = 0, magic = 0;
            var items = new List<string>();
            foreach (var st in def.Stages)
            {
                if (st == null || st.Reward == null) continue;
                QuestRewardMath.Sum(st.Reward,
                    out int sXp, out int sC, out int sW, out int sIr, out int sF, out int sM, out var sItems);
                xp += sXp; crystals += sC; wood += sW; iron += sIr; food += sF; magic += sM;
                if (sItems != null) items.AddRange(sItems);
            }
            // XP first — owner ruling WO-1202: primary reward on the board slab.
            if (xp > 0) parts.Add("XP " + xp);
            if (crystals > 0) parts.Add("Crystals " + crystals);
            if (wood > 0) parts.Add("Wood " + wood);
            if (iron > 0) parts.Add("Iron " + iron);
            if (food > 0) parts.Add("Stone " + food);
            if (magic > 0) parts.Add("Magic " + magic);
            // NAME the item, never key it. The "Item: " prefix is deliberately gone: it cost
            // six glyphs of a row that already cannot seat four chips at FontMicro, and the
            // chip sits in the rewards row under a named quest — the name IS the reward.
            foreach (var it in items) parts.Add(ItemDisplayName(it));
            return parts;
        }

        /// <summary>The same rewards as <see cref="RewardPartsFor"/> joined ASCII for a single
        /// line ("Crystals 20 | Food 10 | Iron Longsword"). "" when unrewarded.</summary>
        public string RewardFor(string id) => string.Join(" | ", RewardPartsFor(id));

        /// <summary>
        /// Player-facing name for a granted item id, read off the SAME row the item resolves to:
        /// gear first (weapons/armor/accessories.json — the only shipped grant today is
        /// `knight_iron` -> "Iron Longsword"), then the non-gear identity catalogs (consumables /
        /// materials). An id NO shipped catalog owns is a CONTENT gap, not a code one, so the last
        /// resort is the kit's P10 formatter (`relic_drowned_ledger` -> "Relic Drowned Ledger") —
        /// a raw snake_case key is never player-visible, and the row is never hidden either: a
        /// reward the player earns is always named.
        /// </summary>
        public static string ItemDisplayName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "";

            var w = GearCatalog.FindWeapon(itemId);
            if (w != null && !string.IsNullOrEmpty(w.name)) return w.name;
            var a = GearCatalog.FindArmor(itemId);
            if (a != null && !string.IsNullOrEmpty(a.name)) return a.name;
            var ac = GearCatalog.FindAccessory(itemId);
            if (ac != null && !string.IsNullOrEmpty(ac.name)) return ac.name;

            var row = ItemIdentity.Resolve(itemId);
            if (row.IsKnown && !string.IsNullOrEmpty(row.DisplayName)) return row.DisplayName;

            return ElarionUiKit.SpacedDisplayName(itemId);
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
            var def = FindDef(id);
            // The Available list already hides a gated quest, but the refusal lives here too so
            // no caller (a stale row, a test, a future view) can start an act out of order.
            if (def != null && !PrerequisiteMet(def))
            {
                Status = "Not yet: finish " + CatalogTitle(def.RequiresQuestId) + " first.";
                Raise();
                return;
            }
            _backend.StartQuest(id);
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

        /// <summary>True when the quest carries no requiresQuestId, or when the quest it names
        /// is already COMPLETED. This is what enforces act ordering (forgemasters_act1 -> act2
        /// -> act3 -> act4); without it every act, including the terminal one that mints the
        /// aegis legendaries, is startable on a fresh save.</summary>
        private bool PrerequisiteMet(QuestDef def)
        {
            string prereq = def != null ? def.RequiresQuestId : null;
            if (string.IsNullOrEmpty(prereq)) return true;
            prereq = prereq.Trim();
            if (prereq.Length == 0) return true;
            return _backend != null && _backend.IsCompleted(prereq);
        }

        /// <summary>Display title for any catalog quest id (not just the tab-filtered ones the
        /// board indexed), so a refusal names the quest the player has to finish rather than a
        /// raw id. Falls back to the id when the catalog cannot answer.</summary>
        private string CatalogTitle(string id)
        {
            if (string.IsNullOrEmpty(id)) return "the quest before it";
            var catalog = _backend != null ? _backend.Catalog : null;
            if (catalog != null)
                foreach (var q in catalog)
                    if (q != null && q.Id == id && !string.IsNullOrEmpty(q.Title)) return q.Title;
            return id;
        }

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
                // A quest whose requiresQuestId names an unfinished quest stays off the board
                // entirely (see PrerequisiteMet). Hidden rather than shown locked: the rumor
                // board's card has no lock affordance: RumorBoardPanel renders an available row
                // from (id, title, hook, "[New]") and never reads ItemVM.Locked/LockReason, so a
                // locked row would look exactly like an acceptable one.
                if (!PrerequisiteMet(def)) continue;
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
                    // WO-810 follow-up (owner F8 "board does not look like mock up"): route
                    // through the shared resolver so "{target}" is substituted — the raw
                    // "Clear {target} waves" titles were this exact skipped call.
                    string title = DailyQuestCatalog.ResolveLabel(q);
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
