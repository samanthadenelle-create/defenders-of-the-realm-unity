using DeNelle.Core.UI;
using TMPro;
using UnityEngine;

namespace DeNelle.GooglePlay
{
    internal sealed class GooglePlayStorefront : MonoBehaviour
    {
        private static GooglePlayStorefront _active;
        private GooglePlayStorefrontVM _vm;
        private ElarionUiKit.ObsidianModal _modal;
        private TextMeshProUGUI _status;
        private PanelHandle _panelHandle;
        private bool _open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRoute() => PanelRouter.Register(PanelId.RealmStore, Open);

        private static void Open()
        {
            if (_active == null)
            {
                var canvas = ElarionUiKit.BuildModalCanvas("GooglePlayStoreHost", 31000);
                _active = canvas.AddComponent<GooglePlayStorefront>();
                _active.Build();
            }
            _active.SetOpen(true);
        }

        private void Awake()
        {
            _vm = GooglePlayStorefrontVM.CreateDefault(SetStatus);
            _panelHandle = PanelManager.Register("Google Play Realm Store", Close, () => _open);
        }

        private void OnDestroy()
        {
            if (_active == this) _active = null;
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
        }

        private void Build()
        {
            // WO-1398: the Play skin titles itself with the store's ONE canon name (storeWordmark),
            // the same words the HUD card that opened it rendered - never a typed literal.
            _modal = ElarionUiKit.BuildObsidianModal("GooglePlayRealmStore", HudStrings.StoreFaceLabel("play-skin"),
                new Vector2(.08f, .04f), new Vector2(.92f, .96f), Close,
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "shop");
            var body = _modal.chrome.layout != null && _modal.chrome.layout.body != null
                ? _modal.chrome.layout.body.transform : _modal.chrome.content.transform;
            ElarionUiKit.Label(body, "Secure purchases through Google Play", .92f, .99f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Center, .02f, .98f);

            var rows = _vm.Rows;
            float top = .90f, height = .095f;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                float y1 = top - i * height, y0 = y1 - .082f;
                ElarionUiKit.BuildObsidianButton(body, row.Label,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    row.Available ? ElarionUiKit.ObsidianButtonColor.Green : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(.03f, y0), new Vector2(.97f, y1),
                    row.Available ? () => _vm.Purchase(row.Sku) : (System.Action)null);
            }

            ElarionUiKit.BuildObsidianButton(body, "Restore purchases",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(.03f, .20f), new Vector2(.97f, .285f), _vm.Restore);
            ElarionUiKit.BuildObsidianButton(body, "Request account and data deletion",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(.03f, .105f), new Vector2(.97f, .19f), _vm.RequestDeletion);
            _status = ElarionUiKit.Label(body, "", .01f, .095f, ElarionUi.ParchmentDim,
                ElarionUi.FontBody, TextAlignmentOptions.Center, .03f, .97f);
            _modal.canvas.SetActive(false);
        }

        private void SetOpen(bool open)
        {
            if (_modal == null || _modal.canvas == null) return;
            _open = open;
            _modal.canvas.SetActive(open);
            if (open && !PanelManager.NotifyOpened(_panelHandle))
            {
                _open = false;
                _modal.canvas.SetActive(false);
            }
            else if (!open) PanelManager.NotifyClosed(_panelHandle);
        }

        private void Close() => SetOpen(false);
        private void SetStatus(string value) { if (_status != null) _status.text = value ?? string.Empty; }
    }
}
