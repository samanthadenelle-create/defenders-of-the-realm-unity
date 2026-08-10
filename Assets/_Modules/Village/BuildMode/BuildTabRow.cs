// =============================================================================
// BuildTabRow — the code-built Build HUD category tab row (Grok slice 3).
// -----------------------------------------------------------------------------
// ⚠ RETIRED FROM THE DOCK — WO-1010 D21 (owner ruling late 2026-08-09). The PICK
// phase's category tabs left the bottom panel: the RIGHT-EDGE quick-tab stack in
// BuildPaletteUI (BuildQuickTabs/AddQuickTab) is now the ONE permanent category
// selector (Town / Defense / Castle Structures), and it took over this file's
// tutorial spotlight registrations ("build.tab_town"/"build.tab_defenses") and
// the gilt-underline active tell. NOTHING constructs this component any more
// (BuildPaletteUI was the sole caller — verified 2026-08-09). Kept on disk
// because an edit-only lane cannot delete files + .meta pairs; the committing
// seat may delete both in one commit if it wants the dead code gone.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The owner-ruled build categories — Town / Defenses / Walls (Walls gated by
// FeatureFlags.WallsTab) — as a reusable kit tab row. Extracted from the inline
// tab loop that used to live in BuildPaletteUI.EnsureBuilt so the Build HUD owns
// ONE tab component (Grok reuse ledger: "Category tabs -> swap to kit BuildTabRow").
//
// The active tab carries a gilt UNDERLINE bar pinned to its bottom edge — the
// POSITION + SHAPE carry the meaning, never colour alone (owner is red/green
// colourblind). ASCII-only TMP captions. Code-built uGUI on the kit
// (BuildObsidianButton); ZERO UXML. Sizes are the caller's (the row fills the
// parent band it is handed), so this file sets no absolute pixel floor.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Catalog;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Reusable Build HUD category tab row. <see cref="Build"/> fills the parent band
    /// with Town / Defenses / (Walls) kit tabs; tapping one raises <c>onSelect</c>.
    /// <see cref="SetActive"/> moves the gilt underline to the matching tab.
    /// </summary>
    public sealed class BuildTabRow : MonoBehaviour
    {
        private readonly Dictionary<BuildType, GameObject> _underlines =
            new Dictionary<BuildType, GameObject>();
        private BuildType _active = BuildType.Town;

        /// <summary>
        /// Build the tabs into <paramref name="parent"/> (a full-band RectTransform host).
        /// Walls is included only when <see cref="FeatureFlags.WallsTab"/> is on; the
        /// two-tab layout re-spans the row otherwise. Registers Town/Defenses as tutorial
        /// spotlight targets (idempotent — the registry re-arms an armed step).
        /// </summary>
        public void Build(Transform parent, Action<BuildType> onSelect, BuildType active)
        {
            _underlines.Clear();
            _active = active;

            // WO-1010 band-tightening (2026-08-09): the slot fractions only carry each
            // tab's CENTRE (PinSize collapses to a fixed 360x132 box), and the old slots
            // spread the centres across the full 1560px band — two tabs sat ~575px apart
            // with a dead centred gap the owner's mockup does not have. Centres now pack
            // the pinned boxes ADJACENT (24px gaps) about the band's middle:
            // two tabs -> 744px cluster (centres 0.377/0.623); three -> 1128px
            // (centres 0.254/0.500/0.746).
            if (DeNelle.Core.FeatureFlags.WallsTab)
            {
                Tab(parent, "Town",     BuildType.Town,    0.199f, 0.309f, onSelect);
                Tab(parent, "Defenses", BuildType.Defense, 0.445f, 0.555f, onSelect);
                Tab(parent, "Walls",    BuildType.Walls,   0.691f, 0.801f, onSelect);
            }
            else
            {
                Tab(parent, "Town",     BuildType.Town,    0.322f, 0.432f, onSelect);
                Tab(parent, "Defenses", BuildType.Defense, 0.568f, 0.678f, onSelect);
            }
        }

        /// <summary>Move the gilt underline to the tab matching <paramref name="type"/>.</summary>
        public void SetActive(BuildType type)
        {
            _active = type;
            foreach (var kv in _underlines)
                if (kv.Value != null) kv.Value.SetActive(kv.Key == type);
        }

        private void Tab(Transform parent, string caption, BuildType type,
            float xMin, float xMax, Action<BuildType> onSelect)
        {
            var btn = ElarionUiKit.BuildObsidianButton(parent, caption,
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(xMin, 0.12f), new Vector2(xMax, 0.88f),
                () => onSelect?.Invoke(type));

            // Owner felt-test 2026-07-15 ("long thin rectangles in horizontal mode"):
            // a tab spanning ~half the 1560px-wide band resolved to a wide-but-short
            // bar (the touch-floor guard only grows the short side, so it stayed a
            // 733x112 bar). Pin each tab to the consistent CTA box centred on its slot
            // — a proper 360x132 button with even gaps between tabs. The xMin/xMax the
            // caller passes now only carry the tab's CENTRE. Height >= MinTouchPx floor.
            PinSize(btn, ElarionUiKit.CanonCtaWidth, ElarionUiKit.CanonCtaHeight);

            // Active-tab tell: a gilt underline pinned to the tab's bottom edge —
            // POSITION + SHAPE carry the meaning (owner is red/green colourblind).
            var underline = new GameObject("ActiveUnderline", typeof(RectTransform), typeof(Image));
            underline.transform.SetParent(btn.transform, false);
            var urt = (RectTransform)underline.transform;
            urt.anchorMin = new Vector2(0.08f, 0f);
            urt.anchorMax = new Vector2(0.92f, 0f);
            urt.pivot = new Vector2(0.5f, 0f);
            urt.sizeDelta = new Vector2(0f, 3f);
            var img = underline.GetComponent<Image>();
            img.color = ElarionUi.Gilt;
            img.raycastTarget = false;
            underline.SetActive(type == _active);
            _underlines[type] = underline;

            // Tutorial spotlight targets (owner 2026-07-13 "highlight town tab to start").
            if (type == BuildType.Town)
                TutorialHighlightRegistry.Register("build.tab_town", (RectTransform)btn.transform);
            else if (type == BuildType.Defense)
                TutorialHighlightRegistry.Register("build.tab_defenses", (RectTransform)btn.transform);
        }

        // ── Consistent-size pin (mirrors ElarionUiKit.PinCanonicalCtaSize) ──────
        /// <summary>
        /// Collapse a kit button's fraction-of-parent anchors to a POINT at the anchor
        /// rect's centre and stamp a fixed <paramref name="w"/> x <paramref name="h"/>
        /// pixel box, so a wide tab band can never stretch the tab into a thin bar.
        /// Height must be >= ElarionUiKit.MinTouchPx so the kit touch-floor guard no-ops.
        /// </summary>
        private static void PinSize(Button button, float w, float h)
        {
            if (button == null) return;
            var rt = button.transform as RectTransform;
            if (rt == null) return;
            Vector2 centre = (rt.anchorMin + rt.anchorMax) * 0.5f;
            rt.anchorMin = centre;
            rt.anchorMax = centre;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
