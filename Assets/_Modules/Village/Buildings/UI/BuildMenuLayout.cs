// =============================================================================
// BuildMenuLayout - the build menu's FIXED-PIXEL band ladder (WO-878).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS FILE EXISTS. Every band on BuildMenu used to be a FRACTION OF THE BODY
// ZONE, and the body zone is far smaller than it looks: with the modal anchored at
// 0.92 of a landscape canvas, ElarionUiKit's close-band reservation raises the
// FrameCore body floor to (0.05 + CanonCtaHeight/panelH) + 0.16, so the body
// resolves to
//
//     bodyPx = 0.625 * panelHeightPx - CanonCtaHeight
//
// i.e. ~430 REFERENCE px at 2340x1080 and ~423 px at the Seeker's real 2670x1200 --
// NOT the ~780 px the panel appears to offer. Against that, the shipped fractions
// resolved to:
//
//     root verb row  0.115 x 357 =  41 px      back button  0.14 x 357 =  50 px
//     upgrade CTA    0.15  x 357 =  54 px      info row     0.095 x 357 =  34 px
//
// every one of them UNDER ElarionUiKit.MinTouchPx (112). ClampMinTouch then grows a
// sub-floor button SYMMETRICALLY ABOUT ITS CENTRE, so each of those rects gained
// 30-36 px on EACH side after layout and ate its neighbours: the five root verbs
// (48.9 px stride, 112 px grown height) overlapped by ~63 px each and sliced one
// another's labels, "< Back" grew straight through the "UPGRADE TOWER" title, and
// the Upgrade CTA grew up into the cost/preview text. That is the WO-841/852/865
// failure class verbatim.
//
// THE RULE THIS FILE ENFORCES: a band is a FIXED REFERENCE-PIXEL height, never a
// fraction of a parent, and every band that holds a BUTTON is >= the kit touch
// floor so ClampMinTouch is provably a no-op and can never grow into a neighbour.
// offsetMin/offsetMax on a CanvasScaler'd canvas ARE reference px -- the same unit
// MinTouchPx is expressed in -- so these rungs hold at every screen size with no
// scaler math (the LeaderboardPanel / SettingsController px-ladder precedent).
//
// Pure constants: no Unity UI types, no state, no logic. The View lays out from
// them; BuildMenuLayoutRegression asserts the ladder fits the measured body at both
// capture aspects, which is the assertion the shipped fractions would have failed.
// =============================================================================

using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// The build menu's authored band ladder, in REFERENCE PIXELS on the kit's
    /// 1080x1920 canvas. Named floors only - the View never writes a raw number and
    /// never sizes a band as a fraction of its parent.
    /// </summary>
    public static class BuildMenuLayout
    {
        /// <summary>The kit touch floor. Every band that carries a button is authored AT this
        /// height, so <c>ElarionUiKit.ClampMinTouch</c> has nothing to grow (it is a pure floor
        /// and never shrinks), and therefore cannot inflate a control into a neighbouring band.</summary>
        public const float TouchFloorPx = ElarionUiKit.MinTouchPx;   // 112

        /// <summary>Top band of every sub-screen: the "&lt; Back" button + the screen title,
        /// side by side (horizontally disjoint), so the title can never clip the button.</summary>
        public const float NavBandPx = TouchFloorPx;

        /// <summary>Bottom band of every sub-screen: the cost/preview lines on the left and the
        /// primary CTA on the right, side by side. Disjoint from the content band above it by a
        /// fixed gap, which is what stops the preview text from landing on the button.</summary>
        public const float ActionBandPx = TouchFloorPx;

        /// <summary>One selectable row inside a scroll well (tower radio row / placed-tower row).
        /// The row IS the tap target, so it sits at the floor. (Was 96 px, which ClampMinTouch grew
        /// by 8 px on each side - exactly consuming the 8 px inter-row spacing.)</summary>
        public const float RowPx = TouchFloorPx;

        /// <summary>One verb cell on the root chooser grid.</summary>
        public const float RootCellPx = TouchFloorPx;

        /// <summary>Gap between two stacked bands. Any positive value keeps them disjoint; 12 px
        /// reads as a deliberate seam at phone density.</summary>
        public const float BandGapPx = 12f;

        /// <summary>Gap between two rows inside a scroll well.</summary>
        public const float RowGapPx = 8f;

        /// <summary>Height of ONE of the two info lines inside the action band. Both halves are a
        /// whole TMP line box at the fonts they render (FontLabel 40 -&gt; ~50, FontMicro 32 -&gt; ~40),
        /// and both are fit-guarded, so neither can spill into the other.</summary>
        public const float InfoLinePx = ActionBandPx * 0.5f;   // 56

        // ── Root chooser grid ────────────────────────────────────────────────
        /// <summary>Verb columns on the root chooser. Five verbs at the 112 px touch floor need
        /// 592 px stacked in a body that is ~423-430 px tall; two columns need 360 px and fit.</summary>
        public const int RootColumns = 2;
        /// <summary>Build Tower / Upgrade Tower / Repair Wall / Manage Towers / Build Mode.</summary>
        public const int RootVerbCount = 5;
        /// <summary>Horizontal pad on each side of a grid cell, as a fraction of the body width
        /// (width is never the constraint on this screen - the body is ~1450 px wide).</summary>
        public const float RootCellPadFrac = 0.012f;

        // ── Horizontal splits (width is never the constraint; these stay fractional) ──
        /// <summary>"&lt; Back" occupies the left of the nav band.</summary>
        public const float BackWidthFrac = 0.24f;
        /// <summary>The screen title starts clear of the Back button.</summary>
        public const float TitleLeftFrac = 0.28f;
        /// <summary>The cost/preview lines occupy the left of the action band.</summary>
        public const float InfoWidthFrac = 0.58f;
        /// <summary>The primary CTA occupies the right of the action band, clear of the text.</summary>
        public const float CtaLeftFrac = 0.62f;

        // ── The modal's own anchors ──────────────────────────────────────────
        // The panel was 0.20-0.80 x 0.10-0.90, which left a body of only ~357 px - too short to
        // seat a 112 px nav band, a 112 px action band and a usable list at the same time. 0.92 of
        // the canvas height yields ~423-430 px, and the wider box also stops the ~1450 px-wide
        // content from being crammed into 60% of a landscape screen.
        public const float ModalXMin = 0.15f;
        public const float ModalYMin = 0.04f;
        public const float ModalXMax = 0.85f;
        public const float ModalYMax = 0.96f;
        /// <summary>Panel height as a fraction of the canvas - the input to the body-height
        /// derivation at the top of this file.</summary>
        public const float ModalHeightFrac = ModalYMax - ModalYMin;   // 0.92

        // ── Derived ladder (the oracle asserts these fit the measured body) ──

        /// <summary>Rows the root grid needs for <see cref="RootVerbCount"/> verbs.</summary>
        public static int RootRows => (RootVerbCount + RootColumns - 1) / RootColumns;

        /// <summary>Total height the root grid occupies, gaps included.</summary>
        public static float RootGridHeightPx => RootRows * RootCellPx + (RootRows - 1) * BandGapPx;

        /// <summary>Distance from the body top to the top of the scrolling content band.</summary>
        public static float ContentTopInsetPx => NavBandPx + BandGapPx;

        /// <summary>Distance from the body bottom to the bottom of the scrolling content band.</summary>
        public static float ContentBottomInsetPx => ActionBandPx + BandGapPx;

        /// <summary>Everything a sub-screen spends before the content band gets a pixel.</summary>
        public static float SubScreenFixedPx => ContentTopInsetPx + ContentBottomInsetPx;
    }
}
