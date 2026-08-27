// =============================================================================
// BenefactorsWallPanel - WO-1073, the Benefactors of the Realm wall.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// A single, GLOBAL honour roll of $500 Founders, identical in every kingdom
// (owner ruling 2026-08-27). Code-built uGUI on the Obsidian master frame, the
// LeaderboardPanel recipe - UXML does not work in builds (CLAUDE.md section 8),
// so nothing here is authored in a .uxml.
//
// Opened by exactly ONE thing: walking up to the Founders Monument near the
// Heart and pressing TALK (DeNelle.Village.FoundersMonument -> PanelRouter ->
// PanelId.Benefactors). Owner ruling 2026-08-27(c): "walking up to the monument
// and reading the names is the moment; a menu item is not."
//
// -----------------------------------------------------------------------------
// ⛔ WHAT THIS SCREEN MAY SHOW, AND WHAT IT MAY NEVER SHOW
// -----------------------------------------------------------------------------
// MAY:  the founding ordinal, the PLAYER-CHOSEN patron name, the founding DATE,
//       and whether that patron's monument has been raised yet.
// NEVER: a wallet address, an email, a real name, or any dollar figure. WO-1073
//       section 4 is explicit - show the TIER, never the amount - and the
//       endpoint does not even send the rest. There is deliberately no code path
//       here that could render one, because BenefactorRow has no field to hold
//       one. Do not add one.
//
// COLOURBLIND RULE: the owner is red/green colourblind. Every distinction on
// this screen is carried by WORDS ("Monument raised" / "Monument in progress")
// and by row order. Colour is decoration only; strip every colour from this file
// and the screen still reads correctly. Verify it that way, not by eye.
//
// ASCII only - the tofu oracle fails a non-ASCII player-facing string.
// Instrumentation: FlowTrace tag "Benefactors". Never strip it.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.Patronage;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class BenefactorsWallPanel : MonoBehaviour
    {
        /// <summary>Player-facing subtitle. States what the wall IS, in words, so an empty
        /// wall reads as a fact rather than as a broken screen.</summary>
        public const string SubtitleLine =
            "The Founders of the Realm, in the order they answered.";

        /// <summary>Row suffix when this patron's own monument has been raised.</summary>
        public const string MonumentRaisedLabel = "Monument raised";

        /// <summary>Row suffix while this patron is still on the shared stand-in. Per patron,
        /// never a global phase - one row can say each of these at the same time.</summary>
        public const string MonumentPendingLabel = "Monument in progress";

        private ElarionUiKit.ObsidianModal _modal;
        private Transform _headerHost;
        private Transform _listContent;
        private TextMeshProUGUI _footer;

        private bool _visible;
        private PanelHandle _panelHandle;

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Benefactors", () => SetVisible(false), () => _visible);
            PanelRouter.Register(PanelId.Benefactors, Open);
            BenefactorsCatalog.Changed += OnCatalogChanged;
            FlowTrace.Step(BenefactorsCatalog.Sys,
                "BenefactorsWallPanel registered PanelId.Benefactors - the Founders Monument now has " +
                "a panel to open.");
        }

        private void OnDestroy()
        {
            BenefactorsCatalog.Changed -= OnCatalogChanged;
            PanelRouter.Unregister(PanelId.Benefactors, Open);
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        /// <summary>The PanelRouter entry point.</summary>
        public void Open() => SetVisible(true);

        public void Toggle() => SetVisible(!_visible);

        private void OnCatalogChanged()
        {
            // Only repaint when the player is actually looking at it. A background refresh
            // that rebuilt a hidden list would be pure garbage churn.
            if (_visible) Render();
        }

        private void SetVisible(bool on)
        {
            if (on)
            {
                FlowTrace.Step(BenefactorsCatalog.Sys, "SetVisible(true) - opening the Benefactors wall.");
                EnsureBuilt();
            }
            if (_modal == null || _modal.canvas == null) { _visible = false; return; }
            _visible = on;
            _modal.canvas.SetActive(on);
            if (on)
            {
                if (!PanelManager.NotifyOpened(_panelHandle))
                {
                    _visible = false;
                    _modal.canvas.SetActive(false);   // battle-lock reject - never force-show
                    return;
                }
                // Render FIRST off the standing wall, THEN ask for a fresh one. The player
                // never watches a blank panel wait on a network call; when the answer lands,
                // BenefactorsCatalog.Changed repaints it underneath them.
                Render();
                BenefactorsService.RequestRefresh();
            }
            else
            {
                PanelManager.NotifyClosed(_panelHandle);
            }
        }

        // ---------------------------------------------------------------------
        //  UI construction (kit modal, lazy on first open)
        // ---------------------------------------------------------------------
        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;

            _modal = ElarionUiKit.BuildObsidianModal("BenefactorsWallUI", BenefactorsCatalog.WallTitle,
                new Vector2(0.26f, 0.10f), new Vector2(0.74f, 0.92f), () => SetVisible(false),
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "crest");   // a crest, not a sword: this is an honour roll

            var body = _modal.chrome.layout != null && _modal.chrome.layout.body != null
                ? (Transform)_modal.chrome.layout.body
                : _modal.chrome.content.transform;

            // FIXED REFERENCE-PIXEL BAND LADDER, the LeaderboardPanel primitive (UI audit
            // 2026-08-01 F1). offsetMin/offsetMax on a CanvasScaler'd canvas are canvas-local
            // units == reference px, the SAME unit ElarionUiKit.MinTouchPx (112) is measured
            // in - so a band's height is provably above the touch floor at every resolution
            // and nothing can be grown out of its band into a neighbour. Fractional bands are
            // what let the leaderboard's tab rail punch through its neighbours.
            //   Header 44 | gap 8 | ListScroll (flex) | gap 8 | Footer 30
            const float HeaderH = 44f, FooterH = 30f, Gap = 8f;
            const float ListTop = HeaderH + Gap;
            const float ListBottom = FooterH + Gap;

            _headerHost = PxBandFromTop(body, "WallHeader", 0.03f, 0.97f, 0f, HeaderH);
            MakeText(_headerHost, SubtitleLine, 14, ElarionUi.Parchment, FontStyles.Italic,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);

            BuildList(body, ListTop, ListBottom);

            var footHost = PxBandFromBottom(body, "Footer", 0.03f, 0.97f, 0f, FooterH);
            _footer = MakeText(footHost, "", 12, ElarionUi.ParchmentDim, FontStyles.Italic,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);

            _modal.canvas.SetActive(false);   // built hidden; SetVisible shows it
        }

        private void BuildList(Transform body, float topInsetPx, float bottomInsetPx)
        {
            var scrollHost = PxStretchBand(body, "ListScroll", 0.03f, 0.97f, topInsetPx, bottomInsetPx);
            var scrollGo = new GameObject("Scroll",
                typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
            scrollGo.transform.SetParent(scrollHost, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var contentGo = new GameObject("Content",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            _listContent = contentGo.transform;
        }

        // ---------------------------------------------------------------------
        //  Repaint - purely from BenefactorsCatalog. No transport, no policy.
        // ---------------------------------------------------------------------
        private void Render()
        {
            if (_modal == null || !_visible || _listContent == null) return;

            // Guard the whole rebuild: one bad row must never blank the wall (CLAUDE.md
            // section 12 rule 2). Never a silent catch - Guard logs through FlowTrace.Fail.
            Guard.Try(BenefactorsCatalog.Sys, "rebuild the Benefactors wall rows", () =>
            {
                for (int i = _listContent.childCount - 1; i >= 0; i--)
                    Destroy(_listContent.GetChild(i).gameObject);

                var rows = BenefactorsCatalog.Rows;
                if (rows == null || rows.Count == 0)
                {
                    MakeEmptyRow(BenefactorsCatalog.EmptyWallLine);
                }
                else
                {
                    for (int i = 0; i < rows.Count; i++) MakeRow(rows[i], i);
                }

                if (_footer != null) _footer.text = BenefactorsCatalog.FooterText();

                FlowTrace.Step(BenefactorsCatalog.Sys,
                    "wall rendered: rows=" + (rows == null ? 0 : rows.Count) +
                    " provenance=" + BenefactorsCatalog.Provenance + ".");
            });
        }

        private void MakeEmptyRow(string line)
        {
            var go = new GameObject("EmptyRow", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(_listContent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 60f;
            MakeText(go.transform, line, 14, ElarionUi.ParchmentDim, FontStyles.Italic,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
        }

        private void MakeRow(BenefactorRow row, int index)
        {
            var rowGo = new GameObject("BenefactorRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowGo.transform.SetParent(_listContent, false);
            rowGo.GetComponent<LayoutElement>().preferredHeight = 40f;
            // Zebra striping only. It carries no meaning; it is a reading aid.
            rowGo.GetComponent<Image>().color =
                (index & 1) == 1 ? new Color(1f, 1f, 1f, 0.04f) : Color.clear;

            MakeText(rowGo.transform, row.Ordinal.ToString(), 14, ElarionUi.ParchmentDim,
                FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(0.10f, 1f));

            MakeText(rowGo.transform, row.PatronName, 16, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.11f, 0.38f), new Vector2(0.66f, 1f));

            // The monument state is a WORD, never a colour or an icon. Per patron, always.
            string state = row.MonumentIsBespoke ? MonumentRaisedLabel : MonumentPendingLabel;
            MakeText(rowGo.transform, state, 12, ElarionUi.Parchment, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0.11f, 0f), new Vector2(0.66f, 0.38f));

            // A DATE, never a timestamp: the hour somebody paid is nobody else's business.
            MakeText(rowGo.transform, row.FoundedOn ?? "", 13, ElarionUi.Aether, FontStyles.Normal,
                TextAlignmentOptions.Right, new Vector2(0.66f, 0f), new Vector2(1f, 1f));
        }

        // ---------------------------------------------------------------------
        //  uGUI helpers (the LeaderboardPanel px-band primitives)
        // ---------------------------------------------------------------------

        private static Transform PxBandFromTop(Transform parent, string name,
            float xMin, float xMax, float topPx, float heightPx)
        {
            var rt = NewBand(parent, name);
            rt.anchorMin = new Vector2(xMin, 1f);
            rt.anchorMax = new Vector2(xMax, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMax = new Vector2(0f, -topPx);
            rt.offsetMin = new Vector2(0f, -(topPx + heightPx));
            return rt.transform;
        }

        private static Transform PxBandFromBottom(Transform parent, string name,
            float xMin, float xMax, float bottomPx, float heightPx)
        {
            var rt = NewBand(parent, name);
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMax, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, bottomPx);
            rt.offsetMax = new Vector2(0f, bottomPx + heightPx);
            return rt.transform;
        }

        private static Transform PxStretchBand(Transform parent, string name,
            float xMin, float xMax, float topInsetPx, float bottomInsetPx)
        {
            var rt = NewBand(parent, name);
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMax, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(0f, bottomInsetPx);
            rt.offsetMax = new Vector2(0f, -topInsetPx);
            return rt.transform;
        }

        private static RectTransform NewBand(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }
    }
}
