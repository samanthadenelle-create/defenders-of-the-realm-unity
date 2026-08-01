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
        private GameObject _canvas;
        private PanelHandle _handle;
        private bool _closed;

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

            // WO-795: long lore scrolls at a fixed readable size (reflow, never shrink
            // fonts) -- vertical ScrollRect well on the body band, RumorBoardPanel pattern.
            var viewportGo = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(body, false);
            var vpr = viewportGo.GetComponent<RectTransform>();
            vpr.anchorMin = new Vector2(0.06f, 0.06f);
            vpr.anchorMax = new Vector2(0.94f, 0.94f);
            vpr.offsetMin = Vector2.zero;
            vpr.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // drag catcher

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var cr = contentGo.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot     = new Vector2(0.5f, 1f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth  = true; vlg.childForceExpandWidth  = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 0f;
            // Bottom pad so the final line scrolls fully clear of the mask edge.
            vlg.padding = new RectOffset(0, 0, 4, 24);
            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.viewport = vpr;
            scroll.content  = cr;
            scroll.horizontal = false;
            scroll.vertical   = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            var text = ElarionUiKit.Label(contentGo.transform, sb.ToString(),
                0f, 1f, ElarionUi.Parchment, ElarionUi.FontBody,
                TextAlignmentOptions.TopLeft, 0f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            // NO FitBlock here (owner law: the scroll carries overflow at a fixed readable size).

            if (_handle == null)
                _handle = PanelManager.Register("LoreReading", Close, () => !_closed && _canvas != null);
            PanelManager.NotifyOpened(_handle);
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
