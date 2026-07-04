// =============================================================================
// BugReportView — WO-596: the player-facing bug-report form (the F8 harness,
// skinned for players). MVVM strict: this View renders BugReportVM and raises
// its commands — it never builds payloads, never reads game state.
// -----------------------------------------------------------------------------
// Built on the ONE master frame factory (ElarionUiKit.BuildObsidianModal +
// drop-zones, per docs/UI_BLINK_TEMPLATE_CANON.md). Owner-ratified form:
//   [header]  REPORT A BUG  (+ the ONE shared Close)
//   [body]    screenshot thumbnail (captured on open, clean frame, fades in)
//             "Include screenshot" untickable toggle (default ON)
//             note field — "What went wrong?"
//             disclosure: "Includes recent game logs to help us fix it."
//   [footer]  Send report  (single CTA — the submit IS the consent)
// Confirmation = toast ("Report sent — thank you, defender."), no modal.
//
// OPEN ORDER MATTERS: the clean-frame capture (BreakCaptureHarness.
// CaptureForReport — privacy-registered UI hidden for the frame) runs BEFORE
// this form builds, so the form is never in its own screenshot. Callers must
// close their own menu first (HelpMenu does).
//
// SMOOTHNESS (owner directive 2026-07-02): eased open/close (~0.2s scale+fade),
// thumbnail fade-in on capture, brief working state on submit, eased toast.
// Tween = the local unscaled-time Ease() below (no DOTween; WebGL-safe; the
// same coroutine pattern as VillageHudController.AnimateIn).
// NOTE (kit promotion candidate): Ease() belongs in ElarionUiKit once a second
// surface needs it — kept local per WO-596 (do not edit ElarionUiKit).
// =============================================================================
using System;
using System.Collections;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.HUD
{
    /// <summary>WO-596 — Obsidian master-frame bug-report form. Open via
    /// <see cref="Open"/> (Settings → "Report a bug"). Transient: destroys itself on close.</summary>
    [DisallowMultipleComponent]
    public sealed class BugReportView : MonoBehaviour
    {
        private const float OpenSeconds  = 0.22f;
        private const float CloseSeconds = 0.15f;
        private const float ThumbFadeSeconds = 0.25f;

        private BugReportVM _vm;
        private PanelHandle _panelHandle;
        private GameObject _canvasRoot;          // the modal canvas (parented under us)
        private CanvasGroup _panelGroup;         // fades the whole modal
        private RectTransform _panelRect;        // scales the frame on open/close
        private RawImage _thumb;
        private CanvasGroup _thumbGroup;
        private TextMeshProUGUI _thumbHint;
        private Texture2D _thumbTex;
        private Button _toggleBtn;
        private TextMeshProUGUI _toggleLabel;
        private Button _sendBtn;
        private TextMeshProUGUI _sendLabel;
        private TMP_InputField _noteInput;
        private float _prevTimeScale = 1f;
        private bool _closing;

        /// <summary>Open the bug-report form (no-op when one is already open).</summary>
        public static void Open()
        {
            if (UnityEngine.Object.FindAnyObjectByType<BugReportView>() != null) return;
            var go = new GameObject("BugReportView");
            go.AddComponent<BugReportView>();
        }

        private void Awake()
        {
            FlowTrace.Step("BugReport", "open — capturing clean frame before the form draws");
            _vm = new BugReportVM();
            _vm.Changed += Repaint;
            _panelHandle = PanelManager.Register("BugReport", Close, () => _canvasRoot != null && !_closing);

            // Freeze so typing the note can't drive the hero (same trick as the F8 note box).
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            StartCoroutine(OpenRoutine());
        }

        private void OnDestroy()
        {
            Guard.Try("BugReport", "view teardown", () =>
            {
                Time.timeScale = _prevTimeScale;
                if (_vm != null) _vm.Changed -= Repaint;
                PanelManager.NotifyClosed(_panelHandle);
                if (_thumbTex != null) Destroy(_thumbTex);
            });
        }

        private IEnumerator OpenRoutine()
        {
            // 1) Clean-frame capture FIRST (privacy UI hidden for the frame; reuses the
            //    factored F8 path — no capture code lives in this assembly).
            yield return BreakCaptureHarness.CaptureForReport(cap => _vm.AttachCapture(cap));

            // 2) Now build + reveal the form.
            Guard.Try("BugReport", "build form", BuildUi);
            if (_canvasRoot == null) { Destroy(gameObject); yield break; }
            PanelManager.NotifyOpened(_panelHandle);
            Repaint();

            // Eased open: scale 0.92 → 1 + fade 0 → 1 (unscaled — the game is frozen).
            if (_panelGroup != null) _panelGroup.alpha = 0f;
            yield return Ease(OpenSeconds, k =>
            {
                if (_panelGroup != null) _panelGroup.alpha = k;
                if (_panelRect  != null) _panelRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, k);
            });

            // Thumbnail fades in once revealed (it was bound in Repaint at alpha 0).
            if (_thumbGroup != null && _thumbTex != null)
                yield return Ease(ThumbFadeSeconds, k => { if (_thumbGroup != null) _thumbGroup.alpha = k; });
        }

        // ── Build (drop-zones only — the frame IS the chrome) ───────────────────
        private void BuildUi()
        {
            var modal = ElarionUiKit.BuildObsidianModal("BugReportModal", "REPORT A BUG",
                new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f), Close,
                sortingOrder: 31000, frameName: RpgUiCatalog.FrameSettings);
            _canvasRoot = modal.canvas;
            _canvasRoot.transform.SetParent(transform, false);   // dies with the view
            _panelGroup = _canvasRoot.AddComponent<CanvasGroup>();
            _panelRect  = modal.chrome.root != null ? modal.chrome.root.GetComponent<RectTransform>() : null;

            var chrome = modal.chrome;
            RectTransform body = (chrome.layout != null && chrome.layout.body != null)
                ? chrome.layout.body
                : chrome.content.GetComponent<RectTransform>();

            // Screenshot thumbnail — top of the body well. RawImage under a CanvasGroup so
            // it can FADE IN when the capture binds (owner smoothness directive).
            var thumbHost = new GameObject("Thumbnail", typeof(RectTransform), typeof(CanvasGroup));
            thumbHost.transform.SetParent(body, false);
            var thr = (RectTransform)thumbHost.transform;
            thr.anchorMin = new Vector2(0.08f, 0.60f); thr.anchorMax = new Vector2(0.92f, 0.98f);
            thr.offsetMin = Vector2.zero; thr.offsetMax = Vector2.zero;
            _thumbGroup = thumbHost.GetComponent<CanvasGroup>();
            _thumbGroup.alpha = 0f;                       // fades in once the capture binds
            _thumbGroup.interactable = false; _thumbGroup.blocksRaycasts = false;

            var thumbGo = new GameObject("Shot", typeof(RawImage));
            thumbGo.transform.SetParent(thumbHost.transform, false);
            var trt = (RectTransform)thumbGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            _thumb = thumbGo.GetComponent<RawImage>();
            _thumb.raycastTarget = false;

            _thumbHint = ElarionUiKit.Label(thumbHost.transform, "", 0f, 1f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Center);

            // "Include screenshot" untickable toggle (default ON) — kit Quiet button.
            _toggleBtn = ElarionUiKit.Button(body, "", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.08f, 0.51f), new Vector2(0.92f, 0.585f), () => _vm.ToggleScreenshot());
            _toggleLabel = _toggleBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Note field — "What went wrong?" (multi-line). The plate is a minimal
            // translucent well so the input is visible; content, not chrome.
            BuildNoteInput(body, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.49f));

            // The one quiet disclosure line — the honesty (logs always go; not a checkbox).
            ElarionUiKit.Label(body, "Includes recent game logs to help us fix it.",
                0.155f, 0.205f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.08f, 0.92f);

            // Single CTA — the submit IS the consent. FrameSettings carves NO footer zone, so the
            // button lives in a THIN band at the bottom of the body (#18: the old footer→body
            // fallback let its 0.06..0.94 anchors inflate the gold button to fill the whole well).
            // The body's bottom edge sits ABOVE the shared Close, so they never collide.
            _sendBtn = ElarionUiKit.Button(body, "Send report", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.14f), OnSendClicked);
            _sendLabel = _sendBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void BuildNoteInput(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var host = new GameObject("NoteInput", typeof(Image), typeof(TMP_InputField));
            host.transform.SetParent(parent, false);
            var rt = (RectTransform)host.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = host.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);      // translucent well over the frame art
            ElarionUiKit.ApplyRounded(bg);

            var areaGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            areaGo.transform.SetParent(host.transform, false);
            var art = (RectTransform)areaGo.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(14f, 10f); art.offsetMax = new Vector2(-14f, -10f);

            var text = ElarionUiKit.Label(areaGo.transform, "", 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.TopLeft, 0f, 1f);
            var placeholder = ElarionUiKit.Label(areaGo.transform, "What went wrong?", 0f, 1f,
                ElarionUi.ParchmentDim, ElarionUi.FontBody, TextAlignmentOptions.TopLeft, 0f, 1f);
            placeholder.fontStyle = FontStyles.Italic;

            _noteInput = host.GetComponent<TMP_InputField>();
            _noteInput.targetGraphic  = bg;
            _noteInput.textViewport   = art;
            _noteInput.textComponent  = text;
            _noteInput.placeholder    = placeholder;
            _noteInput.lineType       = TMP_InputField.LineType.MultiLineNewline;
            _noteInput.characterLimit = 1000;
            _noteInput.onValueChanged.AddListener(v => _vm.SetNote(v));
        }

        // ── Bind (render the VM; no state lives here) ────────────────────────────
        private void Repaint()
        {
            if (_canvasRoot == null || _vm == null) return;
            Guard.Try("BugReport", "repaint", () =>
            {
                // Thumbnail: bind once when bytes arrive; dim when toggled off.
                if (_thumb != null)
                {
                    if (_thumbTex == null && _vm.ScreenshotJpg != null)
                    {
                        _thumbTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                        _thumbTex.LoadImage(_vm.ScreenshotJpg);
                        _thumb.texture = _thumbTex;
                    }
                    _thumb.color = _vm.IncludeScreenshot ? Color.white : new Color(1f, 1f, 1f, 0.25f);
                    if (_thumbHint != null)
                        _thumbHint.text = _vm.ScreenshotJpg == null ? "(no screenshot available)"
                                        : _vm.IncludeScreenshot ? "" : "screenshot excluded";
                }

                if (_toggleLabel != null)
                    _toggleLabel.text = (_vm.IncludeScreenshot ? "[x] " : "[  ] ") + "Include screenshot";
                if (_toggleBtn != null)
                    _toggleBtn.interactable = _vm.State != BugReportVM.Stage.Sending;

                if (_sendBtn != null)
                {
                    _sendBtn.interactable = _vm.CanSubmit;
                    if (_sendLabel != null)
                        _sendLabel.text = _vm.State == BugReportVM.Stage.Sending ? "Sending…"
                                        : _vm.State == BugReportVM.Stage.Failed  ? "Retry send"
                                        : "Send report";
                }
                if (_noteInput != null)
                    _noteInput.interactable = _vm.State != BugReportVM.Stage.Sending;

                if (_vm.State == BugReportVM.Stage.Sent && !_closing)
                {
                    BugReportToast.Show("Report sent — thank you, defender.", ElarionUiKit.ToastTone.Confirm);
                    Close();
                }
                else if (_vm.State == BugReportVM.Stage.Failed)
                {
                    BugReportToast.Show("Couldn't send — check your connection and retry.", ElarionUiKit.ToastTone.Danger);
                }
            });
        }

        private void OnSendClicked()
        {
            if (_vm == null || !_vm.CanSubmit) return;
            StartCoroutine(_vm.Submit());   // VM owns the transport; view just runs it
        }

        // ── Close (eased out, then die) ──────────────────────────────────────────
        public void Close()
        {
            if (_closing) return;
            _closing = true;
            FlowTrace.Step("BugReport", "close");
            if (isActiveAndEnabled) StartCoroutine(CloseRoutine());
            else Destroy(gameObject);
        }

        private IEnumerator CloseRoutine()
        {
            yield return Ease(CloseSeconds, k =>
            {
                if (_panelGroup != null) _panelGroup.alpha = 1f - k;
                if (_panelRect  != null) _panelRect.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 0.94f, k);
            });
            Destroy(gameObject);
        }

        // Small unscaled-time ease-out-cubic tween (kit promotion candidate — see header).
        private static IEnumerator Ease(float duration, Action<float> apply)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                apply(1f - Mathf.Pow(1f - k, 3f));
                yield return null;
            }
            apply(1f);
        }

        // ── Toast — outlives the panel; kit ToastCard on its own tiny canvas ─────
        private sealed class BugReportToast : MonoBehaviour
        {
            private const float HoldSeconds = 2.4f;
            private CanvasGroup _group;

            public static void Show(string message, ElarionUiKit.ToastTone tone)
            {
                Guard.Try("BugReport", "toast", () =>
                {
                    var go = new GameObject("BugReportToast");
                    var toast = go.AddComponent<BugReportToast>();
                    toast.Build(message, tone);
                });
            }

            private void Build(string message, ElarionUiKit.ToastTone tone)
            {
                var canvasGo = ElarionUiKit.BuildModalCanvas("BugReportToastCanvas", 32000);
                canvasGo.transform.SetParent(transform, false);
                _group = canvasGo.AddComponent<CanvasGroup>();
                _group.alpha = 0f;
                _group.interactable = false; _group.blocksRaycasts = false;

                var parts = ElarionUiKit.ToastCard(canvasGo.transform, tone,
                    accentLeft: true, align: TextAnchor.MiddleCenter);
                var rt = (RectTransform)parts.card.transform;
                rt.anchorMin = new Vector2(0.12f, 0.10f);
                rt.anchorMax = new Vector2(0.88f, 0.16f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                parts.label.text = message;

                StartCoroutine(Run());
            }

            private IEnumerator Run()
            {
                yield return Ease(0.20f, k => { if (_group != null) _group.alpha = k; });
                float t = 0f;
                while (t < HoldSeconds) { t += Time.unscaledDeltaTime; yield return null; }
                yield return Ease(0.30f, k => { if (_group != null) _group.alpha = 1f - k; });
                Destroy(gameObject);
            }
        }
    }
}
