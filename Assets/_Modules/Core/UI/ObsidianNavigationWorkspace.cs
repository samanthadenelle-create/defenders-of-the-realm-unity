using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Shared mobile-first shell for card-led destinations. Feature panels supply page models and
    /// content; this class supplies the one modal/pause lifetime and the universal Back/Close law.
    /// </summary>
    public abstract class ObsidianNavigationWorkspace<TPage> : MonoBehaviour
    {
        public const string HoldReason = "obsidian-navigation-workspace";

        private readonly NavigationStack<TPage> _navigation = new NavigationStack<TPage>();
        private PanelHandle _panelHandle;
        private WorldHold.Handle _hold;
        private GameObject _canvas;
        private RectTransform _content;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _subtitle;
        private Button _back;
        private bool _open;

        protected NavigationStack<TPage> Navigation => _navigation;
        protected RectTransform Content => _content;
        public bool IsOpen => _open;

        protected abstract string WorkspaceName { get; }
        protected abstract string TitleFor(TPage page);
        protected virtual string SubtitleFor(TPage page) => string.Empty;
        protected abstract void RenderPage(TPage page, RectTransform content);

        protected virtual void Awake()
        {
            _navigation.Changed += RenderCurrent;
            BuildShell();
        }

        public bool Open(TPage root)
        {
            EnsureShell();
            if (_open)
            {
                _navigation.OpenRoot(root);
                return true;
            }

            _open = true;
            _canvas.SetActive(true);
            _hold = WorldHold.Acquire(HoldReason + ":" + WorkspaceName);
            _panelHandle ??= PanelManager.Register(WorkspaceName, Close, () => _open);
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                Close();
                return false;
            }

            _navigation.OpenRoot(root);
            FlowTrace.Step("Navigation", "opened workspace '" + WorkspaceName + "' at root");
            return true;
        }

        protected void Push(TPage page)
        {
            _navigation.Push(page);
            FlowTrace.Step("Navigation", "'" + WorkspaceName + "' pushed depth=" + _navigation.Count);
        }

        /// <summary>Repaint the current page without adding or removing history.</summary>
        protected void Refresh()
        {
            if (_open && _navigation.Count > 0) RenderCurrent();
        }

        public bool Back()
        {
            bool moved = _navigation.Back();
            FlowTrace.Step("Navigation", "'" + WorkspaceName + "' Back " +
                (moved ? "returned to depth=" + _navigation.Count : "refused at root"));
            return moved;
        }

        /// <summary>
        /// Complete the current task, close the modal lifetime, then return to the
        /// declared parent/mode. A failed commit leaves the workspace open so the
        /// player never loses context or receives a false completion.
        /// </summary>
        protected bool Done(Action commit, Action returnToParent = null)
        {
            if (commit != null && !Guard.Try("Navigation", "commit Done in '" + WorkspaceName + "'", commit))
                return false;
            Close();
            if (returnToParent != null)
                Guard.Try("Navigation", "return from Done in '" + WorkspaceName + "'", returnToParent);
            FlowTrace.Step("Navigation", "Done completed workspace '" + WorkspaceName + "'");
            return true;
        }

        public virtual void Close()
        {
            if (!_open && _hold == null) return;
            _open = false;
            _navigation.Clear();
            if (_canvas != null) _canvas.SetActive(false);
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            _hold?.Dispose();
            _hold = null;
            FlowTrace.Step("Navigation", "closed workspace '" + WorkspaceName + "' to world");
        }

        private void EnsureShell()
        {
            if (_canvas == null) BuildShell();
        }

        private void BuildShell()
        {
            if (_canvas != null) return;
            _canvas = ElarionUiKit.BuildModalCanvas(WorkspaceName + "Canvas", 31300);
            ElarionUiKit.Scrim(_canvas.transform, Close);
            var chrome = ElarionUiKit.BuildObsidianPanel(_canvas.transform, WorkspaceName.ToUpperInvariant(),
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Close);
            ApplyMedievalWorkspaceChrome(chrome);
            _content = chrome.layout != null ? chrome.layout.body : null;
            if (_content == null)
                throw new InvalidOperationException("Obsidian workspace requires a body drop-zone.");
            _title = chrome.title;

            _subtitle = ElarionUiKit.Label(chrome.content.transform, string.Empty, 0.78f, 0.84f,
                ElarionUi.Parchment, (int)ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.10f, 0.90f, bold: false);

            _back = ElarionUiKit.BuildObsidianButton(chrome.content.transform, "BACK",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.035f, 0.865f), new Vector2(0.205f, 0.955f), () => Back());
            MedievalUiSkin.ApplyButton(_back, primary: false);
            ElarionUiKit.ClampMinTouch(_back);
            _canvas.SetActive(false);
        }

        /// <summary>Shared public navigation workspaces use the approved Elarion
        /// medieval shell. Page content and navigation remain native/runtime-driven.</summary>
        private static void ApplyMedievalWorkspaceChrome(ElarionUiKit.PanelChrome chrome)
        {
            if (chrome == null) return;
            var rootImage = chrome.root != null ? chrome.root.GetComponent<Image>() : null;
            var frame = Resources.Load<Sprite>("UI/ElarionMedieval/frames/modal-frame-16x9");
            if (rootImage != null && frame != null)
            {
                rootImage.sprite = frame;
                rootImage.type = Image.Type.Simple;
                rootImage.color = Color.white;
            }
            if (chrome.layout != null && chrome.layout.medallion != null)
                chrome.layout.medallion.gameObject.SetActive(false);
            if (chrome.close == null) return;
            var closeImage = chrome.close.GetComponent<Image>();
            var close = Resources.Load<Sprite>("UI/ElarionMedieval/buttons/close-ornate");
            if (closeImage != null && close != null)
            {
                closeImage.sprite = close;
                closeImage.type = Image.Type.Simple;
                closeImage.color = Color.white;
            }
            var closeText = chrome.close.GetComponentInChildren<TMP_Text>();
            if (closeText != null) closeText.gameObject.SetActive(false);
        }

        private void RenderCurrent()
        {
            if (!_open || _content == null || _navigation.Count == 0) return;
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            TPage page = _navigation.Current;
            if (_title != null)
            {
                string heading = TitleFor(page).ToUpperInvariant();
                string prior = _title.text;
                // The shared chrome may paint a paired title/shadow TMP. Repaint the
                // complete header band so the construction-time workspace name cannot
                // remain visible behind the current page title.
                var header = _title.transform.parent;
                if (header != null)
                    foreach (var text in header.GetComponentsInChildren<TextMeshProUGUI>(true))
                        if (text == _title || string.Equals(text.text, prior, StringComparison.Ordinal))
                            text.text = heading;
                _title.text = heading;
            }
            if (_subtitle != null) _subtitle.text = SubtitleFor(page) ?? string.Empty;
            if (_back != null) _back.gameObject.SetActive(_navigation.CanGoBack);
            Guard.Try("Navigation", "render '" + WorkspaceName + "' page", () => RenderPage(page, _content));
        }

        private void Update()
        {
            if (_open) WorldHold.Renew(_hold);
        }

        protected virtual void OnDisable() => Close();
        protected virtual void OnDestroy()
        {
            _navigation.Changed -= RenderCurrent;
            Close();
        }
    }
}
