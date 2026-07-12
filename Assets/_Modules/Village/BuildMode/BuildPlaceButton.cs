// =============================================================================
// BuildPlaceButton — owner ask 2026-07-12 (demo web build: "cannot place the
// towers … might need a deploy button for web ui? simplest?").
//
// A code-built uGUI PLACE button shown while Build Mode has an armed ghost or
// an in-progress move. Clicking it confirms placement through an explicit
// controller latch (BuildModeController.RequestUiPlaceConfirm) — bypassing the
// whole pointer-input seam, so it works no matter which input link the browser
// breaks (null Mouse device, joystick-zone suppression, raycast source). uGUI
// button clicks provably work on web (the palette arms entries there).
//
// CODE-BUILT uGUI on its own Screen-Space-Overlay Canvas — NOT UXML (UXML
// UIDocuments come up EMPTY in WebGL builds, PIPELINE_STATE landmine). Same
// pattern as BuildFeedbackToast: legacy runtime font, no scene wiring,
// null-guarded, ASCII strings, text label (never color-only — owner colorblind).
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>On-screen PLACE confirm for Build Mode (web-safe explicit intent).
    /// Created/destroyed by BuildModeController; visibility driven per frame.</summary>
    public sealed class BuildPlaceButton : MonoBehaviour
    {
        private GameObject _root;
        private System.Action _onPlace;

        public static BuildPlaceButton Create(Transform parent, System.Action onPlace)
        {
            var host = new GameObject("BuildPlaceButton");
            if (parent != null) host.transform.SetParent(parent, false);
            var b = host.AddComponent<BuildPlaceButton>();
            b._onPlace = onPlace;
            b.Build();
            return b;
        }

        public void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible) _root.SetActive(visible);
        }

        private void Build()
        {
            _root = new GameObject("PlaceButtonCanvas");
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;   // above the palette tray, below modals
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _root.AddComponent<GraphicRaycaster>();

            // The canonical kit button (STYLE EVERYTHING OBSIDIAN law — never hand-roll
            // uGUI widgets): Gold CTA "PLACE", fraction-anchored bottom-centre-right,
            // clear of the 128px palette tray and the toast slot at centre. Sized big
            // for touch (the web/mobile place fix this button exists for).
            DeNelle.Core.UI.ElarionUiKit.Button(_root.transform, "PLACE",
                DeNelle.Core.UI.ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.58f, 0.15f), new Vector2(0.72f, 0.225f),
                () => _onPlace?.Invoke());

            _root.SetActive(false);   // hidden until a ghost is armed / moving
        }
    }
}
