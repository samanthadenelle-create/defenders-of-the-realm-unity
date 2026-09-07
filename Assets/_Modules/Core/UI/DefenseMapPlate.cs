// =============================================================================
// DefenseMapPlate — the frozen "where it went wrong" diagram (WO-1026 follow-up).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// WHY THIS EXISTS AND WHY IT IS NOT HudMinimapWidget (asked and answered, so nobody
// re-opens it):
//   HudMinimapWidget (WO-828) is a LIVE, hero-centred, provider-fed corner map. It
//   reads the running world through Func<> seams wired by HudKitController. This plate
//   draws a FROZEN HISTORICAL RECORD — positions captured minutes or days ago, of
//   structures that may no longer exist, in a town that has since been rebuilt. There
//   is no live world to read, and under model (c) there never will be: a ghost report
//   describes someone ELSE'S base. Feeding a live-provider widget from a dead record
//   would mean stubbing every provider with a constant, which is not reuse.
//   It is also mechanically impossible: DeNelle.HUD references Core + Data ONLY
//   (CLAUDE.md §5's one enforced invariant), so DeNelle.Village cannot reach it.
//
// WHAT IS REUSED, DELIBERATELY:
//   * RealmAtmosphereStyle — the SHARED pin vocabulary. Marks here are the same seven
//     silhouettes the Realm Map and the minimap speak, resolved through
//     RealmAtmosphereStyle.PinAscii, so a triangle means "threat" on every surface in
//     the game. That table's own header exists precisely so two map surfaces cannot
//     drift; this is a third reader of it, not a third vocabulary.
//   * ElarionUi palette + fonts, ASCII-only text, MinTouch-irrelevant (nothing here is
//     tappable — it is a diagram, not a control).
//   * The WO-828 COST RULE, verbatim: NO Camera, NO RenderTexture, NO render pass.
//     Static Images and TMP labels, built once when the report detail is rendered and
//     destroyed with it. Nothing ticks.
//
// ⛔ COLOURBLIND LAW — the owner is red/green colourblind, so this plate is designed to
//    be read in FULL GREYSCALE and loses nothing:
//      * every mark is a distinct GLYPH first (^ = breach, # = destroyed, O = damaged,
//        o = the Heart), never a coloured dot;
//      * the FIRST breach — the headline of the whole report — carries a LITERAL TEXT
//        LABEL, "1st BREACH", and a ring drawn around it. Later breaches carry their
//        ordinal as text ("2nd", "3rd"). The ordinal is never implied by hue;
//      * the attack path is a LINE, which is a shape;
//      * a legend under the plate spells every glyph out in words.
//    Desaturate this plate and every fact survives.
//
// ⛔ AND THE LABELS NEVER PAINT OUTSIDE THE BAND (WO-1585). Label boxes and font sizes
//    are solved in Plate.Relayout from the plate's MEASURED width — one line, NoWrap,
//    floored at ElarionUiKit.FontFloor, and nudged back inside the plate. A label whose
//    words cannot be seated above that floor shows its GLYPH ALONE; the legend under the
//    plate still says what the glyph means, so the colourblind law above is untouched.
//    The band itself is built by BuildBand, whose sizeDelta RCA is a few lines down.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Defense;
using DeNelle.Core.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    /// <summary>Builds the static top-down diagram of one defence report.</summary>
    public static class DefenseMapPlate
    {
        /// <summary>Legend text, spelled out in words. Public so a caller can render it
        /// outside the plate (the report panel puts it directly beneath).</summary>
        public static readonly string[] Legend =
        {
            "o  the Heart",
            "^  where they broke in (the first is labelled)",
            "#  destroyed",
            "O  damaged",
            "line  the path they took",
        };

        // ── ⭐ THE PLATE BAND IS SIZED BY sizeDelta, NEVER BY A LayoutElement (WO-1585) ──
        //
        // RCA, from the owner's frame Logs/device/seeker-shots/Screenshot_20260907-052735.png
        // (Seeker, build 2026.09.07.359076), measured against the shipping arithmetic:
        //
        //   ElarionUiKit.MakeScrollZone builds its content column with childControlHeight =
        //   FALSE (kit law: rows carry their own pixel height). uGUI's
        //   HorizontalOrVerticalLayoutGroup.GetChildSizes then reads `child.sizeDelta[axis]`
        //   and IGNORES the child's LayoutElement entirely
        //   (Library/PackageCache/com.unity.ugui@a9ea81766fbd/Runtime/UGUI/UI/Core/Layout/
        //   HorizontalOrVerticalLayoutGroup.cs:224-229, read at source 2026-09-07).
        //
        //   DefenseReportPanel handed the plate a LayoutElement asking for 420 px and never
        //   touched sizeDelta, so the band shipped at the RectTransform default of 100 px. The
        //   marks inside are anchor-positioned boxes 44 px tall carrying FontBody text in a
        //   160 px-wide box, so they overflowed the band in BOTH axes: "1st BREACH" wrapped to
        //   "^ / 1st / BREA / CH" (~250 px of text in a 44 px box) and painted straight across
        //   "They came from the west." above it and the legend rows below it.
        //
        //   The frame is the arithmetic: at 2670x1200 the kit scaler resolves
        //   sqrt(2670/1080)*sqrt(1200/1920) = 1.2431, so the LIST rows measure a 135.5 device-px
        //   pitch = 109 canvas px = 100 (default sizeDelta) + 10 (ListRowGapPx). Had the
        //   LayoutElement been honoured the pitch would read 122 canvas / 151.7 device px.
        //   The rows are ALSO under ElarionUiKit.MinTouchPx for the same reason.
        //
        // So the band is built HERE, by BuildBand, with its height written to sizeDelta, and both
        // the panel and DefenseReportLayoutRegression go through that one seam.

        /// <summary>Absolute floor for the plate band. Below this the marks crowd into an
        /// unreadable smear whatever the well can spare.</summary>
        public const float PlateMinPx = 260f;

        /// <summary>Ceiling for the plate band — the diagram is DECORATION over facts already
        /// stated in words, so it never eats the whole well however tall the screen is.</summary>
        public const float PlateMaxPx = 560f;

        /// <summary>Share of the visible detail well the plate may take. The legend rows have to
        /// land under the plate and still be on screen, so the plate never claims the whole view.</summary>
        public const float PlateWellFraction = 0.55f;

        /// <summary>Box width for a plate label, as a fraction of the plate width. A label wider
        /// than this crowds its neighbour, so the words shrink (and past the legibility floor,
        /// drop to the glyph alone — the legend under the plate still spells it out).</summary>
        private const float LabelWidthFrac = 0.44f;

        /// <summary>Absolute clamps on that box, so a very narrow or very wide plate still gets a
        /// sane label box.</summary>
        private const float LabelBoxMinPx = 150f, LabelBoxMaxPx = 460f;

        /// <summary>TMP line box (~1.25em) — the same budget the layout oracles in this repo use.</summary>
        private const float LineBoxMul = 1.25f;

        /// <summary>Fraction of the plate kept clear at each edge, so a mark clamped to the
        /// border cannot hang its glyph or its halo onto the report's text rows.</summary>
        private const float EdgeInset = 0.04f;

        /// <summary>
        /// The band height for a detail well of the given inner size. PURE, and public so the
        /// oracle asserts the SHIPPING arithmetic rather than a copy of it.
        /// <para>Square-ish at the column width, capped so the legend still fits under it, and
        /// floored so the marks stay readable. A zero/unmeasured well falls back to the floor.</para>
        /// </summary>
        public static float DeriveHeightPx(float wellWidthPx, float wellHeightPx)
        {
            if (wellHeightPx <= 1f || wellWidthPx <= 1f) return PlateMinPx;
            float want = Mathf.Min(wellWidthPx, wellHeightPx * PlateWellFraction);
            return Mathf.Clamp(want, PlateMinPx, PlateMaxPx);
        }

        /// <summary>
        /// ⭐ THE ONE SEAM that puts a plate into a kit scroll column: a band whose height is
        /// written to <c>sizeDelta</c> (see the RCA above), with the plate stretched inside it.
        /// Returns the band; <paramref name="plate"/> carries the handle whose
        /// <see cref="Plate.Relayout"/> the caller must run after its layout pass.
        /// <para>A LayoutElement is attached as well, so any host that DOES control child height
        /// reads the same number — but sizeDelta is the operative one and must never be dropped.</para>
        /// </summary>
        public static RectTransform BuildBand(Transform host, DefenseOutcomeRecord record,
            float heightPx, out Plate plate)
        {
            plate = null;
            if (host == null) return null;

            float h = Mathf.Max(PlateMinPx, heightPx);
            var band = new GameObject("MapPlateBand", typeof(RectTransform), typeof(LayoutElement));
            band.transform.SetParent(host, false);
            var brt = (RectTransform)band.transform;
            // ⛔ sizeDelta IS THE HEIGHT. MakeScrollZone runs childControlHeight:false.
            brt.sizeDelta = new Vector2(0f, h);
            var le = band.GetComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
            le.flexibleHeight = 0f;

            plate = Build(band.transform, record);
            FlowTrace.Step("DefenseReport",
                $"map band built: requested={heightPx:F0} applied sizeDelta.y={brt.sizeDelta.y:F0} "
                + $"(LayoutElement.preferredHeight={le.preferredHeight:F0} is ADVISORY — "
                + "MakeScrollZone runs childControlHeight:false and reads sizeDelta).");
            return brt;
        }

        /// <summary>
        /// A built plate. Hold it so <see cref="Relayout"/> can be called once the layout pass
        /// has given the plate a real pixel size — see that method for why this is not optional.
        /// </summary>
        public sealed class Plate
        {
            /// <summary>The plate root (parented under the caller's fixed-height band).</summary>
            public GameObject Root;
            internal RectTransform Rect;
            internal readonly List<(RectTransform seg, Vector2 a, Vector2 b)> Segments
                = new List<(RectTransform, Vector2, Vector2)>();

            /// <summary>Every label drawn ON the plate — marks, ring captions, the compass.
            /// Their boxes and font sizes are solved in <see cref="Relayout"/> for the same
            /// reason the path segments are: the plate has no pixel size at build time.</summary>
            internal readonly List<LabelEntry> Labels = new List<LabelEntry>();

            /// <summary>The first-breach halo, clamped inside the plate in <see cref="Relayout"/>
            /// for the same reason the labels are: a 64px ring on a mark near the border hangs
            /// half of itself onto the report's text rows.</summary>
            internal readonly List<(RectTransform ring, Vector2 at)> Halos
                = new List<(RectTransform, Vector2)>();

            /// <summary>How many labels had to fall back to their GLYPH ALONE at the last
            /// Relayout because the words could not be seated above the legibility floor. Read by
            /// the oracle; the legend under the plate carries the words in that case.</summary>
            public int GlyphOnlyFallbacks { get; private set; }

            /// <summary>
            /// Re-solves the PATH segment geometry AND every label box against the plate's real
            /// pixel size.
            ///
            /// <para>⚠ WHY THIS EXISTS: a rotated line needs a PIXEL length, and the plate's
            /// RectTransform has a zero rect until the vertical layout group has run. Building
            /// and forgetting would leave every path segment clamped to its 2px floor — a
            /// polyline that silently renders as a row of dots, which looks like a design choice
            /// rather than a bug. The caller builds, forces a layout pass, then calls this.</para>
            ///
            /// <para>⛔ THE LABELS ARE SOLVED HERE FOR THE SAME REASON (WO-1585). They used to
            /// carry a hardcoded 160x44 box with unbounded FontBody text and TMP's default
            /// wrapping, which is what turned "1st BREACH" into "1st / BREA / CH" across the
            /// report's sentences. A label box is a fraction of the MEASURED plate width, the
            /// words are scaled to fit it on ONE line, and a label that cannot seat its words at
            /// <see cref="ElarionUiKit.FontFloor"/> drops to its glyph — the legend under the
            /// plate says what the glyph means, so nothing is lost.</para>
            /// </summary>
            public void Relayout()
            {
                if (Rect == null) return;
                Vector2 size = Rect.rect.size;
                if (size.x <= 1f || size.y <= 1f) return;   // still unmeasured; leave as-is

                for (int i = 0; i < Segments.Count; i++)
                {
                    var (seg, a, b) = Segments[i];
                    if (seg == null) continue;
                    Vector2 d = new Vector2((b.x - a.x) * size.x, (b.y - a.y) * size.y);
                    seg.sizeDelta = new Vector2(Mathf.Max(2f, d.magnitude), 3f);
                    seg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                }

                for (int i = 0; i < Halos.Count; i++)
                {
                    var (ring, at) = Halos[i];
                    if (ring == null) continue;
                    ring.anchoredPosition = Inset(at, ring.sizeDelta, size);
                }

                GlyphOnlyFallbacks = 0;
                var trace = new System.Text.StringBuilder();
                for (int i = 0; i < Labels.Count; i++)
                {
                    var e = Labels[i];
                    if (e == null || e.Text == null || e.Rect == null) continue;
                    bool dropped = SolveLabel(e, size);
                    if (dropped) GlyphOnlyFallbacks++;
                    if (trace.Length < 900)
                        trace.Append(" | ").Append(e.Text.text.Replace("\n", "/"))
                             .Append(" font=").Append(e.Text.fontSize.ToString("0.#"))
                             .Append(" box=").Append(e.Rect.sizeDelta.x.ToString("0"))
                             .Append('x').Append(e.Rect.sizeDelta.y.ToString("0"))
                             .Append(dropped ? " GLYPH-ONLY" : "");
                }

                FlowTrace.Step("DefenseReport",
                    $"plate relayout: rect={size.x:F0}x{size.y:F0} segments={Segments.Count} "
                    + $"labels={Labels.Count} glyphOnly={GlyphOnlyFallbacks}{trace}");
            }

            /// <summary>Seats one label on ONE line inside a box derived from the plate width, and
            /// nudges the box back inside the plate so it can never paint on a neighbouring row.
            /// Returns true when the words had to be dropped for the glyph alone.</summary>
            private bool SolveLabel(LabelEntry e, Vector2 size)
            {
                var t = e.Text;
                float boxW = Mathf.Clamp(size.x * LabelWidthFrac, LabelBoxMinPx, LabelBoxMaxPx);

                // ⛔ NoWrap is the fix, not a nicety: TMP's default wrapping is what broke the
                //    word "BREACH" in half. Overflow (not Ellipsis) because the FIT below is what
                //    decides what is shown — an ellipsised "1st BRE..." tells the player nothing.
                t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                t.overflowMode = TMPro.TextOverflowModes.Overflow;
                t.enableAutoSizing = false;

                string full = string.IsNullOrEmpty(e.Label) ? e.Glyph : e.Glyph + " " + e.Label;
                t.fontSize = e.MaxFont;
                t.text = full;

                bool dropped = false;
                float wanted = Width(t, full);
                if (wanted > boxW && wanted > 0f)
                {
                    float fitted = e.MaxFont * boxW / wanted;
                    if (fitted >= ElarionUiKit.FontFloor)
                    {
                        t.fontSize = fitted;
                    }
                    else if (!string.IsNullOrEmpty(e.Label))
                    {
                        // Cannot seat the words legibly. The GLYPH carries the mark; the legend
                        // beneath the plate carries the word. Never a sub-floor smear.
                        t.text = e.Glyph;
                        t.fontSize = e.MaxFont;
                        dropped = true;
                    }
                    else
                    {
                        t.fontSize = Mathf.Max(ElarionUiKit.FontFloor, fitted);
                    }
                }

                // The box never squeezes what it now holds — a glyph is allowed to widen it.
                float finalW = Width(t, t.text);
                boxW = Mathf.Max(boxW, finalW + 4f);
                float boxH = Mathf.Max(t.fontSize * LineBoxMul, 8f);
                e.Rect.sizeDelta = new Vector2(boxW, boxH);

                // ⭐ CONTAINMENT. The mark is anchored at a plate fraction, so a mark near an edge
                //    would hang its box outside the band and onto the report's text rows. Nudge it
                //    back in; that is a diagram detail moving, never a fact being hidden.
                e.Rect.anchoredPosition = Inset(e.At, new Vector2(boxW, boxH), size);
                return dropped;
            }

            private static float Width(TMP_Text t, string s)
            {
                if (t == null || string.IsNullOrEmpty(s)) return 0f;
                // The SINGLE-ARG overload: TMP measures unconstrained (it forces NoWrap/Overflow
                // internally) at the current fontSize, which is exactly the natural width the fit
                // below needs. The (s, 0f, 0f) form sets a ~zero text area and is version-sensitive
                // -- and a Width() that misreads makes every label either never shrink or always
                // fall back to its glyph.
                return t.GetPreferredValues(s).x;
            }

            /// <summary>Offset that pulls a box anchored at <paramref name="at"/> wholly inside a
            /// plate of <paramref name="size"/>. Centres it when it is simply too big to fit.</summary>
            private static Vector2 Inset(Vector2 at, Vector2 box, Vector2 size)
            {
                float px = at.x * size.x, py = at.y * size.y;
                float hw = box.x * 0.5f, hh = box.y * 0.5f;
                float dx, dy;
                if (box.x >= size.x) dx = size.x * 0.5f - px;
                else if (px - hw < 0f) dx = hw - px;
                else if (px + hw > size.x) dx = size.x - (px + hw);
                else dx = 0f;
                if (box.y >= size.y) dy = size.y * 0.5f - py;
                else if (py - hh < 0f) dy = hh - py;
                else if (py + hh > size.y) dy = size.y - (py + hh);
                else dy = 0f;
                return new Vector2(dx, dy);
            }
        }

        /// <summary>One label drawn on the plate, with everything <see cref="Plate.Relayout"/>
        /// needs to seat it once the plate has a real pixel size.</summary>
        internal sealed class LabelEntry
        {
            public RectTransform Rect;
            public TextMeshProUGUI Text;
            public Vector2 At;        // plate fraction the mark sits at
            public string Glyph;      // always shown
            public string Label;      // the words; dropped only when they cannot be seated legibly
            public float MaxFont;
        }

        /// <summary>
        /// Builds the plate as a child of <paramref name="parent"/>, filling it.
        /// Returns null only if the record is unusable (traced). Never throws.
        /// <para>The caller MUST call <see cref="Plate.Relayout"/> after its layout pass.</para>
        /// </summary>
        public static Plate Build(Transform parent, DefenseOutcomeRecord record)
        {
            if (parent == null || record == null) return null;

            Plate plateHandle = null;
            GameObject root = null;
            Guard.Try("Siege", "build defense map plate", () =>
            {
                plateHandle = new Plate();
                root = new GameObject("DefenseMapPlate", typeof(RectTransform), typeof(Image));
                root.transform.SetParent(parent, false);
                var rt = root.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var plate = root.GetComponent<Image>();
                plate.color = ElarionUiKit.GlassDeep;   // static dark-glass backing, one Image
                plate.raycastTarget = false;

                plateHandle.Root = root;
                plateHandle.Rect = rt;

                float cx = record.Defender.CoreX;
                float cz = record.Defender.CoreZ;
                float extent = ResolveExtent(record, cx, cz);

                // ── Band rings (they are the report's own stored radii, so the diagram
                //    and the FRONT/SECOND/CORE grouping in the list cannot disagree) ──
                if (record.Defender.FrontRadius > 0f)
                    Ring(plateHandle, rt, record.Defender.FrontRadius / extent, ElarionUi.ParchmentDim, "front line");
                if (record.Defender.CoreRadius > 0f)
                    Ring(plateHandle, rt, record.Defender.CoreRadius / extent, ElarionUi.ParchmentDim, "core");

                // ── The path they took, oldest -> newest ──────────────────────────
                var path = record.Path;
                for (int i = 1; i < path.Count; i++)
                {
                    var a = path[i - 1];
                    var b = path[i];
                    if (a == null || b == null) continue;
                    Segment(plateHandle, rt,
                        Project(a.WorldX, a.WorldZ, cx, cz, extent),
                        Project(b.WorldX, b.WorldZ, cx, cz, extent),
                        ElarionUi.ParchmentDim);
                }
                if (path.Count > 0 && path[0] != null)
                    Mark(plateHandle, rt, Project(path[0].WorldX, path[0].WorldZ, cx, cz, extent),
                        RealmAtmosphereStyle.PinAscii(RealmPinShape.BarHorizontal),
                        "they came from here", ElarionUi.ParchmentDim, small: true);

                // ── Rows ────────────────────────────────────────────────────────
                for (int i = 0; i < record.Rows.Count; i++)
                {
                    var l = record.Rows[i];
                    if (l == null) continue;
                    // A row the vitals watch never resolved has no position; drawing it at the
                    // Heart would be a LIE about where it stood, so it is simply not pinned
                    // (it still appears in the list below the plate).
                    if (l.WorldX == 0f && l.WorldZ == 0f && l.DistanceFromCore <= 0f) continue;

                    Mark(plateHandle, rt, Project(l.WorldX, l.WorldZ, cx, cz, extent),
                        RealmAtmosphereStyle.PinAscii(l.Destroyed ? RealmPinShape.Square : RealmPinShape.Ring),
                        l.Destroyed ? "destroyed" : "damaged",
                        l.Destroyed ? ElarionUi.Danger : ElarionUi.Parchment,
                        small: true);
                }

                // ── Breaches. THE FIRST ONE IS THE HEADLINE and is labelled in words. ──
                for (int i = 0; i < record.Breaches.Count; i++)
                {
                    var b = record.Breaches[i];
                    if (b == null) continue;
                    Vector2 p = Project(b.WorldX, b.WorldZ, cx, cz, extent);
                    bool first = i == 0;
                    Mark(plateHandle, rt, p, RealmAtmosphereStyle.PinAscii(RealmPinShape.TriangleUp),
                        first ? "1st BREACH" : Ordinal(i + 1),
                        first ? ElarionUi.Gilt : ElarionUi.Parchment,
                        small: !first);
                    if (first) Halo(plateHandle, rt, p);   // a RING around it -- shape, not hue
                }

                // ── The Heart, last so it sits on top ─────────────────────────────
                Mark(plateHandle, rt, new Vector2(0.5f, 0.5f),
                    RealmAtmosphereStyle.PinAscii(RealmPinShape.Circle),
                    "HEART", ElarionUi.Gilt, small: false);

                // North-up, like the minimap, and SAID so rather than assumed.
                Caption(plateHandle, rt, "N", new Vector2(0.5f, 0.94f), ElarionUi.ParchmentDim);

                FlowTrace.Step("Siege",
                    $"map plate built: extent={extent:F0}m breaches={record.Breaches.Count} " +
                    $"losses={record.Rows.Count} path={path.Count}.");
            });

            return root != null ? plateHandle : null;
        }

        // =====================================================================
        //  Projection — north-up, Heart-centred
        // =====================================================================

        /// <summary>Half-width of the diagram in metres. Sized so every mark FITS: a plate that
        /// clipped the breach would hide the one thing the player opened it for.</summary>
        private static float ResolveExtent(DefenseOutcomeRecord r, float cx, float cz)
        {
            float e = Mathf.Max(r.Defender.FrontRadius * 1.35f, r.Defender.CoreRadius * 2f);
            for (int i = 0; i < r.Breaches.Count; i++)
                if (r.Breaches[i] != null) e = Mathf.Max(e, Dist(r.Breaches[i].WorldX, r.Breaches[i].WorldZ, cx, cz));
            for (int i = 0; i < r.Rows.Count; i++)
                if (r.Rows[i] != null) e = Mathf.Max(e, r.Rows[i].DistanceFromCore);
            for (int i = 0; i < r.Path.Count; i++)
                if (r.Path[i] != null) e = Mathf.Max(e, Dist(r.Path[i].WorldX, r.Path[i].WorldZ, cx, cz));
            return Mathf.Max(10f, e * 1.1f);   // 10m floor so a tiny base is not magnified into noise
        }

        private static float Dist(float x, float z, float cx, float cz)
            => Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));

        /// <summary>World (x,z) to plate fraction. NORTH-UP: world +Z is plate up, the same
        /// choice HudMinimapWidget makes, so the two surfaces never disagree on which way is up.</summary>
        private static Vector2 Project(float x, float z, float cx, float cz, float extent)
        {
            float u = 0.5f + ((x - cx) / (extent * 2f));
            float v = 0.5f + ((z - cz) / (extent * 2f));
            // Inset rather than Clamp01 (WO-1585): a mark pinned exactly on the border hangs its
            // glyph and its halo half outside the band and onto the report's text rows. The
            // extent is already sized so nothing real lands out here (ResolveExtent adds 10%), so
            // this only moves points that were being clamped -- i.e. already approximate.
            return new Vector2(Mathf.Clamp(u, EdgeInset, 1f - EdgeInset),
                               Mathf.Clamp(v, EdgeInset, 1f - EdgeInset));
        }

        // =====================================================================
        //  Primitives (static Images + TMP; nothing ticks)
        // =====================================================================

        private static void Ring(Plate plate, RectTransform parent, float radiusFrac, Color tint, string label)
        {
            radiusFrac = Mathf.Clamp(radiusFrac, 0.02f, 0.49f);
            var go = new GameObject("Band_" + label, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f - radiusFrac, 0.5f - radiusFrac);
            rt.anchorMax = new Vector2(0.5f + radiusFrac, 0.5f + radiusFrac);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(tint.r, tint.g, tint.b, 0.10f);
            img.raycastTarget = false;
            Caption(plate, parent, label, new Vector2(0.5f, 0.5f + radiusFrac), tint);
        }

        /// <summary>One path segment. Its ANCHOR (the midpoint) is set here; its LENGTH and
        /// ROTATION are deferred to <see cref="Plate.Relayout"/>, because both need the plate's
        /// real pixel size and the layout pass has not run yet.</summary>
        private static void Segment(Plate plate, RectTransform parent, Vector2 a, Vector2 b, Color tint)
        {
            var go = new GameObject("PathSeg", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Vector2 mid = (a + b) * 0.5f;
            rt.anchorMin = mid; rt.anchorMax = mid;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(2f, 3f);
            rt.anchoredPosition = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = new Color(tint.r, tint.g, tint.b, 0.55f);
            img.raycastTarget = false;

            plate.Segments.Add((rt, a, b));
        }

        /// <summary>A ring drawn AROUND a mark. The first-breach emphasis is a SHAPE, so it
        /// survives greyscale; the "1st BREACH" text does the actual telling.</summary>
        private static void Halo(Plate plate, RectTransform parent, Vector2 at)
        {
            var go = new GameObject("FirstBreachHalo", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = at; rt.anchorMax = at;
            rt.sizeDelta = new Vector2(64f, 64f);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.22f);
            img.raycastTarget = false;
            plate?.Halos.Add((rt, at));
        }

        /// <summary>
        /// One mark. The BOX and the FONT SIZE are deliberately NOT decided here — they are
        /// registered on the plate and solved in <see cref="Plate.Relayout"/> against the plate's
        /// measured width (WO-1585). A hardcoded 160x44 box with unbounded FontBody text and TMP's
        /// default wrapping is exactly what broke "1st BREACH" into "1st / BREA / CH" across the
        /// report's sentences on the owner's 2026-09-07 frame.
        /// </summary>
        private static void Mark(Plate plate, RectTransform parent, Vector2 at, string glyph, string label,
            Color tint, bool small)
        {
            var go = new GameObject("Mark_" + label, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = at; rt.anchorMax = at;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            var t = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            // GLYPH first, then the words. ASCII only (the build font tofus anything else).
            t.text = small ? glyph : glyph + " " + label;
            t.fontSize = small ? ElarionUi.FontLabel : ElarionUi.FontBody;
            t.color = tint;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            if (!small) t.fontStyle = FontStyles.Bold;

            Register(plate, rt, t, at, glyph, small ? string.Empty : label,
                small ? ElarionUi.FontLabel : ElarionUi.FontBody);
        }

        private static void Caption(Plate plate, RectTransform parent, string text, Vector2 at, Color tint)
        {
            var go = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = at; rt.anchorMax = at;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = text;
            t.fontSize = ElarionUi.FontLabel;
            t.color = new Color(tint.r, tint.g, tint.b, 0.75f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;

            // A caption is all glyph: there is no second word to drop, so it shrinks to fit and
            // never falls back.
            Register(plate, rt, t, at, text, string.Empty, ElarionUi.FontLabel);
        }

        /// <summary>Provisional box + registration. The provisional size only has to be sane for
        /// the frame before the first <see cref="Plate.Relayout"/>.</summary>
        private static void Register(Plate plate, RectTransform rt, TextMeshProUGUI t, Vector2 at,
            string glyph, string label, float maxFont)
        {
            rt.sizeDelta = new Vector2(LabelBoxMinPx, maxFont * LineBoxMul);
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            t.overflowMode = TMPro.TextOverflowModes.Overflow;
            if (plate == null) return;
            plate.Labels.Add(new LabelEntry
            {
                Rect = rt,
                Text = t,
                At = at,
                Glyph = glyph,
                Label = label,
                MaxFont = maxFont,
            });
        }

        private static string Ordinal(int n)
        {
            switch (n)
            {
                case 2: return "2nd";
                case 3: return "3rd";
                default: return n + "th";
            }
        }

        /// <summary>The plate's marks as TEXT ROWS — the accessible twin of the diagram, and the
        /// fallback when the plate cannot be built. Every fact on the plate appears here in
        /// words, which is what makes the diagram decorative rather than load-bearing.</summary>
        public static List<string> DescribeMarks(DefenseOutcomeRecord record)
        {
            var rows = new List<string>();
            if (record == null) return rows;

            var first = record.Breaches.Count > 0 ? record.Breaches[0] : null;
            if (first != null)
                rows.Add("1st BREACH: " + (string.IsNullOrEmpty(first.DisplayName) ? "open ground" : first.DisplayName)
                         + " at " + Mathf.RoundToInt(first.AtSeconds) + "s"
                         + " (" + Compass(first.WorldX - record.Defender.CoreX,
                                          first.WorldZ - record.Defender.CoreZ) + " of the Heart)");
            else
                rows.Add("No breach: nothing crossed your inner ring.");

            if (record.Path.Count > 0 && record.Path[0] != null)
                rows.Add("They came from the "
                         + Compass(record.Path[0].WorldX - record.Defender.CoreX,
                                   record.Path[0].WorldZ - record.Defender.CoreZ) + ".");
            return rows;
        }

        /// <summary>Eight-point compass word for an offset. Words, not an arrow glyph — the
        /// build font has no reliable arrows, and "north-east" reads at any size.</summary>
        public static string Compass(float dx, float dz)
        {
            if (Mathf.Abs(dx) < 0.001f && Mathf.Abs(dz) < 0.001f) return "centre";
            float ang = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;   // 0 = +Z = north
            if (ang < 0f) ang += 360f;
            int oct = Mathf.RoundToInt(ang / 45f) % 8;
            switch (oct)
            {
                case 0: return "north";
                case 1: return "north-east";
                case 2: return "east";
                case 3: return "south-east";
                case 4: return "south";
                case 5: return "south-west";
                case 6: return "west";
                default: return "north-west";
            }
        }
    }
}
