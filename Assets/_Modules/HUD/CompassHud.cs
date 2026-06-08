// =============================================================================
// CompassHud — top-of-screen NSEW ticks + off-screen enemy arrows.
// -----------------------------------------------------------------------------
// PO direction (2026-05-19): there's no minimap, so the player needs another
// way to read direction. Two cues:
//   1. A compass strip across the top: the world's N/E/S/W marks scroll left/
//      right as the hero rotates, so you always know which way you're facing.
//   2. Red arrow pips around the screen edge that point toward any enemy
//      currently off-screen — so an enemy approaching from behind is visible
//      as a pip on the bottom edge, etc.
//
// Built entirely at runtime — same pattern as HelpMenu — so it lives in any
// scene that has a usable PanelSettings without per-scene UXML authoring.
// Wired by CompassHudBootstrap (separate file).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.UI;   // shared Elarion palette — compass matches the uGUI HUD

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class CompassHud : MonoBehaviour
    {
        private const int CompassWidth  = 240;
        private const int CompassHeight = 28;

        /// <summary>How far inside the screen edge an off-screen arrow sits.</summary>
        private const float EdgeInset = 36f;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _compassStrip;
        private Label _compassLabel;
        private VisualElement _arrowLayer;

        /// <summary>The hero transform — set by the bootstrap.</summary>
        public Transform Hero { get; set; }

        /// <summary>The targets we'll point arrows at (enemies). Updated each frame.</summary>
        public List<Transform> Targets { get; } = new List<Transform>();

        private readonly List<VisualElement> _arrowPool = new List<VisualElement>();
        private Camera _camera;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            if (_document.panelSettings == null)
            {
                foreach (var existing in UnityEngine.Object.FindObjectsByType<UIDocument>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (existing == _document || existing.panelSettings == null) continue;
                    _document.panelSettings = existing.panelSettings;
                    break;
                }
            }
            if (_document.panelSettings == null) { enabled = false; return; }
            _document.sortingOrder = 90; // below Help menu (100), above default HUD
            BuildUi();
        }

        private void OnEnable()
        {
            _camera = Camera.main;
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            UpdateCompass();
            UpdateArrows();
        }

        // ── UI ──────────────────────────────────────────────────────────────
        private void BuildUi()
        {
            _root = _document.rootVisualElement;
            _root.Clear();
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0;
            _root.style.top  = 0; _root.style.bottom = 0;

            // Compass strip at top-center, sitting below the timer/wave indicators.
            _compassStrip = new VisualElement();
            _compassStrip.pickingMode = PickingMode.Ignore;
            _compassStrip.style.position = Position.Absolute;
            _compassStrip.style.top = 70;
            _compassStrip.style.width = CompassWidth;
            _compassStrip.style.height = CompassHeight;
            _compassStrip.style.left = Length.Percent(50);
            _compassStrip.style.translate = new StyleTranslate(new Translate(-CompassWidth / 2f, 0));
            // Elarion stone plate with a runic-gold rim — matches the uGUI HUD panels.
            _compassStrip.style.backgroundColor = ElarionUi.PanelStoneDark;
            ElarionUi.SetRadius(_compassStrip, ElarionUi.RadiusMd);
            ElarionUi.SetBorderWidth(_compassStrip, 1.5f);
            ElarionUi.SetBorderColor(_compassStrip, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f));
            _compassStrip.style.alignItems = Align.Center;
            _compassStrip.style.justifyContent = Justify.Center;
            _root.Add(_compassStrip);

            _compassLabel = new Label("N");
            _compassLabel.style.color = ElarionUi.Parchment;
            _compassLabel.style.fontSize = 14;
            _compassLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _compassLabel.style.letterSpacing = 6;
            _compassStrip.Add(_compassLabel);

            // Arrow layer fills the screen — arrows are children positioned in pixels.
            _arrowLayer = new VisualElement();
            _arrowLayer.pickingMode = PickingMode.Ignore;
            _arrowLayer.style.position = Position.Absolute;
            _arrowLayer.style.left = 0; _arrowLayer.style.right = 0;
            _arrowLayer.style.top  = 0; _arrowLayer.style.bottom = 0;
            _root.Add(_arrowLayer);
        }

        // ── Compass strip ───────────────────────────────────────────────────
        private void UpdateCompass()
        {
            if (Hero == null || _compassLabel == null) return;
            float yaw = Hero.eulerAngles.y;
            // Show the cardinal closest to the hero's facing direction.
            string heading;
            if      (yaw < 22.5f  || yaw >= 337.5f) heading = "N";
            else if (yaw < 67.5f)                   heading = "NE";
            else if (yaw < 112.5f)                  heading = "E";
            else if (yaw < 157.5f)                  heading = "SE";
            else if (yaw < 202.5f)                  heading = "S";
            else if (yaw < 247.5f)                  heading = "SW";
            else if (yaw < 292.5f)                  heading = "W";
            else                                    heading = "NW";
#if UNITY_EDITOR
            _compassLabel.text = heading + "   (" + Mathf.RoundToInt(yaw) + "°)";
#else
            _compassLabel.text = heading;
#endif
        }

        // ── Off-screen target arrows ────────────────────────────────────────
        private void UpdateArrows()
        {
            if (_camera == null || _arrowLayer == null) return;

            EnsurePool(Targets.Count);
            float screenW = Screen.width;
            float screenH = Screen.height;
            float cx = screenW * 0.5f;
            float cy = screenH * 0.5f;

            for (int i = 0; i < _arrowPool.Count; i++)
            {
                var arrow = _arrowPool[i];
                if (i >= Targets.Count || Targets[i] == null)
                {
                    arrow.style.display = DisplayStyle.None;
                    continue;
                }
                Transform t = Targets[i];
                Vector3 sp = _camera.WorldToScreenPoint(t.position);
                bool behind = sp.z < 0f;
                bool onScreen = !behind && sp.x >= 0 && sp.x <= screenW && sp.y >= 0 && sp.y <= screenH;
                if (onScreen) { arrow.style.display = DisplayStyle.None; continue; }

                // For points behind the camera, flip to mirror across the centre
                // so the arrow points the correct way.
                if (behind) { sp.x = screenW - sp.x; sp.y = screenH - sp.y; }

                // Direction from screen centre toward the target's projected point.
                float dx = sp.x - cx;
                float dy = sp.y - cy;
                float len = Mathf.Sqrt(dx * dx + dy * dy);
                if (len < 0.001f) { arrow.style.display = DisplayStyle.None; continue; }
                dx /= len; dy /= len;

                // Clamp the arrow to the inset rectangle along the centre→target ray.
                float halfW = cx - EdgeInset;
                float halfH = cy - EdgeInset;
                float tScale = Mathf.Min(halfW / Mathf.Max(0.001f, Mathf.Abs(dx)),
                                         halfH / Mathf.Max(0.001f, Mathf.Abs(dy)));
                float px = cx + dx * tScale;
                // UI Toolkit screen origin is top-left; Unity screen y is bottom-up. Flip.
                float py = screenH - (cy + dy * tScale);

                arrow.style.display = DisplayStyle.Flex;
                arrow.style.left = px - 10;
                arrow.style.top  = py - 10;
                // Point the chevron toward the target.
                float angle = Mathf.Atan2(-dy, dx) * Mathf.Rad2Deg + 90f;
                arrow.style.rotate = new StyleRotate(new Rotate(angle));
            }
        }

        private void EnsurePool(int count)
        {
            while (_arrowPool.Count < count)
            {
                var arrow = new Label("▲");
                arrow.pickingMode = PickingMode.Ignore;
                arrow.style.position = Position.Absolute;
                arrow.style.width = 20; arrow.style.height = 20;
                arrow.style.fontSize = 18;
                arrow.style.unityFontStyleAndWeight = FontStyle.Bold;
                arrow.style.color = ElarionUi.Danger; // threat-red, shared palette
                arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
                arrow.style.display = DisplayStyle.None;
                _arrowLayer.Add(arrow);
                _arrowPool.Add(arrow);
            }
        }
    }
}
