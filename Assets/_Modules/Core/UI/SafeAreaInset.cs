// =============================================================================
// SafeAreaInset — THE shared Screen.safeArea helper (WO-868).
// -----------------------------------------------------------------------------
// WHY THIS EXISTS (proven from the device capture, not inferred):
// docs/ui-review/2026-08-04-seeker/01-title-screen.png is a 1:1 Seeker screencap
// at 2670x1200. Measured off that PNG, the "Connect Wallet" corner button's
// visible box is x[2354..2653] y[0..101]:
//   * width  = 300 px  == PiSignInController's sizeDelta.x (so the canvas is
//     ConstantPixelSize @ scaleFactor 1 — canvas units ARE device pixels),
//   * right margin = 2669-2653 = 16 px == the raw `anchoredPosition = (-16,-16)`,
//   * the box is 112 px tall (the kit touch floor: ElarionUiKit.ClampMinTouch
//     grows a sub-floor button SYMMETRICALLY about its centre, so the 60-px
//     holder became 112 and gained 26 px on EACH side) and its true top edge is
//     at 16 - 26 = -10 — TEN PIXELS OFF THE TOP OF THE SCREEN. That is the clip
//     the owner saw, and y=0 in the capture is where the screen cuts it.
// A raw 16-device-px inset is ~6 dp on the Seeker (~1.5 mm) — it reads as flush
// and sits INSIDE the rounded-corner / camera-cutout / gesture band. The same
// finding is already written down at EchoUnlockFeedback.cs:342-350, whose TODO
// asks for exactly this file: "replace with the shared Screen.safeArea helper
// once it exists". It did not exist. Now it does.
//
// CONTRACT
//   * Insets come from Unity's Screen.safeArea, so a device cutout/rounded corner
//     is respected wherever it is — never a hard-coded per-device margin.
//   * PLUS a FIXED-PIXEL breathing margin (EdgeMarginPx) — never a fraction of
//     parent (the WO-841/WO-852 fraction-band defect class). Devices that report
//     no cutout (safeArea == full screen) still get a real margin.
//   * The math is PURE + static (TopRightScreenRect / TopRightAnchoredPosition)
//     so it is assertable headlessly at any resolution without a live Screen.
//   * Presentation-only: this touches a RectTransform and nothing else. No game
//     state, no service lookup, no reflection.
//
// Screen space here is Unity's: origin BOTTOM-LEFT, y up. Screen.safeArea uses
// the same convention, so `screenH - (safeArea.y + safeArea.height)` is the TOP
// inset. Values are SCREEN pixels; the applier converts to canvas units with the
// Canvas' scaleFactor so it is correct under any CanvasScaler mode.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Shared <see cref="Screen.safeArea"/> corner inset for screen-anchored HUD
    /// chrome. Pure static math + a one-line applier that re-fits on rotation /
    /// resolution change.
    /// </summary>
    public static class SafeAreaInset
    {
        /// <summary>
        /// FIXED-PIXEL breathing margin applied ON TOP of the reported safe area.
        /// 44 device px is ~16 dp at the Seeker's ~2.75 density — the Material
        /// screen-edge margin, and ~2.75x the 16-px raw inset that measured as
        /// flush-to-edge on the 2026-08-04 capture. Fixed pixels by law
        /// (docs/ui-review/2026-08-04-seeker/README.md §0): never a fraction of
        /// parent, which is what culls/clips the moment the aspect changes.
        /// </summary>
        public const float EdgeMarginPx = 44f;

        // ── Pure math (headlessly assertable — no live Screen required) ────────

        /// <summary>Distance in screen px from the RIGHT screen edge to the safe area. Never negative.</summary>
        public static float RightInset(Rect safeArea, int screenWidth)
            => Mathf.Max(0f, screenWidth - (safeArea.x + safeArea.width));

        /// <summary>Distance in screen px from the TOP screen edge to the safe area. Never negative.</summary>
        public static float TopInset(Rect safeArea, int screenHeight)
            => Mathf.Max(0f, screenHeight - (safeArea.y + safeArea.height));

        /// <summary>Distance in screen px from the LEFT screen edge to the safe area. Never negative.</summary>
        public static float LeftInset(Rect safeArea) => Mathf.Max(0f, safeArea.x);

        /// <summary>Distance in screen px from the BOTTOM screen edge to the safe area. Never negative.</summary>
        public static float BottomInset(Rect safeArea) => Mathf.Max(0f, safeArea.y);

        /// <summary>
        /// anchoredPosition (in SCREEN px) for a rect anchored AND pivoted at the
        /// top-right corner (anchorMin = anchorMax = pivot = (1,1)), inset inside
        /// the safe area by <paramref name="marginPx"/>.
        /// </summary>
        /// <param name="marginPxY">Optional SEPARATE vertical margin. Defaults to
        /// <paramref name="marginPx"/>, so every existing caller and every existing
        /// assertion is byte-identical. It exists because a corner widget can need more
        /// clearance on ONE axis than the other — WO-1083 defect #6: the wallet chip's
        /// vertical position already lands inside the hero-select frame's header band, but
        /// horizontally it runs onto the frame's right-hand border art, so only x moves.</param>
        public static Vector2 TopRightAnchoredPosition(Rect safeArea, int screenWidth, int screenHeight,
                                                       float marginPx = EdgeMarginPx,
                                                       float marginPxY = -1f)
        {
            float m = Mathf.Max(0f, marginPx);
            float my = marginPxY < 0f ? m : Mathf.Max(0f, marginPxY);
            return new Vector2(-(RightInset(safeArea, screenWidth) + m),
                               -(TopInset(safeArea, screenHeight) + my));
        }

        /// <summary>
        /// The resulting SCREEN-space rect (origin bottom-left) of a
        /// <paramref name="size"/>-sized box placed by
        /// <see cref="TopRightAnchoredPosition"/>. This is the value a regression
        /// asserts against <c>Screen.safeArea</c>.
        /// </summary>
        public static Rect TopRightScreenRect(Rect safeArea, int screenWidth, int screenHeight,
                                              Vector2 size, float marginPx = EdgeMarginPx,
                                              float marginPxY = -1f)
        {
            float m = Mathf.Max(0f, marginPx);
            float my = marginPxY < 0f ? m : Mathf.Max(0f, marginPxY);
            float xMax = screenWidth - RightInset(safeArea, screenWidth) - m;
            float yMax = screenHeight - TopInset(safeArea, screenHeight) - my;
            return new Rect(xMax - size.x, yMax - size.y, size.x, size.y);
        }

        // ── Applier (live Screen) ─────────────────────────────────────────────

        /// <summary>
        /// Pins <paramref name="rt"/> to the TOP-RIGHT corner inside the live
        /// <see cref="Screen.safeArea"/>, and keeps it there across rotation /
        /// resolution / safe-area changes. Anchors, pivot and position are all set
        /// here — the caller only supplies the (fixed-pixel) size.
        /// </summary>
        public static void ApplyTopRight(RectTransform rt, float marginPx = EdgeMarginPx,
                                         float marginPxY = -1f)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            var watcher = rt.GetComponent<SafeAreaCornerWatcher>();
            if (watcher == null) watcher = rt.gameObject.AddComponent<SafeAreaCornerWatcher>();
            watcher.Bind(marginPx, marginPxY);
        }

        /// <summary>Reads the live Screen and writes the top-right anchoredPosition once.</summary>
        private static void FitTopRightNow(RectTransform rt, float marginPx, float marginPxY)
        {
            if (rt == null) return;
            Vector2 screenPx = TopRightAnchoredPosition(Screen.safeArea, Screen.width, Screen.height,
                                                        marginPx, marginPxY);

            // anchoredPosition is in CANVAS units, not screen px. Under the default
            // ConstantPixelSize scaler these are identical (scaleFactor 1 — which is
            // what the 2026-08-04 capture measured), but a ScaleWithScreenSize canvas
            // shrinks them, so divide by the live scaleFactor and stay correct either way.
            var canvas = rt.GetComponentInParent<Canvas>();
            float scale = (canvas != null && canvas.scaleFactor > 0.0001f) ? canvas.scaleFactor : 1f;
            rt.anchoredPosition = screenPx / scale;
        }

        /// <summary>
        /// Re-fits the corner when the safe area or the screen changes (rotation,
        /// window resize, a device that reports its cutout late). Cheap: four
        /// float compares per frame, and it writes only on an actual change.
        /// </summary>
        private sealed class SafeAreaCornerWatcher : MonoBehaviour
        {
            private RectTransform _rt;
            private float _margin = EdgeMarginPx;
            private float _marginY = -1f;
            private Rect _lastSafe;
            private int _lastW = -1, _lastH = -1;

            private void Awake() { _rt = transform as RectTransform; }

            /// <summary>(Re)bind the margin and force an immediate fit.</summary>
            public void Bind(float marginPx, float marginPxY = -1f)
            {
                _margin = marginPx;
                _marginY = marginPxY;
                if (_rt == null) _rt = transform as RectTransform;
                Refit();
            }

            private void OnEnable() => Refit();

            private void Update()
            {
                if (Screen.width == _lastW && Screen.height == _lastH && Screen.safeArea == _lastSafe) return;
                Refit();
            }

            private void Refit()
            {
                _lastW = Screen.width;
                _lastH = Screen.height;
                _lastSafe = Screen.safeArea;
                FitTopRightNow(_rt, _margin, _marginY);
            }
        }
    }
}
