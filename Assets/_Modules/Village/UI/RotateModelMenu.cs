// =============================================================================
// RotateModelMenu — WO-335 PLAYER tower-placement rotate panel (yaw-only).
// -----------------------------------------------------------------------------
// The owner-approved, REALLY-SIMPLE placement orient panel from the mockup
// docs/design/rotate-model-player-panel-concept.jpg:
//
//   [ Rotate Model ]            <- title
//   ┌──────────────┐
//   │  3D PREVIEW  │            <- live RT preview of the model
//   └──────────────┘
//   [ Turn Left ] [ Turn Right ]<- 90° per tap, yaw only
//   Rotation: 90°                <- live readout
//   [ Reset to 0° ]
//   ItemName — 120 ◈            <- name + cost
//   [ Confirm Placement ] [ Cancel ]
//
// NO 3-axis sliders, NO snap dropdown, NO runic border — clean dark-navy panel
// with gold buttons. This is the PLAYER UI now; the old UIToolkit 3-axis
// TowerPlacementRotateMenu becomes the editor/dev OFFSET tool (WO-335 split).
//
// CRITICAL build details (both learned the hard way on this project):
//   • Code-built uGUI (Canvas + Image + RawImage + Text + Image "buttons") —
//     NOT UIToolkit. UXML-sourced HUDs render EMPTY in player builds.
//   • NO EventSystem dependency. uGUI Button.onClick needs an EventSystem the
//     builds lack, so clicks are routed by the MANUAL screen-rect hit-test
//     pattern from GameOverScreen.cs (poll the pointer in Update, test against
//     each button's RectTransform). Proven to render + respond in player builds
//     incl. WebGL.
//   • The 3D preview reuses TowerPreviewCamera (enabled=false + manual Render()
//     each frame); its RenderTexture is bound to a RawImage.texture.
//
// Lives in DeNelle.Village (no new asmdef — Village already references the UI +
// URP runtime assemblies). API is generic + prefab-driven so any catalog item
// (not just towers) can be oriented through it.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Player-facing yaw-only placement rotate panel. Open via
    /// <see cref="Open"/>; the panel tears itself down on Confirm / Cancel.
    /// Clicks are hit-tested manually (no EventSystem) and the 3D preview is a
    /// manually-rendered <see cref="TowerPreviewCamera"/> bound to a RawImage.
    /// </summary>
    public sealed class RotateModelMenu : MonoBehaviour
    {
        // Palette from the mockup (dark navy panel + gold buttons).
        private static readonly Color PanelNavy  = new Color(0.055f, 0.086f, 0.165f, 0.98f); // #0e162a
        private static readonly Color ViewportBg = new Color(0.02f, 0.047f, 0.094f, 1f);     // #050c18
        private static readonly Color Gold       = new Color(0.83f, 0.66f, 0.30f, 1f);       // gold button
        private static readonly Color GoldDim    = new Color(0.55f, 0.44f, 0.22f, 1f);       // secondary/cancel
        private static readonly Color InkOnGold   = new Color(0.06f, 0.05f, 0.02f, 1f);
        private static readonly Color CreamText   = new Color(1f, 0.92f, 0.74f, 1f);

        private GameObject _root;
        private RawImage   _previewImage;
        private Text       _rotationReadout;

        // Manual hit-test targets (no EventSystem in builds).
        private RectTransform _turnLeftBtn;
        private RectTransform _turnRightBtn;
        private RectTransform _resetBtn;
        private RectTransform _confirmBtn;
        private RectTransform _cancelBtn;

        private TowerPreviewCamera _preview;

        private int          _yawSteps;          // 0..3 (×90°)
        private Action<int>  _onConfirm;
        private Action       _onCancel;
        private bool         _open;

        /// <summary>
        /// Open the player rotate panel for a prefab model. <paramref name="initialYawSteps"/>
        /// seeds the readout/preview (0..3 → 0/90/180/270°). On Confirm,
        /// <paramref name="onConfirm"/> fires with the chosen yawSteps; on Cancel,
        /// <paramref name="onCancel"/> fires. The panel closes itself either way.
        /// </summary>
        public void Open(
            GameObject previewPrefab,
            string displayName,
            double cost,
            int initialYawSteps,
            Action<int> onConfirm,
            Action onCancel)
        {
            Close(); // never stack two panels

            _yawSteps  = ((initialYawSteps % 4) + 4) % 4;
            _onConfirm = onConfirm;
            _onCancel  = onCancel;

            // --- 3D preview rig (manual-render RT) --------------------------------
            _preview = new TowerPreviewCamera();
            bool previewValid = _preview.Begin(previewPrefab);
            Debug.Log($"[Orient] preview rig: valid={previewValid} prefab={(previewPrefab != null ? previewPrefab.name : "<none>")}");

            BuildPanel(displayName, cost, previewValid);
            ApplyRotation(); // seed preview rotation + readout
            _open = true;
        }

        private void Update()
        {
            if (!_open) return;

            // Keep the off-screen preview camera drawing every frame (URP will not
            // auto-render an enabled=false off-screen Base camera).
            ApplyRotation();

            if (!TryGetTap(out Vector2 tap)) return;

            if (HitTest(_turnLeftBtn, tap))
            {
                _yawSteps = (_yawSteps + 3) & 3; // -90°
            }
            else if (HitTest(_turnRightBtn, tap))
            {
                _yawSteps = (_yawSteps + 1) & 3; // +90°
            }
            else if (HitTest(_resetBtn, tap))
            {
                _yawSteps = 0;
            }
            else if (HitTest(_confirmBtn, tap))
            {
                Confirm();
            }
            else if (HitTest(_cancelBtn, tap))
            {
                Cancel();
            }
        }

        // ── Actions ──────────────────────────────────────────────────────────────

        private void Confirm()
        {
            Debug.Log($"[Orient] confirm: yawSteps={_yawSteps} yaw={_yawSteps * 90}°");
            var cb = _onConfirm;
            int steps = _yawSteps;
            Close();
            // TODO (economy): on Confirm the orientation is LOCKED; re-rotate/redesign
            // later requires a special "redesign token" item (earned or bought) —
            // CoC-style sink + monetization hook. Gate this confirm path on a token
            // spend once the token economy exists (consume a token here, or fall back
            // to "you've already placed this — buy a Redesign Token to rotate it").
            cb?.Invoke(steps);
        }

        private void Cancel()
        {
            Debug.Log("[Orient] cancel (player rotate panel)");
            var cb = _onCancel;
            Close();
            cb?.Invoke();
        }

        /// <summary>Apply the current yaw to the preview model + refresh the readout text.</summary>
        private void ApplyRotation()
        {
            int deg = _yawSteps * 90;
            _preview?.SetRotation(Quaternion.Euler(0f, deg, 0f));
            if (_rotationReadout != null) _rotationReadout.text = $"Rotation: {deg}°";
        }

        /// <summary>Tear down the panel + preview rig. Safe to call repeatedly.</summary>
        public void Close()
        {
            _open = false;
            if (_preview != null) { _preview.Dispose(); _preview = null; }
            if (_root != null) { Destroy(_root); _root = null; }
            _previewImage = null;
            _rotationReadout = null;
            _turnLeftBtn = _turnRightBtn = _resetBtn = _confirmBtn = _cancelBtn = null;
            _onConfirm = null;
            _onCancel  = null;
        }

        private void OnDestroy() => Close();

        // ── Hit-test (no EventSystem — GameOverScreen pattern) ────────────────────

        /// <summary>First touch-began / mouse-down screen position this frame.</summary>
        private static bool TryGetTap(out Vector2 pos)
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == UnityEngine.TouchPhase.Began) { pos = t.position; return true; }
            }
            if (Input.GetMouseButtonDown(0)) { pos = (Vector2)Input.mousePosition; return true; }
            pos = default;
            return false;
        }

        private static bool HitTest(RectTransform rt, Vector2 screenPoint)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null);

        // ── Code-built uGUI panel ─────────────────────────────────────────────────

        private void BuildPanel(string displayName, double cost, bool previewValid)
        {
            _root = new GameObject("RotateModelMenu_Canvas");
            DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32750; // above gameplay HUD, below GameOver overlay
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            // Dim full-screen scrim (also catches stray taps outside the panel).
            var scrim = NewImage(_root.transform, "Scrim", new Color(0f, 0f, 0f, 0.45f));
            Stretch(scrim.rectTransform, Vector2.zero, Vector2.one);

            // Centered navy panel.
            var panel = NewImage(_root.transform, "Panel", PanelNavy);
            var pr = panel.rectTransform;
            pr.anchorMin = new Vector2(0.32f, 0.16f);
            pr.anchorMax = new Vector2(0.68f, 0.92f);
            pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

            // Title.
            var title = NewText(panel.transform, "Title", "Rotate Model", 34, FontStyle.Bold, CreamText);
            Anchor(title.rectTransform, new Vector2(0.06f, 0.90f), new Vector2(0.94f, 0.99f));

            // 3D preview viewport (RawImage bound to the preview RT) — gold-ish frame
            // via a slightly larger backing Image behind it.
            var frame = NewImage(panel.transform, "PreviewFrame", GoldDim);
            Anchor(frame.rectTransform, new Vector2(0.10f, 0.55f), new Vector2(0.90f, 0.89f));

            var viewGo = new GameObject("PreviewViewport");
            viewGo.transform.SetParent(panel.transform, false);
            _previewImage = viewGo.AddComponent<RawImage>();
            _previewImage.color = ViewportBg;
            if (previewValid && _preview != null && _preview.Texture != null)
            {
                _previewImage.texture = _preview.Texture;
                _previewImage.color   = Color.white; // show the RT unmodulated
            }
            Anchor(_previewImage.rectTransform, new Vector2(0.105f, 0.555f), new Vector2(0.895f, 0.885f));

            if (!previewValid)
            {
                // No Level-1 visual prefab — label the viewport instead of a black box.
                var ph = NewText(viewGo.transform, "NoPreview", "(no preview)", 20, FontStyle.Italic, CreamText);
                Stretch(ph.rectTransform, Vector2.zero, Vector2.one);
            }

            // Turn Left / Turn Right buttons.
            _turnLeftBtn  = NewButton(panel.transform, "TurnLeft",  "Turn Left",  Gold, InkOnGold,
                                      new Vector2(0.08f, 0.44f), new Vector2(0.49f, 0.53f));
            _turnRightBtn = NewButton(panel.transform, "TurnRight", "Turn Right", Gold, InkOnGold,
                                      new Vector2(0.51f, 0.44f), new Vector2(0.92f, 0.53f));

            // Rotation readout.
            _rotationReadout = NewText(panel.transform, "Readout", "Rotation: 0°", 26, FontStyle.Bold, CreamText);
            Anchor(_rotationReadout.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.43f));

            // Reset button.
            _resetBtn = NewButton(panel.transform, "Reset", "Reset to 0°", GoldDim, CreamText,
                                  new Vector2(0.28f, 0.27f), new Vector2(0.72f, 0.35f));

            // Item name + cost line.
            string costLabel = cost > 0 ? $"{displayName}  —  {cost:0} ◈" : displayName;
            var nameLine = NewText(panel.transform, "ItemNameCost", costLabel, 22, FontStyle.Normal, CreamText);
            Anchor(nameLine.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.25f));

            // Confirm / Cancel.
            _confirmBtn = NewButton(panel.transform, "Confirm", "Confirm Placement", Gold, InkOnGold,
                                    new Vector2(0.06f, 0.05f), new Vector2(0.62f, 0.15f));
            _cancelBtn  = NewButton(panel.transform, "Cancel", "Cancel", GoldDim, CreamText,
                                    new Vector2(0.64f, 0.05f), new Vector2(0.94f, 0.15f));
        }

        // ── uGUI builder helpers ──────────────────────────────────────────────────

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text NewText(Transform parent, string name, string content,
                                    int size, FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.fontSize = size;
            t.fontStyle = style;
            t.text = content;
            t.supportRichText = true;
            return t;
        }

        /// <summary>Build a code-built "button" (Image + centred Text). Returns its
        /// RectTransform for manual hit-testing — there is no Button/onClick.</summary>
        private RectTransform NewButton(Transform parent, string name, string label,
                                        Color bg, Color textColor,
                                        Vector2 anchorMin, Vector2 anchorMax)
        {
            var img = NewImage(parent, "Btn_" + name, bg);
            Anchor(img.rectTransform, anchorMin, anchorMax);

            var lbl = NewText(img.transform, "Label", label, 22, FontStyle.Bold, textColor);
            Stretch(lbl.rectTransform, Vector2.zero, Vector2.one);
            return img.rectTransform;
        }

        private static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
            => Anchor(rt, anchorMin, anchorMax);
    }
}
