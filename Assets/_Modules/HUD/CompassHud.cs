// =============================================================================
// CompassHud — top-of-screen NSEW heading + off-screen enemy arrows.
// -----------------------------------------------------------------------------
// PO direction (2026-05-19): there's no minimap, so the player needs another
// way to read direction. Two cues:
//   1. A compass strip across the top: shows the cardinal the hero is facing,
//      so you always know which way you're looking.
//   2. Red arrow pips around the screen edge that point toward any enemy
//      currently off-screen — so an enemy approaching from behind is visible
//      as a pip on the bottom edge, etc.
//
// WO-322 ROOT CAUSE (re-fix 2026-06-12): the compass was a UI Toolkit
// (UIDocument / UXML-backed) overlay. Two fatal problems with that:
//   • UIDocument/UXML HUDs DO NOT RENDER in player builds (PIPELINE_STATE §8,
//     CLAUDE.md memory "uxml-uidocuments-dont-render-in-builds"). So even when
//     it built, it was invisible in the actual game.
//   • The main HUD migrated to code-built uGUI (VillageHudController) which has
//     NO PanelSettings, so the old bootstrap could never even find a
//     PanelSettings to borrow → the compass never spawned at all.
// The previous "fix" only made the bootstrap retry borrowing a PanelSettings —
// a symptom patch that left both root causes in place.
//
// REAL FIX: this is now a CODE-BUILT uGUI overlay — its own ScreenSpaceOverlay
// Canvas + CanvasScaler, TMPro labels, and uGUI Image arrows. No UIDocument, no
// PanelSettings, no UXML. Renders in builds, in any scene, with zero per-scene
// wiring. Mirrors VillageHudController's canvas + AddText conventions so it
// reads as part of the same HUD set. HUD → Core only (palette via ElarionUi).
// Wired by CompassHudBootstrap (separate file).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.UI;   // shared Elarion palette — compass matches the uGUI HUD

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class CompassHud : MonoBehaviour
    {
        // Compass strip footprint (in CanvasScaler reference pixels).
        private const float CompassWidth  = 240f;
        private const float CompassHeight = 30f;
        private const float CompassTop    = 70f;   // below the wave/timer cluster

        /// <summary>How far inside the screen edge an off-screen arrow sits (px).</summary>
        private const float EdgeInset = 36f;

        // Reference resolution shared with VillageHudController so the compass
        // scales identically across mobile portrait + web landscape.
        private static readonly Vector2 RefResolution = new Vector2(1080, 1920);

        /// <summary>The hero transform — set by the bootstrap.</summary>
        public Transform Hero { get; set; }

        /// <summary>The targets we'll point arrows at (enemies). Updated each frame.</summary>
        public List<Transform> Targets { get; } = new List<Transform>();

        // ── Runtime UI refs (all code-built uGUI) ─────────────────────────────
        private Canvas _canvas;
        private RectTransform _canvasRect;   // the scaled overlay rect (reference space)
        private TextMeshProUGUI _compassLabel;
        private RectTransform _arrowLayer;
        private readonly List<RectTransform> _arrowPool = new List<RectTransform>();
        private readonly List<RectTransform> _arrowGlyph = new List<RectTransform>();
        private Camera _camera;
        private bool _built;

        private void Start()
        {
            BuildUi();
            _camera = Camera.main;
        }

        private void OnEnable()
        {
            // Recover if we were disabled before Start ran (defensive).
            if (!_built) return;
            if (_canvas != null) _canvas.enabled = true;
        }

        private void LateUpdate()
        {
            if (!_built) return;
            if (_camera == null) _camera = Camera.main;
            UpdateCompass();
            UpdateArrows();
        }

        // ── UI build (code-only uGUI overlay) ─────────────────────────────────
        private void BuildUi()
        {
            if (_built) return;
            _built = true;

            var go = new GameObject("CompassCanvas");
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Sit just below the Help menu / modal panels but above the base HUD
            // chrome so the heading + arrows are always legible.
            _canvas.sortingOrder = 96;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Pure overlay — never intercepts input (no GraphicRaycaster).
            _canvasRect = _canvas.GetComponent<RectTransform>();

            // ── Compass strip: top-centre dark-glass chip with a thin gold rim ──
            var strip = NewRect("CompassStrip", _canvasRect);
            strip.anchorMin = new Vector2(0.5f, 1f);
            strip.anchorMax = new Vector2(0.5f, 1f);
            strip.pivot     = new Vector2(0.5f, 1f);
            strip.sizeDelta = new Vector2(CompassWidth, CompassHeight);
            strip.anchoredPosition = new Vector2(0f, -CompassTop);

            // Dark-glass chip — the shared kit's primary panel glass tint, so the
            // compass reads as part of the same TOWN-HUD presentation language.
            var stripBg = strip.gameObject.AddComponent<Image>();
            stripBg.color = ElarionUiKit.Glass;
            ElarionUiKit.ApplyRounded(stripBg);
            stripBg.raycastTarget = false;

            // Thin faint-gold rim drawn as a slightly larger plate behind the chip —
            // the kit's soft gold accent (matches every framed panel's rim).
            var rim = NewRect("CompassRim", strip);
            rim.anchorMin = Vector2.zero; rim.anchorMax = Vector2.one;
            rim.offsetMin = new Vector2(-1f, -1f);
            rim.offsetMax = new Vector2(1f, 1f);
            rim.SetAsFirstSibling();
            var rimImg = rim.gameObject.AddComponent<Image>();
            rimImg.color = ElarionUiKit.AccentSoft;
            ElarionUiKit.ApplyRounded(rimImg);
            rimImg.raycastTarget = false;

            _compassLabel = AddText(strip, "N", 18f, ElarionUi.Parchment, TextAlignmentOptions.Center);
            _compassLabel.fontStyle = FontStyles.Bold;
            _compassLabel.characterSpacing = 12f;

            // ── Arrow layer fills the screen; arrows positioned in reference px ──
            _arrowLayer = NewRect("ArrowLayer", _canvasRect);
            _arrowLayer.anchorMin = Vector2.zero;
            _arrowLayer.anchorMax = Vector2.one;
            _arrowLayer.offsetMin = Vector2.zero;
            _arrowLayer.offsetMax = Vector2.zero;
        }

        // ── Compass strip ─────────────────────────────────────────────────────
        private void UpdateCompass()
        {
            if (Hero == null || _compassLabel == null) return;
            float yaw = Hero.eulerAngles.y;
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

        // ── Off-screen target arrows ──────────────────────────────────────────
        // Math is identical to the original; only the rendering is now uGUI. We
        // convert real screen pixels → CanvasScaler reference pixels via the
        // canvas rect's scaleFactor so positions are correct at any resolution.
        private void UpdateArrows()
        {
            if (_camera == null || _arrowLayer == null) return;

            EnsurePool(Targets.Count);

            float screenW = Screen.width;
            float screenH = Screen.height;
            float cx = screenW * 0.5f;
            float cy = screenH * 0.5f;
            float scale = (_canvas != null && _canvas.scaleFactor > 0.0001f)
                ? _canvas.scaleFactor : 1f;

            for (int i = 0; i < _arrowPool.Count; i++)
            {
                var arrow = _arrowPool[i];
                if (i >= Targets.Count || Targets[i] == null)
                {
                    if (arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
                    continue;
                }

                Transform t = Targets[i];
                Vector3 sp = _camera.WorldToScreenPoint(t.position);
                bool behind = sp.z < 0f;
                bool onScreen = !behind && sp.x >= 0 && sp.x <= screenW && sp.y >= 0 && sp.y <= screenH;
                if (onScreen) { if (arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false); continue; }

                // Mirror points behind the camera across the centre so the arrow
                // points the correct way.
                if (behind) { sp.x = screenW - sp.x; sp.y = screenH - sp.y; }

                float dx = sp.x - cx;
                float dy = sp.y - cy;
                float len = Mathf.Sqrt(dx * dx + dy * dy);
                if (len < 0.001f) { if (arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false); continue; }
                dx /= len; dy /= len;

                // Clamp the arrow to the inset rectangle along the centre→target ray.
                float halfW = cx - EdgeInset;
                float halfH = cy - EdgeInset;
                float tScale = Mathf.Min(halfW / Mathf.Max(0.001f, Mathf.Abs(dx)),
                                         halfH / Mathf.Max(0.001f, Mathf.Abs(dy)));
                // Screen-space position (Unity origin bottom-left).
                float px = cx + dx * tScale;
                float py = cy + dy * tScale;

                // Convert real pixels → reference (scaled) pixels for the overlay
                // RectTransform, which lives in CanvasScaler reference space.
                float rx = px / scale;
                float ry = py / scale;

                if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);
                // Anchored to the layer's bottom-left → matches Unity screen origin.
                arrow.anchoredPosition = new Vector2(rx, ry);

                // Point the chevron toward the target.
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - 90f;
                _arrowGlyph[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void EnsurePool(int count)
        {
            while (_arrowPool.Count < count)
            {
                // Outer rect = positioned pip; inner glyph rotates to point.
                var pip = NewRect("Arrow", _arrowLayer);
                pip.anchorMin = Vector2.zero; // origin bottom-left (matches screen origin)
                pip.anchorMax = Vector2.zero;
                pip.pivot     = new Vector2(0.5f, 0.5f);
                pip.sizeDelta = new Vector2(28f, 28f);

                var glyph = AddText(pip, "▲", 26f, ElarionUi.Danger, TextAlignmentOptions.Center);
                glyph.fontStyle = FontStyles.Bold;
                var glyphRect = glyph.rectTransform;
                glyphRect.anchorMin = Vector2.zero; glyphRect.anchorMax = Vector2.one;
                glyphRect.offsetMin = Vector2.zero; glyphRect.offsetMax = Vector2.zero;

                pip.gameObject.SetActive(false);
                _arrowPool.Add(pip);
                _arrowGlyph.Add(glyphRect);
            }
        }

        // ── uGUI helpers (mirror VillageHudController conventions) ─────────────
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            return rt;
        }

        private static TextMeshProUGUI AddText(Transform parent, string text, float size,
            Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Txt");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }
    }
}
