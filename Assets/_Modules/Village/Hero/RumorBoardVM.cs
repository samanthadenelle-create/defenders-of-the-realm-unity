// =============================================================================
// RumorBoardVM - the pure ViewModel behind RumorBoardPanel (Brom's rumor board).
// Strict-MVVM migration Silo D.  WO-1192 v3 REBUILD (owner-approved concept).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// WHAT THIS VM IS NOW, AND WHAT IT DELIBERATELY STOPPED BEING (WO-1192, owner
// rulings 2026-08-26):
//
//   "The board is for ACCEPTING new quests." Tracking is the HUD tracker's job.
//   The approved v3 concept is THREE SELF-CONTAINED RUMOR POSTERS, no tabs, no
//   detail pane, no In-Progress section, and a single Next > that pages by three
//   and WRAPS. So the following are RETIRED, not merely unused:
//
//     TabKeys / TabLabels / ActiveTab / SetTab / IsDailyTab   - there are no tabs.
//     ActiveQuests / Track / DailyQuests / DailyRow           - the board only OFFERS.
//
//   They are deleted rather than left dormant because a dormant projection is how
//   a retired surface quietly grows back (CLAUDE.md sec.15: a state change with no
//   canon update is an incomplete change; the same is true of a dead API).
//
// WHAT REPLACED THEM:
//   * PAGING. Available rumors are windowed PageSize (3) at a time. NextPage()
//     advances and WRAPS at the end - the owner chose the keep-going form, so the
//     board never dead-ends on a short page.
//   * HookFor is now a ONE-LINE hook derived at a SENTENCE boundary from the full
//     letter, and LetterFor carries the whole prose for the "Read the letter >"
//     overlay. Both captures of the 2026-08-25/26 shots clipped this text MID-WORD
//     ("begun to sin", "wakes the lantern eels. Sh"); a hook cut at a sentence and
//     a letter that scrolls is what makes that unreachable rather than tuned away.
//   * RewardChipsFor projects READY-TO-DRAW reward chips carrying the reward KIND
//     and AMOUNT, so the View can render the WO-1195 icon+number chip. It still
//     sums through QuestRewardMath over QuestRewardLine (WO-1201/1202 stay the
//     reward authority) and there is NO fixed chip count anywhere.
//   * IsNew(id) reads a SEEN flag off the backend seam, so the NEW chip means
//     something instead of decorating every card forever.
//
// Also owns the PREREQUISITE gate: a quest whose QuestDef.RequiresQuestId names a
// quest the player has not completed is kept out of Available and refused by Accept,
// which is what makes the Forgemasters act chain (act1 -> act2 -> act3 -> act4) an
// order instead of a suggestion.
//
// PURE C#: no UnityEngine UI types; unit-testable over a fake IRumorBoardBackend.
// ASCII-ONLY (including comments) - the shipped LiberationSans SDF has no non-Latin
// glyphs and RumorBoardLayoutRegression asserts the whole file is ASCII.
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
    /// (<see cref="RumorBoardLiveBackend"/>) wires QuestService / QuestCatalog;
    /// tests supply a fake.
    /// </summary>
    public interface IRumorBoardBackend
    {
        IReadOnlyList<QuestDef> Catalog { get; }
        bool Ready { get; }
        bool IsActive(string id);
        bool IsCompleted(string id);
        void StartQuest(string id);
        /// <summary>True once this rumor has been PUT IN FRONT OF the player. Drives the
        /// NEW chip; without it "NEW" decorates every card forever and stops meaning
        /// anything (the WO-1192 law: a badge that is always on is chrome, not state).</summary>
        bool HasSeen(string id);
        /// <summary>Record that this rumor has been shown. Called by the View once per
        /// page paint, AFTER the page's NEW flags were read.</summary>
        void MarkSeen(string id);
        event Action Changed;
    }

    /// <summary>Pure ViewModel for the rumor board.</summary>
    public sealed class RumorBoardVM : IPanelViewModel, IDisposable
    {
        /// <summary>What a reward chip IS, so the View can pick the WO-1195 icon+number
        /// chip for a currency and a WORD chip for XP / a granted item. Deliberately a
        /// VM-local enum: mapping it to the kit's CurrencyKind (and from there to the ONE
        /// concept-id translator, ElarionUiKit.ConceptIdFor) is the View's job. A second
        /// copy of that translator here would be a second registry.</summary>
        public enum RewardKind { Xp, Crystals, Wood, Iron, Stone, Magic, Item }

        /// <summary>One READY-TO-DRAW reward chip. <see cref="Text"/> is the full word form
        /// ("Crystals 220", "Relic Drowned Ledger") and is what a no-icon fallback renders;
        /// <see cref="Amount"/> is what an icon chip renders beside its icon.</summary>
        public readonly struct RewardChipVM
        {
            public readonly RewardKind Kind;
            public readonly int Amount;
            public readonly string Text;
            public RewardChipVM(RewardKind kind, int amount, string text)
            {
                Kind = kind;
                Amount = amount;
                Text = text;
            }
            /// <summary>True for the resource kinds that own a currency icon.</summary>
            public bool IsCurrency => Kind != RewardKind.Xp && Kind != RewardKind.Item;
        }

        /// <summary>Rumors shown per page. The owner-approved v3 board is three posters.</summary>
        public const int PageSize = 3;

        /// <summary>Longest one-line hook before it is cut at a WORD boundary. A hook that
        /// ends mid-word reads as a bug; one that ends on a word reads as a summary.</summary>
        public const int HookMaxChars = 72;

        private readonly IRumorBoardBackend _backend;
        private readonly Action _onClose;
        private readonly Action _changedHandler;

        private readonly List<ItemVM> _available = new List<ItemVM>();
        private readonly List<ItemVM> _page = new List<ItemVM>();
        private readonly Dictionary<string, QuestDef> _byId = new Dictionary<string, QuestDef>();
        private int _pageIndex;
        private bool _disposed;

        // -- IPanelViewModel ---------------------------------------------------

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

        // -- Read-only data the View renders -----------------------------------

        /// <summary>Every available quest (not active, not completed, prerequisite met).
        /// Never null. The View renders <see cref="PageQuests"/>, not this.</summary>
        public IReadOnlyList<ItemVM> AvailableQuests => _available;

        /// <summary>The current window of at most <see cref="PageSize"/> rumors. Never null,
        /// and never longer than PageSize - a page with fewer is a real short page, not an
        /// error, and the View renders only the posters it is given.</summary>
        public IReadOnlyList<ItemVM> PageQuests => _page;

        /// <summary>How many pages of three the available rumors make. 1 when the board is
        /// empty, so "page 1 of 1" is always a truthful sentence.</summary>
        public int PageCount
        {
            get
            {
                int n = _available.Count;
                if (n <= 0) return 1;
                return (n + PageSize - 1) / PageSize;
            }
        }

        /// <summary>Zero-based index of the shown page.</summary>
        public int PageIndex => _pageIndex;

        /// <summary>True when there is more than one page, i.e. Next > actually goes somewhere.</summary>
        public bool HasMultiplePages => PageCount > 1;

        /// <summary>Status line (the board's transient message).</summary>
        public string Status { get; private set; } = "The talk of Elarion. Accept what calls to you.";

        /// <summary>The FULL letter for a rumor - the paragraph the "Read the letter &gt;"
        /// overlay scrolls. Never null; an unauthored quest gets an honest short line rather
        /// than an empty well.</summary>
        public string LetterFor(string id)
        {
            var def = FindDef(id);
            if (def != null && def.Stages != null && def.Stages.Count > 0 && def.Stages[0] != null
                && !string.IsNullOrEmpty(def.Stages[0].ObjectiveText))
                return def.Stages[0].ObjectiveText;
            return "A new thread waits to be picked up.";
        }

        /// <summary>The ONE-LINE hook: the letter's first sentence, cut at a WORD boundary if
        /// that sentence is itself long. It can never end mid-word, which is the defect both
        /// the 2026-08-25 and 2026-08-26 captures showed ("begun to sin" / "lantern eels. Sh").</summary>
        public string HookFor(string id) => OneLineHook(LetterFor(id));

        /// <summary>Pure, testable hook derivation (see <see cref="HookFor"/>).</summary>
        public static string OneLineHook(string letter)
        {
            if (string.IsNullOrEmpty(letter)) return "";
            string s = letter.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (s.Length == 0) return "";

            // First sentence, when there is one and it is not itself the whole paragraph.
            int cut = -1;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '.' && c != '!' && c != '?') continue;
                // A period that is not followed by whitespace is an abbreviation or a
                // decimal, not a sentence end.
                if (i + 1 < s.Length && s[i + 1] != ' ') continue;
                cut = i + 1;
                break;
            }
            if (cut > 0 && cut <= HookMaxChars) return s.Substring(0, cut).Trim();

            if (s.Length <= HookMaxChars) return s;

            // Otherwise cut at the last word boundary that fits, and SAY it is cut.
            int space = s.LastIndexOf(' ', Math.Min(HookMaxChars, s.Length - 1));
            if (space <= 0) space = Math.Min(HookMaxChars, s.Length);
            return s.Substring(0, space).TrimEnd(' ', ',', ';', ':') + "...";
        }

        /// <summary>The quest's display TYPE for the poster's overhanging tag. The label
        /// wording is a display concern; this returns the canonical bucket in title case.</summary>
        public string TypeFor(string id)
        {
            var def = FindDef(id);
            string ty = NormalizedType(def);
            if (ty == "gear") return "Gear";
            if (ty == "endgame") return "Endgame";
            if (ty == "daily") return "Daily";
            if (ty == "side") return "Side";
            return "Main";
        }

        /// <summary>True while this rumor has never been shown to the player. Drives the NEW
        /// chip. False the moment the backend has recorded it as seen.</summary>
        public bool IsNew(string id)
        {
            if (string.IsNullOrEmpty(id) || _backend == null) return false;
            return !_backend.HasSeen(id);
        }

        /// <summary>Record every rumor on the CURRENT page as seen. The View calls this AFTER
        /// it has read the page's NEW flags, so a card is NEW exactly once.</summary>
        public void MarkPageSeen()
        {
            if (_backend == null) return;
            for (int i = 0; i < _page.Count; i++)
            {
                string id = _page[i].Id;
                if (!string.IsNullOrEmpty(id)) _backend.MarkSeen(id);
            }
        }

        /// <summary>
        /// The quest's TOTAL authored rewards across all stages, as READY-TO-DRAW chips.
        /// Empty when unrewarded - the View hides the row rather than drawing an empty rule.
        /// Sums through <see cref="QuestRewardMath"/> over <see cref="QuestRewardLine"/>
        /// (WO-1201/1202 remain the reward authority) and emits ONE chip per authored
        /// reward - there is no fixed chip count and no second reward schema.
        /// </summary>
        public IReadOnlyList<RewardChipVM> RewardChipsFor(string id)
        {
            var chips = new List<RewardChipVM>();
            var def = FindDef(id);
            if (def == null || def.Stages == null) return chips;

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

            // XP first - owner ruling WO-1202: primary reward on the board slab.
            if (xp > 0) chips.Add(new RewardChipVM(RewardKind.Xp, xp, "XP " + xp));
            if (crystals > 0) chips.Add(new RewardChipVM(RewardKind.Crystals, crystals, "Crystals " + crystals));
            if (wood > 0) chips.Add(new RewardChipVM(RewardKind.Wood, wood, "Wood " + wood));
            if (iron > 0) chips.Add(new RewardChipVM(RewardKind.Iron, iron, "Iron " + iron));
            // Canon sec.7: the authored `food` slot IS Stone. Never label it Food.
            if (food > 0) chips.Add(new RewardChipVM(RewardKind.Stone, food, "Stone " + food));
            if (magic > 0) chips.Add(new RewardChipVM(RewardKind.Magic, magic, "Magic " + magic));
            // NAME the item, never key it - the chip sits in a rewards row under a named
            // quest, so the name IS the reward.
            foreach (var it in items)
                chips.Add(new RewardChipVM(RewardKind.Item, 0, ItemDisplayName(it)));
            return chips;
        }

        /// <summary>The same rewards as word-form parts, one per chip ("Crystals 20",
        /// "Iron Longsword"). Kept as the single source for any consumer that wants text.</summary>
        public IReadOnlyList<string> RewardPartsFor(string id)
        {
            var chips = RewardChipsFor(id);
            var parts = new List<string>(chips.Count);
            foreach (var c in chips) parts.Add(c.Text);
            return parts;
        }

        /// <summary>The same rewards joined ASCII for a single line. "" when unrewarded.</summary>
        public string RewardFor(string id) => string.Join(" | ", RewardPartsFor(id));

        /// <summary>
        /// Player-facing name for a granted item id, read off the SAME row the item resolves to:
        /// gear first (weapons/armor/accessories.json), then the non-gear identity catalogs
        /// (consumables / materials). An id NO shipped catalog owns is a CONTENT gap, not a code
        /// one, so the last resort is the kit's formatter (`relic_drowned_ledger` -> "Relic
        /// Drowned Ledger") - a raw snake_case key is never player-visible, and the row is never
        /// hidden either: a reward the player earns is always named.
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

        // -- Commands ----------------------------------------------------------

        /// <summary>Advance one page of three and WRAP at the end (owner ruling: the
        /// keep-going form, no dead end, no page dots). Raises Changed.</summary>
        public void NextPage()
        {
            int pages = PageCount;
            _pageIndex = pages <= 1 ? 0 : (_pageIndex + 1) % pages;
            BuildPage();
            Raise();
        }

        /// <summary>Accept an available quest (StartQuest). It leaves the board - the board
        /// only OFFERS work, so an accepted rumor is not shown here again.</summary>
        public void Accept(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_backend == null || !_backend.Ready) { Status = "Quests aren't ready yet."; Raise(); return; }
            var def = FindDef(id);
            // The Available list already hides a gated quest, but the refusal lives here too so
            // no caller (a stale poster, a test, a future view) can start an act out of order.
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

        // -- Construction / resolution -----------------------------------------

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

        /// <summary>Display title for any catalog quest id (not just the ones the board
        /// indexed), so a refusal names the quest the player has to finish rather than a raw
        /// id. Falls back to the id when the catalog cannot answer.</summary>
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
            _available.Clear();
            _byId.Clear();

            var catalog = _backend != null ? _backend.Catalog : null;
            if (catalog != null)
            {
                foreach (var def in catalog)
                {
                    if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                    _byId[def.Id] = def;

                    if (_backend.IsActive(def.Id)) continue;      // underway - the HUD tracker's job
                    if (_backend.IsCompleted(def.Id)) continue;   // done - off the board
                    // A quest whose requiresQuestId names an unfinished quest stays off the board
                    // entirely (see PrerequisiteMet). Hidden rather than shown locked: a v3 poster
                    // has no lock affordance, so a locked poster would look acceptable.
                    if (!PrerequisiteMet(def)) continue;

                    string title = !string.IsNullOrEmpty(def.Title) ? def.Title : def.Id;
                    _available.Add(new ItemVM(def.Id, title, "quest", def.Id, 0, "", true));
                }
            }

            // A page that no longer exists (the last rumor on it was accepted) walks back to
            // the last real page rather than showing an empty board with rumors still on it.
            int pages = PageCount;
            if (_pageIndex >= pages) _pageIndex = pages - 1;
            if (_pageIndex < 0) _pageIndex = 0;
            BuildPage();
        }

        private void BuildPage()
        {
            _page.Clear();
            int start = _pageIndex * PageSize;
            for (int i = start; i < _available.Count && i < start + PageSize; i++)
                _page.Add(_available[i]);
        }

        // Normalize a quest's free-string Type -> a lowercase bucket; empty/null = "story".
        private static string NormalizedType(QuestDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Type)) return "story";
            return def.Type.Trim().ToLowerInvariant();
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }

    /// <summary>
    /// Live <see cref="IRumorBoardBackend"/> - the sole binding to QuestService /
    /// QuestCatalog. Kept out of the View so RumorBoardPanel stays a dumb skin.
    /// </summary>
    public sealed class RumorBoardLiveBackend : IRumorBoardBackend
    {
        /// <summary>PlayerPrefs key prefix for the NEW-chip seen flag. Deliberately NOT a save
        /// schema field: this is a cosmetic per-device badge, and adding a schema version for a
        /// chip would be a migration the player never sees.</summary>
        private const string SeenPrefix = "rumor.seen.";

        public IReadOnlyList<QuestDef> Catalog => QuestCatalog.Quests;
        public bool Ready => QuestService.Instance != null;

        public bool IsActive(string id) => QuestService.Instance != null && QuestService.Instance.IsActive(id);
        public bool IsCompleted(string id) => QuestService.Instance != null && QuestService.Instance.IsCompleted(id);

        public void StartQuest(string id) { if (QuestService.Instance != null) QuestService.Instance.StartQuest(id); }

        public bool HasSeen(string id) =>
            !string.IsNullOrEmpty(id) && UnityEngine.PlayerPrefs.GetInt(SeenPrefix + id, 0) == 1;

        public void MarkSeen(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (UnityEngine.PlayerPrefs.GetInt(SeenPrefix + id, 0) == 1) return;
            UnityEngine.PlayerPrefs.SetInt(SeenPrefix + id, 1);
        }

        public event Action Changed
        {
            add { if (QuestService.Instance != null) QuestService.Instance.QuestChanged += value; }
            remove { if (QuestService.Instance != null) QuestService.Instance.QuestChanged -= value; }
        }
    }
}
