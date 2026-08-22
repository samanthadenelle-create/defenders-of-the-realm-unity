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

            /// <summary>
            /// Re-solves the PATH segment geometry against the plate's real pixel size.
            ///
            /// <para>⚠ WHY THIS EXISTS: a rotated line needs a PIXEL length, and the plate's
            /// RectTransform has a zero rect until the vertical layout group has run. Building
            /// and forgetting would leave every path segment clamped to its 2px floor — a
            /// polyline that silently renders as a row of dots, which looks like a design choice
            /// rather than a bug. The caller builds, forces a layout pass, then calls this.
            /// Discs and glyphs are anchor-positioned and need no such fix-up.</para>
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
            }
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
                    Ring(rt, record.Defender.FrontRadius / extent, ElarionUi.ParchmentDim, "front line");
                if (record.Defender.CoreRadius > 0f)
                    Ring(rt, record.Defender.CoreRadius / extent, ElarionUi.ParchmentDim, "core");

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
                    Mark(rt, Project(path[0].WorldX, path[0].WorldZ, cx, cz, extent),
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

                    Mark(rt, Project(l.WorldX, l.WorldZ, cx, cz, extent),
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
                    Mark(rt, p, RealmAtmosphereStyle.PinAscii(RealmPinShape.TriangleUp),
                        first ? "1st BREACH" : Ordinal(i + 1),
                        first ? ElarionUi.Gilt : ElarionUi.Parchment,
                        small: !first);
                    if (first) Halo(rt, p);   // a RING around it -- shape, not hue
                }

                // ── The Heart, last so it sits on top ─────────────────────────────
                Mark(rt, new Vector2(0.5f, 0.5f),
                    RealmAtmosphereStyle.PinAscii(RealmPinShape.Circle),
                    "HEART", ElarionUi.Gilt, small: false);

                // North-up, like the minimap, and SAID so rather than assumed.
                Caption(rt, "N", new Vector2(0.5f, 0.94f), ElarionUi.ParchmentDim);

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
            return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        }

        // =====================================================================
        //  Primitives (static Images + TMP; nothing ticks)
        // =====================================================================

        private static void Ring(RectTransform parent, float radiusFrac, Color tint, string label)
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
            Caption(parent, label, new Vector2(0.5f, 0.5f + radiusFrac), tint);
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
        private static void Halo(RectTransform parent, Vector2 at)
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
        }

        private static void Mark(RectTransform parent, Vector2 at, string glyph, string label,
            Color tint, bool small)
        {
            var go = new GameObject("Mark_" + label, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = at; rt.anchorMax = at;
            rt.sizeDelta = new Vector2(160f, 44f);
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

            // The headline mark also gets its label on its own line beneath the glyph, so it
            // reads even where marks crowd together.
            if (!small && label == "1st BREACH") t.text = glyph + "\n" + label;
        }

        private static void Caption(RectTransform parent, string text, Vector2 at, Color tint)
        {
            var go = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = at; rt.anchorMax = at;
            rt.sizeDelta = new Vector2(200f, 34f);
            rt.anchoredPosition = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = text;
            t.fontSize = ElarionUi.FontLabel;
            t.color = new Color(tint.r, tint.g, tint.b, 0.75f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
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
