using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;

namespace DeNelle.Village.UI
{
    /// <summary>One-tap summary of authoritative offline haul and Echo mending.</summary>
    public sealed class WelcomeBackPopup : MonoBehaviour
    {
        private static WelcomeBackPopup s_active;
        private OfflineHarvestResult _result;
        private ElarionUiKit.ObsidianModal _modal;
        private PanelHandle _panelHandle;
        private bool _open;

        public static void Show(OfflineHarvestResult result)
        {
            if (result == null || (result.Total <= 0 && !result.HasMendNews)) return;
            if (s_active != null) s_active.Dismiss();
            var host = new GameObject("WelcomeBackPopup");
            var popup = host.AddComponent<WelcomeBackPopup>();
            s_active = popup;
            popup._result = result;
            popup.BuildUi();
        }

        private void BuildUi()
        {
            _modal = ElarionUiKit.BuildObsidianModal("WelcomeBackUI", "WELCOME BACK, KEEPER",
                new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.92f), Dismiss,
                sortingOrder: 32020, frameName: RpgUiCatalog.FrameCore);
            if (_modal == null || _modal.canvas == null) { Dismiss(); return; }
            MedievalUiSkin.ApplyShell(_modal.chrome, compact: false);

            if (_modal.chrome.layout != null && _modal.chrome.layout.body != null)
            {
                var bodyRect = _modal.chrome.layout.body;
                bodyRect.anchorMin = new Vector2(bodyRect.anchorMin.x, 0.22f);
                bodyRect.anchorMax = new Vector2(bodyRect.anchorMax.x, 0.82f);
                bodyRect.offsetMin = Vector2.zero;
                bodyRect.offsetMax = Vector2.zero;
            }

            var body = _modal.chrome.layout != null && _modal.chrome.layout.body != null
                ? (Transform)_modal.chrome.layout.body : _modal.chrome.content.transform;

            var summary = ElarionUiKit.Label(body, AwayText(), 0.86f, 0.98f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.05f, 0.95f, bold: false);
            ElarionUiKit.FitSingleLine(summary);

            float y = 0.82f;
            AddResourceRow(body, ref y, _result.AetherCrystals, "AETHER CRYSTALS");
            AddResourceRow(body, ref y, _result.Food, "STONE");
            AddResourceRow(body, ref y, _result.Iron, "IRON");
            AddResourceRow(body, ref y, _result.Wood, "WOOD");
            AddMendRows(body, ref y);

            if (_result.WasCapped)
            {
                var capped = ElarionUiKit.Label(body,
                    "Storage filled while you were away. Check in sooner to keep every reward.",
                    Mathf.Max(0.03f, y - 0.12f), y, ElarionUi.Gold,
                    ElarionUi.FontMicro, TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
                ElarionUiKit.FitBlock(capped, 26f, ElarionUi.FontMicro);
            }

            // This report can contain seven data lines; the generic footer zone is
            // re-seated above the shared Close reservation and lands in that data stack.
            // Seat the sole action directly in the shell's bottom thumb band instead.
            var collect = ElarionUiKit.BuildObsidianButton(_modal.chrome.content.transform, "COLLECT",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.37f, 0.045f), new Vector2(0.63f, 0.155f), Dismiss);
            MedievalUiSkin.ApplyButton(collect, primary: true);
            var face = collect != null ? collect.targetGraphic as Image : null;
            if (face != null) face.type = Image.Type.Simple;

            _open = true;
            _panelHandle = PanelManager.Register("Welcome Back", Dismiss, () => _open);
            if (!PanelManager.NotifyOpened(_panelHandle)) Dismiss();
        }

        private static void AddResourceRow(Transform body, ref float y, int amount, string label)
        {
            if (amount <= 0) return;
            const float h = 0.095f;
            var plate = ElarionUiKit.AddImage(body, "Reward_" + label,
                new Vector2(0.08f, y - h), new Vector2(0.92f, y),
                new Color(0.05f, 0.045f, 0.04f, 0.96f), rounded: false);
            var name = ElarionUiKit.Label(plate.transform, label, 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontMicro, TextAlignmentOptions.Left,
                0.05f, 0.70f, bold: false);
            var value = ElarionUiKit.Label(plate.transform, "+" + amount, 0f, 1f,
                ElarionUi.Gold, ElarionUi.FontLabel, TextAlignmentOptions.Right,
                0.70f, 0.95f, bold: true);
            ElarionUiKit.FitSingleLine(name); ElarionUiKit.FitSingleLine(value);
            y -= h + 0.012f;
        }

        private void AddMendRows(Transform body, ref float y)
        {
            var mend = _result != null ? _result.Mend : null;
            if (mend == null || !mend.HasContent) return;
            AddMendLine(body, ref y, EchoMendCopy.AwayMendedLine(mend), ElarionUi.Parchment);
            AddMendLine(body, ref y, EchoMendCopy.AwaySpentLine(mend), ElarionUi.ParchmentDim);
            AddMendLine(body, ref y, EchoMendCopy.AwayStallLine(mend), ElarionUi.Gold);
        }

        private static void AddMendLine(Transform body, ref float y, string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            const float h = 0.09f;
            var label = ElarionUiKit.Label(body, text, y - h, y, color,
                ElarionUi.FontMicro, TextAlignmentOptions.Center, 0.07f, 0.93f, bold: true);
            ElarionUiKit.FitBlock(label, 24f, ElarionUi.FontMicro);
            y -= h + 0.01f;
        }

        private string AwayText()
        {
            double hours = _result.AwaySeconds / 3600.0;
            string span = hours >= 1.0 ? $"{hours:0.#}h" : $"{Mathf.RoundToInt((float)(_result.AwaySeconds / 60.0))}m";
            return _result.WasCapped ? $"YOUR REALM WORKED FOR {span} (STORAGE FULL)" : $"YOUR REALM WORKED FOR {span}";
        }

        private void Dismiss()
        {
            _open = false;
            if (_panelHandle != null) { PanelManager.NotifyClosed(_panelHandle); _panelHandle = null; }
            if (_modal != null && _modal.canvas != null)
            {
                if (Application.isPlaying) Destroy(_modal.canvas); else DestroyImmediate(_modal.canvas);
            }
            _modal = null;
            if (s_active == this) s_active = null;
            if (gameObject != null)
            {
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            }
        }

        private void OnDestroy()
        {
            _open = false;
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            if (s_active == this) s_active = null;
        }
    }
}
