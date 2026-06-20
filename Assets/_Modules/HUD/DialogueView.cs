// =============================================================================
// DialogueView (DeNelle.HUD) — the dumb uGUI skin for OUR dialogue (WO-455).
// -----------------------------------------------------------------------------
// Code-built uGUI (canon: NOT UIDocument), styled with ElarionUiKit so it matches
// every other panel. Binds to a DialogueViewModel and renders it: a bottom box with
// speaker + text (tap to advance), and an option list when the VM is at a choice.
// The VIEW holds no game state — it reads the VM and calls Advance/Choose only.
// Self-bootstraps DDOL behind FeatureFlags.CustomDialogue.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Dialogue;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class DialogueView : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!DeNelle.Core.FeatureFlags.CustomDialogue) return; // migration flag (default off)
            var go = new GameObject("DialogueView");
            DontDestroyOnLoad(go);
            go.AddComponent<DialogueView>();
        }

        private DialogueViewModel _vm;
        private GameObject _ui;
        private TMPro.TextMeshProUGUI _speaker;
        private TMPro.TextMeshProUGUI _body;
        private RectTransform _box;       // the dialogue box (tap to advance)
        private RectTransform _optionsCol;
        private GameObject _tapHint;

        private void OnEnable() { DialogueService.Opened += OnOpened; }
        private void OnDisable() { DialogueService.Opened -= OnOpened; }

        private void OnOpened(DialogueViewModel vm)
        {
            if (_vm != null) Unbind();
            _vm = vm;
            _vm.Changed += Repaint;
            _vm.Closed += OnClosed;
            BuildUi();
            Repaint();
        }

        private void OnClosed() { Unbind(); if (_ui != null) { Destroy(_ui); _ui = null; } }

        private void Unbind()
        {
            if (_vm != null) { _vm.Changed -= Repaint; _vm.Closed -= OnClosed; _vm = null; }
        }

        // ── Build the bottom dialogue box ────────────────────────────────────────
        private void BuildUi()
        {
            if (_ui != null) Destroy(_ui);
            _ui = new GameObject("DialogueViewUI");
            _ui.transform.SetParent(transform, false);

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // above HUD, below hard modals
            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            _ui.AddComponent<GraphicRaycaster>();

            // The box: bottom band, tap anywhere on it to advance the line.
            var boxGo = new GameObject("Box", typeof(Image), typeof(Button));
            boxGo.transform.SetParent(_ui.transform, false);
            _box = boxGo.GetComponent<RectTransform>();
            _box.anchorMin = new Vector2(0.06f, 0.04f);
            _box.anchorMax = new Vector2(0.94f, 0.30f);
            _box.offsetMin = Vector2.zero; _box.offsetMax = Vector2.zero;
            var bg = ElarionUi.PanelStoneDark;
            boxGo.GetComponent<Image>().color = new Color(bg.r, bg.g, bg.b, 0.96f);
            boxGo.GetComponent<Button>().onClick.AddListener(OnBoxTapped);

            // Gilt rim
            var rim = new GameObject("Rim", typeof(Image));
            rim.transform.SetParent(boxGo.transform, false);
            var rr = rim.GetComponent<RectTransform>();
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = new Vector2(-3, -3); rr.offsetMax = new Vector2(3, 3);
            var rimImg = rim.GetComponent<Image>(); rimImg.color = new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.5f);
            rimImg.raycastTarget = false; rim.transform.SetAsFirstSibling();

            _speaker = MakeLabel(boxGo.transform, "Speaker", new Vector2(0.03f, 0.74f), new Vector2(0.97f, 0.98f),
                20, ElarionUi.Gilt, TMPro.FontStyles.Bold, TMPro.TextAlignmentOptions.Left);
            _body = MakeLabel(boxGo.transform, "Body", new Vector2(0.03f, 0.12f), new Vector2(0.97f, 0.72f),
                16, ElarionUi.Parchment, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);

            _tapHint = MakeLabel(boxGo.transform, "TapHint", new Vector2(0.5f, 0.01f), new Vector2(0.97f, 0.12f),
                11, ElarionUi.ParchmentDim, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.BottomRight).gameObject;
            _tapHint.GetComponent<TMPro.TextMeshProUGUI>().text = "tap to continue";

            // Options column (above the box), built on demand.
            var col = new GameObject("Options");
            col.transform.SetParent(_ui.transform, false);
            _optionsCol = col.AddComponent<RectTransform>();
            _optionsCol.anchorMin = new Vector2(0.10f, 0.31f);
            _optionsCol.anchorMax = new Vector2(0.90f, 0.62f);
            _optionsCol.offsetMin = Vector2.zero; _optionsCol.offsetMax = Vector2.zero;
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.childControlHeight = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
            vlg.childAlignment = TextAnchor.LowerCenter;
        }

        private void OnBoxTapped()
        {
            if (_vm == null) return;
            if (!_vm.ShowingOptions) _vm.Advance();   // tapping the box advances lines, not choices
        }

        // ── Render the VM ────────────────────────────────────────────────────────
        private void Repaint()
        {
            if (_vm == null || _ui == null) return;
            bool open = _vm.IsOpen;
            _ui.SetActive(open);
            if (!open) return;

            if (_speaker != null) { _speaker.text = _vm.Speaker; _speaker.gameObject.SetActive(!string.IsNullOrEmpty(_vm.Speaker)); }
            if (_body != null) _body.text = _vm.Text;

            BuildOptions();
            if (_tapHint != null) _tapHint.SetActive(!_vm.ShowingOptions && !string.IsNullOrEmpty(_vm.Text));
        }

        private void BuildOptions()
        {
            if (_optionsCol == null) return;
            for (int i = _optionsCol.childCount - 1; i >= 0; i--) Destroy(_optionsCol.GetChild(i).gameObject);
            if (!_vm.ShowingOptions) return;

            var labels = _vm.OptionLabels;
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                var go = new GameObject("Opt" + i, typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(_optionsCol, false);
                go.GetComponent<LayoutElement>().minHeight = 64;
                var b = ElarionUi.PanelStone;
                go.GetComponent<Image>().color = new Color(b.r, b.g, b.b, 0.96f);
                go.GetComponent<Button>().onClick.AddListener(() => _vm?.Choose(idx));

                var lbl = MakeLabel(go.transform, "L", new Vector2(0.04f, 0f), new Vector2(0.96f, 1f),
                    15, ElarionUi.Parchment, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Left);
                lbl.text = labels[i];
                lbl.raycastTarget = false;
            }
        }

        private static TMPro.TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 aMin, Vector2 aMax,
            int size, Color col, TMPro.FontStyles style, TMPro.TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.fontSize = size; t.color = col; t.fontStyle = style; t.alignment = align;
            t.enableWordWrapping = true; t.raycastTarget = false;
            return t;
        }
    }
}
