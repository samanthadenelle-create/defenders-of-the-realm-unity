// =============================================================================
// HudLayoutBands - THE ONE AUTHORITY for the town HUD's left column and for the
// single reserved TOAST ZONE (WO-1219, owner-approved design 2026-08-26).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// WHY THIS FILE EXISTS AT ALL
// ---------------------------
// The left column is the most over-subscribed strip on the HUD (hero plate,
// SKILL chip, Heart bar, minimap, region status line, gear, Store), and every
// one of those seven things was positioned in a DIFFERENT file:
//   * HudAreasHost           - the area mounts (fractions of screen)
//   * HudKitController       - the hero plate + SKILL chip sub-rects, and the
//                              fixed-pixel gear/Store row inside the Dock band
//   * HudMinimapWidget       - the plate and the region chip inside the Minimap band
//   * HubRepairAffordance    - a Village card that lands ON TOP of all of it
// Nobody owned the WHOLE column, so nobody could see that the sum of the parts
// no longer fit. That is exactly how WO-1219 shipped: the gear/Store row sat on
// the minimap's lower edge and the region status line read out from UNDER the
// gear, in two separate device captures, with every marker green.
//
// So the column is now ONE table. Each consumer reads its band from here; the
// oracle (HudUiRegression check 8) reads the SAME table and asserts that no two
// bands intersect at the owner's device resolution. A layout regression is now a
// FAILING BUILD instead of a felt-test report.
//
// ⛔ THE BANDS ARE EXCLUSIVE. That is the whole invariant. If a new element needs
// a seat in the left column, it gets its OWN band here and the neighbours move -
// it does NOT get drawn across one that is already spoken for.
//
// ⚠ FRACTIONS CANNOT PROMISE PIXELS. Three of the seven elements are authored in
// FIXED reference units (the 200-unit minimap plate, the 30-unit region chip, the
// two 112-unit MinTouchPx controls) precisely because a fraction of a band changes
// its aspect with the device. So this file stores the MOUNTS as fractions and the
// occupants as PIXELS, and RESOLVES the two together for a given screen size
// (ResolveLeftColumn). The band arithmetic that WO-1219 had to be re-derived by
// hand, twice, is now executable.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Authored geometry for the town HUD's left column plus the one reserved toast
    /// zone. Pure data + pure arithmetic: no MonoBehaviour, no scene dependency, so
    /// the Editor regression can resolve the exact same rects the runtime uses.
    /// </summary>
    public static class HudLayoutBands
    {
        // ── The canvas the HUD kit is authored against (HudAreasHost) ────────────
        /// <summary>Reference resolution of the HUD kit canvas (CanvasScaler).</summary>
        public const float CanvasRefWidth  = 1080f;
        /// <summary>Reference resolution of the HUD kit canvas (CanvasScaler).</summary>
        public const float CanvasRefHeight = 1920f;
        /// <summary>CanvasScaler matchWidthOrHeight used by HudAreasHost.</summary>
        public const float CanvasMatch = 0.5f;

        /// <summary>The owner's device, and therefore the resolution every band claim in
        /// this file is stated at (Seeker, landscape).</summary>
        public const float DeviceWidth  = 2670f;
        /// <summary>See <see cref="DeviceWidth"/>.</summary>
        public const float DeviceHeight = 1200f;

        // ── MOUNTS: fractions of screen, y-UP, handed to HudAreasHost ────────────
        // Read top-to-bottom, they ARE the approved stack:
        //   hero plate -> SKILL chip -> Heart bar -> minimap -> status line -> gear + Store.

        /// <summary>Vitals mount - holds TWO exclusive bands: the hero plate on top and the
        /// SKILL (Wisdom) chip beneath it. They are separate sub-rects, never a shared box.</summary>
        public static readonly Rect VitalsMount = Rect.MinMaxRect(0.011f, 0.800f, 0.240f, 0.983f);

        /// <summary>Heart of Elarion bar mount.</summary>
        public static readonly Rect HeartMount = Rect.MinMaxRect(0.011f, 0.700f, 0.240f, 0.790f);

        /// <summary>Minimap mount - holds TWO exclusive bands: the square plate hanging from
        /// the mount's top-left, and the region STATUS LINE in its own band BELOW the plate.
        /// ⛔ The status line is never drawn ACROSS the plate and never beside it: the owner
        /// captured "Elarion - Safe - N threats" competing with both the map and the gear.</summary>
        public static readonly Rect MinimapMount = Rect.MinMaxRect(0.011f, 0.420f, 0.240f, 0.685f);

        /// <summary>Dock mount - the gear + Store row (side by side, never stacked) and the
        /// slide-out drawer that opens to the right of both.</summary>
        public static readonly Rect DockMount = Rect.MinMaxRect(0.000f, 0.360f, 0.230f, 0.470f);

        // ── SUB-RECTS inside the Vitals mount (fractions OF THE MOUNT) ───────────
        // Derived once here so HudKitController never re-derives them by hand.

        /// <summary>Hero nameplate, as a fraction of <see cref="VitalsMount"/>. Screen band
        /// 0.011..0.240 x, 0.883..0.983 y.</summary>
        public static readonly Rect HeroPlateInVitals = Rect.MinMaxRect(0f, 0.180f, 1f, 1f);

        // ── FIXED-PIXEL occupants (reference units, never fractions) ────────────

        /// <summary>Minimap plate edge in reference units (HudMinimapWidget.PlateSize).</summary>
        public const float MinimapPlatePx = 200f;
        /// <summary>Region status-line height in reference units (HudMinimapWidget.ChipHeight).</summary>
        public const float StatusLinePx = 30f;
        /// <summary>Gap between the minimap plate and the status line band below it.</summary>
        public const float StatusLineGapPx = 4f;

        /// <summary>Gear / Store control edge. This is <c>ElarionUiKit.MinTouchPx</c> VERBATIM -
        /// the kit touch floor. ⛔ Nothing here may shrink it, and satisfying it may not create a
        /// new overlap (that is what broke hero-select). Both controls are authored at EXACTLY
        /// the floor, so ClampMinTouch is a no-op on both and is NOT a cause of anything here.</summary>
        public const float DockControlPx = ElarionUiKit.MinTouchPx;
        /// <summary>Gap between the gear and the Store face, and between the pair and the drawer.</summary>
        public const float DockGapPx = 12f;
        /// <summary>Left breathing margin for the dock row (SafeAreaInset.EdgeMarginPx).</summary>
        public const float DockEdgePx = SafeAreaInset.EdgeMarginPx;

        // ── WO-1335 — THE NIGHT MARKET CARD'S OWN BAND ──────────────────────────
        //
        // Owner ruling 2026-09-03: *"the realm store is hidden away needs a permanent face on
        // hud"* / *"can you take the realm store card from settings > night market and anchor it
        // smaller to left side on hud"*. So the card that already exists inside the Realm deck
        // (PlayerDeckWorkspace's "Realm Store" route, art key `realm-store`) gets a permanent
        // seat in this column, and it gets it HERE rather than by being drawn across somebody
        // else's band - the mistake this whole file exists to stop.
        //
        // ⭐ WHY IT SITS IN THE MINIMAP MOUNT, MEASURED RATHER THAN GUESSED. The column has room
        // for exactly one more control and only in one place. At the owner's device (2670x1200,
        // canvas 2147.9 x 965.4 reference units) the clear vertical between the region status
        // line's bottom edge and the MoveCluster mount's top edge is
        //     0.4426 - 0.330 = 0.1126 of screen height = 108.7 reference units,
        // which is 3.3 units UNDER the 112-unit touch floor. So a second control CANNOT be added
        // beside or below the gear without moving a neighbour. The Minimap mount, by contrast, is
        // 491.9 x 255.8 units and is EMPTY at runtime: HudKitController constructs no
        // HudMinimapWidget ("Locked adaptive-HUD ruling: no minimap is constructed on the player
        // HUD"), so MinimapPlatePx / StatusLinePx above currently describe a plate and a status
        // line that nothing draws.
        //
        // ⚠ THEREFORE THIS BAND TAKES THE PLATE'S SEAT, AND THAT CONFLICT IS STATED, NOT HIDDEN.
        // If the minimap plate is ever constructed again, these two collide and MUST be re-split
        // in this file - and the seat to move the card to is already measured: the pocket to the
        // RIGHT of the plate inside the same mount is 291.7 x 200 units
        // (x from MinimapMount.xMin + MinimapPlatePx, y the plate's own band), which still holds
        // a 272 x 132 card. Do not resolve that day by drawing the card across the plate.
        //
        // ⛔ CLEAR OF THE MOVEMENT STICK BY CONSTRUCTION, WHICH IS THE ONE NON-NEGOTIABLE. The
        // card's band bottoms out at y 0.548 of screen; the MoveCluster mount tops out at 0.330.
        // Covering the stick breaks the game's only movement control, so the oracle asserts this
        // rather than trusting the arithmetic above to stay true.

        /// <summary>Night Market card width in reference units. 272 x 132 keeps the 1798x875
        /// `realm-store` card art at its authored 2.055:1 aspect, fits the Minimap mount's
        /// 491.9-unit width with room to spare, and clears the touch floor on BOTH axes.</summary>
        public const float NightMarketCardWidthPx = 272f;
        /// <summary>See <see cref="NightMarketCardWidthPx"/>. 132 = 272 / 2.055, the card art's
        /// own aspect - never an independently chosen number.</summary>
        public const float NightMarketCardHeightPx = 132f;

        /// <summary>
        /// The Night Market card's screen band, hung from the TOP-LEFT of
        /// <see cref="MinimapMount"/> at a fixed reference size (pixels, never a fraction of the
        /// mount - see this file's header: a fraction changes its aspect with the device and a
        /// touch floor is stated in pixels).
        /// </summary>
        public static Rect ResolveNightMarketCard(float screenW, float screenH)
        {
            var refSize = CanvasReferenceSize(screenW, screenH);
            float ux = refSize.x > 0f ? 1f / refSize.x : 0f;
            float uy = refSize.y > 0f ? 1f / refSize.y : 0f;
            return Rect.MinMaxRect(
                MinimapMount.xMin,
                MinimapMount.yMax - NightMarketCardHeightPx * uy,
                MinimapMount.xMin + NightMarketCardWidthPx * ux,
                MinimapMount.yMax);
        }

        // ── THE ONE RESERVED TOAST ZONE ─────────────────────────────────────────

        /// <summary>
        /// ⭐ THE LEGAL SEAT FOR ANY TRANSIENT TOAST ON THE TOWN HUD - centred above the action
        /// bar, overlapping NOTHING (owner-approved, WO-1219 addendum 2026-08-26).
        ///
        /// WHY IT IS A SHARED CONSTANT AND NOT A LOCAL FIX: the Repair All card was authored in
        /// DeNelle.Village against a left-column seat it could not see the contents of, and it
        /// landed on the minimap, the status line AND the gear at once (tmp/shield-seat-101829.png).
        /// Any module can raise a toast; only ONE of them can pick a seat, so the seat is data.
        /// WO-1236's dungeon-flag acknowledgement uses THIS zone too - it is the convention, not
        /// a 1219-local patch.
        ///
        /// Verified clear of every HudAreasHost band at the owner's resolution: ActionBar tops out
        /// at y 0.150, MoveCluster ends at x 0.270, ActionRail and QueueStatus start at x 0.780,
        /// TargetInfo does not begin until y 0.660, and the whole left column ends at x 0.240.
        /// </summary>
        public static readonly Rect ToastZone = Rect.MinMaxRect(0.375f, 0.203f, 0.625f, 0.308f);

        /// <summary>Seat a RectTransform in the reserved toast zone (anchors, full stretch).</summary>
        public static void ApplyToastZone(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(ToastZone.xMin, ToastZone.yMin);
            rt.anchorMax = new Vector2(ToastZone.xMax, ToastZone.yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>A horizontal slice of the reserved toast zone, so a card that needs two
        /// controls (e.g. Repair All + its acknowledge close) still lands wholly inside it.
        /// <paramref name="from"/>/<paramref name="to"/> are 0..1 across the zone's width.</summary>
        public static Rect ToastZoneSlice(float from, float to)
        {
            float x0 = Mathf.Lerp(ToastZone.xMin, ToastZone.xMax, Mathf.Clamp01(from));
            float x1 = Mathf.Lerp(ToastZone.xMin, ToastZone.xMax, Mathf.Clamp01(to));
            return Rect.MinMaxRect(x0, ToastZone.yMin, x1, ToastZone.yMax);
        }

        // ── RESOLUTION: fractions + pixels -> the seven real screen rects ────────

        /// <summary>The CanvasScaler reference size (in reference units) for a screen, using the
        /// kit's authored reference resolution and match mode. This is the arithmetic WO-1219 had
        /// to do by hand twice; it is executable now.</summary>
        public static Vector2 CanvasReferenceSize(float screenW, float screenH)
        {
            if (screenW <= 0f || screenH <= 0f) return new Vector2(CanvasRefWidth, CanvasRefHeight);
            float logW = Mathf.Log(screenW / CanvasRefWidth, 2f);
            float logH = Mathf.Log(screenH / CanvasRefHeight, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, CanvasMatch));   // Unity's own formula
            if (scale <= 0f) return new Vector2(CanvasRefWidth, CanvasRefHeight);
            return new Vector2(screenW / scale, screenH / scale);
        }

        /// <summary>The seven left-column bands, in the order they stack top-to-bottom.
        /// Index order matches <see cref="LeftColumnNames"/>.</summary>
        public static Rect[] ResolveLeftColumn(float screenW, float screenH)
        {
            var refSize = CanvasReferenceSize(screenW, screenH);
            float ux = refSize.x > 0f ? 1f / refSize.x : 0f;   // one reference unit, as an x-fraction
            float uy = refSize.y > 0f ? 1f / refSize.y : 0f;   // one reference unit, as a y-fraction

            var heroPlate = SubRect(VitalsMount, HeroPlateInVitals);

            // The minimap plate hangs from the mount's TOP-LEFT at a fixed square size, and the
            // status line takes its own band immediately BELOW it. Both are pixels, so both are
            // resolved against the canvas rather than the band's aspect.
            float plateW = MinimapPlatePx * ux, plateH = MinimapPlatePx * uy;
            var minimapPlate = Rect.MinMaxRect(MinimapMount.xMin, MinimapMount.yMax - plateH,
                                               MinimapMount.xMin + plateW, MinimapMount.yMax);
            float lineTop    = MinimapMount.yMax - (MinimapPlatePx + StatusLineGapPx) * uy;
            float lineBottom = lineTop - StatusLinePx * uy;
            var statusLine = Rect.MinMaxRect(MinimapMount.xMin + StatusLineGapPx * ux, lineBottom,
                                             MinimapMount.xMax - StatusLineGapPx * ux, lineTop);

            // The gear is a fixed-pixel control centred on the Dock band.
            //
            // ⚠ WO-1335 - THE SECOND SEAT IN THIS ROW IS GONE, AND ITS REMOVAL IS A CORRECTION.
            // This resolver used to return a 112-unit "Store" band beside the gear. No such
            // control has existed since HudKitController folded secondary navigation into the one
            // menu handle ("The former persistent 'Realm' face was actually the Store/Night
            // Market"), so the table was reserving a seat for a widget that is not built - a
            // phantom occupant is worse than a missing one, because the next reader budgets
            // around it. The store face is back as of WO-1335, but as the NIGHT MARKET CARD in
            // its own band (ResolveNightMarketCard), not as a square beside the gear: 108.7 of
            // the 112 units that seat needs are all the column has left there.
            float ctrlH = DockControlPx * uy, ctrlW = DockControlPx * ux;
            float rowMidY = (DockMount.yMin + DockMount.yMax) * 0.5f;
            float rowBottom = rowMidY - ctrlH * 0.5f, rowTop = rowMidY + ctrlH * 0.5f;
            float gearX = DockMount.xMin + DockEdgePx * ux;
            var gear = Rect.MinMaxRect(gearX, rowBottom, gearX + ctrlW, rowTop);

            var nightMarketCard = ResolveNightMarketCard(screenW, screenH);

            return new[] { heroPlate, HeartMount, minimapPlate, statusLine, gear, nightMarketCard };
        }

        /// <summary>Human names for <see cref="ResolveLeftColumn"/>, same order.</summary>
        public static readonly string[] LeftColumnNames =
        {
            "hero plate", "Heart objective", "minimap plate", "status line", "gear",
            "Night Market card",
        };

        /// <summary>Project a sub-rect expressed as a fraction of a parent band into screen
        /// fractions.</summary>
        public static Rect SubRect(Rect parent, Rect childFraction)
        {
            return Rect.MinMaxRect(
                parent.xMin + childFraction.xMin * parent.width,
                parent.yMin + childFraction.yMin * parent.height,
                parent.xMin + childFraction.xMax * parent.width,
                parent.yMin + childFraction.yMax * parent.height);
        }

        /// <summary>True when two bands share any area. Touching edges are NOT an overlap.</summary>
        public static bool Intersects(Rect a, Rect b)
        {
            return a.xMin < b.xMax - Epsilon && b.xMin < a.xMax - Epsilon &&
                   a.yMin < b.yMax - Epsilon && b.yMin < a.yMax - Epsilon;
        }

        /// <summary>Slack allowed before two bands count as colliding (sub-pixel authoring noise).</summary>
        public const float Epsilon = 0.0005f;
    }
}
