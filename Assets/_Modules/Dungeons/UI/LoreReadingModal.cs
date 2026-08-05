// =============================================================================
// LoreReadingModal (WO-770.4, fixes D6) — the reading panel a lore stone opens.
// CODE-BUILT uGUI on the Obsidian kit (ElarionUiKit) — NO uxml (CLAUDE.md §8:
// uxml does not work in builds). Subscribes (via DungeonController) to a stone's
// LoreStone.ReadRequested and renders the canon title + body verbatim. The stone
// module was previously a triple gap: no input caller for Read(), no subscriber
// for ReadRequested, and no view — this file + the LoreStone input + the
// HydrateLoreStones wire close all three.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons (references DeNelle.Core -> ElarionUiKit). Colour never
// carries meaning; PanelManager-registered (top-band modal must register, §arbiter).
// -----------------------------------------------------------------------------
// WO-881 (2026-08-05) — the scroll well was re-banded in FIXED PIXELS and given a
// visible affordance. NOTHING in this file corrects copy: the title "Alduin's
// journal" is rendered VERBATIM from lore-fragments.json and is CORRECT canon
// (Alduin the Mournful, the necromancer whose journal this is — canon-strings.json
// "alduin"; docs/narrative-bible.md §"The leader"). He is NOT the Ice Echo Aldwin
// (EchoRosterCatalog #1) — two distinct characters, one letter apart. Do not
// "fix" either name here or in the data; DungeonLoreReadableRegression pins both.
// =============================================================================
using System;
using System.Text;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Dungeons
{
    /// <summary>Code-built Obsidian reading modal for a lore stone. Call <see cref="Show"/>.</summary>
    public sealed class LoreReadingModal : MonoBehaviour
    {
        // ── FIXED-PIXEL BANDS (WO-881; the WO-841/852 law) ────────────────────────
        // Every band below is a REFERENCE-PIXEL constant, never a fraction of the
        // parent rect. A fraction band is what resolved to MINUS 11px on the rumor
        // board (2026-08-02) — TMP then culled the whole quest body and the panel
        // read as blank. Fixed px cannot go negative, and each is >= the font's line
        // height so a band can never be shorter than the glyphs it must show.
        /// <summary>Side gutter inside the body well (reference px).</summary>
        private const float WellPadX = 26f;
        /// <summary>Gap between the title band and the top of the scroll well (px).</summary>
        private const float WellPadTop = 10f;
        /// <summary>Gap between the hint band and the well's floor (px).</summary>
        private const float WellPadBottom = 8f;
        /// <summary>Fixed footer band inside the body that carries the overflow hint.
        /// >= FontLabel(40) line height so the hint can never be clipped.</summary>
        private const float HintBandPx = 52f;
        /// <summary>Right inset that reserves the scrollbar gutter (px).</summary>
        private const float ScrollbarPx = 14f;
        /// <summary>Floor for the prose row: >= FontBody(50) line height, so even a
        /// zero-measure frame leaves one full readable line instead of collapsing.</summary>
        private const float BodyLinePx = 64f;
        /// <summary>Bottom pad inside the scrolling column so the final line clears the mask.</summary>
        private const int ContentPadBottom = 28;

        /// <summary>ASCII-only, TEXT-ENCODED overflow state (never colour alone): shown
        /// while there is unread prose below the mask, cleared once the reader is at the end.</summary>
        private const string HintMore = "v  MORE BELOW - DRAG TO SCROLL  v";
        /// <summary>ASCII-only end-of-entry marker (the other half of the text-encoded state).</summary>
        private const string HintEnd = "- END OF ENTRY -";

        private GameObject _canvas;
        private PanelHandle _handle;
        private bool _closed;

        // Scroll-state readout (View-local layout only — no game state lives here).
        private ScrollRect _scroll;
        private RectTransform _viewport;
        private RectTransform _content;
        private TMP_Text _hint;

        /// <summary>Open the reading modal for a lore-read request (the LoreStone.ReadRequested payload).</summary>
        public static void Show(LoreReadRequest req)
        {
            if (req == null) return;
            var host = new GameObject("LoreReadingModal");
            host.AddComponent<LoreReadingModal>().Build(req);
        }

        private void Build(LoreReadRequest req)
        {
            using var _ = FlowTrace.Enter("Dungeon", $"LoreReadingModal.Show id='{req.LoreStoneId}'");

            _canvas = ElarionUiKit.BuildModalCanvas("LoreCanvas", 31000);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_canvas, gameObject.scene);

            // Scrim: dims + swallows taps; tap-outside closes.
            var scrim = ElarionUiKit.AddImage(_canvas.transform, "Scrim",
                Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.72f), rounded: false);
            var scrimImg = scrim.GetComponent<Image>();
            if (scrimImg != null)
            {
                scrimImg.raycastTarget = true;
                var b = scrim.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(Close);
            }

            var chrome = ElarionUiKit.BuildObsidianPanel(_canvas.transform,
                string.IsNullOrEmpty(req.Title) ? "LORE" : req.Title.ToUpperInvariant(),
                new Vector2(0.12f, 0.16f), new Vector2(0.88f, 0.84f), onClose: Close,
                withBackdrop: false);

            Transform body = chrome.layout != null && chrome.layout.body != null
                ? chrome.layout.body.transform
                : chrome.content.transform;

            // Canon body — one paragraph per array entry, blank-line separated, verbatim.
            var sb = new StringBuilder();
            string[] paras = req.Body ?? Array.Empty<string>();
            for (int i = 0; i < paras.Length; i++)
            {
                if (i > 0) sb.Append("\n\n");
                sb.Append(paras[i]);
            }
            if (sb.Length == 0) sb.Append("The inscription on this stone has worn away.");

            // ── SCROLL WELL (WO-795 well, re-banded by WO-881) ───────────────────
            // The capture (LoreReadingModal_2340x1080.png) cut journal entry 4 mid-line
            // just above Close: the well DID mask, but nothing told the reader there was
            // more, and the well was sized by FRACTIONS of the body zone (0.06..0.94) —
            // the exact class that resolved NEGATIVE on the rumor board. Rebuilt here as
            // host/viewport/content with FIXED-PIXEL insets + a real scrollbar + a
            // text-encoded overflow hint in its own fixed footer band. Close is untouched:
            // the kit already seats it as a fixed 360x132px box (CanonCtaWidth/Height) and
            // BuildObsidianPanel's close-band reservation already raised this body zone's
            // floor above it, so the well geometrically ends above Close.

            // Host: carries the ScrollRect, inset from the body zone in PIXELS. The hint
            // band's height is subtracted here, so the two bands can never overlap.
            var hostGo = new GameObject("LoreScrollWell", typeof(RectTransform), typeof(ScrollRect));
            hostGo.transform.SetParent(body, false);
            var hrt = hostGo.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = new Vector2(WellPadX, WellPadBottom + HintBandPx);
            hrt.offsetMax = new Vector2(-WellPadX, -WellPadTop);

            // Viewport: the mask + the drag-catching raycast surface. Right-inset by the
            // scrollbar gutter so the thumb never sits on top of a glyph.
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(hostGo.transform, false);
            var vpr = viewportGo.GetComponent<RectTransform>();
            vpr.anchorMin = Vector2.zero;
            vpr.anchorMax = Vector2.one;
            vpr.offsetMin = Vector2.zero;
            vpr.offsetMax = new Vector2(-ScrollbarPx, 0f);
            vpr.pivot     = new Vector2(0f, 1f);          // ScrollRect viewport convention
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // drag catcher

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var cr = contentGo.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot     = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = Vector2.zero;
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth  = true; vlg.childForceExpandWidth  = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 0f;
            // Bottom pad so the final line scrolls fully clear of the mask edge.
            vlg.padding = new RectOffset(0, 0, 4, ContentPadBottom);
            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Slim auto-hiding gilt scrollbar (kit look) — the AFFORDANCE the capture lacked.
            var scrollbar = BuildScrollbar(hostGo.transform);

            var scroll = hostGo.GetComponent<ScrollRect>();
            scroll.viewport = vpr;
            scroll.content  = cr;
            scroll.horizontal = false;
            scroll.vertical   = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
            scroll.verticalScrollbar = scrollbar;
            // AutoHide (NOT AutoHideAndExpandViewport): expanding would let the ScrollRect
            // rewrite the viewport's sizeDelta and undo the fixed-pixel insets above.
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var text = ElarionUiKit.Label(contentGo.transform, sb.ToString(),
                0f, 1f, ElarionUi.Parchment, ElarionUi.FontBody,
                TextAlignmentOptions.TopLeft, 0f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            // NO FitBlock here (owner law: the scroll carries overflow at a fixed readable size).
            // FIXED-PIXEL FLOOR on the prose row: the layout column controls this child's
            // height from TMP's preferred size, and a preferred size of 0 on the creation
            // frame would collapse the column to nothing (built-but-invisible). minHeight is
            // a hard px floor >= one FontBody line, so the reader always gets a line to read.
            var le = text.gameObject.AddComponent<LayoutElement>();
            le.minHeight = BodyLinePx;
            le.flexibleHeight = 0f;

            // Fixed-pixel hint band, pinned to the BODY's floor (not a fraction of it), so
            // it sits between the scroll well and the kit's Close band and never overlaps
            // either. State is carried by the WORDS, never by colour.
            _hint = ElarionUiKit.Label(body, HintMore, 0f, 0f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center, 0f, 1f);
            var hintRt = _hint.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot     = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(-2f * WellPadX, HintBandPx);
            hintRt.anchoredPosition = new Vector2(0f, WellPadBottom);
            _hint.raycastTarget = false;

            _scroll   = scroll;
            _viewport = vpr;
            _content  = cr;
            // Measure NOW so a single-frame headless capture shows the true hint state
            // (LateUpdate would be a frame late, and the UI-capture harness renders once).
            LayoutRebuilder.ForceRebuildLayoutImmediate(cr);
            RefreshScrollHint();

            // §12 — one line naming every band in PIXELS. If a lore entry ever reads as
            // blank/clipped again, this line answers "was a band negative or sub-line?"
            // from data instead of theory (the rumor board's -11px band, 2026-08-02).
            FlowTrace.Step("Dungeon", string.Format(
                "LoreReadingModal bands(px): padX={0:F0} padTop={1:F0} padBottom={2:F0} " +
                "hintBand={3:F0} scrollbar={4:F0} bodyLineFloor={5:F0} paras={6} chars={7} " +
                "— fixed px, never fractions of parent (WO-881)",
                WellPadX, WellPadTop, WellPadBottom, HintBandPx, ScrollbarPx, BodyLinePx,
                paras.Length, sb.Length));

            if (_handle == null)
                _handle = PanelManager.Register("LoreReading", Close, () => !_closed && _canvas != null);
            PanelManager.NotifyOpened(_handle);
        }

        /// <summary>Slim, auto-hiding vertical scrollbar seated in the well's right gutter
        /// (fixed <see cref="ScrollbarPx"/> px wide — a fraction would vanish on a short panel).
        /// Mirrors ElarionUiKit.MakeScrollZone's bar so the lore well reads like every other
        /// kit scroller; shape + position carry the meaning, not the colour.</summary>
        private static Scrollbar BuildScrollbar(Transform host)
        {
            var sbGo = new GameObject("ScrollbarV", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbGo.transform.SetParent(host, false);
            var sbrt = (RectTransform)sbGo.transform;
            sbrt.anchorMin = new Vector2(1f, 0f);
            sbrt.anchorMax = Vector2.one;
            sbrt.pivot     = new Vector2(1f, 1f);
            sbrt.offsetMin = new Vector2(-ScrollbarPx, 0f);
            sbrt.offsetMax = Vector2.zero;
            sbGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            var slideArea = new GameObject("SlidingArea", typeof(RectTransform));
            slideArea.transform.SetParent(sbGo.transform, false);
            var sart = (RectTransform)slideArea.transform;
            sart.anchorMin = Vector2.zero; sart.anchorMax = Vector2.one;
            sart.offsetMin = new Vector2(2f, 2f); sart.offsetMax = new Vector2(-2f, -2f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(slideArea.transform, false);
            var handleRt = (RectTransform)handleGo.transform;
            handleRt.offsetMin = Vector2.zero; handleRt.offsetMax = Vector2.zero;
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = new Color(0.72f, 0.60f, 0.34f, 0.85f);

            var sb = sbGo.GetComponent<Scrollbar>();
            sb.handleRect = handleRt;
            sb.targetGraphic = handleImg;
            sb.direction = Scrollbar.Direction.BottomToTop;
            return sb;
        }

        /// <summary>TEXT-ENCODED overflow readout: says MORE BELOW while unread prose is
        /// masked, END OF ENTRY once the reader has reached the bottom, and nothing at all
        /// when the entry fit without scrolling. Pure layout observation — reads rect sizes
        /// only, owns no state and touches no VM (View = layout/render only).</summary>
        private void RefreshScrollHint()
        {
            if (_hint == null || _content == null || _viewport == null) return;
            // No usable measurement yet (creation frame / canvas scaler not applied): keep the
            // authored MORE-BELOW default rather than falsely clearing it. LateUpdate re-reads.
            if (_viewport.rect.height <= 1f || _content.rect.height <= 1f) return;

            float overflowPx = _content.rect.height - _viewport.rect.height;
            if (overflowPx <= 2f)
            {
                // The whole entry fits — no affordance needed, and no empty band shown.
                if (_hint.text.Length != 0) _hint.text = string.Empty;
                return;
            }

            // normalizedPosition.y: 1 = top, 0 = bottom (ScrollRect convention).
            bool atBottom = _scroll == null || _scroll.verticalNormalizedPosition <= 0.02f;
            string want = atBottom ? HintEnd : HintMore;
            if (!string.Equals(_hint.text, want, StringComparison.Ordinal)) _hint.text = want;
        }

        private void LateUpdate()
        {
            if (_closed) return;
            RefreshScrollHint();
        }

        private void Close()
        {
            if (_closed) return;
            _closed = true;
            if (_handle != null) PanelManager.NotifyClosed(_handle);
            if (_canvas != null) Destroy(_canvas);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_handle != null) PanelManager.NotifyClosed(_handle);
            if (_canvas != null) Destroy(_canvas);
        }
    }
}
