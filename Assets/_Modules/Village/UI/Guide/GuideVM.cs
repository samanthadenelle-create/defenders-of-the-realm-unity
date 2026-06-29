// =============================================================================
// GuideVM — the Game Guide view-model (WO-588). Pure logic, no UnityEngine.UI.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Projects the loaded GuideContentCatalog sections into exactly what the dumb
// View renders: an ordered tab list, a SelectedIndex, and the selected section's
// title / status / body paragraphs / tips. The View raises SelectTab(index); the
// VM mutates SelectedIndex and fires Changed so the View repaints (ui-mvvm-binding-
// seam rule — all state/logic here, never in the View).
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Village
{
    /// <summary>
    /// The Game Guide's view-model: the tab list + the selected section's content.
    /// No UnityEngine references beyond plain data — the View is the only UI layer.
    /// </summary>
    public sealed class GuideVM
    {
        private readonly List<GuideSection> _sections = new List<GuideSection>();
        private readonly List<string> _tabs = new List<string>();
        private int _selected;

        /// <summary>Raised whenever the selection changes so the View repaints.</summary>
        public event Action Changed;

        public GuideVM()
        {
            var src = GuideContentCatalog.Sections;
            if (src != null)
            {
                foreach (var s in src)
                {
                    if (s == null || string.IsNullOrEmpty(s.Tab)) continue;
                    _sections.Add(s);
                    _tabs.Add(s.Tab);
                }
            }
            _selected = _sections.Count > 0 ? 0 : -1;
        }

        /// <summary>The ordered tab labels (one per section).</summary>
        public IReadOnlyList<string> Tabs => _tabs;

        /// <summary>Number of sections / tabs.</summary>
        public int Count => _sections.Count;

        /// <summary>The currently selected tab index (-1 when there are no sections).</summary>
        public int SelectedIndex => _selected;

        /// <summary>True when a real section is selected and available to render.</summary>
        public bool HasSelection => _selected >= 0 && _selected < _sections.Count;

        private GuideSection Current => HasSelection ? _sections[_selected] : null;

        /// <summary>The selected section's body header / title.</summary>
        public string SelectedTitle
        {
            get
            {
                var c = Current;
                if (c == null) return string.Empty;
                return string.IsNullOrEmpty(c.Title) ? (c.Tab ?? string.Empty) : c.Title;
            }
        }

        /// <summary>True when the selected section documents a not-yet-built system.</summary>
        public bool SelectedIsComing => Current != null && Current.IsComing;

        /// <summary>The selected section's body paragraphs (never null).</summary>
        public IReadOnlyList<string> SelectedBody =>
            (Current != null && Current.Body != null) ? Current.Body : EmptyList;

        /// <summary>The selected section's tips (never null).</summary>
        public IReadOnlyList<string> SelectedTips =>
            (Current != null && Current.Tips != null) ? Current.Tips : EmptyList;

        /// <summary>Select the tab at <paramref name="index"/>. Out-of-range / no-op
        /// re-selects are ignored (no spurious Changed). Fires Changed on a real change.</summary>
        public void SelectTab(int index)
        {
            if (index < 0 || index >= _sections.Count) return;
            if (index == _selected) return;
            _selected = index;
            Changed?.Invoke();
        }

        private static readonly IReadOnlyList<string> EmptyList = new List<string>();
    }
}
